# Persistence-1B adapter teardown gate

This isolated `net48` executable links the actual `CarbonLuau.Main.cs`,
`CarbonLuau.Persistence.cs`, managed runtime, Core shared sources, and persistence
supervisor/queue. It uses the supplied real native VM, pinned compiler, and storage
worker. Only Carbon/Oxide host facilities and unused gameplay adapter glue are
replaced by test stubs. No production injection hooks or source rewriting exist.

The two cases run on the runtime owner thread:

1. A real worker Get completes and occupies a reserved native callback slot. The
   stub `NextFrame` throws when the actual `OnTick` schedules its drain. The
   production `OnTick` catch must perform full `ReleaseNative()` teardown.
2. A real worker Get completion executes through the actual scheduled drain and
   submits another public Get. Test-only permission-registration glue throws
   before the next request dispatches. The production `RequestDrain` callback
   catch must perform the same teardown while that later callback is outstanding.

Assertions cover the reached injection site, exact diagnostic category, no leaked
exception details, no stale callback execution, empty request ledger and buffers,
zero live VMs and managed callback roots, native library unmapping, supervisor
thread exit, inert repeated unload/late tick, no replay, and successful new-worker
reacquisition of the exact storage directory after cleanup. These are finite
owned-resource checks, not a whole-process GC/heap leak or throughput benchmark.

## Remote build and run

Build only on the parent-coordinated worker:

```powershell
dotnet build tests/persistence-adapter/PersistenceAdapterTests.csproj -c Release
```

CLI arguments, all absolute paths:

```text
PersistenceAdapterTests.exe <native-library> <storage-worker> <fixture-parent>
mono PersistenceAdapterTests.exe <native-library> <storage-worker> <fixture-parent>
```

Use the first invocation on Windows and the second on glibc Linux. Supply the
current production ABI 1.5 native library and matching production storage worker.
The matching `carbonluau_compiler.exe` or `carbonluau_compiler` must sit beside the
supplied native library. The executable stages all three binaries under a fresh
`PersistenceAdapter-<GUID>` child of the fixture parent. Linux helpers are marked
executable. Use a qualified local filesystem, including the parent's ext4 test
volume on Linux, rather than a Windows-backed container mount.

Successful runs print two `PASS` lines and `ALL PASS`, then remove only their
owned GUID directories. Failures return 1 and preserve those directories for
inspection after attempting bounded cleanup. No existing database or server
installation is used. Windows error dialogs are disabled before loading native
code. The parent runner should additionally impose a 180-second process timeout.

This is the necessary Persistence-1B completion-teardown adapter gate. It does
not execute Carbon itself, reproduce actual process-wide OOM, prove a real Carbon
`NextFrame` failure, or qualify Persistence-1C. The scheduling/permission faults
are deliberate test-host exceptions against unchanged production catch paths.
