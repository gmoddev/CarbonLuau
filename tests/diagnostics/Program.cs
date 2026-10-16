using System;
using System.IO;
using System.Runtime.CompilerServices;

internal static class Program
{
    private static void Check(bool Value, [CallerArgumentExpression("Value")] string Expression = "", [CallerFilePath] string File = "", [CallerLineNumber] int Line = 0)
    {
        if (!TestDiagnostics.Pass(Value, Expression, File, Line)) throw new InvalidOperationException("controlled assertion");
    }

    private static int Main(string[] Args)
    {
        if (Args.Length == 1) {
            try {
                if (Args[0] == "assertion") Check(false);
                if (Args[0] == "trace") {
                    for (int Index = 0; Index < 32; Index++) TestDiagnostics.Observe("trace", Index, 99, 5);
                    Check(false);
                }
                if (Args[0] == "sink-failure") {
                    string File = Path.Combine(Path.GetTempPath(), "CarbonLuauDiagnosticSink-" + Guid.NewGuid().ToString("N"));
                    System.IO.File.WriteAllText(File, "owned fixture");
                    try {
                        Environment.SetEnvironmentVariable("CARBONLUAU_DIAGNOSTICS_DIR", File);
                        TestDiagnostics.Run(() => { throw new TimeoutException("primary-secret"); }, () => { throw new IOException("secondary-secret"); });
                    } finally { System.IO.File.Delete(File); }
                }
                if (Args[0] == "timeout") throw new TimeoutException("private-secret-sentinel");
                if (Args[0] == "allocation") throw new OutOfMemoryException("private-secret-sentinel");
                if (Args[0] == "infrastructure") throw new IOException("private-secret-sentinel");
                if (Args[0] == "cleanup") TestDiagnostics.Run(() => { }, () => { throw new IOException("private-secret-sentinel"); });
                if (Args[0] == "primary-cleanup") TestDiagnostics.Run(() => { throw new TimeoutException("primary-secret"); }, () => { throw new IOException("secondary-secret"); });
                return 0;
            } catch (Exception Error) { TestDiagnostics.ExceptionFailure(Error); return 19; }
        }
        int Cleanups = 0;
        var Primary = new TimeoutException("same original status and identity");
        try {
            TestDiagnostics.Run(() => { throw Primary; }, () => { Cleanups++; throw new IOException("secondary"); });
            return 2;
        } catch (TimeoutException Error) { Check(object.ReferenceEquals(Error, Primary)); Check(Error.StackTrace.Contains("Program")); }
        Check(Cleanups == 1);
        var Secondary = new IOException("cleanup only remains failure");
        try { TestDiagnostics.Run(() => { }, () => { throw Secondary; }); return 3; }
        catch (IOException Error) { Check(object.ReferenceEquals(Error, Secondary)); }
        // A real Windows sharing violation must not erase the original failure.
        string Root = Path.Combine(Path.GetTempPath(), "CarbonLuauDiagnostics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        try {
            using (var Locked = new FileStream(Path.Combine(Root, "locked"), FileMode.CreateNew, FileAccess.Write, FileShare.None)) {
                if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
                    try { TestDiagnostics.Run(() => { throw Primary; }, () => Directory.Delete(Root, true)); return 4; }
                    catch (TimeoutException Error) { Check(object.ReferenceEquals(Error, Primary)); }
                    Check(Directory.Exists(Root));
                }
            }
        } finally { Directory.Delete(Root, true); }
        Console.WriteLine("[CarbonLuau:Diagnostics] PASS original exception identity/stack, secondary cleanup, cleanup-only failure, unique temporary ownership");
        return 0;
    }
}
