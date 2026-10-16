#pragma once
// Test executable only; no provider exports, allocation hooks or VM deadlines change.
#include <cstdio>
#include <cstdlib>
#ifdef _WIN32
#include <process.h>
#else
#include <unistd.h>
#endif

namespace TestDiagnostics {
inline void CleanupFailure()
{
    std::fputs("[CarbonLuau:Diagnostic] {\"schema\":1,\"category\":\"cleanup\"}\n", stderr);
}
inline void PersistenceFailure(unsigned Status)
{
    // Private persistence Error values, distinct from the VM's ClStatus values.
    const char* Category = Status == 8 ? "timeout" : Status >= 3 && Status <= 7 ? "infrastructure" : "exception";
    std::fprintf(stderr, "[CarbonLuau:Diagnostic] {\"schema\":1,\"category\":\"%s\",\"status\":%u,\"actual\":%u,\"expected\":0,\"flags\":2}\n", Category, Status, Status);
}
inline void Resources(unsigned Status, unsigned long long Bytes, unsigned long long Limit, unsigned long long Ready)
{
    static unsigned Count = 0;
    if (Count++ >= 24) return;
    std::fprintf(stderr, "[CarbonLuau:Diagnostic] {\"schema\":1,\"category\":\"observation\",\"status\":%u,\"actual\":%llu,\"expected\":%llu,\"flags\":%llu}\n", Status, Bytes, Limit, Ready);
}
inline void Scheduler(unsigned long long Now, unsigned long long Sequence, unsigned long long Queued, unsigned long long Modules)
{
    static unsigned Count = 0;
    if (Count++ >= 24) return;
    std::fprintf(stderr, "[CarbonLuau:Diagnostic] {\"schema\":1,\"category\":\"observation\",\"cutoffNs\":%llu,\"sequence\":%llu,\"queued\":%llu,\"modules\":%llu}\n", Now, Sequence, Queued, Modules);
}
inline void Failure(const char* File, int Line)
{
    // Source paths and arbitrary assertion text remain in the existing console log.
    // Artifacts contain a stable digest and numeric location, never script contents.
    unsigned long long Digest = 1469598103934665603ull;
    for (const unsigned char* P = reinterpret_cast<const unsigned char*>(File); *P; ++P)
        Digest = (Digest ^ *P) * 1099511628211ull;
    const char* Source = File;
    for (const char* P = File; *P; ++P) if (*P == '/' || *P == '\\') Source = P + 1;
    bool Safe = true;
    for (const char* P = Source; *P; ++P) if (!((*P >= 'a' && *P <= 'z') || (*P >= 'A' && *P <= 'Z') || (*P >= '0' && *P <= '9') || *P == '.' || *P == '_' || *P == '-')) Safe = false;
    std::fprintf(stderr, "[CarbonLuau:Diagnostic] {\"schema\":1,\"category\":\"assertion\",\"sourceDigest\":\"%llx\",\"source\":\"%s\",\"line\":%d,\"actual\":0,\"expected\":1}\n", Digest, Safe ? Source : "", Line);
}
inline void Result(unsigned Actual, unsigned Expected, unsigned Flags)
{
    static unsigned Count = 0;
    if (Count++ >= 24 && Actual == Expected) return;
    std::fprintf(stderr, "[CarbonLuau:Diagnostic] {\"schema\":1,\"category\":\"operation\",\"actual\":%u,\"expected\":%u,\"flags\":%u}\n", Actual, Expected, Flags);
}
}
