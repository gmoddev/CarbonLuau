# Private build-time dependency only. No system SQLite or runtime download.
set(CARBONLUAU_SQLITE_SOURCE_DIR "" CACHE PATH "Verified SQLite 3.53.4 amalgamation cache")
if(NOT CARBONLUAU_SQLITE_SOURCE_DIR)
    include(FetchContent)
    FetchContent_Declare(CarbonLuauSQLite
        URL https://www.sqlite.org/2026/sqlite-amalgamation-3530400.zip
        URL_HASH SHA256=1e71ddf93849c6a6ecf58b827c0692073d2dd7ee40196158068f7b29f422e87d
        DOWNLOAD_EXTRACT_TIMESTAMP TRUE)
    FetchContent_MakeAvailable(CarbonLuauSQLite)
    set(CARBONLUAU_SQLITE_SOURCE_DIR "${carbonluausqlite_SOURCE_DIR}")
endif()
file(SHA256 "${CARBONLUAU_SQLITE_SOURCE_DIR}/sqlite3.c" SQLiteSourceHash)
file(SHA256 "${CARBONLUAU_SQLITE_SOURCE_DIR}/sqlite3.h" SQLiteHeaderHash)
if(NOT SQLiteSourceHash STREQUAL "b1dd5d74ec7f29055a6684fa06fb3c2f6821c87dd38f9a458dfd2e8a1db28189"
   OR NOT SQLiteHeaderHash STREQUAL "919e7f2e8ed1d8f56ac17b412b8971c76aa5d1a879752cc6058f75e7d5910e1d")
    message(FATAL_ERROR "SQLite source/header differ from the D21 qualified pin")
endif()
add_library(CarbonLuau.Sqlite STATIC "${CARBONLUAU_SQLITE_SOURCE_DIR}/sqlite3.c")
target_include_directories(CarbonLuau.Sqlite PUBLIC "${CARBONLUAU_SQLITE_SOURCE_DIR}")
# Exclude WAL before any schema read/hot-journal recovery can open auxiliary files.
target_compile_definitions(CarbonLuau.Sqlite PRIVATE SQLITE_THREADSAFE=0 SQLITE_OMIT_LOAD_EXTENSION SQLITE_OMIT_WAL)
if(UNIX)
    target_link_libraries(CarbonLuau.Sqlite PUBLIC m)
endif()
