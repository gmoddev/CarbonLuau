cmake_minimum_required(VERSION 3.20)
project(CarbonLuauSplitProof LANGUAGES C CXX)

# Research-only. This directory is not included by production CMake targets.
set(SQLITE_AMALGAMATION_DIR "" CACHE PATH "Qualified SQLite 3.53.4 amalgamation")
file(SHA256 "${SQLITE_AMALGAMATION_DIR}/sqlite3.c" SourceHash)
file(SHA256 "${SQLITE_AMALGAMATION_DIR}/sqlite3.h" HeaderHash)
if(NOT SourceHash STREQUAL "b1dd5d74ec7f29055a6684fa06fb3c2f6821c87dd38f9a458dfd2e8a1db28189" OR
   NOT HeaderHash STREQUAL "919e7f2e8ed1d8f56ac17b412b8971c76aa5d1a879752cc6058f75e7d5910e1d")
  message(FATAL_ERROR "Unqualified SQLite pin")
endif()

add_library(SplitProofSqlite STATIC "${SQLITE_AMALGAMATION_DIR}/sqlite3.c")
target_include_directories(SplitProofSqlite PUBLIC "${SQLITE_AMALGAMATION_DIR}")
target_compile_definitions(SplitProofSqlite PRIVATE SQLITE_THREADSAFE=0 SQLITE_OMIT_LOAD_EXTENSION SQLITE_OMIT_WAL)
add_executable(SplitAtomicityProbe SplitAtomicityProbe.cpp)
target_compile_features(SplitAtomicityProbe PRIVATE cxx_std_17)
target_link_libraries(SplitAtomicityProbe PRIVATE SplitProofSqlite)
if(UNIX)
  target_link_libraries(SplitAtomicityProbe PRIVATE m)
endif()
