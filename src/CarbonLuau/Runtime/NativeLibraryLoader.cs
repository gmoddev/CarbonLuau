using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Owned and called exclusively by Carbon's main-thread lifecycle.
        public sealed class NativeLibraryLoader : IDisposable
        {
            private IntPtr Handle;
            private ProbeDelegate Probe;
            private bool Attempted;
            private bool Disposed;
            public bool Available { get; private set; }
            public string Rid { get; private set; }
            public string LibraryPath { get; private set; }
            public string UnloadError { get; private set; }

            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            private delegate int ProbeDelegate();

            public static string GetRid(PlatformID Platform, Architecture ProcessArchitecture)
            {
                if (ProcessArchitecture != Architecture.X64)
                    throw new PlatformNotSupportedException("unsupported platform: requires an x64 process");
                if (Platform == PlatformID.Win32NT) return "win-x64";
                if (Platform == PlatformID.Unix && RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux-x64";
                throw new PlatformNotSupportedException("unsupported platform: Windows x64 and Linux x64 only");
            }

            public static string GetLibraryPath(string DataDirectory, string PlatformRid)
            {
                string FileName;
                if (PlatformRid == "win-x64") FileName = "carbonluau_native.dll";
                else if (PlatformRid == "linux-x64") FileName = "libcarbonluau_native.so";
                else throw new PlatformNotSupportedException("unsupported platform RID");
                if (string.IsNullOrWhiteSpace(DataDirectory)) throw new ArgumentException("Carbon data directory is required");
                return Path.GetFullPath(Path.Combine(DataDirectory, "CarbonLuau", "native", PlatformRid, FileName));
            }

            public void Load(string DataDirectory)
            {
                if (Disposed) throw new ObjectDisposedException("NativeLibraryLoader");
                if (Attempted) throw new InvalidOperationException("native load already attempted for this instance");
                Attempted = true;
                Rid = GetRid(Environment.OSVersion.Platform, RuntimeInformation.ProcessArchitecture);
                LibraryPath = GetLibraryPath(DataDirectory, Rid);
                if (!File.Exists(LibraryPath)) throw new FileNotFoundException("native library missing: " + LibraryPath);
                try
                {
                    Handle = Rid == "win-x64" ? LoadLibraryW(LibraryPath) : dlopen(LibraryPath, 2 /* RTLD_NOW | RTLD_LOCAL */);
                    if (Handle == IntPtr.Zero) throw new InvalidOperationException("native library load failed: " + GetLoaderError());
                    if (Rid == "linux-x64") dlerror(); // Clear the thread-local error before dlsym.
                    IntPtr Symbol = Rid == "win-x64" ? GetProcAddress(Handle, "carbonluau_probe") : dlsym(Handle, "carbonluau_probe");
                    string SymbolError = Rid == "linux-x64" ? ReadDlError() : null;
                    if (Symbol == IntPtr.Zero || SymbolError != null)
                        throw new InvalidOperationException("symbol missing: carbonluau_probe; " + (SymbolError ?? GetLoaderError()));
                    int Value;
                    try
                    {
                        Probe = (ProbeDelegate)Marshal.GetDelegateForFunctionPointer(Symbol, typeof(ProbeDelegate));
                        Value = Probe();
                    }
                    catch (Exception Error) { throw new InvalidOperationException("probe invocation failed: " + Error.Message, Error); }
                    if (Value != 0x4C554155)
                        throw new InvalidOperationException("probe magic mismatch: expected 0x4C554155, received 0x" + Value.ToString("X8"));
                    Available = true;
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            public void Dispose()
            {
                if (Disposed) return;
                Disposed = true;
                Available = false;
                Probe = null;
                IntPtr OwnedHandle = Handle;
                Handle = IntPtr.Zero;
                if (OwnedHandle == IntPtr.Zero) return;
                try
                {
                    bool Released = Rid == "win-x64" ? FreeLibrary(OwnedHandle) : dlclose(OwnedHandle) == 0;
                    if (!Released) UnloadError = "native library unload failed: " + GetLoaderError();
                }
                catch (Exception Error) { UnloadError = "native library unload failed: " + Error.Message; }
            }

            // Runtime bindings use the same explicit loaded module; no alternate
            // probing, DllImport search, or direct Luau exports are introduced.
            public T Bind<T>(string Name) where T : class
            {
                if (!Available || Disposed) throw new ObjectDisposedException("NativeLibraryLoader");
                if (Rid == "linux-x64") dlerror();
                IntPtr Symbol = Rid == "win-x64" ? GetProcAddress(Handle, Name) : dlsym(Handle, Name);
                string Error = Rid == "linux-x64" ? ReadDlError() : null;
                if (Symbol == IntPtr.Zero || Error != null)
                    throw new InvalidOperationException("symbol missing: " + Name);
                return (T)(object)Marshal.GetDelegateForFunctionPointer(Symbol, typeof(T));
            }

            private string GetLoaderError()
            {
                if (Rid == "linux-x64") return ReadDlError() ?? "dlerror returned no detail";
                int Code = Marshal.GetLastWin32Error();
                return "Win32 " + Code + ": " + new Win32Exception(Code).Message;
            }

            private static string ReadDlError()
            {
                IntPtr Error = dlerror();
                return Error == IntPtr.Zero ? null : Marshal.PtrToStringAnsi(Error);
            }

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
            private static extern IntPtr LoadLibraryW(string FileName);
            [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
            private static extern IntPtr GetProcAddress(IntPtr Module, string Name);
            [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            private static extern bool FreeLibrary(IntPtr Module);
            [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr dlopen(string FileName, int Flags);
            [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr dlsym(IntPtr Module, string Name);
            [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
            private static extern int dlclose(IntPtr Module);
            [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr dlerror();
        }
    }
}


