# Documentation consolidation: scope and preservation

The user approved the documentation deletion/compaction/historical classification
on 2026-10-11 against `cdda6d1ea450ee6b0f436841c056f7c00c022044`.
This is documentation maintenance, not another runtime foundation, architecture
amendment, feature, API identity change or release authorization.

## Organization

- [Current index](Index.md): author/operator docs, canonical rules and current
  detailed/deferred contracts.
- [Qualification index](qualification/README.md): accepted source/host-scoped
  evidence and consolidated phase ledgers. Completed is not obsolete.
- [History](history/README.md): superseded proposals, failed investigations,
  adoption history and privacy/curation mappings.
- Compatibility pages at former paths are navigation only, not duplicate rules.
  Public `api/` URLs and offline package names stay stable.

## Preserved evidence and authority

The [coverage manifest](evidence/DocumentationCoverage.json) records original Git
blobs and 97 retained source spans: historical originals/tails, qualified phase
records, detailed invariant decisions and validation criteria. Relocation rebases
relative links and adds scoped anchors; it does not relabel an old tested SHA,
drop a failed result or convert a proposal into implementation PASS.

I1–I12 and the decision register remain in [Invariants](Invariants.md). Detailed
decisions are explicitly delegated canonical parts of that register under
`invariants/`, not a second authority. Compatibility retains support, identities,
upgrade policy and check selection; its detailed evidence criteria are delegated
to [qualification policy](qualification/Policy.md) without weakening them.

Only `FoundationE-Checkpoint.md` and `GuiFoundation1D-Checkpoint.md` are deleted:
both superseded interruption checkpoints have no incoming tracked references or
verification consumer. Original files remain recoverable in preserved v0.4.0
history. The 33 historical-only records are retained, not discarded; ten mixed
records keep current routing/slices and preserve their original bodies/tails.

Six phase ledgers consolidate addon A–G, GUI 1/2/3, Player interaction and public
Persistence-1B implementation/evidence. Existing GUI capability and Player API
document checks retain their exact facts on compatibility pages; no test assertion
or production path is loosened to make the reorganization pass.

## Corrections and boundaries

Current GUI docs no longer call 0.4 unpublished. Current tooling status routes to
qualified A/B completion while preserving Workspace Trust and platform limits.
GiveItem's current reference points to PlayerInventory, not the removed Player
section. The full-history audit has a post-cutover addendum, preserving the earlier
review authorization verbatim. Planned package 0.6 notes remain current planning;
development API 0.7 does not automatically assign package 0.7.

Production source, bootstrap, metadata/generated types, examples, vendor pins,
release manifest and runtime/ABI/provider/schema identities are unchanged.
No new release/tag, VSIX or Marketplace publication is part of this cleanup.
Contributor routing shrinks from 425 to 108 lines, the invariant register from
1,708 to 271, and Compatibility from 973 to 325. This is active-reading reduction,
not evidence deletion or a promise to minimize repository file count: historical
records and compatibility routes necessarily remain available.
Filesystem allocation stays an operational budget, distinct from hard logical
quotas/file extents; sampled inventory and exact Entity lifetime proofs are not
weakened by shorter entry-point documentation.

## Qualification discipline

`tools/Test-Documents.py` passes on the documentation candidate: 97 retained spans,
unchanged global I1-I12 semantics, stable public API paths, and all local link
fragments. Its negative fixtures reject missing files, removed fragments,
duplicate anchors and changed rule text. The baseline and deployment workflows
run it from shallow final-source checkouts; old Git objects are not required.

Run the existing API/architecture/
tooling/release checks, deterministic source packaging and final-source hosted
Windows/Linux/tooling/sanitizer/clean-install workflows. Source-qualified live
receipts are reused only for byte-identical implementation/fixture paths; no new
server/client/power-loss observations are claimed. Actual final tested SHA and
workflow outcomes accompany the completed task, not a preemptive PASS statement.

Local API/link, architecture, tooling-schema and release-identity checks pass.
Two source-package builds are identical to the pre-cleanup 86-source production
package: SHA-256 `CB8CB76BC393C631458A84772024AB01830400B6C587D724AC05AF1846FC4DF8`.
Full platform/metadata/offline clean-install CI must still pass on the final commit
before the documentation candidate is fast-forwarded to main.
