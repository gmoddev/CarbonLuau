using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Off-owner-thread process/transport owner. Contains no VM/native delegates.
        internal sealed class StorageProcess : IDisposable
        {
            internal const int MaximumFrame = 68 * 1024;
            private Process Child;
            private IntPtr Job;
            private readonly Func<bool> Stopping;
            private IAsyncResult Outstanding;
            private bool OutstandingWrite;
            private Task OutstandingFlush;
            internal sealed class StartupFailure : IOException {
                internal readonly uint Code;
                internal StartupFailure(uint Code) : base("storage startup rejected") { this.Code=Code; }
            }
            internal static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
            internal StorageProcess(Func<bool> Stopping) { this.Stopping = Stopping; }
            internal static ulong Now
            {
                get {
                    if (Environment.OSVersion.Platform == PlatformID.Win32NT) return GetTickCount64();
                    Timespec Time;
                    if (clock_gettime(1, out Time) != 0 || Time.Seconds < 0) throw new IOException("monotonic clock unavailable");
                    return checked((ulong)Time.Seconds * 1000 + (ulong)Time.Nanoseconds / 1000000);
                }
            }
            internal void Start(string Executable, string Directory)
            {
                if (Child != null) throw new InvalidOperationException("worker already owned");
                ulong End = Now + 30000;
                var Start = new ProcessStartInfo(Executable, Process.GetCurrentProcess().Id.ToString(System.Globalization.CultureInfo.InvariantCulture)) {
                    UseShellExecute = false, CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden, RedirectStandardInput = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    WorkingDirectory = Path.GetDirectoryName(Executable)
                };
                // The worker needs no environment credentials or dynamic library path.
                Start.EnvironmentVariables.Clear();
                if (Environment.OSVersion.Platform == PlatformID.Win32NT) {
                    string SystemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                    Start.EnvironmentVariables["SystemRoot"] = SystemRoot;
                    Job = CreateJobObjectW(IntPtr.Zero, null);
                    if (Job == IntPtr.Zero) throw new IOException("worker job unavailable");
                    var Limits = new JobLimits();
                    Limits.Basic.Flags = 0x2000 | 0x100; // KILL_ON_JOB_CLOSE | PROCESS_MEMORY
                    Limits.Memory = new UIntPtr(256u * 1024 * 1024);
                    if (!SetInformationJobObject(Job, 9, ref Limits, (uint)Marshal.SizeOf(typeof(JobLimits)))) throw new IOException("worker limit unavailable");
                }
                Child = Process.Start(Start);
                if (Child == null) throw new IOException("worker launch failed");
                if (Job != IntPtr.Zero && !AssignProcessToJobObject(Job, Child.Handle)) throw new IOException("worker job assignment failed");
                byte[] Init;
                using (var Stream = new MemoryStream()) using (var Writer = new BinaryWriter(Stream)) {
                    Writer.Write(new byte[] {67,76,80,73}); Writer.Write(1u);
                    byte[] PathBytes = Utf8.GetBytes(Directory); if (PathBytes.Length > 32768) throw new IOException("storage path bound");
                    Writer.Write((uint)PathBytes.Length); Writer.Write(PathBytes); Init = Stream.ToArray();
                }
                Write(Init, End);
                ValidateReady(Read(End));
            }
            private static void ValidateReady(byte[] Ready)
            {
                if (Ready.Length != 12 || Encoding.ASCII.GetString(Ready,0,4) != "CLPR" || U32(Ready,4) != 1 || U32(Ready,8)>10)
                    throw new IOException("storage startup rejected");
                if (U32(Ready,8)!=0) throw new StartupFailure(U32(Ready,8));
            }
            internal byte[] Exchange(byte[] Request, ulong End)
            { Write(Request, End); return Read(End); }
            private void Await(IAsyncResult Operation, ulong End)
            {
                while (!Operation.IsCompleted) {
                    if (Stopping() || Now >= End) throw new TimeoutException("storage transport deadline");
                    Operation.AsyncWaitHandle.WaitOne(10);
                }
                if (Now >= End) throw new TimeoutException("storage transport deadline");
            }
            private void Write(byte[] Bytes, ulong End)
            {
                if (Bytes.Length < 8 || Bytes.Length > MaximumFrame - 4) throw new IOException("frame bound");
                byte[] Frame = new byte[Bytes.Length + 4]; Put32(Frame,0,(uint)Bytes.Length); Buffer.BlockCopy(Bytes,0,Frame,4,Bytes.Length);
                Stream Output = Child.StandardInput.BaseStream;
                IAsyncResult Pending = Output.BeginWrite(Frame,0,Frame.Length,null,null); Outstanding=Pending; OutstandingWrite=true;
                Await(Pending,End);
                try { Output.EndWrite(Pending); } finally { Pending.AsyncWaitHandle.Close(); Outstanding=null; }
                // Process.StandardInput's FileStream may buffer small frames.
                // Flush off-thread, under the same immutable request deadline.
                // Framework FileStream.FlushAsync also invokes FlushFileBuffers:
                // on a pipe that waits for peer consumption and can fail after a
                // peer has already sent its final reply. Flush only our managed
                // buffer, off-thread and under the same transport deadline.
                OutstandingFlush=Task.Run(() => Output.Flush());
                Await(OutstandingFlush,End);
                try { OutstandingFlush.GetAwaiter().GetResult(); }
                finally { OutstandingFlush.Dispose(); OutstandingFlush=null; }
            }
            private void ReadBytes(byte[] Bytes, ulong End)
            {
                int Offset = 0; Stream Input = Child.StandardOutput.BaseStream;
                while (Offset < Bytes.Length) {
                    IAsyncResult Pending = Input.BeginRead(Bytes,Offset,Bytes.Length-Offset,null,null); Outstanding=Pending; OutstandingWrite=false;
                    Await(Pending,End); int Count;
                    try { Count = Input.EndRead(Pending); } finally { Pending.AsyncWaitHandle.Close(); Outstanding=null; }
                    if (Count <= 0) throw new IOException("worker response ended"); Offset += Count;
                }
            }
            private byte[] Read(ulong End)
            {
                byte[] Header = new byte[4]; ReadBytes(Header,End); uint Length = U32(Header,0);
                if (Length < 8 || Length > MaximumFrame - 4) throw new IOException("response frame bound");
                byte[] Result = new byte[Length]; ReadBytes(Result,End); return Result;
            }
            // Called only off the owner thread. False means no replacement is safe.
            internal bool Stop()
            {
                if (Child == null) { CloseJob(); return true; }
                try {
                    if (Outstanding!=null || OutstandingFlush!=null) { try { Child.Kill(); } catch (Exception) { } }
                    else { try { Child.StandardInput.Close(); } catch (Exception) { } }
                    if (!Child.WaitForExit(1000)) { try { Child.Kill(); } catch (Exception) { } }
                    if (!Child.WaitForExit(1000)) return false;
                    if (OutstandingFlush!=null) {
                        try { if (!OutstandingFlush.Wait(1000)) return false; } catch (AggregateException) { }
                        OutstandingFlush.Dispose(); OutstandingFlush=null;
                    }
                    if (Outstanding!=null) {
                        if (!Outstanding.IsCompleted && !Outstanding.AsyncWaitHandle.WaitOne(1000)) return false;
                        try { if (OutstandingWrite) Child.StandardInput.BaseStream.EndWrite(Outstanding); else Child.StandardOutput.BaseStream.EndRead(Outstanding); }
                        catch (Exception) { }
                        Outstanding.AsyncWaitHandle.Close(); Outstanding=null;
                    }
                    Child.Dispose(); Child = null; CloseJob(); return true;
                } catch (Exception) { return false; }
            }
            private void CloseJob() { if (Job != IntPtr.Zero) { CloseHandle(Job); Job = IntPtr.Zero; } }
            public void Dispose() { Stop(); }
            internal static uint U32(byte[] Bytes, int Offset)
            { return (uint)(Bytes[Offset] | Bytes[Offset+1]<<8 | Bytes[Offset+2]<<16 | Bytes[Offset+3]<<24); }
            internal static ulong U64(byte[] Bytes,int Offset) { return U32(Bytes,Offset) | (ulong)U32(Bytes,Offset+4)<<32; }
            internal static void Put32(byte[] Bytes,int Offset,uint Value)
            { for (int Index=0; Index<4; ++Index) Bytes[Offset+Index]=(byte)(Value>>(Index*8)); }
            [StructLayout(LayoutKind.Sequential)] private struct Timespec { internal long Seconds, Nanoseconds; }
            [StructLayout(LayoutKind.Sequential)] private struct BasicLimits {
                internal long PerProcess, PerJob; internal uint Flags;
                internal UIntPtr Minimum, Maximum; internal uint Active; internal UIntPtr Affinity; internal uint Priority, Scheduling;
            }
            [StructLayout(LayoutKind.Sequential)] private struct IoCounters { internal ulong Read, Write, Other, ReadBytes, WriteBytes, OtherBytes; }
            [StructLayout(LayoutKind.Sequential)] private struct JobLimits {
                internal BasicLimits Basic; internal IoCounters Io; internal UIntPtr Memory, JobMemory, Peak, JobPeak;
            }
            [DllImport("kernel32.dll")] private static extern ulong GetTickCount64();
            [DllImport("libc", SetLastError=true)] private static extern int clock_gettime(int Clock,out Timespec Time);
            [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] private static extern IntPtr CreateJobObjectW(IntPtr Security,string Name);
            [DllImport("kernel32.dll", SetLastError=true)] private static extern bool SetInformationJobObject(IntPtr Job,int Class,ref JobLimits Limits,uint Size);
            [DllImport("kernel32.dll", SetLastError=true)] private static extern bool AssignProcessToJobObject(IntPtr Job,IntPtr Process);
            [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr Handle);
        }
    }
}
