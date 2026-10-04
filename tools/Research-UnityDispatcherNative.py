"""Read-only exact Linux ELF icall/relocation/disassembly research, not a proof gate.

Uses the already bundled Capstone dependency; never loads or patches Unity.
Addresses are image virtual addresses, not runtime pointers.
"""
import argparse
import hashlib
import re
import struct
from pathlib import Path

Parser = argparse.ArgumentParser()
Parser.add_argument("binary", type=Path)
Parser.add_argument("--address", type=lambda Value: int(Value, 0))
Parser.add_argument("--call-target", type=lambda Value: int(Value, 0))
Parser.add_argument("--rip-target", type=lambda Value: int(Value, 0))
Parser.add_argument("--bytes", type=int, default=512)
Parser.add_argument("--icall-pattern", default=r"UnityEngine\.ObjectDispatcher::[^\x00]{1,200}\x00",
                    help="Read-only ASCII icall string inventory; default preserves dispatcher research")
Args = Parser.parse_args()
if not 1 <= Args.bytes <= 32768:
    raise ValueError("Disassembly byte limit exceeded")
Data = Args.binary.read_bytes()
if Data[:6] != b"\x7fELF\x02\x01":
    raise ValueError("Expected ELF64 little endian")
SectionOffset = struct.unpack_from("<Q", Data, 40)[0]
EntrySize, Count, NameIndex = struct.unpack_from("<HHH", Data, 58)
if EntrySize != 64 or not Count or NameIndex >= Count:
    raise ValueError("Unsupported ELF section table")
Sections = [struct.unpack_from("<IIQQQQIIQQ", Data, SectionOffset + Index * EntrySize)
            for Index in range(Count)]
NameSection = Sections[NameIndex]
Names = Data[NameSection[4]:NameSection[4] + NameSection[5]]

def SectionName(Section):
    End = Names.find(b"\0", Section[0])
    if End < 0:
        raise ValueError("Unterminated section name")
    return Names[Section[0]:End].decode("ascii", errors="replace")

def AddressAt(Offset):
    for Section in Sections:
        if Section[1] != 8 and Section[4] <= Offset < Section[4] + Section[5]:
            return Section[3] + Offset - Section[4]
    raise ValueError("Unmapped file offset")

def OffsetAt(Address):
    for Section in Sections:
        if Section[1] != 8 and Section[3] <= Address < Section[3] + Section[5]:
            return Section[4] + Address - Section[3]
    raise ValueError("Unmapped virtual address")

def Disassemble(Address, ByteCount):
    from capstone import Cs, CS_ARCH_X86, CS_MODE_64
    Offset = OffsetAt(Address)
    for Instruction in Cs(CS_ARCH_X86, CS_MODE_64).disasm(Data[Offset:Offset + ByteCount], Address):
        print(f"0x{Instruction.address:x}: {Instruction.mnemonic} {Instruction.op_str}")

print("[CarbonLuau:WorldNativeResearch] SHA256=" + hashlib.sha256(Data).hexdigest())
if sum(Value is not None for Value in (Args.address, Args.call_target, Args.rip_target)) > 1:
    raise ValueError("Select one inspection mode")
if Args.rip_target is not None:
    # Only seven-byte RIP-relative MOV/LEA candidates; not exhaustive xrefs.
    for Section in Sections:
        if not Section[2] & 4:
            continue
        Body = Data[Section[4]:Section[4] + Section[5]]
        for Match in re.finditer(rb"[\x48\x4c][\x8b\x8d][\x05\x0d\x15\x1d\x25\x2d\x35\x3d]....", Body, re.DOTALL):
            Address = Section[3] + Match.start()
            Target = Address + 7 + struct.unpack_from("<i", Match.group(), 3)[0]
            if Target == Args.rip_target:
                print(f"RAW_RIP_CANDIDATE 0x{Address:x} -> 0x{Target:x}; boundary validation required")
elif Args.call_target is not None:
    # Byte candidates only: callers must establish instruction/function boundaries.
    # An E8 byte inside another instruction is not evidence of a call path.
    for Section in Sections:
        if not Section[2] & 4:
            continue
        Body = Data[Section[4]:Section[4] + Section[5]]
        for Match in re.finditer(rb"\xe8....", Body, re.DOTALL):
            Address = Section[3] + Match.start()
            Target = Address + 5 + struct.unpack_from("<i", Match.group(), 1)[0]
            if Target == Args.call_target:
                print(f"RAW_CALL_CANDIDATE 0x{Address:x} -> 0x{Target:x}; boundary validation required")
elif Args.address is not None:
    Disassemble(Args.address, Args.bytes)
else:
    Strings = {}
    for Match in re.finditer(Args.icall_pattern.encode("ascii"), Data):
        Strings[AddressAt(Match.start())] = Match.group()[:-1].decode("ascii", errors="replace")
    for Address, Name in Strings.items():
        print(f"ICALL_STRING 0x{Address:x} {Name}")
        for Width, Format in ((8, "<Q"), (4, "<I")):
            Needle = struct.pack(Format, Address)
            Start = 0
            Matches = 0
            while Matches < 32:
                Offset = Data.find(Needle, Start)
                if Offset < 0:
                    break
                print(f"RAW_ADDRESS_REFERENCE width={Width} address=0x{AddressAt(Offset):x} bytes={Data[max(0, Offset-16):Offset+32].hex()}")
                Start = Offset + Width
                Matches += 1
    for Section in Sections:
        if Section[1] == 4 and Section[9] == 24:
            for Offset in range(Section[4], Section[4] + Section[5], 24):
                Location, Info, Addend = struct.unpack_from("<QQq", Data, Offset)
                if Addend in Strings:
                    print(f"STRING_RELOCATION location=0x{Location:x} type={Info & 0xffffffff} name={Strings[Addend]}")
        if Section[2] & 4:
            # Candidate RIP-relative LEAs, validated only by disassembly below.
            Body = Data[Section[4]:Section[4] + Section[5]]
            for Match in re.finditer(rb"[\x48\x4c]\x8d[\x05\x0d\x15\x1d\x25\x2d\x35\x3d]....", Body, re.DOTALL):
                Address = Section[3] + Match.start()
                Target = Address + 7 + struct.unpack_from("<i", Match.group(), 3)[0]
                if Target in Strings:
                    print(f"ICALL_REGISTRATION_CANDIDATE 0x{Address:x} {Strings[Target]}")
                    Disassemble(Address, 48)
    print(f"[CarbonLuau:WorldNativeResearch] IcallStrings={len(Strings)}")
