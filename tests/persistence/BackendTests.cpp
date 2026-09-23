#include "Backend.hpp"
#include "sqlite3.h"
#include <cstdio>
#include <fstream>
using namespace CarbonLuau::Persistence;
namespace {
Deadline End() { return Clock::now()+std::chrono::seconds(30); }
Value Text(size_t Length) { Value Item; Item.Type=Kind::String; Item.String.assign(Length,'x'); return Item; }
void Run()
{
    auto Root=std::filesystem::current_path()/("backend-"+std::to_string(Clock::now().time_since_epoch().count()));
    Require(std::filesystem::create_directory(Root));
    Identity Id{false,"","Store","Key"}; Bytes Envelope=Encode(Id,Text(100),End());
    {
        auto Started=Clock::now(); Backend Store(Root,End()); Require(Store.Available());
        std::printf("[CarbonLuau:Persistence] Fresh startup %.3fms\n",std::chrono::duration<double,std::milli>(Clock::now()-Started).count());
        auto Result=Store.Execute(Operation::Get,Id,{},End()); Require(Result.Code==Error::None && !Result.Found);
        Result=Store.Execute(Operation::Remove,Id,{},End()); Require(Result.Code==Error::None && !Result.Found);
        Result=Store.Execute(Operation::Set,Id,Envelope,End()); Require(Result.Code==Error::None && Result.Found);
        auto Bad=Envelope; Bad.back()^=1;
        Require(Store.Execute(Operation::Set,Id,Bad,End()).Code==Error::InvalidArgument && Store.Available());
        Result=Store.Execute(Operation::Get,Id,{},End()); Require(Result.Code==Error::None && Result.Envelope==Envelope);
        auto Other=Id; Other.Key="key"; Require(!Store.Execute(Operation::Get,Other,{},End()).Found);
        Other=Id; Other.Store="store"; Require(!Store.Execute(Operation::Get,Other,{},End()).Found);
        Other=Id; Other.Addon=true; Other.Package="economy"; Require(!Store.Execute(Operation::Get,Other,{},End()).Found);
        for (size_t Size : {1u,200u,200u,100u}) Require(Store.Execute(Operation::Set,Id,Encode(Id,Text(Size),End()),End()).Code==Error::None);
        for (unsigned I=0; I<63; ++I) { auto Next=Id; Next.Store="S"+std::to_string(I); Require(Store.Execute(Operation::Set,Next,Encode(Next,Text(10),End()),End()).Code==Error::None); }
        auto Excess=Id; Excess.Store="Excess";
        Require(Store.Execute(Operation::Set,Excess,Encode(Excess,Text(10),End()),End()).Code==Error::QuotaExceeded);
        Require(Store.Execute(Operation::Get,Id,{},Clock::now()).Code==Error::DeadlineExceeded);
        Started=Clock::now();
        for (unsigned Index=0; Index<1000; ++Index) {
            auto Read=Store.Execute(Operation::Get,Id,{},End()); Require(Read.Code==Error::None && Read.Envelope==Envelope);
        }
        std::printf("[CarbonLuau:Persistence] 1000 repeated keyed reads PASS total=%.3fms\n",std::chrono::duration<double,std::milli>(Clock::now()-Started).count());
    }
    {
        Backend Store(Root,End()); auto Result=Store.Execute(Operation::Get,Id,{},End()); Require(Result.Code==Error::None && Result.Envelope==Envelope);
        Require(Store.Execute(Operation::Remove,Id,{},End()).Code==Error::None);
        Require(!Store.Execute(Operation::Remove,Id,{},End()).Found);
    }
    Require(std::filesystem::exists(Root/"store.sqlite3-journal"));
    // An offline accidental quota inconsistency must disable startup, not reset it.
    sqlite3* Db=nullptr; Require(sqlite3_open((Root/"store.sqlite3").string().c_str(),&Db)==SQLITE_OK);
    Require(sqlite3_exec(Db,"PRAGMA journal_mode=PERSIST; UPDATE Totals SET Bytes=Bytes+1",nullptr,nullptr,nullptr)==SQLITE_OK);
    sqlite3_close(Db);
    bool Rejected=false; try { Backend Store(Root,End()); } catch (const Failure& Error) { Rejected=Error.Code==Error::StorageCorrupt; }
    Require(Rejected && std::filesystem::exists(Root/"store.sqlite3"));
    // An observed file-budget violation is terminal, unlike rolled-back SQLITE_FULL.
    auto Budget=Root/"budget"; Require(std::filesystem::create_directory(Budget));
    {
        Backend Store(Budget,End());
        Require(Store.Execute(Operation::Set,Id,Envelope,End()).Code==Error::None);
        { std::ofstream Lock(Budget/"owner.lock",std::ios::binary); Lock.put(0); }
        std::filesystem::resize_file(Budget/"owner.lock",4097);
        auto Result=Store.Execute(Operation::Set,Id,Encode(Id,Text(200),End()),End());
        Require(Result.Code==Error::StorageUnavailable && !Store.Available());
        Require(std::filesystem::file_size(Budget/"owner.lock")==4097);
        std::filesystem::resize_file(Budget/"owner.lock",0); // offline fixture repair, not production behavior
        Require(Store.Execute(Operation::Get,Id,{},End()).Code==Error::StorageUnavailable);
    }
    { Backend Store(Budget,End()); Require(Store.Execute(Operation::Get,Id,{},End()).Envelope==Envelope); }
    std::puts("[CarbonLuau:Persistence] Observed file-budget violation disables admission and preserves value PASS");
    std::printf("[CarbonLuau:Persistence] Backend tests PASS; disposable fixtures retained\n");
}
}
int main()
{
    try { Run(); return 0; }
    catch (const Failure& Problem) { std::fprintf(stderr,"[CarbonLuau:Persistence] Backend failure code %u\n",unsigned(Problem.Code)); return 1; }
    catch (const std::exception& Problem) { std::fprintf(stderr,"[CarbonLuau:Persistence] Backend test failed: %s\n",Problem.what()); return 1; }
}
