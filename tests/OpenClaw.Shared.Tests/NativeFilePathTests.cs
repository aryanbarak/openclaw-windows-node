using Microsoft.Win32.SafeHandles;
using OpenClaw.Shared.IO;
using OpenClaw.TestSupport;

namespace OpenClaw.Shared.Tests;

public sealed class NativeFilePathTests
{
    [Fact]
    public void ResolveFile_ReturnsExistingPhysicalFile()
    {
        using var temp = new TempDirectory("native file path ");
        string path = temp.Combine("model file.gguf");
        File.WriteAllText(path, "model");

        string resolved = NativeFilePath.ResolveFile(path);

        Assert.True(Path.IsPathFullyQualified(resolved));
        Assert.False(resolved.StartsWith(@"\\?\", StringComparison.Ordinal));
        Assert.Equal("model", File.ReadAllText(resolved));
        Assert.Equal(resolved, NativeFilePath.ResolveFile(resolved));
    }

    [Fact]
    public void ResolveFile_MissingFileThrowsInsteadOfReturningLogicalPath()
    {
        using var temp = new TempDirectory("native-path-missing-");
        Assert.Throws<FileNotFoundException>(() => NativeFilePath.ResolveFile(temp.Combine("missing.dll")));
    }

    [Fact]
    public void ResolveHandle_DoesNotReopenOrCloseTheVerifiedFile()
    {
        using var temp = new TempDirectory("native-path-handle-");
        string path = temp.Combine("verified.gguf");
        File.WriteAllText(path, "verified");
        using SafeFileHandle handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.None);

        string resolved = NativeFilePath.ResolveHandle(handle, path);

        Assert.True(File.Exists(resolved));
        Assert.False(handle.IsClosed);
        Assert.False(handle.IsInvalid);
    }

    [Fact]
    public void ResolveHandle_InvalidHandleThrows()
    {
        using var handle = new SafeFileHandle(IntPtr.Zero, ownsHandle: false);
        Assert.Throws<IOException>(() => NativeFilePath.ResolveHandle(handle, "unused"));
    }

    [Theory]
    [InlineData(@"\\?\C:\local cache\llama-server.exe", @"C:\local cache\llama-server.exe")]
    [InlineData(@"\\?\UNC\server\share\model.gguf", @"\\server\share\model.gguf")]
    [InlineData(@"C:\models\weights.gguf", @"C:\models\weights.gguf")]
    public void NormalizeFinalPath_PreservesDriveAndUncPaths(string input, string expected) =>
        Assert.Equal(expected, NativeFilePath.NormalizeFinalPath(input));
}
