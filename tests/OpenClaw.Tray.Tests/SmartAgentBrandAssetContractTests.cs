using System.Security.Cryptography;
using Xunit;

namespace OpenClaw.Tray.Tests;

public sealed class SmartAgentBrandAssetContractTests
{
    private const string CanonicalMarkSha256 =
        "2B9150449DC27ED3D39BCBB76ACEF770DA4A48DE2B4D482C2C4B8C8A6315BACB";

    [Fact]
    public void CanonicalMark_IsExactSmartarynEcosystemAsset()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var path = Path.Combine(
            root, "src", "OpenClaw.Tray.WinUI", "Assets", "Brand", "SmartAgentMark.png");

        Assert.True(File.Exists(path), $"Expected canonical Smart-Agent mark at '{path}'.");
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        Assert.Equal(CanonicalMarkSha256, hash);
    }

    [Fact]
    public void ProductionSources_DoNotReferenceUpstreamOpenClawIcon()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var src = Path.Combine(root, "src");
        var sourceExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".xaml", ".xml",
        };

        var sourceFiles = Directory.EnumerateFiles(src, "*", SearchOption.AllDirectories)
            .Where(path => sourceExtensions.Contains(Path.GetExtension(path)))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase));

        foreach (var path in sourceFiles.Append(Path.Combine(root, "installer.iss")))
        {
            var text = File.ReadAllText(path);
            Assert.False(
                text.Contains("openclaw.ico", StringComparison.OrdinalIgnoreCase),
                $"Smart-Agent production source must not reference the upstream icon: {path}");
        }
    }

    [Fact]
    public void WindowsBrandAssets_ArePresentAndNonEmpty()
    {
        var assets = Path.Combine(
            TestRepositoryPaths.GetRepositoryRoot(), "src", "OpenClaw.Tray.WinUI", "Assets");
        var required = new[]
        {
            "smart-agent.ico",
            "LockScreenLogo.png",
            "SplashScreen.png",
            "Square150x150Logo.png",
            "Square44x44Logo.png",
            "Square44x44Logo.targetsize-24_altform-unplated.png",
            "Square44x44Logo.targetsize-32_altform-unplated.png",
            "Square44x44Logo.targetsize-48_altform-unplated.png",
            "Square44x44Logo.targetsize-256_altform-unplated.png",
            "StoreLogo.png",
            "Wide310x150Logo.png",
        };

        foreach (var name in required)
        {
            var path = Path.Combine(assets, name);
            Assert.True(File.Exists(path), $"Missing Smart-Agent brand asset: {name}");
            Assert.True(new FileInfo(path).Length > 0, $"Smart-Agent brand asset is empty: {name}");
        }

        Assert.False(File.Exists(Path.Combine(assets, "openclaw.ico")));
        Assert.False(File.Exists(Path.Combine(assets, "Setup", "OpenClawMascot.png")));
    }

    [Fact]
    public void BuildAndInstallerPayloads_UseSmartAgentAssetsAndExcludeUpstreamBrandArt()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var trayProject = File.ReadAllText(Path.Combine(
            root, "src", "OpenClaw.Tray.WinUI", "OpenClaw.Tray.WinUI.csproj"));
        var setupProject = File.ReadAllText(Path.Combine(
            root, "src", "OpenClaw.SetupEngine.UI", "OpenClaw.SetupEngine.UI.csproj"));
        var installer = File.ReadAllText(Path.Combine(root, "installer.iss"));

        Assert.Contains("<ApplicationIcon>Assets\\smart-agent.ico</ApplicationIcon>", trayProject);
        Assert.Contains(
            "<Content Remove=\"Assets\\openclaw.ico;Assets\\Setup\\OpenClawMascot.png;Assets\\Brand\\README.md\" />",
            trayProject);
        Assert.Contains(
            "<None Remove=\"Assets\\openclaw.ico;Assets\\Setup\\OpenClawMascot.png;Assets\\Brand\\README.md\" />",
            trayProject);
        Assert.Contains(
            "Exclude=\"Assets\\openclaw.ico;Assets\\Setup\\OpenClawMascot.png;Assets\\Brand\\README.md\"",
            trayProject);
        Assert.Contains(
            "Exclude=\"..\\OpenClaw.Tray.WinUI\\Assets\\Setup\\OpenClawMascot.png\"",
            setupProject);
        Assert.Contains(
            "SetupIconFile=src\\OpenClaw.Tray.WinUI\\Assets\\smart-agent.ico",
            installer);
    }
    [Fact]
    public void OnboardingHero_ShowsCanonicalMarkInsteadOfUpstreamCharacter()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root, "src", "OpenClaw.SetupEngine.UI", "Controls", "OnboardingMascot.cs"));

        Assert.Contains("SmartAgentMark.png", source);
        Assert.Contains("Opacity = 0", source);
        Assert.DoesNotContain("OpenClawMascot.png", source);
    }
}