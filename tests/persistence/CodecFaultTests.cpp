#include "Format.hpp"
#include <atomic>
#include <cstdlib>
#include <cstdio>
#include <new>
using namespace CarbonLuau::Persistence;
namespace {
long FailAfter=-1;
bool Hit=false;
void* Allocate(size_t Size)
{
    if (FailAfter>=0 && FailAfter--==0) { Hit=true; throw std::bad_alloc(); }
    if (void* Pointer=std::malloc(Size ? Size : 1)) return Pointer;
    throw std::bad_alloc();
}
Deadline End() { return Clock::now()+std::chrono::seconds(5); }
}
void* operator new(size_t Size) { return Allocate(Size); }
void* operator new[](size_t Size) { return Allocate(Size); }
void operator delete(void* Pointer) noexcept { std::free(Pointer); }
void operator delete[](void* Pointer) noexcept { std::free(Pointer); }
void operator delete(void* Pointer,size_t) noexcept { std::free(Pointer); }
void operator delete[](void* Pointer,size_t) noexcept { std::free(Pointer); }
int main()
{
    try {
        Identity Id{true,"economy","Store","Key"}; Value Root; Root.Type=Kind::Map;
        for (unsigned I=0; I<24; ++I) {
            auto Child=std::make_shared<Value>(); Child->Type=Kind::Array;
            auto Text=std::make_shared<Value>(); Text->Type=Kind::String; Text->String.assign(100,'x');
            auto Number=std::make_shared<Value>(); Number->Type=Kind::Number; Number->Number=0.125;
            Child->Array={Text,Number}; Root.Map.emplace_back("key"+std::to_string(I),Child);
        }
        auto Expected=Encode(Id,Root,End());
        for (bool Reading : {false,true}) {
            unsigned Faults=0; bool Exhausted=false;
            for (long Position=0; Position<4096; ++Position) {
                Hit=false; FailAfter=Position; bool Failed=false;
                try {
                    if (Reading) { auto Decoded=Decode(Id,Expected,End()); }
                    else { auto Encoded=Encode(Id,Root,End()); }
                } catch (const std::bad_alloc&) { Failed=true; }
                FailAfter=-1;
                if (!Hit) { Require(!Failed); Exhausted=true; break; }
                Require(Failed); ++Faults;
                // Every failed operation releases its private graph/buffers and
                // leaves the input unchanged and usable for a later operation.
                Require(Encode(Id,Root,End())==Expected);
                Require(Encode(Id,*Decode(Id,Expected,End()),End())==Expected);
            }
            Require(Exhausted && Faults>0);
            std::printf("[CarbonLuau:Persistence] Codec %s allocation-fault PASS positions=%u\n",Reading ? "decode" : "encode",Faults);
        }
        return 0;
    } catch (...) { FailAfter=-1; std::fputs("[CarbonLuau:Persistence] Codec allocation-fault failure\n",stderr); return 1; }
}
