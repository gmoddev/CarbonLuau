// Exact-pin bytecode certificate, not an observed-average work claim.
#define main DerivedFunctionalMain
#include "DerivedTests.cpp"
#undef main
#include <fstream>
#include <functional>
#include <regex>

namespace {
uint32_t Threshold=0;
bool Counting=false;
uint64_t Instructions=0,Statements=0;
}
namespace CarbonLuau::Persistence {
uint32_t TestInstructionThreshold(uint32_t Requested){return Threshold?std::min(Threshold,Requested):Requested;}
void TestStatementInstructions(sqlite3_stmt* Statement) noexcept
{
    if(Counting){Instructions+=uint64_t(sqlite3_stmt_status(Statement,SQLITE_STMTSTATUS_VM_STEP,0));++Statements;}
}
}
namespace {
std::string ReadSource(const char* Path)
{
    std::ifstream Input(Path,std::ios::binary);Check(bool(Input),"certificate source available");
    return std::string(std::istreambuf_iterator<char>(Input),{});
}
struct Opcode { std::string Name; int P1=0,P2=0,P3=0; };
struct Certificate { unsigned Programs=0,MaximumSpan=0,MaximumProgram=0; };
void Certify(Connection& Db,const std::string& Sql,const std::map<std::string,bool>& Jumps,Certificate& Result,bool WholeRollback=false)
{
    const auto Explain=std::string("EXPLAIN ")+Sql;
    Statement Query(Db,Explain.c_str());std::vector<Opcode> Program;
    while(Query.Next()){
        Check(Query.Number(0)==int64_t(Program.size()),"single bytecode program, no triggers/subprograms");
        Program.push_back({reinterpret_cast<const char*>(sqlite3_column_text(Query.Handle,1)),int(Query.Number(2)),int(Query.Number(3)),int(Query.Number(4))});
        Check(Program.size()<=4096,"bounded compiled statement");
    }
    Check(!Program.empty(),"nonempty program");
    std::vector<unsigned> Visiting(Program.size()),Lengths(Program.size());
    std::function<unsigned(int)> Span=[&](int Address)->unsigned{
        if(Address==int(Program.size()))return 0;
        Check(Address>=0 && Address<int(Program.size()),"valid bytecode edge");
        if(Visiting[Address]==2)return Lengths[Address];
        if(Visiting[Address]==1){std::fprintf(stderr,"[CarbonLuau:Persistence] unchecked cycle at %d SQL=%s\n",Address,Sql.c_str());throw std::runtime_error("unchecked instruction cycle");}
        Visiting[Address]=1;const auto& Op=Program[Address];unsigned Next=0;
        Check(Jumps.count(Op.Name)!=0,"opcode covered by pinned generated table");
        Check(Op.Name!="Program" && Op.Name!="InitCoroutine" && Op.Name!="Yield" && Op.Name!="EndCoroutine" && Op.Name!="Return" &&
            Op.Name!="VFilter" && Op.Name!="VNext" && Op.Name!="SequenceTest" && Op.Name!="SorterCompare" && Op.Name!="SeekScan" && Op.Name!="Trace",
            "no unproved indirect/subprogram/virtual-table execution");
        if(Op.Name=="Init")Check(Op.P3==0,"no alternate corruption destination, including Column path");
        if(WholeRollback)Check(Op.Name=="Init" || Op.Name=="AutoCommit" || Op.Name=="Halt" || Op.Name=="Goto","fixed rollback has no other execution");
        // Source certificate: Goto/Gosub use jump_to_p2_and_check_for_interrupt;
        // Next/Prev/SorterNext check on both outcomes. ResultRow/Halt go through
        // vdbe_return. Cut there, then evaluate EVERY address as a possible
        // beginning of the next span (including a branch destination).
        const bool Poll=Op.Name=="Halt" || (!WholeRollback && (Op.Name=="Goto" || Op.Name=="Gosub" ||
            Op.Name=="Next" || Op.Name=="Prev" || Op.Name=="SorterNext" ||
            Op.Name=="ResultRow"));
        if(!Poll){
            Next=Span(Address+1);
            if(Jumps.at(Op.Name) && Op.P2>0)Next=std::max(Next,Span(Op.P2));
            if(Op.Name=="Jump"){Next=std::max(Next,Span(Op.P1));Next=std::max(Next,Span(Op.P3));}
        }
        Visiting[Address]=2;return Lengths[Address]=1+Next;
    };
    for(size_t I=0;I<Program.size();++I)Result.MaximumSpan=std::max(Result.MaximumSpan,Span(int(I)));
    Result.MaximumProgram=std::max(Result.MaximumProgram,unsigned(Program.size()));++Result.Programs;
}
void BudgetCertificate(const std::filesystem::path& Root)
{
    const auto Source=ReadSource(CARBONLUAU_SQLITE_CERTIFICATE_SOURCE);
    std::map<std::string,bool> Jumps;
    const std::regex Definitions(R"(#define OP_(\w+)\s+[0-9]+([^\r\n]*))");
    for(std::sregex_iterator It(Source.begin(),Source.end(),Definitions),End;It!=End;++It)
        Jumps[(*It)[1].str()]=(*It)[2].str().find("jump")!=std::string::npos;
    Check(Jumps.size()>150,"pinned opcode definitions");
    const auto Engine=ReadSource(CARBONLUAU_DERIVED_CERTIFICATE_SOURCE);
    // The production engine uses complete single string literals for SQL.
    // Include forensic/startup/diagnostic queries too: stronger than maintenance.
    const std::regex Literals("\"((?:SELECT|INSERT|UPDATE|DELETE|SAVEPOINT|RELEASE|ROLLBACK)[^\"\\\\]*)\"");
    std::set<std::string> Sql{"BEGIN","BEGIN IMMEDIATE","COMMIT","SAVEPOINT CarbonLuauDerived",
        "RELEASE CarbonLuauDerived","ROLLBACK TO CarbonLuauDerived","ROLLBACK"};
    for(std::sregex_iterator It(Engine.begin(),Engine.end(),Literals),End;It!=End;++It)Sql.insert((*It)[1].str());
    // Include the fixed bounded verification-step and diagnostic families.
    // Obsolete pre-Ready global quarantine SQL is no longer compiled.
    Check(Sql.size()==87,"fixed SQL-family inventory changed; review certificate coverage");
    const auto Folder=Fixture(Root,"budget");{Backend Store(Folder,Until());}
    Connection Db(Folder,true);Certificate Result;
    int Effective=0;
    Check(sqlite3_db_config(Db.Db,SQLITE_DBCONFIG_DEFENSIVE,1,&Effective)==SQLITE_OK && Effective==1,"defensive profile");
    Check(sqlite3_db_config(Db.Db,SQLITE_DBCONFIG_TRUSTED_SCHEMA,0,&Effective)==SQLITE_OK && Effective==0,"trusted schema off");
    sqlite3_limit(Db.Db,SQLITE_LIMIT_LENGTH,70*1024);sqlite3_limit(Db.Db,SQLITE_LIMIT_SQL_LENGTH,4096);
    sqlite3_limit(Db.Db,SQLITE_LIMIT_COLUMN,16);sqlite3_limit(Db.Db,SQLITE_LIMIT_ATTACHED,0);
    sqlite3_limit(Db.Db,SQLITE_LIMIT_VARIABLE_NUMBER,16);sqlite3_limit(Db.Db,SQLITE_LIMIT_EXPR_DEPTH,32);
    sqlite3_limit(Db.Db,SQLITE_LIMIT_TRIGGER_DEPTH,0);
    Db.Exec("PRAGMA synchronous=EXTRA; PRAGMA page_size=4096; PRAGMA journal_mode=PERSIST; PRAGMA temp_store=MEMORY; PRAGMA cache_size=-4096; PRAGMA cache_spill=OFF; PRAGMA mmap_size=0; PRAGMA journal_size_limit=-1; PRAGMA max_page_count=262144");
    for(const auto& Text:Sql)Certify(Db,Text,Jumps,Result);
    Certificate Rollback;Certify(Db,"ROLLBACK",Jumps,Rollback,true);
    // Reserve 65,536 instructions before the 1,000,000 hard bound. This gate
    // proves <=4096 unchecked steps plus a <=4096 outer ROLLBACK program;
    // the remaining margin is not used to infer arbitrary-query safety.
    Check(Result.MaximumSpan<=4096 && Result.MaximumProgram<=4096 && Rollback.MaximumSpan<=4096,"certificate within reserved margin");
    std::printf("[CarbonLuau:Persistence] Fixed bytecode certificate programs=%u maximum_program=%u maximum_unchecked_span=%u rollback_total<=%u; no unchecked cycles\n",Result.Programs,Result.MaximumProgram,Result.MaximumSpan,Rollback.MaximumSpan);
}
void Exhaustion(const std::filesystem::path& Root)
{
    uint64_t Maximum=0;
    for(uint32_t Limit:{100u,500u,1000u,5000u,20000u,934464u}){
        const auto Folder=Fixture(Root,("exhaust-"+std::to_string(Limit)).c_str());
        const Identity Id{false,"","Store",""};
        {Backend Store(Folder,Until());for(unsigned I=0;I<70;++I)Set(Store,Key(Id,RecordKey(I)),Record(Text(std::to_string(I)+std::string(1000,'x'))));Check(Store.PrepareDerived(Id,"Field",Until()),"budget preparation");}
        bool Accepted;
        {
            Backend Store(Folder,Until());
            // Reopened BUILDING state is deliberately unadmitted. Finish its
            // bounded proof before measuring the foreground build batch.
            unsigned ProofSteps=0;
            while(!Store.PrepareDerived(Id,"Field",Until()))
                Check(++ProofSteps<4096 && Store.MaintainDerived(Until()),
                    "bounded verification before instruction fault");
            Threshold=Limit;Instructions=Statements=0;Counting=true;
            Accepted=Store.MaintainDerived(Until());Counting=false;Threshold=0;
            Check(Instructions<=uint64_t(Limit)+65536 && Instructions<=1000000,"actual steps include interrupted statement and rollback within reserved margin");
            Check(Statements>1,"count transaction and per-record statements");
            Maximum=std::max(Maximum,Instructions);
            std::printf("[CarbonLuau:Persistence] Maintenance threshold=%u actual_steps=%llu statements=%llu accepted=%d\n",Limit,(unsigned long long)Instructions,(unsigned long long)Statements,int(Accepted));
        }
        const auto State=Inspect(Folder,Id);
        Check(State.View.Status==Derived::State::Building && State.View.Members==(Accepted?32:0),"interrupted batch rolls back checkpoint and entries");
        {Backend Store(Folder,Until());Drain(Store);}CheckProjection(Folder,Id);
    }
    std::printf("[CarbonLuau:Persistence] Maintenance budget boundary regression max_steps=%llu PASS\n",(unsigned long long)Maximum);
}
}
int main()
{
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX|SEM_NOOPENFILEERRORBOX);
#endif
    try{
        const auto Root=std::filesystem::absolute(std::filesystem::current_path()/("derived-budget-"+std::to_string(Clock::now().time_since_epoch().count())));
        Check(std::filesystem::create_directory(Root),"fresh budget fixture");BudgetCertificate(Root);Exhaustion(Root);return 0;
    }catch(const std::exception& Error){std::fprintf(stderr,"[CarbonLuau:Persistence] Budget certificate failed: %s\n",Error.what());return 1;}
}
