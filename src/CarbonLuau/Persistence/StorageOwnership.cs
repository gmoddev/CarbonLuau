using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Nonblocking process-lifetime guard, with no filesystem operation.
        // It spans plugin assembly reloads while a detached supervisor reaps its
        // child. The worker separately holds the authoritative database file lock.
        internal sealed class StorageOwnership : IDisposable
        {
            private Mutex Windows;
            private bool Held;
            private int Socket=-1;
            internal StorageOwnership(string Directory)
            {
                bool IsWindows=Environment.OSVersion.Platform==PlatformID.Win32NT;
                string Canonical=Path.GetFullPath(Directory);
                if (IsWindows) Canonical=Canonical.ToUpperInvariant();
                byte[] Hash;
                using (var Sha=SHA256.Create()) Hash=Sha.ComputeHash(StorageProcess.Utf8.GetBytes(Canonical));
                string Name="CarbonLuau.Storage."+BitConverter.ToString(Hash).Replace("-","");
                try {
                    if (IsWindows) {
                        Windows=new Mutex(false,"Global\\"+Name);
                        try { Held=Windows.WaitOne(0); } catch (AbandonedMutexException) { Held=true; }
                        if (!Held) throw new IOException("storage supervisor already owned");
                    } else {
                        Socket=socket(1,2|0x80000,0); // AF_UNIX, datagram, close-on-exec
                        if (Socket<0) throw new IOException("storage ownership unavailable");
                        byte[] Text=Encoding.ASCII.GetBytes(Name), Address=new byte[3+Text.Length];
                        Address[0]=1; // little-endian sa_family_t; leading NUL is abstract, not a file path
                        Buffer.BlockCopy(Text,0,Address,3,Text.Length);
                        if (bind(Socket,Address,(uint)Address.Length)!=0) throw new IOException("storage supervisor already owned");
                    }
                } catch { Dispose(); throw; }
            }
            public void Dispose()
            {
                if (Windows!=null) { if (Held) Windows.ReleaseMutex(); Windows.Dispose(); Windows=null; Held=false; }
                if (Socket>=0) { close(Socket); Socket=-1; }
            }
            [DllImport("libc",SetLastError=true)] private static extern int socket(int Domain,int Type,int Protocol);
            [DllImport("libc",SetLastError=true)] private static extern int bind(int Socket,byte[] Address,uint Length);
            [DllImport("libc")] private static extern int close(int Socket);
        }
    }
}
