"""Require a genuine native assertion exit and its precise source location."""
import json
from pathlib import Path
import subprocess
import sys

Executable = str(Path(sys.argv[1]).resolve())
subprocess.run([Executable], check=True)
Child = subprocess.run([Executable, "--assertion"], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
assert Child.returncode == 19, Child.returncode
Marker = b"[CarbonLuau:Diagnostic] "
Record = json.loads(next(Line[len(Marker):] for Line in Child.stderr.splitlines() if Line.startswith(Marker)))
assert Record["category"] == "assertion" and Record["source"] == "DiagnosticTests.cpp", Record
assert Record["line"] > 0 and Record["actual"] == 0 and Record["expected"] == 1, Record
print(Marker.decode() + json.dumps(Record))
for Mode, Category, Status in (("--primary-cleanup", "timeout", 5), ("--allocation-cleanup", "allocation", 4)):
    Child = subprocess.run([Executable, Mode], stdout=subprocess.PIPE, stderr=subprocess.PIPE)
    assert Child.returncode == 19, (Mode, Child.returncode)
    Records = [json.loads(Line[len(Marker):]) for Line in Child.stderr.splitlines() if Line.startswith(Marker)]
    assert len(Records) == 2 and Records[0]["category"] == Category and Records[0]["status"] == Status, Records
    assert Records[0]["statusSpace"] == "vm" and Records[0]["expected"] == 0 and Records[0]["flags"] == 1 and Records[1]["category"] == "cleanup", Records
    for Record in Records:
        print(Marker.decode() + json.dumps(Record))
print("[CarbonLuau:Diagnostics] PASS native assertion and primary timeout/allocation plus cleanup; exact exit/status, caller source/line")
