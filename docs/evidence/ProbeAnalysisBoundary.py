"""Architecture evidence only. Task-owned fixtures; no user workspace evaluation."""
import ctypes
import json
import re
from pathlib import Path
import subprocess
import sys
import tempfile
import time

if sys.platform == "win32":
    ctypes.windll.kernel32.SetErrorMode(0x8003)
Lsp = Path(sys.argv[1]).resolve()
Results = []
with tempfile.TemporaryDirectory(prefix="carbonluau-analysis-boundary-") as Directory:
    Root = Path(Directory)
    Source = Root / "main.luau"
    Config = Root / ".config.luau"
    Base = Root / "base.json"
    Base.write_text('{"languageMode":"strict"}', encoding="utf-8")
    def Run(Name, Text, ConfigText=None, Flags=()):
        Source.write_text(Text, encoding="utf-8")
        if ConfigText is None:
            Config.unlink(missing_ok=True)
        else:
            Config.write_text(ConfigText, encoding="utf-8")
        Start = time.monotonic()
        try:
            Solver = [] if any(Flag.startswith("--flag:LuauSolverV2=") for Flag in Flags) else ["--flag:LuauSolverV2=true"]
            Result = subprocess.run([str(Lsp), "analyze", *Solver, "--platform=standard", *Flags, str(Source)], cwd=Root, capture_output=True, text=True, timeout=2, creationflags=0x08000000 if sys.platform == "win32" else 0)
            Output = re.sub(re.escape(str(Root)), "<fixture>", Result.stdout + Result.stderr, flags=re.IGNORECASE)
            Row = {"Name": Name, "Exit": Result.returncode, "Output": Output[:4096]}
        except subprocess.TimeoutExpired:
            Row = {"Name": Name, "KilledByHarnessAfterSeconds": 2}
        Row["ElapsedSeconds"] = round(time.monotonic() - Start, 3)
        Results.append(Row)
    Run("OrdinaryBodyNotExecuted", 'error("ORDINARY_BODY_EXECUTED")\n')
    Run("ConfigBaseDoesNotSuppress", 'local X = 1\n', 'error("CONFIG_EXECUTED_WITH_BASE")\n', ["--base-luaurc=" + str(Base)])
    Run("ConfigCapabilities", 'local X = 1\n', 'error("CAPS:" .. type(io) .. ":" .. type(require) .. ":" .. type(os.execute) .. ":" .. type(os.getenv) .. ":" .. type(loadfile) .. ":" .. type(package))\n')
    Run("ConfigInfinite", 'local X = 1\n', 'while true do end\n')
    def TypeSource(Body):
        return '--!strict\ntype function Probe()\n' + Body + '\nend\ntype Result = Probe<>\nlocal X: Result = 1\nprint(X)\n'
    Run("TypeCapabilities", TypeSource('error("CAPS:" .. type(io) .. ":" .. type(require) .. ":" .. type(os) .. ":" .. type(loadfile) .. ":" .. type(package))'))
    Run("TypeInfinite", TypeSource('while true do end\nreturn types.number'))
    Run("TypeHeapLimit", TypeSource('local Value = buffer.create(2 * 1024 * 1024)\nreturn types.number'), Flags=["--flag:DebugLuauTypeFunctionRuntimeHeapLimit=1048576"])
    Run("OldSolver", TypeSource('error("TYPE_EXECUTED_OLD_SOLVER")'), Flags=["--flag:LuauSolverV2=false"])
print(json.dumps(Results, indent=2))
