using CarbonLuau.Core;

namespace CarbonLuau.Tooling;

internal sealed record PreviewProject(string Id, string EntryName, PackageSources Sources, AddonPackageSnapshot? Package);
