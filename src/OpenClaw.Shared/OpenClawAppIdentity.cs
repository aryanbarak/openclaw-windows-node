namespace OpenClaw.Shared;

/// <summary>
/// Shared profile/path rules for standalone tools that need to find the same
/// release or dev profile used by the tray.
/// </summary>
public static class OpenClawAppIdentity
{
    public const string ReleaseIdentity = "release";
    public const string DevIdentity = "dev";
    public const string IdentityEnvironmentVariable = "OPENCLAW_APP_IDENTITY";
    public const string DataDirectoryOverrideEnvironmentVariable = "OPENCLAW_TRAY_DATA_DIR";
    public const string AppDataRootEnvironmentVariable = "OPENCLAW_TRAY_APPDATA_DIR";
    public const string ReleaseDataDirectoryName = "SmartAgent";
    public const string DevDataDirectoryName = "SmartAgent-Dev";

    public const string ReleaseDisplayName = "Smart-Agent";
    public const string DevDisplayName = "Smart-Agent (Dev)";
    public const string ReleaseFriendlyDescription = "Smart Agent Companion";
    public const string DevFriendlyDescription = "Smart Agent Companion (Dev)";
    public const string ReleaseAppUserModelId = "SmartAgent.Companion";
    public const string DevAppUserModelId = "SmartAgent.Companion.Dev";
    public const string ReleaseProtocolScheme = "smartagent";
    public const string DevProtocolScheme = "smartagent-dev";
    public const string ReleaseDistroName = "SmartAgentGateway";
    public const string DevDistroName = "SmartAgentGateway-Dev";
    public const string ReleaseAutoStartName = "SmartAgent";
    public const string DevAutoStartName = "SmartAgent-Dev";
    public const string ReleaseStartupTaskName = "Smart Agent Companion";
    public const string DevStartupTaskName = "Smart Agent Companion (Dev)";
    public const string ReleasePackageStartupTaskId = "SmartAgentStartup";
    public const string DevPackageStartupTaskId = "SmartAgentStartupDev";
    public const string ReleaseMutexBaseName = "SmartAgent";
    public const string DevMutexBaseName = "SmartAgent-Dev";

    public static string NormalizeIdentity(string? identity)
    {
        if (string.IsNullOrWhiteSpace(identity))
            return ReleaseIdentity;

        if (string.Equals(identity, ReleaseIdentity, StringComparison.OrdinalIgnoreCase))
            return ReleaseIdentity;

        if (string.Equals(identity, DevIdentity, StringComparison.OrdinalIgnoreCase))
            return DevIdentity;

        throw new ArgumentException(
            $"App identity must be '{ReleaseIdentity}' or '{DevIdentity}' (got '{identity}').",
            nameof(identity));
    }

    public static string ResolveIdentity(Func<string, string?> envLookup, string? explicitIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(envLookup);

        return NormalizeIdentity(
            !string.IsNullOrWhiteSpace(explicitIdentity)
                ? explicitIdentity
                : envLookup(IdentityEnvironmentVariable));
    }

    public static string GetDataDirectoryName(string? identity) =>
        NormalizeIdentity(identity) == DevIdentity
            ? DevDataDirectoryName
            : ReleaseDataDirectoryName;

    public static string ResolveRoamingDataDirectory(
        Func<string, string?> envLookup,
        string? explicitIdentity = null)
    {
        ArgumentNullException.ThrowIfNull(envLookup);

        var dataDirOverride = envLookup(DataDirectoryOverrideEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(dataDirOverride))
            return dataDirOverride!;

        var root = envLookup(AppDataRootEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(root))
            root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        return Path.Combine(
            root!,
            GetDataDirectoryName(ResolveIdentity(envLookup, explicitIdentity)));
    }

    public static string ResolveSettingsPath(
        Func<string, string?> envLookup,
        string? explicitIdentity = null) =>
        Path.Combine(ResolveRoamingDataDirectory(envLookup, explicitIdentity), "settings.json");

    public static string ResolveMcpTokenPath(
        Func<string, string?> envLookup,
        string? explicitIdentity = null) =>
        Path.Combine(ResolveRoamingDataDirectory(envLookup, explicitIdentity), "mcp-token.txt");
}
