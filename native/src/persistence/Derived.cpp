#include "Derived.hpp"
#include "sqlite3.h"
#include <algorithm>
#include <array>
#include <cmath>
#include <cstring>
#include <limits>

namespace CarbonLuau::Persistence {
#ifdef CARBONLUAU_PERSISTENCE_TESTING
void TestCheckpoint(const char* Stage);
#define DERIVED_POINT(Stage) TestCheckpoint(Stage)
#else
#define DERIVED_POINT(Stage) ((void)0)
#endif
namespace {
constexpr int64_t NamespaceLimit = 16ll * 1024 * 1024;
constexpr int64_t GlobalLimit = 64ll * 1024 * 1024;
constexpr int64_t GlobalCharge = 65; // kind + eight u64 fields
constexpr int64_t Building = 1, Active = 2, Cleanup = 3;
struct Capacity {};
struct LogicalMismatch {};
struct DerivedStructureInvalid {};
struct SqlFailure : Failure { explicit SqlFailure(Error Code) : Failure(Code) {} };
void Consistent(bool Good) { if(!Good) throw LogicalMismatch{}; }

// This conversion exists ONLY in a test build, at this one named seam. The
// normal SQLite error mapper never turns FULL or another physical failure into
// a recoverable derived logical-capacity event.
void CapacityPoint() {
#ifdef CARBONLUAU_PERSISTENCE_TESTING
    try { TestCheckpoint("derived-capacity-after-change"); }
    catch (const Failure& Problem) {
        if (Problem.Code == Error::QuotaExceeded) throw Capacity{};
        throw;
    }
#endif
}

void Trusted(bool Good) { Require(Good, Error::StorageCorrupt); }
void CheckSql(int Rc) {
    if (Rc == SQLITE_OK) return;
    switch (Rc & 255) {
    case SQLITE_FULL: throw Failure(Error::StorageFull);
    case SQLITE_BUSY: case SQLITE_LOCKED: throw Failure(Error::StorageBusy);
    case SQLITE_CORRUPT: case SQLITE_NOTADB: case SQLITE_SCHEMA: case SQLITE_CONSTRAINT:
        throw SqlFailure(Error::StorageCorrupt);
    case SQLITE_INTERRUPT: throw Failure(Error::DeadlineExceeded);
    default: throw Failure(Error::StorageError);
    }
}
void Sql(sqlite3* Db, const char* Text) { CheckSql(sqlite3_exec(Db, Text, nullptr, nullptr, nullptr)); }
struct Statement {
    sqlite3_stmt* Handle = nullptr;
    Statement(sqlite3* Db, const char* Text) { CheckSql(sqlite3_prepare_v2(Db, Text, -1, &Handle, nullptr)); }
    ~Statement() { sqlite3_finalize(Handle); }
    Statement(const Statement&) = delete;
    Statement& operator=(const Statement&) = delete;
    void Integer(int Index, int64_t Value) { CheckSql(sqlite3_bind_int64(Handle, Index, Value)); }
    void Blob(int Index, const std::string& Value) {
        CheckSql(sqlite3_bind_blob(Handle, Index, Value.data(), int(Value.size()), SQLITE_TRANSIENT));
    }
    bool Next() {
        int Rc = sqlite3_step(Handle);
        if (Rc == SQLITE_ROW) return true;
        if (Rc == SQLITE_DONE) return false;
        CheckSql(Rc); return false;
    }
    void Done() { Trusted(!Next()); }
    void Reset() { CheckSql(sqlite3_reset(Handle)); CheckSql(sqlite3_clear_bindings(Handle)); }
    int64_t Number(int Index) {
        Trusted(sqlite3_column_type(Handle, Index) == SQLITE_INTEGER);
        return sqlite3_column_int64(Handle, Index);
    }
    std::string Data(int Index, size_t Maximum, bool Text = false) {
        Trusted(sqlite3_column_type(Handle, Index) == (Text ? SQLITE_TEXT : SQLITE_BLOB));
        int Length = sqlite3_column_bytes(Handle, Index);
        Trusted(Length >= 0 && size_t(Length) <= Maximum);
        const char* Ptr = static_cast<const char*>(sqlite3_column_blob(Handle, Index));
        Require(Ptr || !Length, Error::StorageError);
        return Length ? std::string(Ptr, size_t(Length)) : std::string();
    }
};
int64_t Scalar(sqlite3* Db, const char* Text) {
    Statement Query(Db, Text); Trusted(Query.Next()); auto Result = Query.Number(0); Query.Done(); return Result;
}
int64_t Add(int64_t Left, int64_t Right) {
    Trusted(Left >= 0 && Right >= 0 && Right <= std::numeric_limits<int64_t>::max() - Left);
    return Left + Right;
}
bool Less(const std::string& Left, const std::string& Right) {
    return std::lexicographical_compare(Left.begin(), Left.end(), Right.begin(), Right.end(),
        [](unsigned char A, unsigned char B) { return A < B; });
}
std::string Namespace(const Identity& Id) { return std::string(1, Id.Addon ? '\1' : '\0') + Id.Package; }
Identity StoreIdentity(const Identity& Id) {
    Identity Result = Id; Result.Key = "K"; CarbonLuau::Persistence::Validate(Result); return Result;
}
Identity StoredIdentity(const std::string& Ns, const std::string& Store, const std::string& Key) {
    Trusted(!Ns.empty() && uint8_t(Ns[0]) <= 1);
    Identity Id{Ns[0] == 1, Ns.substr(1), Store, Key};
    try { CarbonLuau::Persistence::Validate(Id); }
    catch (const Failure&) { throw Failure(Error::StorageCorrupt); }
    return Id;
}
int64_t NamespaceCharge(const std::string& Ns) { return 21 + int64_t(Ns.size()); }
int64_t FieldCharge(const std::string& Ns, const std::string& Store, const std::string& Field) {
    return 29 + int64_t(Ns.size() + Store.size() + Field.size());
}
int64_t GenerationCharge(const std::string& Checkpoint) { return 70 + int64_t(Checkpoint.size()); }
int64_t PrefixCharge(const std::string& Prefix) { return 37 + int64_t(Prefix.size()); }
int64_t EntryCharge(const std::string& Suffix, const std::string& Key) { return 33 + int64_t(Suffix.size() + Key.size()); }
int64_t MemberCharge(const std::string& Key) { return 30 + int64_t(Key.size()); }
std::string KeyBytes(const ExtractedScalar& Item) {
    return std::string(Item.SortKey.begin(), Item.SortKey.end());
}
void ValidateSortKey(const std::string& Key) {
    Trusted(!Key.empty() && Key.size() <= 1025);
    Value Item; Item.Type = Kind(uint8_t(Key[0]));
    if (Item.Type == Kind::Boolean) {
        Trusted(Key.size() == 2 && uint8_t(Key[1]) <= 1); Item.Boolean = Key[1] != 0;
    } else if (Item.Type == Kind::String) {
        Item.String = Key.substr(1); Trusted(ValidText(Item.String, 1024));
    } else if (Item.Type == Kind::Number) {
        Trusted(Key.size() == 9); uint64_t Bits = 0;
        for (size_t Index = 1; Index < 9; ++Index) Bits = (Bits << 8) | uint8_t(Key[Index]);
        Bits = (Bits >> 63) ? (Bits ^ (uint64_t(1) << 63)) : ~Bits;
        std::memcpy(&Item.Number, &Bits, 8); Trusted(std::isfinite(Item.Number));
    } else Trusted(false);
    const auto Encoded = SortScalar(Item);
    Trusted(std::string(Encoded.begin(), Encoded.end()) == Key);
}
struct FieldRow {
    int64_t Id = 0, Charge = 0;
    std::string Ns, Store, Name;
};
struct GenerationRow {
    int64_t Id = 0, Field = 0, State = 0, Members = 0, Booleans = 0, Numbers = 0, Strings = 0, Oversized = 0, Charge = 0;
    std::string Checkpoint;
};
struct MemberRow { bool Found = false; int64_t Type = 0, Entry = 0, Charge = 0; };
struct EntryRow { int64_t Prefix = 0, Charge = 0; std::string Suffix, Key; };
struct PrefixRow { int64_t Id = 0, Generation = 0, References = 0, Charge = 0; std::string Prefix; };

class Engine {
public:
    sqlite3* Db;
    Deadline& End;
    std::unique_ptr<Statement> MemberLookup;
    Engine(sqlite3* Database, Deadline& DeadlineValue) : Db(Database), End(DeadlineValue) {
        Require(Db && sqlite3_get_autocommit(Db) == 0, Error::StorageError); Tick();
    }
    void Tick() { Require(Clock::now() < End, Error::DeadlineExceeded); }
    // Only Capacity and an independently classified representation mismatch
    // are recoverable locally. Any SQLite/codec/deadline/metadata error
    // escapes with the caller transaction requiring rollback, even if SQLite
    // has already automatically rolled it back (FULL/IOERR, for example).
    template<class Function> bool Save(Function Body) {
        DERIVED_POINT("derived-before-savepoint");
        Sql(Db, "SAVEPOINT CarbonLuauDerived");
        DERIVED_POINT("derived-after-savepoint");
        try {
            Body(); DERIVED_POINT("derived-before-release");
            Sql(Db, "RELEASE CarbonLuauDerived");
            DERIVED_POINT("derived-after-release"); return true;
        }
        catch (const Capacity&) { RollbackSave(); return false; }
        catch (const LogicalMismatch&) { RollbackSave(); return false; }
    }
    void RollbackSave() {
        DERIVED_POINT("derived-before-rollback-to");
        Sql(Db, "ROLLBACK TO CarbonLuauDerived");
        DERIVED_POINT("derived-after-rollback-to");
        DERIVED_POINT("derived-before-rollback-release");
        Sql(Db, "RELEASE CarbonLuauDerived");
        DERIVED_POINT("derived-after-rollback-release");
    }
    FieldRow ReadField(Statement& Query, int Offset = 0) {
        FieldRow Field{Query.Number(Offset), Query.Number(Offset+4), Query.Data(Offset+1,66),
            Query.Data(Offset+2,64), Query.Data(Offset+3,64)};
        Trusted(Field.Id > 0 && ValidField(Field.Name)); StoredIdentity(Field.Ns, Field.Store, "K");
        Trusted(Field.Charge == FieldCharge(Field.Ns, Field.Store, Field.Name)); return Field;
    }
    FieldRow Field(int64_t Id) {
        Statement Query(Db, "SELECT Id,Namespace,Store,Name,Charge FROM DerivedFields WHERE Id=?1");
        Query.Integer(1, Id); Trusted(Query.Next()); auto Result = ReadField(Query); Query.Done(); return Result;
    }
    GenerationRow ReadGeneration(Statement& Query, int Offset = 0) {
        GenerationRow Gen;
        Gen.Id=Query.Number(Offset); Gen.Field=Query.Number(Offset+1); Gen.State=Query.Number(Offset+2);
        Gen.Checkpoint=Query.Data(Offset+3,128);
        Gen.Members=Query.Number(Offset+4); Gen.Booleans=Query.Number(Offset+5); Gen.Numbers=Query.Number(Offset+6);
        Gen.Strings=Query.Number(Offset+7); Gen.Oversized=Query.Number(Offset+8); Gen.Charge=Query.Number(Offset+9);
        Trusted(Gen.Id>0 && Gen.Field>0 && Gen.State>=Building && Gen.State<=Cleanup);
        Trusted(Gen.Checkpoint.empty() || ValidText(Gen.Checkpoint,128,true));
        Trusted(Gen.Members>=0 && Gen.Members<=10000 && Gen.Booleans>=0 && Gen.Numbers>=0 && Gen.Strings>=0 &&
            Gen.Oversized>=0 && Gen.Oversized<=Gen.Strings && Gen.Members==Add(Add(Gen.Booleans,Gen.Numbers),Gen.Strings));
        Trusted(Gen.Charge==GenerationCharge(Gen.Checkpoint)); return Gen;
    }
    GenerationRow Generation(int64_t Id) {
        Statement Query(Db, "SELECT Id,FieldId,State,Checkpoint,Members,Booleans,Numbers,Strings,Oversized,Charge FROM DerivedGenerations WHERE Id=?1");
        Query.Integer(1,Id); Trusted(Query.Next()); auto Result=ReadGeneration(Query); Query.Done(); return Result;
    }
    std::vector<GenerationRow> Generations(int64_t FieldId) {
        Statement Query(Db, "SELECT Id,FieldId,State,Checkpoint,Members,Booleans,Numbers,Strings,Oversized,Charge FROM DerivedGenerations WHERE FieldId=?1 ORDER BY Id");
        Query.Integer(1,FieldId); std::vector<GenerationRow> Result; unsigned ActiveCount=0, BuildingCount=0;
        while(Query.Next()) {
            Tick(); Trusted(Result.size()<2); Result.push_back(ReadGeneration(Query));
            ActiveCount+=Result.back().State==Active; BuildingCount+=Result.back().State==Building;
        }
        Trusted(ActiveCount<=1 && BuildingCount<=1); return Result;
    }
    MemberRow Member(int64_t Gen, const std::string& Key) {
        if(!MemberLookup) MemberLookup=std::make_unique<Statement>(Db,
            "SELECT Type,EntryId,Charge FROM DerivedMembers WHERE Generation=?1 AND RecordKey=?2");
        auto& Query=*MemberLookup;
        Query.Integer(1,Gen); Query.Blob(2,Key);
        if(!Query.Next()) { Query.Reset(); return {}; }
        MemberRow Result{true,Query.Number(0),Query.Number(1),Query.Number(2)}; Query.Done(); Query.Reset();
        Trusted(Result.Type>=1 && Result.Type<=3 && Result.Entry>=0 && (Result.Entry || Result.Type==3));
        Trusted(Result.Charge==MemberCharge(Key)); return Result;
    }
    EntryRow Entry(int64_t Id) {
        Statement Query(Db, "SELECT PrefixId,Suffix,RecordKey,Charge FROM DerivedEntries WHERE Id=?1");
        Query.Integer(1,Id); Trusted(Query.Next()); EntryRow Result{Query.Number(0),Query.Number(3),Query.Data(1,513),Query.Data(2,128)}; Query.Done();
        Trusted(Result.Prefix>0 && ValidText(Result.Key,128,true) && Result.Charge==EntryCharge(Result.Suffix,Result.Key)); return Result;
    }
    PrefixRow Prefix(int64_t Id) {
        Statement Query(Db, "SELECT Id,Generation,Prefix,Refs,Charge FROM DerivedPrefixes WHERE Id=?1");
        Query.Integer(1,Id); Trusted(Query.Next());
        PrefixRow Result{Query.Number(0),Query.Number(1),Query.Number(3),Query.Number(4),Query.Data(2,512)}; Query.Done();
        Trusted(Result.Id>0 && Result.Generation>0 && Result.References>0 && Result.References<=10000 && !Result.Prefix.empty());
        Trusted(Result.Charge==PrefixCharge(Result.Prefix)); return Result;
    }
    void Changed() { Trusted(sqlite3_changes(Db)==1); }
    void Charge(const std::string& Ns, int64_t Delta) {
        Tick();
        Statement Query(Db, "SELECT Bytes FROM DerivedNamespaces WHERE Namespace=?1"); Query.Blob(1,Ns);
        Trusted(Query.Next()); auto Local=Query.Number(0); Query.Done();
        auto Global=Scalar(Db,"SELECT Bytes FROM DerivedTotals WHERE Id=1");
        Trusted(Local>=NamespaceCharge(Ns) && Local<=NamespaceLimit && Global>=GlobalCharge && Global<=GlobalLimit);
        if(Delta>0 && (Delta>NamespaceLimit-Local || Delta>GlobalLimit-Global)) throw Capacity{};
        Trusted(Delta>=-Local && Delta>=-Global);
        Trusted(Local+Delta>=NamespaceCharge(Ns) && Global+Delta>=GlobalCharge);
        Statement Update(Db,"UPDATE DerivedNamespaces SET Bytes=Bytes+?2 WHERE Namespace=?1");
        Update.Blob(1,Ns); Update.Integer(2,Delta); Update.Done(); Changed();
        Statement Total(Db,"UPDATE DerivedTotals SET Bytes=Bytes+?1 WHERE Id=1"); Total.Integer(1,Delta); Total.Done(); Changed();
    }
    void NamespaceLedger(const std::string& Ns) {
        Statement Find(Db,"SELECT Charge FROM DerivedNamespaces WHERE Namespace=?1"); Find.Blob(1,Ns);
        if(Find.Next()) { Trusted(Find.Number(0)==NamespaceCharge(Ns)); Find.Done(); return; }
        auto ChargeValue=NamespaceCharge(Ns), Global=Scalar(Db,"SELECT Bytes FROM DerivedTotals WHERE Id=1");
        Trusted(Global>=GlobalCharge && Global<=GlobalLimit); if(ChargeValue>GlobalLimit-Global) throw Capacity{};
        Statement Insert(Db,"INSERT INTO DerivedNamespaces VALUES(?1,?2,?2)"); Insert.Blob(1,Ns); Insert.Integer(2,ChargeValue); Insert.Done();
        Statement Total(Db,"UPDATE DerivedTotals SET Bytes=Bytes+?1 WHERE Id=1"); Total.Integer(1,ChargeValue); Total.Done(); Changed();
    }
    int64_t Allocate(unsigned Which) {
        static const char* Reads[]={"SELECT NextField FROM DerivedTotals WHERE Id=1","SELECT NextGeneration FROM DerivedTotals WHERE Id=1",
            "SELECT NextPrefix FROM DerivedTotals WHERE Id=1","SELECT NextEntry FROM DerivedTotals WHERE Id=1"};
        static const char* Writes[]={"UPDATE DerivedTotals SET NextField=NextField+1 WHERE Id=1","UPDATE DerivedTotals SET NextGeneration=NextGeneration+1 WHERE Id=1",
            "UPDATE DerivedTotals SET NextPrefix=NextPrefix+1 WHERE Id=1","UPDATE DerivedTotals SET NextEntry=NextEntry+1 WHERE Id=1"};
        Trusted(Which<4); auto Id=Scalar(Db,Reads[Which]); Trusted(Id>0);
        if(Id==std::numeric_limits<int64_t>::max()) throw Capacity{};
        Sql(Db,Writes[Which]); Changed(); return Id;
    }
    void Counters(int64_t Gen, int64_t Type, bool Oversized, int64_t Delta) {
        Trusted(Type>=1 && Type<=3 && (!Oversized || Type==3) && (Delta==1 || Delta==-1));
        Statement Update(Db,"UPDATE DerivedGenerations SET Members=Members+?2,Booleans=Booleans+?3,Numbers=Numbers+?4,Strings=Strings+?5,Oversized=Oversized+?6 WHERE Id=?1");
        Update.Integer(1,Gen); Update.Integer(2,Delta); Update.Integer(3,Type==1?Delta:0); Update.Integer(4,Type==2?Delta:0);
        Update.Integer(5,Type==3?Delta:0); Update.Integer(6,Oversized?Delta:0); Update.Done(); Changed();
        Generation(Gen); // constant-size validation, including nonnegative counts
    }
    void Match(int64_t Gen, const std::string& Key, const MemberRow& MemberValue, const ExtractedScalar& Item) {
        if(!MemberValue.Found) { Consistent(!Item.Represented); return; }
        std::string Actual;
        if(MemberValue.Entry) {
            auto EntryValue=Entry(MemberValue.Entry); auto PrefixValue=Prefix(EntryValue.Prefix);
            // Establish internal structure/authority BEFORE classifying a
            // disagreement with the decoded primary value as disposable state.
            Trusted(PrefixValue.Generation==Gen && EntryValue.Key==Key);
            Actual=PrefixValue.Prefix+EntryValue.Suffix; ValidateSortKey(Actual);
            Trusted(int64_t(uint8_t(Actual[0]))==MemberValue.Type && PrefixValue.Prefix.size()==std::min<size_t>(512,Actual.size()));
        }
        Consistent(Item.Represented && MemberValue.Type==int64_t(Item.Type) && bool(MemberValue.Entry)==Item.Usable);
        if(MemberValue.Entry) Consistent(Actual==KeyBytes(Item));
    }
    void Remove(const FieldRow& FieldValue, int64_t Gen, const std::string& Key, const MemberRow& Item) {
        Trusted(Item.Found);
        if(Item.Entry) {
            auto EntryValue=Entry(Item.Entry); auto PrefixValue=Prefix(EntryValue.Prefix);
            Trusted(EntryValue.Key==Key && PrefixValue.Generation==Gen);
            Statement Delete(Db,"DELETE FROM DerivedEntries WHERE Id=?1"); Delete.Integer(1,Item.Entry); Delete.Done(); Changed();
            Charge(FieldValue.Ns,-EntryValue.Charge);
            if(PrefixValue.References==1) {
                Statement Drop(Db,"DELETE FROM DerivedPrefixes WHERE Id=?1"); Drop.Integer(1,PrefixValue.Id); Drop.Done(); Changed();
                Charge(FieldValue.Ns,-PrefixValue.Charge);
            } else {
                Statement Update(Db,"UPDATE DerivedPrefixes SET Refs=Refs-1 WHERE Id=?1"); Update.Integer(1,PrefixValue.Id); Update.Done(); Changed();
            }
        }
        Statement Delete(Db,"DELETE FROM DerivedMembers WHERE Generation=?1 AND RecordKey=?2");
        Delete.Integer(1,Gen); Delete.Blob(2,Key); Delete.Done(); Changed(); Charge(FieldValue.Ns,-Item.Charge);
        Counters(Gen,Item.Type,!Item.Entry,-1);
    }
    void Insert(const FieldRow& FieldValue, int64_t Gen, const std::string& Key, const ExtractedScalar& Item) {
        if(!Item.Represented) return;
        int64_t EntryId=0;
        if(Item.Usable) {
            const auto FullKey=KeyBytes(Item), Head=FullKey.substr(0,512), Tail=FullKey.substr(Head.size());
            Trusted(!Head.empty() && Tail.size()<=513);
            int64_t PrefixId=0;
            {
                Statement Find(Db,"SELECT Id FROM DerivedPrefixes WHERE Generation=?1 AND Prefix=?2"); Find.Integer(1,Gen); Find.Blob(2,Head);
                if(Find.Next()) { PrefixId=Find.Number(0); Find.Done(); }
            }
            if(PrefixId) {
                auto Existing=Prefix(PrefixId); Trusted(Existing.Generation==Gen && Existing.Prefix==Head && Existing.References<10000);
                Statement Update(Db,"UPDATE DerivedPrefixes SET Refs=Refs+1 WHERE Id=?1"); Update.Integer(1,PrefixId); Update.Done(); Changed();
            } else {
                PrefixId=Allocate(2); auto Amount=PrefixCharge(Head); Charge(FieldValue.Ns,Amount);
                Statement AddPrefix(Db,"INSERT INTO DerivedPrefixes VALUES(?1,?2,?3,1,?4)");
                AddPrefix.Integer(1,PrefixId); AddPrefix.Integer(2,Gen); AddPrefix.Blob(3,Head); AddPrefix.Integer(4,Amount); AddPrefix.Done();
            }
            EntryId=Allocate(3); auto Amount=EntryCharge(Tail,Key); Charge(FieldValue.Ns,Amount);
            Statement AddEntry(Db,"INSERT INTO DerivedEntries VALUES(?1,?2,?3,?4,?5)");
            AddEntry.Integer(1,EntryId); AddEntry.Integer(2,PrefixId); AddEntry.Blob(3,Tail); AddEntry.Blob(4,Key); AddEntry.Integer(5,Amount); AddEntry.Done();
        }
        auto Amount=MemberCharge(Key); Charge(FieldValue.Ns,Amount);
        Statement AddMember(Db,"INSERT INTO DerivedMembers VALUES(?1,?2,?3,?4,?5)");
        AddMember.Integer(1,Gen); AddMember.Blob(2,Key); AddMember.Integer(3,int64_t(Item.Type)); AddMember.Integer(4,EntryId); AddMember.Integer(5,Amount); AddMember.Done();
        Counters(Gen,int64_t(Item.Type),!Item.Usable,1);
        CapacityPoint();
    }
    void Withdraw(int64_t Gen) {
        DERIVED_POINT("derived-before-withdraw");
        Statement Update(Db,"UPDATE DerivedGenerations SET State=3 WHERE Id=?1"); Update.Integer(1,Gen); Update.Done(); Changed();
        DERIVED_POINT("derived-after-withdraw");
    }
    void Replace(const FieldRow& FieldValue, const GenerationRow& Gen, const std::string& Key, const Value* Old, const Value* New, bool Scanning) {
        auto Previous=Member(Gen.Id,Key);
        auto Before=Old?ExtractScalar(*Old,FieldValue.Name):ExtractedScalar{};
        auto After=New?ExtractScalar(*New,FieldValue.Name):ExtractedScalar{};
        if(Previous.Found || (!Scanning && (Gen.State==Active || !Less(Gen.Checkpoint,Key)))) Match(Gen.Id,Key,Previous,Before);
        if(Previous.Found && Before.Type==After.Type && Before.Represented==After.Represented && Before.Usable==After.Usable && Before.SortKey==After.SortKey) return;
        if(Previous.Found) Remove(FieldValue,Gen.Id,Key,Previous);
        Insert(FieldValue,Gen.Id,Key,After);
    }
    bool Prepare(const Identity& StoreId, const std::string& Name, bool Force) {
        auto Id=StoreIdentity(StoreId); Require(ValidField(Name)); const auto Ns=Namespace(Id);
        return Save([&] {
            FieldRow Selected;
            Statement Find(Db,"SELECT Id,Namespace,Store,Name,Charge FROM DerivedFields WHERE Namespace=?1 AND Store=?2 AND Name=?3");
            Find.Blob(1,Ns); Find.Blob(2,Id.Store); Find.Blob(3,Name);
            if(Find.Next()) { Selected=ReadField(Find); Find.Done(); }
            if(!Selected.Id) {
                Statement Count(Db,"SELECT count(*) FROM DerivedFields WHERE Namespace=?1 AND Store=?2"); Count.Blob(1,Ns); Count.Blob(2,Id.Store);
                Trusted(Count.Next()); auto Existing=Count.Number(0); Count.Done(); Trusted(Existing>=0 && Existing<=8); if(Existing==8) throw Capacity{};
                NamespaceLedger(Ns); Selected={Allocate(0),FieldCharge(Ns,Id.Store,Name),Ns,Id.Store,Name}; Charge(Ns,Selected.Charge);
                Statement AddField(Db,"INSERT INTO DerivedFields VALUES(?1,?2,?3,?4,?5)");
                AddField.Integer(1,Selected.Id); AddField.Blob(2,Ns); AddField.Blob(3,Id.Store); AddField.Blob(4,Name); AddField.Integer(5,Selected.Charge); AddField.Done();
            }
            const auto Existing=Generations(Selected.Id);
            for(const auto& Gen:Existing) if(Gen.State==Building || (Gen.State==Active && !Force)) return;
            if(Existing.size()==2) throw Capacity{};
            auto Gen=Allocate(1), Amount=GenerationCharge(""); Charge(Ns,Amount);
            Statement InsertGen(Db,"INSERT INTO DerivedGenerations VALUES(?1,?2,1,?3,0,0,0,0,0,?4)");
            InsertGen.Integer(1,Gen); InsertGen.Integer(2,Selected.Id); InsertGen.Blob(3,""); InsertGen.Integer(4,Amount); InsertGen.Done();
        });
    }
    void Mutate(const Identity& Id, const Value* Old, const Value* New) {
        CarbonLuau::Persistence::Validate(Id);
        std::vector<FieldRow> Fields;
        {
            Statement Query(Db,"SELECT Id,Namespace,Store,Name,Charge FROM DerivedFields WHERE Namespace=?1 AND Store=?2 ORDER BY Name");
            Query.Blob(1,Namespace(Id)); Query.Blob(2,Id.Store);
            while(Query.Next()) { Tick(); Trusted(Fields.size()<8); Fields.push_back(ReadField(Query)); }
        }
        for(const auto& FieldValue:Fields) {
            for(const auto& Gen:Generations(FieldValue.Id)) {
                if(Gen.State==Cleanup) continue; Tick();
                if(!Save([&] { Replace(FieldValue,Gen,Id.Key,Old,New,false); })) Withdraw(Gen.Id);
            }
        }
    }
    void Build(const FieldRow& FieldValue, const GenerationRow& Gen) {
        if(!Save([&] {
            std::string Checkpoint=Gen.Checkpoint; size_t BytesRead=0; unsigned Count=0; bool ByteStopped=false;
            {
                Statement Rows(Db,"SELECT Key,Envelope FROM Records WHERE Namespace=?1 AND Store=?2 AND Key>?3 ORDER BY Key LIMIT 32");
                Rows.Blob(1,FieldValue.Ns); Rows.Blob(2,FieldValue.Store); Rows.Blob(3,Checkpoint);
                while(Rows.Next()) {
                    Tick(); auto Key=Rows.Data(0,128);
                    // SQLite holds at most one maximum F1 envelope here. Do not
                    // allocate/decode or advance the checkpoint past the budget.
                    Trusted(sqlite3_column_type(Rows.Handle,1)==SQLITE_BLOB);
                    int Length=sqlite3_column_bytes(Rows.Handle,1); Trusted(Length>=45 && Length<=int(MaximumEnvelope));
                    if(size_t(Length)>256*1024-BytesRead) { ByteStopped=true; break; }
                    auto Envelope=Rows.Data(1,MaximumEnvelope); BytesRead+=Envelope.size(); ++Count; Trusted(Count<=32);
                    auto Id=StoredIdentity(FieldValue.Ns,FieldValue.Store,Key);
                    auto Root=Decode(Id,Bytes(Envelope.begin(),Envelope.end()),End);
                    Replace(FieldValue,Gen,Key,Root.get(),Root.get(),true); Checkpoint=std::move(Key);
                }
            }
            auto Amount=GenerationCharge(Checkpoint); Charge(FieldValue.Ns,Amount-Gen.Charge);
            DERIVED_POINT("derived-before-checkpoint");
            Statement Update(Db,"UPDATE DerivedGenerations SET Checkpoint=?2,Charge=?3 WHERE Id=?1");
            Update.Integer(1,Gen.Id); Update.Blob(2,Checkpoint); Update.Integer(3,Amount); Update.Done(); Changed();
            DERIVED_POINT("derived-after-checkpoint"); CapacityPoint();
            // LIMIT 32 does not prove EOF after exactly 32 rows. Defer that
            // case to the next bounded batch instead of inspecting a 33rd row.
            if(Count<32 && !ByteStopped) {
                // No final scan/count of primary data. Maintained counters and
                // checkpoint form the proof; post-Ready verification checks it
                // independently after a worker restart.
                auto Current=Generation(Gen.Id); Trusted(Current.State==Building && Current.Field==FieldValue.Id && Current.Checkpoint==Checkpoint);
                for(const auto& Other:Generations(FieldValue.Id)) if(Other.State==Active) Withdraw(Other.Id);
                DERIVED_POINT("derived-before-publish");
                Statement Publish(Db,"UPDATE DerivedGenerations SET State=2 WHERE Id=?1 AND State=1"); Publish.Integer(1,Gen.Id); Publish.Done(); Changed();
                DERIVED_POINT("derived-after-publish");
            }
        })) Withdraw(Gen.Id);
    }
    void Clean(const FieldRow& FieldValue, const GenerationRow& Gen) {
        DERIVED_POINT("derived-before-cleanup");
        std::vector<std::string> Keys;
        {
            Statement Rows(Db,"SELECT RecordKey FROM DerivedMembers WHERE Generation=?1 ORDER BY RecordKey LIMIT 32"); Rows.Integer(1,Gen.Id);
            while(Rows.Next()) { Tick(); Keys.push_back(Rows.Data(0,128)); }
        }
        for(const auto& Key:Keys) { Tick(); Remove(FieldValue,Gen.Id,Key,Member(Gen.Id,Key)); }
        auto Current=Generation(Gen.Id);
        if(Current.Members==0) {
            Statement Left(Db,"SELECT 1 FROM DerivedMembers WHERE Generation=?1 LIMIT 1"); Left.Integer(1,Gen.Id); Trusted(!Left.Next());
            Statement Prefixes(Db,"SELECT 1 FROM DerivedPrefixes WHERE Generation=?1 LIMIT 1"); Prefixes.Integer(1,Gen.Id); Trusted(!Prefixes.Next());
            Statement Delete(Db,"DELETE FROM DerivedGenerations WHERE Id=?1"); Delete.Integer(1,Gen.Id); Delete.Done(); Changed(); Charge(FieldValue.Ns,-Current.Charge);
        }
        DERIVED_POINT("derived-after-cleanup");
    }
    void Maintain() {
        auto Last=Scalar(Db,"SELECT WorkGeneration FROM DerivedTotals WHERE Id=1"); Trusted(Last>=0);
        int64_t Id=0;
        {
            Statement Find(Db,"SELECT Id FROM DerivedGenerations WHERE State IN (1,3) AND Id>?1 ORDER BY Id LIMIT 1"); Find.Integer(1,Last);
            if(Find.Next()) Id=Find.Number(0);
        }
        if(!Id) { Statement Find(Db,"SELECT Id FROM DerivedGenerations WHERE State IN (1,3) ORDER BY Id LIMIT 1"); if(Find.Next()) Id=Find.Number(0); }
        if(!Id) return;
        auto Gen=Generation(Id); auto FieldValue=Field(Gen.Field);
        if(Gen.State==Building) Build(FieldValue,Gen); else { Trusted(Gen.State==Cleanup); Clean(FieldValue,Gen); }
        Statement Update(Db,"UPDATE DerivedTotals SET WorkGeneration=?1 WHERE Id=1"); Update.Integer(1,Id); Update.Done(); Changed();
    }
    Derived::VerificationResult VerifyStep(DerivedVerification& Proof);
};
}

struct DerivedVerification {
    enum class Phase {
        Start, Namespace, Field, Generation, Members, Prefixes, PrefixEntries,
        Orphans, Store, StoreMembers, StorePrimary, StoreWithdraw,
        Complete, Invalid
    } Current = Phase::Start;
    std::array<int64_t,4> Next{};
    int64_t ExpectedGlobal=0, Total=0, Local=0, ExpectedLocal=0;
    int64_t Fields=0, Generations=0, Members=0, Prefixes=0, Entries=0;
    std::string Namespace, FieldStore, FieldName, PreviousStore;
    bool NamespaceStarted=false;
    unsigned InStore=0, ActiveCount=0, BuildingCount=0, FieldGenerationCount=0;
    FieldRow Field;
    GenerationRow Generation;
    int64_t LastGeneration=0, GenerationMembers=0, GenerationUsable=0;
    int64_t GenerationBooleans=0, GenerationNumbers=0, GenerationStrings=0, GenerationOversized=0;
    int64_t GenerationEntries=0;
    std::string MemberKey, PrefixKey, EntrySuffix, EntryKey;
    int64_t CurrentPrefix=0, ExpectedReferences=0, CurrentReferences=0;
    unsigned OrphanTable=0;
    int64_t OrphanCount=0, OrphanId=0;
    std::string OrphanKey;
    std::string StoreNamespace, StoreName, StoreMemberKey, StorePrimaryKey;
    struct Candidate { GenerationRow Generation; FieldRow Field; bool Mismatch=false; };
    std::vector<Candidate> Candidates;
    size_t CandidateIndex=0;
    unsigned PrimaryCount=0;
};

Derived::Derived(sqlite3* Db, Deadline& DeadlineValue) : Database(Db), End(DeadlineValue), Verification(std::make_unique<DerivedVerification>()) {
    Require(Db!=nullptr,Error::StorageError);
}
Derived::~Derived() = default;
bool Derived::VerificationComplete() const { return Verification->Current==DerivedVerification::Phase::Complete; }
bool Derived::VerificationFailed() const { return Verification->Current==DerivedVerification::Phase::Invalid; }
void Derived::InvalidateVerification() {
    // A structural derived failure remains disabled for this worker lifetime.
    // Normal foreground writes may continue without repeatedly restarting an
    // expensive proof that cannot currently establish derived authority.
    if(Verification->Current!=DerivedVerification::Phase::Invalid)
        Verification=std::make_unique<DerivedVerification>();
}
const std::vector<std::string>& Derived::Schema() {
    static const std::vector<std::string> Statements={
        "CREATE TABLE DerivedTotals(Id INTEGER PRIMARY KEY CHECK(Id=1),NextField INTEGER NOT NULL,NextGeneration INTEGER NOT NULL,NextPrefix INTEGER NOT NULL,NextEntry INTEGER NOT NULL,WorkGeneration INTEGER NOT NULL,Bytes INTEGER NOT NULL,Charge INTEGER NOT NULL)",
        "CREATE TABLE DerivedNamespaces(Namespace BLOB PRIMARY KEY,Bytes INTEGER NOT NULL,Charge INTEGER NOT NULL) WITHOUT ROWID",
        "CREATE TABLE DerivedFields(Id INTEGER PRIMARY KEY,Namespace BLOB NOT NULL,Store BLOB NOT NULL,Name BLOB NOT NULL,Charge INTEGER NOT NULL)",
        "CREATE UNIQUE INDEX DerivedFieldNames ON DerivedFields(Namespace,Store,Name)",
        "CREATE TABLE DerivedGenerations(Id INTEGER PRIMARY KEY,FieldId INTEGER NOT NULL,State INTEGER NOT NULL,Checkpoint BLOB NOT NULL,Members INTEGER NOT NULL,Booleans INTEGER NOT NULL,Numbers INTEGER NOT NULL,Strings INTEGER NOT NULL,Oversized INTEGER NOT NULL,Charge INTEGER NOT NULL)",
        "CREATE INDEX DerivedGenerationFields ON DerivedGenerations(FieldId,Id)",
        "CREATE INDEX DerivedGenerationWork ON DerivedGenerations(Id) WHERE State IN (1,3)",
        "CREATE TABLE DerivedPrefixes(Id INTEGER PRIMARY KEY,Generation INTEGER NOT NULL,Prefix BLOB NOT NULL,Refs INTEGER NOT NULL,Charge INTEGER NOT NULL)",
        "CREATE UNIQUE INDEX DerivedPrefixOrder ON DerivedPrefixes(Generation,Prefix)",
        "CREATE TABLE DerivedEntries(Id INTEGER PRIMARY KEY,PrefixId INTEGER NOT NULL,Suffix BLOB NOT NULL,RecordKey BLOB NOT NULL,Charge INTEGER NOT NULL)",
        "CREATE UNIQUE INDEX DerivedEntryOrder ON DerivedEntries(PrefixId,Suffix,RecordKey)",
        "CREATE TABLE DerivedMembers(Generation INTEGER NOT NULL,RecordKey BLOB NOT NULL,Type INTEGER NOT NULL,EntryId INTEGER NOT NULL,Charge INTEGER NOT NULL,PRIMARY KEY(Generation,RecordKey)) WITHOUT ROWID",
        "CREATE UNIQUE INDEX DerivedMemberEntries ON DerivedMembers(EntryId) WHERE EntryId<>0"
    };
    return Statements;
}
void Derived::Create() {
    Engine Work(Database,End);
    for(const auto& Text:Schema()) { Work.Tick(); Sql(Database,Text.c_str()); }
    Statement Insert(Database,"INSERT INTO DerivedTotals VALUES(1,1,1,1,1,0,?1,?1)"); Insert.Integer(1,GlobalCharge); Insert.Done();
    // Only this freshly created, empty derived graph may be admitted without
    // retained-state proof. Reopening constructs a new unadmitted instance.
    Verification->Current=DerivedVerification::Phase::Complete;
}
bool Derived::Prepare(const Identity& StoreId, std::string Field, bool Force) {
    Require(VerificationComplete(),Error::StorageUnavailable);
    return Engine(Database,End).Prepare(StoreId,Field,Force);
}
void Derived::Mutate(const Identity& Id, const Value* Old, const Value* New) {
    Require(VerificationComplete(),Error::StorageUnavailable);
    Engine(Database,End).Mutate(Id,Old,New);
}
void Derived::Maintain() {
    Require(VerificationComplete(),Error::StorageUnavailable);
    Engine(Database,End).Maintain();
}
bool Derived::HasWork() {
    if(!VerificationComplete()) return false;
    Engine Work(Database,End); Statement Query(Database,"SELECT 1 FROM DerivedGenerations WHERE State IN (1,3) LIMIT 1"); return Query.Next();
}
Derived::View Derived::Inspect(const Identity& StoreId, const std::string& Name) {
    Engine Work(Database,End); auto Id=StoreIdentity(StoreId); Require(ValidField(Name)); View Result;
    Result.Admitted=VerificationComplete();
    Statement Query(Database,"SELECT Id FROM DerivedFields WHERE Namespace=?1 AND Store=?2 AND Name=?3");
    Query.Blob(1,Namespace(Id)); Query.Blob(2,Id.Store); Query.Blob(3,Name);
    if(!Query.Next()) return Result; Result.FieldId=Query.Number(0); Query.Done(); Work.Field(Result.FieldId);
    for(const auto& Gen:Work.Generations(Result.FieldId)) {
        if(Gen.State==Cleanup || Result.Status==State::Active) continue;
        Result.Generation=Gen.Id; Result.Status=State(Gen.State); Result.Members=Gen.Members; Result.Booleans=Gen.Booleans;
        Result.Numbers=Gen.Numbers; Result.Strings=Gen.Strings; Result.OversizedStrings=Gen.Oversized;
    }
    return Result;
}
Derived::Statistics Derived::Diagnostics() {
    Engine Work(Database,End); Statistics Result;
    Result.Bytes=Scalar(Database,"SELECT Bytes FROM DerivedTotals WHERE Id=1");
    Result.Namespaces=Scalar(Database,"SELECT count(*) FROM DerivedNamespaces"); Result.Fields=Scalar(Database,"SELECT count(*) FROM DerivedFields");
    Result.Active=Scalar(Database,"SELECT count(*) FROM DerivedGenerations WHERE State=2"); Result.Building=Scalar(Database,"SELECT count(*) FROM DerivedGenerations WHERE State=1");
    Result.Cleanup=Scalar(Database,"SELECT count(*) FROM DerivedGenerations WHERE State=3"); Result.Members=Scalar(Database,"SELECT count(*) FROM DerivedMembers");
    Result.Prefixes=Scalar(Database,"SELECT count(*) FROM DerivedPrefixes"); Result.Entries=Scalar(Database,"SELECT count(*) FROM DerivedEntries"); return Result;
}
Derived::VerificationResult Derived::VerifyStep() {
    using Phase=DerivedVerification::Phase;
    if(Verification->Current==Phase::Complete) return VerificationResult::Complete;
    if(Verification->Current==Phase::Invalid) return VerificationResult::DerivedInvalid;
    const auto Started=Verification->Current;
    try { return Engine(Database,End).VerifyStep(*Verification); }
    catch(const SqlFailure&) { InvalidateVerification(); throw; }
    catch(const DerivedStructureInvalid&) { Verification->Current=Phase::Invalid; return VerificationResult::DerivedInvalid; }
    catch(const Failure& Problem) {
        // The graph, orphan and store metadata phases read derived authority
        // only. A controlled structural mismatch there is not a primary or
        // SQLite fault. Primary envelope/identity failures during the later
        // representation pass must retain D21's fatal classification.
        const bool DerivedOnly=Started==Phase::Start || Started==Phase::Namespace ||
            Started==Phase::Field || Started==Phase::Generation || Started==Phase::Members ||
            Started==Phase::Prefixes || Started==Phase::PrefixEntries || Started==Phase::Orphans || Started==Phase::Store ||
            Started==Phase::StoreMembers;
        if(DerivedOnly && Problem.Code==Error::StorageCorrupt) {
            Verification->Current=Phase::Invalid;
            return VerificationResult::DerivedInvalid;
        }
        InvalidateVerification(); throw;
    }
    catch(...) { InvalidateVerification(); throw; }
}

Derived::VerificationResult Engine::VerifyStep(DerivedVerification& Proof) {
    using Phase=DerivedVerification::Phase;
    using Result=Derived::VerificationResult;
    Tick();
    switch(Proof.Current) {
    case Phase::Start: {
        // Backend::CheckSchema has already proved exact SQLite schema and
        // database integrity before primary Ready. Rechecking sqlite_schema
        // here could misclassify a later physical/schema fault as disposable
        // derived structure; SQLite prepare/step failures remain D21-wide.
        Statement Global(Db,"SELECT Id,NextField,NextGeneration,NextPrefix,NextEntry,WorkGeneration,Bytes,Charge FROM DerivedTotals");
        Trusted(Global.Next() && Global.Number(0)==1);
        for(unsigned Index=0;Index<4;++Index) { Proof.Next[Index]=Global.Number(int(Index+1)); Trusted(Proof.Next[Index]>0); }
        Trusted(Global.Number(5)>=0 && Global.Number(5)<Proof.Next[1]);
        Proof.ExpectedGlobal=Global.Number(6);
        Trusted(Proof.ExpectedGlobal>=GlobalCharge && Proof.ExpectedGlobal<=GlobalLimit && Global.Number(7)==GlobalCharge);
        Global.Done(); Proof.Total=GlobalCharge; Proof.Current=Phase::Namespace; return Result::Progress;
    }
    case Phase::Namespace: {
        Statement Find(Db,Proof.NamespaceStarted
            ? "SELECT Namespace,Bytes,Charge FROM DerivedNamespaces WHERE Namespace>?1 ORDER BY Namespace LIMIT 1"
            : "SELECT Namespace,Bytes,Charge FROM DerivedNamespaces ORDER BY Namespace LIMIT 1");
        if(Proof.NamespaceStarted) Find.Blob(1,Proof.Namespace);
        if(!Find.Next()) {
            Trusted(Proof.Total==Proof.ExpectedGlobal);
            Proof.Current=Phase::Orphans; return Result::Progress;
        }
        Proof.NamespaceStarted=true;
        Proof.Namespace=Find.Data(0,66); StoredIdentity(Proof.Namespace,"S","K");
        Proof.Local=NamespaceCharge(Proof.Namespace); Proof.ExpectedLocal=Find.Number(1);
        Trusted(Find.Number(2)==Proof.Local && Proof.ExpectedLocal>=Proof.Local && Proof.ExpectedLocal<=NamespaceLimit);
        Proof.FieldStore.clear(); Proof.FieldName.clear(); Proof.PreviousStore.clear(); Proof.InStore=0;
        Proof.Current=Phase::Field; return Result::Progress;
    }
    case Phase::Field: {
        Statement Find(Db,"SELECT Id,Namespace,Store,Name,Charge FROM DerivedFields WHERE Namespace=?1 AND (Store,Name)>(?2,?3) ORDER BY Store,Name LIMIT 1");
        Find.Blob(1,Proof.Namespace); Find.Blob(2,Proof.FieldStore); Find.Blob(3,Proof.FieldName);
        if(!Find.Next()) {
            Trusted(Proof.Local==Proof.ExpectedLocal); Proof.Total=Add(Proof.Total,Proof.Local);
            Trusted(Proof.Total<=GlobalLimit); Proof.Current=Phase::Namespace; return Result::Progress;
        }
        Proof.Field=ReadField(Find); Trusted(Proof.Field.Ns==Proof.Namespace && Proof.Field.Id<Proof.Next[0]);
        ++Proof.Fields; Proof.Local=Add(Proof.Local,Proof.Field.Charge);
        if(Proof.Field.Store!=Proof.PreviousStore) { Proof.PreviousStore=Proof.Field.Store; Proof.InStore=0; }
        Trusted(++Proof.InStore<=8);
        Proof.FieldStore=Proof.Field.Store; Proof.FieldName=Proof.Field.Name;
        Proof.LastGeneration=0; Proof.ActiveCount=0; Proof.BuildingCount=0; Proof.FieldGenerationCount=0;
        Proof.Current=Phase::Generation; return Result::Progress;
    }
    case Phase::Generation: {
        Statement Find(Db,"SELECT Id,FieldId,State,Checkpoint,Members,Booleans,Numbers,Strings,Oversized,Charge FROM DerivedGenerations WHERE FieldId=?1 AND Id>?2 ORDER BY Id LIMIT 1");
        Find.Integer(1,Proof.Field.Id); Find.Integer(2,Proof.LastGeneration);
        if(!Find.Next()) {
            Trusted(Proof.ActiveCount<=1 && Proof.BuildingCount<=1);
            Proof.Current=Phase::Field; return Result::Progress;
        }
        Proof.Generation=ReadGeneration(Find);
        Trusted(Proof.Generation.Field==Proof.Field.Id && Proof.Generation.Id<Proof.Next[1] &&
            Proof.LastGeneration<Proof.Generation.Id && ++Proof.FieldGenerationCount<=2);
        Proof.LastGeneration=Proof.Generation.Id; ++Proof.Generations;
        Proof.ActiveCount+=Proof.Generation.State==Active;
        Proof.BuildingCount+=Proof.Generation.State==Building;
        Trusted(Proof.ActiveCount<=1 && Proof.BuildingCount<=1 && Proof.Generations<=Proof.Next[1]-1);
        Proof.Local=Add(Proof.Local,Proof.Generation.Charge);
        Proof.MemberKey.clear(); Proof.PrefixKey.clear();
        Proof.GenerationMembers=Proof.GenerationUsable=Proof.GenerationBooleans=Proof.GenerationNumbers=0;
        Proof.GenerationStrings=Proof.GenerationOversized=Proof.GenerationEntries=0;
        Proof.Current=Phase::Members; return Result::Progress;
    }
    case Phase::Members: {
        Statement Rows(Db,"SELECT RecordKey FROM DerivedMembers WHERE Generation=?1 AND RecordKey>?2 ORDER BY RecordKey LIMIT 32");
        Rows.Integer(1,Proof.Generation.Id); Rows.Blob(2,Proof.MemberKey);
        unsigned Count=0;
        while(Rows.Next()) {
            Tick(); ++Count; auto Key=Rows.Data(0,128); StoredIdentity(Proof.Namespace,Proof.Field.Store,Key);
            auto Item=Member(Proof.Generation.Id,Key);
            Trusted(Item.Found && Item.Entry<Proof.Next[3] && ++Proof.GenerationMembers<=10000);
            ++Proof.Members; Proof.Local=Add(Proof.Local,Item.Charge);
            Proof.GenerationBooleans+=Item.Type==1; Proof.GenerationNumbers+=Item.Type==2;
            Proof.GenerationStrings+=Item.Type==3; Proof.GenerationOversized+=Item.Entry==0;
            if(Item.Entry) {
                auto EntryValue=Entry(Item.Entry);
                auto PrefixValue=Prefix(EntryValue.Prefix);
                Trusted(PrefixValue.Id<Proof.Next[2] && PrefixValue.Generation==Proof.Generation.Id && EntryValue.Key==Key);
                auto Full=PrefixValue.Prefix+EntryValue.Suffix; ValidateSortKey(Full);
                Trusted(int64_t(uint8_t(Full[0]))==Item.Type && PrefixValue.Prefix.size()==std::min<size_t>(512,Full.size()));
                ++Proof.GenerationUsable; ++Proof.Entries; Proof.Local=Add(Proof.Local,EntryValue.Charge);
            }
            Proof.MemberKey=std::move(Key);
        }
        if(Count<32) {
            Trusted(Proof.GenerationMembers==Proof.Generation.Members &&
                Proof.GenerationBooleans==Proof.Generation.Booleans && Proof.GenerationNumbers==Proof.Generation.Numbers &&
                Proof.GenerationStrings==Proof.Generation.Strings && Proof.GenerationOversized==Proof.Generation.Oversized);
            Proof.Current=Phase::Prefixes;
        }
        return Result::Progress;
    }
    case Phase::Prefixes: {
        Statement Rows(Db,"SELECT Prefix FROM DerivedPrefixes WHERE Generation=?1 AND Prefix>?2 ORDER BY Prefix LIMIT 1");
        Rows.Integer(1,Proof.Generation.Id); Rows.Blob(2,Proof.PrefixKey);
        if(!Rows.Next()) {
            Trusted(Proof.GenerationEntries==Proof.GenerationUsable); Proof.Current=Phase::Generation;
            return Result::Progress;
        }
        auto Key=Rows.Data(0,512);
        Statement Find(Db,"SELECT Id FROM DerivedPrefixes WHERE Generation=?1 AND Prefix=?2");
        Find.Integer(1,Proof.Generation.Id); Find.Blob(2,Key); Trusted(Find.Next()); auto Item=Prefix(Find.Number(0)); Find.Done();
        Trusted(Item.Id<Proof.Next[2] && Item.Generation==Proof.Generation.Id && Item.Prefix==Key);
        ++Proof.Prefixes; Proof.Local=Add(Proof.Local,Item.Charge);
        Proof.PrefixKey=std::move(Key); Proof.CurrentPrefix=Item.Id;
        Proof.ExpectedReferences=Item.References; Proof.CurrentReferences=0;
        Proof.EntrySuffix.clear(); Proof.EntryKey.clear(); Proof.Current=Phase::PrefixEntries;
        return Result::Progress;
    }
    case Phase::PrefixEntries: {
        Statement Rows(Db,"SELECT Suffix,RecordKey FROM DerivedEntries WHERE PrefixId=?1 AND (Suffix,RecordKey)>(?2,?3) ORDER BY Suffix,RecordKey LIMIT 32");
        Rows.Integer(1,Proof.CurrentPrefix); Rows.Blob(2,Proof.EntrySuffix); Rows.Blob(3,Proof.EntryKey);
        unsigned Count=0;
        while(Rows.Next()) {
            Tick(); ++Count; Proof.EntrySuffix=Rows.Data(0,513); Proof.EntryKey=Rows.Data(1,128);
            Trusted(ValidText(Proof.EntryKey,128,true));
            Trusted(++Proof.CurrentReferences<=Proof.ExpectedReferences);
        }
        if(Count<32) {
            Trusted(Proof.CurrentReferences==Proof.ExpectedReferences);
            Proof.GenerationEntries=Add(Proof.GenerationEntries,Proof.CurrentReferences);
            Proof.Current=Phase::Prefixes;
        }
        return Result::Progress;
    }
    case Phase::Orphans: {
        static const char* First[]={
            "SELECT Id FROM DerivedFields ORDER BY Id LIMIT 32",
            "SELECT Id FROM DerivedGenerations ORDER BY Id LIMIT 32",
            "SELECT Id FROM DerivedPrefixes ORDER BY Id LIMIT 32",
            "SELECT Id FROM DerivedEntries ORDER BY Id LIMIT 32",
            "SELECT Generation,RecordKey FROM DerivedMembers ORDER BY Generation,RecordKey LIMIT 32"
        };
        static const char* Later[]={
            "SELECT Id FROM DerivedFields WHERE Id>?1 ORDER BY Id LIMIT 32",
            "SELECT Id FROM DerivedGenerations WHERE Id>?1 ORDER BY Id LIMIT 32",
            "SELECT Id FROM DerivedPrefixes WHERE Id>?1 ORDER BY Id LIMIT 32",
            "SELECT Id FROM DerivedEntries WHERE Id>?1 ORDER BY Id LIMIT 32",
            "SELECT Generation,RecordKey FROM DerivedMembers WHERE (Generation,RecordKey)>(?1,?2) ORDER BY Generation,RecordKey LIMIT 32"
        };
        const int64_t Expected[]={Proof.Fields,Proof.Generations,Proof.Prefixes,Proof.Entries,Proof.Members};
        if(Proof.OrphanTable==5) { Proof.Current=Phase::Store; return Result::Progress; }
        Statement Rows(Db,Proof.OrphanCount?Later[Proof.OrphanTable]:First[Proof.OrphanTable]);
        if(Proof.OrphanCount) {
            Rows.Integer(1,Proof.OrphanId);
            if(Proof.OrphanTable==4) Rows.Blob(2,Proof.OrphanKey);
        }
        unsigned Count=0;
        while(Rows.Next()) {
            Tick(); ++Count; Proof.OrphanId=Rows.Number(0);
            if(Proof.OrphanTable==4) Proof.OrphanKey=Rows.Data(1,128);
            ++Proof.OrphanCount; Trusted(Proof.OrphanCount<=Expected[Proof.OrphanTable]);
        }
        if(Count<32) {
            Trusted(Proof.OrphanCount==Expected[Proof.OrphanTable]);
            ++Proof.OrphanTable; Proof.OrphanId=Proof.OrphanCount=0; Proof.OrphanKey.clear();
        }
        return Result::Progress;
    }
    case Phase::Store: {
        Statement Find(Db,"SELECT Namespace,Store FROM DerivedFields WHERE (Namespace,Store)>(?1,?2) ORDER BY Namespace,Store LIMIT 1");
        Find.Blob(1,Proof.StoreNamespace); Find.Blob(2,Proof.StoreName);
        if(!Find.Next()) { Proof.Current=Phase::Complete; return Result::Complete; }
        Proof.StoreNamespace=Find.Data(0,66); Proof.StoreName=Find.Data(1,64);
        Proof.Candidates.clear(); Proof.CandidateIndex=0; Proof.StoreMemberKey.clear(); Proof.StorePrimaryKey.clear(); Proof.PrimaryCount=0;
        Statement Gens(Db,"SELECT G.Id,G.FieldId,G.State,G.Checkpoint,G.Members,G.Booleans,G.Numbers,G.Strings,G.Oversized,G.Charge,F.Id,F.Namespace,F.Store,F.Name,F.Charge FROM DerivedFields F CROSS JOIN DerivedGenerations G INDEXED BY DerivedGenerationFields ON G.FieldId=F.Id WHERE F.Namespace=?1 AND F.Store=?2 ORDER BY G.Id LIMIT 17");
        Gens.Blob(1,Proof.StoreNamespace); Gens.Blob(2,Proof.StoreName);
        while(Gens.Next()) {
            Tick(); Trusted(Proof.Candidates.size()<16);
            auto Gen=ReadGeneration(Gens);
            auto FieldValue=ReadField(Gens,10);
            Trusted(Gen.Field==FieldValue.Id && FieldValue.Ns==Proof.StoreNamespace && FieldValue.Store==Proof.StoreName);
            Proof.Candidates.push_back({std::move(Gen),std::move(FieldValue),false});
        }
        Proof.Current=Proof.Candidates.empty()?Phase::Store:Phase::StoreMembers;
        return Result::Progress;
    }
    case Phase::StoreMembers: {
        while(Proof.CandidateIndex<Proof.Candidates.size() && Proof.Candidates[Proof.CandidateIndex].Generation.State==Cleanup) {
            ++Proof.CandidateIndex; Proof.StoreMemberKey.clear();
        }
        if(Proof.CandidateIndex==Proof.Candidates.size()) { Proof.Current=Phase::StorePrimary; return Result::Progress; }
        auto& Candidate=Proof.Candidates[Proof.CandidateIndex];
        Statement Rows(Db,"SELECT RecordKey FROM DerivedMembers WHERE Generation=?1 AND RecordKey>?2 ORDER BY RecordKey LIMIT 32");
        Rows.Integer(1,Candidate.Generation.Id); Rows.Blob(2,Proof.StoreMemberKey);
        unsigned Count=0;
        while(Rows.Next()) {
            Tick(); ++Count; auto Key=Rows.Data(0,128); Proof.StoreMemberKey=Key;
            Statement Find(Db,"SELECT 1 FROM Records WHERE Namespace=?1 AND Store=?2 AND Key=?3");
            Find.Blob(1,Proof.StoreNamespace); Find.Blob(2,Proof.StoreName); Find.Blob(3,Key);
            if(!Find.Next()) Candidate.Mismatch=true;
        }
        if(Count<32) { ++Proof.CandidateIndex; Proof.StoreMemberKey.clear(); }
        return Result::Progress;
    }
    case Phase::StorePrimary: {
        Statement Rows(Db,"SELECT Key,Envelope FROM Records WHERE Namespace=?1 AND Store=?2 AND Key>?3 ORDER BY Key LIMIT 4");
        Rows.Blob(1,Proof.StoreNamespace); Rows.Blob(2,Proof.StoreName); Rows.Blob(3,Proof.StorePrimaryKey);
        unsigned Count=0;
        while(Rows.Next()) {
            Tick(); ++Count; Trusted(++Proof.PrimaryCount<=10000);
            auto Key=Rows.Data(0,128), Envelope=Rows.Data(1,MaximumEnvelope);
            auto Id=StoredIdentity(Proof.StoreNamespace,Proof.StoreName,Key);
            auto Root=Decode(Id,Bytes(Envelope.begin(),Envelope.end()),End);
            std::vector<std::pair<std::string,ExtractedScalar>> Extracted;
            for(auto& Candidate:Proof.Candidates) {
                if(Candidate.Generation.State==Cleanup) continue;
                MemberRow Item;
                try { Item=Member(Candidate.Generation.Id,Key); }
                catch(const SqlFailure&) { throw; }
                catch(const Failure& Problem) {
                    if(Problem.Code==Error::StorageCorrupt) throw DerivedStructureInvalid{};
                    throw;
                }
                if(Candidate.Generation.State!=Active && Less(Candidate.Generation.Checkpoint,Key) && !Item.Found) continue;
                auto Found=std::find_if(Extracted.begin(),Extracted.end(),[&](const auto& Entry) { return Entry.first==Candidate.Field.Name; });
                if(Found==Extracted.end()) {
                    Trusted(Extracted.size()<8);
                    Extracted.emplace_back(Candidate.Field.Name,ExtractScalar(*Root,Candidate.Field.Name));
                    Found=std::prev(Extracted.end());
                }
                try { Match(Candidate.Generation.Id,Key,Item,Found->second); }
                catch(const LogicalMismatch&) { Candidate.Mismatch=true; }
                catch(const SqlFailure&) { throw; }
                catch(const Failure& Problem) {
                    if(Problem.Code==Error::StorageCorrupt) throw DerivedStructureInvalid{};
                    throw;
                }
            }
            Proof.StorePrimaryKey=std::move(Key);
        }
        if(Count<4) { Proof.Current=Phase::StoreWithdraw; }
        return Result::Progress;
    }
    case Phase::StoreWithdraw: {
        // This is the only write step. The caller must commit it before the
        // next VerifyStep; Complete is never published from this transaction.
        for(const auto& Candidate:Proof.Candidates) if(Candidate.Mismatch) Withdraw(Candidate.Generation.Id);
        Proof.Candidates.clear(); Proof.Current=Phase::Store; return Result::Progress;
    }
    case Phase::Complete: return Result::Complete;
    case Phase::Invalid: return Result::DerivedInvalid;
    }
    throw Failure(Error::StorageError);
}

}
