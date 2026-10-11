using OpenClaw.Shared;

namespace OpenClaw.Shared.Tests;

public sealed class OpenClawAppIdentityTests
{
    [Fact]
    public void DefaultRemoteGateway_UsesSmartAgentSecureEndpoint() =>
        Assert.Equal("wss://agent.smartaryn.com", OpenClawAppIdentity.DefaultRemoteGatewayUrl);

    [Fact]
    public void ResolveRoamingDataDirectory_DefaultsToReleaseProfile()
    {
        var root = Path.Combine(Path.GetTempPath(), "openclaw-appdata");
        var path = OpenClawAppIdentity.ResolveRoamingDataDirectory(
            key => key == OpenClawAppIdentity.AppDataRootEnvironmentVariable ? root : null);

        Assert.Equal(Path.Combine(root, "SmartAgent"), path);
    }

    [Fact]
    public void ResolveRoamingDataDirectory_UsesDevProfileFromEnvironment()
    {
        var root = Path.Combine(Path.GetTempPath(), "openclaw-appdata");
        var path = OpenClawAppIdentity.ResolveRoamingDataDirectory(
            key => key switch
            {
                OpenClawAppIdentity.AppDataRootEnvironmentVariable => root,
                OpenClawAppIdentity.IdentityEnvironmentVariable => OpenClawAppIdentity.DevIdentity,
                _ => null
            });

        Assert.Equal(Path.Combine(root, "SmartAgent-Dev"), path);
    }

    [Fact]
    public void ResolveRoamingDataDirectory_ExplicitIdentityWinsOverEnvironment()
    {
        var root = Path.Combine(Path.GetTempPath(), "openclaw-appdata");
        var path = OpenClawAppIdentity.ResolveRoamingDataDirectory(
            key => key switch
            {
                OpenClawAppIdentity.AppDataRootEnvironmentVariable => root,
                OpenClawAppIdentity.IdentityEnvironmentVariable => OpenClawAppIdentity.DevIdentity,
                _ => null
            },
            explicitIdentity: OpenClawAppIdentity.ReleaseIdentity);

        Assert.Equal(Path.Combine(root, "SmartAgent"), path);
    }

    [Fact]
    public void ResolveRoamingDataDirectory_DataDirOverrideWinsOverIdentity()
    {
        var direct = Path.Combine(Path.GetTempPath(), "openclaw-direct-data");
        var path = OpenClawAppIdentity.ResolveRoamingDataDirectory(
            key => key switch
            {
                OpenClawAppIdentity.DataDirectoryOverrideEnvironmentVariable => direct,
                OpenClawAppIdentity.IdentityEnvironmentVariable => OpenClawAppIdentity.DevIdentity,
                _ => null
            });

        Assert.Equal(direct, path);
    }

    [Fact]
    public void ResolveSettingsAndTokenPaths_UseSelectedProfile()
    {
        var root = Path.Combine(Path.GetTempPath(), "openclaw-appdata");
        Func<string, string?> env = key =>
            key == OpenClawAppIdentity.AppDataRootEnvironmentVariable ? root : null;

        Assert.Equal(
            Path.Combine(root, "SmartAgent-Dev", "settings.json"),
            OpenClawAppIdentity.ResolveSettingsPath(env, OpenClawAppIdentity.DevIdentity));
        Assert.Equal(
            Path.Combine(root, "SmartAgent-Dev", "mcp-token.txt"),
            OpenClawAppIdentity.ResolveMcpTokenPath(env, OpenClawAppIdentity.DevIdentity));
    }

    [Fact]
    public void RuntimeIdentity_IsDistinctFromOfficialOpenClaw()
    {
        Assert.Equal("Smart-Agent", OpenClawAppIdentity.ReleaseDisplayName);
        Assert.Equal("SmartAgent", OpenClawAppIdentity.ReleaseDataDirectoryName);
        Assert.Equal("SmartAgent.Companion", OpenClawAppIdentity.ReleaseAppUserModelId);
        Assert.Equal("smartagent", OpenClawAppIdentity.ReleaseProtocolScheme);
        Assert.Equal("SmartAgentGateway", OpenClawAppIdentity.ReleaseDistroName);
        Assert.Equal(18889, OpenClawAppIdentity.ReleaseGatewayPort);
        Assert.Equal("ws://127.0.0.1:18889", OpenClawAppIdentity.ReleaseGatewayUrl);
        Assert.Equal("SmartAgent", OpenClawAppIdentity.ReleaseMutexBaseName);
        Assert.Equal("aryanbarak", OpenClawAppIdentity.UpdateRepositoryOwner);
        Assert.Equal("smart-agent-windows-releases", OpenClawAppIdentity.UpdateRepositoryName);
        Assert.Equal(
            "https://github.com/aryanbarak/smart-agent-windows-releases",
            OpenClawAppIdentity.UpdateRepositoryUrl);
    }

    [Fact]
    public void NormalizeIdentity_RejectsUnknownIdentity()
    {
        var ex = Assert.Throws<ArgumentException>(() => OpenClawAppIdentity.NormalizeIdentity("staging"));

        Assert.Contains("release", ex.Message);
        Assert.Contains("dev", ex.Message);
    }
}
