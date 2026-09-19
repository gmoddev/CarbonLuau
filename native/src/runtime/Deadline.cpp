#include "RuntimeInternal.hpp"

namespace CarbonLuau::Runtime {
// Deliberately not std::exception: script pcall/xpcall must not catch a host
// cancellation. Only thrown at non-GC safepoints. Never reuse the interrupted
// state: close the complete global VM immediately at the ABI boundary.
void Interrupt(lua_State* State, int Gc)
{
    Vm& Runtime = *static_cast<Vm*>(lua_callbacks(State)->userdata);
    if (Gc < 0 && std::chrono::steady_clock::now() >= Runtime.Deadline) throw DeadlineExceeded{};
}
} // namespace CarbonLuau::Runtime

