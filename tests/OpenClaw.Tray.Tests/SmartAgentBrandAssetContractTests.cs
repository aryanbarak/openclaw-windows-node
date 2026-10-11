using System.Security.Cryptography;

namespace OpenClaw.Tray.Tests;

public sealed class SmartAgentBrandAssetContractTests
{
    private const string CanonicalMarkSha256 =
        "2B9150449DC27ED3D39BCBB76ACEF770DA4A48DE2B4D482C2C4B8C8A6315BACB";

    [Fact]
    public void CanonicalMark_MatchesApprovedSmartAgentAsset()
    {
        var path = Path.Combine(
            TestRepositoryPaths.GetRepositoryRoot(),
            "src", "OpenClaw.Tray.WinUI", "Assets", "Brand", "SmartAgentMark.png");

        Assert.True(File.Exists(path), $"Expected Smart-Agent mark at '{path}'.");
        Assert.Equal(
            CanonicalMarkSha256,
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
    }

    [Fact]
    public void WindowsShellAssets_ArePresent()
    {
        var assets = Path.Combine(
            TestRepositoryPaths.GetRepositoryRoot(),
            "src", "OpenClaw.Tray.WinUI", "Assets");
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
    }

    [Fact]
    public void BuildAndInstaller_UseSmartAgentIcon()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var project = File.ReadAllText(Path.Combine(
            root, "src", "OpenClaw.Tray.WinUI", "OpenClaw.Tray.WinUI.csproj"));
        var installer = File.ReadAllText(Path.Combine(root, "installer.iss"));

        Assert.Contains("<ApplicationIcon>Assets\\smart-agent.ico</ApplicationIcon>", project);
        Assert.Contains("<Content Remove=\"Assets\\openclaw.ico\" />", project);
        var statusIcons = File.ReadAllText(Path.Combine(
            root, "src", "OpenClaw.Tray.WinUI", "Helpers", "StatusBadgeIconFactory.cs"));
        Assert.Contains("Path.GetTempPath(), \"SmartAgent\", \"StatusIcons\"", statusIcons);
        Assert.Contains("Path.Combine(AssetsPath, \"smart-agent.ico\")", statusIcons);
        Assert.Contains(
            "SetupIconFile=src\\OpenClaw.Tray.WinUI\\Assets\\smart-agent.ico",
            installer);
    }
}
