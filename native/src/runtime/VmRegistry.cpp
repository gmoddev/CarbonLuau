#include "RuntimeInternal.hpp"

namespace CarbonLuau::Runtime {
std::recursive_mutex RegistryMutex;
std::array<std::unique_ptr<Vm>, 32> Registry;
uint64_t NextId = 1;
uint64_t NextVmGenerationId = 1;
#ifdef CARBONLUAU_TESTING
int TestAllocationFailureAfter = -1;
uint64_t TestLiveBytes = 0;
#endif

Vm* GetVm(ClHandle Id)
{
    for (auto& Entry : Registry)
        if (Entry && Entry->Id == Id && Entry->Owner == std::this_thread::get_id()) return Entry.get();
    return nullptr;
}

Vm* GetThread(ClHandle Id)
{
    if (!Id) return nullptr;
    for (auto& Entry : Registry)
        if (Entry && Entry->ThreadId == Id && Entry->Owner == std::this_thread::get_id()) return Entry.get();
    return nullptr;
}
} // namespace CarbonLuau::Runtime

