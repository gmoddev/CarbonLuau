#include "Backend.hpp"
#include "Derived.hpp"
#include "Sortable.hpp"
#include "sqlite3.h"
#include <algorithm>
#include <cstdio>
#include <map>
#include <set>
#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#endif

using namespace CarbonLuau::Persistence;
namespace {
void Check(bool Condition, const char* Message)
{ if (!Condition) throw std::runtime_error(Message); }
Deadline Until() { return Clock::now() + std::chrono::seconds(30); }
std::filesystem::path Fixture(const std::filesystem::path& Parent, const char* Name)
{
    const auto Path = Parent / Name;
    Check(std::filesystem::create_directory(Path), "fresh fixture directory");
    return Path;
}
struct Connection {
    sqlite3* Db = nullptr;
    explicit Connection(const std::filesystem::path& Folder, bool Writable = false)
    {
        const int Flags = Writable ? SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE : SQLITE_OPEN_READONLY;
        const int Code = sqlite3_open_v2((Folder / "store.sqlite3").u8string().c_str(), &Db, Flags, nullptr);
        if (Code != SQLITE_OK) { sqlite3_close(Db); Db = nullptr; throw std::runtime_error("fixture connection"); }
    }
    ~Connection() { sqlite3_close(Db); }
    Connection(const Connection&) = delete;
    Connection& operator=(const Connection&) = delete;
    void Exec(const char* Sql)
    { Check(sqlite3_exec(Db, Sql, nullptr, nullptr, nullptr) == SQLITE_OK, "fixture SQL"); }
};
struct Statement {
    sqlite3_stmt* Handle = nullptr;
    Statement(Connection& Db, const char* Sql)
    { Check(sqlite3_prepare_v2(Db.Db, Sql, -1, &Handle, nullptr) == SQLITE_OK, "fixture prepare"); }
    ~Statement() { sqlite3_finalize(Handle); }
    Statement(const Statement&) = delete;
    Statement& operator=(const Statement&) = delete;
    void Blob(int Index, const std::string& Data)
    { Check(sqlite3_bind_blob(Handle, Index, Data.data(), int(Data.size()), SQLITE_TRANSIENT) == SQLITE_OK, "fixture bind"); }
    void Blob(int Index, const Bytes& Data)
    { Check(sqlite3_bind_blob(Handle, Index, Data.data(), int(Data.size()), SQLITE_TRANSIENT) == SQLITE_OK, "fixture bind bytes"); }
    void Integer(int Index, int64_t Data)
    { Check(sqlite3_bind_int64(Handle, Index, Data) == SQLITE_OK, "fixture bind integer"); }
    bool Next()
    {
        const int Code = sqlite3_step(Handle);
        Check(Code == SQLITE_ROW || Code == SQLITE_DONE, "fixture step");
        return Code == SQLITE_ROW;
    }
    void Reset()
    {
        Check(sqlite3_reset(Handle) == SQLITE_OK && sqlite3_clear_bindings(Handle) == SQLITE_OK,
            "fixture statement reset");
    }
    int64_t Number(int Index)
    {
        Check(sqlite3_column_type(Handle, Index) == SQLITE_INTEGER, "fixture integer type");
        return sqlite3_column_int64(Handle, Index);
    }
    Bytes Data(int Index)
    {
        Check(sqlite3_column_type(Handle, Index) == SQLITE_BLOB, "fixture blob type");
        const int Count = sqlite3_column_bytes(Handle, Index);
        const auto* Data = static_cast<const uint8_t*>(sqlite3_column_blob(Handle, Index));
        return Count ? Bytes(Data, Data + Count) : Bytes{};
    }
};
int64_t Scalar(Connection& Db, const char* Sql)
{
    Statement Query(Db, Sql); Check(Query.Next(), "fixture scalar row");
    const auto Result = Query.Number(0); Check(!Query.Next(), "fixture single scalar"); return Result;
}
std::string Namespace(const Identity& Id)
{ return std::string(1, Id.Addon ? '\1' : '\0') + Id.Package; }
Value Numeric(double Input)
{ Value Result; Result.Type = Kind::Number; Result.Number = Input; return Result; }
Value Text(const std::string& Input)
{ Value Result; Result.Type = Kind::String; Result.String = Input; return Result; }
Value Flag(bool Input)
{ Value Result; Result.Type = Kind::Boolean; Result.Boolean = Input; return Result; }
Value Record(const Value& Scalar)
{
    Value Result; Result.Map.emplace_back("Field", std::make_shared<Value>(Scalar)); return Result;
}
Identity Key(const Identity& StoreId, const std::string& Name)
{ auto Result = StoreId; Result.Key = Name; return Result; }
void Set(Backend& Store, const Identity& Id, const Value& Input)
{
    const auto Envelope = Encode(Id, Input, Until());
    const auto Result = Store.Execute(Operation::Set, Id, Envelope, Until());
    Check(Result.Code == Error::None && Store.Available(), "primary Set");
    const auto Read = Store.Execute(Operation::Get, Id, {}, Until());
    Check(Read.Code == Error::None && Read.Found && Read.Envelope == Envelope, "primary exact envelope");
}
void Remove(Backend& Store, const Identity& Id)
{
    Check(Store.Execute(Operation::Remove, Id, {}, Until()).Code == Error::None, "primary Remove");
    const auto Read = Store.Execute(Operation::Get, Id, {}, Until());
    Check(Read.Code == Error::None && !Read.Found, "primary removed");
}
void Drain(Backend& Store)
{
    for (unsigned Batch = 0; Batch < 4096; ++Batch) {
        if (!Store.HasDerivedWork()) { Check(Store.Available(), "maintenance health"); return; }
        Check(Store.MaintainDerived(Until()) && Store.Available(), "maintenance batch");
    }
    throw std::runtime_error("maintenance failed to converge within fixture bound");
}
void Verify(Backend& Store)
{
    // A reopened process starts with no retained-derived admission. Complete
    // verification and any pending maintenance before continuing the fixture.
    for (unsigned Step = 0; Step < 65536; ++Step) {
        if (!Store.HasDerivedWork()) { Check(Store.Available(), "verification health"); return; }
        Check(Store.MaintainDerived(Until()) && Store.Available(), "derived verification step");
    }
    throw std::runtime_error("derived verification failed to converge within fixture bound");
}
void AdmitField(Backend& Store, const Identity& Id, const std::string& Field)
{
    for (unsigned Step = 0; Step < 131072; ++Step) {
        if (Store.PrepareDerived(Id, Field, Until())) return;
        Check(Store.MaintainDerived(Until()) && Store.Available(), "retained field verification step");
    }
    throw std::runtime_error("retained field was not admitted within fixture bound");
}

using OrderedEntry = std::pair<Bytes, std::string>;
struct Inspection {
    Derived::View View;
    Derived::Statistics Stats;
    std::string Checkpoint;
    std::vector<OrderedEntry> Entries;
};
Inspection Inspect(const std::filesystem::path& Folder, const Identity& Id, const std::string& Field = "Field")
{
    Connection Db(Folder);
    auto End = Until();
    sqlite3_progress_handler(Db.Db, 1000, [](void* Context) {
        return Clock::now() >= *static_cast<Deadline*>(Context) ? 1 : 0;
    }, &End);
    Db.Exec("BEGIN");
    Derived Engine(Db.Db, End);
    // This is a new diagnostic engine, not the backend's admitted lifetime.
    // Inspect durable rows without manufacturing current-process admission.
    Inspection Result; Result.View = Engine.Inspect(Id, Field); Result.Stats = Engine.Diagnostics();
    Check(Scalar(Db, "SELECT Bytes FROM DerivedTotals") ==
        Scalar(Db, "SELECT (SELECT Charge FROM DerivedTotals)"
            "+coalesce((SELECT sum(Charge) FROM DerivedNamespaces),0)"
            "+coalesce((SELECT sum(Charge) FROM DerivedFields),0)"
            "+coalesce((SELECT sum(Charge) FROM DerivedGenerations),0)"
            "+coalesce((SELECT sum(Charge) FROM DerivedPrefixes),0)"
            "+coalesce((SELECT sum(Charge) FROM DerivedEntries),0)"
            "+coalesce((SELECT sum(Charge) FROM DerivedMembers),0)"), "complete derived charge conservation");
    if (Result.View.Generation) {
        Statement Generation(Db, "SELECT Checkpoint FROM DerivedGenerations WHERE Id=?1");
        Generation.Integer(1, Result.View.Generation); Check(Generation.Next(), "generation checkpoint");
        const auto Checkpoint = Generation.Data(0);
        Result.Checkpoint.assign(Checkpoint.begin(), Checkpoint.end());
        Check(!Generation.Next(), "unique generation");
        Statement Rows(Db, "SELECT P.Prefix,E.Suffix,E.RecordKey FROM DerivedPrefixes P "
            "JOIN DerivedEntries E ON E.PrefixId=P.Id WHERE P.Generation=?1 "
            "ORDER BY P.Prefix,E.Suffix,E.RecordKey");
        Rows.Integer(1, Result.View.Generation);
        while (Rows.Next()) {
            auto Prefix = Rows.Data(0); const auto Suffix = Rows.Data(1); const auto RecordKey = Rows.Data(2);
            Check(!Prefix.empty() && Prefix.size() <= 512 && Suffix.size() <= 513, "split widths");
            Check(Suffix.empty() || Prefix.size() == 512, "canonical split");
            Prefix.insert(Prefix.end(), Suffix.begin(), Suffix.end());
            Result.Entries.emplace_back(std::move(Prefix), std::string(RecordKey.begin(), RecordKey.end()));
        }
    }
    Db.Exec("COMMIT");
    return Result;
}
void CheckProjection(const std::filesystem::path& Folder, const Identity& Id,
    bool AllowWithdrawn = false, const std::string& Field = "Field")
{
    const auto Actual = Inspect(Folder, Id, Field);
    if (AllowWithdrawn && Actual.View.Status != Derived::State::Active) {
        Check(!Actual.View.Queryable(Kind::Number) && !Actual.View.Queryable(Kind::String) &&
            !Actual.View.Queryable(Kind::Boolean), "withdrawn generation is not queryable");
        return;
    }
    Check(Actual.View.Status == Derived::State::Active, "complete active generation");
    std::vector<OrderedEntry> Expected;
    int64_t Booleans = 0, Numbers = 0, Strings = 0, Oversized = 0;
    {
        Connection Db(Folder);
        Statement Rows(Db, "SELECT Key,Envelope FROM Records WHERE Namespace=?1 AND Store=?2 ORDER BY Key");
        Rows.Blob(1, Namespace(Id)); Rows.Blob(2, Id.Store);
        while (Rows.Next()) {
            const auto KeyBytes = Rows.Data(0), Envelope = Rows.Data(1);
            const std::string Name(KeyBytes.begin(), KeyBytes.end());
            const auto Root = Decode(Key(Id, Name), Envelope, Until());
            const auto Scalar = ExtractScalar(*Root, Field);
            if (!Scalar.Represented) continue;
            Booleans += Scalar.Type == Kind::Boolean; Numbers += Scalar.Type == Kind::Number;
            Strings += Scalar.Type == Kind::String; Oversized += Scalar.Type == Kind::String && !Scalar.Usable;
            if (Scalar.Usable) Expected.emplace_back(Scalar.SortKey, Name);
        }
    }
    std::sort(Expected.begin(), Expected.end());
    Check(Actual.Entries == Expected, "derived entries exactly match current primary state/order");
    Check(Actual.View.Booleans == Booleans && Actual.View.Numbers == Numbers && Actual.View.Strings == Strings &&
        Actual.View.OversizedStrings == Oversized && Actual.View.Members == Booleans + Numbers + Strings,
        "represented category/completeness accounting");
    Check(!Actual.View.Admitted && !Actual.View.Queryable(Kind::Number) &&
        !Actual.View.Queryable(Kind::Boolean) && !Actual.View.Queryable(Kind::String),
        "durable ACTIVE metadata does not admit an independent process-local view");
    Check(Actual.View.OversizedStrings == Oversized, "stored typed availability matches primary");
}

// Exact pre-2A schema, deliberately independent of current production Schema().
// Real Foundation 1 envelopes and its original logical accounting are retained.
void Legacy(const std::filesystem::path& Folder,
    const std::vector<std::pair<Identity, Value>>& Records)
{
    Connection Db(Folder, true);
    Db.Exec("PRAGMA page_size=4096; PRAGMA journal_mode=PERSIST; PRAGMA synchronous=EXTRA;"
        "BEGIN IMMEDIATE;"
        "CREATE TABLE Records(Namespace BLOB NOT NULL,Store BLOB NOT NULL,Key BLOB NOT NULL,Envelope BLOB NOT NULL,Charge INTEGER NOT NULL,PRIMARY KEY(Namespace,Store,Key)) WITHOUT ROWID;"
        "CREATE TABLE Quotas(Namespace BLOB PRIMARY KEY,Bytes INTEGER NOT NULL,Keys INTEGER NOT NULL,Stores INTEGER NOT NULL) WITHOUT ROWID;"
        "CREATE TABLE Totals(Id INTEGER PRIMARY KEY CHECK(Id=1),Bytes INTEGER NOT NULL,Keys INTEGER NOT NULL,Namespaces INTEGER NOT NULL);");
    struct Counts { int64_t Charge = 0, Keys = 0; std::set<std::string> Stores; };
    std::map<std::string, Counts> Quotas;
    int64_t Total = 0;
    for (const auto& Row : Records) {
        const auto& Id = Row.first;
        const auto Envelope = Encode(Id, Row.second, Until());
        const int64_t Charge = int64_t(Id.Store.size() + Id.Key.size() + Envelope.size());
        Statement Insert(Db, "INSERT INTO Records VALUES(?1,?2,?3,?4,?5)");
        Insert.Blob(1, Namespace(Id)); Insert.Blob(2, Id.Store); Insert.Blob(3, Id.Key);
        Insert.Blob(4, Envelope); Insert.Integer(5, Charge); Check(!Insert.Next(), "legacy record insert");
        auto& Count = Quotas[Namespace(Id)]; Count.Charge += Charge; ++Count.Keys; Count.Stores.insert(Id.Store);
        Total += Charge;
    }
    for (const auto& Row : Quotas) {
        Statement Insert(Db, "INSERT INTO Quotas VALUES(?1,?2,?3,?4)");
        Insert.Blob(1, Row.first); Insert.Integer(2, Row.second.Charge);
        Insert.Integer(3, Row.second.Keys); Insert.Integer(4, int64_t(Row.second.Stores.size()));
        Check(!Insert.Next(), "legacy quotas");
    }
    {
        Statement Insert(Db, "INSERT INTO Totals VALUES(1,?1,?2,?3)");
        Insert.Integer(1, Total); Insert.Integer(2, int64_t(Records.size())); Insert.Integer(3, int64_t(Quotas.size()));
        Check(!Insert.Next(), "legacy totals");
    }
    Db.Exec("PRAGMA application_id=1129074756; PRAGMA user_version=1; COMMIT");
}

void Upgrade(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "upgrade");
    const std::vector<std::pair<Identity, Value>> Records{
        {{false, "", "Store", "Same"}, Record(Numeric(-0.0))},
        {{true, "addon-a", "Store", "Same"}, Record(Text(std::string(1025, 'x')))},
        {{true, "addon-b", "Store", "Same"}, Flag(false)},
        {{true, "addon-a", "Other", "Same"}, Value{}}};
    Legacy(Folder, Records);
    int64_t Charge;
    {
        Connection Db(Folder);
        Check(Scalar(Db, "PRAGMA user_version") == 1, "real old schema fixture");
        Charge = Scalar(Db, "SELECT Bytes FROM Totals");
        Check(Scalar(Db, "SELECT count(*) FROM sqlite_schema WHERE type='table'") == 3, "old tables only");
    }
    for (unsigned Reopen = 0; Reopen < 2; ++Reopen) {
        {
            Backend Store(Folder, Until());
            Check(Store.Available(), "upgrade primary Ready");
            for (const auto& Row : Records) {
                const auto Read = Store.Execute(Operation::Get, Row.first, {}, Until());
                Check(Read.Code == Error::None && Read.Found &&
                    Read.Envelope == Encode(Row.first, Row.second, Until()), "upgrade primary fidelity/identity");
            }
            Verify(Store);
            Check(!Store.HasDerivedWork(), "upgrade creates no field demand");
        }
        Connection Db(Folder);
        Check(Scalar(Db, "PRAGMA user_version") == 2, "schema upgraded");
        Check(Scalar(Db, "SELECT Bytes FROM Totals") == Charge &&
            Scalar(Db, "SELECT Keys FROM Totals") == int64_t(Records.size()) &&
            Scalar(Db, "SELECT Namespaces FROM Totals") == 3, "primary accounting unchanged");
        Check(Scalar(Db, "SELECT count(*) FROM DerivedFields") == 0, "upgrade must not allocate fields");
    }
    {
        Connection Db(Folder, true); Db.Exec("PRAGMA journal_mode=PERSIST; PRAGMA user_version=999");
    }
    bool Rejected = false;
    try { Backend Store(Folder, Until()); }
    catch (const Failure& Problem) { Rejected = Problem.Code == Error::FormatUnsupported; }
    Check(Rejected, "unknown version fails closed");
    Connection Db(Folder);
    Check(Scalar(Db, "PRAGMA user_version") == 999 && Scalar(Db, "SELECT Bytes FROM Totals") == Charge,
        "unsupported database preserved");
}

void Fresh(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "fresh");
    const Identity Id{false, "", "Store", ""};
    { Backend Store(Folder, Until()); Check(Store.Available() && !Store.HasDerivedWork(), "fresh empty derived graph admitted"); }
    const auto Empty = Inspect(Folder, Id);
    Check(Empty.View.FieldId == 0 && Empty.Stats.Fields == 0 && Empty.Stats.Namespaces == 0 &&
        Empty.Stats.Building == 0 && Empty.Stats.Active == 0 && Empty.Stats.Cleanup == 0 &&
        Empty.Stats.Members == 0 && Empty.Stats.Prefixes == 0 && Empty.Stats.Entries == 0,
        "fresh backend has global metadata but no derived data");
    Connection Db(Folder);
    Check(Scalar(Db, "SELECT count(*) FROM DerivedTotals") == 1 &&
        Scalar(Db, "SELECT Bytes=Charge AND Charge>0 FROM DerivedTotals") == 1,
        "empty global metadata is charged");
}

void FieldLimit(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "fields");
    const Identity Id{false, "", "Empty", ""};
    {
        Backend Store(Folder, Until());
        Check(!Store.HasDerivedWork() && Store.Available(), "new empty derived graph admitted");
        for (unsigned Index = 0; Index < 8; ++Index)
            Check(Store.PrepareDerived(Id, "Field" + std::to_string(Index), Until()), "first eight fields");
        Check(Store.PrepareDerived(Id, "Field0", Until()), "duplicate field uses same slot");
        Check(!Store.PrepareDerived(Id, "Ninth", Until()) && Store.Available(), "ninth field rejected without backend failure");
        Drain(Store);
    }
    {
        Connection Db(Folder);
        Check(Scalar(Db, "SELECT count(*) FROM DerivedFields") == 8, "exact retained field ceiling");
        Check(Scalar(Db, "SELECT Bytes FROM Totals") == 0 && Scalar(Db, "SELECT Keys FROM Totals") == 0 &&
            Scalar(Db, "SELECT Namespaces FROM Totals") == 0 && Scalar(Db, "SELECT count(*) FROM Quotas") == 0,
            "derived-only demand leaves primary quotas empty");
    }
    {
        Backend Store(Folder, Until());
        Verify(Store);
        Check(!Store.PrepareDerived(Id, "Ninth", Until()) && Store.Available(), "field ceiling survives restart");
        auto Other = Id; Other.Store = "Other";
        Check(Store.PrepareDerived(Other, "Ninth", Until()), "field slots scoped to store");
        Drain(Store);
    }
}

std::string RecordKey(unsigned Index)
{
    char Buffer[16]; std::snprintf(Buffer, sizeof(Buffer), "K%03u", Index); return Buffer;
}
void Online(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "online");
    const Identity Id{false, "", "Store", ""};
    {
        Backend Store(Folder, Until());
        Verify(Store);
        for (unsigned Index = 0; Index < 96; ++Index)
            Set(Store, Key(Id, RecordKey(Index)), Record(Numeric(Index)));
        Check(Store.PrepareDerived(Id, "Field", Until()), "prepare online field");
    }
    const auto Before = Inspect(Folder, Id);
    Check(Before.View.Status == Derived::State::Building && Before.View.Members == 0 &&
        Before.Checkpoint.empty(), "durable unstarted build");
    {
        Backend Store(Folder, Until());
        AdmitField(Store, Id, "Field");
        Check(Store.MaintainDerived(Until()), "one build batch");
    }
    const auto Batch = Inspect(Folder, Id);
    Check(Batch.View.Status == Derived::State::Building && Batch.View.Members == 32 &&
        Batch.Checkpoint == "K031", "32-record checkpoint");
    {
        Backend Store(Folder, Until());
        AdmitField(Store, Id, "Field");
        Set(Store, Key(Id, "K005"), Record(Numeric(-5))); // already scanned
        Set(Store, Key(Id, "K080"), Record(Numeric(-80))); // future, then re-derived
        Remove(Store, Key(Id, "K006")); Remove(Store, Key(Id, "K081"));
        Set(Store, Key(Id, "K007"), Record(Text("changed")));
        Set(Store, Key(Id, "K082"), Record(Text(std::string(1025, 'x'))));
        Set(Store, Key(Id, "K008"), Flag(true)); // scalar root no longer participates
        Set(Store, Key(Id, "A"), Record(Flag(false))); // insertion behind checkpoint
        Set(Store, Key(Id, "Z"), Record(Text("future insertion")));
    }
    const auto Changed = Inspect(Folder, Id);
    Check(Changed.View.Status == Derived::State::Building && Changed.Checkpoint == Batch.Checkpoint &&
        Changed.View.Strings == 3 && Changed.View.OversizedStrings == 1 && Changed.View.Booleans == 1,
        "online changes maintained without advancing checkpoint");
    {
        Backend Store(Folder, Until()); Drain(Store);
    }
    CheckProjection(Folder, Id);
    const auto Active = Inspect(Folder, Id);
    Check(Active.View.Generation == Before.View.Generation && Active.View.OversizedStrings > 0,
        "same generation published with oversized string unavailable");
    {
        Backend Store(Folder, Until());
        Verify(Store);
        Set(Store, Key(Id, "K082"), Record(Text("now usable")));
        Set(Store, Key(Id, "K005"), Record(Text(std::string(1024, 'q'))));
        Remove(Store, Key(Id, "A"));
        Value Array; Array.Type = Kind::Array; Array.Array.push_back(std::make_shared<Value>(Numeric(1)));
        Set(Store, Key(Id, "K010"), Record(Array));
        Set(Store, Key(Id, "K011"), Value{});
    }
    CheckProjection(Folder, Id); // includes successful ACTIVE type-change/removal
    Check(Inspect(Folder, Id).View.OversizedStrings == 0, "last oversized string removed");
    { Backend Store(Folder, Until()); Check(Store.HasDerivedWork(), "active restart requires re-verification"); Verify(Store); Check(!Store.HasDerivedWork(), "active restart needs no rebuild"); }
    CheckProjection(Folder, Id);
}

void IsolationAndSplit(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "isolation-split");
    const std::vector<Identity> Stores{{false, "", "Store", ""}, {true, "addon-a", "Store", ""},
        {true, "addon-b", "Store", ""}, {true, "addon-a", "Other", ""}};
    {
        Backend Store(Folder, Until());
        Verify(Store);
        for (size_t Index = 0; Index < Stores.size(); ++Index) {
            Set(Store, Key(Stores[Index], "Same"), Record(Numeric(double(Index))));
            Check(Store.PrepareDerived(Stores[Index], "Field", Until()), "isolated preparation");
        }
        const auto& Id = Stores[0];
        // Full tagged key crosses the 512-byte prefix boundary at 512 data bytes.
        for (size_t Length : {size_t(0), size_t(510), size_t(511), size_t(512), size_t(1023), size_t(1024)})
            Set(Store, Key(Id, "Length" + std::to_string(Length)), Record(Text(std::string(Length, 'x'))));
        Set(Store, Key(Id, "SharedPrefixA"), Record(Text(std::string(511, 'x') + "A")));
        Set(Store, Key(Id, "SharedPrefixB"), Record(Text(std::string(511, 'x') + "B")));
        Set(Store, Key(Id, "Duplicate"), Record(Text(std::string(512, 'x'))));
        Set(Store, Key(Id, "Unicode"), Record(Text(std::string(510, 'x') + "\xc3\xa9")));
        Drain(Store);
    }
    std::set<int64_t> Fields;
    for (const auto& Id : Stores) {
        CheckProjection(Folder, Id); Fields.insert(Inspect(Folder, Id).View.FieldId);
    }
    Check(Fields.size() == Stores.size(), "namespace/store field identities isolated");
    {
        Backend Store(Folder, Until());
        Verify(Store);
        Remove(Store, Key(Stores[0], "Same"));
        Set(Store, Key(Stores[1], "Same"), Record(Flag(true)));
    }
    for (const auto& Id : Stores) CheckProjection(Folder, Id);
    Check(Inspect(Folder, Stores[2]).Entries ==
        std::vector<OrderedEntry>{{SortScalar(Numeric(2)), "Same"}}, "addon mutation cannot leak");
    Check(Inspect(Folder, Stores[3]).Entries ==
        std::vector<OrderedEntry>{{SortScalar(Numeric(3)), "Same"}}, "store mutation cannot leak");
}

void ByteBatch(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "byte-batch");
    const Identity Id{false, "", "Store", ""};
    size_t EnvelopeSize = 0;
    {
        Backend Store(Folder, Until());
        Verify(Store);
        for (unsigned Index = 0; Index < 12; ++Index) {
            auto Input = Record(Numeric(Index));
            for (unsigned Part = 0; Part < 3; ++Part)
                Input.Map.emplace_back("Padding" + std::to_string(Part), std::make_shared<Value>(Text(std::string(16000, 'x'))));
            const auto Next = Key(Id, RecordKey(Index));
            EnvelopeSize = Encode(Next, Input, Until()).size(); Set(Store, Next, Input);
        }
        Check(Store.PrepareDerived(Id, "Field", Until()) && Store.MaintainDerived(Until()), "byte-limited batch");
    }
    const auto Batch = Inspect(Folder, Id);
    const auto Expected = int64_t(256 * 1024 / EnvelopeSize);
    Check(Batch.View.Status == Derived::State::Building && Expected < 12 &&
        Batch.View.Members == Expected && Batch.Checkpoint == RecordKey(unsigned(Expected - 1)),
        "256-KiB batch stops before first unprocessed record");
    { Backend Store(Folder, Until()); Drain(Store); }
    CheckProjection(Folder, Id);
}

// Seed valid retained CLEANUP rows offline, representing prior deleted data.
// This is a bounded accounting-boundary fixture, not an organic workload or
// physical-fit proof. Every row has its canonical charge, references and IDs;
// production VerifyStep independently accepts it before the real quota test.
void RetiredNearQuota(const std::filesystem::path& Folder, const Identity& Id, int64_t Budget = 16ll * 1024 * 1024)
{
    const auto Retired = Inspect(Folder, Id, "Retired").View;
    Check(Retired.Status == Derived::State::Active && Retired.Members == 0, "empty retired fixture field");
    Connection Db(Folder, true);
    Db.Exec("PRAGMA journal_mode=PERSIST; PRAGMA synchronous=EXTRA; BEGIN IMMEDIATE");
    int64_t NextGeneration = Scalar(Db, "SELECT NextGeneration FROM DerivedTotals");
    int64_t NextPrefix = Scalar(Db, "SELECT NextPrefix FROM DerivedTotals");
    int64_t NextEntry = Scalar(Db, "SELECT NextEntry FROM DerivedTotals");
    {
        Statement Retire(Db, "UPDATE DerivedGenerations SET State=3 WHERE Id=?1");
        Retire.Integer(1, Retired.Generation); Check(!Retire.Next(), "retire fixture generation");
    }
    const int64_t Second = NextGeneration++;
    {
        Statement Gen(Db, "INSERT INTO DerivedGenerations VALUES(?1,?2,3,?3,0,0,0,0,0,70)");
        Gen.Integer(1, Second); Gen.Integer(2, Retired.FieldId); Gen.Blob(3, std::string());
        Check(!Gen.Next(), "second cleanup generation");
    }
    int64_t NamespaceBytes;
    {
        Statement Ns(Db, "SELECT Bytes FROM DerivedNamespaces WHERE Namespace=?1");
        Ns.Blob(1, Namespace(Id)); Check(Ns.Next(), "fixture namespace ledger"); NamespaceBytes = Ns.Number(0);
    }
    Check(Budget <= 16ll * 1024 * 1024 && Budget > NamespaceBytes + 70, "bounded quota fixture budget");
    int64_t Added = 70;
    int64_t FirstCount = 0, SecondCount = 0;
    Statement Prefix(Db, "INSERT INTO DerivedPrefixes VALUES(?1,?2,?3,1,?4)");
    Statement Entry(Db, "INSERT INTO DerivedEntries VALUES(?1,?2,?3,?4,?5)");
    Statement Member(Db, "INSERT INTO DerivedMembers VALUES(?1,?2,3,?3,?4)");
    unsigned Index = 0;
    auto Insert = [&](size_t Length) {
        const auto Gen = Index < 7000 ? Retired.Generation : Second;
        std::string KeyName = "Retired" + std::to_string(Index); KeyName.resize(128, 'k');
        std::string Content = std::to_string(Index) + ":"; Content.resize(Length, 'x');
        const auto Full = SortScalar(Text(Content));
        const size_t PrefixLength = std::min<size_t>(512, Full.size());
        const Bytes PrefixBytes(Full.begin(), Full.begin() + PrefixLength);
        const Bytes SuffixBytes(Full.begin() + PrefixLength, Full.end());
        const auto PrefixId = NextPrefix++, EntryId = NextEntry++;
        const int64_t PrefixCharge = 37 + int64_t(PrefixBytes.size());
        const int64_t EntryCharge = 33 + int64_t(SuffixBytes.size() + KeyName.size());
        const int64_t MemberCharge = 30 + int64_t(KeyName.size());
        Prefix.Integer(1, PrefixId); Prefix.Integer(2, Gen); Prefix.Blob(3, PrefixBytes); Prefix.Integer(4, PrefixCharge);
        Check(!Prefix.Next(), "quota fixture prefix"); Prefix.Reset();
        Entry.Integer(1, EntryId); Entry.Integer(2, PrefixId); Entry.Blob(3, SuffixBytes);
        Entry.Blob(4, KeyName); Entry.Integer(5, EntryCharge);
        Check(!Entry.Next(), "quota fixture entry"); Entry.Reset();
        Member.Integer(1, Gen); Member.Blob(2, KeyName); Member.Integer(3, EntryId); Member.Integer(4, MemberCharge);
        Check(!Member.Next(), "quota fixture member"); Member.Reset();
        Added += PrefixCharge + EntryCharge + MemberCharge;
        if (Gen == Retired.Generation) ++FirstCount; else ++SecondCount;
        Check(++Index <= 13000, "bounded quota fixture rows");
    };
    // 128-byte keys plus a 1024-byte string charge exactly 1381 bytes across
    // prefix, entry and member. Leave 512 bytes for the boundary mutation.
    while (Budget - NamespaceBytes - Added >= 1381 + 512) Insert(1024);
    const int64_t Remaining = Budget - NamespaceBytes - Added;
    if (Remaining >= 1016) Insert(size_t(Remaining - 512 - 357));
    Check(Budget - NamespaceBytes - Added >= 0 && Budget - NamespaceBytes - Added < 1016,
        "namespace headroom below real mutation delta");
    for (const auto& Pair : {std::pair<int64_t, int64_t>{Retired.Generation, FirstCount}, {Second, SecondCount}}) {
        Statement Counts(Db, "UPDATE DerivedGenerations SET Members=?2,Strings=?2 WHERE Id=?1");
        Counts.Integer(1, Pair.first); Counts.Integer(2, Pair.second);
        Check(!Counts.Next(), "cleanup category counts");
    }
    {
        Statement Ns(Db, "UPDATE DerivedNamespaces SET Bytes=Bytes+?2 WHERE Namespace=?1");
        Ns.Blob(1, Namespace(Id)); Ns.Integer(2, Added); Check(!Ns.Next(), "fixture namespace charge");
        Statement Total(Db, "UPDATE DerivedTotals SET NextGeneration=?1,NextPrefix=?2,NextEntry=?3,Bytes=Bytes+?4");
        Total.Integer(1, NextGeneration); Total.Integer(2, NextPrefix); Total.Integer(3, NextEntry);
        Total.Integer(4, Added); Check(!Total.Next(), "fixture global charge and identities");
    }
    Db.Exec("COMMIT");
}

void RealNamespaceQuota(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "real-namespace-quota");
    const Identity Id{false, "", "Store", "Key"};
    {
        Backend Store(Folder, Until()); Verify(Store); Set(Store, Id, Record(Numeric(7)));
        Check(Store.PrepareDerived(Id, "Field", Until()) &&
            Store.PrepareDerived(Id, "Retired", Until()), "quota fixture fields");
        Drain(Store);
    }
    RetiredNearQuota(Folder, Id);
    const auto Before = Inspect(Folder, Id); // independent retained charge sum
    Check(Before.View.Status == Derived::State::Active && Before.Stats.Cleanup == 2, "valid near-quota state");
    {
        Backend Store(Folder, Until()); AdmitField(Store, Id, "Field"); // verify without consuming retained cleanup charge
        Set(Store, Id, Record(Text(std::string(1024, 'z')))); // real Charge() limit, no injected exception
    }
    const auto After = Inspect(Folder, Id);
    Check(After.View.FieldId == Before.View.FieldId && After.View.Status == Derived::State::Unavailable &&
        After.Stats.Cleanup == 3 && After.Stats.Bytes == Before.Stats.Bytes,
        "actual namespace quota rolls back partial derived allocation and withdraws");
    CheckProjection(Folder, Id, true);
    std::puts("[CarbonLuau:Persistence] Actual 16-MiB derived namespace limit exercised using valid offline retained-state fixture; not organic/physical-fit proof");
}

void RealGlobalQuota(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "real-global-quota");
    const Identity Target{false, "", "Store", "Key"};
    std::vector<Identity> Stores{Target};
    for (unsigned Index = 0; Index < 4; ++Index)
        Stores.push_back({true, "quota-" + std::to_string(Index), "Store", "Key"});
    {
        Backend Store(Folder, Until());
        Verify(Store);
        for (const auto& Id : Stores) {
            Set(Store, Id, Record(Numeric(7)));
            Check(Store.PrepareDerived(Id, "Field", Until()) &&
                Store.PrepareDerived(Id, "Retired", Until()), "global quota fixture fields");
        }
        Drain(Store);
    }
    for (size_t Index = 1; Index < Stores.size(); ++Index) RetiredNearQuota(Folder, Stores[Index]);
    int64_t Budget;
    {
        Connection Db(Folder);
        Statement Ns(Db, "SELECT Bytes FROM DerivedNamespaces WHERE Namespace=?1");
        Ns.Blob(1, Namespace(Target)); Check(Ns.Next(), "target global quota namespace");
        Budget = Ns.Number(0) + 64ll * 1024 * 1024 - Scalar(Db, "SELECT Bytes FROM DerivedTotals");
    }
    RetiredNearQuota(Folder, Target, Budget);
    const auto Before = Inspect(Folder, Target);
    Check(Before.View.Status == Derived::State::Active &&
        64ll * 1024 * 1024 - Before.Stats.Bytes < 1016, "real global headroom below mutation charge");
    {
        Connection Db(Folder);
        Statement Ns(Db, "SELECT Bytes FROM DerivedNamespaces WHERE Namespace=?1");
        Ns.Blob(1, Namespace(Target)); Check(Ns.Next(), "target namespace headroom");
        Check(16ll * 1024 * 1024 - Ns.Number(0) > 1016, "namespace limit cannot explain global failure");
    }
    { Backend Store(Folder, Until()); AdmitField(Store, Target, "Field"); Set(Store, Target, Record(Text(std::string(1024, 'z')))); }
    const auto After = Inspect(Folder, Target);
    Check(After.View.Status == Derived::State::Unavailable && After.Stats.Bytes == Before.Stats.Bytes &&
        After.Stats.Cleanup == Before.Stats.Cleanup + 1, "actual global quota withdraws without stale ACTIVE or leaked charges");
    CheckProjection(Folder, Target, true);
    std::puts("[CarbonLuau:Persistence] Actual 64-MiB derived global limit exercised using valid offline retained-state fixtures; not organic/physical-fit proof");
}

void Replacement(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "replacement");
    const Identity Id{false, "", "Store", ""};
    {
        Backend Store(Folder, Until());
        Verify(Store);
        for (unsigned Index = 0; Index < 70; ++Index)
            Set(Store, Key(Id, RecordKey(Index)), Record(Numeric(Index)));
        Check(Store.PrepareDerived(Id, "Field", Until()), "replacement initial preparation"); Drain(Store);
    }
    const auto Original = Inspect(Folder, Id);
    {
        Backend Store(Folder, Until());
        Verify(Store);
        Check(Store.PrepareDerived(Id, "Field", Until(), true), "force private replacement");
        Check(Store.MaintainDerived(Until()), "replacement first batch");
        Set(Store, Key(Id, "K005"), Record(Text("past")));
        Set(Store, Key(Id, "K065"), Record(Text("future")));
        Remove(Store, Key(Id, "K006")); Remove(Store, Key(Id, "K066"));
    }
    const auto Building = Inspect(Folder, Id);
    Check(Building.View.Generation == Original.View.Generation && Building.Stats.Active == 1 &&
        Building.Stats.Building == 1 && Building.Stats.Fields == 1 && Building.Stats.Bytes > Original.Stats.Bytes,
        "active plus shadow share one field slot and charge both generations");
    CheckProjection(Folder, Id);
    { Backend Store(Folder, Until()); Drain(Store); }
    const auto Replaced = Inspect(Folder, Id);
    Check(Replaced.View.Generation > Original.View.Generation && Replaced.Stats.Active == 1 &&
        Replaced.Stats.Building == 0 && Replaced.Stats.Cleanup == 0, "replacement publication and bounded retirement");
    CheckProjection(Folder, Id);
}

void LogicalCorruption(const std::filesystem::path& Parent, bool PrefixMismatch = false)
{
    const auto Folder = Fixture(Parent, PrefixMismatch ? "logical-prefix-corruption" : "logical-suffix-corruption");
    const Identity Id{false, "", "Store", "Key"};
    const auto Input = Record(Text(std::string(1024, 'z')));
    {
        Backend Store(Folder, Until()); Verify(Store); Set(Store, Id, Input);
        Check(Store.PrepareDerived(Id, "Field", Until()), "logical corruption fixture"); Drain(Store);
    }
    {
        Connection Db(Folder, true);
        Db.Exec("PRAGMA journal_mode=PERSIST; BEGIN IMMEDIATE");
        if (PrefixMismatch)
            Db.Exec("UPDATE DerivedPrefixes SET Prefix=CAST(substr(Prefix,1,1)||X'79'||substr(Prefix,3) AS BLOB)");
        else Db.Exec("UPDATE DerivedEntries SET Suffix=CAST(X'79'||substr(Suffix,2) AS BLOB)");
        Db.Exec("COMMIT");
    }
    {
        Connection Db(Folder, true); auto End = Until();
        Derived Engine(Db.Db, End);
        bool Rejected = false, Completed = false;
        for (unsigned Step = 0; Step < 4096 && !Rejected && !Completed; ++Step) {
            Db.Exec("BEGIN IMMEDIATE");
            const auto Result = Engine.VerifyStep();
            Check(Result != Derived::VerificationResult::DerivedInvalid,
                "coherent representation mismatch is not invalid derived authority");
            Rejected = Scalar(Db, "SELECT count(*) FROM DerivedGenerations WHERE State=2") == 0;
            Completed = Result == Derived::VerificationResult::Complete;
            Db.Exec("ROLLBACK"); // forensic observation must not persist withdrawal
        }
        Check(Rejected && Scalar(Db, "SELECT count(*) FROM DerivedGenerations WHERE State=2") == 1,
            "bounded forensic verification detects mismatch without persisting quarantine");
    }
    {
        Backend Store(Folder, Until());
        const auto Read = Store.Execute(Operation::Get, Id, {}, Until());
        Check(Store.Available() && Read.Code == Error::None && Read.Envelope == Encode(Id, Input, Until()),
            "coherent derived mismatch preserves primary availability");
        Check(Inspect(Folder, Id).View.Status == Derived::State::Active,
            "primary Ready does not synchronously quarantine derived representation");
        Drain(Store);
    }
    const auto Quarantined = Inspect(Folder, Id);
    Check(Quarantined.View.Status == Derived::State::Unavailable && Quarantined.Stats.Active == 0,
        "validly encoded wrong derived value is withdrawn and cleanup converges");
    {
        Backend Store(Folder, Until());
        Verify(Store);
        Set(Store, Key(Id, "Unrelated"), Flag(false));
        Remove(Store, Key(Id, "Unrelated")); // ordinary F1 writes remain available while cleanup is pending
        Drain(Store);
        Check(Store.PrepareDerived(Id, "Field", Until()), "logical repair preparation"); Drain(Store);
    }
    CheckProjection(Folder, Id);
    // In contrast, untrusted ledger metadata cannot be reclassified as a
    // harmless representation mismatch and must preserve the database.
    {
        Connection Db(Folder, true);
        Db.Exec("PRAGMA journal_mode=PERSIST; UPDATE DerivedTotals SET Bytes=Bytes+1");
    }
    {
        Backend Store(Folder, Until());
        const auto Read = Store.Execute(Operation::Get, Id, {}, Until());
        Check(Store.Available() && Read.Code == Error::None && Read.Envelope == Encode(Id, Input, Until()),
            "derived-only accounting fault does not disable authoritative primary reads");
        for (unsigned Step = 0; Step < 128 && Store.HasDerivedWork(); ++Step)
            Check(Store.MaintainDerived(Until()) && Store.Available(), "derived-only structural proof fails locally");
        Check(!Store.HasDerivedWork() && !Store.PrepareDerived(Id, "Field", Until()),
            "invalid derived authority is terminal and cannot admit Query work");
    }
    Connection Db(Folder);
    Statement Primary(Db, "SELECT Envelope FROM Records WHERE Namespace=?1 AND Store=?2 AND Key=?3");
    Primary.Blob(1, Namespace(Id)); Primary.Blob(2, Id.Store); Primary.Blob(3, Id.Key);
    Check(Primary.Next() && Primary.Data(0) == Encode(Id, Input, Until()), "corrupt metadata does not overwrite primary");
}

void BoundedStartupQuarantine(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "bounded-startup-quarantine");
    std::vector<Identity> Records;
    auto Input = Record(Text(std::string(1024, 'z')));
    Input.Map.emplace_back("Other", std::make_shared<Value>(Text(std::string(1024, 'w'))));
    {
        Backend Store(Folder, Until());
        Verify(Store);
        for (unsigned Index = 0; Index < 40; ++Index) {
            // Deliberately interleave generation IDs across lexical stores.
            const unsigned StoreIndex = (Index * 17) % 40;
            Identity Id{false, "", std::string("Store") + (StoreIndex < 10 ? "0" : "") + std::to_string(StoreIndex), "Key"};
            Set(Store, Id, Input);
            Check(Store.PrepareDerived(Id, "Field", Until()), "quarantine field preparation");
            if (StoreIndex == 31)
                Check(Store.PrepareDerived(Id, "Other", Until()), "mid-store cursor fixture");
            Records.push_back(std::move(Id));
        }
        Drain(Store);
    }
    {
        Connection Db(Folder, true);
        Check(Scalar(Db, "SELECT count(*) FROM DerivedGenerations WHERE State=2") == 41,
            "forty-one active generations before quarantine");
        Db.Exec("PRAGMA journal_mode=PERSIST; BEGIN IMMEDIATE");
        Db.Exec("UPDATE DerivedEntries SET Suffix=CAST(X'79'||substr(Suffix,2) AS BLOB)");
        Db.Exec("COMMIT");
    }
    {
        Backend Store(Folder, Clock::now() + std::chrono::seconds(30));
        Check(Store.Available(), "bounded quarantine startup finishes");
        for (const auto& Id : Records) {
            auto Read = Store.Execute(Operation::Get, Id, {}, Until());
            Check(Read.Code == Error::None && Read.Envelope == Encode(Id, Input, Until()),
                "quarantine keeps authoritative primary value");
        }
        {
            Connection Db(Folder);
            Check(Scalar(Db, "SELECT count(*) FROM DerivedGenerations WHERE State=2") == 41,
                "primary Ready does not wait for derived quarantine");
        }
        Drain(Store);
    }
    Connection Db(Folder);
    Check(Scalar(Db, "SELECT count(*) FROM DerivedGenerations WHERE State=2") == 0 &&
        Scalar(Db, "SELECT count(*) FROM DerivedGenerations WHERE State=3") == 0,
        "bounded verifier withdraws all mismatches and cleanup converges");
}

void InvalidDerivedStructure(const std::filesystem::path& Parent)
{
    const std::vector<std::pair<const char*, const char*>> Cases{
        {"counters", "UPDATE DerivedGenerations SET Members=Members+1,Strings=Strings+1 WHERE Id=(SELECT max(Id) FROM DerivedGenerations)"},
        {"false-empty", "UPDATE DerivedGenerations SET Members=0,Strings=0 WHERE Id=(SELECT max(Id) FROM DerivedGenerations)"},
        {"generation-authority", "UPDATE DerivedGenerations SET FieldId=(SELECT NextField FROM DerivedTotals) WHERE Id=(SELECT max(Id) FROM DerivedGenerations)"},
        {"entry-authority", "UPDATE DerivedEntries SET PrefixId=(SELECT NextPrefix FROM DerivedTotals) WHERE Id=(SELECT max(Id) FROM DerivedEntries)"},
        {"prefix-authority", "UPDATE DerivedPrefixes SET Generation=(SELECT NextGeneration FROM DerivedTotals) WHERE Id=(SELECT max(Id) FROM DerivedPrefixes)"},
        {"prefix-type-tag", "UPDATE DerivedPrefixes SET Prefix=CAST(X'63'||substr(Prefix,2) AS BLOB) WHERE Id=(SELECT max(Id) FROM DerivedPrefixes)"},
        {"identity-high-water", "UPDATE DerivedTotals SET NextEntry=0"}};
    for (const auto& Case : Cases) {
        const auto Folder = Fixture(Parent, (std::string("invalid-") + Case.first).c_str());
        const Identity Id{false, "", "Store", "Key"};
        auto Input = Record(Text(std::string(1024, 'z')));
        Input.Map.emplace_back("Other", std::make_shared<Value>(Text(std::string(1024, 'w'))));
        const auto Envelope = Encode(Id, Input, Until());
        {
            Backend Store(Folder, Until()); Verify(Store); Set(Store, Id, Input);
            Check(Store.PrepareDerived(Id, "Field", Until()) &&
                Store.PrepareDerived(Id, "Other", Until()), "invalid structure fixture fields");
            Drain(Store);
        }
        const auto Before = Inspect(Folder, Id);
        Check(Before.Stats.Active == 2, "two initially healthy representations");
        {
            Connection Db(Folder, true);
            Db.Exec("PRAGMA journal_mode=PERSIST; BEGIN IMMEDIATE");
            // Earlier generation has a coherent, valid same-length mismatch.
            // The later invalid graph must be detected before any quarantine.
            Db.Exec("UPDATE DerivedEntries SET Suffix=CAST(X'79'||substr(Suffix,2) AS BLOB) "
                "WHERE PrefixId IN (SELECT Id FROM DerivedPrefixes WHERE Generation=(SELECT min(Id) FROM DerivedGenerations))");
            Db.Exec(Case.second); Db.Exec("COMMIT");
        }
        {
            Backend Store(Folder, Until());
            const auto Read = Store.Execute(Operation::Get, Id, {}, Until());
            Check(Store.Available() && Read.Code == Error::None && Read.Envelope == Envelope,
                "derived-only invalid graph preserves primary Ready");
            for (unsigned Step = 0; Step < 128 && Store.HasDerivedWork(); ++Step)
                Check(Store.MaintainDerived(Until()) && Store.Available(), "invalid derived graph fails locally");
            Check(!Store.HasDerivedWork() && !Store.PrepareDerived(Id, "Field", Until()),
                "invalid derived graph cannot spin verification or admit Query work");
        }
        Connection Db(Folder);
        Check(Scalar(Db, "SELECT count(*) FROM DerivedGenerations WHERE State=2") == 2 &&
            Scalar(Db, "SELECT count(*) FROM DerivedGenerations WHERE State=3") == 0 &&
            Scalar(Db, "SELECT Bytes FROM DerivedTotals") == Before.Stats.Bytes,
            "invalid graph cannot commit partial quarantine or accounting repair");
        Statement Primary(Db, "SELECT Envelope FROM Records WHERE Namespace=?1 AND Store=?2 AND Key=?3");
        Primary.Blob(1, Namespace(Id)); Primary.Blob(2, Id.Store); Primary.Blob(3, Id.Key);
        Check(Primary.Next() && Primary.Data(0) == Envelope, "invalid graph preserves primary envelope");
    }
}

void EmptyPrimaryQuarantine(const std::filesystem::path& Parent)
{
    const auto Folder=Fixture(Parent,"empty-primary-quarantine");
    const Identity Id{false,"","Store","Key"};
    {
        Backend Store(Folder,Until()); Verify(Store);
        Set(Store,Id,Record(Text("value")));
        Check(Store.PrepareDerived(Id,"Field",Until()),"empty-primary prepared");
        Drain(Store);
    }
    {
        Connection Db(Folder,true);
        Db.Exec("PRAGMA journal_mode=PERSIST; BEGIN IMMEDIATE");
        Db.Exec("DELETE FROM Records; DELETE FROM Quotas; UPDATE Totals SET Bytes=0,Keys=0,Namespaces=0 WHERE Id=1; COMMIT");
    }
    {
        Backend Reopened(Folder,Until());
        Check(Reopened.Available(),"empty-primary reopen available");
        const auto Read=Reopened.Execute(Operation::Get,Id,{},Until());
        Check(Read.Code==Error::None && !Read.Found,"empty-primary authoritative absence");
        Drain(Reopened);
    }
    Connection Db(Folder);
    Check(Scalar(Db,"SELECT count(*) FROM DerivedGenerations WHERE State=2")==0 &&
        Scalar(Db,"SELECT count(*) FROM DerivedGenerations WHERE State=3")==0,
        "orphaned retained generation withdrawn after empty primary proof");
}

void ReadinessAndUnverifiedWrites(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "unverified-active-writes");
    const Identity Id{false, "", "Store", "Key"};
    {
        Backend Store(Folder, Until()); Verify(Store);
        Set(Store, Id, Record(Numeric(1)));
        Check(Store.PrepareDerived(Id, "Field", Until()), "active fixture preparation");
        Drain(Store);
    }
    CheckProjection(Folder, Id);
    {
        Backend Store(Folder, Until());
        Check(Store.Available() && Store.HasDerivedWork(), "retained ACTIVE needs process-local proof");
        Check(!Store.PrepareDerived(Id, "Field", Until()) && Store.Available(),
            "unverified ACTIVE cannot authorize preparation");
        const auto Retained = Inspect(Folder, Id);
        Check(Retained.View.Status == Derived::State::Active && !Retained.View.Queryable(Kind::Number),
            "persisted ACTIVE row is not independent admission");
        const auto Read = Store.Execute(Operation::Get, Id, {}, Until());
        Check(Read.Code == Error::None && Read.Found, "primary Get succeeds before derived proof");
        Set(Store, Id, Record(Numeric(2)));
        Check(Inspect(Folder, Id).View.Status == Derived::State::Active,
            "unverified write need not synchronously rewrite retained index");
        Drain(Store);
        Check(Inspect(Folder, Id).View.Status == Derived::State::Unavailable,
            "verifier withdraws ACTIVE index made stale by unverified Set");
        Check(Store.PrepareDerived(Id, "Field", Until()), "withdrawn index may rebuild");
        Drain(Store);
    }
    CheckProjection(Folder, Id);
    {
        Backend Store(Folder, Until());
        Check(!Store.PrepareDerived(Id, "Field", Until()), "second process cannot reuse admission");
        Remove(Store, Id);
        Drain(Store);
        Check(Inspect(Folder, Id).View.Status == Derived::State::Unavailable,
            "verifier withdraws ACTIVE index made stale by unverified Remove");
        Check(Store.PrepareDerived(Id, "Field", Until()), "removed-key index may rebuild");
        Drain(Store);
    }
    CheckProjection(Folder, Id);
}

void BuildingRestartVerification(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "unverified-building-restart");
    const Identity Id{false, "", "Store", ""};
    {
        Backend Store(Folder, Until()); Verify(Store);
        for (unsigned Index = 0; Index < 48; ++Index)
            Set(Store, Key(Id, RecordKey(Index)), Record(Numeric(Index)));
        Check(Store.PrepareDerived(Id, "Field", Until()) && Store.MaintainDerived(Until()),
            "partial BUILDING checkpoint");
    }
    const auto Before = Inspect(Folder, Id);
    Check(Before.View.Status == Derived::State::Building && Before.View.Members == 32,
        "retained BUILDING is incomplete");
    {
        Backend Store(Folder, Until());
        Check(Store.Available() && !Store.PrepareDerived(Id, "Field", Until()),
            "BUILDING checkpoint cannot resume before proof");
        Set(Store, Key(Id, "K005"), Record(Numeric(-5)));
        Drain(Store);
        Check(Inspect(Folder, Id).View.Status == Derived::State::Unavailable,
            "stale BUILDING checkpoint cannot publish after unverified Set");
        Check(Store.PrepareDerived(Id, "Field", Until()), "withdrawn BUILDING restarts preparation");
        Drain(Store);
    }
    CheckProjection(Folder, Id);
    {
        Backend Store(Folder, Until());
        Check(Store.HasDerivedWork() && !Store.PrepareDerived(Id, "Field", Until()),
            "reopened ACTIVE still needs fresh proof");
        Verify(Store);
        Check(!Store.HasDerivedWork(), "healthy retained proof finishes without rebuild");
    }
}

void PausedVerificationAndRetry(const std::filesystem::path& Parent)
{
    const auto Folder = Fixture(Parent, "paused-derived-verification");
    const Identity Id{false, "", "Store", "Key"};
    {
        Backend Store(Folder, Until()); Verify(Store);
        Set(Store, Id, Record(Numeric(1)));
        Check(Store.PrepareDerived(Id, "Field", Until()), "paused fixture preparation");
        Drain(Store);
    }
    {
        Backend Store(Folder, Until());
        Check(Store.Available() && Store.HasDerivedWork(), "retained proof pending before deadline fault");
        Check(!Store.PrepareDerived(Id, "Field", Until()), "unverified ACTIVE not admitted");
        Check(!Store.MaintainDerived(Clock::now()-std::chrono::milliseconds(1)),
            "expired derived maintenance deadline rejected");
        Check(Store.Available() && !Store.HasDerivedWork() &&
            !Store.PrepareDerived(Id, "Field", Until()),
            "paused verification cannot hot-loop or admit Query work");
        const auto Read = Store.Execute(Operation::Get, Id, {}, Until());
        Check(Read.Code == Error::None && Read.Found, "primary read survives derived pause");
        Set(Store, Id, Record(Numeric(2)));
        Check(Store.Available() && !Store.HasDerivedWork(), "primary write survives derived pause");
    }
    {
        Backend Store(Folder, Until());
        Check(Store.Available() && Store.HasDerivedWork() &&
            !Store.PrepareDerived(Id, "Field", Until()),
            "reopen retries proof without reusing old admission");
        Drain(Store);
        Check(Inspect(Folder, Id).View.Status == Derived::State::Unavailable,
            "restarted proof withdraws index stale from paused write");
        Check(Store.PrepareDerived(Id, "Field", Until()), "reopened worker can prepare replacement");
        Drain(Store);
    }
    CheckProjection(Folder, Id);
}

// Qualification-only cardinality probe. Seed structurally valid private
// metadata directly so a large retained history can be tested without tens
// of thousands of durability-round-trip calls. This is not an author API or
// evidence that every seeded state was physically admitted by public work.
void StartupCardinality(const std::filesystem::path& Parent, unsigned StoreCount, bool PrimaryLoad,
    bool HeavyPrimary, bool ActiveOnly, bool DensePrimary = false, bool PaddedDense = false,
    bool RequireReady = false)
{
    Check(StoreCount <= 46656 && (DensePrimary || StoreCount > 0),
        "bounded three-character stores");
    const auto Folder = Fixture(Parent, "startup-cardinality");
    { Backend Initial(Folder, Until()); Check(Initial.Available(), "fresh backend"); }
    int64_t FieldId = 1, GenerationId = 1;
    int64_t GlobalBytes = 65;
    int64_t PrimaryBytes = 0;
    {
        Connection Db(Folder, true);
        Db.Exec("PRAGMA journal_mode=PERSIST; BEGIN IMMEDIATE");
        Statement NamespaceRow(Db, "INSERT INTO DerivedNamespaces VALUES(?1,?2,?3)");
        Statement FieldRow(Db, "INSERT INTO DerivedFields VALUES(?1,?2,?3,?4,?5)");
        Statement GenerationRow(Db, "INSERT INTO DerivedGenerations VALUES(?1,?2,?3,?4,0,0,0,0,0,70)");
        constexpr char Digits[] = "0123456789abcdefghijklmnopqrstuvwxyz";
        for (unsigned Domain = 0; StoreCount > 0 && Domain < 4; ++Domain) {
            const std::string Ns{char(1), char('a' + Domain)};
            int64_t LocalBytes = 23;
            for (unsigned Index = Domain; Index < StoreCount; Index += 4) {
                const std::string Store{
                    Digits[(Index / 1296) % 36], Digits[(Index / 36) % 36], Digits[Index % 36]};
                for (unsigned FieldIndex = 0; FieldIndex < 8; ++FieldIndex) {
                    const std::string Name(1, char('A' + FieldIndex));
                    FieldRow.Integer(1, FieldId); FieldRow.Blob(2, Ns);
                    FieldRow.Blob(3, Store); FieldRow.Blob(4, Name); FieldRow.Integer(5, 35);
                    Check(!FieldRow.Next(), "insert field"); FieldRow.Reset();
                    for (int State = 2; State >= (ActiveOnly ? 2 : 1); --State) {
                        GenerationRow.Integer(1, GenerationId++);
                        GenerationRow.Integer(2, FieldId); GenerationRow.Integer(3, State);
                        GenerationRow.Blob(4, "");
                        Check(!GenerationRow.Next(), "insert generation"); GenerationRow.Reset();
                    }
                    ++FieldId;
                    LocalBytes += 35 + (ActiveOnly ? 70 : 2 * 70);
                }
            }
            Check(LocalBytes <= 16ll * 1024 * 1024, "namespace derived charge");
            NamespaceRow.Blob(1, Ns); NamespaceRow.Integer(2, LocalBytes);
            NamespaceRow.Integer(3, 23);
            Check(!NamespaceRow.Next(), "insert namespace"); NamespaceRow.Reset();
            GlobalBytes += LocalBytes;
        }
        if (PrimaryLoad) {
            Statement RecordRow(Db, "INSERT INTO Records VALUES(?1,?2,?3,?4,?5)");
            Statement QuotaRow(Db, "INSERT INTO Quotas VALUES(?1,?2,?3,1)");
            Value PrimaryValue{Kind::Map};
            if (HeavyPrimary) PrimaryValue.Map.emplace_back("Padding",
                std::make_shared<Value>(Text(std::string(2048, 'p'))));
            if (DensePrimary) {
                Value DenseArray{Kind::Array};
                for (unsigned Item = 0; Item < 1024; ++Item) {
                    auto Element = std::make_shared<Value>(); Element->Type = Kind::Boolean;
                    Element->Boolean = Item % 2 != 0; DenseArray.Array.push_back(std::move(Element));
                }
                if (PaddedDense) {
                    PrimaryValue.Map.emplace_back("A", std::make_shared<Value>(std::move(DenseArray)));
                    PrimaryValue.Map.emplace_back("Padding", std::make_shared<Value>(Text(std::string(320, 'p'))));
                } else PrimaryValue = std::move(DenseArray);
            }
            const unsigned Domains = HeavyPrimary || DensePrimary ? 16 : 10;
            const unsigned KeysPerDomain = HeavyPrimary || DensePrimary ? 6250 : 10000;
            for (unsigned Domain = 0; Domain < Domains; ++Domain) {
                const std::string Package(1, char('e' + Domain));
                const std::string Ns{char(1), Package[0]};
                int64_t LocalPrimary = 0;
                for (unsigned Index = 0; Index < KeysPerDomain; ++Index) {
                    const std::string KeyName = "K" + std::string(4 - std::to_string(Index).size(), '0') + std::to_string(Index);
                    Identity Id{true, Package, "Data", KeyName};
                    const auto Envelope = Encode(Id, PrimaryValue, Until());
                    const int64_t ChargeValue = int64_t(Id.Store.size() + Id.Key.size() + Envelope.size());
                    RecordRow.Blob(1, Ns); RecordRow.Blob(2, Id.Store); RecordRow.Blob(3, Id.Key);
                    RecordRow.Blob(4, Envelope); RecordRow.Integer(5, ChargeValue);
                    Check(!RecordRow.Next(), "insert primary record"); RecordRow.Reset();
                    LocalPrimary += ChargeValue;
                }
                Check(LocalPrimary <= 16ll * 1024 * 1024, "namespace primary charge");
                QuotaRow.Blob(1, Ns); QuotaRow.Integer(2, LocalPrimary); QuotaRow.Integer(3, KeysPerDomain);
                Check(!QuotaRow.Next(), "insert primary quota"); QuotaRow.Reset();
                PrimaryBytes += LocalPrimary;
                if (DensePrimary) continue;
                int64_t LocalDerived = 23;
                for (unsigned FieldIndex = 0; FieldIndex < 8; ++FieldIndex) {
                    const std::string Name(1, char('A' + FieldIndex));
                    FieldRow.Integer(1, FieldId); FieldRow.Blob(2, Ns);
                    FieldRow.Blob(3, "Data"); FieldRow.Blob(4, Name); FieldRow.Integer(5, 36);
                    Check(!FieldRow.Next(), "insert populated-store field"); FieldRow.Reset();
                    for (int State = 2; State >= (ActiveOnly ? 2 : 1); --State) {
                        GenerationRow.Integer(1, GenerationId++);
                        GenerationRow.Integer(2, FieldId); GenerationRow.Integer(3, State);
                        GenerationRow.Blob(4, "");
                        Check(!GenerationRow.Next(), "insert populated-store generation"); GenerationRow.Reset();
                    }
                    ++FieldId; LocalDerived += 36 + (ActiveOnly ? 70 : 2 * 70);
                }
                NamespaceRow.Blob(1, Ns); NamespaceRow.Integer(2, LocalDerived);
                NamespaceRow.Integer(3, 23);
                Check(!NamespaceRow.Next(), "insert populated-store namespace"); NamespaceRow.Reset();
                GlobalBytes += LocalDerived;
            }
            Check(PrimaryBytes <= 256ll * 1024 * 1024, "global primary charge");
            Statement PrimaryTotals(Db, "UPDATE Totals SET Bytes=?1,Keys=100000,Namespaces=?2 WHERE Id=1");
            PrimaryTotals.Integer(1, PrimaryBytes); PrimaryTotals.Integer(2, Domains);
            Check(!PrimaryTotals.Next(), "update primary totals");
        }
        Check(GlobalBytes <= 64ll * 1024 * 1024, "global derived charge");
        Statement Totals(Db, "UPDATE DerivedTotals SET NextField=?1,NextGeneration=?2,Bytes=?3 WHERE Id=1");
        Totals.Integer(1, FieldId); Totals.Integer(2, GenerationId); Totals.Integer(3, GlobalBytes);
        Check(!Totals.Next(), "update derived totals");
        Db.Exec("COMMIT");
    }
    const auto Started = Clock::now();
    try {
        Backend Reopened(Folder, Started + std::chrono::seconds(30));
        Check(Reopened.Available(), "cardinality reopen available");
        if (RequireReady) {
            if (StoreCount) {
                const Identity Retained{true, "a", "000", ""};
                Check(Reopened.HasDerivedWork() && !Reopened.PrepareDerived(Retained, "A", Until()),
                    "large retained ACTIVE/BUILDING state remains unadmitted at primary Ready");
            }
            if (PrimaryLoad) {
                const Identity Primary{true, "e", "Data", "K0000"};
                const auto Read = Reopened.Execute(Operation::Get, Primary, {}, Until());
                Check(Read.Code == Error::None && Read.Found,
                    "large retained derived proof does not block authoritative primary read");
            }
        }
        std::printf("[CarbonLuau:Persistence] startup-cardinality stores=%u primary=%d primary_bytes=%lld fields=%lld generations=%lld charge=%lld elapsed_ms=%lld result=ready\n",
            StoreCount, PaddedDense ? 4 : DensePrimary ? 3 : HeavyPrimary ? 2 : PrimaryLoad ? 1 : 0,
            static_cast<long long>(PrimaryBytes),
            static_cast<long long>(FieldId - 1), static_cast<long long>(GenerationId - 1),
            static_cast<long long>(GlobalBytes), static_cast<long long>(std::chrono::duration_cast<std::chrono::milliseconds>(Clock::now() - Started).count()));
    } catch (const Failure& Problem) {
        std::printf("[CarbonLuau:Persistence] startup-cardinality stores=%u primary=%d primary_bytes=%lld fields=%lld generations=%lld charge=%lld elapsed_ms=%lld result=error_%u\n",
            StoreCount, PaddedDense ? 4 : DensePrimary ? 3 : HeavyPrimary ? 2 : PrimaryLoad ? 1 : 0,
            static_cast<long long>(PrimaryBytes),
            static_cast<long long>(FieldId - 1), static_cast<long long>(GenerationId - 1),
            static_cast<long long>(GlobalBytes), static_cast<long long>(std::chrono::duration_cast<std::chrono::milliseconds>(Clock::now() - Started).count()),
            unsigned(Problem.Code));
        if (RequireReady) throw;
    }
}

// Test-only retained-cleanup cardinality. All members are charged and linked
// under the exact private schema; no authoritative primary record is invented.
void StartupMembers(const std::filesystem::path& Parent, unsigned PerGeneration, bool WithPrimary,
    bool Oversized = false)
{
    Check(PerGeneration>0 && PerGeneration<=10000,"bounded members per generation");
    const auto Folder=Fixture(Parent,"startup-members");
    { Backend Initial(Folder,Until()); Check(Initial.Available(),"fresh members backend"); }
    int64_t FieldId=1, GenerationId=1, PrefixId=1, EntryId=1, GlobalBytes=65;
    {
        Connection Db(Folder,true);
        Db.Exec("PRAGMA journal_mode=PERSIST; BEGIN IMMEDIATE");
        Statement NamespaceRow(Db,"INSERT INTO DerivedNamespaces VALUES(?1,?2,23)");
        Statement FieldRow(Db,"INSERT INTO DerivedFields VALUES(?1,?2,?3,?4,33)");
        Statement GenerationRow(Db,"INSERT INTO DerivedGenerations VALUES(?1,?2,3,?3,?4,?4,0,0,0,70)");
        Statement OversizedGenerationRow(Db,"INSERT INTO DerivedGenerations VALUES(?1,?2,3,?3,?4,0,0,?4,?4,70)");
        Statement PrefixRow(Db,"INSERT INTO DerivedPrefixes VALUES(?1,?2,?3,?4,39)");
        Statement EntryRow(Db,"INSERT INTO DerivedEntries VALUES(?1,?2,?3,?4,38)");
        Statement MemberRow(Db,"INSERT INTO DerivedMembers VALUES(?1,?2,1,?3,35)");
        Statement OversizedMemberRow(Db,"INSERT INTO DerivedMembers VALUES(?1,?2,3,0,34)");
        const std::string SortPrefix{char(1),char(0)};
        for(unsigned Domain=0;Domain<4;++Domain) {
            const std::string Ns{char(1),char('a'+Domain)};
            int64_t LocalBytes=23;
            for(unsigned StoreIndex=0;StoreIndex<(Oversized ? 3u : 1u);++StoreIndex) {
              const std::string StoreName=Oversized ? std::string(1,char('0'+StoreIndex)) : "S";
              for(unsigned FieldIndex=0;FieldIndex<8;++FieldIndex) {
                const std::string Name(1,char('A'+FieldIndex));
                FieldRow.Integer(1,FieldId); FieldRow.Blob(2,Ns); FieldRow.Blob(3,StoreName); FieldRow.Blob(4,Name);
                Check(!FieldRow.Next(),"members field"); FieldRow.Reset(); LocalBytes+=33;
                for(unsigned Generation=0;Generation<2;++Generation) {
                    const auto CurrentGeneration=GenerationId++;
                    auto& GenRow=Oversized ? OversizedGenerationRow : GenerationRow;
                    GenRow.Integer(1,CurrentGeneration); GenRow.Integer(2,FieldId);
                    GenRow.Blob(3,""); GenRow.Integer(4,PerGeneration);
                    Check(!GenRow.Next(),"members generation"); GenRow.Reset(); LocalBytes+=70;
                    int64_t CurrentPrefix=0;
                    if(!Oversized) {
                        CurrentPrefix=PrefixId++;
                        PrefixRow.Integer(1,CurrentPrefix); PrefixRow.Integer(2,CurrentGeneration);
                        PrefixRow.Blob(3,SortPrefix); PrefixRow.Integer(4,PerGeneration);
                        Check(!PrefixRow.Next(),"members prefix"); PrefixRow.Reset(); LocalBytes+=39;
                    }
                    for(unsigned Index=0;Index<PerGeneration;++Index) {
                        const std::string KeyName=(Oversized ? "" : "K")+
                            std::string(4-std::to_string(Index).size(),'0')+std::to_string(Index);
                        if(Oversized) {
                            OversizedMemberRow.Integer(1,CurrentGeneration); OversizedMemberRow.Blob(2,KeyName);
                            Check(!OversizedMemberRow.Next(),"oversized member"); OversizedMemberRow.Reset();
                            LocalBytes+=34;
                        } else {
                            const auto CurrentEntry=EntryId++;
                            EntryRow.Integer(1,CurrentEntry); EntryRow.Integer(2,CurrentPrefix);
                            EntryRow.Blob(3,""); EntryRow.Blob(4,KeyName);
                            Check(!EntryRow.Next(),"members entry"); EntryRow.Reset();
                            MemberRow.Integer(1,CurrentGeneration); MemberRow.Blob(2,KeyName); MemberRow.Integer(3,CurrentEntry);
                            Check(!MemberRow.Next(),"members member"); MemberRow.Reset();
                            LocalBytes+=73;
                        }
                    }
                }
                ++FieldId;
              }
            }
            Check(LocalBytes<=16ll*1024*1024,"members namespace quota");
            NamespaceRow.Blob(1,Ns); NamespaceRow.Integer(2,LocalBytes);
            Check(!NamespaceRow.Next(),"members namespace ledger"); NamespaceRow.Reset();
            GlobalBytes+=LocalBytes;
        }
        if(WithPrimary) {
            Statement RecordRow(Db,"INSERT INTO Records VALUES(?1,?2,?3,?4,?5)");
            Statement QuotaRow(Db,"INSERT INTO Quotas VALUES(?1,?2,6250,1)");
            Value PrimaryValue{Kind::Map}; Value DenseArray{Kind::Array};
            for(unsigned Item=0;Item<1024;++Item) {
                auto Element=std::make_shared<Value>(); Element->Type=Kind::Boolean;
                Element->Boolean=Item%2!=0; DenseArray.Array.push_back(std::move(Element));
            }
            PrimaryValue.Map.emplace_back("A",std::make_shared<Value>(std::move(DenseArray)));
            PrimaryValue.Map.emplace_back("Padding",std::make_shared<Value>(Text(std::string(320,'p'))));
            int64_t PrimaryBytes=0;
            for(unsigned Domain=0;Domain<16;++Domain) {
                const std::string Package(1,char('e'+Domain));
                const std::string Ns{char(1),Package[0]}; int64_t LocalPrimary=0;
                for(unsigned Index=0;Index<6250;++Index) {
                    const std::string KeyName="K"+std::string(4-std::to_string(Index).size(),'0')+std::to_string(Index);
                    const Identity Id{true,Package,"Data",KeyName};
                    const auto Envelope=Encode(Id,PrimaryValue,Until());
                    const auto ChargeValue=int64_t(Id.Store.size()+Id.Key.size()+Envelope.size());
                    RecordRow.Blob(1,Ns); RecordRow.Blob(2,Id.Store); RecordRow.Blob(3,Id.Key);
                    RecordRow.Blob(4,Envelope); RecordRow.Integer(5,ChargeValue);
                    Check(!RecordRow.Next(),"members primary record"); RecordRow.Reset(); LocalPrimary+=ChargeValue;
                }
                Check(LocalPrimary<=16ll*1024*1024,"members primary namespace quota");
                QuotaRow.Blob(1,Ns); QuotaRow.Integer(2,LocalPrimary);
                Check(!QuotaRow.Next(),"members primary quota"); QuotaRow.Reset(); PrimaryBytes+=LocalPrimary;
            }
            Check(PrimaryBytes<=256ll*1024*1024,"members primary global quota");
            Statement PrimaryTotals(Db,"UPDATE Totals SET Bytes=?1,Keys=100000,Namespaces=16 WHERE Id=1");
            PrimaryTotals.Integer(1,PrimaryBytes); Check(!PrimaryTotals.Next(),"members primary totals");
        }
        Check(GlobalBytes<=64ll*1024*1024,"members global quota");
        Statement Totals(Db,"UPDATE DerivedTotals SET NextField=?1,NextGeneration=?2,NextPrefix=?3,NextEntry=?4,Bytes=?5 WHERE Id=1");
        Totals.Integer(1,FieldId); Totals.Integer(2,GenerationId); Totals.Integer(3,PrefixId);
        Totals.Integer(4,EntryId); Totals.Integer(5,GlobalBytes);
        Check(!Totals.Next(),"members totals");
        Db.Exec("COMMIT");
    }
    const auto Started=Clock::now();
    try {
        Backend Reopened(Folder,Started+std::chrono::seconds(30));
        Check(Reopened.Available(),"members reopen available");
        std::printf("[CarbonLuau:Persistence] startup-members each=%u primary=%d oversized=%d members=%lld charge=%lld elapsed_ms=%lld result=ready\n",
            PerGeneration,int(WithPrimary),int(Oversized),
            static_cast<long long>(Oversized ? 4*3*8*2*PerGeneration : EntryId-1),static_cast<long long>(GlobalBytes),
            static_cast<long long>(std::chrono::duration_cast<std::chrono::milliseconds>(Clock::now()-Started).count()));
    } catch(const Failure& Problem) {
        std::printf("[CarbonLuau:Persistence] startup-members each=%u primary=%d oversized=%d members=%lld charge=%lld elapsed_ms=%lld result=error_%u\n",
            PerGeneration,int(WithPrimary),int(Oversized),
            static_cast<long long>(Oversized ? 4*3*8*2*PerGeneration : EntryId-1),static_cast<long long>(GlobalBytes),
            static_cast<long long>(std::chrono::duration_cast<std::chrono::milliseconds>(Clock::now()-Started).count()),
            unsigned(Problem.Code));
    }
}
void NearEnvelopeMemory(const std::filesystem::path& Parent)
{
    const auto Folder=Fixture(Parent,"near-envelope-memory");
    const Identity Id{false,"","Memory","Key"};
    auto Wide=[&](unsigned Revision) {
        Value Root;
        for(unsigned Field=0;Field<8;++Field)
            Root.Map.emplace_back("F"+std::to_string(Field),
                std::make_shared<Value>(Numeric(double(Revision+Field))));
        for(unsigned Padding=0;Padding<4;++Padding)
            Root.Map.emplace_back("Padding"+std::to_string(Padding),
                std::make_shared<Value>(Text(std::string(15000,char('a'+Padding+Revision%2)))));
        return Root;
    };
    auto Deep=[&](unsigned Revision) {
        Value Root;
        for(unsigned Field=0;Field<8;++Field)
            Root.Map.emplace_back("F"+std::to_string(Field),
                std::make_shared<Value>(Numeric(double(Revision+Field))));
        Value Data{Kind::Array};
        for(unsigned Index=0;Index<1020;++Index) {
            Value Inner{Kind::Array};
            for(unsigned Part=0;Part<3;++Part) {
                auto Flag=std::make_shared<Value>(); Flag->Type=Kind::Boolean;
                Flag->Boolean=((Index+Part+Revision)&1)!=0;
                Inner.Array.push_back(std::move(Flag));
            }
            Data.Array.push_back(std::make_shared<Value>(std::move(Inner)));
        }
        Root.Map.emplace_back("Data",std::make_shared<Value>(std::move(Data)));
        return Root;
    };
    {
        Backend Store(Folder,Until());
        const auto First=Wide(0),Second=Wide(1);
        Check(Encode(Id,First,Until()).size()>60000,"near-maximum envelope fixture");
        Set(Store,Id,First);
        for(unsigned Field=0;Field<8;++Field)
            Check(Store.PrepareDerived(Id,"F"+std::to_string(Field),Until()),
                "eight-field memory preparation");
        Drain(Store);
        Set(Store,Id,Second);
        const auto GraphA=Deep(0),GraphB=Deep(1);
        Set(Store,Id,GraphA);
        Set(Store,Id,GraphB);
        Check(Store.Available(),"near-envelope and maximum-graph worker health");
    }
    for(unsigned Field=0;Field<8;++Field)
        CheckProjection(Folder,Id,false,"F"+std::to_string(Field));
    sqlite3_int64 Current=0,Peak=0;
    Check(sqlite3_status64(SQLITE_STATUS_MEMORY_USED,&Current,&Peak,0)==SQLITE_OK &&
        Peak<128ll*1024*1024,"near-envelope SQLite hard memory bound");
    std::printf("[CarbonLuau:Persistence] near-envelope/4089-entry graph; eight ACTIVE fields; SQLite high-water=%lld bytes PASS\n",
        static_cast<long long>(Peak));
}
}

int main(int Count, char** Arguments)
{
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS | SEM_NOGPFAULTERRORBOX | SEM_NOOPENFILEERRORBOX);
#endif
    try {
        // Mirror the supervised worker's SQLite-only allocation backstop.
        // This is an observed suite high-water, not a proof of process RSS or
        // every legal retained history.
        constexpr sqlite3_int64 SqliteLimit=128ll*1024*1024;
        Check(sqlite3_config(SQLITE_CONFIG_MEMSTATUS,1)==SQLITE_OK,"SQLite memory accounting available");
        Check(sqlite3_hard_heap_limit64(SqliteLimit)>=0 &&
            sqlite3_hard_heap_limit64(-1)==SqliteLimit,"SQLite hard heap limit configured");
        if ((Count == 3 || Count == 4) && std::string(Arguments[1]) == "--verify-cardinality") {
            Check(Count == 3 || std::string(Arguments[3]) == "--normal-deadline", "cardinality verification mode");
            const auto Folder = std::filesystem::absolute(Arguments[2]);
            const auto Started = Clock::now();
            {
                Backend Reopened(Folder, Started + std::chrono::seconds(Count == 4 ? 30 : 90));
                Check(Reopened.Available(), "extended cardinality reopen available");
                const Identity Id{true,"e","Data","K0000"};
                const auto Read = Reopened.Execute(Operation::Get, Id, {}, Until());
                Check(Read.Code == Error::None && Read.Found, "retained authoritative primary record");
            }
            Connection Db(Folder);
            std::printf("[CarbonLuau:Persistence] extended-cardinality-verify records=%lld generations=%lld elapsed_ms=%lld result=ready\n",
                static_cast<long long>(Scalar(Db,"SELECT count(*) FROM Records")),
                static_cast<long long>(Scalar(Db,"SELECT count(*) FROM DerivedGenerations")),
                static_cast<long long>(std::chrono::duration_cast<std::chrono::milliseconds>(Clock::now() - Started).count()));
            return 0;
        }
        const auto Root = std::filesystem::absolute(std::filesystem::current_path() /
            ("derived-" + std::to_string(Clock::now().time_since_epoch().count())));
        Check(std::filesystem::create_directory(Root), "fresh derived fixture root");
        if (Count == 2 && std::string(Arguments[1]) == "--memory-near-envelope") {
            NearEnvelopeMemory(Root); return 0;
        }
        if (Count == 2 && std::string(Arguments[1]) == "--readiness-focused") {
            ReadinessAndUnverifiedWrites(Root); BuildingRestartVerification(Root);
            PausedVerificationAndRetry(Root); LogicalCorruption(Root);
            BoundedStartupQuarantine(Root); InvalidDerivedStructure(Root);
            EmptyPrimaryQuarantine(Root);
            std::puts("[CarbonLuau:Persistence] Focused derived readiness tests PASS");
            return 0;
        }
        if ((Count == 3 || Count == 4) && (std::string(Arguments[1]) == "--startup-cardinality" ||
            std::string(Arguments[1]) == "--readiness-cardinality")) {
            const auto PrimaryMode = Count == 4 ? std::string(Arguments[3]) : std::string();
            Check(Count == 3 || PrimaryMode == "--primary" || PrimaryMode == "--primary-heavy" ||
                PrimaryMode == "--primary-heavy-active" || PrimaryMode == "--primary-dense-only" ||
                PrimaryMode == "--primary-dense-padded", "cardinality mode");
            StartupCardinality(Root, unsigned(std::stoul(Arguments[2])), Count == 4,
                PrimaryMode == "--primary-heavy" || PrimaryMode == "--primary-heavy-active",
                PrimaryMode == "--primary-heavy-active", PrimaryMode == "--primary-dense-only" ||
                PrimaryMode == "--primary-dense-padded", PrimaryMode == "--primary-dense-padded",
                std::string(Arguments[1]) == "--readiness-cardinality");
            return 0;
        }
        if (Count == 3 && (std::string(Arguments[1]) == "--startup-members" ||
            std::string(Arguments[1]) == "--startup-members-primary" ||
            std::string(Arguments[1]) == "--startup-oversized-members" ||
            std::string(Arguments[1]) == "--startup-oversized-members-primary")) {
            const auto Mode=std::string(Arguments[1]);
            StartupMembers(Root,unsigned(std::stoul(Arguments[2])),
                Mode == "--startup-members-primary" || Mode == "--startup-oversized-members-primary",
                Mode == "--startup-oversized-members" || Mode == "--startup-oversized-members-primary");
            return 0;
        }
        Check(Count == 1, "derived test arguments");
        Fresh(Root); Upgrade(Root); FieldLimit(Root); Online(Root); IsolationAndSplit(Root); ByteBatch(Root);
        RealNamespaceQuota(Root); RealGlobalQuota(Root);
        Replacement(Root); LogicalCorruption(Root); LogicalCorruption(Root, true);
        BoundedStartupQuarantine(Root); InvalidDerivedStructure(Root); EmptyPrimaryQuarantine(Root);
        ReadinessAndUnverifiedWrites(Root); BuildingRestartVerification(Root);
        PausedVerificationAndRetry(Root);
        sqlite3_int64 Current=0,Peak=0;
        Check(sqlite3_status64(SQLITE_STATUS_MEMORY_USED,&Current,&Peak,0)==SQLITE_OK &&
            Peak<SqliteLimit,"derived suite under SQLite hard heap limit");
        std::printf("[CarbonLuau:Persistence] Derived SQLite allocated high-water=%lld bytes; worker SQLite cap=%lld bytes\n",
            static_cast<long long>(Peak),static_cast<long long>(SqliteLimit));
        std::puts("[CarbonLuau:Persistence] Derived tests PASS; disposable fixtures retained");
        return 0;
    } catch (const Failure& Problem) {
        std::fprintf(stderr, "[CarbonLuau:Persistence] Derived tests failure code=%u\n", unsigned(Problem.Code));
        return 1;
    } catch (const std::exception& Problem) {
        std::fprintf(stderr, "[CarbonLuau:Persistence] Derived tests failed: %s\n", Problem.what());
        return 1;
    }
}
