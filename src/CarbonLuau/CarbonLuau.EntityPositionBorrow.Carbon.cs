using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Private exact-host adapter, NOT a script-visible Transform capability.
        // Supported topology/reclamation is owner-thread work. This borrow never
        // enters Unity, waits/helps jobs, calls user code, or retains host pointers.
        // Outstanding supported Transform jobs may race scalar values only.
        private sealed class EntityPositionBorrow
        {
            internal const int MaximumRecords = 65;
            private readonly int OwnerThread;
            private readonly EntityPositionComposition.LocalTRS[] Scratch =
                new EntityPositionComposition.LocalTRS[MaximumRecords];
            private bool Ready, Borrowing;
            internal long Observations { get; private set; }
            internal int MaximumObservedRecords { get; private set; }

            [StructLayout(LayoutKind.Explicit, Size = 16)]
            private struct RootHandle
            {
                [FieldOffset(0)] internal TransformHandle Handle;
                [FieldOffset(0)] internal IntPtr Data;
                [FieldOffset(8)] internal int Identity;
            }
            [StructLayout(LayoutKind.Explicit)]
            private struct FloatBits
            {
                [FieldOffset(0)] internal int Bits;
                [FieldOffset(0)] internal float Value;
            }

            internal EntityPositionBorrow(int OwnerThread)
            {
                this.OwnerThread = OwnerThread;
                if (Thread.CurrentThread.ManagedThreadId != OwnerThread || IntPtr.Size != 8) return;
                try {
                    bool Linux = Environment.OSVersion.Platform == PlatformID.Unix;
                    bool Windows = Environment.OSVersion.Platform == PlatformID.Win32NT;
                    if (!Linux && !Windows) return;
                    string Root = Path.GetDirectoryName(Application.dataPath);
                    if (!HasFileHash(Path.Combine(Root, Linux ? "UnityPlayer.so" : "UnityPlayer.dll"), Linux ?
                        "ab9b4ef10cfbfeee199fa00234164df0782f218bce0a579d9123439a4ca1faf1" :
                        "6ca8f6b3999f2de3201d973b56214bd82eb82e6fa31054c208c2e90b054f274a") ||
                        !HasHash(typeof(TransformHandle).Assembly, Linux ?
                        "ada97d7037c7c928d8d432f734115d82a3d805a2da628f481901da5d165795e2" :
                        "93b0e7e8e1b9a82f34d09740c45be2cbd7c13a82683a192af2858831b63ce45a")) return;
                    if (Marshal.SizeOf(typeof(TransformHandle)) != 16 ||
                        Marshal.OffsetOf(typeof(TransformHandle), "pTransformData").ToInt32() != 0 ||
                        Marshal.OffsetOf(typeof(TransformHandle), "id").ToInt32() != 8) return;
                    Ready = true;
                }
                catch (Exception) { Ready = false; }
            }

            internal bool Available { get { return Ready; } }

            private static float Scalar(IntPtr Address, int Offset)
            {
                var Result = new FloatBits { Bits = Marshal.ReadInt32(Address, Offset) };
                return Result.Value;
            }

            internal bool TryObserve(BaseEntity Entity, out EntityPositionComposition.Position Result)
            {
                Result = default(EntityPositionComposition.Position);
                if (!Ready || Borrowing || Thread.CurrentThread.ManagedThreadId != OwnerThread ||
                    ReferenceEquals(Entity, null)) return false;
                Borrowing = true;
                try {
                    // This is a fixed managed field getter, not Component.transform
                    // or an icall. SpawnShared writes it from this exact root on
                    // each incarnation; completed-epoch admission precedes this call.
                    var Root = new RootHandle { Handle = Entity.TransformHandle };
                    if (Root.Identity == 0 || Root.Data == IntPtr.Zero) return false;
                    IntPtr Hierarchy = Marshal.ReadIntPtr(Root.Data);
                    if (Hierarchy == IntPtr.Zero) return false;
                    int Index = Marshal.ReadInt32(Root.Data, 8);
                    int OriginalIndex = Index;
                    int Capacity = Marshal.ReadInt32(Hierarchy, 0x10);
                    // Physical slot offsets must fit Marshal's signed-int offset.
                    if (Capacity < 1 || Capacity > Int32.MaxValue / 48) return false;
                    IntPtr Values = Marshal.ReadIntPtr(Hierarchy, 0x18);
                    IntPtr Parents = Marshal.ReadIntPtr(Hierarchy, 0x20);
                    if (Values == IntPtr.Zero || Parents == IntPtr.Zero) return false;
                    int Count = 0;
                    while (Count < MaximumRecords) {
                        if (Index < 0 || Index >= Capacity) return false;
                        int Offset = checked(Index * 48);
                        Scratch[Count++] = new EntityPositionComposition.LocalTRS(
                            Scalar(Values, Offset), Scalar(Values, Offset + 4), Scalar(Values, Offset + 8),
                            Scalar(Values, Offset + 16), Scalar(Values, Offset + 20), Scalar(Values, Offset + 24), Scalar(Values, Offset + 28),
                            Scalar(Values, Offset + 32), Scalar(Values, Offset + 36), Scalar(Values, Offset + 40));
                        int Parent = Marshal.ReadInt32(Parents, checked(Index * 4));
                        if (Parent == -1) {
                            // No engine call occurs before the borrow ends. A
                            // post-check is diagnostic, not a replacement lease.
                            if (Marshal.ReadIntPtr(Root.Data) != Hierarchy ||
                                Marshal.ReadInt32(Root.Data, 8) != OriginalIndex) return false;
                            bool Valid = EntityPositionComposition.TryCompose(Scratch, Count, MaximumRecords, out Result);
                            if (Valid) {
                                if (Observations < Int64.MaxValue) Observations++;
                                MaximumObservedRecords = Math.Max(MaximumObservedRecords, Count);
                            }
                            return Valid;
                        }
                        if (Parent < 0 || Parent >= Capacity) return false;
                        Index = Parent;
                    }
                    return false; // Whole-query failure, never a subset/defer.
                }
                catch (Exception) { return false; }
                finally { Borrowing = false; }
            }

        }
    }
}
