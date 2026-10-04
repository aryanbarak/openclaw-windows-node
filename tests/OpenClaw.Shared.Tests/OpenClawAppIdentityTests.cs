using OpenClaw.Shared;

namespace OpenClaw.Shared.Tests;

public sealed class OpenClawAppIdentityTests
{
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
    public void RuntimeIdentities_AreIsolatedFromOpenClawReleaseAndDevProfiles()
    {
        Assert.Equal("Smart-Agent", OpenClawAppIdentity.ReleaseDisplayName);
        Assert.Equal("Smart-Agent (Dev)", OpenClawAppIdentity.DevDisplayName);
        Assert.Equal("Smart Agent Companion", OpenClawAppIdentity.ReleaseFriendlyDescription);
        Assert.Equal("Smart Agent Companion (Dev)", OpenClawAppIdentity.DevFriendlyDescription);
        Assert.Equal("SmartAgent.Companion", OpenClawAppIdentity.ReleaseAppUserModelId);
        Assert.Equal("SmartAgent.Companion.Dev", OpenClawAppIdentity.DevAppUserModelId);
        Assert.Equal("smartagent", OpenClawAppIdentity.ReleaseProtocolScheme);
        Assert.Equal("smartagent-dev", OpenClawAppIdentity.DevProtocolScheme);
        Assert.Equal("SmartAgentGateway", OpenClawAppIdentity.ReleaseDistroName);
        Assert.Equal("SmartAgentGateway-Dev", OpenClawAppIdentity.DevDistroName);
        Assert.Equal("SmartAgent", OpenClawAppIdentity.ReleaseAutoStartName);
        Assert.Equal("SmartAgent-Dev", OpenClawAppIdentity.DevAutoStartName);
        Assert.Equal("Smart Agent Companion", OpenClawAppIdentity.ReleaseStartupTaskName);
        Assert.Equal("Smart Agent Companion (Dev)", OpenClawAppIdentity.DevStartupTaskName);
        Assert.Equal("SmartAgentStartup", OpenClawAppIdentity.ReleasePackageStartupTaskId);
        Assert.Equal("SmartAgentStartupDev", OpenClawAppIdentity.DevPackageStartupTaskId);
        Assert.Equal("SmartAgent", OpenClawAppIdentity.ReleaseMutexBaseName);
        Assert.Equal("SmartAgent-Dev", OpenClawAppIdentity.DevMutexBaseName);

        var downstreamValues = typeof(OpenClawAppIdentity)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .ToArray();
        Assert.DoesNotContain("OpenClaw.Companion", downstreamValues);
        Assert.DoesNotContain("OpenClaw.Companion.Dev", downstreamValues);
        Assert.DoesNotContain("openclaw-dev", downstreamValues);
        Assert.DoesNotContain("OpenClawGateway", downstreamValues);
        Assert.DoesNotContain("OpenClawGateway-Dev", downstreamValues);
        Assert.DoesNotContain("OpenClawTray", downstreamValues);
        Assert.DoesNotContain("OpenClawTray-Dev", downstreamValues);
    }

    [Fact]
    public void NormalizeIdentity_RejectsUnknownIdentity()
    {
        var ex = Assert.Throws<ArgumentException>(() => OpenClawAppIdentity.NormalizeIdentity("staging"));

        Assert.Contains("release", ex.Message);
        Assert.Contains("dev", ex.Message);
    }
}
