// Bounded value-shape/round-trip research, NOT a worst-case capacity proof.
#define main StorageFitResearchMain
#include "Probe.cpp"
#undef main
#include <limits>
namespace {
std::vector<P::Value> Shapes(){
    std::vector<P::Value> Result;P::Value V;V.Type=P::Kind::Boolean;Result.push_back(V);
    V.Type=P::Kind::Number;
    for(double Number:{-0.0,std::numeric_limits<double>::denorm_min(),std::numeric_limits<double>::max()}){V.Number=Number;Result.push_back(V);}
    for(size_t Size:{size_t(0),size_t(1),size_t(512),size_t(768),size_t(960),size_t(1024),size_t(2048),size_t(4096),size_t(16384)}){V={};V.Type=P::Kind::String;V.String.assign(Size,'s');Result.push_back(V);}
    V={};V.Type=P::Kind::Array;
    for(unsigned I=0;I<4;++I){auto Part=std::make_shared<P::Value>();Part->Type=P::Kind::String;Part->String.assign(I==3?16315:16384,'h');V.Array.push_back(Part);}Result.push_back(V);
    auto Leaf=std::make_shared<P::Value>();Leaf->Type=P::Kind::Boolean;
    V={};V.Type=P::Kind::Array;
    for(unsigned I=0;I<4;++I){auto Part=std::make_shared<P::Value>();Part->Type=P::Kind::Array;Part->Array.assign(1023,Leaf);V.Array.push_back(Part);}Result.push_back(V);
    auto Root=std::make_shared<P::Value>(),Current=Root;
    for(unsigned I=1;I<16;++I){auto Next=std::make_shared<P::Value>();Current->Map.emplace_back("nested",Next);Current=Next;}Result.push_back(*Root);
    V={};for(unsigned I=0;I<1024;++I)V.Map.emplace_back(std::string(100,'k')+std::to_string(I),Leaf);
    // Full 1024-entry maps with long keys exceed 64 KiB; use bounded short
    // keys here and test maximum map-key length independently.
    V.Map.clear();for(unsigned I=0;I<1024;++I)V.Map.emplace_back("k"+std::to_string(I),Leaf);Result.push_back(V);
    V={};V.Map.emplace_back(std::string(128,'k'),Leaf);Result.push_back(V);
    return Result;
}
void RunShapes(const std::string& Root){
    if(std::filesystem::exists(Root))throw std::runtime_error("destination exists");std::filesystem::create_directories(Root);
    auto Values=Shapes();
    for(size_t Size:{size_t(512),size_t(640),size_t(768),size_t(896),size_t(960)}){
        Connection Db(Root+"/shape-"+std::to_string(Size)+".sqlite3",true);
        Db.Sql("PRAGMA page_size=4096;PRAGMA synchronous=EXTRA;PRAGMA journal_mode=PERSIST;PRAGMA cache_spill=OFF;PRAGMA cache_size=-4096;PRAGMA mmap_size=0;PRAGMA temp_store=MEMORY;PRAGMA max_page_count=131072;CREATE TABLE Chunks(RecordId INTEGER NOT NULL,Part INTEGER NOT NULL,Payload BLOB NOT NULL,PRIMARY KEY(RecordId,Part)) WITHOUT ROWID");
        Statement Write(Db,"INSERT INTO Chunks VALUES(?1,?2,?3)"),Read(Db,"SELECT Part,Payload FROM Chunks WHERE RecordId=?1 ORDER BY Part");
        size_t Count=0,MaximumParts=0;int64_t MaximumSteps=0;
        for(unsigned Pass=0;Pass<4;++Pass)for(const auto& V:Values){
            P::Identity Id{Pass%2!=0,Pass%2?"research.storage":"",Pass<2?"S":std::string(64,'S'),Pass%2?std::string(128,'K'):"K"};
            auto Envelope=P::Encode(Id,V,P::Clock::now()+std::chrono::seconds(5));
            // Long-lived IDs exercise maximum integer field widths as well
            // as short IDs; they are private and not author-controlled.
            int64_t RecordId=Pass<2?int64_t(++Count):std::numeric_limits<int64_t>::max()-int64_t(++Count);
            Db.Sql("BEGIN IMMEDIATE;SAVEPOINT Shape");
            for(size_t At=0;At<Envelope.size();At+=Size){Write.Number(1,RecordId);Write.Number(2,At/Size);Write.Blob(3,std::string(Envelope.begin()+At,Envelope.begin()+std::min(Envelope.size(),At+Size)));Write.Done();MaximumSteps=std::max(MaximumSteps,int64_t(sqlite3_stmt_status(Write.St,SQLITE_STMTSTATUS_VM_STEP,1)));}
            if(Pass==3){Db.Sql("ROLLBACK TO Shape;RELEASE Shape;COMMIT");Read.Number(1,RecordId);if(Read.Next())throw std::runtime_error("savepoint leaked chunks");}
            else {Db.Sql("RELEASE Shape;COMMIT");Read.Number(1,RecordId);std::string Actual;int64_t Part=0;
                while(Read.Next()){if(Read.Number(0)!=Part++)throw std::runtime_error("part order");Actual+=Read.Bytes(1);}
                if(P::Bytes(Actual.begin(),Actual.end())!=Envelope)throw std::runtime_error("byte fidelity");
                P::Decode(Id,P::Bytes(Actual.begin(),Actual.end()),P::Clock::now()+std::chrono::seconds(5));MaximumParts=std::max(MaximumParts,size_t(Part));}
            Check(sqlite3_reset(Read.St));Check(sqlite3_clear_bindings(Read.St));
        }
        Integrity(Db);
        if(Scalar(Db,"SELECT count(*) FROM dbstat WHERE pagetype='overflow'")!=0)throw std::runtime_error("unexpected chunk overflow");
        std::cout<<"[CarbonLuau:Shapes] chunk="<<Size<<" cases="<<Count<<" maximum_parts="<<MaximumParts<<" pages="<<Scalar(Db,"PRAGMA page_count")<<" max_single_insert_vdbe="<<MaximumSteps<<" sqlite_memory_highwater="<<sqlite3_memory_highwater(0)<<" exact=true rollback=true overflow=0"<<std::endl;
    }
}
}
int main(int Count,char** Args){
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX|SEM_NOOPENFILEERRORBOX);
#endif
    try{if(Count!=2)throw std::runtime_error("new destination directory");RunShapes(Args[1]);return 0;}
    catch(const P::Failure& E){std::cerr<<"[CarbonLuau:Shapes] codec_error="<<unsigned(E.Code)<<std::endl;return 1;}
    catch(const std::exception& E){std::cerr<<"[CarbonLuau:Shapes] FAILED "<<E.what()<<std::endl;return 1;}
}
