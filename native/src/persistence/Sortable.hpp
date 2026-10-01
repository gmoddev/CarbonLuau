#pragma once

// Private Persistence-2A derived representation, not a public ABI or API.
#include "Format.hpp"

namespace CarbonLuau::Persistence {
struct ExtractedScalar {
    Kind Type = Kind::Map;
    Bytes SortKey;
    bool Represented = false;
    bool Usable = false;
};

// Exact 1..64 UTF-8 bytes without NUL; punctuation is literal.
bool ValidField(const std::string& Field);

// Kind tag followed by one boolean byte, eight big-endian sortable binary64
// bytes, or exact string bytes (at most 1024). Invalid input throws Failure.
// Ordering is defined within one Kind only. Primary number bits are unchanged.
Bytes SortScalar(const Value& Scalar);

// Root must come from the qualified Foundation 1 codec. Inspect only the named
// top-level field, never descendants. Ordinary nonparticipating shapes return
// the default result. An oversized string is represented but unusable, with an
// empty SortKey; callers must retain that evidence of incomplete string state.
ExtractedScalar ExtractScalar(const Value& Root, const std::string& Field);
}
