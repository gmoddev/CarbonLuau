#include "Backend.hpp"
#include "Query.hpp"
#include "sqlite3.h"
#include <algorithm>
#include <cstdio>
#include <cstring>
#include <map>

using namespace CarbonLuau::Persistence;
namespace CarbonLuau::Persistence { void TestCheckpoint(const char*) {} }
namespace {
void Check(bool Good,const char* Message) { if(!Good)throw std::runtime_error(Message); }
Deadline Until(){return Clock::now()+std::chrono::seconds(30);}
struct FixtureGuard {
    std::filesystem::path Parent,Folder;
    FixtureGuard(){
        Parent=std::filesystem::weakly_canonical(std::filesystem::temp_directory_path());
        Folder=Parent/("carbonluau-query-"+std::to_string(Clock::now().time_since_epoch().count()));
        Check(Folder.parent_path()==Parent && Folder.filename().string().rfind("carbonluau-query-",0)==0,
            "fixture target confined");
        Check(std::filesystem::create_directory(Folder),"fresh fixture");
    }
    ~FixtureGuard() noexcept {
        try {
            if(Folder.parent_path()==Parent && Folder.filename().string().rfind("carbonluau-query-",0)==0 &&
                std::filesystem::is_directory(Folder) && !std::filesystem::is_symlink(Folder))
                std::filesystem::remove_all(Folder);
        } catch(...) {}
    }
};
Value Number(double Input){Value Result;Result.Type=Kind::Number;Result.Number=Input;return Result;}
Value Text(const std::string& Input){Value Result;Result.Type=Kind::String;Result.String=Input;return Result;}
Value Boolean(bool Input){Value Result;Result.Type=Kind::Boolean;Result.Boolean=Input;return Result;}
Value Record(double Input){Value Result;Result.Type=Kind::Map;
    Result.Map.emplace_back("Score",std::make_shared<Value>(Number(Input)));return Result;}
Value Record(const Value& Input){Value Result;Result.Type=Kind::Map;
    Result.Map.emplace_back("Score",std::make_shared<Value>(Input));return Result;}
Bytes Descriptor(uint32_t Flags,uint32_t Type,uint32_t Direction,uint32_t Limit,
    const std::string& Equals={},const std::string& Min={},const std::string& Max={},const std::string& Cursor={}){
    Bytes Result{'C','L','Q','D'};Put32(Result,1);Put32(Result,Flags);Put32(Result,Type);
    Put32(Result,Direction);Put32(Result,Limit);
    QueryWire::Append(Result,Equals);QueryWire::Append(Result,Min);
    QueryWire::Append(Result,Max);QueryWire::Append(Result,Cursor);return Result;
}
std::string RawNumber(double Input){uint64_t Bits;std::memcpy(&Bits,&Input,8);
    Bytes Raw;Put64(Raw,Bits);return std::string(Raw.begin(),Raw.end());}
struct Page{std::vector<std::string> Keys;std::string Cursor;};
void CheckPlan(sqlite3* Db,const char* Sql,const char* Index){
    std::string Explain="EXPLAIN QUERY PLAN ";Explain+=Sql;sqlite3_stmt* Statement=nullptr;
    Check(sqlite3_prepare_v2(Db,Explain.c_str(),-1,&Statement,nullptr)==SQLITE_OK,"prepare plan");
    bool Seek=false;
    while(sqlite3_step(Statement)==SQLITE_ROW){
        const char* Raw=reinterpret_cast<const char*>(sqlite3_column_text(Statement,3));
        std::string Detail=Raw?Raw:"";
        Check(Detail.find("SCAN ")==std::string::npos && Detail.find("TEMP B-TREE")==std::string::npos,"no full scan or temp sort");
        Seek|=Detail.find("SEARCH ")!=std::string::npos && Detail.find(Index)!=std::string::npos;
    }
    sqlite3_finalize(Statement);Check(Seek,"required indexed seek");
}
Page ParsePage(const Bytes& Data){
    Check(Data.size()>=16 && std::memcmp(Data.data(),"CLQR",4)==0 && Read32(Data.data()+4)==1,"CLQR header");
    uint32_t Count=Read32(Data.data()+8),CursorLength=Read32(Data.data()+12);size_t Position=16;
    Check(CursorLength<=1712 && CursorLength<=Data.size()-Position,"cursor length");
    Page Result;Result.Cursor.assign(reinterpret_cast<const char*>(Data.data()+Position),CursorLength);Position+=CursorLength;
    for(uint32_t Index=0;Index<Count;++Index){
        Check(Data.size()-Position>=4,"key length");uint32_t Length=Read32(Data.data()+Position);Position+=4;
        Check(Length>=1 && Length<=128 && Length<=Data.size()-Position,"key bytes");
        Result.Keys.emplace_back(reinterpret_cast<const char*>(Data.data()+Position),Length);Position+=Length;
        Check(Data.size()-Position>=4,"envelope length");Length=Read32(Data.data()+Position);Position+=4;
        Check(Length>=45 && Length<=65536 && Length<=Data.size()-Position,"envelope bytes");Position+=Length;
    }
    Check(Position==Data.size(),"trailing page data");return Result;
}
void CombinedWorkload(){
    FixtureGuard Fixture;
    Backend Store(Fixture.Folder,Until());
    Identity Root{false,"","Combined",""};
    Identity Addon{true,"combined.addon","Combined",""};
    std::map<std::string,double> RootValues,AddonValues;
    auto Set=[&](Identity Id,std::map<std::string,double>& Values,const std::string& Key,double Score){
        Id.Key=Key;
        Check(Store.Execute(Operation::Set,Id,Encode(Id,Record(Score),Until()),Until()).Code==Error::None,
            "combined Set");
        Values[Key]=Score;
    };
    auto Verify=[&](Identity Id,const std::map<std::string,double>& Values){
        Id.Key="Score";
        std::vector<std::pair<double,std::string>> Ordered;
        for(const auto& Entry:Values)Ordered.emplace_back(Entry.second,Entry.first);
        std::sort(Ordered.begin(),Ordered.end());
        std::vector<std::string> Actual;
        std::string Cursor;
        for(unsigned Pages=0;Pages<16;++Pages){
            auto Result=Store.QueryDerived(Id,Descriptor(Cursor.empty()?0:8,2,0,17,{},{},{},Cursor),Until());
            Check(Result.Code==Error::None,"combined Query");
            auto PageResult=ParsePage(Result.Envelope);
            Actual.insert(Actual.end(),PageResult.Keys.begin(),PageResult.Keys.end());
            Cursor=PageResult.Cursor;
            if(Cursor.empty())break;
            Check(Pages<15,"bounded combined pagination");
        }
        Check(Actual.size()==Ordered.size(),"combined page count");
        for(size_t Index=0;Index<Ordered.size();++Index)
            Check(Actual[Index]==Ordered[Index].second,"combined indexed order and current primary state");
    };
    for(unsigned Index=0;Index<120;++Index){
        auto Key="K"+std::to_string(1000+Index);
        Set(Root,RootValues,Key,double(Index%11));
        Set(Addon,AddonValues,Key,double((Index*3)%13));
    }
    Root.Key="Score";Addon.Key="Score";
    Check(Store.PrepareDerived(Root,"Score",Until()),"combined root prepare");
    Check(Store.PrepareDerived(Addon,"Score",Until()),"combined addon prepare");
    for(unsigned Step=0;Step<1024 && Store.HasDerivedWork();++Step)
        Check(Store.MaintainDerived(Until()),"combined initial maintenance");
    Check(!Store.HasDerivedWork(),"combined initial convergence");
    Verify(Root,RootValues);Verify(Addon,AddonValues);
    auto OldPage=Store.QueryDerived(Root,Descriptor(0,2,0,17),Until());
    Check(OldPage.Code==Error::None,"combined old generation page");
    auto OldCursor=ParsePage(OldPage.Envelope).Cursor;
    Check(!OldCursor.empty(),"combined old cursor");
    Check(Store.PrepareDerived(Root,"Score",Until(),true),"combined simultaneous ACTIVE/BUILDING");
    for(unsigned Step=0;Step<180;++Step){
        auto Key="K"+std::to_string(1000+(Step%120));
        auto& Id=Step%2==0?Root:Addon;
        auto& Values=Step%2==0?RootValues:AddonValues;
        if(Step%9==0){
            Id.Key=Key;
            Check(Store.Execute(Operation::Remove,Id,{},Until()).Code==Error::None,"combined Remove");
            Values.erase(Key);
        }else Set(Id,Values,Key,double((Step*7)%19));
        Id.Key=Key;
        auto Read=Store.Execute(Operation::Get,Id,{},Until());
        Check(Read.Code==Error::None && Read.Found==(Values.count(Key)!=0),"combined Get");
        if(Step%3==0 && Store.HasDerivedWork())
            Check(Store.MaintainDerived(Until()),"combined foreground/build progress");
        if(Step%30==0){Verify(Root,RootValues);Verify(Addon,AddonValues);}
    }
    for(unsigned Step=0;Step<1024 && Store.HasDerivedWork();++Step)
        Check(Store.MaintainDerived(Until()),"combined final maintenance");
    Check(!Store.HasDerivedWork(),"combined final convergence");
    Verify(Root,RootValues);Verify(Addon,AddonValues);
    Root.Key="Score";
    Check(Store.QueryDerived(Root,Descriptor(8,2,0,17,{},{},{},OldCursor),Until()).Code==
        QueryWire::InvalidCursor,"combined retired generation cursor");
    std::printf("[CarbonLuau:Persistence] Combined root/addon build/write/read/remove/pagination workload PASS (240 initial, 180 mutations)\n");
}
}
int main(){
    try{
        FixtureGuard Fixture;
        const auto& Folder=Fixture.Folder;
        Identity Id{false,"","Store","K"};
        std::string PreviousSessionCursor;
        {
            Backend Store(Folder,Until());
            for(unsigned Index=0;Index<6;++Index){
                Id.Key="K"+std::to_string(Index);auto Envelope=Encode(Id,Record(double(Index/2)),Until());
                Check(Store.Execute(Operation::Set,Id,Envelope,Until()).Code==Error::None,"Set");
            }
            Id.Key="Score";
            Check(Store.PrepareDerived(Id,"Score",Until()),"prepare");
            for(unsigned I=0;I<1024 && Store.HasDerivedWork();++I)Check(Store.MaintainDerived(Until()),"maintenance");
            Check(!Store.HasDerivedWork(),"maintenance converged");
            auto First=Store.QueryDerived(Id,Descriptor(0,2,0,2),Until());
            Check(First.Code==Error::None,"first query");auto One=ParsePage(First.Envelope);
            Check(One.Keys==std::vector<std::string>({"K0","K1"}) && !One.Cursor.empty(),"first page order");
            PreviousSessionCursor=One.Cursor;
            auto Second=Store.QueryDerived(Id,Descriptor(8,2,0,2,{},{},{},One.Cursor),Until());
            Check(Second.Code==Error::None,"continuation");auto Two=ParsePage(Second.Envelope);
            Check(Two.Keys==std::vector<std::string>({"K2","K3"}),"second page order");
            auto Smaller=Store.QueryDerived(Id,Descriptor(8,2,0,1,{},{},{},One.Cursor),Until());
            Check(Smaller.Code==Error::None && ParsePage(Smaller.Envelope).Keys==
                std::vector<std::string>({"K2"}),"continuation may change page limit");
            Identity OtherStore=Id;OtherStore.Store="Other";
            Check(Store.QueryDerived(OtherStore,Descriptor(8,2,0,2,{},{},{},One.Cursor),Until()).Code==
                QueryWire::InvalidCursor,"cursor cannot cross stores");
            Identity OtherNamespace=Id;OtherNamespace.Addon=true;OtherNamespace.Package="other";
            Check(Store.QueryDerived(OtherNamespace,Descriptor(8,2,0,2,{},{},{},One.Cursor),Until()).Code==
                QueryWire::InvalidCursor,"cursor cannot cross namespaces");
            Check(Store.QueryDerived(Id,Descriptor(8,2,1,2,{},{},{},One.Cursor),Until()).Code==
                QueryWire::InvalidCursor,"cursor cannot change order");
            auto Untyped=Store.QueryDerived(Id,Descriptor(0,0,0,2),Until());
            Check(Untyped.Code==Error::None && ParsePage(Untyped.Envelope).Keys==One.Keys,"sole-type inference");
            auto Reverse=Store.QueryDerived(Id,Descriptor(0,2,1,2),Until());
            Check(Reverse.Code==Error::None && ParsePage(Reverse.Envelope).Keys==
                std::vector<std::string>({"K5","K4"}),"descending order");
            Id.Key="K1";
            Check(Store.Execute(Operation::Remove,Id,{},Until()).Code==Error::None,"remove cursor boundary");
            Id.Key="Score";
            auto AfterDelete=Store.QueryDerived(Id,Descriptor(8,2,0,2,{},{},{},One.Cursor),Until());
            Check(AfterDelete.Code==Error::None && ParsePage(AfterDelete.Envelope).Keys==Two.Keys,
                "cursor does not require boundary row");
            auto Equal=Store.QueryDerived(Id,Descriptor(1,2,0,100,RawNumber(1)),Until());
            Check(Equal.Code==Error::None && ParsePage(Equal.Envelope).Keys==
                std::vector<std::string>({"K2","K3"}),"equality");
            auto Range=Store.QueryDerived(Id,Descriptor(6,2,0,100,{},RawNumber(1),RawNumber(2)),Until());
            Check(Range.Code==Error::None && ParsePage(Range.Envelope).Keys==
                std::vector<std::string>({"K2","K3","K4","K5"}),"inclusive range");
            auto Lower=Store.QueryDerived(Id,Descriptor(2,2,0,100,{},RawNumber(2)),Until());
            Check(Lower.Code==Error::None && ParsePage(Lower.Envelope).Keys==
                std::vector<std::string>({"K4","K5"}),"inclusive Min only");
            auto Upper=Store.QueryDerived(Id,Descriptor(4,2,0,100,{},{},RawNumber(0)),Until());
            Check(Upper.Code==Error::None && ParsePage(Upper.Envelope).Keys==
                std::vector<std::string>({"K0"}),"inclusive Max only");
            Check(Store.QueryDerived(Id,Descriptor(6,2,0,50,{},RawNumber(2),RawNumber(1)),Until()).Code==
                QueryWire::InvalidQuery,"Min greater than Max rejected");
            auto Forged=One.Cursor;Forged[0]=Forged[0]=='A'?'B':'A';
            Check(Store.QueryDerived(Id,Descriptor(8,2,0,2,{},{},{},Forged),Until()).Code==QueryWire::InvalidCursor,"forged cursor");
            Check(Store.QueryDerived(Id,Descriptor(0,2,0,0),Until()).Code==QueryWire::InvalidQuery,"invalid limit");
            Check(Store.QueryDerived(Id,Descriptor(1,0,0,50,RawNumber(1)),Until()).Code==QueryWire::InvalidQuery,
                "wire predicates require normalized scalar type");
            Check(Store.Available(),"invalid query keeps worker healthy");
            {
                Identity Mixed{false,"","Store","Bool"};
                auto Envelope=Encode(Mixed,Record(Boolean(false)),Until());
                Check(Store.Execute(Operation::Set,Mixed,Envelope,Until()).Code==Error::None,"boolean Set");
                Mixed.Key="Score";
                Check(Store.QueryDerived(Mixed,Descriptor(0,0,0,50),Until()).Code==QueryWire::AmbiguousFieldType,
                    "mixed type inference");
                auto False=Store.QueryDerived(Mixed,Descriptor(1,1,0,50,std::string(1,'\0')),Until());
                Check(False.Code==Error::None && ParsePage(False.Envelope).Keys==std::vector<std::string>({"Bool"}),
                    "Equals=false");
                Check(Store.QueryDerived(Mixed,Descriptor(0,1,0,50),Until()).Code==QueryWire::InvalidQuery,
                    "boolean ordered request invalid");
                Mixed.Key="Text";Envelope=Encode(Mixed,Record(Text("alpha")),Until());
                Check(Store.Execute(Operation::Set,Mixed,Envelope,Until()).Code==Error::None,"string Set");
                Mixed.Key="Score";
                auto StringEqual=Store.QueryDerived(Mixed,Descriptor(1,3,0,50,"alpha"),Until());
                Check(StringEqual.Code==Error::None && ParsePage(StringEqual.Envelope).Keys==
                    std::vector<std::string>({"Text"}),"string equality");
                Mixed.Key="Long";Envelope=Encode(Mixed,Record(Text(std::string(1025,'z'))),Until());
                Check(Store.Execute(Operation::Set,Mixed,Envelope,Until()).Code==Error::None,"oversized string primary legal");
                Mixed.Key="Score";
                Check(Store.QueryDerived(Mixed,Descriptor(0,3,0,50),Until()).Code==QueryWire::QueryUnavailable,
                    "oversized string facet unavailable");
            }
            {
                Identity Empty{false,"","Store","Missing"};
                Check(Store.PrepareDerived(Empty,"Missing",Until()),"empty field prepare");
                for(unsigned I=0;I<1024 && Store.HasDerivedWork();++I)Check(Store.MaintainDerived(Until()),"empty maintenance");
                Check(!Store.HasDerivedWork(),"empty ready");
                auto NoValues=Store.QueryDerived(Empty,Descriptor(0,0,0,50),Until());
                Check(NoValues.Code==Error::None && ParsePage(NoValues.Envelope).Keys.empty() &&
                    ParsePage(NoValues.Envelope).Cursor.empty(),"untyped complete empty field");
            }
            {
                Identity OnlyBool{false,"","Boolean","Only"};
                auto Envelope=Encode(OnlyBool,Record(Boolean(true)),Until());
                Check(Store.Execute(Operation::Set,OnlyBool,Envelope,Until()).Code==Error::None,"boolean-only Set");
                OnlyBool.Key="Score";
                Check(Store.PrepareDerived(OnlyBool,"Score",Until()),"boolean-only prepare");
                for(unsigned I=0;I<1024 && Store.HasDerivedWork();++I)Check(Store.MaintainDerived(Until()),"boolean-only maintenance");
                Check(!Store.HasDerivedWork(),"boolean-only ready");
                Check(Store.QueryDerived(OnlyBool,Descriptor(0,0,0,50),Until()).Code==QueryWire::InvalidQuery,
                    "untyped boolean-only ordered invalid");
            }
            {
                Identity Load{false,"","Load",""};
                for(unsigned Index=0;Index<150;++Index){
                    Load.Key="K"+std::to_string(1000+Index);
                    auto Envelope=Encode(Load,Record(double(Index)),Until());
                    Check(Store.Execute(Operation::Set,Load,Envelope,Until()).Code==Error::None,"bulk Set");
                }
                Load.Key="Score";Check(Store.PrepareDerived(Load,"Score",Until()),"bulk prepare");
                for(unsigned I=0;I<1024 && Store.HasDerivedWork();++I)Check(Store.MaintainDerived(Until()),"bulk maintenance");
                Check(!Store.HasDerivedWork(),"bulk ready");
                auto Bulk=Store.QueryDerived(Load,Descriptor(0,2,0,100),Until());
                Check(Bulk.Code==Error::None && Bulk.Envelope.size()<=QueryWire::MaximumPage,"bulk first page");
                auto BulkPage=ParsePage(Bulk.Envelope);
                Check(BulkPage.Keys.size()==100 && !BulkPage.Cursor.empty(),"100 item limit and lookahead");
                auto Remainder=Store.QueryDerived(Load,Descriptor(8,2,0,100,{},{},{},BulkPage.Cursor),Until());
                Check(Remainder.Code==Error::None && ParsePage(Remainder.Envelope).Keys.size()==50,"bulk continuation");
            }
            {
                Identity Large{false,"","Large",""};
                Value Root=Record(1);Value Payload;Payload.Type=Kind::Array;
                for(unsigned I=0;I<4;++I){auto Child=std::make_shared<Value>();Child->Type=Kind::String;
                    Child->String.assign(16000,char('A'+I));Payload.Array.push_back(std::move(Child));}
                Root.Map.emplace_back("Payload",std::make_shared<Value>(Payload));
                for(unsigned I=0;I<2;++I){Large.Key="Large"+std::to_string(I);
                    auto Envelope=Encode(Large,Root,Until());Check(Envelope.size()>64000,"near-limit fixture");
                    Check(Store.Execute(Operation::Set,Large,Envelope,Until()).Code==Error::None,"large Set");}
                Large.Key="Score";Check(Store.PrepareDerived(Large,"Score",Until()),"large prepare");
                for(unsigned I=0;I<1024 && Store.HasDerivedWork();++I)Check(Store.MaintainDerived(Until()),"large maintenance");
                Check(!Store.HasDerivedWork(),"large ready");
                auto FirstLarge=Store.QueryDerived(Large,Descriptor(0,2,0,100),Until());
                Check(FirstLarge.Code==Error::None && FirstLarge.Envelope.size()<=QueryWire::MaximumPage,"large page bound");
                auto FirstLargePage=ParsePage(FirstLarge.Envelope);
                Check(FirstLargePage.Keys.size()==1 && !FirstLargePage.Cursor.empty(),"near-limit item plus cursor");
                auto SecondLarge=Store.QueryDerived(Large,Descriptor(8,2,0,100,{},{},{},FirstLargePage.Cursor),Until());
                Check(SecondLarge.Code==Error::None && ParsePage(SecondLarge.Envelope).Keys.size()==1,"large continuation");
            }
            {
                Identity Expanded{false,"","Expanded",""};
                Value Root=Record(1);Value Payload;Payload.Type=Kind::Array;
                for(unsigned Group=0;Group<3;++Group){
                    auto Part=std::make_shared<Value>();Part->Type=Kind::Array;
                    for(unsigned I=0;I<1000;++I){auto Child=std::make_shared<Value>();Child->Type=Kind::Boolean;
                        Child->Boolean=true;Part->Array.push_back(std::move(Child));}
                    Payload.Array.push_back(std::move(Part));
                }
                Root.Map.emplace_back("Payload",std::make_shared<Value>(Payload));
                for(unsigned I=0;I<3;++I){Expanded.Key="Item"+std::to_string(I);
                    auto Envelope=Encode(Expanded,Root,Until());
                    Check(Store.Execute(Operation::Set,Expanded,Envelope,Until()).Code==Error::None,"expanded Set");}
                Expanded.Key="Score";Check(Store.PrepareDerived(Expanded,"Score",Until()),"expanded prepare");
                for(unsigned I=0;I<1024 && Store.HasDerivedWork();++I)Check(Store.MaintainDerived(Until()),"expanded maintenance");
                Check(!Store.HasDerivedWork(),"expanded ready");
                auto Page=Store.QueryDerived(Expanded,Descriptor(0,2,0,100),Until());
                Check(Page.Code==Error::None,"expanded query");auto Parsed=ParsePage(Page.Envelope);
                Check(Parsed.Keys.size()==2 && !Parsed.Cursor.empty(),"8192 wrapper/graph bound");
            }
            sqlite3* Db=nullptr;
            Check(sqlite3_open_v2((Folder/"store.sqlite3").u8string().c_str(),&Db,SQLITE_OPEN_READONLY,nullptr)==SQLITE_OK,"plan DB");
            for(const char* Sql:{QueryWire::PrefixAscending,QueryWire::PrefixDescending})
                CheckPlan(Db,Sql,"DerivedPrefixOrder");
            for(const char* Sql:{QueryWire::EntryAscending,QueryWire::EntryDescending})
                CheckPlan(Db,Sql,"DerivedEntryOrder");
            CheckPlan(Db,QueryWire::MemberPoint,"PRIMARY KEY");
            CheckPlan(Db,QueryWire::PrimaryPoint,"PRIMARY KEY");
            sqlite3_close(Db);
            Id.Key="Score";
            Check(Store.PrepareDerived(Id,"Score",Until(),true),"forced replacement generation");
            for(unsigned I=0;I<1024 && Store.HasDerivedWork();++I)Check(Store.MaintainDerived(Until()),"replacement maintenance");
            Check(!Store.HasDerivedWork(),"replacement ready");
            Check(Store.QueryDerived(Id,Descriptor(8,2,0,2,{},{},{},One.Cursor),Until()).Code==QueryWire::InvalidCursor,
                "old generation cursor cannot retarget");
            // Test-only impossible graph: >103 empty prefixes under an
            // admitted generation. The bounded walker must fail closed, not
            // mistake a capped prefix SELECT for an exhausted result set.
            {
                Identity Sparse{false,"","Sparse","Score"};
                Check(Store.PrepareDerived(Sparse,"Score",Until()),"sparse prepare");
                for(unsigned I=0;I<1024 && Store.HasDerivedWork();++I)Check(Store.MaintainDerived(Until()),"sparse maintenance");
                Check(!Store.HasDerivedWork(),"sparse ready");
                Store.TestInjectEmptyPrefixes(Sparse,105);
                Check(Store.QueryDerived(Sparse,Descriptor(0,2,0,50),Until()).Code==QueryWire::QueryUnavailable,
                    ">103 prefix rows fail controlled");
                Store.TestRemoveEmptyPrefixes();
            }
        }
        {
            Backend Reopened(Folder,Until());
            Id.Key="Score";
            for(unsigned I=0;I<4096 && Reopened.HasDerivedWork();++I)Check(Reopened.MaintainDerived(Until()),"reopen proof");
            auto Page=Reopened.QueryDerived(Id,Descriptor(0,2,0,2),Until());
            Check(Page.Code==Error::None,"reopened query");
            Check(Reopened.QueryDerived(Id,Descriptor(8,2,0,2,{},{},{},PreviousSessionCursor),Until()).Code==
                QueryWire::InvalidCursor,"session secret invalidates old cursor");
        }
        CombinedWorkload();
        std::printf("[CarbonLuau:Persistence] Query worker tests PASS\n");return 0;
    }catch(const std::exception& Problem){std::fprintf(stderr,"[CarbonLuau:Persistence] Query worker tests failed: %s\n",Problem.what());return 1;}
}
