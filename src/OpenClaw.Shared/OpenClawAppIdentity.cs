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

    public const int ReleaseManagedGatewayPort = 18889;
    public const int DevManagedGatewayPort = 18890;
    public const int ReleaseLocalMcpPort = 18893;
    public const int DevLocalMcpPort = 18894;
    public const string ReleaseManagedGatewayUrl = "ws://127.0.0.1:18889";
    public const string DevManagedGatewayUrl = "ws://127.0.0.1:18890";

    /// <summary>
    /// Smart-Agent v1 owns its WSL Gateway lifecycle. The upstream native package remains
    /// available as source, but its package-wide isolated-session lifecycle is not an
    /// independently attributable Smart-Agent runtime.
    /// </summary>
    public const bool ManagedNativeGatewayEnabled = false;
    public const string ManagedNativeGatewayDeferredMessage =
        "Smart-Agent managed Native Gateway support is deferred until the upstream package provides independent per-consumer lifecycle ownership.";

    /// <summary>
    /// Planned Smart-Agent release ownership. The repository target is approved, but the
    /// repository itself has not yet been created/published and no production signing
    /// identity has been configured. Runtime update ownership therefore remains disabled.
    /// </summary>
    public const string PlannedReleaseRepositoryOwner = "aryanbarak";
    public const string PlannedReleaseRepositoryName = "smart-agent-windows";
    public const string PlannedReleaseRepositorySlug =
        PlannedReleaseRepositoryOwner + "/" + PlannedReleaseRepositoryName;
    public const string PlannedReleaseRepositoryUrl =
        "https://github.com/aryanbarak/smart-agent-windows";
    public const string PlannedReleaseSupportUrl =
        "https://github.com/aryanbarak/smart-agent-windows/issues";
    public const string PlannedReleaseUpdatesUrl =
        "https://github.com/aryanbarak/smart-agent-windows/releases";
    public const string ReleaseChecksumAlgorithm = "SHA-256";
    public const bool ReleaseAuthenticodeRequired = true;
    public const bool ReleaseChecksumManifestRequired = true;

    public static readonly bool ReleaseRepositoryOwnershipConfigured = false;
    public static readonly bool ReleaseSigningIdentityConfigured = false;
    public static readonly bool ReleaseArtifactVerificationConfigured = false;

    public static bool ManagedReleaseUpdaterEnabled =>
        ReleaseRepositoryOwnershipConfigured &&
        ReleaseSigningIdentityConfigured &&
        ReleaseArtifactVerificationConfigured;

    public const string ManagedReleaseUpdaterDeferredMessage =
        "Smart-Agent updates are disabled until repository ownership, release signing, and artifact verification are configured.";

    public static int GetManagedGatewayPort(string? identity) =>
        NormalizeIdentity(identity) == DevIdentity ? DevManagedGatewayPort : ReleaseManagedGatewayPort;

    public static int GetBrowserControlPort(string? identity) => GetManagedGatewayPort(identity) + 2;

    public static bool IsManagedLocalGatewayPort(int port) =>
        port is ReleaseManagedGatewayPort or DevManagedGatewayPort;

    public static int GetLocalMcpPort(string? identity) =>
        NormalizeIdentity(identity) == DevIdentity ? DevLocalMcpPort : ReleaseLocalMcpPort;

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
