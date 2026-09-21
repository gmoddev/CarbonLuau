# Tooling contract models

Normative model details under [ToolingBaseline](ToolingBaseline.md). PascalCase is
used for project-owned model fields; upstream VS Code manifest/LSP/framing names
retain their required spellings. Examples in the historical design are conceptual,
not an already deployed wire format. Nothing here claims an executable protocol.

## API catalog

Schema 1 is [carbonluau-api.schema.json](../api/carbonluau-api.schema.json).
`SchemaVersion`, `Api {Name, Version, Status}`, `Types`, `Members` and `LimitKeys`
are mandatory. Types have stable IDs and kinds Service, Class, Value, Enum,
Singleton or Global; optional BaseTypeId expresses inheritance. Members name an
OwnerId and have Property, Method, Signal, Constructor or Singleton kind.
Structured signatures carry named/typed optional/variadic parameters and return
lists. Type expressions are Luau type syntax consumed only by the qualified
definition generator, never evaluated. Signals use signatures for callback shape.
Properties carry ValueType and Writable. Every declaration has documentation,
availability (SinceApi, optional DeprecatedSince/RemovedSince, Implemented,
Qualification) and Preview behavior. Qualification is distinct from availability:
an implemented client-unqualified GUI member still exists in the runtime.

Foundation A's catalog loader must additionally enforce uniqueness of IDs,
OwnerId/BaseTypeId references, acyclic inheritance, valid type references, kind
constraints, signature ordering and implemented/version/qualification consistency.
JSON Schema alone cannot prove these relationships or runtime coverage. Empty
collections are useful schema fixtures, not a valid shipping API catalog.

LimitKeys are stable references; actual limit values are exported by shared code
into `generated/tooling-metadata.json` with semantic/API origin. No duplicated
numeric policy. `api/carbonluau-api.json` will own public signatures/docs while
`GuiDescriptors.cs` and binding declarations remain implementation evidence.
Refactor declaration shape to consume generated descriptor/binding tables where
possible; otherwise require explicit contract IDs and executable two-way audits.
Do not infer a complete API by regular expressions, runtime reflection or Markdown.

Generation inputs: selected catalog, shared descriptors/policy export, release
identity and exact adapter pin. Outputs: `generated/carbonluau.d.luau`,
`generated/carbonluau-docs.json`, `generated/tooling-metadata.json`, generated
reference tables. Stable ordinal ID ordering, UTF-8 without BOM, LF, invariant
numeric formatting and no timestamps/absolute paths make output reproducible.
Generation `--check` must fail on differences rather than silently rewrite.
The schema check added here is a seam only: runtime/catalog coverage and generated
definition determinism cannot pass until Foundation A implements that pipeline.

## Local protocol

Identity `CarbonLuau.Tooling`, major 1, minor 0; preview schema 1. Neither is
`CarbonLuau.Addons/1.2`, the native ABI, a release package version or CUI JSON.
UTF-8 JSON bodies use ASCII `Content-Length: <byte count>\r\n\r\n` on local stdio.
Bound header accumulation to 4096 bytes and body to 8 MiB before allocation;
reject duplicate/malformed lengths, non-UTF-8, duplicate JSON keys and overflow.
Stdout carries protocol only; stderr carries separately bounded operational logs.

Request fields: `Protocol {Name, Major, Minor}`, `Id`, `Method`, `Params`, and
`ProjectRevision` for project operations. Id is an integer 1..2147483647, unique
while outstanding; no strings/floating point coercion. A project revision is
`sha256:` followed by 64 lowercase hex digits over the immutable source graph,
selected API/pack and operation inputs (including preview viewport). Response
echoes protocol, Id and revision and contains exactly one of Result or Error.
Error has stable Code and bounded Message; optional structured source diagnostics
use logical source IDs/ranges, never raw exception objects. Logs are bounded
notifications with no request Id. Unknown fields/methods must never enable an
unadvertised capability. The server validates each operation's schema before work.

Reserved methods: initialize, getMetadata, validateProject, resolveProjectGraph,
buildAddon, preview, shutdown. No public compile or inspectObject round trip.
Unsupported/unimplemented operations return controlled errors, not success stubs.

Initialize must precede work. The client supplies protocol support, extension
version, requested API/schema, platform and capabilities; the host returns selected
protocol, exact semantic/build and pack identities, API/schema support, runtime
Luau pin, preview schema, limits and implemented capabilities. Reject unknown major,
API, package schema, preview schema or incompatible pack; negotiate minor only
within explicit supported capabilities. No silent fallback. Other requests before
initialization fail closed. Shutdown stops admission, cancels/reaps workers and
closes streams. EOF/crash also tears down children.

Bound outstanding requests and queued work before admission. Foundation A's static
transport starts with one active project request per project and a bounded latest
revision slot; never an unbounded request queue. Cancellation control identifies
the original Id/revision, terminates its worker if present and forbids applying
its late result. Consumer must compare revision even if cancellation raced with
completion. Foundation B qualifies full supervision/fuzz/restart behavior. Exact
per-operation payload schemas land with operations, not permissive Any payloads
mistaken for implemented validation.

## Preview plan

`ToolingPreviewPlan` schema 1 is produced and validated in CarbonLuau tooling.
The backend-neutral model consists of:

| Field | Required semantic content |
|---|---|
| Schema, ProjectRevision, SemanticRevision, ApiVersion | exact identity and stale-result rejection |
| Viewport | positive finite pixel width/height used for canonical projection |
| Screen | opaque screen ID/name, optional source, aggregate metrics |
| Nodes | bounded retained hierarchy including non-painting layout helpers |
| Node Id, ParentId, ClassName, Name | unique opaque revision-local identities, no cycles/missing parents |
| Retained | typed immutable inspection values; no executable values/handles |
| Projected | final RectPx, effective visibility, layout owner, effective clip rectangle/topology/depth and paint order |
| Paint | kind, text, colors/transparency, image identity/placeholder, font identity; no arbitrary URLs/HTML |
| Scroll | retained canvas configuration, resolved content/viewport geometry, labeled preview-local intent; never readable runtime CanvasPosition |
| Accounting | stable limit keys, measured values/bounds and origin, projected cost including private projection nodes |
| Source | optional canonical module/source ID and one-based creation line; absence stays unknown |
| Fidelity | per-aspect Authoritative, Approximate or ConvenienceOnly; no client delivery/authentication claim |

All numeric geometry must be finite. Serialize parents before children; validate
IDs, references, property kinds, accounting and bounded text/node counts before
forwarding. The 8 MiB frame ceiling is additional to canonical GUI resource limits.
Retained hierarchy and projected paint nodes are distinct: private clip/scroll
projection costs cannot disappear because the inspector shows only retained nodes.
No CUI payload, production action/connection token or server identifier is allowed.
The concrete serializer/schema and golden fixtures are Foundation B work; this
baseline fixes ownership and required content without inventing an unused renderer.

## Tooling pack

A pack is an immutable release manifest plus platform payloads, not an API version
alias. Required manifest fields before shipping:

| Identity/content | Contract |
|---|---|
| PackVersion / ManifestSchema | independent immutable pack release and manifest format |
| SemanticRevision / ToolingBuildId | exact CarbonLuau source commit and build identity |
| Protocol / PreviewPlanSchema / ApiMetadataSchema | explicit supported contracts |
| Api identities / PackageSchemas | supported author-facing targets; no implicit latest |
| RuntimeLuauRevision / ToolingNativeAbi | exact VM/compiler and optional tooling-native boundary |
| LanguageServerVersion / LanguageServerLuauRevision | pinned upstream binary and exact embedded Luau commit |
| TransformRevision / ConfigurationDigest | compatible trusted analysis plugin and flags |
| AnalysisSecurityPolicyVersion / AnalysisProfile | policy 1 TrustedSnapshotAnalysis; trusted-workspace execution, not a non-executing LSP claim |
| AnalysisContainmentProfile / qualification evidence | exact platform resource/lifetime controls; distinguish hard limits, soft monitoring and OS sandbox claims |
| Platforms | exact target with host, native, LSP, definitions/docs/metadata paths and SHA-256 for every artifact |
| Provenance / Licenses / Verification | build provenance, notices and signed manifest or equivalent verifiable attestation |

The installed extension version lives in its manifest and participates in
negotiation but is not derived from PackVersion. Packaging verifies hashes,
platform, contracts and required files before enabling the pack. Local payload
paths must be relative, confined and non-symlink escaping. Runtime release.json
is unchanged; no fabricated pack version, LSP pin or native ABI is assigned until
qualified artifacts exist. The extension baseline version `0.0.1` is development
bootstrap identity only, not a Marketplace or scripting API release.

The [D19 analysis amendment](ToolingLanguageAnalysisSecurity.md) requires bounded
LSP proxy framing and scoped URI/response admission separately from the static
host protocol. A boolean LanguageServerQualified cannot replace policy/pack/
platform compatibility checks. Unknown analysis policy disables language features;
independently compatible static inspection may remain available. No new protocol
operation or executable launcher is implemented by this documentation amendment.
