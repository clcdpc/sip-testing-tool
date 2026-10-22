namespace Clc.Sip.Desktop.Tests.TestSupport;

internal sealed class ScratchDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sip-desktop-tests-" + Guid.NewGuid().ToString("N"));
    public ScratchDirectory() => Directory.CreateDirectory(Path);
    public void Dispose()
    {
        var resolved = System.IO.Path.GetFullPath(Path);
        var temporaryRoot = System.IO.Path.GetFullPath(System.IO.Path.GetTempPath()).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
        if (!resolved.StartsWith(temporaryRoot, StringComparison.OrdinalIgnoreCase)
            || !System.IO.Path.GetFileName(resolved).StartsWith("sip-desktop-tests-", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Unexpected test directory.");
        }

        Directory.Delete(resolved, true);
    }
}
