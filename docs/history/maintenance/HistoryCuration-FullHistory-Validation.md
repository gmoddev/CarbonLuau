# HistoryCuration-FullHistory-Validation: historical evidence

These are dated records, not current API declarations or new PASS claims.
Their source revisions, host pins, failures, adoption scope and qualification
limits remain unchanged. Current authority is the [invariant register](../../Invariants.md).

## Records

- [HistoryCuration-FullHistory-Validation.md](#historycuration-fullhistory-validation)

<!-- BEGIN RECORD HistoryCuration-FullHistory-Validation.md -->

<a id="historycuration-fullhistory-validation"></a>

<a id="historycuration-fullhistory-validation-full-history-curation-review-branch"></a>

# Full-history curation review branch

The user approved preparation and normal publication of a separate review branch
on 2026-10-11. This is history/documentation maintenance only. Main cutover,
force-pushing main, moving tags, deleting history and publishing releases are
not authorized by that approval.

<a id="historycuration-fullhistory-validation-scope-and-preservation"></a>

## Scope and preservation

Approved source: `0f6ee0f2c27e9dbc9a3a7c056496ddc24541cf58` (144 reachable
commits; 143 first-parent commits). The 136 commits through v0.5.0 become 16
checkpoints, including the unchanged initial root. The eight newer feature/docs
groups keep their boundaries and acquire new descendant SHAs. This yields 24
source-history commits plus this separate audit commit: 25 commits in total.

Candidate: `history/full-curated-20261011`. Archive:
`archive/pre-full-history-squash-20261011`, retaining the approved source above.
The earlier `archive/pre-history-squash-20261011` remains at
`4e608692f1faf2871fb37bd20b29a9ebd6679212` and preserves the original,
pre-first-squash history. Existing qualification branches, other worktrees,
untracked research and local recovery references are not rewritten or removed.

The original root `c8e6b74694039d191e48fdf18c0cb513046a14eb` is retained.
All subsequent checkpoints are single-parent commits made from the exact
original endpoint trees. No patches are reapplied and no synthetic intermediate
source trees are invented. Commit messages distinguish historical checkpoints
from new qualification or publication.

<a id="historycuration-fullhistory-validation-exact-mapping"></a>

## Exact mapping

Each count includes all original commits assigned to that row, not merely its
first-parent count. Every original commit is assigned exactly once.

| # | Original endpoint | Curated checkpoint | Original count | Group |
|---|---|---|---:|---|
| 1 | `c8e6b74` | `c8e6b74` | 1 | Initialize CarbonLuau |
| 2 | `10c31c2` | `008fd06` | 9 | Native loading and bounded runtime |
| 3 | `43f402d` | `3d4c1d2` | 3 | Gameplay facade, commands and scripting API |
| 4 | `43907c1` | `608be7a` | 9 | 0.3 release qualification and artifact preparation |
| 5 | `d51b890` | `d241bf3` | 1 | Experimental 0.3 release checkpoint |
| 6 | `956440f` | `ccce7de` | 17 | Addon/provider foundations and isolated compiler |
| 7 | `b00bf21` | `334080a` | 20 | GUI Foundations 1-3 |
| 8 | `c37c36e` | `68b710e` | 19 | Player expansion and inventory qualification |
| 9 | `069387c` | `afbe2e9` | 7 | Editor tooling Foundations A/B |
| 10 | `ef045f2` | `3448da9` | 3 | 0.4 release qualification and artifact preparation |
| 11 | `2ada473` | `4a60255` | 1 | Experimental 0.4 release checkpoint |
| 12 | `7717426` | `ab6c2ea` | 5 | Historical World/Entity architecture and lifetime blocker |
| 13 | `034f28f` | `ccec7ab` | 14 | Persistence Foundation 1 |
| 14 | `8d303ce` | `94d5b57` | 22 | Persistence Foundation 2 / Query |
| 15 | `e525402` | `dec900b` | 4 | 0.5 release qualification and artifact preparation |
| 16 | `3c0d057` | `4921495` | 1 | Experimental 0.5 release checkpoint |
| 17 | `14210a1` | `58b1ae6` | 1 | Qualified Workspace/Entity and bounded discovery |
| 18 | `9a8fd16` | `f5b5a7b` | 1 | Player and Entity lifecycle signals |
| 19 | `c29c9a3` | `78ca58a` | 1 | Connection-bound PlayerInventory facade |
| 20 | `d5b353c` | `5bcd5b7` | 1 | Barrel Loot Replace |
| 21 | `e28ed42` | `6c05642` | 1 | EntityDied and attribution |
| 22 | `7e42773` | `2234417` | 1 | Sampled ItemChanged |
| 23 | `10d3f3b` | `be5da2c` | 1 | Complete PlayerInventory and explicit API 0.7 migration |
| 24 | `0f6ee0f` | `50b9e4b` | 1 | Approachable README and first privacy audit |

Player closure `c37c36e` originally branched from `9b27ba5` while tooling began
on a sibling branch; `64eca4a` merged them. Choosing the existing Player closure
snapshot before the existing tooling completion snapshot linearizes that merge
without inventing or reordering patches. The Player checkpoint includes its
cold-module/publication correction and qualification. Tooling includes the shared
Core/GUI extraction, analysis containment, metadata and preview integration.

<a id="historycuration-fullhistory-validation-tree-parity-and-original-releases"></a>

## Tree parity and original releases

The source tip `50b9e4bff716d335d99dba5f5b067fa7808b6686` has exactly the approved
main tree `8e37a8e6ae502fdbf6b1b30fd4c63d87d319ce25`. Every mapped checkpoint
also has exactly its original endpoint tree. The only subsequent source-tree
difference permitted for this review is this new audit document.

| Original release | Original commit | Curated counterpart | Shared tree |
|---|---|---|---|
| v0.3.0 | `d51b890` | `d241bf3` | `a65ea1ebec8afd9565496194fe08fa114e0e3f77` |
| v0.4.0 | `2ada473` | `4a60255` | `55f0ca320f4aab68c10887c55fb0c3979722aa2a` |
| v0.5.0 | `3c0d057` | `4921495` | `484bcdc75a4cc62f74891147782eddcf62408a22` |

The annotated tag objects remain `30cc955d20f85cfe8edba397eb411ea0a8b170d1`,
`87f28154fd4af6fc86b58470489a35d42a3966c3` and
`bb91c3d355a0eed4f92911a3b97c82b9bdaf7dea`, respectively. Original tags are
not ancestors of the full-curated candidate. They stay on the archived original
graph; their release links, assets and provenance continue to refer to that graph.
No tags are created for curated counterparts. Commands assuming an ancestral
release tag, such as tag-based describe, must not substitute that assumption for
the explicit release manifest.

Milestone names record qualification/artifact preparation followed by an
experimental-release checkpoint. They do not create a new release, assert a
stable/final release, or claim the old GitHub publication action ran at a new SHA.
No redundant empty "Final Release" checkpoint is manufactured.

<a id="historycuration-fullhistory-validation-evidence-privacy-and-backup"></a>

## Evidence, privacy and backup

Historical documents, CI/source links, host qualification, negative investigations
and limitations remain unchanged. Old CI success is not assigned to new SHAs.
Exact unchanged runtime/fixture trees permit reuse of the old host evidence with
its original attribution, while fresh final-candidate CI qualifies the rewrite.
The [first curation audit](HistoryCuration-Validation.md#historycuration-validation) remains the record of
that earlier operation, not a claim that this full-curated branch descends from
the original v0.5 tag.

The pre-curation all-refs bundle is verified and email-clean. Its SHA-256 is
`622372cb310e5220b941200b23df2e9a4a0d38d949e3b9ebe214211f6a892a42`.
Local recovery material, complete original-to-curated SHA mapping, preflight ref/
release-asset inventory and validation receipts are retained under
`C:\Users\aiden\Documents\ChatGPT\CarbonLuau-history-backup-20261011\FullCuration`.
This operation does not prune objects or expire reflogs.

Stored author/committer/tagger identities are checked against GitHub no-reply
identities, including GitHub's public web-flow no-reply committer. Newly created
commits use the configured GitHub user no-reply author and committer. Commit
messages and the added document are separately checked for private-email leakage.
Existing third-party license attribution is not removed. External clones/forks/
caches are outside this local and advertised-ref audit; no universal erasure
claim is made.

<a id="historycuration-fullhistory-validation-review-qualification-and-cutover-boundary"></a>

## Review qualification and cutover boundary

Required preparation checks are complete original-commit coverage, all-checkpoint
tree parity, exact release-tree parity, final source-tree parity, single-parent
curated lineage, preserved refs/tags/assets, Git integrity, privacy and verified
backup. Local API/link, architecture, tooling-contract, release-identity and
deterministic production-package checks pass. Two independently generated candidate
source packages and the original-main source package have the same SHA-256:
`CB8CB76BC393C631458A84772024AB01830400B6C587D724AC05AF1846FC4DF8`.
All contain exactly 86 production C# sources and no fixtures or native files.
Fresh hosted Windows/Linux native, managed/real-VM, sanitizer/
leak, tooling and clean-install workflows must identify the audit commit itself;
their run IDs and actual results are retained in the review receipt. This document
does not claim a hosted result before that run completes.

Only the archive and candidate branches may be published by normal push.
Documentation deployment is intentionally not dispatched from this review branch:
the live site remains on main. CI dry-run artifacts are not published release
assets. Package 0.5.0, development API 0.7.0-experimental, ABI 1.5, provider 1.2,
schema 1 and Luau remain unchanged.

Main stays at the approved source and requires a separate explicit cutover
approval. Do not merge the archived lineage into the candidate: doing so would
restore the history that was deliberately consolidated. A later approved cutover
would need fresh remote-main verification and a scoped force-with-lease, not tag
movement, unrelated branch rewriting or deletion of another checkout's work.

<!-- END RECORD HistoryCuration-FullHistory-Validation.md -->

## Post-review main cutover — 2026-10-11

After inspecting the validated branch, the user separately requested main
integration. Main was promoted from `0f6ee0f2c27e9dbc9a3a7c056496ddc24541cf58`
to the exact qualified `cdda6d1ea450ee6b0f436841c056f7c00c022044`, with 25
commits and no merge of the original ancestry. The final synchronization
invocation observed remote main already at that exact candidate; it issued no
additional force-push and synchronized local main with `reset --keep`.

All three tag objects/targets, 27 release assets, archives and unrelated worktrees
remained unchanged. Tracked main was clean; existing untracked research remained.
The email-clean verified bundle and prior mapping are retained. No release/tag,
runtime/API change, object pruning or reflog expiration occurred.

Exact-source candidate runs passed, followed by main-source
[Windows/Linux/sanitizer validation](https://github.com/gmoddev/CarbonLuau/actions/runs/38188030139),
[tooling validation](https://github.com/gmoddev/CarbonLuau/actions/runs/38188030098),
[baseline contracts](https://github.com/gmoddev/CarbonLuau/actions/runs/38188030125)
and [documentation deployment](https://github.com/gmoddev/CarbonLuau/actions/runs/38188030102).
The earlier review-stage authority above remains verbatim; this addendum does
not retroactively authorize its preparation step to perform main cutover.
