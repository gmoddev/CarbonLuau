#include "carbonluau_native.h"
#include <cstring>
#include <cstdio>
#include <stdexcept>
#include <string>
#include <chrono>

static void Check(bool Value, const char* Message) { if (!Value) throw std::runtime_error(Message); }
static ClHandle Create(uint32_t Capacity = 4096) {
    ClVmConfig Config{64 * 1024 * 1024}; ClHandle Vm = 0;
    Check(cl_vm_create(&Config, &Vm) == CL_OK, "create");
    Check(cl_vm_scripts(Vm, Capacity) == CL_OK, "scripts install"); return Vm;
}
static void Module(ClHandle Vm, const char* Name, const char* Source) {
    Check(cl_vm_module(Vm, Name, Source, uint32_t(std::strlen(Source))) == CL_OK, "add module");
}
static ClStatus Execute(ClHandle Vm, const char* Source, ClResult& Result) {
    ClHandle Thread = 0;
    ClStatus Status = cl_vm_load_source(Vm, "entry", Source, uint32_t(std::strlen(Source)), &Thread, &Result);
    if (Status == CL_OK) Status = cl_thread_resume(Thread, 100000000, &Result);
    if (Thread) Check(cl_thread_destroy(Thread) == ((Result.Flags & 1) ? CL_INVALID_ARGUMENT : CL_OK), "thread cleanup");
    return Status;
}
static ClSchedulerInfo Info(ClHandle Vm) { ClSchedulerInfo Value{}; Check(cl_vm_scheduler(Vm, &Value) == CL_OK, "scheduler info"); return Value; }
static ClStatus Run(ClHandle Vm, ClSchedulerInfo Cutoff, ClResult& Result, bool Expected = true) {
    uint32_t Ran = 99; ClStatus Status = cl_vm_callback(Vm, Cutoff.NowNs, Cutoff.Sequence, 3000000, &Ran, &Result);
    Check(Ran == uint32_t(Expected), "attempt count"); return Status;
}
static ClHandle Domain(ClHandle Vm, uint32_t Capacity = 4096) {
    ClHandle Value = 0; Check(cl_domain_create(Vm, Capacity, &Value) == CL_OK && Value != 0, "domain create"); return Value;
}
static void DomainModule(ClHandle Vm, ClHandle DomainId, const char* Name, const char* Source) {
    Check(cl_domain_module(Vm, DomainId, Name, Source, uint32_t(std::strlen(Source))) == CL_OK, "domain module");
}
static ClStatus DomainExecute(ClHandle Vm, ClHandle DomainId, const char* Source, ClResult& Result) {
    ClHandle Thread = 0;
    ClStatus Status = cl_domain_load_source(Vm, DomainId, "domain-entry", Source, uint32_t(std::strlen(Source)), &Thread, &Result);
    if (Status == CL_OK) Status = cl_thread_resume(Thread, 100000000, &Result);
    if (Thread) Check(cl_thread_destroy(Thread) == ((Result.Flags & 1) ? CL_INVALID_ARGUMENT : CL_OK), "domain thread cleanup");
    return Status;
}
static ClHandle ReentryVm = 0, ReentryOwner = 0, ReentryDomain = 0, ReentryThread = 0;
static bool ReentryRejected = false;
static uint32_t ReentryHost(uint64_t, uint32_t Operation, const char*, uint32_t, char*, uint32_t, uint32_t* Written) {
    *Written = 0;
    if (Operation != 1) return 0;
    ClHandle DomainId = 0, ThreadId = 0; ClResult Result{}; uint32_t Ran = 0;
    const char Event[] = {'p', 0};
    const char* ModuleSource = "return 1";
    ReentryRejected =
        cl_vm_destroy(ReentryVm) == CL_INVALID_ARGUMENT &&
        cl_thread_resume(ReentryThread, 3000000, &Result) == CL_INVALID_ARGUMENT &&
        cl_thread_destroy(ReentryThread) == CL_INVALID_ARGUMENT &&
        cl_domain_create(ReentryVm, 1, &DomainId) == CL_INVALID_ARGUMENT && DomainId == 0 &&
        cl_domain_module(ReentryVm, ReentryDomain, "late", ModuleSource, uint32_t(std::strlen(ModuleSource))) == CL_INVALID_ARGUMENT &&
        cl_domain_commit(ReentryVm, ReentryDomain) == CL_INVALID_ARGUMENT &&
        cl_domain_destroy(ReentryVm, ReentryDomain) == CL_INVALID_ARGUMENT &&
        cl_domain_facade(ReentryVm, ReentryDomain, ReentryHost) == CL_INVALID_ARGUMENT &&
        cl_domain_load_source(ReentryVm, ReentryDomain, "nested", "return 1", 8, &ThreadId, &Result) == CL_INVALID_ARGUMENT && ThreadId == 0 &&
        cl_domain_event(ReentryVm, ReentryOwner, Event, sizeof(Event)) == CL_INVALID_ARGUMENT &&
        cl_vm_callback(ReentryVm, UINT64_MAX, UINT64_MAX, 3000000, &Ran, &Result) == CL_INVALID_ARGUMENT && Ran == 0;
    return 0;
}
int main() try {
    ClResult Result{};
    ClVmConfig BareConfig{16*1024*1024}; ClHandle Bare=0;
    Check(cl_vm_create(&BareConfig,&Bare)==CL_OK,"bare ABI fixture");
    Check(cl_vm_scripts(Bare,0)==CL_INVALID_ARGUMENT && cl_vm_scripts(Bare,4097)==CL_INVALID_ARGUMENT,"queue config bounds");
    Check(cl_vm_scheduler(Bare,nullptr)==CL_INVALID_ARGUMENT,"null scheduler output");
    Check(cl_vm_destroy(Bare)==CL_OK && cl_vm_scripts(Bare,1)==CL_INVALID_ARGUMENT,"stale VM script install");

    ClVmConfig DomainConfig{64*1024*1024}; ClHandle Shared=0;
    Check(cl_vm_create(&DomainConfig,&Shared)==CL_OK,"shared VM create");
    ClVmGenerationInfo Identity{}; Check(cl_vm_generation(Shared,&Identity)==CL_OK && Identity.VmGenerationId!=0 && Identity.Domains==0,"VM generation identity");
    ClHandle First=Domain(Shared), Second=Domain(Shared);
    DomainModule(Shared,First,"value","task.defer(function() print('first-domain') end); return {Value=1}");
    DomainModule(Shared,Second,"value","task.defer(function() print('second-domain') end); return {Value=2}");
    Check(DomainExecute(Shared,First,"local V=require('value'); assert(V.Value==1 and V==require('value'))",Result)==CL_OK,"first provisional domain");
    Check(Info(Shared).Modules==0 && Info(Shared).Queued==0,"provisional cache/resources hidden");
    Check(cl_domain_commit(Shared,First)==CL_OK && Info(Shared).Modules==1 && Info(Shared).Queued==1,"first domain commit publishes atomically");
    Check(DomainExecute(Shared,Second,"assert(require('value').Value==2)",Result)==CL_OK,"same logical path isolated by domain");
    Check(cl_domain_commit(Shared,Second)==CL_OK && Info(Shared).Modules==2 && Info(Shared).Queued==2,"second domain shares VM with distinct cache");
    Check(cl_vm_generation(Shared,&Identity)==CL_OK && Identity.Domains==2,"multiple live domains reported");
    Check(cl_domain_destroy(Shared,First)==CL_OK,"domain retirement");
    Check(DomainExecute(Shared,First,"return 1",Result)==CL_INVALID_ARGUMENT,"stale domain rejected");
    Check(Info(Shared).Modules==1 && Info(Shared).Queued==1,"domain-owned cache and queue retired independently");
    Check(Run(Shared,Info(Shared),Result)==CL_OK && std::string(Result.Logs)=="second-domain\n","surviving domain callback");
    Check(cl_domain_destroy(Shared,Second)==CL_OK && cl_vm_destroy(Shared)==CL_OK,"shared VM/domain teardown");

    Check(cl_vm_create(&DomainConfig,&ReentryVm)==CL_OK,"reentry VM create");
    ReentryOwner=Domain(ReentryVm); ReentryDomain=Domain(ReentryVm);
    Check(cl_domain_facade(ReentryVm,ReentryOwner,ReentryHost)==CL_OK,"reentry facade install");
    Check(cl_domain_commit(ReentryVm,ReentryOwner)==CL_OK,"reentry owner commit");
    const char* ReentrySource="assert(#game:GetService('Players'):GetPlayers()==0)";
    Check(cl_domain_load_source(ReentryVm,ReentryOwner,"reentry-entry",ReentrySource,uint32_t(std::strlen(ReentrySource)),&ReentryThread,&Result)==CL_OK,"reentry source load");
    Check(cl_thread_resume(ReentryThread,100000000,&Result)==CL_OK && ReentryRejected,"all host-driven recursive VM entry rejected");
    Check(cl_thread_destroy(ReentryThread)==CL_OK,"reentry thread cleanup"); ReentryThread=0;
    Check(cl_domain_destroy(ReentryVm,ReentryDomain)==CL_OK && cl_domain_destroy(ReentryVm,ReentryOwner)==CL_OK && cl_vm_destroy(ReentryVm)==CL_OK,"reentry fixture cleanup");
    ReentryVm=ReentryOwner=ReentryDomain=0;

    ClHandle Vm = Create();
    Module(Vm, "counter", "print('once'); return {Value=42}");
    Module(Vm, "util/adder", "return function(A,B) return A+B end");
    Module(Vm, "nilvalue", "return nil");
    Module(Vm, "novalue", "return");
    Module(Vm, "primitive", "return false");
    Module(Vm, "private", "assert(EntrySecret==nil); return 7");
    Module(Vm, "badcompile", "local =");
    Module(Vm, "badruntime", "error('module boom')");
    Module(Vm, "yielding", "coroutine.yield()");
    Module(Vm,"caughtfailure","task.defer(function() error('leaked module resource') end); error('caught module failure')");
    Module(Vm, "a", "return require('b')"); Module(Vm, "b", "return require('c')"); Module(Vm, "c", "return require('a')");
    Check(Execute(Vm,
        "EntrySecret=123; assert(require('private')==7); local A=require('counter'); assert(A==require('counter'));"
        "assert(require('util/adder')(2,3)==5); assert(require('nilvalue')==true and require('novalue')==true);"
        "assert(require('primitive')==false); assert(os==nil and io==nil and debug==nil and game==nil and loadstring==nil);"
        "assert(getfenv==nil and setfenv==nil and package==nil and load==nil and dofile==nil and collectgarbage==nil and newproxy==nil);"
        "assert(not pcall(function() task.spawn=1 end)); assert(not pcall(function() table.insert=1 end)); return 3", Result) == CL_OK, Result.Error);
    Check(std::string(Result.Logs) == "once\n", "module executes once");
    Check(Info(Vm).Modules == 6, "successful cache count");
    Check(cl_vm_module(Vm,"late","return 1",8)==CL_INVALID_ARGUMENT,"sealed snapshot cannot mutate");
    Check(cl_vm_scripts(Vm,1)==CL_INVALID_ARGUMENT,"cannot reinstall capabilities");
    auto CachedStart = std::chrono::steady_clock::now();
    Check(Execute(Vm, "for I=1,1000 do assert(require('counter').Value==42) end", Result) == CL_OK, "cached require benchmark");
    std::printf("[CarbonLuau:ScriptTest] 1000 cached requires including chunk compile/setup: %.3f ms\n", std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-CachedStart).count());
    for (const char* Name : {"../foo", "a/../foo", "a//foo", "a\\..\\foo", "/foo", "C:/foo", "//server/share", "", "Foo", "foo.luau", "a/", "a/./b"}) {
        std::string Source = "return require([=[" + std::string(Name) + "]=])";
        Check(Execute(Vm, Source.c_str(), Result) == CL_RUNTIME_ERROR, "invalid module rejected");
    }
    Check(Execute(Vm, "require('missing')", Result) == CL_RUNTIME_ERROR && std::strstr(Result.Error, "NOT_FOUND"), "missing attribution");
    Check(Execute(Vm, "require('badcompile')", Result) == CL_RUNTIME_ERROR && std::strstr(Result.Error, "COMPILE_ERROR"), "compile attribution");
    Check(Execute(Vm, "require('badruntime')", Result) == CL_RUNTIME_ERROR && std::strstr(Result.Error, "module boom"), "runtime attribution");
    Check(Execute(Vm, "require('yielding')", Result) == CL_RUNTIME_ERROR && std::strstr(Result.Error, "must not yield"), "module yield rejected");
    for (int Retry = 0; Retry < 3; ++Retry) Check(Execute(Vm, "require('a')", Result) == CL_RUNTIME_ERROR && std::strstr(Result.Error, "a -> b -> c -> a"), "cycle retry");
    Check(Info(Vm).Modules == 6, "failed modules not cached");
    Check(Execute(Vm,"local Ok=pcall(require,'caughtfailure'); assert(not Ok); task.defer(function() print('parent survives') end)",Result)==CL_OK,"caught failed module operation continues");
    Check(Info(Vm).Queued==1 && Info(Vm).Modules==6,"caught failed module publishes no cache or resource");
    Check(Run(Vm,Info(Vm),Result)==CL_OK && std::string(Result.Logs)=="parent survives\n","only surrounding operation resource publishes");
    Check(Execute(Vm,"assert(not pcall(require,'caughtfailure'))",Result)==CL_OK && Info(Vm).Modules==6,"failed module retry remains uncached");
    Check(Execute(Vm, "task.defer(function(A,B,C,D) assert(A==nil and B==true and C==2 and D=='hi'); print('first'); task.spawn(function() print('later') end) end,nil,true,2,'hi'); task.spawn(function() print('second') end)", Result) == CL_OK, "enqueue");
    auto Cutoff = Info(Vm);
    Check(Run(Vm, Cutoff, Result) == CL_OK && std::string(Result.Logs) == "first\n", "FIFO first/arguments");
    Check(Run(Vm, Cutoff, Result) == CL_OK && std::string(Result.Logs) == "second\n", "FIFO second");
    Check(Run(Vm, Cutoff, Result, false) == CL_OK, "recursive work excluded from drain");
    Check(Run(Vm, Info(Vm), Result) == CL_OK && std::string(Result.Logs) == "later\n", "next drain");
    Check(Execute(Vm, "task.delay(10,function() print('ten') end); task.delay(5,function() print('five') end)", Result) == CL_OK, "delays");
    Cutoff = Info(Vm); Check(Run(Vm, Cutoff, Result, false) == CL_OK, "not before");
    Cutoff.NowNs += 6000000000ULL; Check(Run(Vm, Cutoff, Result) == CL_OK && std::string(Result.Logs) == "five\n", "delayed order first");
    Check(Run(Vm, Cutoff, Result, false) == CL_OK, "remaining future");
    Cutoff.NowNs += 5000000000ULL; Check(Run(Vm, Cutoff, Result) == CL_OK && std::string(Result.Logs) == "ten\n", "delayed order second");
    Check(Execute(Vm, "for _,V in {0/0,math.huge,-1,86401} do assert(not pcall(task.delay,V,function() end)) end; assert(not pcall(task.defer,function() end,{})); assert(not pcall(task.defer,function() end,string.rep('x',4097)))", Result) == CL_OK, "invalid task arguments");
    Check(Execute(Vm, "task.defer(function() error('boom') end); task.defer(function() print('survived') end)", Result) == CL_OK, "error queue");
    Cutoff = Info(Vm); Check(Run(Vm, Cutoff, Result) == CL_RUNTIME_ERROR, "callback error isolated");
    Check(Run(Vm, Cutoff, Result) == CL_OK && std::string(Result.Logs) == "survived\n", "unrelated survives");
    Check(Execute(Vm, "task.defer(function() buffer.create(67108865) end); task.defer(function() print('memory survived') end)", Result) == CL_OK, "memory queue");
    Cutoff = Info(Vm); Check(Run(Vm, Cutoff, Result) == CL_MEMORY_LIMIT, "callback memory isolated");
    Check(Run(Vm, Cutoff, Result) == CL_OK && std::string(Result.Logs) == "memory survived\n", "memory recovery");
    Check(Execute(Vm,"task.defer(function() coroutine.yield() end)",Result)==CL_OK,"yield queue");
    Check(Run(Vm,Info(Vm),Result)==CL_RUNTIME_ERROR && std::strstr(Result.Error,"cannot yield"),"callback yield released");
    Check(Execute(Vm, "task.defer(function() while true do end end); task.defer(function() error('stale') end)", Result) == CL_OK, "timeout enqueue");
    Cutoff = Info(Vm); Check(Run(Vm, Cutoff, Result) == CL_TIMEOUT && (Result.Flags & 1), "timeout retires");
    uint32_t Ran = 1; Check(cl_vm_callback(Vm, UINT64_MAX, UINT64_MAX, 3000000, &Ran, &Result) == CL_INVALID_ARGUMENT && Ran == 0, "retired callback rejected");
    Check(cl_vm_destroy(Vm) == CL_OK, "destroy retired");

    Vm = Create(); Module(Vm, "hang", "task.defer(function() end); while true do end");
    Check(Execute(Vm, "pcall(require,'hang')", Result) == CL_TIMEOUT && (Result.Flags & 1), "module timeout bypasses catches and retires");
    Check(Info(Vm).Discarded == 1 && Info(Vm).Queued == 0, "module-created callback retired accounting");
    Check(cl_vm_destroy(Vm) == CL_OK, "module timeout cleanup");

    Vm = Create();
    auto EnqueueStart = std::chrono::steady_clock::now();
    int Accepted = 0;
    // Ten bounded resumes also keep sanitizer instrumentation within the same
    // production 100ms maximum; the queue is never drained between batches.
    for (int Batch = 0; Batch < 10; ++Batch) {
        Check(Execute(Vm, "local Accepted=0; for I=1,1000 do if pcall(task.defer,function() end) then Accepted+=1 end end; return Accepted", Result) == CL_OK, Result.Error);
        Accepted += int(Result.Number);
    }
    Check(Accepted == 4096, "10000 enqueue attempts accept exactly capacity");
    Check(Info(Vm).Queued == 4096 && Info(Vm).Rejected == 5904, "hard saturation");
    std::printf("[CarbonLuau:ScriptTest] 10000 scheduling attempts / 4096 accepted, including batch compile/setup: %.3f ms\n", std::chrono::duration<double,std::milli>(std::chrono::steady_clock::now()-EnqueueStart).count());
    auto Start = std::chrono::steady_clock::now(); Cutoff = Info(Vm);
    for (int I = 0; I < 4096; ++I) Check(Run(Vm, Cutoff, Result) == CL_OK, "saturated drain");
    std::printf("[CarbonLuau:ScriptTest] 4096 trivial callbacks drained in %.3f ms\n", std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now()-Start).count());
    for(int Batch=0;Batch<4;++Batch)
        Check(Execute(Vm,"for I=1,1000 do task.delay((I%64)/1000,function(N) assert(N>0) end,I) end",Result)==CL_OK,"scaled varied delays");
    Check(Execute(Vm,"for I=1,96 do task.delay((I%64)/1000,function(N) assert(N>0) end,I) end",Result)==CL_OK,"scaled delay remainder");
    Cutoff=Info(Vm); Check(Cutoff.Queued==4096,"4096 delayed tasks bounded"); Cutoff.NowNs+=1000000000;
    for(int I=0;I<4096;++I) Check(Run(Vm,Cutoff,Result)==CL_OK,"scaled due tasks all progress");
    Check(cl_vm_destroy(Vm) == CL_OK, "queue destroy");
    for (int Cycle = 0; Cycle < 1000; ++Cycle) {
        Vm = Create(); Module(Vm, "value", "return {Value=1}");
        Check(Execute(Vm, "local V=require('value'); for I=1,10 do task.delay(I,function(N) assert(V.Value==1 and N>0) end,I) end", Result) == CL_OK, "cycle initialize");
        if (Cycle % 2 == 0) { Cutoff = Info(Vm); Cutoff.NowNs += 11000000000ULL; for (int I=0; I<10; ++I) Check(Run(Vm,Cutoff,Result)==CL_OK,"cycle drain"); }
        Check(cl_vm_destroy(Vm) == CL_OK, "cycle cancel/destroy");
    }
    std::puts("[CarbonLuau:ScriptTest] PASS: modules, sandbox, errors, cycles, tasks, saturation, timeout; 1000 generation lifecycles");
    return 0;
} catch (const std::exception& Error) { std::fprintf(stderr, "[CarbonLuau:ScriptTest] FAIL: %s\n", Error.what()); return 1; }
