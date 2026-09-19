#pragma once

#include <cstddef>
#include <cstdint>
#include <cstring>
#include <string>
#include <vector>

namespace CarbonLuau::Runtime::CompilerProtocol {
constexpr uint32_t Version = 1;
constexpr size_t RevisionLength = 40;
constexpr size_t RequestHeaderSize = 4 + 4 + 4 + 8 + RevisionLength;
constexpr size_t ResponseHeaderSize = 4 + 4 + 4 + 4 + 8 + RevisionLength;
constexpr uint32_t MaximumSourceBytes = 64 * 1024;
constexpr uint32_t MaximumPayloadBytes = 1024 * 1024;
constexpr uint32_t DeadlineMilliseconds = 1000;
constexpr uint64_t WorkerMemoryBytes = 256ull * 1024ull * 1024ull;

inline void AppendU32(std::vector<uint8_t>& Bytes, uint32_t Value)
{
    for (unsigned Shift = 0; Shift < 32; Shift += 8) Bytes.push_back(uint8_t(Value >> Shift));
}

inline void AppendU64(std::vector<uint8_t>& Bytes, uint64_t Value)
{
    for (unsigned Shift = 0; Shift < 64; Shift += 8) Bytes.push_back(uint8_t(Value >> Shift));
}

inline uint32_t ReadU32(const uint8_t* Bytes)
{
    uint32_t Value = 0;
    for (unsigned Shift = 0; Shift < 32; Shift += 8) Value |= uint32_t(Bytes[Shift / 8]) << Shift;
    return Value;
}

inline uint64_t ReadU64(const uint8_t* Bytes)
{
    uint64_t Value = 0;
    for (unsigned Shift = 0; Shift < 64; Shift += 8) Value |= uint64_t(Bytes[Shift / 8]) << Shift;
    return Value;
}

inline std::vector<uint8_t> Request(uint64_t Nonce, const std::string& Source)
{
    std::vector<uint8_t> Bytes;
    Bytes.reserve(RequestHeaderSize + Source.size());
    Bytes.insert(Bytes.end(), {'C', 'L', 'C', 'Q'});
    AppendU32(Bytes, Version);
    AppendU32(Bytes, uint32_t(Source.size()));
    AppendU64(Bytes, Nonce);
    Bytes.insert(Bytes.end(), CARBONLUAU_REVISION, CARBONLUAU_REVISION + RevisionLength);
    Bytes.insert(Bytes.end(), Source.begin(), Source.end());
    return Bytes;
}

inline std::vector<uint8_t> Response(uint64_t Nonce, uint32_t Status, const std::string& Payload)
{
    std::vector<uint8_t> Bytes;
    Bytes.reserve(ResponseHeaderSize + Payload.size());
    Bytes.insert(Bytes.end(), {'C', 'L', 'C', 'R'});
    AppendU32(Bytes, Version);
    AppendU32(Bytes, Status);
    AppendU32(Bytes, uint32_t(Payload.size()));
    AppendU64(Bytes, Nonce);
    Bytes.insert(Bytes.end(), CARBONLUAU_REVISION, CARBONLUAU_REVISION + RevisionLength);
    Bytes.insert(Bytes.end(), Payload.begin(), Payload.end());
    return Bytes;
}
} // namespace CarbonLuau::Runtime::CompilerProtocol
