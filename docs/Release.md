# Release identity

This source snapshot maps package 0.7.0 / planned v0.7.0
to API 0.7.0-experimental. Publication, source preparation, API assignment and
subsystem qualification are separate decisions. ABI/provider/schema/Luau remain
independent values in release.json.

PUBLICATION.json maps the independent public tree to its exact development
source and transformation policy. Future public builds must record their public
commit plus publication tree digest; do not put inaccessible private commit URLs
in installation instructions or editor pins. No snapshot creates a release tag.

Historical published assets and their checksums must remain immutable. Migration
must preserve an explicitly approved public archive and link map; a new public
repository does not inherit old commit URLs, releases, stars, forks or redirects.
New public tags identify independently qualified public commits, never private
objects imported to make an old link resolve.

## Historical binaries

Archived experimental binaries remain available as [v0.3.0](https://github.com/gmoddev/CarbonLuau/releases/tag/v0.3.0),
[v0.4.0](https://github.com/gmoddev/CarbonLuau/releases/tag/v0.4.0),
[v0.5.0](https://github.com/gmoddev/CarbonLuau/releases/tag/v0.5.0) and
[v0.7.0](https://github.com/gmoddev/CarbonLuau/releases/tag/v0.7.0).
They retain original checksums/provenance and release-specific API/host limits.
The independently rooted archive source tags are curated projections; original
build revisions remain audit identifiers in unchanged provenance, not ancestors
of this public history. Old releases are not a current-host support promise.
The v0.4.0 editor assets remain historical; no new editor artifact is published.
Current 0.7.0 source and its examples must not be mistaken for
newly distributed runtime binaries. No v0.7.5 binary release exists from this cutover.
