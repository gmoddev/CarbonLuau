#include "Sortable.hpp"
#include <algorithm>
#include <cmath>
#include <cstdio>
#include <cstring>
#include <limits>

using namespace CarbonLuau::Persistence;
namespace {
void Check(bool Condition, const char* Message)
{
    if (!Condition) throw std::runtime_error(Message);
}
template<class F> void Reject(F Action)
{
    try { Action(); }
    catch (const Failure& Problem) {
        Check(Problem.Code == Error::InvalidArgument, "wrong controlled failure");
        return;
    }
    throw std::runtime_error("expected controlled failure");
}
Value Number(double Input)
{
    Value Result; Result.Type = Kind::Number; Result.Number = Input; return Result;
}
Value Text(const std::string& Input)
{
    Value Result; Result.Type = Kind::String; Result.String = Input; return Result;
}
Value Boolean(bool Input)
{
    Value Result; Result.Type = Kind::Boolean; Result.Boolean = Input; return Result;
}
double FromBits(uint64_t Bits)
{
    double Result; std::memcpy(&Result, &Bits, sizeof(Result)); return Result;
}
std::shared_ptr<Value> Qualified(const Value& Input)
{
    const Identity Id{false, "", "Sortable", "Record"};
    const auto End = Clock::now() + std::chrono::seconds(5);
    return Decode(Id, Encode(Id, Input, End), End);
}
ExtractedScalar Extract(const Value& Scalar)
{
    Value Root; Root.Map.emplace_back("Field", std::make_shared<Value>(Scalar));
    return ExtractScalar(*Qualified(Root), "Field");
}
void Absent(const ExtractedScalar& Result)
{
    Check(!Result.Represented && !Result.Usable && Result.SortKey.empty() &&
        Result.Type == Kind::Map, "nonparticipating shape");
}
bool ByteLess(const std::string& Left, const std::string& Right)
{
    return std::lexicographical_compare(Left.begin(), Left.end(), Right.begin(), Right.end(),
        [](unsigned char A, unsigned char B) { return A < B; });
}

void Numbers()
{
    Check(SortScalar(Number(-1)) == Bytes({2, 0x40, 0x0f, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff}), "negative golden key");
    Check(SortScalar(Number(1)) == Bytes({2, 0xbf, 0xf0, 0, 0, 0, 0, 0, 0}), "positive golden key");
    Check(SortScalar(Number(-0.0)) == Bytes({2, 0x80, 0, 0, 0, 0, 0, 0, 0}), "zero golden key");

    std::vector<double> Corpus{0.0, -0.0, 1, -1, 0.1, -0.1,
        std::numeric_limits<double>::denorm_min(), -std::numeric_limits<double>::denorm_min(),
        std::numeric_limits<double>::min(), -std::numeric_limits<double>::min(),
        std::numeric_limits<double>::max(), -std::numeric_limits<double>::max(),
        9007199254740991.0, 9007199254740992.0, 9007199254740994.0};
    for (double Numeric : Corpus) {
        const auto Decoded = Qualified(Number(Numeric));
        const auto Result = Extract(*Decoded);
        Check(Result.Represented && Result.Usable && Result.Type == Kind::Number &&
            Result.SortKey == SortScalar(Number(Numeric)), "decoded numeric boundary extraction");
        Check(!std::memcmp(&Decoded->Number, &Numeric, sizeof(Numeric)), "primary numeric boundary bits");
    }
    // Both signs, every finite exponent, and adjacent mantissa boundaries.
    for (uint64_t Exponent = 0; Exponent < 2047; ++Exponent)
        for (uint64_t Mantissa : {0ull, 1ull, 0x0007ffffffffffffull, 0x000ffffffffffffeull, 0x000fffffffffffffull})
            for (uint64_t Sign : {0ull, 0x8000000000000000ull})
                Corpus.push_back(FromBits(Sign | (Exponent << 52) | Mantissa));
    uint64_t Random = 0x123456789abcdef0ull;
    for (unsigned Index = 0; Index < 20000; ++Index) {
        Random ^= Random << 13; Random ^= Random >> 7; Random ^= Random << 17;
        if ((Random & 0x7ff0000000000000ull) != 0x7ff0000000000000ull)
            Corpus.push_back(FromBits(Random));
    }
    struct Sample { double Numeric; Bytes Key; };
    std::vector<Sample> Encoded;
    for (double Numeric : Corpus) {
        auto Scalar = Number(Numeric);
        auto Key = SortScalar(Scalar);
        Check(!std::memcmp(&Scalar.Number, &Numeric, sizeof(Numeric)), "number input mutated");
        Check(Key.size() == 9 && Key[0] == uint8_t(Kind::Number), "number key width/tag");
        Encoded.push_back({Numeric, std::move(Key)});
    }
    // Reference comparisons are binary64 < and ==, as in the managed/Luau
    // finite-number model; they do not reuse the sortable bit transform.
    for (size_t Index = 1; Index < Encoded.size(); ++Index) {
        const auto& A = Encoded[Index - 1]; const auto& B = Encoded[Index];
        Check((A.Numeric < B.Numeric) == (A.Key < B.Key), "corpus pair ordering");
        Check((A.Numeric == B.Numeric) == (A.Key == B.Key), "corpus pair equality");
    }
    std::sort(Corpus.begin(), Corpus.end());
    std::sort(Encoded.begin(), Encoded.end(), [](const Sample& A, const Sample& B) { return A.Key < B.Key; });
    for (size_t Index = 0; Index < Corpus.size(); ++Index) {
        Check(Corpus[Index] == Encoded[Index].Numeric, "numeric sort disagrees with key sort");
        if (Index != 0)
            Check((Corpus[Index - 1] == Corpus[Index]) == (Encoded[Index - 1].Key == Encoded[Index].Key), "sorted equality");
    }
    const auto Zero = Qualified(Number(-0.0));
    Check(std::signbit(Zero->Number), "codec lost negative zero");
    Check(SortScalar(*Zero) == SortScalar(Number(0.0)) && std::signbit(Zero->Number), "derived zero changed primary");
    for (uint64_t Bits : {0x7ff0000000000000ull, 0xfff0000000000000ull,
        0x7ff8000000000001ull, 0xfff8000000000001ull, 0x7ff0000000000001ull}) {
        Reject([&] { SortScalar(Number(FromBits(Bits))); });
        Value Root; Root.Map.emplace_back("Field", std::make_shared<Value>(Number(FromBits(Bits))));
        Reject([&] { ExtractScalar(Root, "Field"); });
    }
}

void StringsAndFields()
{
    const std::vector<std::string> Strings{"", "\x01", "A", "AA", "Z", "a", "aA", "aa",
        "e\xcc\x81", "\x7f", "\xc2\x80", "\xc3\xa9", "\xdf\xbf", "\xe0\xa0\x80",
        "\xee\x80\x80", "\xef\xbf\xbf", "\xf0\x90\x80\x80", "\xf0\x9f\x98\x80", "\xf4\x8f\xbf\xbf"};
    for (const auto& Left : Strings) {
        const auto Scalar = Qualified(Text(Left));
        Bytes Expected{3}; Expected.insert(Expected.end(), Left.begin(), Left.end());
        Check(SortScalar(*Scalar) == Expected, "string bytes changed");
        for (const auto& Right : Strings) {
            Check((SortScalar(*Scalar) < SortScalar(Text(Right))) == ByteLess(Left, Right), "UTF-8 byte order");
            Check((SortScalar(*Scalar) == SortScalar(Text(Right))) == (Left == Right), "string equality");
        }
    }
    for (const auto& Field : {std::string("."), std::string(".."), std::string("a/b\\c:d.e"),
        std::string("'\";--"), std::string("\x01\x7f"), std::string(64, 'x'), std::string(62, 'x') + "\xc3\xa9"})
        Check(ValidField(Field), "legal literal field rejected");
    for (const auto& Invalid : {std::string(), std::string(65, 'x'), std::string(63, 'x') + "\xc3\xa9"}) {
        Check(!ValidField(Invalid), "invalid field size accepted");
        Reject([&] { ExtractScalar(Value{}, Invalid); });
    }
    for (const auto& Invalid : {std::string("a\0b", 3), std::string("\0", 1), std::string("\x80"),
        std::string("\xc0\x80"), std::string("\xc1\xbf"), std::string("\xc2"), std::string("\xc2x"),
        std::string("\xe0\x80\x80"), std::string("\xed\xa0\x80"), std::string("\xf0\x80\x80\x80"),
        std::string("\xf4\x90\x80\x80"), std::string("\xf5\x80\x80\x80"), std::string("\xff")}) {
        Check(!ValidField(Invalid), "invalid UTF-8 field accepted");
        Reject([&] { SortScalar(Text(Invalid)); });
        Reject([&] { ExtractScalar(Value{}, Invalid); });
        Value Root; Root.Map.emplace_back("Field", std::make_shared<Value>(Text(Invalid)));
        Reject([&] { ExtractScalar(Root, "Field"); });
    }
    for (size_t Length : {size_t(0), size_t(1023), size_t(1024), size_t(1025), size_t(16384)}) {
        const auto Scalar = Text(std::string(Length, 'x'));
        const auto Result = Extract(Scalar);
        Check(Result.Type == Kind::String && Result.Represented, "string category lost");
        Check(Result.Usable == (Length <= 1024), "string availability bound");
        if (Length <= 1024) Check(Result.SortKey == SortScalar(Scalar), "bounded string key");
        else {
            Check(Result.SortKey.empty(), "oversized string truncated");
            Reject([&] { SortScalar(Scalar); });
        }
    }
    std::string Multibyte;
    for (unsigned Index = 0; Index < 256; ++Index) Multibyte += "\xf0\x9f\x98\x80";
    Check(Extract(Text(Multibyte)).SortKey.size() == 1025, "UTF-8 byte boundary");
    Multibyte += 'x';
    const auto Oversized = Extract(Text(Multibyte));
    Check(Oversized.Represented && !Oversized.Usable && Oversized.SortKey.empty(), "UTF-8 bound is bytes");
    for (const auto& Invalid : {std::string(16385, 'x'), std::string(1024, 'x') + "\xc0\x80"}) {
        Value Root; Root.Map.emplace_back("Field", std::make_shared<Value>(Text(Invalid)));
        Reject([&] { ExtractScalar(Root, "Field"); });
    }
}

void Shapes()
{
    Absent(ExtractScalar(*Qualified(Value{}), "Field"));
    for (const auto& Scalar : {Boolean(false), Boolean(true), Number(-1), Text("value")}) {
        Absent(ExtractScalar(*Qualified(Scalar), "Field"));
        const auto Result = Extract(Scalar);
        Check(Result.Represented && Result.Usable && Result.Type == Scalar.Type &&
            Result.SortKey == SortScalar(Scalar), "scalar extraction");
    }
    Check(SortScalar(Boolean(false)) == Bytes({1, 0}), "false encoding");
    Check(SortScalar(Boolean(true)) == Bytes({1, 1}), "true encoding");
    Check(SortScalar(Boolean(false)) < SortScalar(Boolean(true)), "boolean order");
    Value Array; Array.Type = Kind::Array; Array.Array.push_back(std::make_shared<Value>(Number(1)));
    Absent(ExtractScalar(*Qualified(Array), "Field"));
    Absent(Extract(Array)); Absent(Extract(Value{}));
    Reject([&] { SortScalar(Array); }); Reject([&] { SortScalar(Value{}); });
    Value Invalid; Invalid.Type = Kind(99); Reject([&] { SortScalar(Invalid); });
    Value InvalidRoot; InvalidRoot.Map.emplace_back("Field", std::make_shared<Value>(Invalid));
    Reject([&] { ExtractScalar(InvalidRoot, "Field"); });
    Value Root;
    Root.Map = {{"Field", std::make_shared<Value>(Number(1))},
        {"field", std::make_shared<Value>(Text("different"))},
        {"a.b", std::make_shared<Value>(Boolean(true))},
        {"a", std::make_shared<Value>()},
        {"e\xcc\x81", std::make_shared<Value>(Number(2))},
        {"\xc3\xa9", std::make_shared<Value>(Number(3))},
        {std::string(65, 'x'), std::make_shared<Value>(Number(4))}};
    Root.Map[3].second->Map.emplace_back("b", std::make_shared<Value>(Number(99)));
    const auto Decoded = Qualified(Root);
    Check(ExtractScalar(*Decoded, "Field").Type == Kind::Number, "exact uppercase field");
    Check(ExtractScalar(*Decoded, "field").Type == Kind::String, "exact lowercase field");
    Check(ExtractScalar(*Decoded, "a.b").SortKey == SortScalar(Boolean(true)), "punctuation must be literal");
    Check(ExtractScalar(*Decoded, "e\xcc\x81").SortKey != ExtractScalar(*Decoded, "\xc3\xa9").SortKey, "field normalization");
    Absent(ExtractScalar(*Decoded, "b")); Absent(ExtractScalar(*Decoded, "FIELD"));
    Reject([&] { ExtractScalar(*Decoded, std::string(65, 'x')); });
    Value Wide;
    for (unsigned Index = 0; Index < 1024; ++Index)
        Wide.Map.emplace_back("Key" + std::to_string(Index), std::make_shared<Value>(Number(Index)));
    const auto Full = Qualified(Wide);
    Check(ExtractScalar(*Full, "Key999").SortKey == SortScalar(Number(999)), "maximum map extraction");
    Absent(ExtractScalar(*Full, "Missing"));
    Wide.Map.emplace_back("Overflow", std::make_shared<Value>());
    Reject([&] { ExtractScalar(Wide, "Missing"); });
    Value Null; Null.Map.emplace_back("Field", nullptr);
    Reject([&] { ExtractScalar(Null, "Field"); });

    // Equal scalar keys leave the record key as an independent exact-byte
    // tie-breaker; reversing the pair order reverses both components.
    using Row = std::pair<Bytes, std::string>;
    std::vector<Row> Rows{{SortScalar(Number(1)), "B"}, {SortScalar(Number(-0.0)), "b"},
        {SortScalar(Number(0.0)), "A"}, {SortScalar(Number(1)), "A"}};
    std::sort(Rows.begin(), Rows.end());
    Check(Rows[0].second == "A" && Rows[1].second == "b" &&
        Rows[2].second == "A" && Rows[3].second == "B", "record-key tie ordering");
    std::sort(Rows.begin(), Rows.end(), [](const Row& A, const Row& B) { return B < A; });
    Check(Rows[0].second == "B" && Rows[1].second == "A" &&
        Rows[2].second == "b" && Rows[3].second == "A", "descending tie ordering");
}
}

int main()
{
    try {
        Numbers(); StringsAndFields(); Shapes();
        std::puts("[CarbonLuau:Persistence] Sortable tests PASS");
        return 0;
    } catch (const std::exception& Problem) {
        std::fprintf(stderr, "[CarbonLuau:Persistence] Sortable tests failed: %s\n", Problem.what());
        return 1;
    }
}
