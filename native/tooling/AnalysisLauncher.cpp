// Trusted launch boundary. No VM and no workspace-selected arguments or executable.
#include <string>
#include <vector>
#include <cstdlib>
#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
static std::wstring Quote(const wchar_t* Value) {
    std::wstring Result = L"\""; unsigned Slashes = 0;
    for (; *Value; ++Value) {
        if (*Value == L'\\') { ++Slashes; continue; }
        Result.append(Slashes * (*Value == L'"' ? 2 : 1), L'\\'); Slashes = 0;
        if (*Value == L'"') Result += L'\\';
        Result += *Value;
    }
    Result.append(Slashes * 2, L'\\'); return Result + L'"';
}
int wmain(int Count, wchar_t** Args) {
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
    if (Count < 3) return 120;
    HANDLE Parent = OpenProcess(SYNCHRONIZE, FALSE, wcstoul(Args[1], nullptr, 10));
    HANDLE Job = CreateJobObjectW(nullptr, nullptr);
    if (!Parent || !Job) return 121;
    JOBOBJECT_EXTENDED_LIMIT_INFORMATION Limits{};
    Limits.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE | JOB_OBJECT_LIMIT_PROCESS_MEMORY | JOB_OBJECT_LIMIT_ACTIVE_PROCESS;
    Limits.BasicLimitInformation.ActiveProcessLimit = 1;
    Limits.ProcessMemoryLimit = SIZE_T(1024) * 1024 * 1024;
    if (!SetInformationJobObject(Job, JobObjectExtendedLimitInformation, &Limits, sizeof(Limits))) return 122;
    SIZE_T Size = 0;
    InitializeProcThreadAttributeList(nullptr, 1, 0, &Size);
    std::vector<unsigned char> Storage(Size);
    auto Attributes = reinterpret_cast<LPPROC_THREAD_ATTRIBUTE_LIST>(Storage.data());
    if (!InitializeProcThreadAttributeList(Attributes, 1, 0, &Size)) return 123;
    HANDLE Handles[] = {GetStdHandle(STD_INPUT_HANDLE), GetStdHandle(STD_OUTPUT_HANDLE), GetStdHandle(STD_ERROR_HANDLE)};
    for (HANDLE Handle : Handles) if (!SetHandleInformation(Handle, HANDLE_FLAG_INHERIT, HANDLE_FLAG_INHERIT)) return 124;
    if (!UpdateProcThreadAttribute(Attributes, 0, PROC_THREAD_ATTRIBUTE_HANDLE_LIST, Handles, sizeof(Handles), nullptr, nullptr)) return 125;
    STARTUPINFOEXW Startup{}; Startup.StartupInfo.cb = sizeof(Startup);
    Startup.StartupInfo.dwFlags = STARTF_USESTDHANDLES;
    Startup.StartupInfo.hStdInput = Handles[0]; Startup.StartupInfo.hStdOutput = Handles[1]; Startup.StartupInfo.hStdError = Handles[2];
    Startup.lpAttributeList = Attributes;
    std::wstring Command;
    for (int Index = 2; Index < Count; ++Index) { if (Index != 2) Command += L' '; Command += Quote(Args[Index]); }
    PROCESS_INFORMATION Child{};
    if (!CreateProcessW(Args[2], Command.data(), nullptr, nullptr, TRUE, CREATE_NO_WINDOW | CREATE_SUSPENDED | EXTENDED_STARTUPINFO_PRESENT,
        nullptr, nullptr, &Startup.StartupInfo, &Child)) return 126;
    DeleteProcThreadAttributeList(Attributes);
    if (!AssignProcessToJobObject(Job, Child.hProcess) || ResumeThread(Child.hThread) == DWORD(-1)) {
        TerminateProcess(Child.hProcess, 127); WaitForSingleObject(Child.hProcess, INFINITE); return 127;
    }
    CloseHandle(Child.hThread);
    HANDLE Waits[] = {Child.hProcess, Parent};
    DWORD Wait = WaitForMultipleObjects(2, Waits, FALSE, INFINITE), Code = 128;
    if (Wait != WAIT_OBJECT_0) TerminateJobObject(Job, 128);
    WaitForSingleObject(Child.hProcess, INFINITE); GetExitCodeProcess(Child.hProcess, &Code);
    CloseHandle(Child.hProcess); CloseHandle(Parent); CloseHandle(Job); return int(Code);
}
#else
#include <unistd.h>
#include <signal.h>
#include <sys/wait.h>
#include <sys/resource.h>
#include <cerrno>
#include <cstdio>
#ifdef __linux__
#include <sys/prctl.h>
#else
#include <libproc.h>
#endif
static volatile sig_atomic_t Stopping = 0;
static void Stop(int) { Stopping = 1; }
int main(int Count, char** Args) {
    if (Count < 3) return 120;
    pid_t Parent = pid_t(strtol(Args[1], nullptr, 10));
    if (Parent <= 1 || getppid() != Parent) return 121;
    signal(SIGTERM, Stop); signal(SIGINT, Stop); signal(SIGHUP, Stop);
#ifdef __linux__
    if (prctl(PR_SET_PDEATHSIG, SIGTERM) != 0 || getppid() != Parent) return 122;
#endif
    pid_t Child = fork();
    if (Child < 0) return 123;
    if (Child == 0) {
        signal(SIGTERM, SIG_DFL); signal(SIGINT, SIG_DFL); signal(SIGHUP, SIG_DFL);
        if (setsid() < 0) _exit(124);
#ifdef __linux__
        pid_t Launcher = getppid();
        if (prctl(PR_SET_PDEATHSIG, SIGKILL) != 0 || getppid() != Launcher || Launcher == 1) _exit(125);
        rlimit Limit{rlim_t(2) * 1024 * 1024 * 1024, rlim_t(2) * 1024 * 1024 * 1024};
        if (setrlimit(RLIMIT_AS, &Limit) != 0) _exit(126);
#endif
        rlimit Core{0, 0}; if (setrlimit(RLIMIT_CORE, &Core) != 0) _exit(127);
        execv(Args[2], Args + 2); _exit(128);
    }
    int Status = 0;
    for (;;) {
        pid_t Result = waitpid(Child, &Status, WNOHANG);
        if (Result == Child) break;
        if (Result < 0 && errno != EINTR) { Stopping = 1; }
        unsigned long long Resident = 0;
#ifdef __linux__
        char Name[80]; snprintf(Name, sizeof(Name), "/proc/%d/statm", int(Child));
        FILE* File = fopen(Name, "r"); unsigned long long Pages = 0, Total = 0;
        if (File) { if (fscanf(File, "%llu %llu", &Total, &Pages) == 2) Resident = Pages * sysconf(_SC_PAGESIZE); else Stopping = 1; fclose(File); }
        else Stopping = 1;
#else
        proc_taskinfo Info{};
        if (proc_pidinfo(Child, PROC_PIDTASKINFO, 0, &Info, sizeof(Info)) == sizeof(Info)) Resident = Info.pti_resident_size;
        else Stopping = 1;
#endif
        if (Stopping || getppid() != Parent || Resident > 1024ull * 1024 * 1024) {
            kill(-Child, SIGKILL); kill(Child, SIGKILL);
            while (waitpid(Child, &Status, 0) < 0 && errno == EINTR) {}
            return 129;
        }
        usleep(50000);
    }
    kill(-Child, SIGKILL);
    return WIFEXITED(Status) ? WEXITSTATUS(Status) : 130;
}
#endif
