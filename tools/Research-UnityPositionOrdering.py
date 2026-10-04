"""Read-only pinned-image caller inventory for Position ordering research.

Unwind function ranges are attribution aids, not thread-affinity proof. Raw
references may occur inside operands; executable references are labelled as
candidates until independently decoded from a qualified entry. Never load,
patch, or execute the supplied Unity image.
"""
import argparse
import bisect
import hashlib
import re
import struct
from pathlib import Path
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

Parser = argparse.ArgumentParser()
Parser.add_argument("binary", type=Path)
Parser.add_argument("--target", type=lambda Value: int(Value, 0))
Parser.add_argument("--function", type=lambda Value: int(Value, 0))
Parser.add_argument("--bytes", type=int, default=1024)
Args = Parser.parse_args()
if (Args.target is None) == (Args.function is None) or not 1 <= Args.bytes <= 32768:
    raise ValueError("Choose one mode and a bounded disassembly length")
Data = Args.binary.read_bytes()
Ranges, Functions, Relocations = [], [], []
if Data[:6] == b"\x7fELF\x02\x01":
    Offset = struct.unpack_from("<Q", Data, 40)[0]
    EntrySize, Count, NameIndex = struct.unpack_from("<HHH", Data, 58)
    if EntrySize != 64 or not Count or NameIndex >= Count:
        raise ValueError("Unsupported ELF table")
    Sections = [struct.unpack_from("<IIQQQQIIQQ", Data, Offset + Index * 64)
                for Index in range(Count)]
    NamesSection = Sections[NameIndex]
    Names = Data[NamesSection[4]:NamesSection[4] + NamesSection[5]]
    for Section in Sections:
        Name = Names[Section[0]:Names.find(b"\0", Section[0])]
        if Section[1] != 8:
            Ranges.append((Section[3], Section[4], Section[5], bool(Section[2] & 4)))
        if Name == b".eh_frame_hdr":
            Base, FileOffset, Size = Section[3:6]
            if Data[FileOffset:FileOffset + 4] != b"\x01\x1b\x03\x3b":
                raise ValueError("Unsupported unwind-header encoding")
            Rows = struct.unpack_from("<I", Data, FileOffset + 8)[0]
            if 12 + Rows * 8 > Size:
                raise ValueError("Invalid unwind-header count")
            Functions = [Base + struct.unpack_from("<i", Data, FileOffset + 12 + Index * 8)[0]
                         for Index in range(Rows)]
        if Section[1] == 4 and Section[9] == 24:
            for Position in range(Section[4], Section[4] + Section[5], 24):
                Location, Info, Addend = struct.unpack_from("<QQq", Data, Position)
                Relocations.append((Location, Info & 0xffffffff, Addend))
else:
    if Data[:2] != b"MZ":
        raise ValueError("Expected ELF64/PE64")
    Offset = struct.unpack_from("<I", Data, 60)[0]
    if Data[Offset:Offset + 4] != b"PE\0\0":
        raise ValueError("Invalid PE signature")
    Machine, Count = struct.unpack_from("<HH", Data, Offset + 4)
    HeaderSize = struct.unpack_from("<H", Data, Offset + 20)[0]
    if Machine != 0x8664 or struct.unpack_from("<H", Data, Offset + 24)[0] != 0x20b:
        raise ValueError("Expected PE64 x64")
    for Index in range(Count):
        Position = Offset + 24 + HeaderSize + Index * 40
        Name = Data[Position:Position + 8].rstrip(b"\0")
        _, Address, Size, FileOffset = struct.unpack_from("<IIII", Data, Position + 8)
        Flags = struct.unpack_from("<I", Data, Position + 36)[0]
        Ranges.append((Address, FileOffset, Size, bool(Flags & 0x20000000)))
        if Name == b".pdata":
            Functions = [struct.unpack_from("<I", Data, FileOffset + Index * 12)[0]
                         for Index in range(Size // 12)
                         if struct.unpack_from("<I", Data, FileOffset + Index * 12)[0]]
Functions = sorted(set(Functions))

def Entry(Address):
    Index = bisect.bisect_right(Functions, Address) - 1
    return Functions[Index] if Index >= 0 else None

print("[CarbonLuau:PositionOrderingResearch] SHA256=" + hashlib.sha256(Data).hexdigest())
if Args.function is not None:
    Start = Entry(Args.function)
    if Start is None:
        raise ValueError("No unwind attribution")
    Index = bisect.bisect_left(Functions, Start)
    End = Functions[Index + 1] if Index + 1 < len(Functions) else Start + Args.bytes
    print(f"UNWIND_ENTRY 0x{Start:x} next=0x{End:x} queried=0x{Args.function:x}")
    for Address, FileOffset, Size, Executable in Ranges:
        if Executable and Address <= Start < Address + Size:
            Delta = Start - Address
            Body = Data[FileOffset + Delta:FileOffset + min(Size, Delta + min(End - Start, Args.bytes))]
            for Instruction in Cs(CS_ARCH_X86, CS_MODE_64).disasm(Body, Start):
                print(f"0x{Instruction.address:x}: {Instruction.mnemonic} {Instruction.op_str}")
            break
    else:
        raise ValueError("Entry outside executable sections")
else:
    for Location, Kind, Addend in Relocations:
        if Addend == Args.target:
            print(f"ELF_RELOCATION location=0x{Location:x} kind={Kind} addend=0x{Addend:x}")
    for Address, FileOffset, Size, Executable in Ranges:
        if not Executable:
            continue
        Body = Data[FileOffset:FileOffset + Size]
        for Match in re.finditer(rb"[\xe8\xe9]....", Body, re.DOTALL):
            Current = Address + Match.start()
            Target = Current + 5 + struct.unpack_from("<i", Match.group(), 1)[0]
            if Target == Args.target:
                print(f"RAW_BRANCH_CANDIDATE 0x{Current:x} entry={Entry(Current)} target=0x{Target:x}")
        for Match in re.finditer(rb"[\x48\x4c][\x8b\x8d][\x05\x0d\x15\x1d\x25\x2d\x35\x3d]....", Body, re.DOTALL):
            Current = Address + Match.start()
            Target = Current + 7 + struct.unpack_from("<i", Match.group(), 3)[0]
            if Target == Args.target:
                print(f"RAW_RIP_CANDIDATE 0x{Current:x} entry={Entry(Current)} target=0x{Target:x}")
