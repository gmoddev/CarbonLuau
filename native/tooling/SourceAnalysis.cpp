#include "Luau/Parser.h"
#include "Luau/Ast.h"
#include "../src/semantics/ModulePolicy.hpp"
#include <string>
#include <vector>
#include <cstring>
#include <stdexcept>
#ifdef _WIN32
#define CL_EXPORT extern "C" __declspec(dllexport)
#else
#define CL_EXPORT extern "C" __attribute__((visibility("default")))
#endif

namespace {
std::string Quote(const char* Data, size_t Length)
{
    const char* Hex = "0123456789abcdef";
    std::string Result = "\"";
    for (size_t Index = 0; Index < Length; ++Index) {
        unsigned char C = Data[Index];
        if (C == '"' || C == '\\') { Result += '\\'; Result += char(C); }
        else if (C < 32 || C >= 127) {
            Result += "\\u00"; Result += Hex[C >> 4]; Result += Hex[C & 15];
        } else Result += char(C);
    }
    return Result + "\"";
}
int Copy(const std::string& Value, char* Output, size_t Capacity)
{
    if (!Output || Value.size() >= Capacity || Value.size() > 1024 * 1024) return -2;
    std::memcpy(Output, Value.data(), Value.size()); Output[Value.size()] = 0;
    return int(Value.size());
}
struct Visitor : Luau::AstVisitor {
    std::string Result; unsigned Count = 0, GlobalRequires = 0;
    // AstVisitor skips types by default; typeof(require(...)) has the same
    // native import authority as an expression in an ordinary statement.
    bool visit(Luau::AstType*) override { return true; }
    bool visit(Luau::AstTypePack*) override { return true; }
    bool visit(Luau::AstExprGlobal* Global) override {
        if (!std::strcmp(Global->name.value, "require")) ++GlobalRequires;
        return true;
    }
    bool visit(Luau::AstExprCall* Call) override {
        auto Global = Call->func->as<Luau::AstExprGlobal>();
        if (!Global || std::strcmp(Global->name.value, "require") || Call->self || Call->args.size != 1) return true;
        auto Literal = Call->args.data[0]->as<Luau::AstExprConstantString>();
        if (!Literal) return true;
        if (++Count > 1024) throw std::runtime_error("require count exceeds bound");
        if (!Result.empty()) Result += ',';
        auto Begin = Literal->location.begin, End = Literal->location.end;
        Result += "{\"Name\":" + Quote(Literal->value.data, Literal->value.size) +
            ",\"Line\":" + std::to_string(Begin.line) + ",\"Column\":" + std::to_string(Begin.column) +
            ",\"EndLine\":" + std::to_string(End.line) + ",\"EndColumn\":" + std::to_string(End.column) + "}";
        return true;
    }
};
}

CL_EXPORT int carbonluau_analysis_version() { return 1; }
CL_EXPORT int carbonluau_analysis_source(const char* Source, size_t Length, char* Output, size_t Capacity)
{
    try {
        if (!Source || Length > 65536) return -1;
        Luau::Allocator Allocator;
        Luau::AstNameTable Names(Allocator);
        auto Parsed = Luau::Parser::parse(Source, Length, Names, Allocator);
        Visitor Result;
        if (Parsed.errors.empty() && Parsed.root) Parsed.root->visit(&Result);
        std::string Errors;
        for (const auto& Error : Parsed.errors) {
            if (Errors.size() > 16384) break;
            if (!Errors.empty()) Errors += ',';
            auto Position = Error.getLocation().begin;
            auto Message = Error.getMessage();
            Errors += "{\"Line\":" + std::to_string(Position.line) + ",\"Column\":" +
                std::to_string(Position.column) + ",\"Message\":" + Quote(Message.data(), Message.size()) + "}";
        }
        return Copy("{\"Requires\":[" + Result.Result + "],\"UnsafeRequire\":" +
            (Result.GlobalRequires != Result.Count ? "true" : "false") + ",\"Errors\":[" + Errors + "]}", Output, Capacity);
    } catch (...) { return -3; }
}

CL_EXPORT int carbonluau_analysis_import(const char* Name, size_t Length, int Declared, int Available,
    const char* Main, const char* Exports, size_t ExportLength, char* Output, size_t Capacity)
{
    try {
        if (!Name || !Main || !Exports || Length >= 196 || ExportLength > 65536) return -1;
        std::vector<std::string> Public;
        std::string Values(Exports, ExportLength); size_t Start = 0;
        while (Start < Values.size()) {
            size_t End = Values.find('\n', Start); if (End == std::string::npos) End = Values.size();
            Public.push_back(Values.substr(Start, End - Start)); Start = End + 1;
            if (Public.size() > 256) return -1;
        }
        std::string Logical;
        auto Error = CarbonLuau::Semantics::SelectModule(Name, Length, Declared != 0, Available != 0, Main, Public, Logical);
        return Copy("{\"Code\":" + std::to_string(int(Error)) + ",\"Logical\":" + Quote(Logical.data(), Logical.size()) + "}", Output, Capacity);
    } catch (...) { return -3; }
}
