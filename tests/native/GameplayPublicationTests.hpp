#pragma once
#include "../../native/src/runtime/RuntimeInternal.hpp"

// Included only by the white-box allocation executable. No production export
// or alternate bootstrap implementation is needed to inspect its private roots.
namespace CarbonLuau::GameplayPublicationTests {
using namespace CarbonLuau::Runtime;
static uint64_t Registration;
static void Check(bool Good, const char* Message)
{
    if (!Good) { std::fprintf(stderr, "[CarbonLuau:GameplayPublicationNative] %s\n", Message); std::exit(1); }
}
static uint32_t Host(uint64_t, uint32_t Operation, const char*, uint32_t,
    char* Output, uint32_t Capacity, uint32_t* Written)
{
    *Written = 0;
    if (Operation == 6 || Operation == 8)
        *Written = uint32_t(std::snprintf(Output, Capacity, "%llu", (unsigned long long)++Registration) + 1);
    return 0;
}
static void Run(ClHandle VmId, ClHandle OwnerId, const char* Source)
{
    ClHandle Thread = 0; ClResult Result{};
    Check(cl_domain_load_source(VmId, OwnerId, "gameplay.publication.native", Source,
        uint32_t(std::strlen(Source)), &Thread, &Result) == CL_OK, "load actual bootstrap fixture");
    auto Status = cl_thread_resume(Thread, 100000000, &Result);
    if (Status != CL_OK) std::fprintf(stderr, "[CarbonLuau:GameplayPublicationNative] %s\n", Result.Error);
    Check(Status == CL_OK && cl_thread_destroy(Thread) == CL_OK, "execute real callback registration");
}
static size_t Roots(lua_State* State, int Holder)
{
    lua_getref(State, Holder); lua_getfield(State, -1, "Roots");
    size_t Count = 0; lua_pushnil(State);
    while (lua_next(State, -2)) { ++Count; lua_pop(State, 1); }
    lua_pop(State, 2); return Count;
}
static void FillCallbackEnvelope(lua_State* State, int Holder)
{
    // The production envelope is 256 Player listeners + 64 commands + 256
    // GUI listeners. A one-entry clone can reuse Luau page freelists without
    // reaching the injected backing allocator; this bounded hash clone cannot.
    lua_getref(State, Holder); lua_getfield(State, -1, "Roots");
    lua_pushnil(State); Check(lua_next(State, -2) != 0, "existing callback for bounded clone fixture");
    lua_remove(State, -2);
    for (unsigned Index = 2; Index <= 256 + 64 + 256; ++Index) {
        char Id[21]; std::snprintf(Id, sizeof(Id), "%u", Index);
        lua_pushstring(State, Id); lua_pushvalue(State, -2); lua_rawset(State, -4);
    }
    lua_pop(State, 3);
}
static void Create(ClHandle& VmId, ClHandle& OwnerId)
{
    Registration = 0; ClVmConfig Config{16 * MiB};
    Check(cl_vm_create(&Config, &VmId) == CL_OK && cl_domain_create(VmId, 64, &OwnerId) == CL_OK &&
        cl_domain_facade(VmId, OwnerId, Host) == CL_OK, "private facade installation");
    Run(VmId, OwnerId, "game:GetService('Players').PlayerAdded:Connect(function() end)");
    Check(cl_domain_commit(VmId, OwnerId) == CL_OK, "commit test domain");
}
inline void RunFaults()
{
    unsigned Failed = 0, Succeeded = 0;
    for (int Position = 0; Position < 64; ++Position) {
        TestAllocationFailureAfter = -1;
        ClHandle VmId = 0, OwnerId = 0; Create(VmId, OwnerId);
        Vm& Runtime = *GetVm(VmId); Domain& Owner = *GetDomain(Runtime, OwnerId);
        FillCallbackEnvelope(Runtime.State, Owner.FacadeCallbackRoots);
        Check(Roots(Runtime.State, Owner.FacadeCallbackRoots) == 576, "existing bounded callback envelope");
        TestAllocationFailureAfter = Position;
        bool Began = ControlPublication(Runtime, Owner, 10);
        TestAllocationFailureAfter = -1;
        if (Began) {
            ++Succeeded;
            Check(Owner.FacadeCallbackPublications.size() == 1, "one snapshot per begin");
            Check(ControlPublication(Runtime, Owner, Position % 2 ? 11 : 12), "commit/rollback snapshot cleanup");
            Check(Owner.FacadeCallbackPublications.empty() && Roots(Runtime.State, Owner.FacadeCallbackRoots) == 576,
                "snapshot reference is released and callback survives");
        } else { ++Failed; Check(Runtime.IntegrityFailed, "snapshot allocation failure fails closed"); }
        Check(cl_vm_destroy(VmId) == CL_OK && !TestLiveBytes, "partial snapshot teardown retains no allocator bytes");
    }
    Check(Failed != 0 && Succeeded != 0, "snapshot allocation boundary was exercised");
    {
        ClHandle VmId = 0, OwnerId = 0; Create(VmId, OwnerId);
        Vm& Runtime = *GetVm(VmId); Domain& Owner = *GetDomain(Runtime, OwnerId);
        Check(ControlPublication(Runtime, Owner, 10), "begin rollback snapshot");
        Run(VmId, OwnerId, "game:GetService('Players').PlayerAdded:Connect(function() end)");
        Check(Roots(Runtime.State, Owner.FacadeCallbackRoots) == 2, "live callback added after begin");
        Check(ControlPublication(Runtime, Owner, 12) && Roots(Runtime.State, Owner.FacadeCallbackRoots) == 1,
            "rollback releases newly registered Lua callback");
        Check(ControlPublication(Runtime, Owner, 10), "begin commit snapshot");
        Run(VmId, OwnerId, "game:GetService('Players').PlayerAdded:Connect(function() end)");
        Check(ControlPublication(Runtime, Owner, 11) && Roots(Runtime.State, Owner.FacadeCallbackRoots) == 2,
            "successful commit retains callbacks and releases snapshot");
        for (int Depth = 0; Depth < 32; ++Depth) Check(ControlPublication(Runtime, Owner, 10), "bounded nested snapshots");
        lua_getref(Runtime.State, Owner.FacadeCallbackRoots);
        int EscapedHolder = lua_ref(Runtime.State, -1); lua_pop(Runtime.State, 1);
        Check(cl_domain_destroy(VmId, OwnerId) == CL_OK && Owner.FacadeCallbackPublications.empty() &&
            Owner.FacadeCallbackRoots == LUA_NOREF && Roots(Runtime.State, EscapedHolder) == 0,
            "retirement clears callbacks even through an escaped private holder and releases all snapshots");
        lua_unref(Runtime.State, EscapedHolder);
        Check(cl_vm_destroy(VmId) == CL_OK && !TestLiveBytes, "nested/retired roots have no allocator leak");
    }
    std::printf("[CarbonLuau:GameplayPublicationNative] PASS snapshot allocation, commit, rollback and retirement\n");
}
}
