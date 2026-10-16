# CI failure diagnostics and reliability hardening

This continues `e293e5b` and the [inventory investigation](Inventory-CI-Investigation.md).
The historical Windows inventory occurrence remains unattributed and its release
HOLD remains in force. This work changes test infrastructure and evidence retention;
it does not claim to explain that occurrence or change production behavior.

## Assertion and cleanup audit

The audit covers native tests, managed model/loader tests, real-VM tests,
persistence integration, tooling integration and clean-install execution.
All 58 non-live C# boolean `Check` helpers retain caller file/line and an expression
digest. All 17 native boolean helpers retain the actual caller file/line. These
counts include diagnostic contract fixtures. A coverage regression detects future
uninstrumented helpers. Original predicates, short-circuit evaluation, exception
types/messages and exit behavior remain; diagnostics never re-evaluate conditions.

Combined assertions stay combined where splitting changes operation order.
Inventory initialization/reload additionally retain individual status, scan, watch,
demand, pending, elapsed and live-VM values. Native execution retains load/resume
status, expected status and retirement flags before cleanup. Native accounting and
scheduler snapshots retain actual counters. Persistence startup retains ready,
pending and sent values; derived crash/plan catches retain private error codes.
VM and persistence status namespaces remain distinct (`flags=2` identifies private
persistence failure records). A caller location identifies an unsatisfied compound
predicate, not every operand's value; numeric snapshots are deliberately targeted.

First-failure protection covers directory cleanup in inventory initialization and
observation, loader/runtime/C4, catalog memory, discovery, entity death, inventory
binding, Loot and clean-install fixtures, plus clean-install VM cleanup. A primary
exception is re-thrown unchanged, or remains the reported failure in an existing
catch/return path; cleanup executes once. Cleanup-only failure still fails. The
query fixture's silent destructor catch now reports cleanup failure and ordinary
successful execution checks cleanup; unwinding retains its original exception.

Historical live Carbon fixtures retain their host logging/qualification records.
No test code is installed in Carbon, and this work claims no new live-host evidence.
Specialized SQLite/result guards retain their existing numeric reporting. Unknown
process failures are explicitly `unclassified-process-failure`; exit codes are not
guessed to mean timeout.

## Bounded evidence

`tools/ci/Run.py` invokes each child once and preserves its exit. Bash uses explicit
fail-fast/pipefail; PowerShell retains GitHub's error preference/native exit footer.
No retry, warmup, delay or new deadline is added. Output/error share one drained
pipe. Console forwarding stops at 1 MiB while draining continues.

Each invocation has a unique directory. Evidence retains exit, elapsed, output
count/digest, source SHA, platform, architecture, Python version, CPU count,
workspace-relative working directory and workflow step number. First failure is
separate from the first 32/latest 32 events. Managed tests keep a 24-observation
ring flushed on first failure and at most 40 other records. Exception type,
HResult, native status and message/stack digest remain separate; arbitrary
exception messages, script contents and environment dumps are excluded.

The collector revalidates fields and excludes arbitrary child files/unknown fields.
Limits are 256 summaries, at most 64 KiB each. It records job outcome and its own
source digest. Curated disk-full signatures/source frames retain actionable
location/category information without free text. All existing CI jobs collect and
upload with `always()`, including sanitizers/tooling. The runtime matrix keeps both
OS jobs after a failure. Existing successful package/tooling gates are unchanged.

The manual `CI diagnostics expected negative qualification` workflow intentionally
fails on both operating systems with exit 19; artifact upload must still succeed.
It is never a green/release gate. Runner loss before collection cannot guarantee
an upload. Structured evidence complements the original console; it does not claim
a complete raw trace, universal error classification or historical attribution.

## Focused fault qualification

Eight managed children must genuinely exit 19: assertion, timeout, allocation,
infrastructure, cleanup-only, timeout plus cleanup, recent-ring overflow and broken
diagnostic sink. Checks cover original exception identity/stack, one cleanup,
24 recent observations, expression/location and secret exclusion. Windows also
uses a real `FileShare.None` deletion failure. Native qualification requires an
assertion exit 19 with exact source/line and actual=false/expected=true.

Python fixtures cover all categories, missing tool (127), disk-full classification,
source frames, 2.4 MB noisy output, concurrent isolation, bounded collection and
exclusion of secret sentinels/forged summary fields. Existing real-VM fault and
resource assertions keep their deadlines. No production source, gameplay API, resource ceiling, release
identity, published tag or asset changes.

## Executed qualification

Windows worker `dockerbox`: full 24-test native suite, six focused native
regressions after diagnostic additions, managed loader's 100 unload cycles,
fresh inventory/init faults, eight diagnostic negatives and the complete real-VM
suite passed. Linux worker `BigKVM`: all 26 native tests, fresh inventory/init,
eight diagnostic negatives and complete managed/real-VM suite passed. Both full
suites include the existing 100-addon fairness/resource closure. Linux managed
persistence used ext4; temporary native fixtures were isolated in container tmpfs.
No production filesystem qualification is inferred from tmpfs fixtures.

Worker logs, negative attempts, hashes and hosted artifact receipts are indexed in
[the evidence ledger](qualification/CI-Reliability-Evidence.json). The deliberately
failed [Windows/Linux hosted qualification](https://github.com/gmoddev/CarbonLuau/actions/runs/38847500133)
uploaded both artifacts successfully. Downloaded summaries preserved genuine exit
19, original `System.TimeoutException`, secondary `System.IO.IOException`, failed
job outcome and source SHA; neither secret sentinel was present. Final-source
normal workflows and a fresh negative dispatch are required before the completion
verdict, which reports the final SHA and exact run URLs. These statements do not
convert the manual red workflow into a green test gate.

## Retained negative attempts

Raw receipts are retained under `C:/Sandbox/Codex/CiHardening20261015` and the
matching Linux task directory. Initial Windows scripts exposed execution-policy/
redirection setup, an incomplete argument list and a wrong working directory;
failed logs remain separately from corrected invocations. The first Linux overlay
omitted the already-qualified status fix: its `INTERNAL_ERROR` versus `TIMEOUT`
assertion is retained as obsolete worker-source evidence, not a new defect.

A Linux native attempt exhausted its nearly full host disk: three tests explicitly
reported ENOSPC; two earlier generic controlled persistence failures did not retain
their inner code. Their exact attribution remains unknown. Nine directories made
by that attempt were inventoried and removed with an exact-target receipt,
recovering about 461 MiB. Unrelated data/eight containers were preserved. Subsequent
native fixtures use isolated temporary storage. A later managed attempt rejected
tmpfs at the production persistence worker filesystem gate; subsequent qualification
uses the existing explicit fixture-root override on ext4. No filesystem admission
or assertion is weakened.

Hosted qualification also retained implementation mistakes: workspace expressions
were rejected in custom-shell fields before any job could start; those workflows
now use literal paths with a path regression. Windows initially selected the WSL
`bash.exe`; resolution now explicitly selects installed Git Bash and fails closed
when absent. Its regression canonicalizes the expected Windows 8.3/long path.
A newly added startup counter initially dereferenced a null worker in synthetic
fixtures; the failed Linux run retained recent counters and exact exception type,
and the observation now handles the optional worker. Original assertions and
deadlines were unchanged. A disposable Linux closure script also assumed an
optional NuGet cache existed; the missing-cache command failure is retained in
the task transcript and the corrected setup restores dependencies normally.

Malformed workflow definitions and abrupt runner loss precede or prevent custom
collection; GitHub's validation/platform records remain the evidence in those
cases. The historical inventory HOLD and the two generic disk-pressure negatives'
unresolved inner attribution remain explicitly preserved.
