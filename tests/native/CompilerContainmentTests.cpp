#include "../../native/src/scripts/Compiler.hpp"
#include "Luau/Compiler.h"

#include <chrono>
#include <cstdio>
#include <cstdlib>
#include <string>

using namespace CarbonLuau::Runtime;

namespace {
void Check(bool Condition, const char* Message)
{
    if (!Condition) { std::fprintf(stderr, "[CarbonLuau:CompilerTest] FAIL: %s\n", Message); std::exit(1); }
}

double CompileMilliseconds(const char* Label, const std::string& Source, unsigned Count)
{
    auto Start = std::chrono::steady_clock::now();
    for (unsigned Index = 0; Index < Count; ++Index) {
        CompileResult Result = CompileSource(Source);
        if (Result.Status != CompileStatus::Success) {
            std::fprintf(stderr, "[CarbonLuau:CompilerTest] %s failed: status=%d diagnostic=%s\n",
                Label, int(Result.Status), Result.Diagnostic.c_str());
            Check(false, "qualification compile");
        }
    }
    return std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now() - Start).count() / Count;
}

double DirectMilliseconds(const std::string& Source, unsigned Count)
{
    Luau::CompileOptions Options;
    Options.optimizationLevel = 1; Options.debugLevel = 1;
    auto Start = std::chrono::steady_clock::now();
    for (unsigned Index = 0; Index < Count; ++Index) Check(!Luau::compile(Source, Options).empty(), "direct qualification compile");
    return std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now() - Start).count() / Count;
}
}

int main(int ArgumentCount, char** Arguments)
{
    Check(ArgumentCount == 10, "worker and eight fixture paths required");
    SetCompilerExecutableForTesting(Arguments[1]);
    auto ColdStart = std::chrono::steady_clock::now();
    CompileResult Normal = CompileSource("return 42");
    double ColdStartMs = std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now() - ColdStart).count();
    Check(Normal.Status == CompileStatus::Success && !Normal.Payload.empty() && Normal.Payload[0] != 0, "normal worker compile");
    Check(CompilerWorkerRunningForTesting(), "persistent worker remains available");
    CompileResult Syntax = CompileSource("local =");
    Check(Syntax.Status == CompileStatus::Success && !Syntax.Payload.empty() && Syntax.Payload[0] == 0, "syntax failure remains compiler payload");

    std::string MaximumStatements;
    MaximumStatements = "local A=0\n";
    while (MaximumStatements.size() + 12 < 65536) MaximumStatements += "A=A+1\n";
    MaximumStatements += "return A\n";
    std::string MaximumTable = "return {";
    for (unsigned Index = 0;; ++Index) {
        std::string Field = "Field" + std::to_string(Index) + "=" + std::to_string(Index) + ",";
        if (MaximumTable.size() + Field.size() + 2 > 65536) break;
        MaximumTable += Field;
    }
    MaximumTable += "}\n";
    std::string MaximumTypes;
    for (unsigned Index = 0;; ++Index) {
        std::string Alias = "type T" + std::to_string(Index) + "={Value:number}\n";
        if (MaximumTypes.size() + Alias.size() + 12 > 65536) break;
        MaximumTypes += Alias;
    }
    MaximumTypes += "return true\n";
    Check(MaximumStatements.size() <= 65536 && MaximumTable.size() <= 65536 && MaximumTypes.size() <= 65536,
        "pathological corpus respects source bound");
    Check(CompileSource(std::string(65537, 'a')).Status == CompileStatus::ProtocolFailure,
        "oversized source request rejected before transport");
    double NormalMs = CompileMilliseconds("normal", "return 42", 20);
    double StatementsMs = CompileMilliseconds("statements", MaximumStatements, 5);
    double TableMs = CompileMilliseconds("table", MaximumTable, 5);
    double TypesMs = CompileMilliseconds("types", MaximumTypes, 5);
    double DirectNormalMs = DirectMilliseconds("return 42", 20);
    double DirectStatementsMs = DirectMilliseconds(MaximumStatements, 5);
    double DirectTableMs = DirectMilliseconds(MaximumTable, 5);
    double DirectTypesMs = DirectMilliseconds(MaximumTypes, 5);
    std::printf("[CarbonLuau:CompilerTest] isolated cold=%.3f ms; warm normal=%.3f ms; statements=%.3f ms/%zu bytes; table=%.3f ms/%zu bytes; types=%.3f ms/%zu bytes\n",
        ColdStartMs, NormalMs, StatementsMs, MaximumStatements.size(), TableMs, MaximumTable.size(), TypesMs, MaximumTypes.size());
    std::printf("[CarbonLuau:CompilerTest] direct baseline normal=%.3f ms; statements=%.3f ms; table=%.3f ms; types=%.3f ms\n",
        DirectNormalMs, DirectStatementsMs, DirectTableMs, DirectTypesMs);

    SetCompilerExecutableForTesting(std::string(Arguments[1]) + ".missing");
    Check(CompileSource("return 1").Status == CompileStatus::WorkerFailure, "missing worker fails closed");
    SetCompilerExecutableForTesting(Arguments[1]);
    Check(CompileSource("return 2").Status == CompileStatus::Success, "missing-worker recovery");

    double TimeoutMs = 0;
    for (int Cycle = 0; Cycle < 3; ++Cycle) {
        auto TimeoutStart = std::chrono::steady_clock::now();
        SetCompilerExecutableForTesting(Arguments[2]);
        CompileResult Timeout = CompileSource("return 1");
        double Elapsed = std::chrono::duration<double, std::milli>(std::chrono::steady_clock::now() - TimeoutStart).count();
        if (!Cycle) TimeoutMs = Elapsed;
        Check(Timeout.Status == CompileStatus::Timeout && Elapsed >= 900 && Elapsed < 1600, "killable wall deadline");
        Check(!CompilerWorkerRunningForTesting(), "timed-out worker was terminated");
        SetCompilerExecutableForTesting(Arguments[1]);
        Check(CompileSource("return 2").Status == CompileStatus::Success, "post-timeout worker recovery");
    }

    const CompileStatus Expected[] = {
        CompileStatus::WorkerFailure, CompileStatus::WorkerFailure, CompileStatus::ProtocolFailure,
        CompileStatus::ProtocolFailure, CompileStatus::ProtocolFailure, CompileStatus::ProtocolFailure
    };
    for (int Index = 3; Index < 9; ++Index) {
        SetCompilerExecutableForTesting(Arguments[Index]);
        Check(CompileSource("return 1").Status == Expected[Index - 3], "crash/malformed/truncated/oversized/mismatched response rejection");
        SetCompilerExecutableForTesting(Arguments[1]);
        Check(CompileSource("return 2").Status == CompileStatus::Success, "clean worker recovery");
    }
    for (int Cycle = 0; Cycle < 20; ++Cycle) {
        SetCompilerExecutableForTesting(Arguments[3]);
        Check(CompileSource("return 1").Status == CompileStatus::WorkerFailure, "repeated crash containment");
        SetCompilerExecutableForTesting(Arguments[1]);
        Check(CompileSource("return 2").Status == CompileStatus::Success, "repeated restart recovery");
    }
#ifndef CARBONLUAU_SANITIZE
    SetCompilerExecutableForTesting(Arguments[9]);
    Check(CompileSource("return 1").Status == CompileStatus::WorkerFailure,
        "worker memory limit terminates an oversized allocation");
    Check(!CompilerWorkerRunningForTesting(), "memory-limited worker was reclaimed");
    SetCompilerExecutableForTesting(Arguments[1]);
    Check(CompileSource("return 2").Status == CompileStatus::Success, "post-memory-limit worker recovery");
#endif
    for (int Cycle = 0; Cycle < 500; ++Cycle)
        Check(CompileSource("return 3").Status == CompileStatus::Success, "persistent worker stress");
    ResetCompilerForTesting();
    Check(!CompilerWorkerRunningForTesting(), "deterministic worker teardown");
    std::printf("[CarbonLuau:CompilerTest] PASS timeout=%.3f ms; 3 timeout recoveries; crash/protocol/provenance/stale rejection; 20 restart cycles; 500-request stress; 256 MiB worker limit%s\n",
        TimeoutMs,
#ifdef CARBONLUAU_SANITIZE
        " skipped under sanitizer virtual-address requirements"
#else
        " enforced and recovered"
#endif
    );
}
