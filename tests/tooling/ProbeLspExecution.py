import ctypes
from pathlib import Path
import subprocess
import tempfile
import sys

if sys.platform == "win32":
    ctypes.windll.kernel32.SetErrorMode(0x8003)
Lsp = Path(sys.argv[1]).resolve()
with tempfile.TemporaryDirectory(prefix="carbonluau-qualification-") as Folder:
    Root = Path(Folder)
    Source = Root / "main.luau"
    Source.write_text("--!strict\nprint(1)\n", encoding="utf-8")
    (Root / ".config.luau").write_text('error("CARBONLUAU_CONFIG_EXECUTED")\n', encoding="utf-8")
    Result = subprocess.run([str(Lsp), "analyze", "--flag:LuauSolverV2=true", "--platform=standard", str(Source)], cwd=Root, capture_output=True, text=True, creationflags=0x08000000 if sys.platform == "win32" else 0, timeout=10)
    print("CONFIG", Result.returncode, Result.stdout, Result.stderr)
    assert "CARBONLUAU_CONFIG_EXECUTED" in Result.stdout + Result.stderr
    (Root / ".config.luau").unlink()
    Source.write_text('--!strict\ntype function Executed()\n    error("CARBONLUAU_TYPE_FUNCTION_EXECUTED")\nend\ntype Probe = Executed<>\nlocal Value: Probe = 1\nprint(Value)\n', encoding="utf-8")
    Result = subprocess.run([str(Lsp), "analyze", "--flag:LuauSolverV2=true", "--platform=standard", str(Source)], cwd=Root, capture_output=True, text=True, creationflags=0x08000000 if sys.platform == "win32" else 0, timeout=10)
    print("TYPE_FUNCTION", Result.returncode, Result.stdout, Result.stderr)
    assert "CARBONLUAU_TYPE_FUNCTION_EXECUTED" in Result.stdout + Result.stderr
