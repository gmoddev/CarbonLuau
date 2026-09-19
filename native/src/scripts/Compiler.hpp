#pragma once
#include <string>

namespace CarbonLuau::Runtime {
enum class CompileStatus { Success, Timeout, WorkerFailure, ProtocolFailure };
struct CompileResult {
    CompileStatus Status = CompileStatus::WorkerFailure;
    std::string Payload;
    std::string Diagnostic;
};

CompileResult CompileSource(const std::string& Source);

#ifdef CARBONLUAU_TESTING
void SetCompilerExecutableForTesting(const std::string& Path);
void ResetCompilerForTesting();
bool CompilerWorkerRunningForTesting();
#endif
}
