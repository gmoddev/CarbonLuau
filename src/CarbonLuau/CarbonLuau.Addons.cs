// Reference: System.IO.Compression
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public enum AddonRegistrationState { Registered, Blocked, Initializing, Active, Failed, Stopping }

        public static class AddonPolicy
        {
            public const string ProtocolName = "CarbonLuau.Addons", ProtocolVersion = "1.0";
            public const int Schema = 1, MaxArchiveBytes = 4 * 1024 * 1024, MaxExpandedBytes = 8 * 1024 * 1024;
            public const int MaxManifestBytes = 65536, MaxSourceBytes = 65536, MaxAggregateSourceBytes = 4 * 1024 * 1024;
            public const int MaxSourceModules = 256, MaxArchiveEntries = 512, MaxPathCharacters = 127, MaxPathDepth = 32;
            public const int MaxDependencies = 32, MaxRegistrations = 128, MaxRegistrationsPerProvider = 32;
            public const int MaxAggregateSnapshotBytes = 32 * 1024 * 1024, MaxDiagnosticCharacters = 1024, MaxTombstones = 1024;
            public static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

            public static void ValidateId(string Value)
            {
                if (String.IsNullOrEmpty(Value) || Value.Length > 65 || Value == "carbonluau" || Value.StartsWith("carbonluau.", StringComparison.Ordinal))
                    throw new InvalidOperationException("addon id is invalid or reserved");
                string[] Segments = Value.Split('.');
                if (Segments.Length < 1 || Segments.Length > 2) throw new InvalidOperationException("addon id must contain one or two segments");
                foreach (string Segment in Segments) {
                    if (Segment.Length < 1 || Segment.Length > 32 || !IsAlphaNumeric(Segment[0]) || !IsAlphaNumeric(Segment[Segment.Length - 1]))
                        throw new InvalidOperationException("addon id segment is invalid");
                    foreach (char Character in Segment)
                        if (!IsAlphaNumeric(Character) && Character != '_' && Character != '-')
                            throw new InvalidOperationException("addon id must be canonical lowercase ASCII");
                }
            }
            private static bool IsAlphaNumeric(char Character)
            { return (Character >= 'a' && Character <= 'z') || (Character >= '0' && Character <= '9'); }
            public static void ValidateVersion(string Value)
            {
                if (String.IsNullOrEmpty(Value) || Value.Length > 32) throw new InvalidOperationException("addon version must be MAJOR.MINOR.PATCH");
                string[] Parts = Value.Split('.');
                if (Parts.Length != 3) throw new InvalidOperationException("addon version must be MAJOR.MINOR.PATCH");
                foreach (string Part in Parts) {
                    if (Part.Length < 1 || Part.Length > 10 || (Part.Length > 1 && Part[0] == '0'))
                        throw new InvalidOperationException("addon version must be canonical MAJOR.MINOR.PATCH");
                    ulong Number;
                    if (!UInt64.TryParse(Part, NumberStyles.None, CultureInfo.InvariantCulture, out Number) || Number > UInt32.MaxValue)
                        throw new InvalidOperationException("addon version component is invalid");
                }
            }
            public static string Diagnostic(string Value)
            {
                if (String.IsNullOrEmpty(Value)) return "operation failed";
                Value = Value.Replace('\0', ' ').Replace('\r', ' ').Replace('\n', ' ');
                return Value.Substring(0, Math.Min(MaxDiagnosticCharacters, Value.Length));
            }
        }

        public sealed class AddonPackageSnapshot
        {
            private readonly string EntrySource;
            private readonly SortedDictionary<string, string> ModuleSources;
            private readonly string[] RequiredDependencies, OptionalDependencies;
            public readonly string Id, Version, Hash;
            public readonly int SourceBytes;
            public int DependencyCount { get { return RequiredDependencies.Length + OptionalDependencies.Length; } }
            private AddonPackageSnapshot(string Id, string Version, string EntrySource,
                SortedDictionary<string, string> Modules, List<string> Required, List<string> Optional, string Hash, int SourceBytes)
            {
                this.Id = Id; this.Version = Version; this.EntrySource = EntrySource;
                ModuleSources = new SortedDictionary<string, string>(Modules, StringComparer.Ordinal);
                RequiredDependencies = Required.ToArray(); OptionalDependencies = Optional.ToArray();
                this.Hash = Hash; this.SourceBytes = SourceBytes;
            }
            public ScriptSnapshot ToScriptSnapshot()
            {
                var Result = new ScriptSnapshot {EntryName = "init.luau", EntrySource = EntrySource};
                foreach (var Module in ModuleSources) Result.Modules.Add(Module.Key, Module.Value);
                return Result;
            }
            public string[] Dependencies(bool Optional)
            { return (string[])(Optional ? OptionalDependencies : RequiredDependencies).Clone(); }

            public static AddonPackageSnapshot FromSource(string Id, string Version, byte[] Source)
            {
                AddonPolicy.ValidateId(Id); AddonPolicy.ValidateVersion(Version);
                if (Source == null) throw new InvalidOperationException("source bytes are required");
                byte[] Copy = (byte[])Source.Clone();
                if (Copy.Length > AddonPolicy.MaxSourceBytes) throw new InvalidOperationException("source exceeds 65536 bytes");
                string Text = Decode(Copy, "source");
                byte[] Identity = AddonPolicy.Utf8.GetBytes(Id + "\0" + Version + "\0" + Text);
                return new AddonPackageSnapshot(Id, Version, Text, new SortedDictionary<string, string>(StringComparer.Ordinal),
                    new List<string>(), new List<string>(), HashBytes(Identity), Copy.Length);
            }

            public static AddonPackageSnapshot FromArchive(byte[] ArchiveBytes)
            {
                if (ArchiveBytes == null) throw new InvalidOperationException("archive bytes are required");
                byte[] Copy = (byte[])ArchiveBytes.Clone();
                if (Copy.Length > AddonPolicy.MaxArchiveBytes) throw new InvalidOperationException("archive exceeds 4 MiB");
                ValidateZipContainer(Copy);
                byte[] ManifestBytes = null; string EntrySource = null;
                var Modules = new SortedDictionary<string, string>(StringComparer.Ordinal);
                int Expanded = 0, Sources = 0, SourceBytes = 0, Entries = 0;
                try {
                    using (var Stream = new MemoryStream(Copy, false))
                    using (var Archive = new ZipArchive(Stream, ZipArchiveMode.Read, false)) {
                        foreach (var Entry in Archive.Entries) {
                            if (++Entries > AddonPolicy.MaxArchiveEntries) throw new InvalidOperationException("archive exceeds 512 entries");
                            ValidateArchiveEntry(Entry);
                            string Name = Entry.FullName;
                            int Limit = Name == "addon.json" ? AddonPolicy.MaxManifestBytes : AddonPolicy.MaxSourceBytes;
                            byte[] Bytes = ReadEntry(Entry, Limit, ref Expanded);
                            if (Name == "addon.json") {
                                if (ManifestBytes != null) throw new InvalidOperationException("duplicate addon.json");
                                ManifestBytes = Bytes; continue;
                            }
                            if (!Name.EndsWith(".luau", StringComparison.Ordinal)) throw new InvalidOperationException("archive contains unsupported entry");
                            ScriptSnapshot.ValidatePath(Name, true);
                            if (Name.Split('/').Length > AddonPolicy.MaxPathDepth) throw new InvalidOperationException("source path depth exceeds 32");
                            if (++Sources > AddonPolicy.MaxSourceModules + 1) throw new InvalidOperationException("archive exceeds 256 modules plus init.luau");
                            SourceBytes = checked(SourceBytes + Bytes.Length);
                            if (SourceBytes > AddonPolicy.MaxAggregateSourceBytes) throw new InvalidOperationException("aggregate source exceeds 4 MiB");
                            string Text = Decode(Bytes, "source " + Name);
                            if (Name == "init.luau") {
                                if (EntrySource != null) throw new InvalidOperationException("duplicate init.luau");
                                EntrySource = Text;
                            } else {
                                string Logical = Name.Substring(0, Name.Length - 5);
                                if (Modules.ContainsKey(Logical)) throw new InvalidOperationException("duplicate normalized source path");
                                Modules.Add(Logical, Text);
                            }
                        }
                    }
                } catch (InvalidOperationException) { throw; }
                catch (Exception Error) when (Error is InvalidDataException || Error is IOException || Error is NotSupportedException) {
                    throw new InvalidOperationException("archive is malformed, encrypted or uses unsupported ZIP features");
                }
                if (ManifestBytes == null) throw new InvalidOperationException("archive is missing addon.json");
                if (EntrySource == null) throw new InvalidOperationException("archive is missing init.luau");
                string ManifestText = Decode(ManifestBytes, "manifest");
                string Id, Version; List<string> Required, Optional;
                try { ParseManifest(ManifestText, out Id, out Version, out Required, out Optional); }
                catch (InvalidOperationException) { throw; }
                catch (JsonException) { throw new InvalidOperationException("manifest JSON is malformed"); }
                return new AddonPackageSnapshot(Id, Version, EntrySource, Modules, Required, Optional,
                    HashBytes(Copy), SourceBytes);
            }

            private static void ValidateZipContainer(byte[] Bytes)
            {
                int Minimum = Math.Max(0, Bytes.Length - 65557), Eocd = -1;
                for (int Offset = Bytes.Length - 22; Offset >= Minimum; --Offset)
                    if (ReadUInt32(Bytes, Offset) == 0x06054b50 && Offset + 22 + ReadUInt16(Bytes, Offset + 20) == Bytes.Length) { Eocd = Offset; break; }
                if (Eocd < 0) throw new InvalidOperationException("archive end record is missing or malformed");
                int Disk = ReadUInt16(Bytes, Eocd + 4), DirectoryDisk = ReadUInt16(Bytes, Eocd + 6);
                int DiskEntries = ReadUInt16(Bytes, Eocd + 8), Entries = ReadUInt16(Bytes, Eocd + 10);
                uint DirectorySize = ReadUInt32(Bytes, Eocd + 12), DirectoryOffset = ReadUInt32(Bytes, Eocd + 16);
                if (Disk != 0 || DirectoryDisk != 0 || DiskEntries != Entries) throw new InvalidOperationException("multi-disk ZIP archives are unsupported");
                if (Entries == UInt16.MaxValue || DirectorySize == UInt32.MaxValue || DirectoryOffset == UInt32.MaxValue)
                    throw new InvalidOperationException("ZIP64 archives are unsupported");
                if (Entries > AddonPolicy.MaxArchiveEntries || (ulong)DirectoryOffset + DirectorySize != (ulong)Eocd)
                    throw new InvalidOperationException("archive central directory is malformed");
                int Cursor = checked((int)DirectoryOffset);
                for (int Index = 0; Index < Entries; ++Index) {
                    if (Cursor < 0 || Cursor + 46 > Eocd || ReadUInt32(Bytes, Cursor) != 0x02014b50)
                        throw new InvalidOperationException("archive central directory is malformed");
                    int Flags = ReadUInt16(Bytes, Cursor + 8), Method = ReadUInt16(Bytes, Cursor + 10);
                    int NameLength = ReadUInt16(Bytes, Cursor + 28), ExtraLength = ReadUInt16(Bytes, Cursor + 30), CommentLength = ReadUInt16(Bytes, Cursor + 32);
                    int EntryDisk = ReadUInt16(Bytes, Cursor + 34); uint LocalOffset = ReadUInt32(Bytes, Cursor + 42);
                    if ((Flags & (1 | 0x40 | 0x2000)) != 0) throw new InvalidOperationException("encrypted ZIP entries are unsupported");
                    if (Method != 0 && Method != 8) throw new InvalidOperationException("ZIP compression method is unsupported");
                    int AllowedFlags = 0x0008 | 0x0800 | (Method == 8 ? 0x0006 : 0);
                    if ((Flags & ~AllowedFlags) != 0) throw new InvalidOperationException("ZIP entry flags are unsupported");
                    if (EntryDisk != 0 || LocalOffset == UInt32.MaxValue) throw new InvalidOperationException("multi-disk and ZIP64 entries are unsupported");
                    int Next = checked(Cursor + 46 + NameLength + ExtraLength + CommentLength);
                    int Local = checked((int)LocalOffset);
                    if (Next > Eocd || Local < 0 || Local + 30 > Bytes.Length || ReadUInt32(Bytes, Local) != 0x04034b50)
                        throw new InvalidOperationException("archive entry headers are malformed");
                    int LocalFlags = ReadUInt16(Bytes, Local + 6), LocalMethod = ReadUInt16(Bytes, Local + 8), LocalNameLength = ReadUInt16(Bytes, Local + 26);
                    if (LocalFlags != Flags || LocalMethod != Method || LocalNameLength != NameLength || Local + 30 + LocalNameLength > Bytes.Length)
                        throw new InvalidOperationException("archive entry headers disagree");
                    for (int NameIndex = 0; NameIndex < NameLength; ++NameIndex)
                        if (Bytes[Cursor + 46 + NameIndex] != Bytes[Local + 30 + NameIndex]) throw new InvalidOperationException("archive entry names disagree");
                    Cursor = Next;
                }
                if (Cursor != Eocd) throw new InvalidOperationException("archive central directory contains trailing data");
            }
            private static ushort ReadUInt16(byte[] Bytes, int Offset)
            {
                if (Offset < 0 || Offset + 2 > Bytes.Length) return 0;
                return (ushort)(Bytes[Offset] | (Bytes[Offset + 1] << 8));
            }
            private static uint ReadUInt32(byte[] Bytes, int Offset)
            {
                if (Offset < 0 || Offset + 4 > Bytes.Length) return 0;
                return (uint)(Bytes[Offset] | (Bytes[Offset + 1] << 8) | (Bytes[Offset + 2] << 16) | (Bytes[Offset + 3] << 24));
            }

            private static void ValidateArchiveEntry(ZipArchiveEntry Entry)
            {
                string Name = Entry.FullName;
                if (String.IsNullOrEmpty(Name) || Name.Length > AddonPolicy.MaxPathCharacters || Name.EndsWith("/", StringComparison.Ordinal) || Name.IndexOf('\\') >= 0)
                    throw new InvalidOperationException("archive entry path is noncanonical");
                int UnixType = (Entry.ExternalAttributes >> 16) & 0xF000;
                if (UnixType != 0 && UnixType != 0x8000) throw new InvalidOperationException("archive symlink or special entry is unsupported");
                if ((Entry.ExternalAttributes & 0x10) != 0) throw new InvalidOperationException("archive directory entries are unsupported");
                if (Name == "addon.json") return;
                if (!Name.EndsWith(".luau", StringComparison.Ordinal)) throw new InvalidOperationException("archive contains unsupported entry");
                ScriptSnapshot.ValidatePath(Name, true);
            }
            private static byte[] ReadEntry(ZipArchiveEntry Entry, int Limit, ref int Expanded)
            {
                if (Entry.Length < 0 || Entry.Length > Limit) throw new InvalidOperationException("archive entry exceeds its decompressed-byte limit");
                using (Stream Input = Entry.Open())
                using (var Output = new MemoryStream()) {
                    byte[] Buffer = new byte[8192]; int Read, Count = 0;
                    while ((Read = Input.Read(Buffer, 0, Buffer.Length)) != 0) {
                        Count = checked(Count + Read); Expanded = checked(Expanded + Read);
                        if (Count > Limit) throw new InvalidOperationException("archive entry exceeds its decompressed-byte limit");
                        if (Expanded > AddonPolicy.MaxExpandedBytes) throw new InvalidOperationException("archive expanded bytes exceed 8 MiB");
                        Output.Write(Buffer, 0, Read);
                    }
                    return Output.ToArray();
                }
            }
            private static string Decode(byte[] Bytes, string Label)
            {
                try {
                    int Start = Bytes.Length >= 3 && Bytes[0] == 239 && Bytes[1] == 187 && Bytes[2] == 191 ? 3 : 0;
                    string Result = AddonPolicy.Utf8.GetString(Bytes, Start, Bytes.Length - Start);
                    if (Result.IndexOf('\0') >= 0) throw new InvalidOperationException(Label + " contains NUL");
                    return Result;
                } catch (DecoderFallbackException) { throw new InvalidOperationException(Label + " must be valid UTF-8"); }
            }
            private static string HashBytes(byte[] Bytes)
            {
                using (var Hash = SHA256.Create()) {
                    byte[] Digest = Hash.ComputeHash(Bytes); var Result = new StringBuilder(Digest.Length * 2);
                    foreach (byte Value in Digest) Result.Append(Value.ToString("x2", CultureInfo.InvariantCulture));
                    return Result.ToString();
                }
            }

            private static void ParseManifest(string Text, out string Id, out string Version,
                out List<string> Required, out List<string> Optional)
            {
                Id = null; Version = null; Required = new List<string>(); Optional = new List<string>(); int? Schema = null;
                var Seen = new HashSet<string>(StringComparer.Ordinal);
                using (var Reader = new JsonTextReader(new StringReader(Text))) {
                    Reader.DateParseHandling = DateParseHandling.None; Reader.FloatParseHandling = FloatParseHandling.Decimal; Reader.MaxDepth = 8;
                    ReadToken(Reader); Expect(Reader, JsonToken.StartObject, "manifest must be a JSON object");
                    while (ReadToken(Reader) && Reader.TokenType != JsonToken.EndObject) {
                        Expect(Reader, JsonToken.PropertyName, "manifest property expected"); string Name = (string)Reader.Value;
                        if (!Seen.Add(Name)) throw new InvalidOperationException("duplicate manifest property: " + Name);
                        ReadToken(Reader);
                        if (Name == "schema") {
                            if (Reader.TokenType != JsonToken.Integer) throw new InvalidOperationException("schema must be integer 1");
                            try { Schema = Convert.ToInt32(Reader.Value, CultureInfo.InvariantCulture); }
                            catch { throw new InvalidOperationException("schema must be integer 1"); }
                        } else if (Name == "id") Id = ReadString(Reader, "id");
                        else if (Name == "version") Version = ReadString(Reader, "version");
                        else if (Name == "dependencies") ParseDependencies(Reader, Required, Optional);
                        else throw new InvalidOperationException("unknown manifest property: " + Name);
                    }
                    if (Reader.TokenType != JsonToken.EndObject || ReadToken(Reader)) throw new InvalidOperationException("manifest contains trailing JSON");
                }
                if (Schema != AddonPolicy.Schema) throw new InvalidOperationException("unsupported addon schema");
                AddonPolicy.ValidateId(Id); AddonPolicy.ValidateVersion(Version);
                if (Required.Contains(Id) || Optional.Contains(Id)) throw new InvalidOperationException("addon cannot depend on itself");
            }
            private static void ParseDependencies(JsonTextReader Reader, List<string> Required, List<string> Optional)
            {
                Expect(Reader, JsonToken.StartObject, "dependencies must be an object");
                var SeenNames = new HashSet<string>(StringComparer.Ordinal); var SeenIds = new HashSet<string>(StringComparer.Ordinal);
                while (ReadToken(Reader) && Reader.TokenType != JsonToken.EndObject) {
                    Expect(Reader, JsonToken.PropertyName, "dependency property expected"); string Name = (string)Reader.Value;
                    if (!SeenNames.Add(Name)) throw new InvalidOperationException("duplicate dependency property: " + Name);
                    ReadToken(Reader); List<string> Target = Name == "required" ? Required : Name == "optional" ? Optional : null;
                    if (Target == null) throw new InvalidOperationException("unknown dependency property: " + Name);
                    Expect(Reader, JsonToken.StartArray, "dependency list must be an array");
                    while (ReadToken(Reader) && Reader.TokenType != JsonToken.EndArray) {
                        string Id = ReadString(Reader, "dependency id"); AddonPolicy.ValidateId(Id);
                        if (!SeenIds.Add(Id)) throw new InvalidOperationException("duplicate dependency id: " + Id);
                        Target.Add(Id);
                        if (SeenIds.Count > AddonPolicy.MaxDependencies) throw new InvalidOperationException("dependencies exceed 32");
                    }
                    if (Reader.TokenType != JsonToken.EndArray) throw new InvalidOperationException("unterminated dependency list");
                }
                if (Reader.TokenType != JsonToken.EndObject) throw new InvalidOperationException("unterminated dependencies object");
            }
            private static bool ReadToken(JsonTextReader Reader)
            {
                bool Result = Reader.Read();
                if (Result && (Reader.TokenType == JsonToken.Comment || Reader.TokenType == JsonToken.Undefined))
                    throw new InvalidOperationException("manifest comments and undefined values are unsupported");
                return Result;
            }
            private static string ReadString(JsonTextReader Reader, string Label)
            {
                if (Reader.TokenType != JsonToken.String) throw new InvalidOperationException(Label + " must be a string");
                return (string)Reader.Value;
            }
            private static void Expect(JsonTextReader Reader, JsonToken Token, string Message)
            { if (Reader.TokenType != Token) throw new InvalidOperationException(Message); }
        }

        public sealed class AddonRegistry : IDisposable
        {
            private sealed class ReferenceComparer : IEqualityComparer<object>
            {
                public static readonly ReferenceComparer Instance = new ReferenceComparer();
                public new bool Equals(object Left, object Right) { return Object.ReferenceEquals(Left, Right); }
                public int GetHashCode(object Value) { return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(Value); }
            }
            private sealed class Registration
            {
                public object Provider; public string Token; public AddonPackageSnapshot Snapshot, PendingSnapshot;
                public AddonRegistrationState State; public RuntimeDomain Domain; public string Failure;
            }
            private readonly ScriptHost Host;
            private readonly long HostLifetimeId;
            private readonly int OwnerThread = Thread.CurrentThread.ManagedThreadId;
            private readonly Dictionary<string, Registration> Registrations = new Dictionary<string, Registration>(StringComparer.Ordinal);
            private readonly Dictionary<string, Registration> Ids = new Dictionary<string, Registration>(StringComparer.Ordinal);
            private readonly Dictionary<object, int> ProviderCounts = new Dictionary<object, int>(ReferenceComparer.Instance);
            private readonly Queue<string> Pending = new Queue<string>();
            private readonly HashSet<string> PendingSet = new HashSet<string>(StringComparer.Ordinal);
            private readonly Dictionary<string, object> Tombstones = new Dictionary<string, object>(StringComparer.Ordinal);
            private readonly Queue<string> TombstoneOrder = new Queue<string>();
            private long NextToken;
            private int AggregateSnapshotBytes;
            private bool Disposed;
            public AddonRegistry(ScriptHost Host, long HostLifetimeId) { this.Host = Host; this.HostLifetimeId = HostLifetimeId; }
            public bool HasPending { get {
                if (Pending.Count != 0) return true;
                foreach (Registration Value in Registrations.Values)
                    if (Value.State == AddonRegistrationState.Active && (Value.Domain == null || !Value.Domain.Alive)) return true;
                return false;
            } }
            public int Count { get { return Registrations.Count; } }
            public string[] RegisterArchive(object Provider, byte[] Archive)
            { try { return Register(Provider, AddonPackageSnapshot.FromArchive(Archive)); } catch (Exception Error) { return ErrorResponse(Error); } }
            public string[] RegisterSource(object Provider, string Id, string Version, byte[] Source)
            { try { return Register(Provider, AddonPackageSnapshot.FromSource(Id, Version, Source)); } catch (Exception Error) { return ErrorResponse(Error); } }
            private string[] Register(object Provider, AddonPackageSnapshot Snapshot)
            {
                CheckOwner(); CheckLive(); if (Provider == null) throw new InvalidOperationException("provider object is required");
                if (Registrations.Count >= AddonPolicy.MaxRegistrations) throw new InvalidOperationException("registration limit reached");
                int ProviderCount; ProviderCounts.TryGetValue(Provider, out ProviderCount);
                if (ProviderCount >= AddonPolicy.MaxRegistrationsPerProvider) throw new InvalidOperationException("provider registration limit reached");
                if (Ids.ContainsKey(Snapshot.Id)) throw new InvalidOperationException("addon id is already reserved");
                if (AggregateSnapshotBytes > AddonPolicy.MaxAggregateSnapshotBytes - Snapshot.SourceBytes)
                    throw new InvalidOperationException("aggregate package snapshots exceed 32 MiB");
                if (NextToken == Int64.MaxValue) throw new InvalidOperationException("registration token space exhausted");
                string Token = HostLifetimeId.ToString("x16", CultureInfo.InvariantCulture) + "-" + (++NextToken).ToString("x16", CultureInfo.InvariantCulture);
                var Value = new Registration {Provider = Provider, Token = Token, Snapshot = Snapshot,
                    State = Snapshot.DependencyCount == 0 ? AddonRegistrationState.Registered : AddonRegistrationState.Blocked,
                    Failure = Snapshot.DependencyCount == 0 ? "" : "dependency resolution is deferred in Foundation B"};
                Registrations.Add(Token, Value); Ids.Add(Snapshot.Id, Value); ProviderCounts[Provider] = ProviderCount + 1;
                AggregateSnapshotBytes += Snapshot.SourceBytes;
                if (Value.State == AddonRegistrationState.Registered) Enqueue(Value);
                return Response(Value);
            }
            public string[] Status(object Provider, string Token)
            {
                try { CheckOwner(); CheckLive(); Registration Value = FindOwned(Provider, Token); Reconcile(Value); return Response(Value); }
                catch (Exception Error) { return ErrorResponse(Error); }
            }
            public string[] ReplaceArchive(object Provider, string Token, byte[] Archive)
            { try { return Replace(Provider, Token, AddonPackageSnapshot.FromArchive(Archive)); } catch (Exception Error) { return ErrorResponse(Error); } }
            public string[] ReplaceSource(object Provider, string Token, string Version, byte[] Source)
            {
                try {
                    CheckOwner(); CheckLive(); Registration Value = FindOwned(Provider, Token);
                    return Replace(Provider, Token, AddonPackageSnapshot.FromSource(Value.Snapshot.Id, Version, Source));
                } catch (Exception Error) { return ErrorResponse(Error); }
            }
            private string[] Replace(object Provider, string Token, AddonPackageSnapshot Snapshot)
            {
                CheckOwner(); CheckLive(); Registration Value = FindOwned(Provider, Token);
                if (Value.State == AddonRegistrationState.Stopping) throw new InvalidOperationException("registration is stopping");
                if (!String.Equals(Value.Snapshot.Id, Snapshot.Id, StringComparison.Ordinal)) throw new InvalidOperationException("replacement id must match reserved id");
                if (Snapshot.DependencyCount != 0) throw new InvalidOperationException("dependency-bearing replacement is blocked until dependency resolution exists");
                if (Value.PendingSnapshot != null || Value.State == AddonRegistrationState.Initializing) throw new InvalidOperationException("replacement is already pending");
                if (AggregateSnapshotBytes > AddonPolicy.MaxAggregateSnapshotBytes - Snapshot.SourceBytes)
                    throw new InvalidOperationException("aggregate package snapshots exceed 32 MiB");
                AggregateSnapshotBytes += Snapshot.SourceBytes;
                Value.PendingSnapshot = Snapshot; Value.Failure = ""; Enqueue(Value); return Response(Value);
            }
            public string[] Unregister(object Provider, string Token)
            {
                try {
                    CheckOwner(); CheckLive(); Registration Value;
                    if (!Registrations.TryGetValue(Token ?? "", out Value)) {
                        object Owner;
                        if (Token != null && Tombstones.TryGetValue(Token, out Owner) && Object.ReferenceEquals(Owner, Provider))
                            return new[] {"OK", Token, "Stopping", "", "", "", "", "", ""};
                        throw new InvalidOperationException("stale registration token");
                    }
                    if (!Object.ReferenceEquals(Value.Provider, Provider)) throw new InvalidOperationException("registration owner mismatch");
                    Stop(Value); return new[] {"OK", Token, "Stopping", Value.Snapshot.Id, Value.Snapshot.Version, "", Value.Snapshot.Hash, "", ""};
                } catch (Exception Error) { return ErrorResponse(Error); }
            }
            public int UnloadProvider(object Provider)
            {
                CheckOwner(); if (Disposed || Provider == null) return 0;
                var Owned = new List<Registration>();
                foreach (Registration Value in Registrations.Values) if (Object.ReferenceEquals(Value.Provider, Provider)) Owned.Add(Value);
                foreach (Registration Value in Owned) Stop(Value);
                return Owned.Count;
            }
            public bool ProcessOne()
            {
                CheckOwner(); if (Disposed) return false; ReconcileAll();
                if (!Host.Ready || Host.Busy) return false;
                Registration Value = null;
                while (Pending.Count != 0 && Value == null) {
                    string Token = Pending.Dequeue(); PendingSet.Remove(Token);
                    Registration Candidate;
                    if (Registrations.TryGetValue(Token, out Candidate) && Candidate.State != AddonRegistrationState.Stopping &&
                        (Candidate.State == AddonRegistrationState.Registered || Candidate.PendingSnapshot != null)) Value = Candidate;
                }
                if (Value == null) return false;
                AddonPackageSnapshot CandidateSnapshot = Value.PendingSnapshot ?? Value.Snapshot;
                RuntimeDomain Previous = Value.Domain; bool Replacing = Previous != null && Previous.Alive;
                Value.State = AddonRegistrationState.Initializing; Value.Failure = "";
                AddonActivation Activation = Host.ActivateAddon(CandidateSnapshot, Previous);
                if (!Registrations.ContainsKey(Value.Token) || Value.State == AddonRegistrationState.Stopping) {
                    if (Activation.Domain != null) Host.RetireAddon(Activation.Domain); return true;
                }
                if (Activation.Result.Status == RuntimeStatus.OK && Activation.Domain != null) {
                    AggregateSnapshotBytes -= Value.Snapshot.SourceBytes;
                    Value.Snapshot = CandidateSnapshot; Value.PendingSnapshot = null; Value.Domain = Activation.Domain;
                    Value.State = AddonRegistrationState.Active; Value.Failure = "";
                } else {
                    if (Value.PendingSnapshot != null) AggregateSnapshotBytes -= CandidateSnapshot.SourceBytes;
                    Value.PendingSnapshot = null;
                    Value.Failure = AddonPolicy.Diagnostic((Replacing ? "replacement failed: " : "initialization failed: ") +
                        Activation.Result.Status + (String.IsNullOrEmpty(Activation.Result.Error) ? "" : " " + Activation.Result.Error));
                    if (Replacing && Previous.Alive) { Value.Domain = Previous; Value.State = AddonRegistrationState.Active; }
                    else { Value.Domain = null; Value.State = AddonRegistrationState.Failed; }
                }
                return true;
            }
            private void ReconcileAll() { foreach (Registration Value in Registrations.Values) Reconcile(Value); }
            private void Reconcile(Registration Value)
            {
                if (Value.State == AddonRegistrationState.Active && (Value.Domain == null || !Value.Domain.Alive)) {
                    Value.Domain = null; Value.State = AddonRegistrationState.Registered;
                    Value.Failure = "VM generation retired; reconstruction queued"; Enqueue(Value);
                }
            }
            private void Enqueue(Registration Value) { if (PendingSet.Add(Value.Token)) Pending.Enqueue(Value.Token); }
            private Registration FindOwned(object Provider, string Token)
            {
                Registration Value;
                if (Provider == null || Token == null || !Registrations.TryGetValue(Token, out Value)) throw new InvalidOperationException("stale registration token");
                if (!Object.ReferenceEquals(Value.Provider, Provider)) throw new InvalidOperationException("registration owner mismatch");
                return Value;
            }
            private void Stop(Registration Value)
            {
                Value.State = AddonRegistrationState.Stopping; RemovePending(Value.Token);
                if (Value.Domain != null) { Host.RetireAddon(Value.Domain); Value.Domain = null; }
                Registrations.Remove(Value.Token); Ids.Remove(Value.Snapshot.Id);
                int Count = ProviderCounts[Value.Provider] - 1;
                if (Count == 0) ProviderCounts.Remove(Value.Provider); else ProviderCounts[Value.Provider] = Count;
                AggregateSnapshotBytes -= Value.Snapshot.SourceBytes;
                if (Value.PendingSnapshot != null) AggregateSnapshotBytes -= Value.PendingSnapshot.SourceBytes;
                AddTombstone(Value.Token, Value.Provider);
                Value.PendingSnapshot = null;
            }
            private void AddTombstone(string Token, object Provider)
            {
                Tombstones[Token] = Provider; TombstoneOrder.Enqueue(Token);
                while (TombstoneOrder.Count > AddonPolicy.MaxTombstones) Tombstones.Remove(TombstoneOrder.Dequeue());
            }
            private void RemovePending(string Token)
            {
                if (!PendingSet.Remove(Token)) return;
                int Count = Pending.Count;
                for (int Index = 0; Index < Count; ++Index) {
                    string Value = Pending.Dequeue();
                    if (!String.Equals(Value, Token, StringComparison.Ordinal)) Pending.Enqueue(Value);
                }
            }
            private string[] Response(Registration Value)
            {
                return new[] {"OK", Value.Token, Value.State.ToString(), Value.Snapshot.Id, Value.Snapshot.Version,
                    Value.Failure ?? "", Value.Snapshot.Hash, Value.Domain == null ? "" : Value.Domain.DomainLifetimeId.ToString(CultureInfo.InvariantCulture),
                    Value.PendingSnapshot == null ? "" : Value.PendingSnapshot.Version};
            }
            private static string[] ErrorResponse(Exception Error)
            { return new[] {"ERROR", "", "", "", "", AddonPolicy.Diagnostic(Error is InvalidOperationException ? Error.Message : "provider operation failed"), "", "", ""}; }
            private void CheckOwner() { if (Thread.CurrentThread.ManagedThreadId != OwnerThread) throw new InvalidOperationException("addon registry owner-thread required"); }
            private void CheckLive() { if (Disposed) throw new InvalidOperationException("stale CarbonLuau host lifetime"); }
            public void Dispose()
            {
                CheckOwner(); if (Disposed) return;
                var Values = new List<Registration>(Registrations.Values);
                foreach (Registration Value in Values) Stop(Value);
                Disposed = true; Pending.Clear(); PendingSet.Clear(); Tombstones.Clear(); TombstoneOrder.Clear(); ProviderCounts.Clear(); Ids.Clear();
            }
        }

        internal sealed class AddonActivation
        {
            public ExecutionResult Result; public RuntimeDomain Domain;
        }

        public sealed partial class ScriptHost
        {
            private readonly SortedDictionary<long, RuntimeDomain> AddonDomains = new SortedDictionary<long, RuntimeDomain>();
            public ulong DomainCount { get { return Vm == null || !Vm.Alive ? 0 : Native.GenerationInfo(Vm.Handle).Domains; } }
            internal AddonActivation ActivateAddon(AddonPackageSnapshot Package, RuntimeDomain Previous)
            {
                Native.CheckOwner(); var Activation = new AddonActivation {Result = new ExecutionResult {Status = RuntimeStatus.INVALID_ARGUMENT, Error = "runtime unavailable or busy"}};
                if (!Ready || Busy) return Activation;
                RuntimeDomain Candidate = null; Busy = true;
                try {
                    ScriptSnapshot Snapshot = Package.ToScriptSnapshot();
                    Candidate = new RuntimeDomain(Native, Vm, Settings, Snapshot);
                    if (Facade != null) Candidate.Facade(new FacadeSession(Facade, Candidate.VmGenerationId, Candidate.DomainLifetimeId, Settings.MaxQueuedCallbacks));
                    ExecutionResult Result = Candidate.Execute("addon." + Package.Id + ".init", Snapshot.EntrySource, Settings.MaxCallbackMilliseconds);
                    Activation.Result = Result;
                    if (Result.Status != RuntimeStatus.OK) {
                        if (Result.Retired || Vm.Info.Ready == 0) {
                            var Recovery = Recover();
                            Result.Error += "; root recovery=" + (Recovery == null ? "exhausted" : Recovery.Status.ToString());
                        }
                        return Activation;
                    }
                    Candidate.Commit();
                    if (Facade != null) Facade.CommitAddon(Previous == null ? null : Previous.FacadeSession, Candidate.FacadeSession);
                    AddonDomains.Add(Candidate.DomainLifetimeId, Candidate);
                    if (Previous != null) { AddonDomains.Remove(Previous.DomainLifetimeId); Previous.Dispose(); }
                    Activation.Domain = Candidate; Candidate = null; return Activation;
                } catch (Exception Error) {
                    Activation.Result = new ExecutionResult {Status = RuntimeStatus.INTERNAL_ERROR,
                        Error = AddonPolicy.Diagnostic(Error is InvalidOperationException ? Error.Message : "addon native initialization failed")};
                    return Activation;
                } finally {
                    if (Candidate != null) {
                        if (Candidate.FacadeSession != null && Facade != null) Facade.Retire(Candidate.FacadeSession);
                        Candidate.Dispose();
                    }
                    Busy = false;
                }
            }
            internal void RetireAddon(RuntimeDomain Domain)
            {
                Native.CheckOwner(); if (Domain == null) return;
                AddonDomains.Remove(Domain.DomainLifetimeId);
                if (Domain.FacadeSession != null && Facade != null) Facade.Retire(Domain.FacadeSession);
                Domain.Dispose();
            }
            private void ReleaseAddonDomains()
            {
                var Values = new List<RuntimeDomain>(AddonDomains.Values); AddonDomains.Clear();
                foreach (RuntimeDomain Domain in Values) {
                    if (Domain.FacadeSession != null && Facade != null) Facade.Retire(Domain.FacadeSession);
                    Domain.Dispose();
                }
            }
            private void FlushAddonFacades(Stopwatch Watch)
            {
                foreach (RuntimeDomain Domain in AddonDomains.Values) {
                    if (Watch.Elapsed.TotalMilliseconds >= Settings.FrameDrainBudgetMilliseconds) break;
                    if (Domain.Alive && Domain.FacadeSession != null) Domain.FacadeSession.Flush(Domain, Watch, Settings.FrameDrainBudgetMilliseconds);
                }
            }
        }
    }
}
