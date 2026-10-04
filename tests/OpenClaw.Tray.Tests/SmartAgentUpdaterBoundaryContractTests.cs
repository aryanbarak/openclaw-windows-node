using Xunit;

namespace OpenClaw.Tray.Tests;

public sealed class SmartAgentUpdaterBoundaryContractTests
{
    [Fact]
    public void AppComposition_DoesNotConfigureTheUpstreamOpenClawReleaseRepository()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var app = File.ReadAllText(Path.Combine(
            root, "src", "OpenClaw.Tray.WinUI", "App.xaml.cs"));

        Assert.Contains("ManagedReleaseUpdaterEnabled", app);
        Assert.Contains("UpdatumManager? AppUpdater = CreateAppUpdater()", app);
        Assert.DoesNotContain("new(\"openclaw\", \"openclaw-windows-node\")", app);
        Assert.DoesNotContain("github.com/openclaw/openclaw-windows-node", app,
            StringComparison.OrdinalIgnoreCase);

        var workspace = File.ReadAllText(Path.Combine(
            root, "src", "OpenClaw.Tray.WinUI", "Windows", "WorkspaceWindow.xaml.cs"));
        Assert.DoesNotContain("https://docs.openclaw.ai/releases", workspace,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UpdateCoordinator_FailsClosedBeforeAnyUpdatePipelineCall()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var coordinator = File.ReadAllText(Path.Combine(
            root, "src", "OpenClaw.Tray.WinUI", "Services", "UpdateCoordinator.cs"));

        var policyGate = coordinator.IndexOf(
            "if (!OpenClawAppIdentity.ManagedReleaseUpdaterEnabled)",
            StringComparison.Ordinal);
        var pipelineCall = coordinator.IndexOf(
            "UpdateCheckPipeline.CheckAsync(",
            StringComparison.Ordinal);

        Assert.True(policyGate >= 0, "Expected Smart-Agent updater policy gate.");
        Assert.True(pipelineCall > policyGate,
            "Update pipeline must remain unreachable until Smart-Agent release ownership is enabled.");
        Assert.Contains("ManagedReleaseUpdaterDeferredMessage", coordinator);
        Assert.Contains("UpdatumManager? updater", coordinator);
    }

    [Fact]
    public void DownloadPath_RequiresAnOwnedUpdaterRuntime()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var coordinator = File.ReadAllText(Path.Combine(
            root, "src", "OpenClaw.Tray.WinUI", "Services", "UpdateCoordinator.cs"));

        Assert.Contains("var activeUpdater = _updater;", coordinator);
        Assert.Contains("if (activeUpdater is null)", coordinator);
        Assert.Contains(
            "Update download blocked: no Smart-Agent-owned updater runtime is configured",
            coordinator);
        Assert.Contains("activeUpdater.DownloadUpdateAsync()", coordinator);
        Assert.Contains("activeUpdater.InstallUpdateAsync(downloadedAsset)", coordinator);
    }
}
