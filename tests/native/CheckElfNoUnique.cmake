# Inspect the final shared ELF, including symbols contributed by static libraries.
# This regression guard supplements, never replaces, actual load/unmap tests.
if(NOT DEFINED ReadElf OR NOT DEFINED Bridge OR NOT EXISTS "${Bridge}")
    message(FATAL_ERROR "[Native:ElfUnload] Missing readelf or shared-library input")
endif()
execute_process(
    COMMAND "${CMAKE_COMMAND}" -E env LC_ALL=C "${ReadElf}" --dyn-syms --wide "${Bridge}"
    RESULT_VARIABLE Status OUTPUT_VARIABLE Symbols ERROR_VARIABLE Errors TIMEOUT 15)
if(NOT "${Status}" STREQUAL "0")
    message(FATAL_ERROR "[Native:ElfUnload] readelf failed (${Status}): ${Errors}")
endif()
if(NOT Symbols MATCHES "Symbol table '\\.dynsym'")
    message(FATAL_ERROR "[Native:ElfUnload] No dynamic symbol table inspected: ${Bridge}")
endif()
if(Symbols MATCHES "[^\n]*[ \t](UNIQUE|GNU_UNIQUE)[ \t][^\n]*")
    message(FATAL_ERROR "[Native:ElfUnload] GNU-unique symbol can prevent unmapping: ${CMAKE_MATCH_0}")
endif()
message(STATUS "[Native:ElfUnload] No GNU-unique dynamic symbols: ${Bridge}")
