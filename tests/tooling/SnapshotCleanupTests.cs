using CarbonLuau.Tooling;

internal static class SnapshotCleanupTests
{
    internal static void Run()
    {
        string Root = Directory.CreateTempSubdirectory("carbonluau-cleanup-test-").FullName;
        try {
            string FileName = Path.Combine(Root, "owned.txt");
            if (OperatingSystem.IsWindows()) {
                // Real Windows sharing violations, not a mocked delete failure.
                var Held = new FileStream(FileName, FileMode.Create, FileAccess.Write, FileShare.None);
                Task Release = Task.Run(async () => { await Task.Delay(150); Held.Dispose(); });
                try { AnalysisSnapshotCleanup.Remove(Root); } finally { Release.GetAwaiter().GetResult(); Held.Dispose(); }
                if (Directory.Exists(Root)) throw new Exception("Transient snapshot sharing violation was not recovered.");
                Directory.CreateDirectory(Root);
                using (var Persistent = new FileStream(FileName, FileMode.Create, FileAccess.Write, FileShare.None)) {
                    bool Failed = false;
                    var Time = System.Diagnostics.Stopwatch.StartNew();
                    try { AnalysisSnapshotCleanup.Remove(Root); } catch (IOException) { Failed = true; }
                    if (!Failed || !Directory.Exists(Root) || Time.ElapsedMilliseconds > 3000)
                        throw new Exception("Persistent snapshot sharing violation did not fail within its bound.");
                }
            } else File.WriteAllText(FileName, "owned test data");
            AnalysisSnapshotCleanup.Remove(Root);
            if (Directory.Exists(Root)) throw new Exception("Private snapshot cleanup failed.");
        } finally { AnalysisSnapshotCleanup.Remove(Root); }
        Console.WriteLine("[CarbonLuau:SnapshotCleanup] PASS private-directory removal and platform sharing-violation policy");
    }
}
