using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public sealed partial class FacadeSession
        {
            internal StorageQueue Storage;
            internal StorageQueue.Binding StorageBinding;
            internal ulong StorageVm;
            private ulong LastStorageRoute;
            private readonly List<KeyValuePair<string,string>> PendingStorageHints=new List<KeyValuePair<string,string>>(8);
            internal void FlushStorageHints()
            {
                if (!Active || Disposed || Storage==null || StorageBinding==null) return;
                int Count=Math.Min(32,PendingStorageHints.Count);
                for (int Index=0; Index<Count; ++Index) {
                    var Hint=PendingStorageHints[0]; Storage.Hint(StorageBinding,Hint.Key,Hint.Value);
                    PendingStorageHints.RemoveAt(0);
                }
            }
            internal void ClearStorageHints() { PendingStorageHints.Clear(); }

            // Native-only binary operations. No script-controlled namespace or
            // publication flag crosses this boundary; native has already checked
            // ResourceOwner == admitted domain and CanMutateHost.
            private uint StorageCall(ulong ExpectedDomain,uint Code,IntPtr Request,uint Length)
            {
                try {
                    World.Players.CheckOwner();
                    StorageQueue.Binding Binding=StorageBinding;
                    if (Storage==null || Binding==null) return 2;
                    if (ExpectedDomain!=(ulong)DomainLifetimeId || Binding.Domain!=ExpectedDomain ||
                        Binding.Vm!=(ulong)VmGenerationId) return 5;
                    if (Request==IntPtr.Zero || Length>StorageProcess.MaximumFrame ||
                        (Code==31 ? Length<56 : Code==32 ? Length!=32 : Code==33 ? Length<32 || Length>640 : true)) return 1;
                    if (Code==32) {
                        // This path also runs during fatal allocation cleanup. Read
                        // the fixed little-endian frame without allocating a buffer.
                        if (StorageScalar(Request,0,4)!=0x52504c43UL || StorageScalar(Request,4,4)!=1) return 1;
                        if (StorageScalar(Request,8,8)!=Binding.Vm || StorageScalar(Request,16,8)!=Binding.Domain) return 5;
                        ulong Route=StorageScalar(Request,24,8); if (Route==0) return 1;
                        Storage.ReleaseCallback(Binding,Route); return 0;
                    }
                    if (Code==33) {
                        var HintBytes=new byte[Length]; Marshal.Copy(Request,HintBytes,0,(int)Length);
                        if (HintBytes[0]!=67 || HintBytes[1]!=76 || HintBytes[2]!=80 || HintBytes[3]!=72 ||
                            StorageProcess.U32(HintBytes,4)!=1 || StorageProcess.U64(HintBytes,8)!=Binding.Vm ||
                            StorageProcess.U64(HintBytes,16)!=Binding.Domain || Disposed || !Binding.Alive) return 5;
                        uint HintStoreSize=StorageProcess.U32(HintBytes,24), Fields=StorageProcess.U32(HintBytes,28);
                        if (HintStoreSize<1 || HintStoreSize>64 || Fields<1 || Fields>8 || 32UL+HintStoreSize>Length) return 1;
                        int Position=32;
                        string HintStore=StorageProcess.Utf8.GetString(HintBytes,Position,(int)HintStoreSize); Position+=(int)HintStoreSize;
                        StorageQueue.Name(HintStore,64);
                        var Staged=new List<KeyValuePair<string,string>>((int)Fields);
                        for (uint Index=0; Index<Fields; ++Index) {
                            if (Position+4>Length) return 1;
                            uint FieldSize=StorageProcess.U32(HintBytes,Position); Position+=4;
                            if (FieldSize<1 || FieldSize>64 || Position+FieldSize>Length) return 1;
                            string Field=StorageProcess.Utf8.GetString(HintBytes,Position,(int)FieldSize); Position+=(int)FieldSize;
                            StorageQueue.ValidateField(Field);
                            Staged.Add(new KeyValuePair<string,string>(HintStore,Field));
                        }
                        if (Position!=Length) return 1;
                        var Novel=new List<KeyValuePair<string,string>>(Staged.Count);
                        foreach (var Hint in Staged)
                            if (!PendingStorageHints.Contains(Hint) && !Novel.Contains(Hint)) Novel.Add(Hint);
                        if (PendingStorageHints.Count+Novel.Count>512) return 3;
                        PendingStorageHints.AddRange(Novel);
                        return 0;
                    }
                    var Bytes=new byte[Length]; Marshal.Copy(Request,Bytes,0,(int)Length);
                    if (Bytes[0]!=67 || Bytes[1]!=76 || Bytes[2]!=80 ||
                        Bytes[3]!=66 || StorageProcess.U32(Bytes,4)!=1) return 1;
                    if (Disposed || !Active || !World.IsActive(this) || !Binding.Alive) return 5;
                    if (StorageProcess.U64(Bytes,12)!=Binding.Vm || StorageProcess.U64(Bytes,20)!=Binding.Domain) return 5;
                    ulong Token=StorageProcess.U64(Bytes,28);
                    if (Token==0 || Token<=LastStorageRoute) return 5;
                    uint Tag=StorageProcess.U32(Bytes,36), PackageSize=StorageProcess.U32(Bytes,40),
                        StoreSize=StorageProcess.U32(Bytes,44), KeySize=StorageProcess.U32(Bytes,48),
                        EnvelopeSize=StorageProcess.U32(Bytes,52), Operation=StorageProcess.U32(Bytes,8);
                    if (PackageSize>65 || StoreSize==0 || StoreSize>64 || KeySize==0 || KeySize>128 || EnvelopeSize>65536 ||
                        56UL+PackageSize+StoreSize+KeySize+EnvelopeSize!=Length || Operation<1 || Operation>3 ||
                        (Operation==2 ? EnvelopeSize<45 : EnvelopeSize!=0)) return 1;
                    if (Tag!=(Binding.Package==null ? 0u : 1u)) return 5;
                    int Offset=56;
                    string Package=StorageProcess.Utf8.GetString(Bytes,Offset,(int)PackageSize); Offset+=(int)PackageSize;
                    if (!String.Equals(Package,Binding.Package??"",StringComparison.Ordinal)) return 5;
                    string Store=StorageProcess.Utf8.GetString(Bytes,Offset,(int)StoreSize); Offset+=(int)StoreSize;
                    string Key=StorageProcess.Utf8.GetString(Bytes,Offset,(int)KeySize); Offset+=(int)KeySize;
                    var Envelope=new byte[EnvelopeSize]; Buffer.BlockCopy(Bytes,Offset,Envelope,0,(int)EnvelopeSize);
                    Storage.Submit(Binding,Binding.Vm,Binding.Domain,true,(StorageQueue.Operation)Operation,Store,Key,Envelope,Token);
                    // Successful acceptance must not allocate or marshal a response.
                    LastStorageRoute=Token; return 0;
                } catch (StorageQueue.Rejection Error) { return Error.Status; }
                catch (ArgumentException) { return 1; }
                catch (Exception) { return 6; }
            }
            private static ulong StorageScalar(IntPtr Frame,int Offset,int Length)
            {
                ulong Value=0;
                for (int Index=0; Index<Length; ++Index) Value|=(ulong)Marshal.ReadByte(Frame,Offset+Index)<<(8*Index);
                return Value;
            }
        }
    }
}
