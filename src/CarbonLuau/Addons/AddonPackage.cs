using System;
using System.Text;

namespace Carbon.Plugins
{
    public partial class CarbonLuau
    {
        public enum AddonRegistrationState { Registered, Blocked, Initializing, Active, Failed, Stopping }
        public static class AddonPolicy
        {
            public const string ProtocolName = global::CarbonLuau.Core.AddonPolicy.ProtocolName;
            public const string ProtocolVersion = global::CarbonLuau.Core.AddonPolicy.ProtocolVersion;
            public const int Schema = global::CarbonLuau.Core.AddonPolicy.Schema;
            public const int MaxArchiveBytes = global::CarbonLuau.Core.AddonPolicy.MaxArchiveBytes;
            public const int MaxExpandedBytes = global::CarbonLuau.Core.AddonPolicy.MaxExpandedBytes;
            public const int MaxManifestBytes = global::CarbonLuau.Core.AddonPolicy.MaxManifestBytes;
            public const int MaxSourceBytes = global::CarbonLuau.Core.AddonPolicy.MaxSourceBytes;
            public const int MaxAggregateSourceBytes = global::CarbonLuau.Core.AddonPolicy.MaxAggregateSourceBytes;
            public const int MaxSourceModules = global::CarbonLuau.Core.AddonPolicy.MaxSourceModules;
            public const int MaxArchiveEntries = global::CarbonLuau.Core.AddonPolicy.MaxArchiveEntries;
            public const int MaxPathCharacters = global::CarbonLuau.Core.AddonPolicy.MaxPathCharacters;
            public const int MaxPathDepth = global::CarbonLuau.Core.AddonPolicy.MaxPathDepth;
            public const int MaxDependencies = global::CarbonLuau.Core.AddonPolicy.MaxDependencies;
            public const int MaxRegistrations = global::CarbonLuau.Core.AddonPolicy.MaxRegistrations;
            public const int MaxRegistrationsPerProvider = global::CarbonLuau.Core.AddonPolicy.MaxRegistrationsPerProvider;
            public const int MaxGraphEdges = global::CarbonLuau.Core.AddonPolicy.MaxGraphEdges;
            public const int MaxAggregateSnapshotBytes = global::CarbonLuau.Core.AddonPolicy.MaxAggregateSnapshotBytes;
            public const int MaxDiagnosticCharacters = global::CarbonLuau.Core.AddonPolicy.MaxDiagnosticCharacters;
            public const int MaxTombstones = global::CarbonLuau.Core.AddonPolicy.MaxTombstones;
            public static readonly UTF8Encoding Utf8 = global::CarbonLuau.Core.AddonPolicy.Utf8;
            public static void ValidateId(string Value) { global::CarbonLuau.Core.AddonPolicy.ValidateId(Value); }
            public static void ValidateVersion(string Value) { global::CarbonLuau.Core.AddonPolicy.ValidateVersion(Value); }
            public static string Diagnostic(string Value) { return global::CarbonLuau.Core.AddonPolicy.Diagnostic(Value); }
        }
        public sealed class AddonPackageSnapshot
        {
            private readonly global::CarbonLuau.Core.AddonPackageSnapshot Value;
            public readonly string Id, Version, Hash, Main;
            public readonly int SourceBytes;
            public int DependencyCount { get { return Value.DependencyCount; } }
            private AddonPackageSnapshot(global::CarbonLuau.Core.AddonPackageSnapshot Value)
            { this.Value = Value; Id = Value.Id; Version = Value.Version; Hash = Value.Hash; Main = Value.Main; SourceBytes = Value.SourceBytes; }
            public string[] Dependencies(bool Optional) { return Value.Dependencies(Optional); }
            public string[] PublicModules() { return Value.PublicModules(); }
            public ScriptSnapshot ToScriptSnapshot()
            {
                var Sources = Value.GetSources();
                var Result = new ScriptSnapshot { EntryName = "init.luau", EntrySource = Sources.EntrySource };
                foreach (var Module in Sources.Modules) Result.Modules.Add(Module.Key, Module.Value);
                return Result;
            }
            public static AddonPackageSnapshot FromSource(string Id, string Version, byte[] Source)
            { return new AddonPackageSnapshot(global::CarbonLuau.Core.AddonPackageSnapshot.FromSource(Id, Version, Source)); }
            public static AddonPackageSnapshot FromArchive(byte[] ArchiveBytes)
            { return new AddonPackageSnapshot(global::CarbonLuau.Core.AddonPackageSnapshot.FromArchive(ArchiveBytes)); }
        }
    }
}
