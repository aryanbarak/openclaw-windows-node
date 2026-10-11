namespace OpenClaw.Tray.Tests;

public sealed class UpdateRepositoryContractTests
{
    [Fact]
    public void AppUpdater_UsesSmartAgentReleaseIdentity()
    {
        var appSource = File.ReadAllText(Path.Combine(
            TestRepositoryPaths.GetRepositoryRoot(),
            "src",
            "OpenClaw.Tray.WinUI",
            "App.xaml.cs"));

        Assert.Contains("OpenClawAppIdentity.UpdateRepositoryOwner", appSource);
        Assert.Contains("OpenClawAppIdentity.UpdateRepositoryName", appSource);
        Assert.Contains("^SmartAgent-Setup-arm64\\\\.exe$", appSource);
        Assert.Contains("^SmartAgent-Setup-x64\\\\.exe$", appSource);
        Assert.Contains("AssetExtensionFilter = \"exe\"", appSource);
        Assert.Contains(
            "InstallUpdateWindowsExeType = UpdatumWindowsExeType.Installer",
            appSource);
        Assert.DoesNotContain(
            "new(\"openclaw\", \"openclaw-windows-node\")",
            appSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsGitHubLink_UsesSmartAgentReleaseIdentity()
    {
        var settingsSource = File.ReadAllText(Path.Combine(
            TestRepositoryPaths.GetRepositoryRoot(),
            "src",
            "OpenClaw.Tray.WinUI",
            "Pages",
            "SettingsPage.xaml.cs"));

        Assert.Contains("OpenClawAppIdentity.UpdateRepositoryUrl", settingsSource);
        Assert.DoesNotContain(
            "https://github.com/openclaw/openclaw-windows-node",
            settingsSource,
            StringComparison.Ordinal);
    }
}
