using OpenClaw.Shared;
using Xunit;

namespace OpenClaw.Tray.Tests;

public sealed class SmartAgentReleaseOwnershipContractTests
{
    [Fact]
    public void PlannedRepository_IsSmartAgentOwnedButNotYetActivated()
    {
        Assert.Equal("aryanbarak/smart-agent-windows", OpenClawAppIdentity.PlannedReleaseRepositorySlug);
        Assert.False(OpenClawAppIdentity.ReleaseRepositoryOwnershipConfigured);
        Assert.False(OpenClawAppIdentity.ReleaseSigningIdentityConfigured);
        Assert.False(OpenClawAppIdentity.ReleaseArtifactVerificationConfigured);
        Assert.False(OpenClawAppIdentity.ManagedReleaseUpdaterEnabled);
        Assert.True(OpenClawAppIdentity.ReleaseAuthenticodeRequired);
        Assert.True(OpenClawAppIdentity.ReleaseChecksumManifestRequired);
        Assert.Equal("SHA-256", OpenClawAppIdentity.ReleaseChecksumAlgorithm);
    }

    [Fact]
    public void Installer_DoesNotAdvertiseUpstreamOrUnownedReleaseEndpoints()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var installer = File.ReadAllText(Path.Combine(root, "installer.iss"));

        Assert.DoesNotContain("AppPublisherURL=", installer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AppSupportURL=", installer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AppUpdatesURL=", installer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github.com/openclaw/openclaw-windows-node/releases", installer,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github.com/openclaw/openclaw-windows-node/issues", installer,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductRepositoryLinks_AreHiddenUntilOwnershipIsConfigured()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var settings = File.ReadAllText(Path.Combine(
            root, "src", "OpenClaw.Tray.WinUI", "Pages", "SettingsPage.xaml.cs"));
        var workspace = File.ReadAllText(Path.Combine(
            root, "src", "OpenClaw.Tray.WinUI", "Windows", "WorkspaceWindow.xaml.cs"));

        Assert.Contains("ReleaseRepositoryOwnershipConfigured", settings);
        Assert.Contains("PlannedReleaseRepositoryUrl", settings);
        Assert.Contains("ReleaseRepositoryOwnershipConfigured", workspace);
        Assert.Contains("PlannedReleaseSupportUrl", workspace);
        Assert.Contains("PlannedReleaseRepositoryUrl", workspace);
        Assert.DoesNotContain("github.com/openclaw/openclaw-windows-node", settings,
            StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("github.com/openclaw/openclaw-windows-node", workspace,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void LegacyReleaseWorkflow_IsExplicitlyRestrictedToUpstreamRepository()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(root, ".github", "workflows", "ci.yml"));
        const string guard = "github.repository == 'openclaw/openclaw-windows-node'";

        var prepareIndex = workflow.IndexOf("  prepare-release-assets:", StringComparison.Ordinal);
        var releaseIndex = workflow.IndexOf("  release:\n", StringComparison.Ordinal);
        Assert.True(prepareIndex >= 0);
        Assert.True(releaseIndex > prepareIndex);

        var prepareIf = workflow.IndexOf(guard, prepareIndex, StringComparison.Ordinal);
        var releaseIf = workflow.IndexOf(guard, releaseIndex, StringComparison.Ordinal);
        Assert.True(prepareIf > prepareIndex && prepareIf < releaseIndex);
        Assert.True(releaseIf > releaseIndex);
    }
}
