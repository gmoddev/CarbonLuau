"""Read-only ELF symbol inventory; absence is not a native complexity theorem."""
import argparse
import hashlib
import pathlib
import struct

Parser = argparse.ArgumentParser()
Parser.add_argument("binary")
Args = Parser.parse_args()
Path = pathlib.Path(Args.binary)
Data = Path.read_bytes()
if Data[:6] != b"\x7fELF\x02\x01":
    raise RuntimeError("Expected ELF64 little endian")
Offset = struct.unpack_from("<Q", Data, 40)[0]
EntrySize, Count, NamesIndex = struct.unpack_from("<HHH", Data, 58)
if EntrySize != 64 or not Count or NamesIndex >= Count:
    raise RuntimeError("Unsupported section table")
Sections = [struct.unpack_from("<IIQQQQIIQQ", Data, Offset + Index * EntrySize)
            for Index in range(Count)]
NamesHeader = Sections[NamesIndex]
Names = Data[NamesHeader[4]:NamesHeader[4] + NamesHeader[5]]

def StringAt(Buffer, Position):
    End = Buffer.find(b"\0", Position)
    if End < 0:
        raise RuntimeError("Unterminated string")
    return Buffer[Position:End].decode("utf-8", errors="replace")

print("[CarbonLuau:WorldNativeResearch] SHA256=" + hashlib.sha256(Data).hexdigest())
Matched = 0
for Section in Sections:
    Name = StringAt(Names, Section[0])
    if Name.startswith(".debug") or Section[1] in (2, 11):
        print("[CarbonLuau:WorldNativeResearch] SECTION " + Name + " bytes=" + str(Section[5]))
    if Section[1] not in (2, 11):
        continue
    if Section[9] != 24 or Section[6] >= Count:
        raise RuntimeError("Unsupported symbol table")
    StringsHeader = Sections[Section[6]]
    Strings = Data[StringsHeader[4]:StringsHeader[4] + StringsHeader[5]]
    for Position in range(Section[4], Section[4] + Section[5], 24):
        Symbol = struct.unpack_from("<IBBHQQ", Data, Position)
        SymbolName = StringAt(Strings, Symbol[0])
        if "ObjectDispatcher" in SymbolName or "TransformDispatch" in SymbolName:
            Matched += 1
            print("[CarbonLuau:WorldNativeResearch] SYMBOL " + SymbolName)
print("[CarbonLuau:WorldNativeResearch] DispatcherSymbols=" + str(Matched))
