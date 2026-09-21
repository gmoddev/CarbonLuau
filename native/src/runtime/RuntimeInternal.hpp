#pragma once

#include "carbonluau_native.h"
#include "lua.h"
#include "lualib.h"
#include <algorithm>
#include <array>
#include <chrono>
#include <cmath>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <iterator>
#include <limits>
#include <map>
#include <memory>
#include <mutex>
#include <string>
#include <thread>
#include <vector>

namespace CarbonLuau::Runtime {
constexpr uint64_t MiB = 1024 * 1024;

struct Module { std::string Source; int Reference = LUA_NOREF; bool Loading = false, Loaded = false; };
struct Domain;
struct DependencyBinding { std::string Id; Domain* Target = nullptr; };
struct Callback { uint64_t Due, Sequence; Domain* Owner; lua_State* Thread; int Reference, Arguments; std::string Gate; };
struct Later {
    bool operator()(const Callback& A, const Callback& B) const {
        return A.Due > B.Due || (A.Due == B.Due && A.Sequence > B.Sequence);
    }
};
struct StagedModule { Domain* Owner; Module* Value; int Reference; };
struct PublicationScope;

struct Domain {
    uint64_t Id = 0;
    bool Alive = true, Active = false;
    uint32_t MaxQueued = 0;
    uint64_t Rejected = 0, Discarded = 0;
    size_t SourceBytes = 0;
    std::map<std::string, Module> Modules;
    std::string PackageId, PackageVersion, MainModule;
    std::vector<std::string> PublicModules;
    std::vector<DependencyBinding> Dependencies;
    std::vector<std::string> Loading;
    std::vector<Callback> Queue;
    std::vector<StagedModule> PendingModules;
    std::vector<Callback> PendingCallbacks;
    ClHostCall Host = nullptr;
    uint64_t HostIdentity = 0;
    int Game = LUA_NOREF, Dispatch = LUA_NOREF, GuiBindings = LUA_NOREF;
    std::unique_ptr<std::array<char, 262144>> HostBuffer;
};

struct AdmissionContext {
    Domain* Owner = nullptr;
    uint64_t OperationId = 0;
    bool Provisional = false;
};

struct Vm {
    uint64_t Id = 0;
    uint64_t GenerationId = 0;
    std::thread::id Owner = std::this_thread::get_id();
    uint64_t Used = 0, Limit = 0;
    bool AllocationFailed = false;
    lua_State* State = nullptr;
    lua_State* Thread = nullptr;
    int Reference = LUA_NOREF;
    uint64_t ThreadId = 0;
    Domain* ThreadDomain = nullptr;
    bool Resumable = false;
    bool Started = false;
    std::chrono::steady_clock::time_point Deadline;
    char Chunk[128]{};
    char Logs[4096]{};
    size_t LogSize = 0;
    bool LogTruncated = false;
    bool Scripts = false, Sealed = false;
    uint64_t Sequence = 0, OperationSequence = 0, RetiredDiscarded = 0, SchedulerCursor = 0;
    std::vector<std::string> ModuleLoads;
    std::vector<std::unique_ptr<Domain>> Domains;
    Domain* LegacyDomain = nullptr;
    AdmissionContext* Admission = nullptr;
    PublicationScope* Publication = nullptr;
    bool IntegrityFailed = false;
    int GuiValueEqual = LUA_NOREF;
    ~Vm();
};

extern std::recursive_mutex RegistryMutex;
extern std::array<std::unique_ptr<Vm>, 32> Registry;
extern uint64_t NextId;
extern uint64_t NextVmGenerationId;

// Compilation owns no VM data. The owner thread remains synchronously blocked,
// so its VM/domain lifetime cannot change while unrelated VMs use the registry.
struct RegistryWaitScope {
    RegistryWaitScope() { RegistryMutex.unlock(); }
    ~RegistryWaitScope() { RegistryMutex.lock(); }
    RegistryWaitScope(const RegistryWaitScope&) = delete;
    RegistryWaitScope& operator=(const RegistryWaitScope&) = delete;
};

struct PublicationScope {
    Vm& Runtime;
    PublicationScope* Parent;
    std::vector<StagedModule> Modules;
    std::vector<Callback> Callbacks;
    std::vector<Domain*> Facades;
    bool Complete = false;
    PublicationScope(Vm& Runtime) : Runtime(Runtime), Parent(Runtime.Publication) { Runtime.Publication = this; }
    ~PublicationScope();
    bool Uses(Domain* Owner) const { return std::find(Facades.begin(), Facades.end(), Owner) != Facades.end(); }
    void Commit();
};

#ifdef CARBONLUAU_TESTING
extern int TestAllocationFailureAfter;
extern uint64_t TestLiveBytes;
#endif

struct DeadlineExceeded {};
struct CallbackScope {
    lua_State* State;
    int Reference = LUA_NOREF;
    ~CallbackScope() { if (Reference != LUA_NOREF) lua_unref(State, Reference); }
};

void* Allocate(void* Context, void* Pointer, size_t OldSize, size_t NewSize);
Vm* GetVm(ClHandle Id);
Vm* GetThread(ClHandle Id);
Domain* GetDomain(Vm& Runtime, ClHandle Id, bool Active = false);
void ReleaseDomain(Vm& Runtime, Domain& Value);
Domain* AddDomain(Vm& Runtime, uint32_t MaxQueued);
bool ControlPublication(Vm& Runtime, Domain& Owner, uint32_t Operation);
bool CanMutateHost(const Vm& Runtime);
void RollbackPublication(PublicationScope& Scope);
int FindStaged(Vm& Runtime, Domain* Owner, Module* Value);
void Interrupt(lua_State* State, int Gc);
void Append(Vm& Runtime, const char* Text, size_t Length);
int Print(lua_State* State);
int Initialize(lua_State* State);
void Retire(Vm& Runtime);
void Diagnostic(Vm& Runtime, ClResult& Result, const char* Message, lua_State* Trace = nullptr);
int CreateThread(lua_State* State);
int SandboxThread(lua_State* State);
int Collect(lua_State* State);
int StepCollect(lua_State* State);
void ReleaseThread(Vm& Runtime, bool Full = true);
uint64_t NowNs();
bool ModuleName(const char* Name, size_t Capacity);
Domain& BoundDomain(lua_State* State);
bool PackageName(const char* Name);
bool PackageVersion(const char* Version);
int RequireModule(lua_State* State);
int IsDependencyAvailable(lua_State* State);
int Schedule(lua_State* State);
void InstallDomainBindings(lua_State* State, Domain& Owner);
int InstallScripts(lua_State* State);
void PushFields(lua_State* State, const char* Bytes, size_t Length);
int HostPrimitive(lua_State* State);
int InstallFacade(lua_State* State);
int EnqueueEvent(lua_State* State);
} // namespace CarbonLuau::Runtime
