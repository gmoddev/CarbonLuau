using System;
using System.IO;
using Newtonsoft.Json.Linq;

// Test input authority is the pinned manifest, independently from either generated check.
internal static class LootItemManifestTests
{
    internal readonly struct Definition
    {
        internal readonly string Name, Family;
        internal readonly int Id, Stack;
        internal Definition(string Name, int Id, int Stack, string Family)
        { this.Name = Name; this.Id = Id; this.Stack = Stack; this.Family = Family; }
    }

    internal static readonly Definition[] Definitions = Read();

    private static Definition[] Read()
    {
        using (Stream Stream = typeof(LootItemManifestTests).Assembly.GetManifestResourceStream("LootItemsManifest.json")) {
            if (Stream == null || Stream.Length > 65536) throw new InvalidOperationException("bounded pinned Loot test manifest unavailable");
            using (var Reader = new StreamReader(Stream)) {
                var Manifest = JObject.Parse(Reader.ReadToEnd());
                var Rows = (JArray)Manifest["Definitions"];
                if ((int)Manifest["SchemaVersion"] != 1 || Rows.Count == 0 || Rows.Count > 512)
                    throw new InvalidOperationException("invalid pinned Loot test manifest");
                var Result = new Definition[Rows.Count]; string Previous = null;
                for (int Index = 0; Index < Result.Length; Index++) {
                    var Row = (JObject)Rows[Index]; string Name = (string)Row["Name"];
                    if (String.IsNullOrEmpty(Name) || Name.Length > 15 || Previous != null && StringComparer.Ordinal.Compare(Previous, Name) >= 0)
                        throw new InvalidOperationException("Loot test names are not canonical bounded ordinal identities");
                    Result[Index] = new Definition(Name, (int)Row["Id"], (int)Row["Stack"], (string)Row["Family"]);
                    Previous = Name;
                }
                return Result;
            }
        }
    }

    internal static bool Contains(string Name)
    {
        foreach (Definition Entry in Definitions)
            if (String.Equals(Entry.Name, Name, StringComparison.Ordinal)) return true;
        return false;
    }
}
