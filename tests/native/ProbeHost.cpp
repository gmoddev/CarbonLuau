#include <cstdio>
#ifdef _WIN32
#include <windows.h>
#else
#include <dlfcn.h>
#endif

int main(int Count, char** Args)
{
    if (Count != 2) return 2;
    for (int Cycle = 0; Cycle < 100; ++Cycle)
    {
#ifdef _WIN32
        auto Handle = LoadLibraryA(Args[1]);
        if (!Handle) return 3;
        auto Probe = reinterpret_cast<int (*)()>(GetProcAddress(Handle, "carbonluau_probe"));
#else
        auto Handle = dlopen(Args[1], RTLD_NOW | RTLD_LOCAL);
        if (!Handle) { std::fprintf(stderr, "%s\n", dlerror()); return 3; }
        auto Probe = reinterpret_cast<int (*)()>(dlsym(Handle, "carbonluau_probe"));
#endif
        bool Valid = Probe && Probe() == 0x4C554155;
#ifdef _WIN32
        if (!FreeLibrary(Handle)) return 5;
#else
        if (dlclose(Handle) != 0) return 5;
#endif
        if (!Valid) return 4;
    }
    std::puts("[CarbonLuau:Test] PASS: 100 native load/probe/unload cycles, ABI 0x4C554155");
    return 0;
}
