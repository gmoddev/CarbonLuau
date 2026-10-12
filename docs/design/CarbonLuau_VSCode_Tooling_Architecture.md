# Tooling design and deferred roadmap

[D19](../invariants/Tooling.md), [ToolingBaseline](../ToolingBaseline.md),
[ToolingContracts](../ToolingContracts.md) and the
[analysis security profile](../ToolingLanguageAnalysisSecurity.md) are current
authority. The [complete supplied architecture](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture)
is preserved as original supporting source, with its superseded execution and
phase assumptions clearly historical. Do not reinterpret it as current availability.

## Implemented foundation

[A completion](../ToolingFoundationACompletion.md) records canonical Core/catalog/
generation, bounded project/static host, and qualified trusted Windows/Linux
supervised analysis. [B completion](../ToolingFoundationBCompletion.md) records
bounded GUI semantic preview/plan integration and its determinism/platform limits.
Restricted Mode remains non-executing; trusted analysis is not a portable OS sandbox.
Tooling and runtime pins/identities remain distinct. macOS tooling evidence does
not establish macOS server runtime or unmeasured containment.

## Retained future seams, not implementation approval

- The GUI inspector/preview WebView consumes bounded canonical render/inspection
  data. It is paint-only: no alternate layout/validity semantics, raw CUI access,
  simulated host success or authenticated-client claims.
- Open GUI Inspector is a same-panel focus/open seam, not another independent
  state model. Source IDs and revision checks remain bounded and authority-scoped.
- Workspace Trust, strict CSP, packaged resources, no injected HTML/eval/remote
  assets, validated messages/state and constrained local resource roots remain
  the baseline for an eventual WebView. Source text remains authoritative.
- Visual authoring proposes explicit source edits, not a second .gui format or
  automatic rewrite of arbitrary Luau. Future debugging is a separate adapter.
- Matching offline verified packs retain independent extension/protocol/catalog/
  preview/native/LSP/runtime identities. No workspace-selected executable/plugin,
  silent definition downloads, telemetry or unqualified pack publication is implied.
- Live integration stays disabled without separately authorized operator-enabled,
  authenticated capability-scoped bounded transport; normal tooling needs no server.

Later task sequencing must follow the current baseline/contracts and its separately
authorized phase routing, not the original supplied document's phase labels.
No new command, setting, GUI mock, server endpoint, release or editor publication
is implemented by documentation consolidation.

## Historical section links

These compatibility anchors route to the dated original, not a current gate.

<a id="carbonluau-vs-code-extension-and-tooling-architecture"></a>

- [CarbonLuau VS Code Extension and Tooling Architecture](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-carbonluau-vs-code-extension-and-tooling-architecture)

<a id="1-tldr"></a>

- [1. TL;DR](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-1-tldr)

<a id="2-product-goals-and-non-goals"></a>

- [2. Product goals and non-goals](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-2-product-goals-and-non-goals)

<a id="goals"></a>

- [Goals](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-goals)

<a id="non-goals"></a>

- [Non-goals](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-non-goals)

<a id="3-researched-current-luau--vs-code-tooling-landscape"></a>

- [3. Researched current Luau / VS Code tooling landscape](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-3-researched-current-luau--vs-code-tooling-landscape)

<a id="carbonluau-repository-findings"></a>

- [CarbonLuau repository findings](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-carbonluau-repository-findings)

<a id="luau"></a>

- [Luau](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-luau)

<a id="luau-lsp"></a>

- [`luau-lsp`](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-luau-lsp)

<a id="vs-code"></a>

- [VS Code](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-vs-code)

<a id="toolchain-language"></a>

- [Toolchain language](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-toolchain-language)

<a id="4-recommended-repository-structure"></a>

- [4. Recommended repository structure](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-4-recommended-repository-structure)

<a id="gmoddevcarbonluau"></a>

- [`gmoddev/CarbonLuau`](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-gmoddevcarbonluau)

<a id="gmoddevcarbonluau-vscode"></a>

- [`gmoddev/carbonluau-vscode`](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-gmoddevcarbonluau-vscode)

<a id="why-not-one-monorepo"></a>

- [Why not one monorepo?](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-why-not-one-monorepo)

<a id="5-complete-component-architecture-diagram"></a>

- [5. Complete component architecture diagram](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-5-complete-component-architecture-diagram)

<a id="6-ownership--responsibility-table"></a>

- [6. Ownership / responsibility table](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-6-ownership--responsibility-table)

<a id="7-language-server--type-system-integration"></a>

- [7. Language-server / type-system integration](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-7-language-server--type-system-integration)

<a id="decision"></a>

- [Decision](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-decision)

<a id="carbonluau-require-bridge"></a>

- [CarbonLuau require bridge](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-carbonluau-require-bridge)

<a id="lspruntime-luau-revision-mismatch"></a>

- [LSP/runtime Luau revision mismatch](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-lspruntime-luau-revision-mismatch)

<a id="8-canonical-api-metadata-pipeline"></a>

- [8. Canonical API metadata pipeline](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-8-canonical-api-metadata-pipeline)

<a id="decision-1"></a>

- [Decision](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-decision-1)

<a id="generated-outputs"></a>

- [Generated outputs](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-generated-outputs)

<a id="drift-prevention"></a>

- [Drift prevention](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-drift-prevention)

<a id="9-project--addon-detection-model"></a>

- [9. Project / addon detection model](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-9-project--addon-detection-model)

<a id="detection-priority"></a>

- [Detection priority](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-detection-priority)

<a id="scripting-api-selection"></a>

- [Scripting API selection](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-scripting-api-selection)

<a id="package-schema-selection"></a>

- [Package schema selection](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-package-schema-selection)

<a id="10-module--dependency-tooling"></a>

- [10. Module / dependency tooling](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-10-module--dependency-tooling)

<a id="local-multi-addon-workspace-model"></a>

- [Local multi-addon workspace model](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-local-multi-addon-workspace-model)

<a id="public-module-type-surfaces"></a>

- [Public module type surfaces](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-public-module-type-surfaces)

<a id="11-package-validation--build-model"></a>

- [11. Package validation / build model](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-11-package-validation--build-model)

<a id="ownership"></a>

- [Ownership](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-ownership)

<a id="deterministic-build"></a>

- [Deterministic build](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-deterministic-build)

<a id="12-companion-tooling-host-design"></a>

- [12. Companion tooling-host design](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-12-companion-tooling-host-design)

<a id="decision-yes"></a>

- [Decision: yes](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-decision-yes)

<a id="implementation-language"></a>

- [Implementation language](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-implementation-language)

<a id="modes"></a>

- [Modes](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-modes)

<a id="coordinator-responsibilities"></a>

- [Coordinator responsibilities](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-coordinator-responsibilities)

<a id="13-process-sandbox--resource-model"></a>

- [13. Process sandbox / resource model](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-13-process-sandbox--resource-model)

<a id="preview-worker"></a>

- [Preview worker](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-preview-worker)

<a id="no-ambient-authority"></a>

- [No ambient authority](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-no-ambient-authority)

<a id="crash-handling"></a>

- [Crash handling](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-crash-handling)

<a id="14-tooling-protocol"></a>

- [14. Tooling protocol](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-14-tooling-protocol)

<a id="envelope"></a>

- [Envelope](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-envelope)

<a id="v1-operations"></a>

- [v1 operations](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-v1-operations)

<a id="initialize-negotiation"></a>

- [Initialize negotiation](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-initialize-negotiation)

<a id="cancellationstaleness"></a>

- [Cancellation/staleness](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-cancellationstaleness)

<a id="15-gui-preview-execution-model"></a>

- [15. GUI preview execution model](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-15-gui-preview-execution-model)

<a id="initial-execution"></a>

- [Initial execution](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-initial-execution)

<a id="screen-selection"></a>

- [Screen selection](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-screen-selection)

<a id="preview-safe-services-in-v1"></a>

- [Preview-safe services in v1](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-preview-safe-services-in-v1)

<a id="16-backend-neutral-preview-plan-design"></a>

- [16. Backend-neutral preview-plan design](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-16-backend-neutral-preview-plan-design)

<a id="important-rule"></a>

- [Important rule](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-important-rule)

<a id="tooling-ids"></a>

- [Tooling IDs](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-tooling-ids)

<a id="17-webview-renderer-architecture"></a>

- [17. WebView renderer architecture](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-17-webview-renderer-architecture)

<a id="18-hierarchy--property-inspector"></a>

- [18. Hierarchy / property inspector](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-18-hierarchy--property-inspector)

<a id="19-resource-limit-visualization"></a>

- [19. Resource-limit visualization](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-19-resource-limit-visualization)

<a id="20-viewport--resolution-model"></a>

- [20. Viewport / resolution model](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-20-viewport--resolution-model)

<a id="definition-of-preview-accurate"></a>

- [Definition of “preview accurate”](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-definition-of-preview-accurate)

<a id="21-images-fonts-clipping-and-scroll-preview-fidelity"></a>

- [21. Images, fonts, clipping, and scroll preview fidelity](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-21-images-fonts-clipping-and-scroll-preview-fidelity)

<a id="images"></a>

- [Images](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-images)

<a id="fonts"></a>

- [Fonts](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-fonts)

<a id="clipping"></a>

- [Clipping](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-clipping)

<a id="scrolling"></a>

- [Scrolling](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-scrolling)

<a id="22-mock-player--items-fixture-decision"></a>

- [22. Mock Player / Items fixture decision](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-22-mock-player--items-fixture-decision)

<a id="23-interaction-preview-decision"></a>

- [23. Interaction-preview decision](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-23-interaction-preview-decision)

<a id="24-source-mapping-strategy"></a>

- [24. Source-mapping strategy](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-24-source-mapping-strategy)

<a id="25-refresh--hot-reload-strategy"></a>

- [25. Refresh / hot-reload strategy](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-25-refresh--hot-reload-strategy)

<a id="v1-trigger-policy"></a>

- [V1 trigger policy](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-v1-trigger-policy)

<a id="26-diagnostics--error-ux"></a>

- [26. Diagnostics / error UX](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-26-diagnostics--error-ux)

<a id="static-carbonluau-diagnostics"></a>

- [Static CarbonLuau diagnostics](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-static-carbonluau-diagnostics)

<a id="luau-language-diagnostics"></a>

- [Luau language diagnostics](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-luau-language-diagnostics)

<a id="authoritative-compile-diagnostics"></a>

- [Authoritative compile diagnostics](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-authoritative-compile-diagnostics)

<a id="previewruntime-diagnostics"></a>

- [Preview/runtime diagnostics](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-previewruntime-diagnostics)

<a id="logging"></a>

- [Logging](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-logging)

<a id="27-workspace-trust--security"></a>

- [27. Workspace Trust / security](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-27-workspace-trust--security)

<a id="allowed-in-restricted-mode"></a>

- [Allowed in Restricted Mode](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-allowed-in-restricted-mode)

<a id="disabled-until-trusted"></a>

- [Disabled until trusted](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-disabled-until-trusted)

<a id="workspace-code-is-never-host-code"></a>

- [Workspace code is never host code](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-workspace-code-is-never-host-code)

<a id="28-webview-security"></a>

- [28. WebView security](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-28-webview-security)

<a id="29-versioning--compatibility--update-model"></a>

- [29. Versioning / compatibility / update model](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-29-versioning--compatibility--update-model)

<a id="compatibility-behavior"></a>

- [Compatibility behavior](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-compatibility-behavior)

<a id="api-definition-updates"></a>

- [API definition updates](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-api-definition-updates)

<a id="integrity"></a>

- [Integrity](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-integrity)

<a id="30-tooling-distribution--platform-support"></a>

- [30. Tooling distribution / platform support](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-30-tooling-distribution--platform-support)

<a id="extension-distribution"></a>

- [Extension distribution](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-extension-distribution)

<a id="macos"></a>

- [macOS](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-macos)

<a id="remote-vs-code"></a>

- [Remote VS Code](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-remote-vs-code)

<a id="not-v1"></a>

- [Not v1](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-not-v1)

<a id="31-configuration-and-commands"></a>

- [31. Configuration and commands](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-31-configuration-and-commands)

<a id="initial-configuration-surface"></a>

- [Initial configuration surface](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-initial-configuration-surface)

<a id="command-palette"></a>

- [Command palette](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-command-palette)

<a id="tasks"></a>

- [Tasks](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-tasks)

<a id="32-complete-first-use-workflow"></a>

- [32. Complete first-use workflow](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-32-complete-first-use-workflow)

<a id="33-performance-strategy"></a>

- [33. Performance strategy](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-33-performance-strategy)

<a id="language-path"></a>

- [Language path](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-language-path)

<a id="project-model"></a>

- [Project model](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-project-model)

<a id="file-watching"></a>

- [File watching](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-file-watching)

<a id="preview"></a>

- [Preview](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-preview)

<a id="34-testing--qualification-plan"></a>

- [34. Testing / qualification plan](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-34-testing--qualification-plan)

<a id="languageapi"></a>

- [Language/API](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-languageapi)

<a id="addonsmodules"></a>

- [Addons/modules](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-addonsmodules)

<a id="tooling-protocolprocess"></a>

- [Tooling protocol/process](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-tooling-protocolprocess)

<a id="gui-semantic-parity"></a>

- [GUI semantic parity](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-gui-semantic-parity)

<a id="webview"></a>

- [WebView](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-webview)

<a id="security"></a>

- [Security](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-security)

<a id="extension-e2e"></a>

- [Extension E2E](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-extension-e2e)

<a id="release-gates"></a>

- [Release gates](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-release-gates)

<a id="35-telemetry--privacy-decision"></a>

- [35. Telemetry / privacy decision](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-35-telemetry--privacy-decision)

<a id="36-future-live-server-integration-seam"></a>

- [36. Future live-server integration seam](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-36-future-live-server-integration-seam)

<a id="37-future-debugging-seam"></a>

- [37. Future debugging seam](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-37-future-debugging-seam)

<a id="38-future-visual-authoring-seam"></a>

- [38. Future visual-authoring seam](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-38-future-visual-authoring-seam)

<a id="39-exact-v1-scope"></a>

- [39. Exact v1 scope](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-39-exact-v1-scope)

<a id="ship-in-v1"></a>

- [Ship in v1](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-ship-in-v1)

<a id="v1-does-not-require-a-rust-server-installation"></a>

- [v1 does not require a Rust server installation.](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-v1-does-not-require-a-rust-server-installation)

<a id="40-exact-deferred-scope"></a>

- [40. Exact deferred scope](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-40-exact-deferred-scope)

<a id="41-implementation-phases"></a>

- [41. Implementation phases](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-41-implementation-phases)

<a id="tooling-foundation-a--semantic-extraction--api-contract"></a>

- [Tooling Foundation A — semantic extraction + API contract](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-tooling-foundation-a--semantic-extraction--api-contract)

<a id="tooling-foundation-b--projectlanguage-integration"></a>

- [Tooling Foundation B — project/language integration](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-tooling-foundation-b--projectlanguage-integration)

<a id="tooling-foundation-c--coordinator--validatebuild"></a>

- [Tooling Foundation C — coordinator + validate/build](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-tooling-foundation-c--coordinator--validatebuild)

<a id="tooling-foundation-d--preview-semantic-worker"></a>

- [Tooling Foundation D — preview semantic worker](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-tooling-foundation-d--preview-semantic-worker)

<a id="tooling-foundation-e--webviewinspectorrelease-closure"></a>

- [Tooling Foundation E — WebView/inspector/release closure](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-tooling-foundation-e--webviewinspectorrelease-closure)

<a id="later-foundation-f--fixtures-and-interaction-simulation"></a>

- [Later Foundation F — fixtures and interaction simulation](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-later-foundation-f--fixtures-and-interaction-simulation)

<a id="later-foundation-g--live-server--debugger--visual-edit-seams"></a>

- [Later Foundation G — live server / debugger / visual edit seams](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-later-foundation-g--live-server--debugger--visual-edit-seams)

<a id="42-qualification-gates-for-each-phase"></a>

- [42. Qualification gates for each phase](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-42-qualification-gates-for-each-phase)

<a id="foundation-a-gate"></a>

- [Foundation A gate](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-foundation-a-gate)

<a id="foundation-b-gate"></a>

- [Foundation B gate](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-foundation-b-gate)

<a id="foundation-c-gate"></a>

- [Foundation C gate](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-foundation-c-gate)

<a id="foundation-d-gate"></a>

- [Foundation D gate](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-foundation-d-gate)

<a id="foundation-e-gate"></a>

- [Foundation E gate](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-foundation-e-gate)

<a id="43-genuinely-unresolved-external--tool-dependent-questions"></a>

- [43. Genuinely unresolved external / tool-dependent questions](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-43-genuinely-unresolved-external--tool-dependent-questions)

<a id="44-final-verdict"></a>

- [44. Final verdict](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-44-final-verdict)

<a id="ready-for-implementation-design"></a>

- [READY FOR IMPLEMENTATION DESIGN](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-ready-for-implementation-design)

<a id="research-references"></a>

- [Research references](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-research-references)

<a id="carbonluau-repository"></a>

- [CarbonLuau repository](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-carbonluau-repository)

<a id="luau--language-tooling"></a>

- [Luau / language tooling](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-luau--language-tooling)

<a id="vs-code-1"></a>

- [VS Code](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-vs-code-1)

<a id="toolchain"></a>

- [Toolchain](../history/tooling/CarbonLuau_VSCode_Tooling_Architecture.md#carbonluau-vscode-tooling-architecture-toolchain)
