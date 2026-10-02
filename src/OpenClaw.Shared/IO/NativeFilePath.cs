using Microsoft.Win32.SafeHandles;
using System.Runtime.InteropServices;
using System.Text;

namespace OpenClaw.Shared.IO;

/// <summary>
/// Resolves an existing file to the physical path visible to native child processes,
/// including files redirected by MSIX. Callers retain ownership and reparse-point validation.
/// </summary>
public static class NativeFilePath
{
    public static string ResolveFile(string path)
    {
        using SafeFileHandle handle = File.OpenHandle(
            path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return ResolveHandle(handle, path);
    }

    /// <summary>
    /// Resolves the same handle a caller verified, without reopening its possibly redirected path.
    /// Retain the handle when the file identity must remain pinned for subsequent native reads.
    /// </summary>
    public static string ResolveHandle(SafeFileHandle handle, string path)
    {
        ArgumentNullException.ThrowIfNull(handle);
        if (handle.IsInvalid || handle.IsClosed)
            throw new IOException("Cannot resolve a closed or invalid file handle.");

        if (!OperatingSystem.IsWindows())
        {
            FileSystemInfo? resolved = new FileInfo(path).ResolveLinkTarget(returnFinalTarget: true);
            return WindowsPathSafety.NormalizePath(resolved?.FullName ?? path);
        }

        int capacity = 512;
        while (capacity <= 32_768)
        {
            var builder = new StringBuilder(capacity);
            uint length = GetFinalPathNameByHandleW(handle, builder, (uint)builder.Capacity, 0);
            if (length == 0)
                throw new IOException($"Cannot resolve a native file path (Win32 error {Marshal.GetLastWin32Error()}).");
            if (length < builder.Capacity)
                return WindowsPathSafety.NormalizePath(NormalizeFinalPath(builder.ToString()));
            capacity = checked((int)length + 1);
        }

        throw new IOException("A resolved native file path exceeded the supported length.");
    }

    internal static string NormalizeFinalPath(string path)
    {
        const string extendedPrefix = @"\\?\";
        const string extendedUncPrefix = @"\\?\UNC\";
        if (path.StartsWith(extendedUncPrefix, StringComparison.OrdinalIgnoreCase))
            return @"\\" + path[extendedUncPrefix.Length..];

        return path.StartsWith(extendedPrefix, StringComparison.OrdinalIgnoreCase)
            ? path[extendedPrefix.Length..]
            : path;
    }

    [DllImport("kernel32.dll", EntryPoint = "GetFinalPathNameByHandleW", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern uint GetFinalPathNameByHandleW(
        SafeFileHandle file, StringBuilder filePath, uint filePathLength, uint flags);
}
