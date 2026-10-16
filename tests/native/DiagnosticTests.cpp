#include "../shared/NativeDiagnostics.hpp"
#include <cstring>

namespace {
int Location(int Line = __builtin_LINE()) { return Line; }
void Check(bool Value, const char* File = __builtin_FILE(), int Line = __builtin_LINE())
{
    if (!Value) { TestDiagnostics::Failure(File, Line); std::exit(19); }
}
}
int main(int Count, char** Arguments)
{
    if (Count == 2 && std::strcmp(Arguments[1], "--assertion") == 0) Check(false);
    if (Count == 2 && (std::strcmp(Arguments[1], "--primary-cleanup") == 0 || std::strcmp(Arguments[1], "--allocation-cleanup") == 0)) {
        TestDiagnostics::Result(std::strcmp(Arguments[1], "--primary-cleanup") == 0 ? 5 : 4, 0, 1, true);
        TestDiagnostics::CleanupFailure();
        return 19;
    }
    const int Expected = __LINE__ + 1;
    const int Actual = Location();
    Check(Actual == Expected);
    std::puts("[CarbonLuau:Diagnostics] PASS native caller location");
    return 0;
}
