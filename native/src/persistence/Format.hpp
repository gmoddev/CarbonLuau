#pragma once

// Private Persistence-1A format. Not a public ABI or scripting capability.
#include <array>
#include <chrono>
#include <cstdint>
#include <memory>
#include <stdexcept>
#include <string>
#include <utility>
#include <vector>

namespace CarbonLuau::Persistence {
using Bytes = std::vector<uint8_t>;
using Clock = std::chrono::steady_clock;
using Deadline = Clock::time_point;
constexpr size_t MaximumEnvelope = 65536;
constexpr size_t MaximumFrame = 68 * 1024;
constexpr uint32_t FormatVersion = 1;

enum class Error : uint32_t {
    None, InvalidArgument, QuotaExceeded, StorageUnavailable, StorageBusy,
    StorageFull, StorageCorrupt, FormatUnsupported, DeadlineExceeded,
    StorageError, Indeterminate
};
struct Failure : std::runtime_error {
    Error Code;
    explicit Failure(Error Code) : std::runtime_error("[CarbonLuau:Persistence] controlled failure"), Code(Code) {}
};
inline void Require(bool Condition, Error Code = Error::InvalidArgument)
{ if (!Condition) throw Failure(Code); }

struct Identity {
    // Root has an empty Package; Addon has a canonical host-selected package ID.
    bool Addon = false;
    std::string Package, Store, Key;
};
enum class Kind : uint8_t { Boolean = 1, Number = 2, String = 3, Array = 4, Map = 5 };
struct Value {
    Kind Type = Kind::Map;
    bool Boolean = false;
    double Number = 0;
    std::string String;
    std::vector<std::shared_ptr<Value>> Array;
    std::vector<std::pair<std::string, std::shared_ptr<Value>>> Map;
};

bool ValidText(const std::string& Text, size_t Maximum, bool Name = false);
bool ValidPackage(const std::string& Package);
void Validate(const Identity& Identity);
std::array<uint8_t, 32> Digest(const Bytes& Data);
Bytes Encode(const Identity& Identity, const Value& Value, Deadline End);
std::shared_ptr<Value> Decode(const Identity& Identity, const Bytes& Envelope, Deadline End);

inline void Put32(Bytes& Output, uint32_t Value)
{ for (unsigned Shift = 0; Shift < 32; Shift += 8) Output.push_back(uint8_t(Value >> Shift)); }
inline void Put64(Bytes& Output, uint64_t Value)
{ for (unsigned Shift = 0; Shift < 64; Shift += 8) Output.push_back(uint8_t(Value >> Shift)); }
inline uint32_t Read32(const uint8_t* Data)
{ return uint32_t(Data[0]) | uint32_t(Data[1]) << 8 | uint32_t(Data[2]) << 16 | uint32_t(Data[3]) << 24; }
inline uint64_t Read64(const uint8_t* Data)
{ return uint64_t(Read32(Data)) | uint64_t(Read32(Data + 4)) << 32; }
}
