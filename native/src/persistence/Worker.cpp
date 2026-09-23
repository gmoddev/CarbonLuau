#include "Backend.hpp"
#include <algorithm>
#include <cerrno>
#include <cstdio>
#include <cstring>
#include <cwchar>
#include <fstream>
#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#include <fcntl.h>
#include <io.h>
#else
#include <csignal>
#include <fcntl.h>
#include <sys/file.h>
#include <sys/prctl.h>
#include <sys/resource.h>
#include <sys/vfs.h>
#include <unistd.h>
#define _read read
#define _write write
#endif

using namespace CarbonLuau::Persistence;
namespace {
bool ReadAll(uint8_t* Data, size_t Size)
{ while (Size) { int Count=int(_read(0,Data,unsigned(std::min<size_t>(Size,16384)))); if (Count<=0) return false; Data+=Count; Size-=size_t(Count); } return true; }
bool WriteAll(const uint8_t* Data, size_t Size)
{ while (Size) { int Count=int(_write(1,Data,unsigned(std::min<size_t>(Size,16384)))); if (Count<=0) return false; Data+=Count; Size-=size_t(Count); } return true; }
bool ReadFrame(Bytes& Frame)
{
    uint8_t Header[4]; if (!ReadAll(Header,4)) return false;
    uint32_t Length=Read32(Header); Require(Length>=8 && Length<=MaximumFrame-4);
    Frame.resize(Length); Require(ReadAll(Frame.data(),Frame.size())); return true;
}
bool WriteFrame(const Bytes& Frame)
{ Bytes Header; Put32(Header,uint32_t(Frame.size())); return WriteAll(Header.data(),Header.size()) && WriteAll(Frame.data(),Frame.size()); }
struct Reader {
    const Bytes& Data; size_t Position=0;
    const uint8_t* Take(size_t Size) { Require(Size<=Data.size()-Position); const auto* Ptr=Data.data()+Position; Position+=Size; return Ptr; }
    uint32_t U32() { return Read32(Take(4)); }
    uint64_t U64() { return Read64(Take(8)); }
    std::string String(size_t Limit) { uint32_t Size=U32(); Require(Size<=Limit); const auto* Ptr=Take(Size); return std::string(reinterpret_cast<const char*>(Ptr),Size); }
};
// Same OS monotonic millisecond clock used by the managed supervisor. No UTC.
uint64_t Now()
{
#ifdef _WIN32
    return GetTickCount64();
#else
    timespec Time{}; Require(clock_gettime(CLOCK_MONOTONIC,&Time)==0,Error::StorageUnavailable);
    return uint64_t(Time.tv_sec)*1000+uint64_t(Time.tv_nsec)/1000000;
#endif
}
void CheckDirectory(const std::filesystem::path& Directory)
{
        Require(Directory.is_absolute(),Error::StorageUnavailable);
        for (auto Part=Directory; !Part.empty(); Part=Part.parent_path()) {
#ifdef _WIN32
            DWORD Attributes=GetFileAttributesW(Part.c_str());
            Require(Attributes!=INVALID_FILE_ATTRIBUTES && !(Attributes&FILE_ATTRIBUTE_REPARSE_POINT),Error::StorageUnavailable);
#else
            Require(!std::filesystem::is_symlink(std::filesystem::symlink_status(Part)),Error::StorageUnavailable);
#endif
            if (Part==Part.parent_path()) break;
        }
}
void CheckFilesystem(const std::filesystem::path& Directory)
{
    // Only directly qualified local storage profiles. No network/FUSE fallback.
#ifdef _WIN32
    wchar_t Volume[32768]{}, Format[32]{};
    Require(GetVolumePathNameW(Directory.c_str(),Volume,32768)!=0 && GetDriveTypeW(Volume)==DRIVE_FIXED &&
        GetVolumeInformationW(Volume,nullptr,0,nullptr,nullptr,nullptr,Format,32)!=0 && std::wcscmp(Format,L"NTFS")==0,
        Error::StorageUnavailable);
#else
    struct statfs Info{};
    Require(statfs(Directory.c_str(),&Info)==0 && Info.f_type==0xef53,Error::StorageUnavailable);
#endif
}
class Ownership {
public:
    explicit Ownership(const std::filesystem::path& Directory)
    {
        CheckDirectory(Directory);
        auto Path=Directory/"owner.lock";
#ifdef _WIN32
        DWORD Attributes=GetFileAttributesW(Path.c_str());
        Require(Attributes==INVALID_FILE_ATTRIBUTES || !(Attributes&(FILE_ATTRIBUTE_REPARSE_POINT|FILE_ATTRIBUTE_DIRECTORY)),Error::StorageUnavailable);
        Handle=CreateFileW(Path.c_str(),GENERIC_READ|GENERIC_WRITE,FILE_SHARE_READ,nullptr,OPEN_ALWAYS,FILE_ATTRIBUTE_NORMAL,nullptr);
        Require(Handle!=INVALID_HANDLE_VALUE,Error::StorageBusy);
#else
        Handle=open(Path.c_str(),O_RDWR|O_CREAT|O_CLOEXEC|O_NOFOLLOW,0600);
        Require(Handle>=0,Error::StorageUnavailable);
        if (flock(Handle,LOCK_EX|LOCK_NB)!=0) { close(Handle); Handle=-1; throw Failure(Error::StorageBusy); }
#endif
    }
    ~Ownership() {
#ifdef _WIN32
        if (Handle!=INVALID_HANDLE_VALUE) CloseHandle(Handle);
#else
        if (Handle>=0) close(Handle);
#endif
    }
private:
#ifdef _WIN32
    HANDLE Handle=INVALID_HANDLE_VALUE;
#else
    int Handle=-1;
#endif
};
void Contain(unsigned Parent)
{
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX|SEM_NOOPENFILEERRORBOX);
    _setmode(0,_O_BINARY); _setmode(1,_O_BINARY);
    // The parent assigns its kill-on-close job before sending the initialization
    // frame. No database access occurs before that frame or this verification.
    (void)Parent;
#else
    Require(Parent>0 && unsigned(getppid())==Parent,Error::StorageUnavailable);
    Require(prctl(PR_SET_PDEATHSIG,SIGKILL)==0 && unsigned(getppid())==Parent,Error::StorageUnavailable);
    rlimit Memory{256ull*1024*1024,256ull*1024*1024}; Require(setrlimit(RLIMIT_AS,&Memory)==0,Error::StorageUnavailable);
    rlimit Core{0,0}; Require(setrlimit(RLIMIT_CORE,&Core)==0,Error::StorageUnavailable);
#endif
}
void VerifyContainment()
{
#ifdef _WIN32
    JOBOBJECT_EXTENDED_LIMIT_INFORMATION Limits{};
    Require(QueryInformationJobObject(nullptr,JobObjectExtendedLimitInformation,&Limits,sizeof(Limits),nullptr)!=0 &&
        (Limits.BasicLimitInformation.LimitFlags&JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE) &&
        (Limits.BasicLimitInformation.LimitFlags&JOB_OBJECT_LIMIT_PROCESS_MEMORY) && Limits.ProcessMemoryLimit<=256ull*1024*1024,
        Error::StorageUnavailable);
#endif
}
}
int main(int Count,char** Args)
{
    const char* Stage="containment";
    try {
        Require(Count==2); unsigned Parent=unsigned(std::stoul(Args[1])); Contain(Parent);
        Bytes Frame; Require(ReadFrame(Frame)); VerifyContainment();
        Stage="initialization";
        Reader Init{Frame}; Require(!std::memcmp(Init.Take(4),"CLPI",4) && Init.U32()==1);
        const auto Directory=Init.String(32768); Require(ValidText(Directory,32768) && Init.Position==Frame.size());
        auto Path=std::filesystem::u8path(Directory);
        Require(Path.is_absolute(),Error::StorageUnavailable);
        Stage="directory";
        // Host passes the fixed persistence child; parent directories must exist.
        CheckDirectory(Path.parent_path());
        if (!std::filesystem::exists(Path)) std::filesystem::create_directory(Path);
        Stage="filesystem"; CheckFilesystem(Path);
        Stage="ownership"; Ownership Owner(Path);
        Stage="backend"; Backend Store(Path,Clock::now()+std::chrono::seconds(30));
        Bytes Ready{'C','L','P','R'}; Put32(Ready,1); Put32(Ready,0); Require(WriteFrame(Ready));
        Stage="protocol";
        uint64_t Last=0;
        while (ReadFrame(Frame)) {
            Reader Input{Frame}; Require(!std::memcmp(Input.Take(4),"CLPQ",4) && Input.U32()==1);
            Operation Op=Operation(Input.U32()); uint64_t Nonce=Input.U64(); Require(Nonce>Last); Last=Nonce;
            std::array<uint64_t,4> Route{}; for (auto& Token : Route) { Token=Input.U64(); Require(Token!=0); }
            uint64_t Expires=Input.U64(); uint64_t Time=Now(); Require(Expires<=Time+5000);
            uint32_t Tag=Input.U32(); Require(Tag<=1);
            Identity Id{Tag!=0,Input.String(65),Input.String(64),Input.String(128)};
            uint32_t Length=Input.U32(); Require(Length<=MaximumEnvelope); const auto* Start=Input.Take(Length);
            Bytes Envelope(Start,Start+Length); Require(Input.Position==Frame.size());
#if defined(CARBONLUAU_STORAGE_FIXTURE) && CARBONLUAU_STORAGE_FIXTURE == 2
            // Test executable only: exercise immutable transport deadline/reaping.
            if (Op==Operation::Set) for (;;) {
#ifdef _WIN32
                Sleep(1000);
#else
                sleep(1);
#endif
            }
#endif
            Result Outcome=Expires<=Time ? Result{Error::DeadlineExceeded} :
                Store.Execute(Op,Id,Envelope,Clock::now()+std::chrono::milliseconds(Expires-Time));
#if defined(CARBONLUAU_STORAGE_FIXTURE) && CARBONLUAU_STORAGE_FIXTURE == 1
            if (Op==Operation::Set && Outcome.Code==Error::None) std::_Exit(79);
#endif
            Bytes Reply{'C','L','P','S'}; Put32(Reply,1); Put32(Reply,uint32_t(Outcome.Code)); Put64(Reply,Nonce);
            for (auto Token : Route) Put64(Reply,Token);
            Put32(Reply,(Outcome.Found ? 1u : 0u) | (Outcome.NamespacePresent ? 2u : 0u)); Put32(Reply,uint32_t(Outcome.Envelope.size()));
            Reply.insert(Reply.end(),Outcome.Envelope.begin(),Outcome.Envelope.end());
            if (!WriteFrame(Reply)) return 0;
            if (!Store.Available()) return 2;
        }
        return 0;
    } catch (const Failure& Problem) {
        std::fprintf(stderr,"[CarbonLuau:Storage] failure stage=%s code=%u\n",Stage,unsigned(Problem.Code));
        Bytes Reply{'C','L','P','R'}; Put32(Reply,1); Put32(Reply,uint32_t(Problem.Code)); WriteFrame(Reply); return 2;
    } catch (...) {
        std::fprintf(stderr,"[CarbonLuau:Storage] failure stage=%s code=exception\n",Stage);
        return 3;
    }
}
