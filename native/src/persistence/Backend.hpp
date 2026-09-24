#pragma once
#include "Format.hpp"
#include <filesystem>
struct sqlite3;
namespace CarbonLuau::Persistence {
enum class Operation : uint32_t { Get = 1, Set = 2, Remove = 3 };
struct Result { Error Code = Error::None; bool Found = false; Bytes Envelope; bool NamespacePresent = false; };

// Called exclusively by the storage worker, never by a game/VM thread.
class Backend {
public:
    Backend(const std::filesystem::path& Directory, Deadline StartupEnd);
    ~Backend();
    Backend(const Backend&) = delete;
    Backend& operator=(const Backend&) = delete;
    Result Execute(Operation Op, const Identity& Id, const Bytes& Envelope, Deadline End);
    bool Available() const { return Healthy; }
private:
    sqlite3* Database = nullptr;
    std::filesystem::path Directory;
    Deadline End;
    bool Healthy = false;
    void Open();
    void CheckFiles();
    void CheckSchema(bool New);
    void CheckRecords();
};
}
