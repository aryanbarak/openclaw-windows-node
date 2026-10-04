namespace OpenClaw.Tray.Tests;

public sealed class SmartAgentInstallReadinessContractTests
{
    [Fact]
    public void InnoReleaseIdentity_IsSideBySideDistinctFromOpenClaw()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var installer = File.ReadAllText(Path.Combine(root, "installer.iss"));

        Assert.Contains("#define MyAppName \"Smart-Agent\"", installer);
        Assert.Contains("#define MyAppAumid \"SmartAgent.Companion\"", installer);
        Assert.Contains("#define MyInstallDir \"SmartAgent\"", installer);
        Assert.Contains("#define MyMutex \"SmartAgent\"", installer);
        Assert.Contains("#define MyDistroName \"SmartAgentGateway\"", installer);
        Assert.Contains("#define MyProtocol \"smartagent\"", installer);
        Assert.Contains("#define MyStoreMigrationEnabled 0", installer);
        Assert.DoesNotContain("AppUpdatesURL=", installer);
        Assert.DoesNotContain("AppSupportURL=", installer);
        Assert.DoesNotContain("AppPublisherURL=", installer);
    }

    [Fact]
    public void RuntimePolicy_RemainsFailClosedBeforeFirstInstall()
    {
        Assert.False(OpenClaw.Shared.OpenClawAppIdentity.ManagedNativeGatewayEnabled);
        Assert.False(OpenClaw.Shared.OpenClawAppIdentity.ManagedReleaseUpdaterEnabled);
        Assert.Equal(18889, OpenClaw.Shared.OpenClawAppIdentity.ReleaseManagedGatewayPort);
        Assert.Equal(18893, OpenClaw.Shared.OpenClawAppIdentity.ReleaseLocalMcpPort);
        Assert.Equal("SmartAgentGateway", OpenClaw.Shared.OpenClawAppIdentity.ReleaseDistroName);
        Assert.Equal("SmartAgent", OpenClaw.Shared.OpenClawAppIdentity.ReleaseDataDirectoryName);
        Assert.Equal("smartagent", OpenClaw.Shared.OpenClawAppIdentity.ReleaseProtocolScheme);
    }

    [Fact]
    public void FunctionalUiErrorPath_CannotWriteToOpenClawTrayState()
    {
        var root = TestRepositoryPaths.GetRepositoryRoot();
        var functionalUi = File.ReadAllText(Path.Combine(
            root, "src", "OpenClawTray.FunctionalUI", "FunctionalUI.cs"));

        Assert.Contains("OpenClawAppIdentity.ResolveLocalDataDirectory", functionalUi);
        Assert.DoesNotContain("\"OpenClawTray\"", functionalUi);
    }
}