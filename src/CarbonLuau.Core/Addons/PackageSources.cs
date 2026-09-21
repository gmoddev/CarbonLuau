using System;
using System.Collections.Generic;

namespace CarbonLuau.Core
{
    public sealed class PackageSources
    {
        public readonly string EntrySource;
        private readonly SortedDictionary<string, string> Values;
        public SortedDictionary<string, string> Modules { get { return new SortedDictionary<string, string>(Values, StringComparer.Ordinal); } }
        public PackageSources(string EntrySource, SortedDictionary<string, string> Modules)
        { this.EntrySource = EntrySource; Values = new SortedDictionary<string, string>(Modules, StringComparer.Ordinal); }
    }
}
