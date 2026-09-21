#pragma once
#include <cstdint>
#include <cstring>
#include <string>
#include <vector>
#include <algorithm>

namespace CarbonLuau::Semantics {
inline bool ModuleName(const char* Name, size_t Capacity)
{
    size_t Length = 0, Segment = 0;
    while (Length < Capacity && Name[Length]) {
        char C = Name[Length++];
        if (C == '/') { if (!Segment) return false; Segment = 0; }
        else if ((C >= 'a' && C <= 'z') || (C >= '0' && C <= '9') || C == '_' || C == '-') ++Segment;
        else return false;
    }
    return Length > 0 && Length < Capacity && Segment > 0;
}
inline bool PackageName(const char* Name)
{
    size_t Length = 0, Segment = 0, Segments = 1;
    while (Length < 66 && Name[Length]) {
        char C = Name[Length++];
        if (C == '.') {
            if (!Segment || Segments == 2) return false;
            Segment = 0; ++Segments;
        } else if ((C >= 'a' && C <= 'z') || (C >= '0' && C <= '9') || C == '_' || C == '-') {
            if (!Segment && (C == '_' || C == '-')) return false;
            if (++Segment > 32) return false;
        } else return false;
    }
    if (!Length || Length > 65 || !Segment || Name[Length] || Name[Length - 1] == '_' || Name[Length - 1] == '-') return false;
    return std::strcmp(Name, "carbonluau") != 0 && std::strncmp(Name, "carbonluau.", 11) != 0;
}
inline bool PackageVersion(const char* Version)
{
    size_t Length = 0;
    while (Length < 33 && Version[Length]) ++Length;
    if (!Length || Length > 32 || Version[Length]) return false;
    const char* Cursor = Version;
    for (int Part = 0; Part < 3; ++Part) {
        const char* Start = Cursor; uint64_t Value = 0;
        while (*Cursor && *Cursor != '.') {
            if (*Cursor < '0' || *Cursor > '9' || Cursor - Start >= 10) return false;
            Value = Value * 10 + uint64_t(*Cursor++ - '0');
            if (Value > UINT32_MAX) return false;
        }
        if (Cursor == Start || (Cursor - Start > 1 && *Start == '0')) return false;
        if (Part < 2) { if (*Cursor != '.') return false; ++Cursor; }
        else if (*Cursor) return false;
    }
    return true;
}

enum class ImportError { None, InvalidName, InvalidPackage, Undeclared, Unavailable, MissingMain, PrivateModule };
inline ImportError SelectModule(const char* Name, size_t Length, bool Declared, bool Available,
    const std::string& Main, const std::vector<std::string>& PublicModules, std::string& Logical)
{
    if (Length >= 196 || std::strlen(Name) != Length) return ImportError::InvalidName;
    if (Length && Name[0] == '@') {
        const char* Slash = std::strchr(Name + 1, '/');
        std::string Package(Name + 1, Slash ? size_t(Slash - Name - 1) : Length - 1);
        if (!PackageName(Package.c_str())) return ImportError::InvalidPackage;
        if (!Declared) return ImportError::Undeclared;
        if (!Available) return ImportError::Unavailable;
        if (!Slash) {
            if (Main.empty()) return ImportError::MissingMain;
            Logical = Main;
        } else {
            Logical.assign(Slash + 1);
            if (!ModuleName(Logical.c_str(), 128)) return ImportError::InvalidPackage;
            if (std::find(PublicModules.begin(), PublicModules.end(), Logical) == PublicModules.end())
                return ImportError::PrivateModule;
        }
    } else {
        if (Length >= 128 || !ModuleName(Name, Length + 1)) return ImportError::InvalidName;
        Logical.assign(Name, Length);
    }
    return ImportError::None;
}
}
