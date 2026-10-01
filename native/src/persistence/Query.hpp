#pragma once

#include "Sortable.hpp"
#include <algorithm>
#include <array>
#include <cmath>
#include <cstring>
#include <limits>

// Worker-private wire format and cursor codec. No Luau or host objects enter here.
namespace CarbonLuau::Persistence::QueryWire {
constexpr Error InvalidQuery = Error(11);
constexpr Error AmbiguousFieldType = Error(12);
constexpr Error IndexPreparing = Error(13);
constexpr Error QueryUnavailable = Error(14);
constexpr Error InvalidCursor = Error(15);
constexpr size_t MaximumDescriptor = 6144;
constexpr size_t MaximumPage = 67584;
constexpr size_t MaximumCursorIngress = 2048;
constexpr size_t MaximumCursorOutput = 1712;
inline constexpr const char* PrefixAscending =
    "SELECT Id,Prefix FROM DerivedPrefixes INDEXED BY DerivedPrefixOrder WHERE Generation=?1 AND Prefix>=?2 AND Prefix<=?3 ORDER BY Prefix ASC";
inline constexpr const char* PrefixDescending =
    "SELECT Id,Prefix FROM DerivedPrefixes INDEXED BY DerivedPrefixOrder WHERE Generation=?1 AND Prefix>=?2 AND Prefix<=?3 ORDER BY Prefix DESC";
inline constexpr const char* EntryAscending =
    "SELECT Id,Suffix,RecordKey FROM DerivedEntries INDEXED BY DerivedEntryOrder WHERE PrefixId=?1 AND Suffix>=?2 AND Suffix<=?3 AND (Suffix,RecordKey)>(?4,?5) ORDER BY Suffix ASC,RecordKey ASC";
inline constexpr const char* EntryDescending =
    "SELECT Id,Suffix,RecordKey FROM DerivedEntries INDEXED BY DerivedEntryOrder WHERE PrefixId=?1 AND Suffix>=?2 AND Suffix<=?3 AND (Suffix,RecordKey)<(?4,?5) ORDER BY Suffix DESC,RecordKey DESC";
inline constexpr const char* MemberPoint =
    "SELECT Type,EntryId FROM DerivedMembers WHERE Generation=?1 AND RecordKey=?2";
inline constexpr const char* PrimaryPoint =
    "SELECT Envelope FROM Records WHERE Namespace=?1 AND Store=?2 AND Key=?3";

struct Request {
    Kind Type = Kind::Map;
    bool ExplicitType = false;
    bool Descending = false;
    uint32_t Limit = 50;
    uint32_t Flags = 0;
    std::string Equals, Min, Max, Cursor;
};
inline void Check(bool Good, Error Code = InvalidQuery) { if (!Good) throw Failure(Code); }
inline std::string Take(const Bytes& Input, size_t& Position, size_t Maximum) {
    Check(Position <= Input.size() && Input.size()-Position >= 4);
    uint32_t Length=Read32(Input.data()+Position); Position+=4;
    Check(Length<=Maximum && Input.size()-Position>=Length);
    std::string Result(reinterpret_cast<const char*>(Input.data()+Position),Length); Position+=Length;
    return Result;
}
inline std::string Scalar(const std::string& Raw, Kind Type) {
    Value Item; Item.Type=Type;
    switch(Type) {
    case Kind::Boolean: Check(Raw.size()==1 && uint8_t(Raw[0])<=1); Item.Boolean=Raw[0]!=0; break;
    case Kind::Number:
        Check(Raw.size()==8);
        { uint64_t Bits=Read64(reinterpret_cast<const uint8_t*>(Raw.data())); std::memcpy(&Item.Number,&Bits,8); }
        Check(std::isfinite(Item.Number)); break;
    case Kind::String: Check(ValidText(Raw,1024)); Item.String=Raw; break;
    default: throw Failure(InvalidQuery);
    }
    auto Sorted=SortScalar(Item); return std::string(Sorted.begin(),Sorted.end());
}
inline Request Parse(const Bytes& Input) {
    Check(Input.size()<=MaximumDescriptor && Input.size()>=24);
    Check(std::memcmp(Input.data(),"CLQD",4)==0 && Read32(Input.data()+4)==1);
    Request Result; Result.Flags=Read32(Input.data()+8);
    Check((Result.Flags & ~15u)==0);
    uint32_t RawType=Read32(Input.data()+12), Direction=Read32(Input.data()+16);
    Check(RawType<=3 && Direction<=1); Result.ExplicitType=RawType!=0;
    if(RawType)Result.Type=Kind(RawType);
    Result.Descending=Direction==1; Result.Limit=Read32(Input.data()+20);
    Check(Result.Limit>=1 && Result.Limit<=100);
    size_t Position=24;
    std::string* Slots[] = {&Result.Equals,&Result.Min,&Result.Max,&Result.Cursor};
    for(uint32_t Bit=0;Bit<4;++Bit) {
        auto Text=Take(Input,Position,Bit==3?MaximumCursorIngress:1024);
        if(Result.Flags&(1u<<Bit))*Slots[Bit]=std::move(Text);
        else Check(Text.empty());
    }
    Check(Position==Input.size());
    Check(!(Result.Flags&1) || !(Result.Flags&6));
    if(Result.Flags&7) {
        Kind Selected=Result.Type;
        // The wire Type is mandatory when any predicate is present. Native
        // bindings infer from the Luau scalar before constructing CLQD.
        Check(Result.ExplicitType && Selected!=Kind::Map);
        if(Result.Flags&1)Result.Equals=Scalar(Result.Equals,Selected);
        if(Result.Flags&2)Result.Min=Scalar(Result.Min,Selected);
        if(Result.Flags&4)Result.Max=Scalar(Result.Max,Selected);
        Check(Selected!=Kind::Boolean || (Result.Flags&1));
        if((Result.Flags&6)==6)Check(Result.Min<=Result.Max);
    }
    if(Result.ExplicitType && Result.Type==Kind::Boolean)Check(Result.Flags&1);
    if(Result.Flags&8)Check(!Result.Cursor.empty() && Result.Cursor.size()<=MaximumCursorIngress);
    return Result;
}
inline void Append(Bytes& Output,const std::string& Text) {
    Put32(Output,uint32_t(Text.size()));Output.insert(Output.end(),Text.begin(),Text.end());
}
inline std::array<uint8_t,32> Hmac(const std::array<uint8_t,32>& Secret,const Bytes& Message) {
    Bytes Inner(64,0x36), Outer(64,0x5c);
    for(size_t I=0;I<32;++I){Inner[I]^=Secret[I];Outer[I]^=Secret[I];}
    Inner.insert(Inner.end(),Message.begin(),Message.end());
    auto First=Digest(Inner); Outer.insert(Outer.end(),First.begin(),First.end());
    return Digest(Outer);
}
inline std::string Base64(const Bytes& Data) {
    constexpr char Alphabet[]="ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-_";
    std::string Output;Output.reserve((Data.size()*4+2)/3);
    uint32_t Buffer=0;unsigned Bits=0;
    for(uint8_t Byte:Data){Buffer=(Buffer<<8)|Byte;Bits+=8;while(Bits>=6){Bits-=6;Output.push_back(Alphabet[(Buffer>>Bits)&63]);}}
    if(Bits)Output.push_back(Alphabet[(Buffer<<(6-Bits))&63]);
    return Output;
}
inline Bytes Unbase64(const std::string& Text) {
    Check(!Text.empty() && Text.size()<=MaximumCursorIngress && Text.size()%4!=1,InvalidCursor);
    Bytes Output;Output.reserve(Text.size()*3/4);uint32_t Buffer=0;unsigned Bits=0;
    for(char Char:Text){int Value=-1;
        if(Char>='A'&&Char<='Z')Value=Char-'A';else if(Char>='a'&&Char<='z')Value=Char-'a'+26;
        else if(Char>='0'&&Char<='9')Value=Char-'0'+52;else if(Char=='-')Value=62;else if(Char=='_')Value=63;
        Check(Value>=0,InvalidCursor);Buffer=(Buffer<<6)|uint32_t(Value);Bits+=6;
        if(Bits>=8){Bits-=8;Output.push_back(uint8_t(Buffer>>Bits));}
    }
    Check(Bits==0 || (Buffer&((1u<<Bits)-1))==0,InvalidCursor);
    Check(Base64(Output)==Text,InvalidCursor);return Output;
}
inline Bytes Binding(const Identity& Id,const Request& Query,Kind Type) {
    Bytes Data;Data.push_back(Id.Addon?1:0);Append(Data,Id.Package);Append(Data,Id.Store);Append(Data,Id.Key);
    Data.push_back(uint8_t(Type));Data.push_back(Query.Descending?1:0);
    Put32(Data,Query.Flags&7);Append(Data,Query.Equals);Append(Data,Query.Min);Append(Data,Query.Max);
    return Data;
}
struct Boundary { Kind Type=Kind::Map;uint64_t Generation=0;std::string Scalar,Key; };
inline std::string MakeCursor(const std::array<uint8_t,32>& Secret,const Identity& Id,
    const Request& Query,Kind Type,uint64_t Generation,const std::string& Scalar,const std::string& Key) {
    Check(Scalar.size()<=1025 && ValidText(Key,128,true),Error::StorageCorrupt);
    Bytes Data{'C','L','Q','C',1,uint8_t(Type),uint8_t(Query.Descending?1:0)};
    Put64(Data,Generation);
    auto Bind=Digest(Binding(Id,Query,Type));Data.insert(Data.end(),Bind.begin(),Bind.end());
    Data.push_back(uint8_t(Scalar.size()));Data.push_back(uint8_t(Scalar.size()>>8));
    Data.insert(Data.end(),Scalar.begin(),Scalar.end());Data.push_back(uint8_t(Key.size()));
    Data.insert(Data.end(),Key.begin(),Key.end());
    auto Tag=Hmac(Secret,Data);Data.insert(Data.end(),Tag.begin(),Tag.end());
    Check(Data.size()<=1283,Error::StorageError);
    auto Encoded=Base64(Data);Check(Encoded.size()<=MaximumCursorOutput,Error::StorageError);return Encoded;
}
inline Boundary ReadCursor(const std::array<uint8_t,32>& Secret,const Identity& Id,
    const Request& Query,const std::string& Text) {
    auto Data=Unbase64(Text);Check(Data.size()>=7+8+32+2+1+32 && Data.size()<=1283,InvalidCursor);
    size_t Position=0;Check(std::memcmp(Data.data(),"CLQC",4)==0 && Data[4]==1,InvalidCursor);
    Boundary Result;Result.Type=Kind(Data[5]);Check(Result.Type>=Kind::Boolean && Result.Type<=Kind::String,InvalidCursor);
    Check(Data[6]==uint8_t(Query.Descending?1:0),InvalidCursor);Position=7;
    Result.Generation=Read64(Data.data()+Position);Position+=8;Check(Result.Generation!=0,InvalidCursor);
    auto Bind=Digest(Binding(Id,Query,Result.Type));
    unsigned Difference=0;for(size_t I=0;I<32;++I)Difference|=Data[Position+I]^Bind[I];Position+=32;
    size_t Length=size_t(Data[Position])|(size_t(Data[Position+1])<<8);Position+=2;
    Check(Length>=2 && Length<=1025 && Position+Length+1+32<=Data.size(),InvalidCursor);
    Result.Scalar.assign(reinterpret_cast<const char*>(Data.data()+Position),Length);Position+=Length;
    Length=Data[Position++];Check(Length>=1 && Length<=128 && Position+Length+32==Data.size(),InvalidCursor);
    Result.Key.assign(reinterpret_cast<const char*>(Data.data()+Position),Length);Position+=Length;
    auto Tag=Hmac(Secret,Bytes(Data.begin(),Data.begin()+Position));
    for(size_t I=0;I<32;++I)Difference|=Data[Position+I]^Tag[I];
    Check(Difference==0 && ValidText(Result.Key,128,true),InvalidCursor);
    Check(uint8_t(Result.Scalar[0])==uint8_t(Result.Type),InvalidCursor);
    if(Query.ExplicitType)Check(Query.Type==Result.Type,InvalidCursor);
    return Result;
}
}
