"""Supervise expected-negative managed fixtures; every child must really fail."""
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile

Runner = Path(__file__).with_name("Run.py")
Executable = str(Path(sys.argv[1]).resolve())
Prefix = [] if sys.platform == "win32" else ["mono"]
subprocess.run([*Prefix, Executable], check=True)
with tempfile.TemporaryDirectory(prefix="CarbonLuauManagedDiagnostics-") as Directory:
    Root = Path(Directory)
    for Mode in ("assertion", "timeout", "allocation", "infrastructure", "cleanup", "primary-cleanup", "trace", "sink-failure"):
        Before = set(Root.glob("*/summary.json"))
        Child = subprocess.run([sys.executable, str(Runner), "--command", *Prefix, Executable, Mode],
                               env=dict(os.environ, CARBONLUAU_CI_ROOT=str(Root)), stdout=subprocess.PIPE, stderr=subprocess.PIPE)
        assert Child.returncode == 19, (Mode, Child.returncode, Child.stderr.decode(errors="replace"))
        Summary = json.loads(next(iter(set(Root.glob("*/summary.json")) - Before)).read_text())
        Expected = "timeout" if Mode in ("primary-cleanup", "sink-failure") else "assertion" if Mode == "trace" else Mode
        assert Summary["classification"] == Expected, (Mode, Summary)
        assert "secret" not in json.dumps(Summary), Mode
        if Mode == "primary-cleanup":
            assert any(Event["category"] == "cleanup" for Event in Summary["events"]), Summary
        if Mode == "assertion":
            assert Summary["firstFailure"]["line"] > 0
            assert Summary["firstFailure"]["evidenceDigest"]
        if Mode == "trace":
            Trace = [Event for Event in Summary["events"] if Event["category"] == "observation"]
            assert len(Trace) == 24 and Trace[0]["actual"] == 8 and Trace[-1]["actual"] == 31, Trace
        # Keep sanitized records in the outer CI console/evidence too.
        for Event in Summary["events"]:
            print("[CarbonLuau:Diagnostic] " + json.dumps(Event))
print("[CarbonLuau:Diagnostics] PASS eight genuine failing children, categories, caller expression, bounded recent trace, broken sink, primary/cleanup and secret exclusion")
