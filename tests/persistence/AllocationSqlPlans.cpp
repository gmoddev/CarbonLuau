// Research-only EXPLAIN of the fixed production statement literals, never executes
// a mutation. Pass an existing research production-schema DB and Backend.cpp.
#include "sqlite3.h"
#include <fstream>
#include <iostream>
#include <iterator>
#include <regex>
#include <stdexcept>
#include <string>
int main(int Count, char** Args)
{
    sqlite3* Db = nullptr;
    try {
        if (Count != 3) throw std::runtime_error("expected database and Backend.cpp");
        if (sqlite3_open_v2(Args[1], &Db, SQLITE_OPEN_READONLY, nullptr) != SQLITE_OK) throw std::runtime_error("read-only open");
        if (sqlite3_exec(Db, "PRAGMA temp_store=MEMORY; PRAGMA cache_spill=OFF; PRAGMA mmap_size=0", nullptr, nullptr, nullptr) != SQLITE_OK)
            throw std::runtime_error("plan settings");
        sqlite3_limit(Db, SQLITE_LIMIT_LENGTH, 70 * 1024); sqlite3_limit(Db, SQLITE_LIMIT_SQL_LENGTH, 4096);
        sqlite3_limit(Db, SQLITE_LIMIT_COLUMN, 16); sqlite3_limit(Db, SQLITE_LIMIT_ATTACHED, 0);
        sqlite3_limit(Db, SQLITE_LIMIT_VARIABLE_NUMBER, 16); sqlite3_limit(Db, SQLITE_LIMIT_EXPR_DEPTH, 32);
        sqlite3_limit(Db, SQLITE_LIMIT_TRIGGER_DEPTH, 0);
        sqlite3_db_config(Db, SQLITE_DBCONFIG_DEFENSIVE, 1, nullptr);
        sqlite3_db_config(Db, SQLITE_DBCONFIG_TRUSTED_SCHEMA, 0, nullptr);
        std::ifstream Input(Args[2]); if (!Input) throw std::runtime_error("source open");
        const std::string Source{std::istreambuf_iterator<char>(Input), std::istreambuf_iterator<char>()};
        const std::regex Literal("\"((?:SELECT|INSERT|UPDATE|DELETE|CREATE|PRAGMA integrity_check)[^\"\\\\]*)\"");
        unsigned Statements = 0;
        for (auto It = std::sregex_iterator(Source.begin(), Source.end(), Literal); It != std::sregex_iterator(); ++It) {
            const auto Sql = (*It)[1].str();
            // CREATE TABLE plans need an empty schema; their data-bearing effects
            // are startup-only and are reviewed in source, not run on this DB.
            if (Sql.rfind("CREATE", 0) == 0) { std::cout << "[CarbonLuau:Plans] startup DDL: " << Sql << '\n'; continue; }
            sqlite3_stmt* Statement = nullptr;
            if (sqlite3_prepare_v2(Db, ("EXPLAIN QUERY PLAN " + Sql).c_str(), -1, &Statement, nullptr) != SQLITE_OK)
                throw std::runtime_error(sqlite3_errmsg(Db));
            std::cout << "[CarbonLuau:Plans] " << Sql << '\n'; ++Statements;
            for (int Index = 1; Index <= sqlite3_bind_parameter_count(Statement); ++Index) {
                if (Sql.rfind("UPDATE Totals", 0) == 0 || (Sql.rfind("INSERT INTO Quotas", 0) == 0 && Index > 1) || Index == 5)
                    sqlite3_bind_int64(Statement, Index, 1);
                else sqlite3_bind_blob(Statement, Index, "x", 1, SQLITE_STATIC);
            }
            int Rc;
            while ((Rc = sqlite3_step(Statement)) == SQLITE_ROW) {
                const auto* Text = sqlite3_column_text(Statement, 3);
                std::cout << "  " << (Text ? reinterpret_cast<const char*>(Text) : "") << '\n';
            }
            sqlite3_finalize(Statement); if (Rc != SQLITE_DONE) throw std::runtime_error("plan step");
            if (sqlite3_prepare_v2(Db, ("EXPLAIN " + Sql).c_str(), -1, &Statement, nullptr) != SQLITE_OK)
                throw std::runtime_error("opcode prepare");
            unsigned Opcodes = 0;
            while ((Rc = sqlite3_step(Statement)) == SQLITE_ROW) {
                ++Opcodes;
                const auto* Raw = sqlite3_column_text(Statement, 1);
                const std::string Op = Raw ? reinterpret_cast<const char*>(Raw) : "";
                if (Op.find("Sort") != std::string::npos || Op.find("Open") != std::string::npos || Op == "Transaction" || Op == "IntegrityCk")
                    std::cout << "  opcode=" << Op << " p1=" << sqlite3_column_int(Statement, 2)
                        << " p2=" << sqlite3_column_int(Statement, 3) << " p3=" << sqlite3_column_int(Statement, 4) << '\n';
            }
            sqlite3_finalize(Statement); if (Rc != SQLITE_DONE) throw std::runtime_error("opcode step");
            std::cout << "  total-opcodes=" << Opcodes << '\n';
        }
        if (Statements < 12) throw std::runtime_error("fixed statement extraction incomplete");
        sqlite3_close(Db); Db = nullptr;
        std::cout << "[CarbonLuau:Plans] PASS prepared=" << Statements << "; EQP uses typed placeholders; opcode EXPLAIN unbound; no mutation executed\n";
        return 0;
    } catch (const std::exception& Error) {
        std::cerr << "[CarbonLuau:Plans] " << Error.what() << '\n'; if (Db) sqlite3_close(Db); return 1;
    }
}
