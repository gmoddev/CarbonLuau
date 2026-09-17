using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public sealed partial class RuntimeGeneration : IDisposable
        {
            private readonly NativeRuntime Native;
            internal ulong Handle;
            public readonly long Number;
            public bool Alive { get { return Handle != 0; } }
            public RuntimeGeneration(NativeRuntime Native, long Number, RuntimeConfig Config)
            {
                this.Native = Native; this.Number = Number;
                Handle = Native.Create(Config);
            }
            public VmInfo Info { get { return Native.Info(Handle); } }
            public ExecutionResult Execute(string Chunk, string Source, int Milliseconds)
            {
                if (!Alive) return new ExecutionResult { Status = RuntimeStatus.INVALID_ARGUMENT };
                return Native.Execute(Handle, Chunk, Source, Milliseconds);
            }
            public void Dispose()
            {
                if (!Alive) return;
                Native.CheckOwner();
                ulong Owned = Handle;
                Handle = 0;
                Native.Destroy(Owned);
            }
        }
    }
}

