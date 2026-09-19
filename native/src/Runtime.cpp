#include "carbonluau_native.h"
#include "runtime/RuntimeInternal.hpp"
#include "scripts/Compiler.hpp"
#include <cstring>
#include <limits>
#include <memory>
#include <mutex>

using namespace CarbonLuau::Runtime;

uint32_t carbonluau_abi_version(void) { return 0x00010004; }
ClStatus cl_luau_revision(char* Buffer, uint32_t Capacity)
{
    if (!Buffer || Capacity < sizeof(CARBONLUAU_REVISION)) return CL_INVALID_ARGUMENT;
    std::memcpy(Buffer, CARBONLUAU_REVISION, sizeof(CARBONLUAU_REVISION));
    return CL_OK;
}

ClStatus cl_vm_create(const ClVmConfig* Config, ClHandle* OutVm) try
{
    if (OutVm) *OutVm = 0;
    if (!Config || !OutVm || Config->MemoryLimitBytes < 16 * MiB || Config->MemoryLimitBytes > 256 * MiB)
        return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    for (auto& Entry : Registry) if (!Entry) {
        if (NextId == std::numeric_limits<uint64_t>::max() || NextVmGenerationId == std::numeric_limits<uint64_t>::max()) return CL_INTERNAL_ERROR;
        auto Candidate = std::make_unique<Vm>();
        Candidate->Limit = Config->MemoryLimitBytes;
        Candidate->State = lua_newstate(Allocate, Candidate.get());
        if (!Candidate->State) return CL_MEMORY_LIMIT;
        lua_callbacks(Candidate->State)->userdata = Candidate.get();
        int Status = lua_cpcall(Candidate->State, Initialize, nullptr);
        if (Status != LUA_OK) return Status == LUA_ERRMEM ? CL_MEMORY_LIMIT : CL_INTERNAL_ERROR;
        lua_settop(Candidate->State, 0);
        Candidate->Id = NextId++;
        Candidate->GenerationId = NextVmGenerationId++;
        *OutVm = Candidate->Id;
        Entry = std::move(Candidate);
        return CL_OK;
    }
    return CL_INTERNAL_ERROR;
} catch (...) { return CL_INTERNAL_ERROR; }
ClStatus cl_vm_destroy(ClHandle Id) try
{
    if (!Id) return CL_OK;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || Runtime->Admission) return CL_INVALID_ARGUMENT;
    for (auto& Entry : Registry) if (Entry.get() == Runtime) { Entry.reset(); return CL_OK; }
    return CL_INTERNAL_ERROR;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_info(ClHandle Id, ClVmInfo* Info) try
{
    if (!Info) return CL_INVALID_ARGUMENT;
    *Info = {};
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime) return CL_INVALID_ARGUMENT;
    *Info = {Runtime->Used, Runtime->Limit, Runtime->State ? uint64_t(1) : uint64_t(0)};
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

static ClStatus LoadSourceLocked(Vm* Runtime, Domain* Owner, const char* Chunk, const char* Source, uint32_t Length,
    ClHandle* OutThread, ClResult* Result) try
{
    if (OutThread) *OutThread = 0;
    if (Result) *Result = {};
    if (!Chunk || !Source || !OutThread || !Result || Length > 65536) return CL_INVALID_ARGUMENT;
    size_t ChunkLength = 0;
    while (ChunkLength < 128 && Chunk[ChunkLength]) {
        unsigned char Character = Chunk[ChunkLength++];
        if (!(Character >= 'a' && Character <= 'z') && !(Character >= 'A' && Character <= 'Z') &&
            !(Character >= '0' && Character <= '9') && Character != '_' && Character != '-' && Character != '.') return CL_INVALID_ARGUMENT;
    }
    if (!ChunkLength || ChunkLength >= 128) return CL_INVALID_ARGUMENT;
    if (!Runtime || Runtime->ThreadId) return CL_INVALID_ARGUMENT;
    if (Runtime->Admission || (Owner && !Owner->Alive)) return CL_INVALID_ARGUMENT;
    if (!Runtime->State) { Result->Flags = 1; return CL_INTERNAL_ERROR; }
    Runtime->Sealed = true;
    std::memcpy(Runtime->Chunk, Chunk, ChunkLength + 1);
    Runtime->AllocationFailed = false;
    Runtime->IntegrityFailed = false;
    try {
        CompileResult Compilation;
        { RegistryWaitScope Wait; Compilation = CompileSource(std::string(Source, Length)); }
        if (Compilation.Status != CompileStatus::Success) {
            Diagnostic(*Runtime, *Result, Compilation.Diagnostic.c_str());
            return Compilation.Status == CompileStatus::Timeout ? CL_TIMEOUT : CL_COMPILE_ERROR;
        }
        std::string& Bytecode = Compilation.Payload;
        if (Bytecode.size() > 1024 * 1024) {
            Diagnostic(*Runtime, *Result, "compiled bytecode exceeds 1 MiB ingestion bound");
            return CL_INVALID_ARGUMENT;
        }
        if (Bytecode.empty()) { Diagnostic(*Runtime, *Result, "compiler returned no bytecode"); return CL_INTERNAL_ERROR; }
        if (Bytecode[0] == 0) {
            Diagnostic(*Runtime, *Result, Bytecode.c_str() + 1);
            return CL_COMPILE_ERROR;
        }
        int Status = lua_cpcall(Runtime->State, CreateThread, Runtime);
        if (Status == LUA_OK) Status = lua_cpcall(Runtime->Thread, SandboxThread, nullptr);
        if (Status == LUA_OK) {
            lua_settop(Runtime->Thread, 0);
            if (Owner) InstallDomainBindings(Runtime->Thread, *Owner);
            Status = luau_load(Runtime->Thread, Runtime->Chunk, Bytecode.data(), Bytecode.size(), 0);
        }
        if (Status != LUA_OK) {
            Diagnostic(*Runtime, *Result, "source loading failed");
            bool Memory = Runtime->AllocationFailed || Status == LUA_ERRMEM;
            Retire(*Runtime);
            Result->Flags = 1;
            return Memory ? CL_MEMORY_LIMIT : CL_INTERNAL_ERROR;
        }
        if (NextId == std::numeric_limits<uint64_t>::max()) { Retire(*Runtime); Result->Flags = 1; return CL_INTERNAL_ERROR; }
        Runtime->ThreadId = NextId++;
        Runtime->ThreadDomain = Owner;
        Runtime->Resumable = true;
        Runtime->Started = false;
        *OutThread = Runtime->ThreadId;
        return CL_OK;
    } catch (...) {
        bool Memory = Runtime->AllocationFailed;
        Diagnostic(*Runtime, *Result, "native compile/load failure; VM retired");
        Retire(*Runtime);
        Result->Flags = 1;
        return Memory ? CL_MEMORY_LIMIT : CL_INTERNAL_ERROR;
    }
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_load_source(ClHandle Id, const char* Chunk, const char* Source, uint32_t Length,
    ClHandle* OutThread, ClResult* Result) try
{
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    return LoadSourceLocked(Runtime, Runtime && Runtime->Scripts ? Runtime->LegacyDomain : nullptr,
        Chunk, Source, Length, OutThread, Result);
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_load_source(ClHandle Id, ClHandle DomainId, const char* Chunk, const char* Source,
    uint32_t Length, ClHandle* OutThread, ClResult* Result) try
{
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId) : nullptr;
    if (!Owner) { if (OutThread) *OutThread = 0; if (Result) *Result = {}; return CL_INVALID_ARGUMENT; }
    return LoadSourceLocked(Runtime, Owner, Chunk, Source, Length, OutThread, Result);
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_scripts(ClHandle Id, uint32_t MaxQueued) try
{
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || !Runtime->State || Runtime->Admission || Runtime->Sealed || Runtime->Scripts || MaxQueued < 1 || MaxQueued > 4096) return CL_INVALID_ARGUMENT;
    Runtime->LegacyDomain = AddDomain(*Runtime, MaxQueued);
    if (!Runtime->LegacyDomain) return CL_INVALID_ARGUMENT;
    Runtime->LegacyDomain->Active = true;
    if (lua_cpcall(Runtime->State, InstallScripts, nullptr) != LUA_OK) { Retire(*Runtime); return CL_MEMORY_LIMIT; }
    lua_settop(Runtime->State, 0);
    Runtime->Scripts = true;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_module(ClHandle Id, const char* Name, const char* Source, uint32_t Length) try
{
    if (!Name || !Source || Length > 65536) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? Runtime->LegacyDomain : nullptr;
    if (!Runtime || !Runtime->State || Runtime->Admission || !Runtime->Scripts || !Owner || Runtime->Sealed || Owner->Modules.size() >= 256 ||
        Owner->SourceBytes + Length > 4 * MiB || !ModuleName(Name, 128)) return CL_INVALID_ARGUMENT;
    auto Added = Owner->Modules.emplace(Name, Module{std::string(Source, Length)});
    if (!Added.second) return CL_INVALID_ARGUMENT;
    Owner->SourceBytes += Length;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_module(ClHandle Id, ClHandle DomainId, const char* Name, const char* Source, uint32_t Length) try
{
    if (!Name || !Source || Length > 65536) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId) : nullptr;
    if (!Runtime || !Runtime->State || Runtime->Admission || !Owner || Owner->Active || Owner->Modules.size() >= 256 ||
        Owner->SourceBytes + Length > 4 * MiB || !ModuleName(Name, 128)) return CL_INVALID_ARGUMENT;
    auto Added = Owner->Modules.emplace(Name, Module{std::string(Source, Length)});
    if (!Added.second) return CL_INVALID_ARGUMENT;
    Owner->SourceBytes += Length;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_addon(ClHandle Id, ClHandle DomainId, const char* PackageId, const char* Version,
    const char* MainModule) try
{
    if (!PackageId || !Version || !MainModule) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId) : nullptr;
    if (!Runtime || !Runtime->State || Runtime->Admission || Runtime->ThreadId || !Owner || Owner->Active ||
        !Owner->PackageId.empty() || !PackageName(PackageId) || !PackageVersion(Version)) return CL_INVALID_ARGUMENT;
    if (*MainModule) {
        if (!ModuleName(MainModule, 128) || Owner->Modules.find(MainModule) == Owner->Modules.end()) return CL_INVALID_ARGUMENT;
        Owner->MainModule = MainModule;
        Owner->PublicModules.push_back(MainModule);
    }
    Owner->PackageId = PackageId;
    Owner->PackageVersion = Version;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_public_module(ClHandle Id, ClHandle DomainId, const char* Name) try
{
    if (!Name) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId) : nullptr;
    if (!Runtime || !Runtime->State || Runtime->Admission || Runtime->ThreadId || !Owner || Owner->Active ||
        Owner->PackageId.empty() || !ModuleName(Name, 128) || Owner->Modules.find(Name) == Owner->Modules.end() ||
        std::find(Owner->PublicModules.begin(), Owner->PublicModules.end(), Name) != Owner->PublicModules.end() ||
        Owner->PublicModules.size() >= 256) return CL_INVALID_ARGUMENT;
    Owner->PublicModules.emplace_back(Name);
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_dependency(ClHandle Id, ClHandle DomainId, const char* PackageId,
    ClHandle TargetDomainId) try
{
    if (!PackageId) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId) : nullptr;
    Domain* Target = TargetDomainId && Runtime ? GetDomain(*Runtime, TargetDomainId, true) : nullptr;
    if (!Runtime || !Runtime->State || Runtime->Admission || Runtime->ThreadId || !Owner || Owner->Active ||
        Owner->PackageId.empty() || !PackageName(PackageId) || (TargetDomainId && !Target) ||
        (Target && Target->PackageId != PackageId) || Owner->Dependencies.size() >= 32) return CL_INVALID_ARGUMENT;
    for (const auto& Binding : Owner->Dependencies) if (Binding.Id == PackageId) return CL_INVALID_ARGUMENT;
    Owner->Dependencies.push_back(DependencyBinding{PackageId, Target});
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_generation(ClHandle Id, ClVmGenerationInfo* Info) try
{
    if (!Info) return CL_INVALID_ARGUMENT;
    *Info = {};
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime) return CL_INVALID_ARGUMENT;
    Info->VmGenerationId = Runtime->GenerationId;
    for (const auto& Item : Runtime->Domains) if (Item && Item->Alive) ++Info->Domains;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_create(ClHandle Id, uint32_t MaxQueued, ClHandle* OutDomain) try
{
    if (OutDomain) *OutDomain = 0;
    if (!OutDomain) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || !Runtime->State || Runtime->Admission || Runtime->ThreadId) return CL_INVALID_ARGUMENT;
    Domain* Owner = AddDomain(*Runtime, MaxQueued);
    if (!Owner) return CL_INVALID_ARGUMENT;
    *OutDomain = Owner->Id;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_commit(ClHandle Id, ClHandle DomainId) try
{
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = Runtime ? GetDomain(*Runtime, DomainId) : nullptr;
    if (!Runtime || !Runtime->State || !Owner || Owner->Active || Runtime->Admission || Runtime->ThreadId) return CL_INVALID_ARGUMENT;
    for (const auto& Item : Owner->PendingModules) {
        if (Item.Owner != Owner || !Item.Value || Item.Value->Loaded) { Retire(*Runtime); return CL_INTERNAL_ERROR; }
        Item.Value->Reference = Item.Reference; Item.Value->Loaded = true;
    }
    Owner->PendingModules.clear();
    Owner->Queue.insert(Owner->Queue.end(), std::make_move_iterator(Owner->PendingCallbacks.begin()),
        std::make_move_iterator(Owner->PendingCallbacks.end()));
    Owner->PendingCallbacks.clear();
    std::make_heap(Owner->Queue.begin(), Owner->Queue.end(), Later{});
    Owner->Active = true;
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_domain_destroy(ClHandle Id, ClHandle DomainId) try
{
    if (!DomainId) return CL_OK;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    Domain* Owner = nullptr;
    if (Runtime) for (const auto& Item : Runtime->Domains) if (Item && Item->Id == DomainId) { Owner = Item.get(); break; }
    if (!Runtime || !Owner || Runtime->Admission) return CL_INVALID_ARGUMENT;
    if (!Owner->Alive) return CL_OK;
    if (Runtime->ThreadDomain == Owner || (Runtime->Admission && Runtime->Admission->Owner == Owner))
        return CL_INVALID_ARGUMENT;
    ReleaseDomain(*Runtime, *Owner);
    if (Runtime->LegacyDomain == Owner) Runtime->LegacyDomain = nullptr;
    if (Runtime->State) {
        if (lua_cpcall(Runtime->State, Collect, nullptr) != LUA_OK) { Retire(*Runtime); return CL_INTERNAL_ERROR; }
        lua_settop(Runtime->State, 0);
    }
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_scheduler(ClHandle Id, ClSchedulerInfo* Info) try
{
    if (!Info) return CL_INVALID_ARGUMENT;
    *Info = {};
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || (!Runtime->Scripts && Runtime->Domains.empty())) return CL_INVALID_ARGUMENT;
    Info->NowNs = NowNs(); Info->Sequence = Runtime->Sequence;
    Info->Discarded = Runtime->RetiredDiscarded;
    for (const auto& Item : Runtime->Domains) if (Item) {
        const Domain& Owner = *Item;
        Info->Rejected += Owner.Rejected;
        if (!Owner.Alive || !Owner.Active) continue;
        Info->Queued += Owner.Queue.size();
        if (!Owner.Queue.empty() && (!Info->NextDueNs || Owner.Queue.front().Due < Info->NextDueNs)) Info->NextDueNs = Owner.Queue.front().Due;
        for (const auto& Entry : Owner.Modules) if (Entry.second.Loaded) ++Info->Modules;
    }
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_vm_callback(ClHandle Id, uint64_t CutoffNs, uint64_t Sequence, uint64_t BudgetNs,
    uint32_t* Ran, ClResult* Result) try
{
    if (Ran) *Ran = 0;
    if (Result) *Result = {};
    if (!Ran || !Result || BudgetNs < 1000000 || BudgetNs > 100000000) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetVm(Id);
    if (!Runtime || !Runtime->State || Runtime->ThreadId || Runtime->Admission) return CL_INVALID_ARGUMENT;
    Domain* Selected = nullptr;
    for (const auto& Item : Runtime->Domains) if (Item && Item->Alive && Item->Active && !Item->Queue.empty()) {
        const Callback& Candidate = Item->Queue.front();
        if (Candidate.Due > CutoffNs || Candidate.Sequence > Sequence) continue;
        bool CandidateAfterCursor = Item->Id > Runtime->SchedulerCursor;
        bool SelectedAfterCursor = Selected && Selected->Id > Runtime->SchedulerCursor;
        if (!Selected || (CandidateAfterCursor && !SelectedAfterCursor) ||
            (CandidateAfterCursor == SelectedAfterCursor && Item->Id < Selected->Id)) Selected = Item.get();
    }
    if (!Selected) return CL_OK;
    Runtime->SchedulerCursor = Selected->Id;
    std::pop_heap(Selected->Queue.begin(), Selected->Queue.end(), Later{});
    Callback Work = std::move(Selected->Queue.back()); Selected->Queue.pop_back();
    *Ran = 1;
    Runtime->Thread = Work.Thread; Runtime->Reference = Work.Reference;
    Runtime->ThreadDomain = Work.Owner;
    Runtime->LogSize = 0; Runtime->Logs[0] = 0; Runtime->LogTruncated = false;
    Runtime->AllocationFailed = false;
    Runtime->IntegrityFailed = false;
    if (!Work.Gate.empty()) {
        uint32_t Written = 0;
        Domain& Owner = *Work.Owner;
        if (!Owner.Alive || !Owner.Active || !Owner.Host || Owner.Host(Owner.HostIdentity, 9, Work.Gate.data(), uint32_t(Work.Gate.size()),
            Owner.HostBuffer->data(), uint32_t(Owner.HostBuffer->size()), &Written) != 0) {
            ++Owner.Rejected;
            ReleaseThread(*Runtime, false);
            if (!Runtime->State) Result->Flags = 1;
            return CL_OK;
        }
    }
    std::snprintf(Runtime->Chunk, sizeof(Runtime->Chunk), "scheduled-%llu", (unsigned long long)Work.Sequence);
    Runtime->Deadline = std::chrono::steady_clock::now() + std::chrono::nanoseconds(BudgetNs);
    AdmissionContext Admission{Work.Owner, ++Runtime->OperationSequence, false};
    Runtime->Admission = &Admission;
    lua_callbacks(Runtime->State)->interrupt = Interrupt;
    ClStatus Status;
    try {
        int Code = lua_resume(Work.Thread, nullptr, Work.Arguments);
        lua_callbacks(Runtime->State)->interrupt = nullptr;
        Runtime->Admission = nullptr;
        if (Runtime->AllocationFailed || Code == LUA_ERRMEM) {
            Status = CL_MEMORY_LIMIT; Diagnostic(*Runtime, *Result, "callback memory limit", Work.Thread);
        } else if (Code == LUA_OK) Status = CL_OK;
        else {
            Status = CL_RUNTIME_ERROR;
            const char* Message = Code == LUA_YIELD ? "scheduled callbacks cannot yield" :
                (lua_type(Work.Thread, -1) == LUA_TSTRING ? lua_tostring(Work.Thread, -1) : "callback non-string error");
            Diagnostic(*Runtime, *Result, Message, Work.Thread);
        }
        if (Runtime->IntegrityFailed) {
            Status = CL_INTERNAL_ERROR;
            Diagnostic(*Runtime, *Result, "publication integrity failure; VM retired");
            Retire(*Runtime);
        } else ReleaseThread(*Runtime, Status == CL_MEMORY_LIMIT);
        if (!Runtime->State) Result->Flags |= 1;
    } catch (const DeadlineExceeded&) {
        Runtime->Admission = nullptr;
        Status = CL_TIMEOUT; Diagnostic(*Runtime, *Result, "callback deadline exceeded; VM retired");
        Retire(*Runtime); Result->Flags |= 1;
    } catch (...) {
        Runtime->Admission = nullptr;
        Status = CL_INTERNAL_ERROR; Diagnostic(*Runtime, *Result, "callback native failure; VM retired");
        Retire(*Runtime); Result->Flags |= 1;
    }
    std::memcpy(Result->Logs, Runtime->Logs, sizeof(Result->Logs));
    if (Runtime->LogTruncated) Result->Flags |= 2;
    return Status;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_thread_resume(ClHandle Id, uint64_t BudgetNs, ClResult* Result) try
{
    if (Result) *Result = {};
    if (!Result || BudgetNs < 1000000 || BudgetNs > 100000000) return CL_INVALID_ARGUMENT;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetThread(Id);
    if (!Runtime || Runtime->Admission || !Runtime->Resumable) return CL_INVALID_ARGUMENT;
    Runtime->LogSize = 0;
    Runtime->Logs[0] = 0;
    Runtime->LogTruncated = false;
    Runtime->AllocationFailed = false;
    Runtime->IntegrityFailed = false;
    Runtime->Deadline = std::chrono::steady_clock::now() + std::chrono::nanoseconds(BudgetNs);
    lua_callbacks(Runtime->State)->interrupt = Interrupt;
    ClStatus Status = CL_INTERNAL_ERROR;
    AdmissionContext Admission{Runtime->ThreadDomain, ++Runtime->OperationSequence,
        Runtime->ThreadDomain && !Runtime->ThreadDomain->Active};
    Runtime->Admission = &Admission;
    try {
        std::unique_ptr<PublicationScope> Publication;
        if (Admission.Provisional) Publication = std::make_unique<PublicationScope>(*Runtime);
        if (Runtime->Started) lua_settop(Runtime->Thread, 0);
        Runtime->Started = true;
        int Code = lua_resume(Runtime->Thread, nullptr, 0);
        lua_callbacks(Runtime->State)->interrupt = nullptr;
        Runtime->Resumable = Code == LUA_YIELD;
        if (Runtime->AllocationFailed || Code == LUA_ERRMEM) {
            Status = CL_MEMORY_LIMIT;
            Diagnostic(*Runtime, *Result, "VM allocation limit/exhaustion", Runtime->Thread);
            // Even a pcall-caught allocation refusal invalidates this execution.
            Runtime->Resumable = false;
        } else if (Code == LUA_OK || Code == LUA_YIELD) {
            Status = Code == LUA_OK ? CL_OK : CL_YIELDED;
            if (Code == LUA_OK && Publication) Publication->Commit();
            if (lua_gettop(Runtime->Thread) && lua_type(Runtime->Thread, 1) == LUA_TNUMBER) {
                Result->Number = lua_tonumber(Runtime->Thread, 1); Result->HasNumber = 1;
            }
        } else {
            Status = CL_RUNTIME_ERROR;
            const char* Message = lua_type(Runtime->Thread, -1) == LUA_TSTRING ? lua_tostring(Runtime->Thread, -1) : "non-string runtime error";
            Diagnostic(*Runtime, *Result, Message, Runtime->Thread);
        }
        if (Runtime->IntegrityFailed) {
            Status = CL_INTERNAL_ERROR;
            Diagnostic(*Runtime, *Result, "publication integrity failure; VM retired");
            Retire(*Runtime); Result->Flags |= 1;
        }
        Runtime->Admission = nullptr;
    } catch (const DeadlineExceeded&) {
        Runtime->Admission = nullptr;
        Status = CL_TIMEOUT;
        Diagnostic(*Runtime, *Result, "monotonic execution deadline exceeded; VM retired");
        Retire(*Runtime);
        Result->Flags |= 1;
    } catch (...) {
        Runtime->Admission = nullptr;
        Status = CL_INTERNAL_ERROR;
        Diagnostic(*Runtime, *Result, "unexpected native execution failure; VM retired");
        Retire(*Runtime);
        Result->Flags |= 1;
    }
    std::memcpy(Result->Logs, Runtime->Logs, sizeof(Result->Logs));
    if (Runtime->LogTruncated) Result->Flags |= 2;
    return Status;
} catch (...) { return CL_INTERNAL_ERROR; }

ClStatus cl_thread_destroy(ClHandle Id) try
{
    if (!Id) return CL_OK;
    std::lock_guard<std::recursive_mutex> Lock(RegistryMutex);
    Vm* Runtime = GetThread(Id);
    if (!Runtime || Runtime->Admission) return CL_INVALID_ARGUMENT;
    ReleaseThread(*Runtime);
    return CL_OK;
} catch (...) { return CL_INTERNAL_ERROR; }
