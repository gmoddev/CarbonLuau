#include "CompilerProtocol.hpp"
#include "Luau/Compiler.h"

#include <algorithm>
#include <cstdint>
#include <cstdio>
#include <exception>
#include <string>
#include <vector>

#ifdef _WIN32
#include <fcntl.h>
#include <io.h>
#else
#include <unistd.h>
#define _read read
#define _write write
#endif

namespace {
bool ReadAll(uint8_t* Bytes, size_t Length)
{
    size_t Offset = 0;
    while (Offset < Length) {
        int Read = int(_read(0, Bytes + Offset, unsigned(std::min<size_t>(Length - Offset, 16384))));
        if (Read <= 0) return false;
        Offset += size_t(Read);
    }
    return true;
}

bool WriteAll(const uint8_t* Bytes, size_t Length)
{
    size_t Offset = 0;
    while (Offset < Length) {
        int Written = int(_write(1, Bytes + Offset, unsigned(std::min<size_t>(Length - Offset, 16384))));
        if (Written <= 0) return false;
        Offset += size_t(Written);
    }
    return true;
}
}

int main()
{
#ifdef _WIN32
    _setmode(0, _O_BINARY); _setmode(1, _O_BINARY);
#endif
    using namespace CarbonLuau::Runtime;
    for (;;) {
        std::vector<uint8_t> Header(CompilerProtocol::RequestHeaderSize);
        int First = int(_read(0, Header.data(), 1));
        if (First == 0) return 0;
        if (First != 1 || !ReadAll(Header.data() + 1, Header.size() - 1)) return 2;
        uint64_t Nonce = CompilerProtocol::ReadU64(Header.data() + 12);
        uint32_t SourceLength = CompilerProtocol::ReadU32(Header.data() + 8);
        if (std::memcmp(Header.data(), "CLCQ", 4) || CompilerProtocol::ReadU32(Header.data() + 4) != CompilerProtocol::Version ||
            SourceLength > CompilerProtocol::MaximumSourceBytes ||
            std::memcmp(Header.data() + 20, CARBONLUAU_REVISION, CompilerProtocol::RevisionLength))
            return 3;
        std::string Source(SourceLength, '\0');
        if (SourceLength && !ReadAll(reinterpret_cast<uint8_t*>(Source.data()), Source.size())) return 4;
        uint32_t Status = 0;
        std::string Payload;
        try {
            Luau::CompileOptions Options;
            Options.optimizationLevel = 1;
            Options.debugLevel = 1;
            Payload = Luau::compile(Source, Options);
            if (Payload.empty() || Payload.size() > CompilerProtocol::MaximumPayloadBytes) {
                Status = 1; Payload = "compiler output exceeded its bound";
            }
        } catch (const std::exception& Error) {
            Status = 1; Payload = Error.what();
        } catch (...) {
            Status = 1; Payload = "compiler worker exception";
        }
        if (Payload.size() > CompilerProtocol::MaximumPayloadBytes) Payload.resize(CompilerProtocol::MaximumPayloadBytes);
        std::vector<uint8_t> Response = CompilerProtocol::Response(Nonce, Status, Payload);
        if (!WriteAll(Response.data(), Response.size())) return 5;
    }
}
