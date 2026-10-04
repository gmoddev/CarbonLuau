// Isolated exact-host read research. Never included in production packages.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Jobs;
using Unity.Jobs;

namespace Oxide.Plugins
{
    [Info("CarbonLuau.WorldPositionBoundResearch", "CarbonLuau", "0.1.0")]
    [Description("Read-only icall mapping and current-position alternatives; not a work-bound PASS")]
    public class CarbonLuauWorldPositionBoundResearch : RustPlugin
    {
        private readonly List<GameObject> Owned = new List<GameObject>();
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

        private void MapCalls()
        {
            bool Linux = Application.platform == RuntimePlatform.LinuxServer;
            MonoMethod Lookup = null, GetName = null;
            IntPtr Image = IntPtr.Zero;
            if (!Linux) {
                IntPtr Mono = GetModuleHandle("mono-2.0-bdwgc.dll");
                Image = GetModuleHandle("UnityPlayer.dll");
                if (Mono == IntPtr.Zero || Image == IntPtr.Zero) throw new Exception("expected runtime image absent");
                Lookup = (MonoMethod)Marshal.GetDelegateForFunctionPointer(GetProcAddress(Mono, "mono_lookup_internal_call"), typeof(MonoMethod));
                GetName = (MonoMethod)Marshal.GetDelegateForFunctionPointer(GetProcAddress(Mono, "mono_method_get_name"), typeof(MonoMethod));
            }
            foreach (Type Type in new[] { typeof(Transform), typeof(TransformHandle), typeof(UnityEngine.Jobs.TransformAccess), typeof(JobHandle), typeof(Component) }) {
                foreach (MethodInfo Method in Type.GetMethods(BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)) {
                    if ((Method.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) == 0) continue;
                    string Name = Method.Name;
                    if (Name.IndexOf("GetPosition", StringComparison.Ordinal) < 0 && Name != "get_position_Injected" &&
                        Name != "get_localPosition_Injected" && Name != "GetParent_Injected" &&
                        Name.IndexOf("HierarchyCount", StringComparison.Ordinal) < 0 &&
                        Name != "Internal_TryGetParent" && Name != "Internal_HasParent" &&
                        Name.IndexOf("LocalToWorldMatrix", StringComparison.OrdinalIgnoreCase) < 0 &&
                        Name.IndexOf("ScheduleBatchedJobsAndIsCompleted", StringComparison.Ordinal) < 0 &&
                        Name != "get_transformHandle_Injected") continue;
                    IntPtr Handle = Method.MethodHandle.Value;
                    if (Marshal.PtrToStringAnsi(Linux ? mono_method_get_name(Handle) : GetName(Handle)) != Name)
                        throw new Exception("method identity mismatch");
                    IntPtr Address = Linux ? mono_lookup_internal_call(Handle) : Lookup(Handle);
                    if (Address == IntPtr.Zero) throw new Exception("unresolved icall");
                    if (Linux) {
                        ImageInfo Info;
                        if (dladdr(Address, out Info) == 0) throw new Exception("unresolved image");
                        Image = Info.Base;
                    }
                    Puts("[CarbonLuau:PositionBoundResearch] ICALL type=" + Type.FullName + " method=" + Name +
                        " offset=0x" + (Address.ToInt64() - Image.ToInt64()).ToString("x"));
                }
            }
        }

        // Exact-layout experiment on TASK-OWNED transforms only. These raw reads
        // do NOT prove native storage stability or universal dependency coverage,
        // and must not be used on arbitrary world entities as a production guard.
        private static bool ProbeGuard(Transform Leaf, int Maximum, out int Probes, out int Count, out bool Pending)
        {
            Probes = Count = 0;
            Pending = false;
            if (!Leaf) throw new Exception("owned transform absent");
            TransformHandle Handle = Leaf.transformHandle;
            FieldInfo DataField = typeof(TransformHandle).GetField("pTransformData", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (DataField == null) throw new Exception("pinned handle field absent");
            IntPtr Data = (IntPtr)DataField.GetValue(Handle);
            if (Data == IntPtr.Zero) throw new Exception("owned transform data absent");
            IntPtr Hierarchy = Marshal.ReadIntPtr(Data);
            if (Hierarchy == IntPtr.Zero) throw new Exception("owned hierarchy absent");
            Pending = Marshal.ReadInt64(Hierarchy) != 0;
            if (Pending) return false; // No getter, hierarchyCount or parent walk.
            IntPtr CountData = Marshal.ReadIntPtr(Hierarchy, 0x28);
            Count = Marshal.ReadInt32(CountData);
            if (Count < 1 || Count > 4096) throw new Exception("task-owned hierarchy layout mismatch");
            IntPtr Parents = Marshal.ReadIntPtr(Hierarchy, 0x20);
            int Index = Marshal.ReadInt32(Data, 8);
            while (Probes < Maximum) {
                if (Index < 0 || Index >= Count) throw new Exception("task-owned parent index invalid");
                ++Probes;
                Index = Marshal.ReadInt32(Parents, Index * 4);
                if (Index < 0) return true;
            }
            return false; // Excess depth is NOT successful omission.
        }

        private struct PositionJob : IJobParallelForTransform
        {
            public void Execute(int Index, TransformAccess Transform)
            { Transform.localPosition += new Vector3(0.25f, 0, 0); }
        }

        private void CheckGuard(Transform Leaf)
        {
            int Probes, Count;
            bool Pending;
            if (ProbeGuard(Leaf, 64, out Probes, out Count, out Pending) || Pending || Probes != 64 || Count != 65)
                throw new Exception("bounded depth rejection mismatch");
            if (!ProbeGuard(Leaf, 65, out Probes, out Count, out Pending) || Pending || Probes != 65)
                throw new Exception("bounded terminating walk mismatch");
            Puts("[CarbonLuau:PositionBoundResearch] GUARD depth64=reject depth65=accept probes=65 count=" + Count);
            var Access = new TransformAccessArray(new[] { Leaf });
            JobHandle Job = default(JobHandle);
            try {
                Job = new PositionJob().Schedule(Access);
                if (ProbeGuard(Leaf, 65, out Probes, out Count, out Pending) || !Pending || Probes != 0)
                    throw new Exception("scheduled dependency did not reject before raw walk/getter");
                Puts("[CarbonLuau:PositionBoundResearch] GUARD scheduled=reject probes=0 getter=not-called");
                Job.Complete(); // Explicit fixture synchronization, NEVER a proposed bounded query operation.
                Vector3 Position = Leaf.position; // Explicit fixture drain of retained completed fence.
                if (!ProbeGuard(Leaf, 65, out Probes, out Count, out Pending) || Pending || Probes != 65 || Position.x != 72.25f)
                    throw new Exception("post-completion guard/read mismatch");
                Puts("[CarbonLuau:PositionBoundResearch] GUARD fixture-drained=accept worldX=" + Position.x + " NOT_SYNCHRONIZATION_PROOF");
            } finally { Job.Complete(); Access.Dispose(); }
        }

        private void CheckAlternatives()
        {
            Transform Leaf = null;
            for (int Index = 0; Index < 65; ++Index) {
                var Node = new GameObject("CarbonLuauPositionBoundResearch");
                Owned.Add(Node);
                Node.transform.SetParent(Leaf, false);
                Node.transform.localPosition = new Vector3(1, 0, 0);
                Leaf = Node.transform;
            }
            Vector3 Position, Local;
            Quaternion Rotation;
            Leaf.GetPositionAndRotation(out Position, out Rotation);
            Leaf.GetLocalPositionAndRotation(out Local, out Rotation);
            if (Position != Leaf.position || Position.x != 65 || Local.x != 1 || Leaf.hierarchyCount != 65)
                throw new Exception("hierarchy/current position observation mismatch");
            Leaf.root.localPosition += new Vector3(7, 0, 0);
            Leaf.GetPositionAndRotation(out Position, out Rotation);
            if (Position != Leaf.position || Position.x != 72) throw new Exception("ancestor movement mismatch");
            Puts("[CarbonLuau:PositionBoundResearch] OBSERVED Nodes=65 LocalX=1 WorldX=" + Position.x +
                " HierarchyCount=" + Leaf.hierarchyCount + " HierarchyCapacity=" + Leaf.hierarchyCapacity);
            CheckGuard(Leaf);
            Puts("[CarbonLuau:PositionBoundResearch] OBSERVATION_PASS NOT_HARD_BOUND; mapping does not invoke raw icalls or patch Unity");
        }

        private void OnServerInitialized(bool Initial)
        {
            timer.Once(2, () => {
                try { MapCalls(); CheckAlternatives(); }
                catch (Exception Error) { Puts("[CarbonLuau:PositionBoundResearch] FAIL " + Error.GetType().Name + ": " + Error.Message); }
                finally { Cleanup(); ConsoleSystem.Run(ConsoleSystem.Option.Server, "quit"); }
            });
        }
        private void Cleanup()
        {
            for (int Index = Owned.Count - 1; Index >= 0; --Index)
                if (Owned[Index] != null) UnityEngine.Object.DestroyImmediate(Owned[Index]);
            Owned.Clear();
        }
        private void Unload() { Cleanup(); }
    }
}
