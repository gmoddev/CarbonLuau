# HistoryCuration-Validation: historical evidence

These are dated records, not current API declarations or new PASS claims.
Their source revisions, host pins, failures, adoption scope and qualification
limits remain unchanged. Current authority is the [invariant register](../../Invariants.md).

## Records

- [HistoryCuration-Validation.md](#historycuration-validation)

<!-- BEGIN RECORD HistoryCuration-Validation.md -->

<a id="historycuration-validation"></a>

<a id="historycuration-validation-unreleased-history-curation-and-privacy-audit"></a>

# Unreleased history curation and privacy audit

The user approved the scoped history cleanup/cutover on 2026-10-11 after reviewing
the proposed seven-feature squash and privacy consequences. Published releases
are not rewritten. This is Git/documentation maintenance, not a runtime phase,
API change, package release or tag creation.

<a id="historycuration-validation-validated-scope"></a>

## Validated scope

Original public main: `4e608692f1faf2871fb37bd20b29a9ebd6679212`.
The supplied review stopped at `cf86b91`, before C4. Local parent/merge inspection
finds 181 reachable commits on original main: 136 through v0.5.0 and 45 afterward.
The unreleased range has 44 first-parent commits because C4 preserves a separate
README edit through a merge. Counts are not inferred from GitHub compare alone.

The aggressive contiguous option combines the review's two World groups and two
early Gameplay groups, with no non-contiguous reordering. Seven feature commits
replace the 45 unreleased commits; one subsequent documentation commit contains
the requested approachable README and this audit record. No speculative future
release milestone is manufactured.

| Original base, exclusive | Original endpoint, inclusive | Original commits | Curated commit | Feature |
|---|---|---:|---|---|
| `3c0d057` | `fc71db4` | 16 | `14210a1` | Qualified Workspace/Entity and bounded discovery |
| `fc71db4` | `35bd8c3` | 9 | `9a8fd16` | Player lifecycle, EntitySpawned and EntityDestroyed |
| `35bd8c3` | `35701d5` | 2 | `c29c9a3` | Original-connection PlayerInventory facade |
| `35701d5` | `6d23072` | 6 | `d5b353c` | Qualified barrel Loot Replace |
| `6d23072` | `e7b2309` | 3 | `e28ed42` | EntityDied and qualified attribution |
| `e7b2309` | `cf86b91` | 5 | `7e42773` | Sampled ItemChanged |
| `cf86b91` | `4e60869` | 4 | `10d3f3b` | Complete PlayerInventory and explicit API 0.7 migration |

Every curated checkpoint has exactly the original endpoint's Git tree. The feature
tip `10d3f3bdfdd096ada189714c9936e4f2d57398d8` has the identical complete tree to
original main. The subsequent documentation diff is confined to `README.md` and
this file; production, tests, metadata, generated types, examples and manifests
are unchanged. All historical negative findings and qualification reports remain.

<a id="historycuration-validation-release-anchors-and-source-evidence"></a>

## Release anchors and source evidence

The annotated tag objects and peeled commits remain exactly:

| Tag | Tag object | Release commit |
|---|---|---|
| v0.3.0 | `30cc955d20f85cfe8edba397eb411ea0a8b170d1` | `d51b89058771c63d3ff3b50d0fada3e81c3b3915` |
| v0.4.0 | `87f28154fd4af6fc86b58470489a35d42a3966c3` | `2ada473399e752e8d178cce089cc9a7d442f32cf` |
| v0.5.0 | `bb91c3d355a0eed4f92911a3b97c82b9bdaf7dea` | `3c0d057c490e88bbcd9ed6d6552a2213ba7783b1` |

The curated lineage remains a descendant of v0.5.0. Existing release assets and
provenance are not regenerated or replaced. Package remains 0.5.0, development
API 0.7.0-experimental, ABI 1.5, provider 1.2, schema 1 and Luau pin unchanged.

`archive/pre-history-squash-20261011` preserves the original **already email-clean**
public main for old source/CI links, including the C4 implementation and evidence.
The old qualification branches are preserved, not merged back into curated main.
Reports retain their actual original SHAs and test outcomes. New qualification
artifacts use the new actual Git SHA; provenance is never relabeled as an old build.

<a id="historycuration-validation-email-and-recovery-cleanup"></a>

## Email and recovery cleanup

Initial public main and published tag ancestry had only GitHub no-reply identities.
One local legacy backup contained ten private-email commits. It was never uploaded
as part of this task. Its author/committer metadata and two affected descendants
were reconstructed with no-reply identities, retaining every tree, message and
timestamp. The existing local backup ref now points to that clean equivalent.

The user explicitly approved removing the original sensitive objects/recovery
references. Fourteen affected recovery entries were removed selectively; no global
reflog expiration was used. Git repacking unpacked unrelated unreachable data rather
than pruning it. Only twelve verified superseded metadata objects were removed.
All other original Git objects remain available; other worktrees and user files
were not reset, cleaned, rebased or deleted.

Full stored-object/recovery-identity inspection and `git fsck --full` pass afterward.
All stored commit author/committer and tagger identities are no-reply. Original
private-email objects are absent, release tags and backup contents are unchanged,
and no private address was printed or copied into the report. Current tracked file content
contains no occurrence of the identified private address; public third-party
license attribution is preserved.

Verified email-clean Git bundles cover the original clean public history and
the clean equivalent local backup. A fresh bundle mirror was independently audited.
The private-address originals are deliberately not included in these backups.
Local receipts/bundles are under
`C:\Users\aiden\AppData\Local\Temp\CarbonLuau-History-20261011`.

This proves the inspected local Git store and advertised refs, not every external
copy. GitHub cached views, forks, other clones and unrelated older backup files can
retain past data. A force-push cannot remove them; cleanup may require owner action
or [GitHub Support](https://docs.github.com/en/authentication/keeping-your-account-and-data-secure/removing-sensitive-data-from-a-repository).
No universal remote-erasure claim is made.

<a id="historycuration-validation-qualification-and-cutover-discipline"></a>

## Qualification and cutover discipline

Local API/link, architecture, tooling-contract and deterministic package checks
run on the curated worktree. Fresh hosted Windows/Linux native, managed/real-VM,
full regressions, sanitizers/leaks, tooling and clean-install checks qualify the
new final SHA before main cutover; old CI statuses are not transferred to it.
Unchanged exact-host live C4 evidence is reusable because runtime/fixture trees
are identical, not because the new commit inherits an old CI status.

Cutover uses the user's explicit approval and `--force-with-lease` against the
recorded original main SHA. It does not disable branch protection (main was
unprotected), alter tags or force-push unrelated branches. No open PRs existed
at preflight. Remote SHA, release anchors, CI and documentation are checked after
cutover. Existing `.codexlock` and untracked research remain untouched.

Collaborators should start a clean clone or preserve their work and rebase/cherry-
pick new changes onto curated main. Do not merge the old unreleased lineage back
into main, and do not push an old private-email backup. These instructions do not
authorize discarding another checkout's uncommitted files or rewriting its branch.

<!-- END RECORD HistoryCuration-Validation.md -->

