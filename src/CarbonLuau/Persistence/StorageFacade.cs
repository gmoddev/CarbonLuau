using System;
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
                        (Code==31 ? Length<56 : Length!=32)) return 1;
                    if (Code==32) {
                        // This path also runs during fatal allocation cleanup. Read
                        // the fixed little-endian frame without allocating a buffer.
                        if (StorageScalar(Request,0,4)!=0x52504c43UL || StorageScalar(Request,4,4)!=1) return 1;
                        if (StorageScalar(Request,8,8)!=Binding.Vm || StorageScalar(Request,16,8)!=Binding.Domain) return 5;
                        ulong Route=StorageScalar(Request,24,8); if (Route==0) return 1;
                        Storage.ReleaseCallback(Binding,Route); return 0;
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
