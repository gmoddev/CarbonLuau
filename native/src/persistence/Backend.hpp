#pragma once
#include "Format.hpp"
#include <filesystem>
struct sqlite3;
namespace CarbonLuau::Persistence {
class Derived;
enum class Operation : uint32_t { Get = 1, Set = 2, Remove = 3, Demand = 4 };
struct Result { Error Code = Error::None; bool Found = false; Bytes Envelope; bool NamespacePresent = false; };
struct DemandResult { Error Code = Error::None; uint32_t Flags = 2; };

// Called exclusively by the storage worker, never by a game/VM thread.
class Backend {
public:
    Backend(const std::filesystem::path& Directory, Deadline StartupEnd);
    ~Backend();
    Backend(const Backend&) = delete;
    Backend& operator=(const Backend&) = delete;
    Result Execute(Operation Op, const Identity& Id, const Bytes& Envelope, Deadline End);
    // Key is the exact top-level field. Zero means verification/build pending;
    // one means a current-process admitted ACTIVE generation; two is unavailable.
    // Ready admits the field generation, not every scalar facet: an ACTIVE
    // field with OversizedStrings still has an unavailable string facet.
    DemandResult DemandDerived(const Identity& Id, Deadline End);
    bool Available() const { return Healthy; }
    // Private 2A test substrate; no Luau Query surface.
    bool PrepareDerived(const Identity& StoreId, const std::string& Field, Deadline End, bool Force = false);
    bool MaintainDerived(Deadline End);
    bool HasDerivedWork();
#ifdef CARBONLUAU_PERSISTENCE_TESTING
    // Deterministic real-SQLite FULL seam; never compiled into the worker.
    uint32_t TestCapAtCurrentPages();
#endif
private:
    sqlite3* Database = nullptr;
    std::filesystem::path Directory;
    Deadline End;
    bool Healthy = false;
    bool LegacySchema = false;
    // A recoverable derived maintenance failure must not restart the primary
    // worker or spin on the same failing bounded step.
    bool DerivedPaused = false;
    std::unique_ptr<Derived> Indexes;
    uint32_t InstructionBudget = 0;
    void Progress(uint32_t Instructions = 0);
    bool DerivedOperation(const Identity* Id, const std::string* Field, Deadline OperationEnd, bool Force = false);
    void Open();
    void CheckFiles();
    void CheckSchema(bool New);
    void CheckRecords();
};
}
