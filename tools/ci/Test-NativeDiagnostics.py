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
print("[CarbonLuau:Diagnostics] PASS native negative assertion exit 19, actual/expected, caller source and line")
