using System.Diagnostics;
using System.Xml.Linq;
using OpenClaw.TestSupport;

namespace OpenClaw.Tray.Tests;

public sealed class MigrationBuildConfigurationTests
{
    [Theory]
    [InlineData("win-x64", false)]
    [InlineData("win-x64", true)]
    [InlineData("win-arm64", false)]
    [InlineData("win-arm64", true)]
    public async Task SmartAgent_RejectsOpenClawProductionMigration(string runtime, bool packaged)
    {
        var result = await EvaluateAsync(
            ("MigrationProductionEnabled", "true"),
            ("Version", "2027.1.1"),
            ("RuntimeIdentifier", runtime),
            ("PackageMsix", packaged.ToString()));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Smart-Agent builds cannot enable OpenClaw production state migration", result.Output);
    }

    [Fact]
    public async Task SmartAgent_DefaultsDisableOpenClawMigration()
    {
        var result = await EvaluateAsync(("Version", "2027.1.1"));

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("PRODUCTION_MIGRATION", result.Output);
        Assert.DoesNotContain("MigrationStoreProductId=", result.Output);
        Assert.DoesNotContain("MigrationMinimumSourceVersion=", result.Output);
        Assert.DoesNotContain("MIGRATION_PREVIEW", result.Output);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("1")]
    public async Task MigrationSwitch_RejectsInvalidValues(string value)
    {
        var result = await EvaluateAsync(("MigrationProductionEnabled", value));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("true or false", result.Output);
    }

    [Theory]
    [InlineData("Debug", "false")]
    [InlineData("Debug", "true")]
    [InlineData("Release", "true")]
    [InlineData("Release", "false")]
    public async Task ProductionMigration_IsExcludedFromAllSmartAgentBuilds(
        string configuration, string devBuild)
    {
        var result = await EvaluateAsync(
            ("Configuration", configuration), ("DevBuild", devBuild));

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("PRODUCTION_MIGRATION", result.Output);
        Assert.DoesNotContain("MigrationStoreProductId=", result.Output);
        Assert.DoesNotContain("MigrationMinimumSourceVersion=", result.Output);
    }

    [Theory]
    [InlineData(false, "InnoMigrationPreview", "INNO_MIGRATION_PREVIEW")]
    [InlineData(true, "StoreMigrationPreview", "STORE_MIGRATION_PREVIEW")]
    public async Task ExplicitDebugPreviewsRemainAvailable(bool packaged, string flag, string symbol)
    {
        var result = await EvaluateAsync(
            ("Configuration", "Debug"), ("PackageMsix", packaged.ToString()),
            ("MigrationProductionEnabled", "false"), (flag, "true"),
            ("MigrationPreviewStoreProductId", "9NFPR3BGDRR5"),
            ("StoreMigrationPreviewMinimumSourceVersion", "2026.9.5.0"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains(symbol, result.Output);
        Assert.DoesNotContain("PRODUCTION_MIGRATION", result.Output);
    }

    [Theory]
    [InlineData(false, "InnoMigrationPreview")]
    [InlineData(true, "StoreMigrationPreview")]
    public async Task PreviewFlagsCannotShipInRelease(bool packaged, string flag)
    {
        var result = await EvaluateAsync(
            ("MigrationProductionEnabled", "false"), ("PackageMsix", packaged.ToString()),
            (flag, "true"), ("StoreMigrationPreviewMinimumSourceVersion", "2026.9.5.0"));

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("restricted to Debug", result.Output);
    }

    [Fact]
    public void TrayImportsOneCanonicalMigrationBuildPolicy()
    {
        var directory = Path.Combine(TestRepositoryPaths.GetRepositoryRoot(), "src", "OpenClaw.Tray.WinUI");
        var project = XDocument.Load(Path.Combine(directory, "OpenClaw.Tray.WinUI.csproj"));
        Assert.Single(project.Descendants("Import"), element =>
            (string?)element.Attribute("Project") == "Migration.Build.props");
        var policy = XDocument.Load(Path.Combine(directory, "Migration.Build.props"));
        Assert.Equal("9NFPR3BGDRR5", policy.Descendants("MigrationStoreProductId").Single().Value);
        var minimum = policy.Descendants("MigrationMinimumSourceVersion").Single().Value;
        Assert.Equal("2026.9.5.0", minimum);
        Assert.DoesNotContain("$(Version)", minimum);
        Assert.DoesNotContain("GitVersion", minimum);
        Assert.Equal("false", policy.Descendants("MigrationProductionEnabled").Single().Value);
        Assert.Equal("CoreCompile", policy.Descendants("Target")
            .Single(element => (string?)element.Attribute("Name") == "ValidateMigrationBuild")
            .Attribute("BeforeTargets")!.Value);
    }

    private static async Task<(int ExitCode, string Output)> EvaluateAsync(params (string Key, string Value)[] overrides)
    {
        using var temp = new TempDirectory();
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var properties = new Dictionary<string, string>
        {
            ["Configuration"] = "Release",
            ["RuntimeIdentifier"] = "win-x64",
            ["DevBuild"] = "false",
            ["PackageMsix"] = "false"
        };
        var path = temp.Combine("migration-build.proj");
        new XDocument(new XElement("Project",
            new XElement("PropertyGroup", properties.Select(pair => new XElement(pair.Key, pair.Value))),
            new XElement("Import", new XAttribute("Project",
                Path.Combine(root, "src", "OpenClaw.Tray.WinUI", "Migration.Build.props"))),
            new XElement("Target", new XAttribute("Name", "Probe"),
                new XAttribute("DependsOnTargets", "ValidateMigrationBuild"),
                new XElement("Message", new XAttribute("Importance", "high"),
                    new XAttribute("Text", "MIGRATION-DEFINES=$(DefineConstants)")),
                new XElement("Message", new XAttribute("Importance", "high"),
                    new XAttribute("Text", "@(AssemblyAttribute->'%(_Parameter1)=%(_Parameter2)', '|')")))))
            .Save(path);
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = root,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (var argument in new[] { "msbuild", path, "-t:Probe", "-nologo", "-v:minimal" })
            start.ArgumentList.Add(argument);
        foreach (var (key, value) in overrides)
            start.ArgumentList.Add($"-p:{key}={value}");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start migration build validation.");
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(1));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
            throw new TimeoutException("Migration build validation timed out.");
        }
        return (process.ExitCode, $"{await stdout}\n{await stderr}");
    }
}
