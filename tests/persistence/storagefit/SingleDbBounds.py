"""Offline exact-integer proof ledger; NOT a SQLite/runtime qualification.

No database opens, subprocesses or writes. Exit 0 means arithmetic/self-checks
passed, not that the single-DB envelope was proved. Missing proofs remain null.
Run with --sqlite-source to authenticate the inspected amalgamation.
"""
import argparse
import hashlib
import json
from pathlib import Path


MiB = 1024 * 1024
Page = 4096
Chunk = 768
SourceHash = "b1dd5d74ec7f29055a6684fa06fb3c2f6821c87dd38f9a458dfd2e8a1db28189"


def Ceil(Numerator, Denominator):
    return (Numerator + Denominator - 1) // Denominator


def Varint(Value):
    assert 0 <= Value <= (1 << 64) - 1
    return min(9, max(1, Ceil(Value.bit_length(), 7)))


def Blob(Length):
    return (12 + 2 * Length, Length)


# Eight-byte INTEGER is a conservative bound for each non-alias integer column,
# including counters whose actual ranges need fewer bytes. Rowid alias is NULL.
Integer = (6, 8)
Null = (0, 0)


def Record(Columns):
    SerialBytes = sum(Varint(Type) for Type, _ in Columns)
    Header = SerialBytes + 1
    while Header != SerialBytes + Varint(Header):
        Header = SerialBytes + Varint(Header)
    return Header + sum(Size for _, Size in Columns)


def Local(Payload, TableLeaf=False):
    Minimum = (Page - 12) * 32 // 255 - 23
    Maximum = Page - 35 if TableLeaf else (Page - 12) * 64 // 255 - 23
    if Payload <= Maximum:
        return Payload
    Surplus = Minimum + (Payload - Minimum) % (Page - 4)
    return Surplus if Surplus <= Maximum else Minimum


def Cell(Columns, Table=False):
    Payload = Record(Columns)
    Resident = Local(Payload, Table)
    Overflow = Ceil(Payload - Resident, Page - 4)
    # Includes two-byte page cell pointer. Table leaf uses worst nine-byte
    # rowid; index interior uses a four-byte child pointer. Leaf index is less.
    Bytes = 2 + Varint(Payload) + (9 if Table else 4) + Resident
    if Overflow:
        Bytes += 4
    return {"PayloadUpper": Payload, "CellUpperIncludingPointer": Bytes,
            "OverflowPagesUpper": Overflow}


def BitvecBytes(Pages):
    # Exact source geometry on x64: 512-byte Bitvec, 496-byte union,
    # 3968 bitmap bits and 62 child pointers. Bound fully populated branches;
    # this excludes malloc headers, rehash scratch and other SQLite objects.
    if Pages <= 3968:
        return 512
    Divisor = max(Ceil(Pages, 62), 3968)
    return 512 + Ceil(Pages, Divisor) * BitvecBytes(Divisor)


def Journal(Pages, Inherited=0):
    # CONDITIONAL: original-page count, one <=64-KiB header, no page-move
    # journaling exception. This does not prove the supplied dirty-page count.
    return max(Inherited, 65536 + (Page + 8) * Pages)


def Tables():
    return {
        "Stores": (True, [Null, Blob(66), Blob(64)]),
        "StoresUnique": (False, [Blob(66), Blob(64), Integer]),
        "RecordIdentity": (True, [Null, Integer, Blob(128), Integer]),
        "RecordIdentityUnique": (False, [Integer, Blob(128), Integer]),
        "Chunks": (False, [Integer, Integer, Blob(Chunk)]),
        "Quotas": (False, [Blob(66), Integer, Integer, Integer]),
        "Totals": (True, [Null, Integer, Integer, Integer]),
        "DerivedTotals": (True, [Null] + [Integer] * 7),
        "DerivedNamespaces": (False, [Blob(66), Integer, Integer]),
        "DerivedFields": (True, [Null, Blob(66), Blob(64), Blob(64), Integer]),
        "DerivedFieldNames": (False, [Blob(66), Blob(64), Blob(64), Integer]),
        "DerivedGenerations": (True, [Null, Integer, Integer, Blob(128)] + [Integer] * 6),
        # Index explicitly contains Id; appending a second rowid here is a
        # safe overestimate even where SQLite eliminates the duplicate.
        "DerivedGenerationFields": (False, [Integer, Integer, Integer]),
        "DerivedGenerationWork": (False, [Integer, Integer]),
        "DerivedPrefixes": (True, [Null, Integer, Blob(512), Integer, Integer]),
        "DerivedPrefixOrder": (False, [Integer, Blob(512), Integer]),
        "DerivedEntries": (True, [Null, Integer, Blob(513), Blob(128), Integer]),
        "DerivedEntryOrder": (False, [Integer, Blob(513), Blob(128), Integer]),
        "DerivedMembers": (False, [Integer, Blob(128), Integer, Integer, Integer]),
        # WITHOUT ROWID secondary includes missing primary-key columns.
        "DerivedMemberEntries": (False, [Integer, Integer, Blob(128)]),
    }


def Check():
    assert [Varint(N) for N in (0, 127, 128, 16383, 16384, (1 << 63) - 1)] == [1, 1, 2, 2, 3, 9]
    assert Record([Null, Integer, Blob(768)]) == 781
    assert Local(1002) == 1002 and Local(1003) == 489
    assert Local(4061, True) == 4061 and Local(4062, True) == 489
    assert Journal(131072) == 537985024
    assert Journal(1, 537985024) == 537985024
    assert BitvecBytes(131072) == 17920
    for _, Columns in Tables().values():
        assert Record(Columns) <= 1002
    # Exhaust every legal envelope length: ceil(E/C) <= (E+C-1)/C.
    for Envelope in range(46, 65537):
        assert Chunk * Ceil(Envelope, Chunk) <= Envelope + Chunk - 1


def Main():
    Parser = argparse.ArgumentParser(description=__doc__)
    Parser.add_argument("--sqlite-source", type=Path)
    Parser.add_argument("--primary-bytes", type=int, default=256 * MiB)
    Parser.add_argument("--record-cap", type=int, default=100000)
    Parser.add_argument("--derived-bytes", type=int, default=64 * MiB)
    Args = Parser.parse_args()
    if min(Args.primary_bytes, Args.record_cap) < 0 or Args.derived_bytes < 65:
        Parser.error("nonnegative primary limits and at least 65 derived bytes required")
    Verified = False
    if Args.sqlite_source:
        Actual = hashlib.sha256(Args.sqlite_source.read_bytes()).hexdigest()
        if Actual != SourceHash:
            Parser.error("unqualified SQLite source hash")
        Verified = True
    Check()
    Records = min(Args.record_cap, Args.primary_bytes // 48)
    # Sum ceil(E_i/C) <= floor((sum E_i+(C-1)*N)/C),
    # sum E_i <= Q-2*N. This relaxed bound need not be attainable.
    Chunks = min(86 * Records, (Args.primary_bytes + (Chunk - 3) * Records) // Chunk)
    Derived = Args.derived_bytes - 65
    # Independent cardinality ceilings: never sum as simultaneously attainable.
    Counts = {"Namespaces": Derived // 22, "Fields": Derived // 32,
              "Generations": Derived // 70, "Prefixes": Derived // 38,
              "Entries": Derived // 34, "Members": Derived // 31}
    Allowances = 128 * MiB + 8 * MiB + 2 * 65536
    # Necessary envelope limit ONLY, with whole-DB journal and zero reserve.
    # N*(4096+4104)+65536+allowances <= 1280 MiB.
    AlgebraicPages = (1280 * MiB - Allowances - 65536) // (Page + Page + 8)
    Result = {
        "Verdict": "SINGLE-DB BOUND NOT PROVEN — PROCEED TO SPLIT ARCHITECTURE",
        "SelfChecks": "PASS (arithmetic only)", "SQLiteSourceHashVerified": Verified,
        "CanonicalInputs": Args.primary_bytes == 256 * MiB and Args.record_cap == 100000 and Args.derived_bytes == 64 * MiB,
        "PageBytes": Page, "UsableBytes": Page, "IndexMaxLocal": Local(1002),
        "IndexMinLocal": Local(1003), "TableMaxLocal": 4061,
        "OverflowPayloadBytes": Page - 4,
        "PrimaryRecordUpper": Records, "PrimaryChunksUpper": Chunks,
        "MaximumChunksPerValue": 86, "IndependentDerivedRowUpper": Counts,
        "CellBounds": {Name: Cell(Columns, Table) for Name, (Table, Columns) in Tables().items()},
        "ExistingWholeDbJournalBytes": Journal(131072),
        "ExistingSecondaryAllowancesBytes": Allowances,
        "ExistingEnvelopeBytes": 512 * MiB + Journal(131072) + Allowances,
        "AlgebraOnlyWholeDbJournalNoReserveDbPages": AlgebraicPages,
        "AlgebraOnlyWholeDbJournalNoReserveDbBytes": AlgebraicPages * Page,
        "BitvecBytesAtExistingCapExcludingAllocator": BitvecBytes(131072),
        "ProvenAllHistoryDbPages": None, "ProvenTransactionDirtyPages": None,
        "ProvenSavepointProcessBytes": None, "ProvenMigrationPeakPages": None,
        "SelectedNewDbCeiling": None,
        "Blockers": ["all-history B-tree occupancy and directory reclamation",
                     "transaction-specific page touches including startup quarantine",
                     "inherited PERSIST high-water and subjournal/memory proof",
                     "insert-before-delete near-ceiling migration",
                     "compact operation aggregate VDBE certificate",
                     "healthy-worker exact-pin empirical validation"],
    }
    print(json.dumps(Result, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    Main()
