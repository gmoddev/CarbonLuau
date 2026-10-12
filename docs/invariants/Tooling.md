# Tooling: canonical detail

This page contains normative detail delegated by the [canonical register](../Invariants.md).
It reorganizes existing rules; it creates no second authority or new contract.
Existing decision/criteria text is retained; qualification remains separately scoped.

<!-- BEGIN RECORD Invariants.md -->

#### D19 — official editor tooling

Accepted architecture, implemented through Tooling Foundations A–C. CarbonLuau owns shared
Core semantics, API metadata, generated definitions, package/module validation,
tooling coordinator/worker and preview-plan generation. The separate
`gmoddev/carbonluau-vscode` repository owns editor integration and paint-only UX.
Any rule deciding project/package/GUI validity or deterministic GUI geometry and
resource accounting belongs to canonical CarbonLuau code or generated metadata,
never an independent TypeScript implementation.

[ToolingBaseline.md](../ToolingBaseline.md) owns the detailed tooling contracts,
security, identities and repository boundary under this decision;
[ToolingFoundationA.md](../ToolingFoundationA.md) owns implementation routing.
The [D19 language-analysis amendment](../ToolingLanguageAnalysisSecurity.md) owns
the distinction between StaticInspection, ExecutableAnalysis and PreviewExecution.
Restricted Mode permits only StaticInspection. ExecutableAnalysis requires
Workspace Trust and a qualified tooling-supervised LSP process over bounded
snapshots: type functions and the pack-owned transform may execute there;
workspace configuration/plugins may not. Workspace-controlled code never runs
in extension host, WebView or static coordinator. Process separation, VM limits,
Workspace Trust and OS sandboxing are distinct; no portable OS sandbox is claimed.
Trust never selects tool paths or relaxes analysis bounds. Preview gates remain
unchanged. The amendment resolves the architectural stop condition, not the
partial Foundation A implementation's qualification gates.
The [supplied design](../design/CarbonLuau_VSCode_Tooling_Architecture.md) is supporting
evidence at its recorded revision. Adoption changes no runtime API, ABI, provider
protocol, package schema, release identity or authenticated-client qualification.
Do not infer authorization for preview, fixtures, live integration, debugging or
visual authoring from the existence of this baseline.

Tooling A/B qualification records establish the pinned language pairing and
Windows/Linux worker controls; the extension's Foundation C record establishes
the paint-only preview consumer. macOS remains static-only. Distribution
signing/attestation and publication administration remain separate gates; local
release-candidate VSIX packaging is not a signature or publication claim.

<!-- END RECORD Invariants.md -->

