#pragma once
#include "Sortable.hpp"

struct sqlite3;

namespace CarbonLuau::Persistence {
struct DerivedVerification;
// Private worker-only Persistence-2A engine. No public Query API or SQLite handle
// accessor. The connection and referenced deadline must outlive this object.
//
// SQLite-touching methods require an existing caller-owned transaction.
// VerificationComplete/VerificationFailed/InvalidateVerification are memory-only.
// Create/Prepare/Maintain/Mutate/VerifyStep require a write transaction;
// Inspect/Diagnostics/HasWork may use a read transaction. No method begins or
// commits a transaction.
// On ANY exception the caller must roll back the entire transaction and apply
// D21 error/uncertain-outcome handling. A false Prepare result is a controlled
// logical admission failure, not a SQLite error. Mutate must run after the
// primary row changes, before its transaction commits; Old/New are decoded F1
// snapshots (nullptr means absent). Never replay a dispatched primary mutation.
//
// The backend MUST install its operation progress handler before calling this
// engine: <=1,000,000 aggregate VDBE instructions per maintenance operation and
// the original operation deadline. Do not reset that budget per SQL statement.
// The engine checks End between rows, but cannot interrupt sqlite3_step itself.
//
// Maintain performs one round-robin build or cleanup batch, <=32 primary/member
// rows and <=256 KiB primary envelopes. Metadata operations are additional and
// bounded. Capacity failure withdraws the building generation atomically; it
// does not automatically retry unavailable fields. Prepare can request a retry.
// At most two total retained generations per field (including cleanup); at most
// one ACTIVE and one BUILDING. Fields/namespace ledgers are retained and charged.
//
// VerifyStep performs authority/accounting and representation proof in bounded turns.
// No durable ACTIVE state is Query-admitted until its full current-process
// proof completes. InvalidateVerification resets that admission before a
// foreground write which skips index maintenance. Only serialized owner-thread
// access is supported: while verifying, the caller pauses Prepare/Maintain and
// invalidates on every foreground primary mutation. On a failed verification
// transaction, call InvalidateVerification before retrying.
// VerifyStep first streams and proves the ENTIRE authority/accounting graph.
// Only then may a coherent representation differing from verified primary
// values/completeness be withdrawn for bounded cleanup. Unknown metadata,
// malformed sort keys, accounting/counter errors, primary corruption and SQL
// failures fail closed. It never repairs counters or drops uncharged records.
// The backend proves SQLite integrity and F1 primary records/quotas before
// Ready; the post-Ready verifier checks retained derived authority in bounded
// transactions. The backend owns whole-database schema/profile checks,
// physical limits, transaction rollback, and post-commit checks. Schema() includes fixed
// CREATE INDEX statements as well as tables; no dynamic per-field schema exists.
//
// Format v1 charge: one row-kind byte, u32 length + bytes for every BLOB,
// u64 for each ID/count/charge/ledger value, u8 for state/type. Authority flows
// through charged field/generation/prefix/member mappings, never free side data.
// SQL secondary-index/B-tree overhead is PHYSICAL, not logical encoded charge.
// Global/namespace Bytes INCLUDE their own metadata Charge; "empty" therefore
// means no data entries, not zero charged metadata. High-water IDs never reset
// after committed cleanup (an aborted allocation may be reused).
//
// Split representation: full tagged SortKey = Prefix[<=512] + Suffix[<=513].
// Prefixes are ordered within Generation; entries within Prefix by Suffix,Key.
// Prefix IDs are identities, NOT ordering ranks. Future Query must seek in both
// levels, preserve full-key cursors, and qualify its fixed plans/work budget.
// This implementation is NOT a physical-fit or platform qualification claim.
//
// Test builds reuse TestCheckpoint(Stage). Stages are derived-before/after-
// savepoint, release, rollback-to, rollback-release, checkpoint, publish,
// cleanup, withdraw (each spelled "derived-before-savepoint", etc.). ONLY at
// "derived-capacity-after-change", a test callback's Failure(QuotaExceeded)
// injects a private logical Capacity after partial edits. Other exceptions and
// other stages propagate normally. Production builds contain no such injection.
class Derived {
public:
    enum class State : uint8_t { Unavailable = 0, Building = 1, Active = 2, Cleanup = 3 };
    struct View {
        int64_t FieldId = 0, Generation = 0;
        State Status = State::Unavailable;
        int64_t Members = 0, Booleans = 0, Numbers = 0, Strings = 0, OversizedStrings = 0;
        bool Admitted = false;
        bool Queryable(Kind Type) const {
            return Admitted && Status == State::Active && (Type == Kind::Boolean || Type == Kind::Number ||
                (Type == Kind::String && OversizedStrings == 0));
        }
    };
    struct Statistics {
        int64_t Bytes = 0, Namespaces = 0, Fields = 0;
        int64_t Active = 0, Building = 0, Cleanup = 0, Members = 0, Prefixes = 0, Entries = 0;
    };
    Derived(sqlite3* Db, Deadline& End);
    ~Derived();
    enum class VerificationResult { Progress, Complete, DerivedInvalid };
    // Caller owns a write-capable transaction and bounded progress/deadline.
    // A result of DerivedInvalid forbids all derived admission but does not
    // itself change authoritative primary data. SQLite/primary faults throw.
    VerificationResult VerifyStep();
    bool VerificationComplete() const;
    bool VerificationFailed() const;
    // Call before every foreground primary mutation that skips derived
    // maintenance. No retained ACTIVE generation is admitted until reproven.
    void InvalidateVerification();
    static const std::vector<std::string>& Schema();
    void Create();
    // Force starts a private replacement BUILDING generation alongside ACTIVE;
    // it joins an existing build and never exceeds two retained generations.
    bool Prepare(const Identity& StoreId, std::string Field, bool Force = false);
    bool HasWork();
    void Maintain();
    void Mutate(const Identity& Id, const Value* Old, const Value* New);
    View Inspect(const Identity& StoreId, const std::string& Field);
    // Explicit diagnostic operation: bounded output/memory, table aggregates
    // subject to caller progress/deadline. Never call on each foreground write.
    Statistics Diagnostics();
private:
    sqlite3* Database;
    Deadline& End;
    std::unique_ptr<DerivedVerification> Verification;
};
}
