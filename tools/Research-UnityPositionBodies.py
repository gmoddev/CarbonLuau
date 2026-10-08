"""Read-only pinned PE/ELF position-body inspection. Never loads native code.

Supply an address established by the isolated runtime icall mapper. PE addresses
are image-relative RVAs; ELF addresses are image virtual addresses. Disassembly
does not itself certify reachability, dependency safety or a caller work bound.
"""
import argparse
import hashlib
import struct
from pathlib import Path
from capstone import Cs, CS_ARCH_X86, CS_MODE_64

Parser = argparse.ArgumentParser()
Parser.add_argument("binary", type=Path)
Parser.add_argument("--address", required=True, type=lambda Value: int(Value, 0))
Parser.add_argument("--bytes", type=int, default=320)
Args = Parser.parse_args()
if not 1 <= Args.bytes <= 32768:
    raise ValueError("Read-only disassembly byte limit exceeded")
Data = Args.binary.read_bytes()
Ranges = []
if Data[:6] == b"\x7fELF\x02\x01":
    Offset = struct.unpack_from("<Q", Data, 40)[0]
    EntrySize, Count, _ = struct.unpack_from("<HHH", Data, 58)
    if EntrySize != 64 or Count == 0:
        raise ValueError("Unsupported ELF sections")
    for Index in range(Count):
        Section = struct.unpack_from("<IIQQQQIIQQ", Data, Offset + Index * EntrySize)
        if Section[1] != 8 and Section[2] & 4:
            Ranges.append((Section[3], Section[4], Section[5]))
    Format = "ELF64 virtual address"
elif Data[:2] == b"MZ":
    Offset = struct.unpack_from("<I", Data, 60)[0]
    if Data[Offset:Offset + 4] != b"PE\0\0":
        raise ValueError("Invalid PE signature")
    Machine, Count = struct.unpack_from("<HH", Data, Offset + 4)
    HeaderSize = struct.unpack_from("<H", Data, Offset + 20)[0]
    if Machine != 0x8664 or struct.unpack_from("<H", Data, Offset + 24)[0] != 0x20b:
        raise ValueError("Expected PE64 x64")
    for Index in range(Count):
        Section = Offset + 24 + HeaderSize + Index * 40
        _, Address, Size, FileOffset = struct.unpack_from("<IIII", Data, Section + 8)
        Characteristics = struct.unpack_from("<I", Data, Section + 36)[0]
        if Characteristics & 0x20000000:
            Ranges.append((Address, FileOffset, Size))
    Format = "PE64 image RVA"
else:
    raise ValueError("Expected pinned ELF64/PE64 image")
print("[CarbonLuau:PositionNativeResearch] SHA256=" + hashlib.sha256(Data).hexdigest() + " Format=" + Format)
for Address, FileOffset, Size in Ranges:
    if Address <= Args.address < Address + Size:
        Delta = Args.address - Address
        Body = Data[FileOffset + Delta:FileOffset + min(Size, Delta + Args.bytes)]
        for Instruction in Cs(CS_ARCH_X86, CS_MODE_64).disasm(Body, Args.address):
            print(f"0x{Instruction.address:x}: {Instruction.mnemonic} {Instruction.op_str}")
        break
else:
    raise ValueError("Address outside executable image sections")
