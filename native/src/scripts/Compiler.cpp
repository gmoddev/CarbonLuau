#include "Compiler.hpp"
#include "Luau/Compiler.h"

namespace CarbonLuau::Runtime {
std::string CompileSource(const std::string& Source)
{
    Luau::CompileOptions Options;
    Options.optimizationLevel = 1;
    Options.debugLevel = 1;
    return Luau::compile(Source, Options);
}
} // namespace CarbonLuau::Runtime

