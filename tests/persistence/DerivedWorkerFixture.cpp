// Disposable qualification seed/check tool. Never distributed with the worker.
#define main DerivedFunctionalMain
#include "DerivedTests.cpp"
#undef main
int main(int Count,char** Arguments)
{
#ifdef _WIN32
    SetErrorMode(SEM_FAILCRITICALERRORS|SEM_NOGPFAULTERRORBOX|SEM_NOOPENFILEERRORBOX);
#endif
    try{
        Check(Count==3,"fixture seed|verify new qualification directory");
        const std::string Mode=Arguments[1];const auto Folder=std::filesystem::absolute(Arguments[2]);
        const Identity Id{false,"","Store","Key"};
        if(Mode=="seed"){
            Check(!std::filesystem::exists(Folder) && std::filesystem::create_directory(Folder),"fresh seed only");
            Backend Store(Folder,Until());
            for(unsigned I=0;I<512;++I){
                Value Root;
                for(unsigned F=0;F<8;++F)Root.Map.emplace_back("F"+std::to_string(F),std::make_shared<Value>(Text(std::to_string(I)+std::string(1000,'a'+F))));
                Set(Store,Key(Id,I?"K"+std::to_string(I):"Key"),Root);
            }
            for(unsigned F=0;F<8;++F)Check(Store.PrepareDerived(Id,"F"+std::to_string(F),Until()),"seed field");
            Check(Store.HasDerivedWork(),"seed leaves durable maintenance work");
        }else{
            Check(Mode=="verify" && std::filesystem::exists(Folder),"known fixture verification");
            {
                Connection Db(Folder);
                Check(Scalar(Db,"SELECT count(*) FROM DerivedGenerations WHERE State=1")==0,
                    "real worker finished durable resumed builds");
            }
            {
                Backend Store(Folder,Until());
                unsigned Steps=0;
                while(Store.HasDerivedWork()) {
                    const bool Progress=Store.MaintainDerived(Until());
                    // Eight fields x 512 distinct scalar prefixes require at
                    // least two bounded turns each, plus graph/orphan scans.
                    if(!Progress || ++Steps>=32768)
                        std::fprintf(stderr,"[CarbonLuau:Persistence] reopened proof steps=%u progress=%d primary_available=%d\n",
                            Steps,int(Progress),int(Store.Available()));
                    Check(Progress && Steps<32768,"bounded reopened derived proof");
                }
                Check(Store.Available(),"reopened primary remains available");
            }
            for(unsigned F=0;F<8;++F)CheckProjection(Folder,Id,false,"F"+std::to_string(F));
            Connection Db(Folder);Check(Scalar(Db,"SELECT count(*) FROM Records")==512,"foreground final Set restored key");
        }
        std::printf("[CarbonLuau:Persistence] Worker derived fixture %s PASS\n",Mode.c_str());return 0;
    }catch(const std::exception& Error){std::fprintf(stderr,"[CarbonLuau:Persistence] Derived worker fixture failed: %s\n",Error.what());return 1;}
}
