#pragma once
#include "Compiler.hpp"
#include "CompilerProtocol.hpp"
#include "Luau/Compiler.h"
#include <exception>

namespace CarbonLuau::Runtime {
// Shared compiler configuration and bounds. Call only inside a disposable,
// externally supervised compiler/preview process, never a long-lived host.
inline CompileResult CompileBoundedSource(const std::string& Source) try
{
    if (Source.size() > CompilerProtocol::MaximumSourceBytes)
        return {CompileStatus::ProtocolFailure, {}, "source exceeds compiler request bound"};
    Luau::CompileOptions Options;
    Options.optimizationLevel = 1;
    Options.debugLevel = 1;
    std::string Payload = Luau::compile(Source, Options);
    if (Payload.empty() || Payload.size() > CompilerProtocol::MaximumPayloadBytes)
        return {CompileStatus::WorkerFailure, {}, "compiler output exceeded its bound"};
    return {CompileStatus::Success, std::move(Payload), {}};
} catch (const std::exception& Error) {
    return {CompileStatus::WorkerFailure, {}, std::string(Error.what()).substr(0, CompilerProtocol::MaximumPayloadBytes)};
} catch (...) {
    return {CompileStatus::WorkerFailure, {}, "compiler worker exception"};
}
}
