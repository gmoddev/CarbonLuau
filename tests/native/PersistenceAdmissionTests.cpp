// White-box dispatch seam only. No persistence names enter shipped Luau globals
// or native exports; the real conversion/completion facade belongs to 1B.
#include "../../native/src/runtime/RuntimeInternal.hpp"
#include "../../native/src/scripts/Compiler.hpp"
using namespace CarbonLuau::Runtime;
namespace {
Vm* TestRuntime=nullptr;
unsigned Dispatches=0;
int Dispatch(lua_State* State)
{
    auto* Owner=GetDomain(*TestRuntime,ClHandle(luaL_checkinteger(State,1)));
    bool Allowed=Owner && CanDispatchStorage(*TestRuntime,*Owner);
    if (Allowed) { Allowed=ReserveStorage(*TestRuntime,*Owner,uint64_t(Dispatches)+1); if (Allowed) ++Dispatches; }
    lua_pushboolean(State,Allowed); return 1;
}
void Check(bool Good,const char* Message) { if (!Good) { std::fprintf(stderr,"[CarbonLuau:Persistence] %s\n",Message); std::exit(1); } }
void Execute(ClHandle Vm,ClHandle Domain,const std::string& Source)
{
    ClHandle Thread=0; ClResult Result{};
    Check(cl_domain_load_source(Vm,Domain,"storage.admission",Source.data(),uint32_t(Source.size()),&Thread,&Result)==CL_OK,"load admission fixture");
    Check(cl_thread_resume(Thread,100000000,&Result)==CL_OK,"execute admission fixture");
    Check(cl_thread_destroy(Thread)==CL_OK,"destroy fixture thread");
}
}
int main(int Count,char** Args)
{
    Check(Count==2,"compiler worker argument"); SetCompilerExecutableForTesting(Args[1]);
    ClVmConfig Config{16*MiB}; ClHandle Vm=0,Provider=0,Consumer=0;
    Check(cl_vm_create(&Config,&Vm)==CL_OK && cl_vm_scripts(Vm,64)==CL_OK,"fixture VM");
    TestRuntime=GetVm(Vm);
    lua_setreadonly(TestRuntime->State,LUA_GLOBALSINDEX,0);
    lua_pushcfunction(TestRuntime->State,Dispatch,"storage-test-dispatch"); lua_setglobal(TestRuntime->State,"_storage_test");
    lua_setreadonly(TestRuntime->State,LUA_GLOBALSINDEX,1);
    Check(cl_domain_create(Vm,64,&Provider)==CL_OK && cl_domain_addon(Vm,Provider,"provider","1.0.0","")==CL_OK,"provider");
    Check(cl_domain_create(Vm,64,&Consumer)==CL_OK && cl_domain_addon(Vm,Consumer,"consumer","1.0.0","")==CL_OK,"consumer");
    const auto A=std::to_string(Provider), B=std::to_string(Consumer);
    const auto Public="assert(not _storage_test("+B+")); assert(not _storage_test("+A+")); return function() assert(_storage_test("+B+")); assert(not _storage_test("+A+")) end";
    Check(cl_domain_module(Vm,Provider,"api",Public.data(),uint32_t(Public.size()))==CL_OK &&
        cl_domain_public_module(Vm,Provider,"api")==CL_OK && cl_domain_commit(Vm,Provider)==CL_OK &&
        cl_domain_dependency(Vm,Consumer,"provider",Provider)==CL_OK,"public module binding");
    const auto Cold="assert(not _storage_test("+B+")); local Api=require('@provider/api'); return Api";
    Check(cl_domain_module(Vm,Consumer,"cold",Cold.data(),uint32_t(Cold.size()))==CL_OK,"cold local module");
    Execute(Vm,Consumer,"assert(not _storage_test("+B+")); task.defer(function() local Api=require('cold'); Api() end)");
    Check(Dispatches==0,"provisional dispatch zero");
    Check(cl_domain_commit(Vm,Consumer)==CL_OK,"consumer commit");
    ClSchedulerInfo Info{}; Check(cl_vm_scheduler(Vm,&Info)==CL_OK,"scheduler");
    uint32_t Ran=0; ClResult Result{};
    Check(cl_vm_callback(Vm,Info.NowNs,Info.Sequence,100000000,&Ran,&Result)==CL_OK && Ran && Dispatches==1,"cold local/public zero; cached export consumer authority");
    Execute(Vm,Consumer,"require('cold')(); assert(_storage_test("+B+"))");
    Check(Dispatches==3,"committed callback dispatch");
    Execute(Vm,Consumer,"for i=1,64 do task.defer(function() end) end");
    auto* Old=GetDomain(*TestRuntime,Consumer);
    AdmissionContext Fake{Old,1,false}; TestRuntime->Admission=&Fake;
    Check(CanDispatchStorage(*TestRuntime,*Old),"exact resource owner");
    Check(!CanDispatchStorage(*TestRuntime,*GetDomain(*TestRuntime,Provider)),"foreign resource rejected");
    for (uint64_t Id=4; Id<=8; ++Id) Check(ReserveStorage(*TestRuntime,*Old,Id),"separate intake survives ordinary task saturation");
    Check(!ReserveStorage(*TestRuntime,*Old,9) && TestRuntime->StorageReserved==8,"8/9 native domain reservation bound");
    Check(ReleaseStorage(*TestRuntime,*Old,8) && !ReleaseStorage(*TestRuntime,*Old,8) && ReserveStorage(*TestRuntime,*Old,9),"exact release; no duplicate release");
    TestRuntime->Admission=nullptr;
    Check(cl_domain_destroy(Vm,Consumer)==CL_OK && !CanDispatchStorage(*TestRuntime,*Old),"retired owner rejected");
    Check(TestRuntime->StorageReserved==0,"domain retirement releases private intake");
    for (unsigned Index=0; Index<17; ++Index) {
        ClHandle Domain=0; Check(cl_domain_create(Vm,64,&Domain)==CL_OK && cl_domain_commit(Vm,Domain)==CL_OK,"reservation fixture domain");
        auto* Owner=GetDomain(*TestRuntime,Domain); Fake.Owner=Owner; TestRuntime->Admission=&Fake;
        for (uint64_t Id=1; Id<=8; ++Id) Check(ReserveStorage(*TestRuntime,*Owner,Id)==(Index<16),"128/129 native global intake bound");
        TestRuntime->Admission=nullptr;
    }
    Check(TestRuntime->StorageReserved==128,"bounded global intake");
    Check(cl_vm_destroy(Vm)==CL_OK && !TestLiveBytes,"zero retained VM bytes"); ResetCompilerForTesting();
    std::puts("[CarbonLuau:Persistence] Native publication/domain dispatch seam PASS; no public persistence API");
}
