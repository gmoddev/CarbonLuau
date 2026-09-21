using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        // Snapshot before entering Luau: no file access from native require or a
        // scheduler callback. Filesystem owner is trusted against concurrent edits.
        public sealed class ScriptSnapshot
        {
            public string EntryName, EntrySource;
            public readonly SortedDictionary<string, string> Modules = new SortedDictionary<string, string>(StringComparer.Ordinal);
            private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
            public static void ValidatePath(string Value, bool File)
            { global::CarbonLuau.Core.ModulePath.Validate(Value, File); }
            private static void CheckNode(string PathValue)
            {
                FileAttributes Attributes = File.GetAttributes(PathValue);
                if ((Attributes & FileAttributes.ReparsePoint) != 0) throw new InvalidOperationException("source path: symlinks/reparse points are unsupported");
            }
            private static string Resolve(string Root, string Relative, bool File)
            {
                ValidatePath(Relative, File);
                string Current = Root;
                foreach (string Part in Relative.Split('/')) {
                    // Ordinal segment equality prevents case-insensitive Windows aliases.
                    bool Exact = false;
                    int Count = 0;
                    foreach (string Child in Directory.EnumerateFileSystemEntries(Current)) {
                        if (++Count > 1024) throw new InvalidOperationException("source directory exceeds 1024 entries");
                        if (String.Equals(Path.GetFileName(Child), Part, StringComparison.Ordinal)) { Exact = true; break; }
                    }
                    if (!Exact) throw new InvalidOperationException("source not found: " + Relative);
                    Current = Path.Combine(Current, Part); CheckNode(Current);
                }
                return Current;
            }
            private static string ReadSource(string FileName, string Logical)
            {
                try {
                    using (var Stream = new FileStream(FileName, FileMode.Open, FileAccess.Read, FileShare.Read)) {
                        if (Stream.Length > 65536) throw new InvalidOperationException("source exceeds 65536 bytes: " + Logical);
                        byte[] Bytes = new byte[65537]; int Count = 0, Read;
                        while (Count < Bytes.Length && (Read = Stream.Read(Bytes, Count, Bytes.Length - Count)) != 0) Count += Read;
                        if (Count > 65536) throw new InvalidOperationException("source exceeds 65536 bytes: " + Logical);
                        int Start = Count >= 3 && Bytes[0] == 239 && Bytes[1] == 187 && Bytes[2] == 191 ? 3 : 0;
                        string Text = Utf8.GetString(Bytes, Start, Count - Start);
                        if (Text.IndexOf('\0') >= 0) throw new InvalidOperationException("NUL source rejected: " + Logical);
                        return Text;
                    }
                } catch (DecoderFallbackException) { throw new InvalidOperationException("source must be valid UTF-8: " + Logical); }
                catch (IOException) { throw new InvalidOperationException("source could not be read: " + Logical); }
                catch (UnauthorizedAccessException) { throw new InvalidOperationException("source access denied: " + Logical); }
            }
            public static ScriptSnapshot Load(string DataDirectory, RuntimeConfig Config)
            {
                ValidatePath(Config.ScriptRoot, false); ValidatePath(Config.ModuleRoot, false); ValidatePath(Config.EntryScript, true);
                if (Config.EntryScript.Length > 121) throw new InvalidOperationException("entry path exceeds 121 characters including extension");
                string Root = Path.GetFullPath(Path.Combine(DataDirectory, "CarbonLuau"));
                // Fail closed for links anywhere in the configured ancestor chain.
                for (var Node = new DirectoryInfo(Root); Node != null; Node = Node.Parent) CheckNode(Node.FullName);
                Root = Resolve(Root, Config.ScriptRoot, false);
                var Snapshot = new ScriptSnapshot { EntryName = Config.EntryScript };
                Snapshot.EntrySource = ReadSource(Resolve(Root, Config.EntryScript, true), Config.EntryScript);
                string ModuleDirectory = Resolve(Root, Config.ModuleRoot, false);
                var Pending = new Stack<KeyValuePair<string, string>>();
                Pending.Push(new KeyValuePair<string, string>(ModuleDirectory, ""));
                int Nodes = 0, Total = Utf8.GetByteCount(Snapshot.EntrySource);
                while (Pending.Count != 0) {
                    var Directory = Pending.Pop();
                    foreach (string Child in System.IO.Directory.EnumerateFileSystemEntries(Directory.Key)) {
                        if (++Nodes > 1024) throw new InvalidOperationException("module tree exceeds 1024 entries");
                        CheckNode(Child);
                        string Relative = Directory.Value + Path.GetFileName(Child);
                        if ((File.GetAttributes(Child) & FileAttributes.Directory) != 0) {
                            ValidatePath(Relative, false);
                            Pending.Push(new KeyValuePair<string, string>(Child, Relative + "/"));
                        } else {
                            if (!Relative.EndsWith(".luau", StringComparison.Ordinal)) continue;
                            ValidatePath(Relative, true);
                            if (Snapshot.Modules.Count == 256) throw new InvalidOperationException("module count exceeds 256");
                            string Source = ReadSource(Child, Config.ModuleRoot + "/" + Relative);
                            Total = checked(Total + Utf8.GetByteCount(Source));
                            if (Total > 4 * 1024 * 1024) throw new InvalidOperationException("source snapshot exceeds 4 MiB");
                            Snapshot.Modules.Add(Relative.Substring(0, Relative.Length - 5), Source);
                        }
                    }
                }
                return Snapshot;
            }
        }

    }
}
