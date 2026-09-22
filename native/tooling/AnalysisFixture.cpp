// Qualification fixture only; never included in a tooling pack.
#include <cstdio>
#include <cstring>
#include <thread>
#include <chrono>
#include <cstdlib>
#include <vector>
#ifdef _WIN32
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#else
#include <unistd.h>
#include <sys/resource.h>
#endif
int main(int Count, char** Args) {
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
#endif
    if (Count > 1 && !std::strcmp(Args[1], "profile")) {
#ifdef _WIN32
        JOBOBJECT_EXTENDED_LIMIT_INFORMATION Limits{};
        if (!QueryInformationJobObject(nullptr, JobObjectExtendedLimitInformation, &Limits, sizeof(Limits), nullptr)) return 71;
        DWORD Required = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE | JOB_OBJECT_LIMIT_PROCESS_MEMORY | JOB_OBJECT_LIMIT_ACTIVE_PROCESS;
        if ((Limits.BasicLimitInformation.LimitFlags & Required) != Required || Limits.BasicLimitInformation.ActiveProcessLimit != 1 || Limits.ProcessMemoryLimit != SIZE_T(1024) * 1024 * 1024) return 72;
        if (Limits.BasicLimitInformation.LimitFlags & (JOB_OBJECT_LIMIT_BREAKAWAY_OK | JOB_OBJECT_LIMIT_SILENT_BREAKAWAY_OK)) return 73;
#elif defined(__linux__)
        rlimit Limit{};
        if (getrlimit(RLIMIT_AS, &Limit) != 0 || Limit.rlim_cur != rlim_t(2) * 1024 * 1024 * 1024 || Limit.rlim_max != Limit.rlim_cur || getpgrp() != getpid()) return 74;
#endif
        return 0;
    }
    if (Count > 1 && !std::strcmp(Args[1], "memory")) {
        std::vector<void*> Blocks;
        for (;;) {
            void* Block = std::malloc(16 * 1024 * 1024);
            if (!Block) return 129;
            std::memset(Block, 0x17, 16 * 1024 * 1024); Blocks.push_back(Block);
        }
    }
    if (Count > 1 && !std::strcmp(Args[1], "wait")) {
#ifdef _WIN32
        std::printf("%lu\n", GetCurrentProcessId());
#else
        std::printf("%d\n", int(getpid()));
#endif
        std::fflush(stdout);
        for (;;) std::this_thread::sleep_for(std::chrono::milliseconds(20));
    }
    if (std::strstr(Args[0], "malformed")) { std::fputs("Content-Length: 2\r\n\r\n{}", stdout); std::fflush(stdout); }
    return 66;
}
