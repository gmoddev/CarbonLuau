# GUI Foundation 1D checkpoint

Status: superseded by the completed [GUI Foundation 1D implementation and qualification record](GuiFoundation1D.md).
This file preserves the interruption boundary and is not the completion record.

Starting commit: `94503cf3484da506d251c84382dbe23501fcd1d6` on `main`.

## Implemented so far

- Per-ScreenGui synchronization revisions, structural-dirty state, and bounded property-dirty tracking.
- Coalesced property patches with full-rebuild promotion for structural changes and cursor-state changes.
- Presentation revision tracking, patch checkpoints, retry/full-resync state, and projection-failure suppression until a newer revision.
- Exact serialized-operation measurement in GUI backends and Rust CUI partial updates using `update=true`.
- Fair, bounded domain and presentation flushing after Luau callbacks have returned.
- Publication, disconnection, teardown, transient backend failure, and stale-player handling updates.
- A new `GuiFoundation1DTests` model/native test scaffold and `--gui1d-only` runner wiring.

## Evidence already obtained

- `dotnet build tests/runtime/RuntimeTests.csproj -c Release -f net48` passed before the GUI Foundation 1D test file was added.
- `RuntimeTests.exe --gui1c-only` passed after updating the transient target-unavailable expectation.
- Focused GUI Foundation 1A and 1B tests passed against the implementation worktree.

## Exact stopping point

The large GUI Foundation 1D test patch is present, but it has not been compiled or executed. Resume by reviewing `tests/runtime/GuiFoundation1DTests.cs`, adding explicit Rust partial-update payload assertions, then build and run `--gui1d-only`. Fix any failures before documentation or broader qualification.

## Remaining work

1. Compile and run the focused GUI Foundation 1D model/native tests.
2. Complete coverage for Rust `update=true` serialization, actual serialized-byte bounds, coalescing, overflow collapse, failure convergence, publication rollback, lifetime invalidation, fairness, and viewer-scale measurements.
3. Run GUI Foundation 1A through 1C and the available Foundations A through G and Phase 0 through 3 regressions.
4. Update the architecture checker and canonical GUI/compatibility documentation without changing public API or version identities.
5. Run available Windows/Linux, sanitizer, packaging, and live Carbon qualification; clearly record any unavailable live infrastructure.
6. Review the final diff, configure the required GitHub noreply identity, then commit and push only after all required available gates pass.

No Activated ingress, action tokens, private client command, event rate limiting, GUI Foundation 1E behavior, or public version change has been started.
