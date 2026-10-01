#include "Sortable.hpp"
#include <cmath>
#include <cstring>
#include <limits>

namespace CarbonLuau::Persistence {
namespace {
constexpr size_t MaximumScalarString = 1024;
constexpr uint64_t SignBit = uint64_t(1) << 63;
static_assert(std::numeric_limits<double>::is_iec559 && sizeof(double) == 8 &&
    std::numeric_limits<double>::digits == 53, "sort keys require IEEE-754 binary64");
}

bool ValidField(const std::string& Field)
{
    // Store/key name restrictions do not apply to literal map fields.
    return !Field.empty() && ValidText(Field, 64);
}

Bytes SortScalar(const Value& Scalar)
{
    switch (Scalar.Type) {
    case Kind::Boolean:
        return {uint8_t(Kind::Boolean), uint8_t(Scalar.Boolean ? 1 : 0)};
    case Kind::Number: {
        Require(std::isfinite(Scalar.Number));
        uint64_t Bits;
        std::memcpy(&Bits, &Scalar.Number, sizeof(Bits));
        // Canonicalize only the derived key; preserve the stored signed zero.
        if ((Bits & ~SignBit) == 0) Bits = 0;
        Bits = (Bits & SignBit) ? ~Bits : (Bits ^ SignBit);
        Bytes Result(9);
        Result[0] = uint8_t(Kind::Number);
        for (unsigned Index = 0; Index < 8; ++Index)
            Result[Index + 1] = uint8_t(Bits >> (56 - 8 * Index));
        return Result;
    }
    case Kind::String: {
        Require(ValidText(Scalar.String, MaximumScalarString));
        Bytes Result;
        Result.reserve(1 + Scalar.String.size());
        Result.push_back(uint8_t(Kind::String));
        Result.insert(Result.end(), Scalar.String.begin(), Scalar.String.end());
        return Result;
    }
    default:
        throw Failure(Error::InvalidArgument);
    }
}

ExtractedScalar ExtractScalar(const Value& Root, const std::string& Field)
{
    Require(ValidField(Field));
    if (Root.Type != Kind::Map) return {};
    // Decode already enforces this count. Keep the scan bounded even if an
    // internal caller accidentally passes an unqualified in-memory map.
    Require(Root.Map.size() <= 1024);
    for (const auto& Entry : Root.Map) {
        if (Entry.first != Field) continue;
        Require(bool(Entry.second));
        const auto& Scalar = *Entry.second;
        if (Scalar.Type == Kind::Array || Scalar.Type == Kind::Map) return {};
        ExtractedScalar Result;
        Result.Type = Scalar.Type;
        Result.Represented = true;
        if (Scalar.Type == Kind::String && Scalar.String.size() > MaximumScalarString) {
            // Legal primary strings can exceed the derived limit. Invalid
            // primary text is still an error, never disguised as unavailable.
            Require(ValidText(Scalar.String, 16384));
            return Result;
        }
        Result.SortKey = SortScalar(Scalar);
        Result.Usable = true;
        return Result;
    }
    return {};
}
}
