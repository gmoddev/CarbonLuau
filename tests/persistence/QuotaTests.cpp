#include "Backend.hpp"
#include "sqlite3.h"
#include <cstdio>
#include <iomanip>
#include <sstream>
using namespace CarbonLuau::Persistence;
namespace {
Deadline End() { return Clock::now()+std::chrono::seconds(5); }
std::string Key(unsigned Index) { std::ostringstream Text; Text<<std::setw(4)<<std::setfill('0')<<Index; return Text.str(); }
Identity Id(unsigned Namespace,unsigned Index) { return {Namespace!=0,Namespace ? "addon"+std::to_string(Namespace) : "","S",Key(Index)}; }
Bytes Sized(const Identity& Id,size_t Length)
{
    Require(Length>=69 && Length<=65536);
    Value Item; Item.Type=Kind::Array; size_t Remaining=Length-69;
    for (unsigned I=0; I<4; ++I) {
        auto Child=std::make_shared<Value>(); Child->Type=Kind::String;
        size_t Size=std::min<size_t>(16384,Remaining); Child->String.assign(Size,'x'); Remaining-=Size; Item.Array.push_back(Child);
    }
    Require(Remaining==0); auto Value=Encode(Id,Item,End()); Require(Value.size()==Length); return Value;
}
void Store(Backend& Db,unsigned Namespace,unsigned Index,size_t Length,Error Expected=Error::None)
{ auto Identity=Id(Namespace,Index); Require(Db.Execute(Operation::Set,Identity,Sized(Identity,Length),End()).Code==Expected); }
std::filesystem::path Folder(const std::filesystem::path& Root,const char* Name)
{ auto Path=Root/Name; Require(std::filesystem::create_directory(Path)); return Path; }
void ByteLimits(const std::filesystem::path& Path)
{
    const auto Start=Clock::now();
    {
        Backend Db(Path,End());
        for (unsigned Ns=0; Ns<16; ++Ns) {
            for (unsigned Index=0; Index<256; ++Index) Store(Db,Ns,Index,65531); // 65531 + S + four-digit key = 65536
            Store(Db,Ns,0,65532,Error::QuotaExceeded); // exactly one byte over namespace
            Store(Db,Ns,0,65531); // same-size overwrite at exact quota
        }
        Store(Db,16,0,95,Error::QuotaExceeded); // global full, new namespace otherwise fits
        Store(Db,0,0,65431); // release exactly 100 bytes
        Store(Db,16,0,95); // exact global cap again, this namespace not at its cap
        Store(Db,16,0,96,Error::QuotaExceeded); // exactly one byte over global
        auto Found=Db.Execute(Operation::Get,Id(16,0),{},End()); Require(Found.Code==Error::None && Found.Envelope.size()==95);
        Store(Db,16,0,94); Store(Db,16,0,95); // smaller then larger restores exact cap
        Require(Db.Execute(Operation::Remove,Id(16,0),{},End()).Code==Error::None);
        Store(Db,0,0,65531); // removal atomically freed 100 bytes
        // Direct private-backend observations, not a script throughput promise.
        auto Observe=[&](Operation Op,const char* Label) {
            auto Identity=Id(0,0); auto Envelope=Op==Operation::Set ? Sized(Identity,65531) : Bytes{};
            const auto Before=Clock::now(); auto Result=Db.Execute(Op,Identity,Envelope,End());
            Require(Result.Code==Error::None);
            std::printf("[CarbonLuau:Persistence] Near-full %s (65531-byte envelope) %.3fms\n",Label,std::chrono::duration<double,std::milli>(Clock::now()-Before).count());
        };
        Observe(Operation::Get,"Get"); Observe(Operation::Set,"same-size Set"); Observe(Operation::Remove,"Remove");
        Store(Db,0,0,65531); // restart still checks the complete exact-limit database
    }
    const auto Reload=Clock::now(); { Backend Db(Path,Clock::now()+std::chrono::seconds(30)); }
    std::printf("[CarbonLuau:Persistence] Exact 16MiB/256MiB quota PASS fill/rejections/overwrite/remove %.1fms; full restart %.1fms\n",
        std::chrono::duration<double,std::milli>(Reload-Start).count(),std::chrono::duration<double,std::milli>(Clock::now()-Reload).count());
}
void NamespaceLimits(const std::filesystem::path& Path)
{
    { Backend Db(Path,End());
      for (unsigned Ns=0; Ns<256; ++Ns) Store(Db,Ns,0,100);
      Store(Db,256,0,100,Error::QuotaExceeded);
      Require(Db.Execute(Operation::Remove,Id(0,0),{},End()).Code==Error::None);
      Store(Db,256,0,100); }
    Backend Db(Path,Clock::now()+std::chrono::seconds(30));
    Require(!Db.Execute(Operation::Get,Id(0,0),{},End()).Found);
    std::puts("[CarbonLuau:Persistence] 256/257 nonempty namespace accounting PASS");
}
void KeyLimits(const std::filesystem::path& Path)
{
    { Backend Db(Path,End()); }
    // Bounded offline fixture setup, not production mutation evidence. Avoid
    // 100,000 fsyncs solely to reach the exact count boundary. Every row uses the
    // production codec; subsequent startup revalidates all data/counters.
    sqlite3* Db=nullptr; Require(sqlite3_open((Path/"store.sqlite3").string().c_str(),&Db)==SQLITE_OK);
    Require(sqlite3_exec(Db,"PRAGMA journal_mode=PERSIST; PRAGMA synchronous=EXTRA; BEGIN IMMEDIATE",nullptr,nullptr,nullptr)==SQLITE_OK);
    sqlite3_stmt* Insert=nullptr; Require(sqlite3_prepare_v2(Db,"INSERT INTO Records VALUES(?1,?2,?3,?4,?5)",-1,&Insert,nullptr)==SQLITE_OK);
    for (unsigned Ns=0; Ns<10; ++Ns) for (unsigned Index=0; Index<10000; ++Index) {
        auto Identity=Id(Ns,Index); Value Boolean; Boolean.Type=Kind::Boolean; auto Data=Encode(Identity,Boolean,End());
        std::string Name=std::string(1,Ns ? '\1' : '\0')+Identity.Package;
        Require(sqlite3_bind_blob(Insert,1,Name.data(),int(Name.size()),SQLITE_TRANSIENT)==SQLITE_OK);
        Require(sqlite3_bind_blob(Insert,2,Identity.Store.data(),int(Identity.Store.size()),SQLITE_TRANSIENT)==SQLITE_OK);
        Require(sqlite3_bind_blob(Insert,3,Identity.Key.data(),int(Identity.Key.size()),SQLITE_TRANSIENT)==SQLITE_OK);
        Require(sqlite3_bind_blob(Insert,4,Data.data(),int(Data.size()),SQLITE_TRANSIENT)==SQLITE_OK);
        Require(sqlite3_bind_int64(Insert,5,int64_t(Data.size()+Identity.Store.size()+Identity.Key.size()))==SQLITE_OK);
        Require(sqlite3_step(Insert)==SQLITE_DONE && sqlite3_reset(Insert)==SQLITE_OK);
    }
    sqlite3_finalize(Insert);
    Require(sqlite3_exec(Db,"INSERT INTO Quotas SELECT Namespace,sum(Charge),count(*),count(DISTINCT Store) FROM Records GROUP BY Namespace; UPDATE Totals SET Bytes=(SELECT sum(Charge) FROM Records),Keys=100000,Namespaces=10; COMMIT",nullptr,nullptr,nullptr)==SQLITE_OK);
    Require(sqlite3_close(Db)==SQLITE_OK);
    {
        Backend StoreDb(Path,Clock::now()+std::chrono::seconds(30));
        Store(StoreDb,0,10000,100,Error::QuotaExceeded); // per namespace
        Store(StoreDb,10,0,100,Error::QuotaExceeded); // global, empty namespace
        Require(StoreDb.Execute(Operation::Remove,Id(0,0),{},End()).Code==Error::None);
        Store(StoreDb,10,0,100); // replace global slot, create namespace
        Store(StoreDb,0,10000,100,Error::QuotaExceeded);
    }
    Backend StoreDb(Path,Clock::now()+std::chrono::seconds(30));
    std::puts("[CarbonLuau:Persistence] 10000/10001 namespace and 100000/100001 global keys PASS (offline seeded boundary)");
}
}
int main()
{
    try {
        auto Root=std::filesystem::current_path()/("quota-"+std::to_string(Clock::now().time_since_epoch().count()));
        Require(std::filesystem::create_directory(Root)); ByteLimits(Folder(Root,"bytes"));
        NamespaceLimits(Folder(Root,"namespaces")); KeyLimits(Folder(Root,"keys")); return 0;
    } catch (const Failure& Problem) { std::fprintf(stderr,"[CarbonLuau:Persistence] Quota test failure code=%u\n",unsigned(Problem.Code)); return 1; }
    catch (const std::exception& Problem) { std::fprintf(stderr,"[CarbonLuau:Persistence] Quota test failure: %s\n",Problem.what()); return 1; }
}
