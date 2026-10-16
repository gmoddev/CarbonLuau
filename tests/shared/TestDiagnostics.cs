// Test assemblies only. Never linked into the provider or its shipped payload.
using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;

#if NETFRAMEWORK
namespace System.Runtime.CompilerServices
{
    [AttributeUsage(AttributeTargets.Parameter)]
    internal sealed class CallerArgumentExpressionAttribute : Attribute
    {
        public CallerArgumentExpressionAttribute(string ParameterName) { }
    }
}
#endif

internal static class TestDiagnostics
{
    private static readonly object Gate = new object();
    private static int Count;
    private static int Observations;
    private struct Observation { public string Phase, File; public int Line; public long Actual, Expected, Status; }
    private static readonly Observation[] Recent = new Observation[24];
    private static int Next, Saved;
    private static bool Flushed;
    private static readonly string Identity = Guid.NewGuid().ToString("N");

    // The expression is hashed, never evaluated again or serialized as source.
    public static bool Pass(bool Value, string Expression, string File, int Line)
    {
        if (!Value) Write("assertion", File, Line, Digest(Expression), "", 0, 1);
        return Value;
    }

    public static string Digest(string Value)
    {
        try {
            using (var Hash = SHA256.Create())
                return BitConverter.ToString(Hash.ComputeHash(Encoding.UTF8.GetBytes(Value ?? ""))).Replace("-", "").ToLowerInvariant();
        } catch (Exception) { return ""; }
    }

    public static void ExceptionFailure(Exception Error, bool Cleanup = false)
    {
        string Category = Cleanup ? "cleanup" : Error is TimeoutException ? "timeout" :
            Error is OutOfMemoryException ? "allocation" : Error is IOException || Error is System.ComponentModel.Win32Exception ? "infrastructure" : "exception";
        // Exception messages can contain user scripts, paths or secrets.
        try {
            long Status = 0;
            if (Error.GetType().Name == "NativeOperationException") {
                var Field = Error.GetType().GetField("Status", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                if (Field != null) Status = Convert.ToInt64(Field.GetValue(Error), CultureInfo.InvariantCulture);
                if (!Cleanup && Status == 5) Category = "timeout";
                if (!Cleanup && Status == 4) Category = "allocation";
            }
            Write(Category, "", 0, Digest(Error.ToString()), Digest(Error.GetType().FullName), Error.HResult, 0, Status, Error.GetType().FullName);
        } catch (Exception) { Write(Category, "", 0, "", "", 0, 0); }
    }

    public static void Observe(string Phase, long Actual, long Expected, long Status = 0,
        [CallerFilePath] string File = "", [CallerLineNumber] int Line = 0)
    {
        // Phase is hashed too: callers cannot accidentally publish arbitrary text.
        lock (Gate) {
            Recent[Next] = new Observation { Phase = Phase, File = File, Line = Line, Actual = Actual, Expected = Expected, Status = Status };
            Next = (Next + 1) % Recent.Length;
            if (Saved < Recent.Length) Saved++;
        }
    }

    public static void Run(Action Body, Action Cleanup)
    {
        ExceptionDispatchInfo Primary = null;
        try { Body(); }
        catch (Exception Error) { Primary = ExceptionDispatchInfo.Capture(Error); ExceptionFailure(Error); }
        try { Cleanup(); }
        catch (Exception Error) {
            ExceptionFailure(Error, true);
            if (Primary == null) throw;
        }
        if (Primary != null) Primary.Throw();
    }

    public static void Cleanup(Action Action, Exception Primary)
    {
        try { Action(); }
        catch (Exception Error) {
            ExceptionFailure(Error, true);
            if (Primary == null) throw;
        }
    }

    private static void Write(string Category, string File, int Line, string Evidence, string Type,
        long Actual, long Expected, long Status = 0, string ExceptionType = "")
    {
        // Bounded, primitive-only records. Failure of the sink never replaces a test failure.
        try {
            lock (Gate) {
                if (Category != "observation" && !Flushed) {
                    Flushed = true;
                    for (int Index = 0; Index < Saved; Index++) {
                        var Value = Recent[(Next - Saved + Index + Recent.Length) % Recent.Length];
                        Write("observation", Value.File, Value.Line, Digest(Value.Phase), "", Value.Actual, Value.Expected, Value.Status);
                    }
                }
                if (Category == "observation") { if (Observations++ >= 24) return; }
                else if (Count++ >= 40) return;
                string Source = Path.GetFileName(File ?? "");
                foreach (char Value in Source) if (!char.IsLetterOrDigit(Value) && Value != '.' && Value != '_' && Value != '-') { Source = ""; break; }
                if (ExceptionType.Length > 192) ExceptionType = "";
                foreach (char Value in ExceptionType) if (!((Value >= 'A' && Value <= 'Z') || (Value >= 'a' && Value <= 'z') || Value == '.' || Value == '+' || Value == '`' || (Value >= '0' && Value <= '9'))) { ExceptionType = ""; break; }
                string Record = "{\"schema\":1,\"category\":\"" + Category + "\",\"sourceDigest\":\"" + Digest(File) +
                    "\",\"source\":\"" + Source + "\",\"line\":" + Line + ",\"evidenceDigest\":\"" + Evidence + "\",\"typeDigest\":\"" + Type +
                    "\",\"actual\":" + Actual.ToString(CultureInfo.InvariantCulture) + ",\"expected\":" + Expected.ToString(CultureInfo.InvariantCulture) +
                    ",\"status\":" + Status.ToString(CultureInfo.InvariantCulture) + ",\"exceptionType\":\"" + ExceptionType + "\"}";
                Console.Error.WriteLine("[CarbonLuau:Diagnostic] " + Record);
                string Root = Environment.GetEnvironmentVariable("CARBONLUAU_DIAGNOSTICS_DIR");
                if (!string.IsNullOrEmpty(Root)) {
                    Directory.CreateDirectory(Root);
                    System.IO.File.AppendAllText(Path.Combine(Root, "managed-" + Identity + ".jsonl"), Record + "\n");
                }
            }
        } catch (Exception) {
            try { Console.Error.WriteLine("[CarbonLuau:Diagnostic] sink-unavailable"); } catch (Exception) { }
        }
    }
}
