#pragma once
#include "../../native/src/runtime/RuntimeInternal.hpp"
#include <chrono>
#include <cstdio>
#include <unordered_set>

namespace CarbonLuau::CatalogTests {
namespace {
#include "../../native/src/runtime/generated/CatalogData.inc"
void Check(bool Value, const char* Message)
{
    if (!Value) { std::fprintf(stderr, "[CarbonLuau:CatalogTest] FAIL %s\n", Message); std::exit(1); }
}
void Execute(ClHandle Vm, ClHandle Domain, const std::string& Source)
{
    ClHandle Thread = 0; ClResult Result{};
    ClStatus Status = cl_domain_load_source(Vm, Domain, "catalog.test", Source.data(), uint32_t(Source.size()), &Thread, &Result);
    if (Status == CL_OK) Status = cl_thread_resume(Thread, 100000000, &Result);
    if (Status != CL_OK) std::fprintf(stderr, "[CarbonLuau:CatalogTest] %s\n", Result.Error);
    Check(Status == CL_OK && cl_thread_destroy(Thread) == CL_OK, "catalog execution");
}
}
inline void Run()
{
    using namespace CarbonLuau::Runtime;
    ClVmConfig Config{64 * MiB};
    ClHandle Rejected = 999;
    TestCatalogFingerprintMismatch = true;
    Check(cl_vm_create(&Config, &Rejected) == CL_INTERNAL_ERROR && !Rejected && !TestLiveBytes,
        "fingerprint mismatch rejects protected setup without partial namespace or leaked refs");
    TestCatalogFingerprintMismatch = false;
    for (int Generation = 0; Generation < 3; ++Generation) {
        auto Start = std::chrono::steady_clock::now();
        ClHandle Handle = 0;
        Check(cl_vm_create(&Config, &Handle) == CL_OK, "protected catalog generation");
        Vm* Runtime = GetVm(Handle);
        Check(Runtime && Runtime->ItemCatalog != LUA_NOREF && Runtime->PrefabCatalog != LUA_NOREF, "complete shared refs");
        lua_getref(Runtime->State, Runtime->ItemCatalog);
        const void* Identity = lua_topointer(Runtime->State, -1);
        Check(lua_getreadonly(Runtime->State, -1), "native Item readonly"); lua_pop(Runtime->State, 1);
        std::printf("[CarbonLuau:CatalogTest] Generation=%d CatalogBytes=%llu InitBytes=%llu StartupMs=%.3f\n", Generation,
            static_cast<unsigned long long>(Runtime->CatalogBytes), static_cast<unsigned long long>(Runtime->Used),
            std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now() - Start).count());
        std::string Assertions = "assert(type(Item)=='table' and type(Prefab)=='table' and type(Prefab.Loot)=='table'); local N=0; for K,V in Item do assert(type(K)=='string' and type(V)=='string'); N+=1 end; assert(N==221); ";
        for (const auto& Row : CatalogItems)
            Assertions += "assert(Item." + std::string(Row[0]) + "=='" + Row[1] + "');";
        for (const auto& Row : CatalogPrefabs)
            Assertions += "assert(Prefab.Loot." + std::string(Row[0]) + "=='" + Row[1] + "');";
        Assertions += "local P=0; for K in Prefab do assert(K=='Loot'); P+=1 end; assert(P==1); local L=0; for K,V in Prefab.Loot do assert(type(V)=='string' and (K=='Barrel1' or K=='Barrel2')); L+=1 end; assert(L==2); ";
        Assertions += "assert(Item.scrap==nil and Item.Get==nil and Item.Revolver==nil and Prefab.Barrel1==nil and Prefab.Entity==nil); assert(tostring(Item.Scrap)=='scrap'); assert(ImageSource==nil); ";
        const std::string Mutations = "for _,F in {function() Item.Scrap='bad' end,function() rawset(Item,'Scrap','bad') end,function() Prefab.Loot.Barrel1='bad' end,function() rawset(Prefab.Loot,'Barrel1','bad') end,function() Prefab.Loot={} end,function() rawset(Prefab,'Loot',{}) end,function() setmetatable(Item,{}) end} do assert(not pcall(F)) end; ";
        ClHandle Root = 0;
        Check(cl_domain_create(Handle, 64, &Root) == CL_OK, "root domain");
        const char* Module = "return {Item=Item,Prefab=Prefab,Value=Item.Scrap}";
        Check(cl_domain_module(Handle, Root, "catalog", Module, uint32_t(std::strlen(Module))) == CL_OK, "module source");
        Execute(Handle, Root, Assertions + Mutations + "assert(require('catalog').Item==Item and require('catalog').Prefab==Prefab); Item={Scrap='shadow'}; Prefab={Loot={}}; assert(Item.Scrap=='shadow')");
        Execute(Handle, Root, Assertions + "assert(require('catalog').Value=='scrap')");
        Check(cl_domain_commit(Handle, Root) == CL_OK, "publish root");
        std::vector<ClHandle> Addons;
        for (int Index = 1; Index <= 100; ++Index) {
            ClHandle Domain = 0;
            Check(cl_domain_create(Handle, 64, &Domain) == CL_OK, "addon create");
            std::string Id = "catalog" + std::to_string(Index);
            Check(cl_domain_addon(Handle, Domain, Id.c_str(), "1.0.0", "") == CL_OK, "addon identity");
            Execute(Handle, Domain, Mutations + "assert(Item.Scrap=='scrap' and Prefab.Loot.Barrel2~=nil); task.defer(function() assert(Item.Wood=='wood'); print(addon.Id) end)");
            Check(cl_domain_commit(Handle, Domain) == CL_OK, "addon publication");
            Addons.push_back(Domain);
            lua_getref(Runtime->State, Runtime->ItemCatalog);
            Check(lua_topointer(Runtime->State, -1) == Identity, "single-copy catalog identity"); lua_pop(Runtime->State, 1);
            if (Index == 1 || Index == 10 || Index == 50 || Index == 100)
                std::printf("[CarbonLuau:CatalogTest] Generation=%d Addons=%d VmBytes=%llu CatalogBytes=%llu\n", Generation, Index,
                    static_cast<unsigned long long>(Runtime->Used), static_cast<unsigned long long>(Runtime->CatalogBytes));
        }
        ClSchedulerInfo Cutoff{};
        Check(cl_vm_scheduler(Handle, &Cutoff) == CL_OK && Cutoff.Queued == 100, "100 addon callbacks admitted");
        std::unordered_set<std::string> Delivered;
        for (int Index = 0; Index < 100; ++Index) {
            uint32_t Ran = 0; ClResult Result{};
            Check(cl_vm_callback(Handle, Cutoff.NowNs, Cutoff.Sequence, 100000000, &Ran, &Result) == CL_OK && Ran == 1,
                "fair shared catalog callback progress");
            Check(Delivered.insert(Result.Logs).second, "every addon callback advances exactly once");
        }
        Check(Delivered.size() == 100 && cl_vm_scheduler(Handle, &Cutoff) == CL_OK && !Cutoff.Queued, "fairness converges");
        for (int Cycle = 0; Cycle < 10; ++Cycle) {
            Check(cl_domain_destroy(Handle, Root) == CL_OK && cl_domain_create(Handle, 64, &Root) == CL_OK, "healthy root replacement");
            Execute(Handle, Root, "assert(Item.Scrap=='scrap' and Prefab.Loot.Barrel1~=nil)");
            Check(cl_domain_commit(Handle, Root) == CL_OK, "replacement publication");
        }
        for (auto Domain : Addons) Check(cl_domain_destroy(Handle, Domain) == CL_OK, "addon unload");
        Check(cl_domain_destroy(Handle, Root) == CL_OK, "root unload");
        lua_getref(Runtime->State, Runtime->ItemCatalog);
        Check(lua_topointer(Runtime->State, -1) == Identity, "catalog survives domain retirement"); lua_pop(Runtime->State, 1);
        Retire(*Runtime);
        Check(!Runtime->State && Runtime->ItemCatalog == LUA_NOREF && Runtime->PrefabCatalog == LUA_NOREF && !TestLiveBytes,
            "fatal retirement releases catalogs and every Lua allocation");
        Check(cl_vm_destroy(Handle) == CL_OK && !TestLiveBytes, "generation teardown baseline");
    }
    bool Complete = false; int Failed = 0;
    for (int Position = 0; Position < 2048; ++Position) {
        TestAllocationFailureAfter = Position;
        ClHandle Handle = 999;
        ClStatus Status = cl_vm_create(&Config, &Handle);
        TestAllocationFailureAfter = -1;
        if (Status == CL_OK) {
            Check(cl_vm_destroy(Handle) == CL_OK, "allocation successful construction cleanup"); Complete = true;
        } else { Check(Status == CL_MEMORY_LIMIT && !Handle, "allocation failure never publishes partial VM"); ++Failed; }
        Check(!TestLiveBytes, "catalog allocation fault returns zero bytes");
        if (Complete) break;
    }
    // Luau's small-object pages serve many strings per allocator call. Sweep
    // every allocator prefix through the FIRST successful complete setup;
    // a row count is not an allocator-call count.
    Check(Complete && Failed > 0, "fault sweep reaches every allocator prefix and successful complete setup");
    std::printf("[CarbonLuau:CatalogTest] PASS 221 exact item/two prefab strings; shared frozen namespaces, modules, shadowing, 100 addons, replacement, three generations; %d allocation faults and zero teardown bytes\n", Failed);
}
}
