// Read-only exact-host access mapping. No host mutations or production reader.
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Jobs;

namespace Oxide.Plugins
{
    [Info("CarbonLuau.WorldPositionAccessResearch", "CarbonLuau", "0.1.0")]
    [Description("Maps topology/scalar access wrappers; NOT a storage-lease qualification")]
    public class CarbonLuauWorldPositionAccessResearch : RustPlugin
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct ImageInfo { public IntPtr FileName, Base, SymbolName, SymbolAddress; }
        [DllImport("libmonobdwgc-2.0.so", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mono_lookup_internal_call(IntPtr Method);
        [DllImport("libmonobdwgc-2.0.so", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr mono_method_get_name(IntPtr Method);
        [DllImport("libdl.so.2", CallingConvention = CallingConvention.Cdecl)]
        private static extern int dladdr(IntPtr Address, out ImageInfo Info);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr GetModuleHandle(string Name);
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr Module, string Name);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr MonoMethod(IntPtr Method);

        private void MapAccess()
        {
            bool Linux = Application.platform == RuntimePlatform.LinuxServer;
            if (!Linux && Application.platform != RuntimePlatform.WindowsServer)
                throw new Exception("unsupported research platform");
            MonoMethod Lookup = null, GetName = null;
            IntPtr Image = IntPtr.Zero;
            if (!Linux) {
                IntPtr Mono = GetModuleHandle("mono-2.0-bdwgc.dll");
                Image = GetModuleHandle("UnityPlayer.dll");
                if (Mono == IntPtr.Zero || Image == IntPtr.Zero) throw new Exception("expected runtime image absent");
                Lookup = (MonoMethod)Marshal.GetDelegateForFunctionPointer(GetProcAddress(Mono, "mono_lookup_internal_call"), typeof(MonoMethod));
                GetName = (MonoMethod)Marshal.GetDelegateForFunctionPointer(GetProcAddress(Mono, "mono_method_get_name"), typeof(MonoMethod));
            }
            int Mapped = 0;
            foreach (Type Type in new[] { typeof(Transform), typeof(TransformHandle), typeof(TransformAccess), typeof(UnityEngine.Object) }) {
                MethodInfo[] Methods = Type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (Methods.Length > 1024) throw new Exception("access method inventory exceeded");
                foreach (MethodInfo Method in Methods) {
                    if ((Method.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) == 0) continue;
                    string Name = Method.Name;
                    if (Name != "SetParent_Injected" && Name != "Internal_SetParent_Injected" &&
                        Name != "Internal_IsValid" && Name != "Internal_TryGetParent" &&
                        Name != "GetLocalPosition" && Name != "GetLocalRotation" && Name != "GetLocalScale" &&
                        Name != "SetLocalPosition" && Name != "Destroy_Injected" && Name != "DestroyImmediate_Injected") continue;
                    IntPtr Handle = Method.MethodHandle.Value;
                    if (Marshal.PtrToStringAnsi(Linux ? mono_method_get_name(Handle) : GetName(Handle)) != Name)
                        throw new Exception("method identity mismatch");
                    IntPtr Address = Linux ? mono_lookup_internal_call(Handle) : Lookup(Handle);
                    if (Address == IntPtr.Zero) throw new Exception("unresolved access icall");
                    if (Linux) {
                        ImageInfo Info;
                        if (dladdr(Address, out Info) == 0) throw new Exception("unresolved native image");
                        Image = Info.Base;
                    }
                    ++Mapped;
                    Puts("[CarbonLuau:PositionAccessResearch] ICALL type=" + Type.FullName + " method=" + Name +
                        " offset=0x" + (Address.ToInt64() - Image.ToInt64()).ToString("x"));
                }
            }
            if (Mapped < 8 || Mapped > 16) throw new Exception("expected access mapping inventory changed");
            Puts("[CarbonLuau:PositionAccessResearch] OBSERVATION_PASS mapped=" + Mapped + " NOT_HARD_BOUND NOT_STORAGE_LEASE");
        }

        private void OnServerInitialized()
        {
            timer.Once(1, () => {
                try { MapAccess(); }
                catch (Exception Error) { Puts("[CarbonLuau:PositionAccessResearch] FAIL " + Error.GetType().Name + " " + Error.Message); }
                finally { timer.Once(1, () => ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit")); }
            });
        }
    }
}
