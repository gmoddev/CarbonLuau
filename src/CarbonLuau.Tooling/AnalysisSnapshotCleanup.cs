using System.Diagnostics;

namespace CarbonLuau.Tooling;

internal static class AnalysisSnapshotCleanup
{
    // Only the supervisor-generated private directory is passed here, after
    // process/watch disposal. Windows may briefly retain a closing directory
    // or file handle. Retry sharing violations only, never suppress cleanup.
    internal static void Remove(string Root)
    {
        var Elapsed = Stopwatch.StartNew();
        while (Directory.Exists(Root)) {
            try { Directory.Delete(Root, true); return; }
            catch (IOException Error) when (OperatingSystem.IsWindows() &&
                (Error.HResult & 0xffff) is 32 or 33 && Elapsed.ElapsedMilliseconds < 1000) {
                Thread.Sleep(25);
            }
        }
    }
}
