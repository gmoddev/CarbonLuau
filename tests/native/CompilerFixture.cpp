#include "../../native/src/scripts/CompilerProtocol.hpp"

#include <algorithm>
#include <chrono>
#include <cstdint>
#include <cstring>
#include <string>
#include <thread>
#include <vector>

#ifdef _WIN32
#include <fcntl.h>
#include <io.h>
#else
#include <unistd.h>
#define _read read
#define _write write
#endif

#ifndef CARBONLUAU_COMPILER_FIXTURE
#define CARBONLUAU_COMPILER_FIXTURE 0
#endif

namespace {
bool ReadAll(uint8_t* Bytes, size_t Length)
{
    size_t Offset = 0;
    while (Offset < Length) {
        int Count = int(_read(0, Bytes + Offset, unsigned(std::min<size_t>(Length - Offset, 16384))));
        if (Count <= 0) return false;
        Offset += size_t(Count);
    }
    return true;
}

void WriteAll(const uint8_t* Bytes, size_t Length)
{
    size_t Offset = 0;
    while (Offset < Length) {
        int Count = int(_write(1, Bytes + Offset, unsigned(std::min<size_t>(Length - Offset, 16384))));
        if (Count <= 0) return;
        Offset += size_t(Count);
    }
}
}

int main()
{
#ifdef _WIN32
    _setmode(0, _O_BINARY); _setmode(1, _O_BINARY);
#endif
    using namespace CarbonLuau::Runtime;
    std::vector<uint8_t> Request(CompilerProtocol::RequestHeaderSize);
    if (!ReadAll(Request.data(), Request.size())) return 2;
    uint32_t Length = CompilerProtocol::ReadU32(Request.data() + 8);
    uint64_t Nonce = CompilerProtocol::ReadU64(Request.data() + 12);
    std::vector<uint8_t> Source(Length);
    if (Length && !ReadAll(Source.data(), Source.size())) return 3;
#if CARBONLUAU_COMPILER_FIXTURE == 1
    std::this_thread::sleep_for(std::chrono::seconds(10));
#elif CARBONLUAU_COMPILER_FIXTURE == 2
    return 42;
#elif CARBONLUAU_COMPILER_FIXTURE == 3
    const uint8_t Invalid[] = {'B', 'A', 'D', '!'};
    WriteAll(Invalid, sizeof(Invalid));
#elif CARBONLUAU_COMPILER_FIXTURE == 4
    std::vector<uint8_t> Response = CompilerProtocol::Response(Nonce, 0, "payload");
    WriteAll(Response.data(), CompilerProtocol::ResponseHeaderSize + 2);
#elif CARBONLUAU_COMPILER_FIXTURE == 5
    std::vector<uint8_t> Response = CompilerProtocol::Response(Nonce, 0, "payload");
    Response[12] = 1; Response[13] = 0; Response[14] = 0x10; Response[15] = 0;
    WriteAll(Response.data(), CompilerProtocol::ResponseHeaderSize);
#elif CARBONLUAU_COMPILER_FIXTURE == 6
    std::vector<uint8_t> Response = CompilerProtocol::Response(Nonce, 0, "payload");
    Response[24] = Response[24] == '0' ? '1' : '0';
    WriteAll(Response.data(), Response.size());
#elif CARBONLUAU_COMPILER_FIXTURE == 7
    std::vector<uint8_t> Response = CompilerProtocol::Response(Nonce + 1, 0, "payload");
    WriteAll(Response.data(), Response.size());
#elif CARBONLUAU_COMPILER_FIXTURE == 8
    std::vector<uint8_t> Memory(300u * 1024u * 1024u);
    for (size_t Offset = 0; Offset < Memory.size(); Offset += 4096) Memory[Offset] = uint8_t(Offset);
    std::vector<uint8_t> Response = CompilerProtocol::Response(Nonce, 0, "unexpected memory-limit escape");
    WriteAll(Response.data(), Response.size());
#endif
    return 0;
}
