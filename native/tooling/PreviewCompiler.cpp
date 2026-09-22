#include "../src/scripts/CompilePolicy.hpp"
#include "carbonluau_native.h"

namespace CarbonLuau::Runtime {
// The preview worker already is the disposable compiler/VM boundary. No nested
// process and no workspace-selectable compiler executable are needed here.
CompileResult CompileSource(const std::string& Source) { return CompileBoundedSource(Source); }
}
extern "C" CARBONLUAU_EXPORT uint32_t cl_preview_bridge_version(void) { return 1; }
#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#else
#include <sys/resource.h>
#include <sys/prctl.h>
#include <signal.h>
#endif
extern "C" CARBONLUAU_EXPORT uint32_t cl_preview_containment(void)
{
    constexpr unsigned long long Bytes = 256ull * 1024 * 1024;
#ifdef _WIN32
    JOBOBJECT_EXTENDED_LIMIT_INFORMATION Limits{};
    if (!QueryInformationJobObject(nullptr, JobObjectExtendedLimitInformation, &Limits, sizeof(Limits), nullptr)) return 0;
    DWORD Required = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE | JOB_OBJECT_LIMIT_PROCESS_MEMORY | JOB_OBJECT_LIMIT_ACTIVE_PROCESS;
    return (Limits.BasicLimitInformation.LimitFlags & Required) == Required && Limits.ProcessMemoryLimit <= Bytes &&
        Limits.ProcessMemoryLimit > 0 && Limits.BasicLimitInformation.ActiveProcessLimit == 1;
#else
    rlimit Data{}, Address{}; int ParentSignal = 0;
    return getrlimit(RLIMIT_DATA, &Data) == 0 && Data.rlim_max <= Bytes &&
        getrlimit(RLIMIT_AS, &Address) == 0 && Address.rlim_max <= 2ull * 1024 * 1024 * 1024 &&
        prctl(PR_GET_PDEATHSIG, &ParentSignal) == 0 && ParentSignal == SIGKILL;
#endif
}
