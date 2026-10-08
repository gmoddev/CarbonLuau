# Foundation 2 — exact native Position ordering investigation

## Current disposition — 2026-10-04

[Foundation 2A](WorldEntityFoundation2A.md) owns the current qualified
supported-host owner-thread direct TRS borrow conclusion and its final gates.
That producer copies actual transform ancestry with capacity checks, a 65-record
cap and finite managed root-world composition, without entering the guarded
Transform getter or waiting for scalar jobs. Its structural-storage premise and
exact Entity lifetime validation remain mandatory; permitted external value
races do not waive memory safety or create an atomic snapshot. The earlier
guard/publisher serialization theorem is not a prerequisite for this route.
The [current observation disposition](WorldEntityObservationBoundaryInvestigation.md#current-disposition--2026-10-04)
records the latest Linux fixed-pump live PASS and pending Windows status. Final
Foundation 2A PASS remains conditional on final gates; existing public Position
semantics and the getter's historical evidence are unchanged.

## Preserved investigation record (historical scope)

All native tokens, wait/publisher findings and negative probe receipts below are
preserved. Their incomplete serialization proof and then-unqualified conclusions
remain accurate for the guarded-getter route, not a prohibition of the later
qualified supported-host direct-copy premise.

**Preserved guarded-getter route.** The latest
[position-alternatives investigation](WorldEntityPositionAlternativesInvestigation.md)
continues through independent cache writers, host phases and snapshot-job
admission. Do not repeat this guard/publisher research as the default next task
or interpret it as the only possible position architecture.

**FOUNDATION 2 ASYNC TRAVERSAL NEEDS REVIEW — native dependency publication
and hierarchy-storage stability across the bounded guard/read interval remain
unproved on the exact Windows/Linux host.**

## Scope and disposition

Continuation on 2026-10-04 from
`f36ff7c83a9e273816f7b726fe3062618b430539`, preserving all intentionally
uncommitted research and the direct completed-Spawn catalog implementation.
This does not reopen catalog, discovery, movement or spatial-index architecture.
[The catalog receipt](WorldEntityCatalogInvestigation.md) retains its Windows/Linux
17,275 production-linked model checks and the distinct live-activation gates.
This investigation addresses only the per-candidate current root Position bound.

No concurrent publisher counterexample was demonstrated. The outcome is an
**incomplete serialization proof**, not proof that bounded complete traversal is
impossible. No numeric traversal policy, unsafe reader, production native patch,
discovery callback binding or public discovery API is adopted. Pending-work
deferral is not an accepted substitute for closing the read interval.

## Exact target and reproduction

Reuse the qualified Rust `25653776`, protocol `2634.289.1`, revision `166494`,
Windows Carbon `2.0.262.0` / `8a81d70`, Linux Carbon `2.0.261.0` / `c74c4ca`.
Both native images embed Unity `6000.3.15x1-13 (a91cf34396ee)`.

| Native image | SHA-256 |
|---|---|
| Windows UnityPlayer.dll | `6ca8f6b3999f2de3201d973b56214bd82eb82e6fa31054c208c2e90b054f274a` |
| Linux UnityPlayer.so | `ab9b4ef10cfbfeee199fa00234164df0782f218bce0a579d9123439a4ca1faf1` |

All addresses below are Linux ELF virtual addresses or Windows PE RVAs, not
runtime absolute pointers. ELF `.text` begins at VA `0x7d4a20`, file offset
`0x7d3a20`; do not use a virtual address directly as a file offset.

`tools/Research-UnityPositionOrdering.py` adds read-only PE `.pdata` / ELF
`.eh_frame_hdr` entry attribution and raw branch/RIP/ELF relocation candidate
inventory. It does not load, execute or patch Unity. Unwind starts and raw
references are inspection aids, **not an exhaustive caller graph or thread proof**.
Decode candidates at independently established instruction boundaries. The
existing `Research-UnityPositionBodies.py` supplies correctly mapped body reads.

## New result: completed fences have a fixed native fast path

The hierarchy dependency word at `[Hierarchy+0]` packs a low 32-bit group index
and high 32-bit generation. `[Hierarchy+8]` is a companion fence field, **not**
the generation compared by the queue completion predicate.

| Path | Linux | Windows |
|---|---|---|
| Fixed completion predicate | `0xc08c80..0xc08cad` | `0x562590..0x5625f1` |
| Queue location | `[Service+0x20]` | same |
| Group table | `[Queue+0x08]`, stride `0x40` | same |
| Completed wait branch | `0xc08015`, branch `0xc08019` to `0xc08598` | `0x566696`, branch `0x566698` to `0x566c41` |

The valid-token predicate compares the group slot's generation to the captured
generation. Inequality indicates completion. These status paths are fixed reads,
comparisons and branches; they do not flush, allocate, invoke callbacks or wait.
The inspected ordinary getter's completed branch likewise returns before its
expensive wait/help/profiler paths. Linux forwarding is `0xc044c0` ->
`0xc05fd0` / `0xc05c30` -> `0xc07fd0`; clearing follows the wait's return.
Windows ordinary/Handle getter forwards to `0x566650` then clears its fence.

This is distinct from public `JobHandle.IsCompleted`, which invokes the scheduler
flush path. The public API is not a qualified constant-cost guard. A future exact
adapter must reject null/unqualified queue state, invalid group indices or zero
generations rather than inherit helpers' permissive invalid-handle returns.

Completing a job is irreversible, but retaining its fence does not reserve its
group-table slot. Linux retirement `0xc0ad80` advances a zero-skipping generation
at `0xc0ae2c`; Windows `0x567250` reaches the generation-checked CAS through
`0x564a40`. Allocation retains the current slot generation. One reuse does not
resurrect an old token, but a complete 32-bit generation cycle can repeat it.
Consequently the repeated completed check inside an ordinary getter also needs
queue-lifetime/generation stability during the read interval. This is a theoretical
work-bound obligation, **not an observed full-cycle ABA**. The zero-only guard
does not add that group-reuse obligation. A no-wait accessor avoids the second
wait check but still needs the relevant hierarchy publication/storage proof.

## Publisher and phase trace: registration is not serialization

The Linux transform-job wrappers `0xd2ab50` / `0xd2abd0` reach the hierarchy
publisher `0xd23800`. The native callback `0xb940e0` also schedules/flushes work
before publication at call site `0xb94884`. Its associated inspected worker
`0xb98f60` writes change masks; this did not establish concurrent topology/TRS
relocation or close the publisher's calling-context theorem.

New concrete callback ownership evidence:

- Initializer `0xb94000` takes the manager pointer through global `0x22a84e0`.
- `0xb9403e` loads callback address `0xb940e0`; `0xb94045` selects slot 2;
  `0xb9404a` calls registration method `0xaae170`.
- `0xaae170` stores that pointer at `[Manager + Slot*0x48 + 8]`, slot-2 offset
  `0x98`. Manager initialization `0xaae2d0` creates its bounded 20-slot table.
- Inspected collection dispatch `0xaadb40` invokes subscription callbacks and
  calls `0xd22f70` at `0xaadca7`. The latter also publishes dependencies **inline**
  at `0xd23505` / `0xd23508`. Counting only calls to `0xd23800` would therefore
  be an incomplete publisher inventory.
- Global-pointer references were attributed to 46 unwind entries; several are
  registration, lazy-component processing or frame-stamp paths. The direct
  dispatcher callers inspected include `0x991890`, `0x9aee80`, `0x9ce1a0`,
  `0xabda00`, `0xbbe9b0`, `0xbc6e90` and `0xf7fd30`. This candidate inventory
  does not prove every indirect dispatch or native producer's thread affinity.

The callback is not merely an unnamed address now: its native registration is
identified. However, the inspected chain has **not proved its invocation is
serialized with CarbonLuau's uninterrupted owner-thread turn**, nor established
a complete exclusion of inlined/indirect publication during that turn. A
PlayerLoop-looking caller, an initialized frame stamp or registration slot cannot
substitute for that proof. No bounded per-candidate lease was found in the
inspected publisher/read paths.

Unity's [public job creation rules](https://docs.unity3d.com/cn/2022.3/Manual/JobSystemCreatingJobs.html)
restrict managed scheduling/completion to the main thread. That excludes new
ordinary managed scheduling inside an uninterrupted owner-thread segment, but is
not exact-build proof of the engine's internal callback/publication affinity.
Foundation 1's accepted getter correctness remains unchanged; the additional
Foundation 2 requirement is bounded work, not requalification of every possible
movement writer.

## Alternatives and narrow-adapter review

### Independent Windows publisher trace

The same exact Windows image independently exposes these counterparts:

| Mechanism | Windows RVA / observation |
|---|---|
| Hierarchy enrollment | `0x668c30`; fence store at `0x668c50` |
| Collection dispatch | `0x668790` calls `0x669a90` at `0x668bb8`; helper publishes at `0x669ade` |
| Schedule/flush-before-enrollment callback | `0x4edb10`; schedule `0x4ee2a5`, flush `0x4ee2bc`, enroll `0x4ee2eb` |
| Slot-2 registration | Initializer `0x4f8d30` reads manager through global `0x22172e8`; callback address at `0x4f8d9b` is stored at `[Manager+0x98]` at `0x4f8da7` |

The Windows publishers use a 16-byte `movups`, unlike the inspected Linux
separate stores. This is not an atomic 128-bit publication guarantee. Neither
inspected Windows publication body establishes reader exclusion or thread affinity.

Static icall table inspection, cross-checked with the previously runtime-resolved
Position and completion icalls, identifies writable/read-only scheduling wrappers
`0x64220` / `0x64280` forwarding to `0x56abc0` / `0x56b340`. They call
`0x5606c0`, which compares the current thread ID with global main-thread ID
`0x21bf5f0` at `0x560726`. **This is a routing check, not a rejection/assertion:**
the off-main path can enqueue bookkeeping or return to the scheduling caller.
It cannot be promoted to an enforcing main-thread scheduling boundary.

These findings strengthen the platform mechanism comparison, not the missing
slot-2 invocation/dispatch serialization proof. No supported concurrent publisher
was observed on either platform.

| Candidate | Why it does not yet close the gate |
|---|---|
| Zero-only dependency guard + capped parents + normal Position | Best conservative candidate; it avoids completed-token reuse, but still needs exclusion of new relevant publication and stable descriptor/topology storage. |
| Zero/completed guard + normal Position | Fixed completed fast path is newly established; does not acquire a lease and adds the repeated-generation-check obligation above. |
| TransformAccess / Facepunch Unsafe.GetPosMT | No dependency wait, but ancestry remains. Job-style access supplies no independent owner-thread storage/dependency lease for arbitrary world objects. |
| Combined position/rotation or matrix | Inspected implementations still wait and/or walk parents. No independent universal fresh world scalar was established. |
| Managed/server/network cached or local position | Local or specialized state cannot replace current root world Position. |
| Synchronous read-only transform job | Dependency collection/array preparation may complete prior jobs and process hierarchy/array state; neither bounded setup nor bounded wait is established. |
| Asynchronous read job | Changes encounter-time owner-thread read semantics; completion is not a demonstrated bounded current-position read primitive. |
| Reader-only guard/patch | Can cap ancestry and refuse pending work, but cannot prevent an uncoordinated writer from publishing work or invalidating captured storage. Rechecking after reading is not a lease. |

A writer-participating lease would require complete coverage of relevant native
publication and hierarchy storage changes. Neither one reader patch nor patching
only `0xd23800` provides that coverage. No broad Unity detour, movement system or
unsupported native lock was introduced to manufacture a result.

**Accepted failure policy:** genuine pending work, excessive depth or invalid
read authority fails the **whole query** controlledly, with no partial success.
There is no candidate omission, retry-until-quiescent or pending-job deferral
architecture. This policy does not require every encounter to be quiescent.
It does require that a guard accepted as safe cannot subsequently enter an
unbounded getter wait. An absolute query deadline cannot interrupt such a wait.

## Matching symbols/source avenue

Linux debug identity is build ID `bfce41964503bbc0dd782d99c6a7e18bdc4dd0a9`,
debuglink `UnityPlayer_s.debug`, CRC32 `a26870d4`; no `.symtab`/embedded native
debug sections were found. Windows RSDS identity is GUID
`d120a3b4-1f82-4fdb-8a79-88250feab61e`, age 4, filename
`UnityPlayer_Win64_server_mono_x64.pdb`.

Unity's [documented symbol server](https://docs.unity3d.com/6000.3/Documentation/Manual/WindowsDebugging-instructions.html)
returned 404 for the exact GUID/age PDB lookup, compressed form and `file.ptr`.
The release API returned no exact custom-version entry. Probed standard package
paths for revision `a91cf34396ee` also returned 404; these inferred paths do not
prove global artifact absence. Stock `6000.3.15f1` has a different revision and
was not substituted. Conventional Linux symbol lookup probes are undocumented
and carry only limited negative evidence. Availability of this exact custom
revision through [licensed native source access](https://unity.com/products/source-code)
remains unverified. No installation or large unrelated download was performed.

Matching symbols/source would aid the remaining dispatch trace; names alone would
still not prove serialization. Missing public artifacts are an evidence limit,
not proof that the architecture is impossible.

## Qualification and next exact gate

Independent read-only reviews covered completed-wait work, group retirement/reuse,
alternative read paths and matching symbol availability. New evidence is native
binary inspection, not new live catalog or traversal qualification. The earlier
Windows/Linux task-owned getter observations and catalog model results are inherited
unchanged. No new live server, native sanitizer run, hosted CI, package/release,
commit or push is claimed by this receipt.

Final local `Test-Architecture.ps1` (including GiveItem structural safety),
`Test-Api.ps1` (including relative links), `Test-Release.ps1`, Python AST syntax
and `git diff --check` passed after the receipt updates. These are structural/
documentation checks, not newly built release packages or runtime proof. The
catalog's three source hashes still match its preserved qualified model receipt.
The new read-only inspection tool SHA-256 is
`a3d5adc3239572df701b3bdd560a1931aebc35b01f8d69c836e8862b0de806cd`.

The remaining exact gate is a proven owner-thread exclusion/lease for **all
relevant dependency publication and hierarchy storage changes during one capped
candidate read**, including the identified native callback and inlined publisher.
If that closes, qualify Handle acquisition/validity/marshaling, select shared
numeric traversal limits and finish the private scheduler/callback/live matrix.
Until then, production discovery remains disabled. Do not revisit catalog or
movement architecture, substitute timing for work, or call the absent interval
proof a supported concurrent-publication counterexample.
