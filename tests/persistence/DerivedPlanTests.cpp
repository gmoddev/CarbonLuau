// Test-only fixed seek-family qualification. No production Query implementation.
#define main DerivedFunctionalMain
#include "DerivedTests.cpp"
#undef main
#include <limits>

namespace {
struct Work { unsigned Candidates=0,Lookups=0; int64_t Instructions=0; };
Bytes PrefixOf(const Bytes& Key) { return Bytes(Key.begin(),Key.begin()+std::min(size_t(512),Key.size())); }
Bytes SuffixOf(const Bytes& Key) { return Key.size()>512 ? Bytes(Key.begin()+512,Key.end()) : Bytes{}; }
void Blob(Statement& Query,int Index,const Bytes& Data)
{
    // SQLite distinguishes an empty BLOB from NULL; vector::data may be null.
    Check(sqlite3_bind_blob(Query.Handle,Index,Data.empty()?static_cast<const void*>(""):Data.data(),int(Data.size()),SQLITE_TRANSIENT)==SQLITE_OK,"plan byte bind");
}
void Plan(Connection& Db,const char* Sql,const char* Index)
{
    const auto Explain=std::string("EXPLAIN QUERY PLAN ")+Sql;
    Statement Query(Db,Explain.c_str()); bool Searched=false;
    while(Query.Next()) {
        const std::string Detail(reinterpret_cast<const char*>(sqlite3_column_text(Query.Handle,3)));
        Check(Detail.find("SCAN ")==std::string::npos && Detail.find("TEMP B-TREE")==std::string::npos,"no scan or temporary sort");
        Searched|=Detail.find("SEARCH ")!=std::string::npos && Detail.find(Index)!=std::string::npos;
    }
    Check(Searched,"expected constrained index seek");
}
void Count(Statement& Query,Work& Used)
{
    Check(sqlite3_stmt_status(Query.Handle,SQLITE_STMTSTATUS_FULLSCAN_STEP,0)==0,"no full scan steps");
    Check(sqlite3_stmt_status(Query.Handle,SQLITE_STMTSTATUS_SORT,0)==0,"no sort");
    Used.Instructions+=sqlite3_stmt_status(Query.Handle,SQLITE_STMTSTATUS_VM_STEP,0);
    Check(Used.Instructions<=1000000,"aggregate VDBE work bound");
}
std::vector<OrderedEntry> Seek(Connection& Db,int64_t Generation,const Bytes& Lower,const Bytes& Upper,
    bool Descending,const OrderedEntry* After,Work& Used)
{
    // Two ordered levels, not an unrestricted join + ORDER BY. Prefix ids are
    // bound identities; their numeric ordering is never treated as value order.
    const char* PrefixSql=Descending ?
        "SELECT Id,Prefix FROM DerivedPrefixes WHERE Generation=?1 AND Prefix>=?2 AND Prefix<=?3 ORDER BY Prefix DESC LIMIT 102" :
        "SELECT Id,Prefix FROM DerivedPrefixes WHERE Generation=?1 AND Prefix>=?2 AND Prefix<=?3 ORDER BY Prefix ASC LIMIT 102";
    const char* EntrySql=Descending ?
        "SELECT Suffix,RecordKey FROM DerivedEntries WHERE PrefixId=?1 AND Suffix>=?2 AND Suffix<=?3 ORDER BY Suffix DESC,RecordKey DESC LIMIT ?4" :
        "SELECT Suffix,RecordKey FROM DerivedEntries WHERE PrefixId=?1 AND Suffix>=?2 AND Suffix<=?3 ORDER BY Suffix ASC,RecordKey ASC LIMIT ?4";
    const char* ContinueSql=Descending ?
        "SELECT Suffix,RecordKey FROM DerivedEntries WHERE PrefixId=?1 AND Suffix>=?2 AND Suffix<=?3 AND (Suffix,RecordKey)<(?5,?6) ORDER BY Suffix DESC,RecordKey DESC LIMIT ?4" :
        "SELECT Suffix,RecordKey FROM DerivedEntries WHERE PrefixId=?1 AND Suffix>=?2 AND Suffix<=?3 AND (Suffix,RecordKey)>(?5,?6) ORDER BY Suffix ASC,RecordKey ASC LIMIT ?4";
    Plan(Db,PrefixSql,"DerivedPrefixOrder");Plan(Db,EntrySql,"DerivedEntryOrder");Plan(Db,ContinueSql,"DerivedEntryOrder");
    Bytes LowPrefix=PrefixOf(Lower),HighPrefix=PrefixOf(Upper);
    const auto CursorPrefix=After?PrefixOf(After->first):Bytes{};
    if(After){if(Descending)HighPrefix=std::min(HighPrefix,CursorPrefix);else LowPrefix=std::max(LowPrefix,CursorPrefix);}
    Statement Prefixes(Db,PrefixSql);Prefixes.Integer(1,Generation);Blob(Prefixes,2,LowPrefix);Blob(Prefixes,3,HighPrefix);
    std::vector<OrderedEntry> Result;
    while(Result.size()<101 && Prefixes.Next()) {
        const auto Prefix=Prefixes.Data(1); const bool Continuing=After && Prefix==CursorPrefix;
        Statement Entries(Db,Continuing?ContinueSql:EntrySql);
        Entries.Integer(1,Prefixes.Number(0));
        Blob(Entries,2,Prefix==PrefixOf(Lower)?SuffixOf(Lower):Bytes{});
        Blob(Entries,3,Prefix==PrefixOf(Upper)?SuffixOf(Upper):Bytes(514,255));
        Entries.Integer(4,101-int64_t(Result.size()));
        if(Continuing){Blob(Entries,5,SuffixOf(After->first));Entries.Blob(6,After->second);}
        while(Entries.Next()){
            auto Full=Prefix;auto Suffix=Entries.Data(0);Full.insert(Full.end(),Suffix.begin(),Suffix.end());
            auto Key=Entries.Data(1);Result.emplace_back(std::move(Full),std::string(Key.begin(),Key.end()));++Used.Candidates;
        }
        Count(Entries,Used);
    }
    Count(Prefixes,Used);
    Check(Used.Candidates<=101,"candidate ceiling");
    return Result;
}
void Plans(const std::filesystem::path& Root)
{
    const auto Folder=Fixture(Root,"plans");const Identity Id{false,"","Store",""};
    {
        Backend Store(Folder,Until());
        for(unsigned I=0;I<700;++I){
            Value Input=I<200?Numeric(double(int(I%90)-45)):I<600?
                Text((I%3?std::string(511,'x'):std::string())+std::to_string(I%170)):Flag(I%2!=0);
            Set(Store,Key(Id,"Key"+std::to_string(I)),Record(Input));
        }
        const std::vector<double> Boundaries{-std::numeric_limits<double>::max(),-std::numeric_limits<double>::min(),
            -std::numeric_limits<double>::denorm_min(),-0.0,0.0,std::numeric_limits<double>::denorm_min(),
            std::numeric_limits<double>::min(),std::numeric_limits<double>::max()};
        for(size_t I=0;I<Boundaries.size();++I)Set(Store,Key(Id,"Boundary"+std::to_string(I)),Record(Numeric(Boundaries[I])));
        for(const std::string TextValue:{"A","a","\xc3\xa9","\xe4\xb8\xad"})
            Set(Store,Key(Id,"Unicode"+TextValue),Record(Text(TextValue)));
        Check(Store.PrepareDerived(Id,"Field",Until()),"plan prepare");Drain(Store);
    }
    const auto All=Inspect(Folder,Id);CheckProjection(Folder,Id);
    Connection Db(Folder);Db.Exec("BEGIN");
    // Startup's store cursor must seek the unique field-name index. A grouped
    // scan here multiplied the validation cost by the retained store count.
    Plan(Db,"SELECT Namespace,Store FROM DerivedFields WHERE (Namespace,Store)>(?1,?2) ORDER BY Namespace,Store LIMIT 1",
        "DerivedFieldNames");
    {
        Statement Query(Db,"EXPLAIN QUERY PLAN SELECT G.Id,G.FieldId,G.State,G.Checkpoint,G.Members,G.Booleans,G.Numbers,G.Strings,G.Oversized,G.Charge,F.Id,F.Namespace,F.Store,F.Name,F.Charge FROM DerivedFields F CROSS JOIN DerivedGenerations G INDEXED BY DerivedGenerationFields ON G.FieldId=F.Id WHERE F.Namespace=?1 AND F.Store=?2 AND G.Id>?3 ORDER BY G.Id");
        bool FieldSeek=false, GenerationSeek=false;
        while(Query.Next()) {
            const std::string Detail(reinterpret_cast<const char*>(sqlite3_column_text(Query.Handle,3)));
            Check(Detail.find("SCAN ")==std::string::npos,"store generations cannot full-scan");
            FieldSeek|=Detail.find("SEARCH F ")!=std::string::npos && Detail.find("DerivedFieldNames")!=std::string::npos;
            GenerationSeek|=Detail.find("SEARCH G ")!=std::string::npos && Detail.find("DerivedGenerationFields")!=std::string::npos;
        }
        // The final sort covers at most eight fields/two generations each.
        Check(FieldSeek && GenerationSeek,"bounded per-store generation seeks");
    }
    int64_t MaxInstructions=0;unsigned Cases=0;
    const std::vector<std::pair<Bytes,Bytes>> Bounds{
        {Bytes{1},Bytes{4}}, // ascending/descending top-N
        {SortScalar(Numeric(0)),SortScalar(Numeric(0))}, // duplicate equality including +/-zero
        {SortScalar(Numeric(-20)),Bytes{3}}, // lower bound within type
        {Bytes{2},SortScalar(Numeric(20))}, // upper bound
        {SortScalar(Numeric(-10)),SortScalar(Numeric(10))}, // bounded range
        {SortScalar(Text("")),SortScalar(Text(std::string(1024,'z')))},
        {SortScalar(Text(std::string(511,'x')+"12")),SortScalar(Text(std::string(511,'x')+"16"))},
        {SortScalar(Flag(false)),SortScalar(Flag(true))}
    };
    for(const auto& Bound:Bounds)for(bool Descending:{false,true}){
        std::vector<OrderedEntry> Expected;
        for(const auto& Entry:All.Entries)if(Entry.first>=Bound.first && Entry.first<=Bound.second)Expected.push_back(Entry);
        if(Descending)std::reverse(Expected.begin(),Expected.end());
        std::vector<OrderedEntry> Combined;OrderedEntry Last;bool HasLast=false;
        do{
            Work Used;auto Page=Seek(Db,All.View.Generation,Bound.first,Bound.second,Descending,HasLast?&Last:nullptr,Used);
            // Future materialization is one exact primary lookup per candidate;
            // count it independently without exposing Query or cursor objects.
            for(const auto& Entry:Page){
                Statement Primary(Db,"SELECT Envelope FROM Records WHERE Namespace=?1 AND Store=?2 AND Key=?3");
                Primary.Blob(1,Namespace(Id));Primary.Blob(2,Id.Store);Primary.Blob(3,Entry.second);
                Check(Primary.Next(),"candidate primary exists");Decode(Key(Id,Entry.second),Primary.Data(0),Until());
                Check(!Primary.Next(),"unique primary");++Used.Lookups;Count(Primary,Used);
            }
            Check(Used.Lookups==Used.Candidates,"exact point lookup accounting");
            MaxInstructions=std::max(MaxInstructions,Used.Instructions);++Cases;
            Combined.insert(Combined.end(),Page.begin(),Page.end());
            if(Page.size()<101)break;
            Last=Page.back();HasLast=true;
            Check(Cases<200,"bounded test continuation");
        }while(true);
        if(Combined!=Expected)std::fprintf(stderr,"[CarbonLuau:Persistence] plan mismatch descending=%d expected=%zu actual=%zu pages=%u\n",int(Descending),Expected.size(),Combined.size(),Cases);
        Check(Combined==Expected,"seek/range/order/continuation exact corpus parity");
    }
    Db.Exec("COMMIT");
    std::printf("[CarbonLuau:Persistence] Derived fixed plans PASS pages=%u max_vdbe=%lld fullscan=0 sorts=0 candidates_per_page<=101\n",Cases,(long long)MaxInstructions);
}
}
int main()
{
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX|SEM_NOOPENFILEERRORBOX);
#endif
    try{
        const auto Root=std::filesystem::absolute(std::filesystem::current_path()/("derived-plans-"+std::to_string(Clock::now().time_since_epoch().count())));
        Check(std::filesystem::create_directory(Root),"fresh plan root");Plans(Root);return 0;
    }catch(const std::exception& Problem){std::fprintf(stderr,"[CarbonLuau:Persistence] Derived plan failure: %s\n",Problem.what());return 1;}
}
