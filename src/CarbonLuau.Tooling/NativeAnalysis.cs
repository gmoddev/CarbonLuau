using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json.Linq;

namespace CarbonLuau.Tooling;

internal static class NativeAnalysis
{
    // Only the pack-owned parser library is loaded. It links Luau.Ast, never the VM.
    static NativeAnalysis()
    {
        NativeLibrary.SetDllImportResolver(typeof(NativeAnalysis).Assembly, (Name, Assembly, Search) => {
            if (Name != "carbonluau_analysis") return IntPtr.Zero;
            string File = OperatingSystem.IsWindows() ? "carbonluau_analysis.dll" : OperatingSystem.IsMacOS() ? "libcarbonluau_analysis.dylib" : "libcarbonluau_analysis.so";
            return NativeLibrary.Load(Path.Combine(AppContext.BaseDirectory, File));
        });
        if (Version() != 1) throw new InvalidOperationException("Static analyzer ABI mismatch.");
    }
    [DllImport("carbonluau_analysis", EntryPoint = "carbonluau_analysis_version", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Version();
    [DllImport("carbonluau_analysis", EntryPoint = "carbonluau_analysis_source", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Source(byte[] Text, nuint Length, byte[] Output, nuint Capacity);
    [DllImport("carbonluau_analysis", EntryPoint = "carbonluau_analysis_import", CallingConvention = CallingConvention.Cdecl)]
    private static extern int Import(byte[] Name, nuint Length, int Declared, int Available, byte[] Main, byte[] Exports, nuint ExportsLength, byte[] Output, nuint Capacity);
    internal static JObject Parse(string Text)
    {
        byte[] Bytes = Protocol.Utf8.GetBytes(Text), Output = new byte[1024 * 1024 + 1];
        int Length = Source(Bytes, (nuint)Bytes.Length, Output, (nuint)Output.Length);
        if (Length < 0) throw new ProtocolError("AnalysisLimit", "Source analysis exceeded its bound.");
        return JObject.Parse(Encoding.UTF8.GetString(Output, 0, Length));
    }
    internal static JObject Resolve(string Name, bool Declared, bool Available, string? Main, string[] Exports)
    {
        byte[] NameBytes = Protocol.Utf8.GetBytes(Name + "\0"), Public = Protocol.Utf8.GetBytes(string.Join("\n", Exports)), Output = new byte[4096];
        int Length = Import(NameBytes, (nuint)(NameBytes.Length - 1), Declared ? 1 : 0, Available ? 1 : 0,
            Protocol.Utf8.GetBytes((Main ?? "") + "\0"), Public, (nuint)Public.Length, Output, (nuint)Output.Length);
        if (Length < 0) return new JObject { ["Code"] = 1, ["Logical"] = "" };
        return JObject.Parse(Encoding.UTF8.GetString(Output, 0, Length));
    }
}
