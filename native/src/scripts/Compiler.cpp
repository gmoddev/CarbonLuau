#include "Compiler.hpp"
#include "CompilerProtocol.hpp"

#include <algorithm>
#include <array>
#include <atomic>
#include <chrono>
#include <filesystem>
#include <mutex>
#include <thread>
#include <vector>

#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#else
#include <cerrno>
#include <csignal>
#include <fcntl.h>
#include <poll.h>
#include <sys/prctl.h>
#include <sys/resource.h>
#include <sys/socket.h>
#include <sys/syscall.h>
#include <sys/types.h>
#include <sys/wait.h>
#include <unistd.h>
#include <dlfcn.h>
#endif

namespace CarbonLuau::Runtime {
namespace {
using Clock = std::chrono::steady_clock;
using Deadline = Clock::time_point;

std::filesystem::path DefaultCompilerPath()
{
#ifdef _WIN32
    static int Anchor = 0;
    HMODULE Module = nullptr;
    if (!GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
            reinterpret_cast<LPCWSTR>(&Anchor), &Module))
        return {};
    std::wstring Buffer(32768, L'\0');
    DWORD Length = GetModuleFileNameW(Module, Buffer.data(), DWORD(Buffer.size()));
    if (!Length || Length == Buffer.size()) return {};
    Buffer.resize(Length);
    return std::filesystem::path(Buffer).parent_path() / L"carbonluau_compiler.exe";
#else
    static int Anchor = 0;
    Dl_info Info{};
    if (!dladdr(&Anchor, &Info) || !Info.dli_fname) return {};
    return std::filesystem::absolute(Info.dli_fname).parent_path() / "carbonluau_compiler";
#endif
}

class CompilerProcess {
public:
    ~CompilerProcess() { Stop(); }

    CompileResult Compile(const std::string& Source) try
    {
        std::lock_guard<std::mutex> Lock(Mutex);
        if (Source.size() > CompilerProtocol::MaximumSourceBytes)
            return {CompileStatus::ProtocolFailure, {}, "source exceeds compiler request bound"};
        Deadline End = Clock::now() + std::chrono::milliseconds(CompilerProtocol::DeadlineMilliseconds);
        if (!Alive() && !Start()) return {CompileStatus::WorkerFailure, {}, "compiler worker could not start"};
        uint64_t RequestNonce = ++Nonce;
        if (!RequestNonce) RequestNonce = ++Nonce;
        std::vector<uint8_t> Request = CompilerProtocol::Request(RequestNonce, Source);
        if (!WriteAll(Request.data(), Request.size(), End)) {
            CompileStatus Status = Clock::now() >= End ? CompileStatus::Timeout : CompileStatus::WorkerFailure;
            Stop();
            return {Status, {}, Status == CompileStatus::Timeout ? "compiler deadline exceeded" : "compiler worker request failed"};
        }
        std::array<uint8_t, CompilerProtocol::ResponseHeaderSize> Header{};
        if (!ReadAll(Header.data(), Header.size(), End)) {
            CompileStatus Status = Clock::now() >= End ? CompileStatus::Timeout : CompileStatus::WorkerFailure;
            Stop();
            return {Status, {}, Status == CompileStatus::Timeout ? "compiler deadline exceeded" : "compiler worker ended without a response"};
        }
        if (std::memcmp(Header.data(), "CLCR", 4) || CompilerProtocol::ReadU32(Header.data() + 4) != CompilerProtocol::Version) {
            Stop();
            return {CompileStatus::ProtocolFailure, {}, "compiler worker response header mismatch"};
        }
        uint32_t WorkerStatus = CompilerProtocol::ReadU32(Header.data() + 8);
        uint32_t PayloadLength = CompilerProtocol::ReadU32(Header.data() + 12);
        uint64_t ResponseNonce = CompilerProtocol::ReadU64(Header.data() + 16);
        if (WorkerStatus > 1 || PayloadLength > CompilerProtocol::MaximumPayloadBytes || ResponseNonce != RequestNonce ||
            std::memcmp(Header.data() + 24, CARBONLUAU_REVISION, CompilerProtocol::RevisionLength)) {
            Stop();
            return {CompileStatus::ProtocolFailure, {}, "compiler worker response provenance or size mismatch"};
        }
        std::string Payload(PayloadLength, '\0');
        if (PayloadLength && !ReadAll(reinterpret_cast<uint8_t*>(Payload.data()), Payload.size(), End)) {
            CompileStatus Status = Clock::now() >= End ? CompileStatus::Timeout : CompileStatus::ProtocolFailure;
            Stop();
            return {Status, {}, Status == CompileStatus::Timeout ? "compiler deadline exceeded" : "compiler worker response was truncated"};
        }
        if (WorkerStatus != 0) {
            Stop();
            return {CompileStatus::WorkerFailure, {}, Payload.empty() ? "compiler worker reported an internal failure" : Payload};
        }
        if (Payload.empty()) {
            Stop();
            return {CompileStatus::ProtocolFailure, {}, "compiler worker returned an empty payload"};
        }
        return {CompileStatus::Success, std::move(Payload), {}};
    } catch (...) {
        std::lock_guard<std::mutex> Lock(Mutex);
        Stop();
        return {CompileStatus::WorkerFailure, {}, "compiler containment failure"};
    }

#ifdef CARBONLUAU_TESTING
    void SetPath(const std::string& Value)
    {
        std::lock_guard<std::mutex> Lock(Mutex);
        Stop();
        OverridePath = std::filesystem::u8path(Value);
    }

    void Reset()
    {
        std::lock_guard<std::mutex> Lock(Mutex);
        Stop();
        OverridePath.clear();
    }

    bool Running()
    {
        std::lock_guard<std::mutex> Lock(Mutex);
        return Alive();
    }
#endif

private:
    std::filesystem::path Path() const
    {
#ifdef CARBONLUAU_TESTING
        if (!OverridePath.empty()) return OverridePath;
#endif
        return DefaultCompilerPath();
    }

#ifdef _WIN32
    bool Start()
    {
        Stop();
        std::filesystem::path Executable = Path();
        if (Executable.empty() || !std::filesystem::is_regular_file(Executable)) return false;
        SECURITY_ATTRIBUTES Security{sizeof(Security), nullptr, TRUE};
        HANDLE ChildInput = nullptr, ParentInput = nullptr, ParentOutput = nullptr, ChildOutput = nullptr;
        HANDLE NullOutput = INVALID_HANDLE_VALUE;
        SIZE_T AttributeBytes = 0;
        PPROC_THREAD_ATTRIBUTE_LIST AttributeList = nullptr;
        HANDLE Inherited[3]{};
        STARTUPINFOEXW Startup{};
        std::wstring Command;
        PROCESS_INFORMATION ProcessInfo{};
        JOBOBJECT_EXTENDED_LIMIT_INFORMATION Limits{};
        Command = L"\"" + Executable.wstring() + L"\"";
        InitializeProcThreadAttributeList(nullptr, 1, 0, &AttributeBytes);
        Attributes.resize(AttributeBytes);
        if (!CreatePipe(&ChildInput, &ParentInput, &Security, 65536) ||
            !SetHandleInformation(ParentInput, HANDLE_FLAG_INHERIT, 0) ||
            !CreatePipe(&ParentOutput, &ChildOutput, &Security, 65536) ||
            !SetHandleInformation(ParentOutput, HANDLE_FLAG_INHERIT, 0))
            goto Failure;
        NullOutput = CreateFileW(L"NUL", GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, &Security, OPEN_EXISTING, 0, nullptr);
        if (NullOutput == INVALID_HANDLE_VALUE) goto Failure;
        AttributeList = reinterpret_cast<PPROC_THREAD_ATTRIBUTE_LIST>(Attributes.data());
        if (!InitializeProcThreadAttributeList(AttributeList, 1, 0, &AttributeBytes)) goto Failure;
        AttributesInitialized = true;
        Inherited[0] = ChildInput; Inherited[1] = ChildOutput; Inherited[2] = NullOutput;
        if (!UpdateProcThreadAttribute(AttributeList, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST, Inherited, sizeof(Inherited), nullptr, nullptr))
            goto Failure;
        Startup.StartupInfo.cb = sizeof(Startup);
        Startup.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
        Startup.StartupInfo.hStdInput = ChildInput;
        Startup.StartupInfo.hStdOutput = ChildOutput;
        Startup.StartupInfo.hStdError = NullOutput;
        Startup.lpAttributeList = AttributeList;
        Job = CreateJobObjectW(nullptr, nullptr);
        if (!Job) goto Failure;
        Limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE | JOB_OBJECT_LIMIT_PROCESS_MEMORY | JOB_OBJECT_LIMIT_ACTIVE_PROCESS;
        Limits.BasicLimitInformation.ActiveProcessLimit = 1;
        Limits.ProcessMemoryLimit = SIZE_T(CompilerProtocol::WorkerMemoryBytes);
        if (!SetInformationJobObject(Job, JobObjectExtendedLimitInformation, &Limits, sizeof(Limits))) goto Failure;
        if (!CreateProcessW(Executable.c_str(), Command.data(), nullptr, nullptr, TRUE,
                CREATE_NO_WINDOW | CREATE_SUSPENDED | EXTENDED_STARTUPINFO_PRESENT, nullptr,
                Executable.parent_path().c_str(), &Startup.StartupInfo, &ProcessInfo))
            goto Failure;
        Process = ProcessInfo.hProcess;
        Thread = ProcessInfo.hThread;
        if (!AssignProcessToJobObject(Job, Process) || ResumeThread(Thread) == DWORD(-1)) goto Failure;
        CloseHandle(Thread); Thread = nullptr;
        CloseHandle(ChildInput); ChildInput = nullptr;
        CloseHandle(ChildOutput); ChildOutput = nullptr;
        CloseHandle(NullOutput); NullOutput = INVALID_HANDLE_VALUE;
        Input = ParentInput; ParentInput = nullptr;
        Output = ParentOutput; ParentOutput = nullptr;
        DeleteProcThreadAttributeList(AttributeList);
        AttributesInitialized = false; Attributes.clear();
        return true;
    Failure:
        if (Thread) CloseHandle(Thread);
        if (Process) { TerminateProcess(Process, 1); CloseHandle(Process); }
        if (Job) CloseHandle(Job);
        if (ChildInput) CloseHandle(ChildInput);
        if (ParentInput) CloseHandle(ParentInput);
        if (ParentOutput) CloseHandle(ParentOutput);
        if (ChildOutput) CloseHandle(ChildOutput);
        if (NullOutput != INVALID_HANDLE_VALUE) CloseHandle(NullOutput);
        if (AttributesInitialized) DeleteProcThreadAttributeList(reinterpret_cast<PPROC_THREAD_ATTRIBUTE_LIST>(Attributes.data()));
        Process = Thread = Job = Input = Output = nullptr;
        AttributesInitialized = false; Attributes.clear();
        return false;
    }

    bool Alive()
    {
        return Process && WaitForSingleObject(Process, 0) == WAIT_TIMEOUT;
    }

    bool WriteAll(const uint8_t* Bytes, size_t Length, Deadline End)
    {
        size_t Offset = 0;
        while (Offset < Length && Clock::now() < End) {
            DWORD Written = 0;
            DWORD Chunk = DWORD(std::min<size_t>(Length - Offset, 16384));
            if (!WriteFile(Input, Bytes + Offset, Chunk, &Written, nullptr) || !Written) return false;
            Offset += Written;
        }
        return Offset == Length;
    }

    bool ReadAll(uint8_t* Bytes, size_t Length, Deadline End)
    {
        size_t Offset = 0;
        // Anonymous pipes do not support overlapped reads. Give the already
        // running worker a short cooperative window before using timed sleeps;
        // otherwise Windows' coarse scheduler tick adds one full tick to every
        // small module compile. A pathological compile reaches the sleeping
        // path after this bounded window and remains governed by End.
        Deadline CooperativeEnd = std::min(End, Clock::now() + std::chrono::milliseconds(5));
        while (Offset < Length && Clock::now() < End) {
            DWORD Available = 0;
            if (!PeekNamedPipe(Output, nullptr, 0, nullptr, &Available, nullptr)) return false;
            if (Available) {
                DWORD Read = 0;
                DWORD Chunk = DWORD(std::min<size_t>({Length - Offset, size_t(Available), size_t(16384)}));
                if (!ReadFile(Output, Bytes + Offset, Chunk, &Read, nullptr) || !Read) return false;
                Offset += Read;
                continue;
            }
            if (!Alive()) return false;
            if (Clock::now() < CooperativeEnd) { SwitchToThread(); continue; }
            std::this_thread::sleep_for(std::chrono::milliseconds(1));
        }
        return Offset == Length;
    }

    void Stop()
    {
        if (Input) { CloseHandle(Input); Input = nullptr; }
        if (Process && WaitForSingleObject(Process, 100) == WAIT_TIMEOUT) {
            if (Job) TerminateJobObject(Job, 1); else TerminateProcess(Process, 1);
            WaitForSingleObject(Process, 100);
        }
        if (Output) CloseHandle(Output);
        if (Thread) CloseHandle(Thread);
        if (Process) CloseHandle(Process);
        if (Job) CloseHandle(Job);
        Output = Thread = Process = Job = nullptr;
    }

    HANDLE Process = nullptr, Thread = nullptr, Job = nullptr, Input = nullptr, Output = nullptr;
    std::vector<uint8_t> Attributes;
    bool AttributesInitialized = false;
#else
    bool Start()
    {
        Stop();
        std::filesystem::path Executable = Path();
        if (Executable.empty() || !std::filesystem::is_regular_file(Executable) || access(Executable.c_str(), X_OK) != 0) return false;
        int InputPipe[2]{-1, -1}, OutputPipe[2]{-1, -1};
        if (socketpair(AF_UNIX, SOCK_STREAM | SOCK_CLOEXEC, 0, InputPipe) ||
            socketpair(AF_UNIX, SOCK_STREAM | SOCK_CLOEXEC, 0, OutputPipe)) goto Failure;
        Process = fork();
        if (Process < 0) goto Failure;
        if (Process == 0) {
#ifndef CARBONLUAU_SANITIZE
            struct rlimit Address{CompilerProtocol::WorkerMemoryBytes, CompilerProtocol::WorkerMemoryBytes};
#endif
            struct rlimit Files{16, 16};
            struct rlimit Core{0, 0};
#ifndef CARBONLUAU_SANITIZE
            setrlimit(RLIMIT_AS, &Address);
#endif
            setrlimit(RLIMIT_NOFILE, &Files); setrlimit(RLIMIT_CORE, &Core);
            prctl(PR_SET_NO_NEW_PRIVS, 1, 0, 0, 0);
            dup2(InputPipe[0], STDIN_FILENO); dup2(OutputPipe[1], STDOUT_FILENO);
            int Null = open("/dev/null", O_WRONLY);
            if (Null >= 0) { dup2(Null, STDERR_FILENO); if (Null > STDERR_FILENO) close(Null); }
#ifdef SYS_close_range
            syscall(SYS_close_range, 3u, ~0u, 0u);
#else
            for (int Descriptor = 3; Descriptor < 1024; ++Descriptor) close(Descriptor);
#endif
            execl(Executable.c_str(), Executable.c_str(), static_cast<char*>(nullptr));
            _exit(127);
        }
        close(InputPipe[0]); close(OutputPipe[1]);
        Input = InputPipe[1]; Output = OutputPipe[0];
        fcntl(Input, F_SETFL, fcntl(Input, F_GETFL) | O_NONBLOCK);
        fcntl(Output, F_SETFL, fcntl(Output, F_GETFL) | O_NONBLOCK);
        return true;
    Failure:
        if (InputPipe[0] >= 0) close(InputPipe[0]); if (InputPipe[1] >= 0) close(InputPipe[1]);
        if (OutputPipe[0] >= 0) close(OutputPipe[0]); if (OutputPipe[1] >= 0) close(OutputPipe[1]);
        Process = -1;
        return false;
    }

    bool Alive()
    {
        if (Process <= 0) return false;
        int Status = 0;
        pid_t Result = waitpid(Process, &Status, WNOHANG);
        if (Result == 0) return true;
        if (Result == Process || (Result < 0 && errno == ECHILD)) Process = -1;
        return false;
    }

    bool Wait(short Events, Deadline End)
    {
        auto Remaining = std::chrono::duration_cast<std::chrono::milliseconds>(End - Clock::now()).count();
        if (Remaining <= 0) return false;
        struct pollfd Descriptor{Events == POLLOUT ? Input : Output, Events, 0};
        int Result;
        do Result = poll(&Descriptor, 1, int(std::min<int64_t>(Remaining, 25))); while (Result < 0 && errno == EINTR);
        return Result > 0 && (Descriptor.revents & Events);
    }

    bool WriteAll(const uint8_t* Bytes, size_t Length, Deadline End)
    {
        size_t Offset = 0;
        while (Offset < Length && Clock::now() < End) {
            ssize_t Written = send(Input, Bytes + Offset, Length - Offset, MSG_NOSIGNAL);
            if (Written > 0) { Offset += size_t(Written); continue; }
            if (Written < 0 && errno == EINTR) continue;
            if (Written < 0 && (errno == EAGAIN || errno == EWOULDBLOCK)) { if (!Wait(POLLOUT, End)) continue; else continue; }
            return false;
        }
        return Offset == Length;
    }

    bool ReadAll(uint8_t* Bytes, size_t Length, Deadline End)
    {
        size_t Offset = 0;
        while (Offset < Length && Clock::now() < End) {
            ssize_t Read = read(Output, Bytes + Offset, Length - Offset);
            if (Read > 0) { Offset += size_t(Read); continue; }
            if (Read == 0) return false;
            if (errno == EINTR) continue;
            if (errno == EAGAIN || errno == EWOULDBLOCK) { if (!Alive()) return false; Wait(POLLIN, End); continue; }
            return false;
        }
        return Offset == Length;
    }

    void Stop()
    {
        if (Input >= 0) { close(Input); Input = -1; }
        if (Process > 0) {
            Deadline End = Clock::now() + std::chrono::milliseconds(100);
            while (Clock::now() < End) {
                int Status = 0; pid_t Result = waitpid(Process, &Status, WNOHANG);
                if (Result == Process) { Process = -1; break; }
                if (Result < 0 && errno == ECHILD) { Process = -1; break; }
                std::this_thread::sleep_for(std::chrono::milliseconds(1));
            }
            if (Process > 0) {
                kill(Process, SIGKILL);
                int Status = 0; pid_t Result;
                do Result = waitpid(Process, &Status, 0); while (Result < 0 && errno == EINTR);
                Process = -1;
            }
        }
        if (Output >= 0) { close(Output); Output = -1; }
        Process = -1;
    }

    pid_t Process = -1;
    int Input = -1, Output = -1;
#endif

    std::mutex Mutex;
    uint64_t Nonce = 0;
#ifdef CARBONLUAU_TESTING
    std::filesystem::path OverridePath;
#endif
};

CompilerProcess& Service()
{
    static CompilerProcess Value;
    return Value;
}
} // namespace

CompileResult CompileSource(const std::string& Source) { return Service().Compile(Source); }

#ifdef CARBONLUAU_TESTING
void SetCompilerExecutableForTesting(const std::string& Path) { Service().SetPath(Path); }
void ResetCompilerForTesting() { Service().Reset(); }
bool CompilerWorkerRunningForTesting() { return Service().Running(); }
#endif
} // namespace CarbonLuau::Runtime
