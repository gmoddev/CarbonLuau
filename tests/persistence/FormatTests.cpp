#include "Format.hpp"
#include <cstdio>
#include <cstring>
#include <limits>
using namespace CarbonLuau::Persistence;
namespace {
Deadline End() { return Clock::now() + std::chrono::seconds(5); }
template<class F> void Reject(F Action, Error Code = Error::InvalidArgument)
{ try { Action(); } catch (const Failure& Failure) { Require(Failure.Code == Code); return; } throw std::runtime_error("expected rejection"); }
std::string Hex(const std::array<uint8_t,32>& Hash)
{ std::string Result; for (auto Byte : Hash) { Result += "0123456789abcdef"[Byte>>4]; Result += "0123456789abcdef"[Byte&15]; } return Result; }
Bytes Wrapped(const Identity& Id,const Bytes& Payload)
{
    Bytes Envelope{'C','L','P','V'}; Put32(Envelope,1); Put32(Envelope,uint32_t(Payload.size())); Envelope.insert(Envelope.end(),Payload.begin(),Payload.end());
    Bytes Bound{uint8_t(Id.Addon)};
    for (const auto& Text : {Id.Package,Id.Store,Id.Key}) { Put32(Bound,uint32_t(Text.size())); Bound.insert(Bound.end(),Text.begin(),Text.end()); }
    Bound.insert(Bound.end(),Envelope.begin(),Envelope.end()); auto Hash=Digest(Bound); Envelope.insert(Envelope.end(),Hash.begin(),Hash.end()); return Envelope;
}
void Run()
{
    Require(Hex(Digest({})) == "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");
    Require(Hex(Digest({'a','b','c'})) == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
    Identity Id{false, "", "Store", "Key"}; Value Item; Item.Type = Kind::Number;
    for (double Number : {0.0, -0.0, 0.1, -1.5, std::numeric_limits<double>::denorm_min(),
        std::numeric_limits<double>::min(), std::numeric_limits<double>::max(), -std::numeric_limits<double>::max(),
        9007199254740991.0, 9007199254740992.0}) {
        Item.Number = Number; auto Copy = Decode(Id, Encode(Id, Item, End()), End());
        Require(!std::memcmp(&Number, &Copy->Number, 8));
    }
    for (double Number : {std::numeric_limits<double>::infinity(), -std::numeric_limits<double>::infinity(), std::numeric_limits<double>::quiet_NaN()}) {
        Item.Number = Number; Reject([&] { Encode(Id, Item, End()); });
    }
    Item.Type = Kind::Boolean; Item.Boolean = false;
    auto Envelope = Encode(Id, Item, End()); Require(!Decode(Id, Envelope, End())->Boolean);
    auto Other = Id; Other.Key = "key"; Reject([&] { Decode(Other, Envelope, End()); }, Error::StorageCorrupt);
    Envelope.back() ^= 1; Reject([&] { Decode(Id, Envelope, End()); }, Error::StorageCorrupt);
    Envelope = Encode(Id, Item, End()); Envelope[4] = 2; Reject([&] { Decode(Id, Envelope, End()); }, Error::FormatUnsupported);
    Envelope = Encode(Id, Item, End()); Envelope.push_back(0); Reject([&] { Decode(Id, Envelope, End()); }, Error::StorageCorrupt);
    Item.Type = Kind::String; Item.String = std::string(16384, 'a'); Require(Decode(Id, Encode(Id, Item, End()), End())->String == Item.String);
    Item.String += 'x'; Reject([&] { Encode(Id, Item, End()); });
    for (const std::string Text : {std::string("\xc0\x80"), std::string("\xed\xa0\x80"), std::string("a\0b",3)}) { Item.String = Text; Reject([&] { Encode(Id, Item, End()); }); }
    Item.String = "\xf0\x9f\x98\x80"; Require(Decode(Id, Encode(Id, Item, End()), End())->String == Item.String);
    for (const std::string Name : {"", ".", "..", "a/b", "a\\b", "a:b", "a\n"}) { Other = Id; Other.Key = Name; Reject([&] { Validate(Other); }); }
    Other = Id; Other.Key.assign(128, 'a'); Validate(Other); Other.Key += 'a'; Reject([&] { Validate(Other); });
    for (const std::string Package : {"economy", "gmoddev.admin", "a-b.c_d"}) Require(ValidPackage(Package));
    for (const std::string Package : {"", "carbonluau", "carbonluau.test", "A", "-a", "a_", "a-.b", "a._b", "a.b.c"}) Require(!ValidPackage(Package));
    auto Shared = std::make_shared<Value>(); Shared->Type = Kind::Boolean;
    Item = {}; Item.Map = {{"z", Shared},{"a",Shared}};
    auto Copy = Decode(Id, Encode(Id, Item, End()), End());
    Require(Copy->Map[0].first == "a" && Copy->Map[0].second != Copy->Map[1].second);
    Item.Map.push_back({"a", Shared}); Reject([&] { Encode(Id, Item, End()); });
    auto Cycle = std::make_shared<Value>(); Cycle->Map.push_back({"self", Cycle});
    Reject([&] { Encode(Id, *Cycle, End()); }); Cycle->Map.clear();
    Item = {}; Item.Type = Kind::Array; Reject([&] { Encode(Id, Item, End()); });
    Item.Array.assign(1024, Shared); Require(Decode(Id, Encode(Id, Item, End()), End())->Array.size() == 1024);
    Item.Array.push_back(Shared); Reject([&] { Encode(Id, Item, End()); });
    auto Nested = std::make_shared<Value>(); auto Root = Nested;
    for (unsigned I=1; I<16; ++I) { auto Child = std::make_shared<Value>(); Nested->Map.emplace_back("x",Child); Nested=Child; }
    Encode(Id, *Root, End()); Nested->Map.emplace_back("x", std::make_shared<Value>()); Reject([&] { Encode(Id, *Root, End()); });
    Item = {}; Item.Type = Kind(99); Reject([&] { Encode(Id, Item, End()); });
    Item.Type = Kind::Boolean; Reject([&] { Encode(Id, Item, Clock::now()); }, Error::DeadlineExceeded);
    // Exact 64 KiB envelope using four independent strings, then one byte over.
    Item = {}; Item.Type = Kind::Array;
    for (unsigned I=0; I<4; ++I) { auto Part=std::make_shared<Value>(); Part->Type=Kind::String; Part->String.assign(I==3 ? 16315 : 16384, 'a'); Item.Array.push_back(Part); }
    Require(Encode(Id, Item, End()).size() == 65536);
    Item.Array.back()->String.push_back('x'); Reject([&] { Encode(Id, Item, End()); });
    Item={}; Item.Type=Kind::Array;
    for (unsigned I=0; I<4; ++I) { auto Part=std::make_shared<Value>(); Part->Type=Kind::Array; Part->Array.assign(1023,Shared); Item.Array.push_back(Part); }
    Encode(Id,Item,End()); // four outer entries + 4092 leaves = 4096
    Item.Array.back()->Array.push_back(Shared); Reject([&] { Encode(Id,Item,End()); });
    Require(Hex(Digest(Bytes(56,'a'))) == "b35439a4ac6f0948b6d6f9e3c6af0f5f590ce20f1bde7090ef7970686ec6738a");
    Require(Hex(Digest(Bytes(64,'a'))) == "ffe054fe7ae0cb6dc65c3af9b61d5209f439851db43d0ba5997337df154668eb");
    // Deterministic arbitrary finite binary64 samples, not decimal conversions.
    uint64_t Random=0x123456789abcdef0ull;
    Item={}; Item.Type=Kind::Number;
    for (unsigned I=0; I<10000; ++I) {
        Random^=Random<<13; Random^=Random>>7; Random^=Random<<17;
        if ((Random&0x7ff0000000000000ull)==0x7ff0000000000000ull) continue;
        std::memcpy(&Item.Number,&Random,8); auto Result=Decode(Id,Encode(Id,Item,End()),End());
        Require(!std::memcmp(&Result->Number,&Random,8));
    }
    for (const Bytes Payload : {Bytes{0},Bytes{1,2},Bytes{3,255,255,255,255},Bytes{4,0,0,0,0},Bytes{5,1,0,0,0},Bytes{99}})
        Reject([&] { Decode(Id,Wrapped(Id,Payload),End()); },Error::StorageCorrupt);
    for (uint64_t Bits : {0x7ff0000000000000ull,0xfff0000000000000ull,0x7ff8000000000001ull}) {
        Bytes Payload{2}; Put64(Payload,Bits); Reject([&] { Decode(Id,Wrapped(Id,Payload),End()); },Error::StorageCorrupt);
    }
    // Exercise the parser past its checksum, with bounded deterministic noise.
    for (unsigned I=0; I<10000; ++I) {
        Bytes Payload(I%257);
        for (auto& Byte : Payload) { Random^=Random<<13; Random^=Random>>7; Random^=Random<<17; Byte=uint8_t(Random); }
        try { Decode(Id,Wrapped(Id,Payload),End()); }
        catch (const Failure& Problem) { Require(Problem.Code==Error::StorageCorrupt); }
    }
}
}
int main()
{
    try { Run(); std::puts("[CarbonLuau:Persistence] Format tests PASS"); return 0; }
    catch (const std::exception& Error) { std::fprintf(stderr, "[CarbonLuau:Persistence] Format test failed: %s\n", Error.what()); return 1; }
}
