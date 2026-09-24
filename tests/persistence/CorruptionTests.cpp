#include "Backend.hpp"
#include "sqlite3.h"
#include <cstdio>
#include <fstream>
using namespace CarbonLuau::Persistence;
namespace {
Deadline End() { return Clock::now()+std::chrono::seconds(5); }
Bytes Read(const std::filesystem::path& Path)
{
    Require(std::filesystem::file_size(Path)<=MaximumFrame);
    std::ifstream Input(Path,std::ios::binary); return Bytes(std::istreambuf_iterator<char>(Input),{});
}
void Write(const std::filesystem::path& Path,const Bytes& Data)
{ std::ofstream Out(Path,std::ios::binary|std::ios::trunc); Out.write(reinterpret_cast<const char*>(Data.data()),Data.size()); Require(bool(Out)); }
void Reject(const std::filesystem::path& Path,Error Expected,bool Preserve=true)
{
    const auto Db=Path/"store.sqlite3", Journal=Path/"store.sqlite3-journal";
    Bytes Before=Preserve ? Read(Db) : Bytes{}, BeforeJournal=Preserve && std::filesystem::exists(Journal) ? Read(Journal) : Bytes{};
    bool Rejected=false;
    try { Backend Store(Path,End()); }
    catch (const Failure& Problem) { Rejected=Problem.Code==Expected; }
    Require(Rejected);
    if (Preserve) {
        Require(Read(Db)==Before);
        if (!BeforeJournal.empty()) Require(Read(Journal)==BeforeJournal);
    }
}
void Edit(const std::filesystem::path& Path,const char* Sql)
{
    sqlite3* Db=nullptr; Require(sqlite3_open((Path/"store.sqlite3").string().c_str(),&Db)==SQLITE_OK);
    Require(sqlite3_exec(Db,"PRAGMA journal_mode=PERSIST; PRAGMA synchronous=EXTRA",nullptr,nullptr,nullptr)==SQLITE_OK);
    Require(sqlite3_exec(Db,Sql,nullptr,nullptr,nullptr)==SQLITE_OK); Require(sqlite3_close(Db)==SQLITE_OK);
}
void Big32(Bytes& Data,size_t At,uint32_t Value)
{ for (unsigned I=0; I<4; ++I) Data[At+I]=uint8_t(Value>>(24-8*I)); }
}
int main()
{
    try {
        auto Root=std::filesystem::current_path()/("corrupt-"+std::to_string(Clock::now().time_since_epoch().count()));
        Require(std::filesystem::create_directory(Root)); auto Base=Root/"baseline"; Require(std::filesystem::create_directory(Base));
        Identity Id{false,"","S","K"}; Value Boolean; Boolean.Type=Kind::Boolean;
        { Backend Store(Base,End()); Require(Store.Execute(Operation::Set,Id,Encode(Id,Boolean,End()),End()).Code==Error::None); }
        auto Copy=[&](const char* Name) {
            auto Path=Root/Name; Require(std::filesystem::create_directory(Path));
            for (const auto& File : std::filesystem::directory_iterator(Base)) std::filesystem::copy_file(File.path(),Path/File.path().filename());
            return Path;
        };
        auto Path=Copy("schema-version"); Edit(Path,"PRAGMA user_version=2"); Reject(Path,Error::FormatUnsupported);
        Path=Copy("application-id"); Edit(Path,"PRAGMA application_id=0"); Reject(Path,Error::FormatUnsupported);
        Path=Copy("page-size"); Edit(Path,"PRAGMA page_size=8192; VACUUM"); Reject(Path,Error::StorageUnavailable);
        Path=Copy("auto-vacuum"); Edit(Path,"PRAGMA auto_vacuum=FULL; VACUUM"); Reject(Path,Error::StorageUnavailable);
        Path=Copy("schema-shape"); Edit(Path,"CREATE TABLE Surprise(X)"); Reject(Path,Error::StorageCorrupt);
        Path=Copy("quota"); Edit(Path,"UPDATE Totals SET Bytes=Bytes+1"); Reject(Path,Error::StorageCorrupt);
        Path=Copy("checksum"); Edit(Path,"UPDATE Records SET Envelope=CAST(substr(Envelope,1,length(Envelope)-1)||X'FF' AS BLOB)"); Reject(Path,Error::StorageCorrupt);
        Path=Copy("envelope-version"); Edit(Path,"UPDATE Records SET Envelope=CAST(substr(Envelope,1,4)||X'02000000'||substr(Envelope,9) AS BLOB)"); Reject(Path,Error::FormatUnsupported);
        Path=Copy("header"); auto Data=Read(Path/"store.sqlite3"); Data[0]^=1; Write(Path/"store.sqlite3",Data); Reject(Path,Error::StorageCorrupt);
        Path=Copy("unexpected"); Write(Path/"unexpected",Bytes{'x'}); Reject(Path,Error::StorageUnavailable);
        const Bytes Magic{0xd9,0xd5,0x05,0xf9,0x20,0xa1,0x63,0xd7};
        Path=Copy("hot-original-size"); Data.assign(512,0); std::copy(Magic.begin(),Magic.end(),Data.begin());
        Big32(Data,16,131073); Big32(Data,20,512); Big32(Data,24,4096); Write(Path/"store.sqlite3-journal",Data); Reject(Path,Error::StorageCorrupt);
        Path=Copy("super-journal"); Data.assign(512,0); auto Protected=Root/"must-remain"; Write(Protected,Bytes{'s','a','f','e'});
        std::string Name=Protected.u8string(); Data.insert(Data.end(),Name.begin(),Name.end());
        size_t Footer=Data.size(); Data.resize(Footer+16); Big32(Data,Footer,uint32_t(Name.size()));
        uint32_t Sum=0; for (char Byte : Name) Sum+=int8_t(Byte); Big32(Data,Footer+4,Sum);
        std::copy(Magic.begin(),Magic.end(),Data.begin()+Footer+8); Write(Path/"store.sqlite3-journal",Data);
        Reject(Path,Error::StorageCorrupt); Require(Read(Protected)==Bytes({'s','a','f','e'}));
        Path=Copy("physical-db-size"); std::filesystem::resize_file(Path/"store.sqlite3",512ull*1024*1024+1); Reject(Path,Error::StorageFull,false);
        Require(std::filesystem::file_size(Path/"store.sqlite3")==512ull*1024*1024+1);
        Path=Copy("physical-journal-size");
        constexpr uint64_t ExcessJournal=131072ull*(4096+8)+65536+1;
        std::filesystem::resize_file(Path/"store.sqlite3-journal",ExcessJournal);
        Reject(Path,Error::StorageFull,false);
        Require(std::filesystem::file_size(Path/"store.sqlite3-journal")==ExcessJournal);
        Path=Root/"hardlink"; Require(std::filesystem::create_directory(Path));
        std::filesystem::create_hard_link(Base/"store.sqlite3",Path/"store.sqlite3"); Reject(Path,Error::StorageUnavailable);
        Path=Root/"symlink"; Require(std::filesystem::create_directory(Path)); std::error_code LinkError;
        std::filesystem::create_symlink(Base/"store.sqlite3",Path/"store.sqlite3",LinkError);
        if (LinkError) std::printf("[CarbonLuau:Persistence] Symlink fixture unavailable: OS code %d (not qualified)\n",LinkError.value());
        else Reject(Path,Error::StorageUnavailable);
        std::puts("[CarbonLuau:Persistence] Corruption/unknown format/physical preflight PASS; files preserved"); return 0;
    } catch (const Failure& Problem) { std::fprintf(stderr,"[CarbonLuau:Persistence] Corruption test failure code=%u\n",unsigned(Problem.Code)); return 1; }
    catch (const std::exception& Problem) { std::fprintf(stderr,"[CarbonLuau:Persistence] Corruption test failure: %s\n",Problem.what()); return 1; }
}
