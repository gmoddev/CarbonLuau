#include "Format.hpp"
#include <algorithm>
#include <cmath>
#include <cstring>
#include <limits>

namespace CarbonLuau::Persistence {
static_assert(std::numeric_limits<double>::is_iec559 && sizeof(double)==8,"storage requires IEEE-754 binary64");
bool ValidText(const std::string& Text, size_t Maximum, bool Name)
{
    if (Text.size() > Maximum || (Name && (Text.empty() || Text == "." || Text == ".."))) return false;
    for (size_t Index = 0; Index < Text.size();) {
        uint32_t Code = uint8_t(Text[Index++]);
        if (!Code || (Name && (Code < 32 || Code == 127 || Code == '/' || Code == '\\' || Code == ':'))) return false;
        if (Code < 128) continue;
        unsigned Count; uint32_t Minimum;
        if (Code >= 0xc2 && Code <= 0xdf) { Count = 1; Minimum = 0x80; Code &= 31; }
        else if (Code >= 0xe0 && Code <= 0xef) { Count = 2; Minimum = 0x800; Code &= 15; }
        else if (Code >= 0xf0 && Code <= 0xf4) { Count = 3; Minimum = 0x10000; Code &= 7; }
        else return false;
        if (Count > Text.size() - Index) return false;
        while (Count--) { uint8_t Next = uint8_t(Text[Index++]); if ((Next & 0xc0) != 0x80) return false; Code = (Code << 6) | (Next & 63); }
        if (Code < Minimum || Code > 0x10ffff || (Code >= 0xd800 && Code <= 0xdfff)) return false;
    }
    return true;
}

bool ValidPackage(const std::string& Package)
{
    if (Package.empty() || Package.size() > 65 || Package == "carbonluau" || Package.rfind("carbonluau.", 0) == 0) return false;
    auto AlphaNumeric = [](unsigned char C) { return (C >= 'a' && C <= 'z') || (C >= '0' && C <= '9'); };
    if (!AlphaNumeric(Package.front()) || !AlphaNumeric(Package.back())) return false;
    unsigned Segments = 1, Length = 0;
    for (size_t I = 0; I < Package.size(); ++I) {
        unsigned char C = Package[I];
        if (C == '.') { if (!Length || Length > 32 || ++Segments > 2 || !AlphaNumeric(Package[I-1]) || I+1 == Package.size() || !AlphaNumeric(Package[I+1])) return false; Length = 0; }
        else { if (!((C >= 'a' && C <= 'z') || (C >= '0' && C <= '9') || C == '_' || C == '-')) return false; ++Length; }
    }
    return Length && Length <= 32;
}
void Validate(const Identity& Id)
{
    Require(Id.Addon ? ValidPackage(Id.Package) : Id.Package.empty());
    Require(ValidText(Id.Store, 64, true) && ValidText(Id.Key, 128, true));
}

namespace {
uint32_t Rotate(uint32_t X, unsigned N) { return (X >> N) | (X << (32 - N)); }
constexpr uint32_t Constants[] = {
    0x428a2f98,0x71374491,0xb5c0fbcf,0xe9b5dba5,0x3956c25b,0x59f111f1,0x923f82a4,0xab1c5ed5,
    0xd807aa98,0x12835b01,0x243185be,0x550c7dc3,0x72be5d74,0x80deb1fe,0x9bdc06a7,0xc19bf174,
    0xe49b69c1,0xefbe4786,0x0fc19dc6,0x240ca1cc,0x2de92c6f,0x4a7484aa,0x5cb0a9dc,0x76f988da,
    0x983e5152,0xa831c66d,0xb00327c8,0xbf597fc7,0xc6e00bf3,0xd5a79147,0x06ca6351,0x14292967,
    0x27b70a85,0x2e1b2138,0x4d2c6dfc,0x53380d13,0x650a7354,0x766a0abb,0x81c2c92e,0x92722c85,
    0xa2bfe8a1,0xa81a664b,0xc24b8b70,0xc76c51a3,0xd192e819,0xd6990624,0xf40e3585,0x106aa070,
    0x19a4c116,0x1e376c08,0x2748774c,0x34b0bcb5,0x391c0cb3,0x4ed8aa4a,0x5b9cca4f,0x682e6ff3,
    0x748f82ee,0x78a5636f,0x84c87814,0x8cc70208,0x90befffa,0xa4506ceb,0xbef9a3f7,0xc67178f2
};
bool Less(const std::string& A, const std::string& B)
{
    return std::lexicographical_compare(A.begin(), A.end(), B.begin(), B.end(),
        [](unsigned char X, unsigned char Y) { return X < Y; });
}
void Text(Bytes& Output, const std::string& Value)
{ Put32(Output, uint32_t(Value.size())); Output.insert(Output.end(), Value.begin(), Value.end()); }
Bytes Bound(const Identity& Id, const Bytes& Envelope, size_t Length)
{
    Bytes Result{uint8_t(Id.Addon)};
    Text(Result, Id.Package); Text(Result, Id.Store); Text(Result, Id.Key);
    Result.insert(Result.end(), Envelope.begin(), Envelope.begin() + Length);
    return Result;
}
struct Encoder {
    Bytes Output;
    Deadline End;
    unsigned Entries = 0;
    std::vector<const Value*> Ancestors;
    void Check() { Require(Clock::now() < End, Error::DeadlineExceeded); Require(Output.size() <= MaximumEnvelope - 44); }
    void Room(size_t Count) { Check(); Require(Count<=MaximumEnvelope-44-Output.size()); }
    void Write(const Value& Item, unsigned Depth)
    {
        Room(1); Output.push_back(uint8_t(Item.Type));
        switch (Item.Type) {
        case Kind::Boolean: Room(1); Output.push_back(Item.Boolean ? 1 : 0); break;
        case Kind::Number: {
            Require(std::isfinite(Item.Number)); uint64_t Bits; static_assert(sizeof(Bits) == sizeof(Item.Number));
            Room(8); std::memcpy(&Bits, &Item.Number, 8); Put64(Output, Bits); break;
        }
        case Kind::String: Require(ValidText(Item.String, 16384)); Room(4+Item.String.size()); Text(Output, Item.String); break;
        case Kind::Array: case Kind::Map: {
            Require(Depth < 16 && std::find(Ancestors.begin(), Ancestors.end(), &Item) == Ancestors.end());
            const size_t Count = Item.Type == Kind::Array ? Item.Array.size() : Item.Map.size();
            Require(Count <= 1024 && Count <= 4096 - Entries && (Item.Type != Kind::Array || Count));
            Entries += unsigned(Count); Room(4); Put32(Output, uint32_t(Count)); Ancestors.push_back(&Item);
            if (Item.Type == Kind::Array) for (const auto& Child : Item.Array) { Require(bool(Child)); Write(*Child, Depth + 1); }
            else {
                std::vector<size_t> Order; Order.reserve(Count);
                for (size_t I = 0; I < Count; ++I) { Require(ValidText(Item.Map[I].first, 128)); Order.push_back(I); }
                std::sort(Order.begin(), Order.end(), [&](size_t A, size_t B) { return Less(Item.Map[A].first, Item.Map[B].first); });
                for (size_t I = 0; I < Count; ++I) {
                    const auto& Pair = Item.Map[Order[I]];
                    Require((I == 0 || Pair.first != Item.Map[Order[I - 1]].first) && bool(Pair.second));
                    Room(4+Pair.first.size()); Text(Output, Pair.first); Write(*Pair.second, Depth + 1);
                }
            }
            Ancestors.pop_back(); break;
        }
        default: throw Failure(Error::InvalidArgument);
        }
        Check();
    }
};
struct Decoder {
    const Bytes& Input;
    size_t Offset, Limit;
    Deadline End;
    unsigned Entries = 0;
    const uint8_t* Take(size_t Count)
    {
        Require(Clock::now() < End, Error::DeadlineExceeded);
        Require(Count <= Limit - Offset, Error::StorageCorrupt);
        const uint8_t* Data = Input.data() + Offset; Offset += Count; return Data;
    }
    std::string String(size_t Maximum)
    {
        uint32_t Length = Read32(Take(4)); Require(Length <= Maximum, Error::StorageCorrupt);
        const auto* Data = Take(Length); std::string Result(reinterpret_cast<const char*>(Data), Length);
        Require(ValidText(Result, Maximum), Error::StorageCorrupt); return Result;
    }
    std::shared_ptr<Value> Read(unsigned Depth)
    {
        auto Item = std::make_shared<Value>(); Item->Type = Kind(*Take(1));
        switch (Item->Type) {
        case Kind::Boolean: { auto Flag = *Take(1); Require(Flag <= 1, Error::StorageCorrupt); Item->Boolean = Flag != 0; break; }
        case Kind::Number: { uint64_t Bits = Read64(Take(8)); std::memcpy(&Item->Number, &Bits, 8); Require(std::isfinite(Item->Number), Error::StorageCorrupt); break; }
        case Kind::String: Item->String = String(16384); break;
        case Kind::Array: case Kind::Map: {
            uint32_t Count = Read32(Take(4));
            Require(Depth < 16 && Count <= 1024 && Count <= 4096 - Entries && (Item->Type != Kind::Array || Count), Error::StorageCorrupt);
            Entries += Count;
            // Do not reserve from an untrusted count before validating child bytes.
            for (uint32_t I = 0; I < Count; ++I) {
                if (Item->Type == Kind::Array) Item->Array.push_back(Read(Depth + 1));
                else {
                    auto Key = String(128);
                    Require(I == 0 || Less(Item->Map.back().first, Key), Error::StorageCorrupt);
                    Item->Map.emplace_back(std::move(Key), Read(Depth + 1));
                }
            }
            break;
        }
        default: throw Failure(Error::StorageCorrupt);
        }
        return Item;
    }
};
}

std::array<uint8_t, 32> Digest(const Bytes& Data)
{
    Require(Data.size() <= MaximumFrame);
    uint32_t State[] = {0x6a09e667,0xbb67ae85,0x3c6ef372,0xa54ff53a,0x510e527f,0x9b05688c,0x1f83d9ab,0x5be0cd19};
    Bytes Padded = Data; Padded.push_back(0x80);
    while (Padded.size() % 64 != 56) Padded.push_back(0);
    for (int Shift = 56; Shift >= 0; Shift -= 8) Padded.push_back(uint8_t((uint64_t(Data.size()) * 8) >> Shift));
    for (size_t Offset = 0; Offset < Padded.size(); Offset += 64) {
        uint32_t Words[64];
        for (unsigned I = 0; I < 16; ++I) { const auto* P = Padded.data() + Offset + I * 4; Words[I] = uint32_t(P[0]) << 24 | uint32_t(P[1]) << 16 | uint32_t(P[2]) << 8 | P[3]; }
        for (unsigned I = 16; I < 64; ++I) {
            uint32_t A = Words[I-15], B = Words[I-2];
            Words[I] = Words[I-16] + (Rotate(A,7)^Rotate(A,18)^(A>>3)) + Words[I-7] + (Rotate(B,17)^Rotate(B,19)^(B>>10));
        }
        uint32_t A=State[0], B=State[1], C=State[2], D=State[3], E=State[4], F=State[5], G=State[6], H=State[7];
        for (unsigned I = 0; I < 64; ++I) {
            uint32_t First = H + (Rotate(E,6)^Rotate(E,11)^Rotate(E,25)) + ((E&F)^(~E&G)) + Constants[I] + Words[I];
            uint32_t Second = (Rotate(A,2)^Rotate(A,13)^Rotate(A,22)) + ((A&B)^(A&C)^(B&C));
            H=G; G=F; F=E; E=D+First; D=C; C=B; B=A; A=First+Second;
        }
        State[0]+=A; State[1]+=B; State[2]+=C; State[3]+=D; State[4]+=E; State[5]+=F; State[6]+=G; State[7]+=H;
    }
    std::array<uint8_t,32> Result{};
    for (unsigned I=0; I<8; ++I) for (unsigned J=0; J<4; ++J) Result[I*4+J] = uint8_t(State[I] >> (24-J*8));
    return Result;
}
Bytes Encode(const Identity& Id, const Value& Item, Deadline End)
{
    Validate(Id); Encoder Writer{{}, End}; Writer.Write(Item, 0);
    Bytes Envelope{'C','L','P','V'}; Put32(Envelope, FormatVersion); Put32(Envelope, uint32_t(Writer.Output.size()));
    Envelope.insert(Envelope.end(), Writer.Output.begin(), Writer.Output.end());
    const auto Hash = Digest(Bound(Id, Envelope, Envelope.size()));
    Envelope.insert(Envelope.end(), Hash.begin(), Hash.end());
    Require(Clock::now() < End, Error::DeadlineExceeded); return Envelope;
}
std::shared_ptr<Value> Decode(const Identity& Id, const Bytes& Envelope, Deadline End)
{
    Validate(Id);
    Require(Envelope.size() >= 45 && Envelope.size() <= MaximumEnvelope && !std::memcmp(Envelope.data(), "CLPV", 4), Error::StorageCorrupt);
    Require(Read32(Envelope.data()+4) == FormatVersion, Error::FormatUnsupported);
    Require(Read32(Envelope.data()+8) == Envelope.size()-44, Error::StorageCorrupt);
    const auto Hash = Digest(Bound(Id, Envelope, Envelope.size()-32));
    Require(std::equal(Hash.begin(), Hash.end(), Envelope.end()-32), Error::StorageCorrupt);
    Decoder Reader{Envelope, 12, Envelope.size()-32, End}; auto Result = Reader.Read(0);
    Require(Reader.Offset == Reader.Limit, Error::StorageCorrupt); return Result;
}
}
