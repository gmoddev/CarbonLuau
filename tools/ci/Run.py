"""Bounded, primitive-only CI evidence. Child exit codes are never retried or hidden.

Use as a GitHub custom shell, or --command executable args. Raw output continues
to the CI console (1 MiB cap); artifacts never contain arbitrary console text,
environment dumps, scripts, exception messages, credentials, or command lines.
"""
import argparse
from collections import deque
import hashlib
import json
import os
from pathlib import Path
import platform
import re
import shutil
import subprocess
import sys
import tempfile
import time
import uuid

MARKER = b"[CarbonLuau:Diagnostic] "
CATEGORIES = {"assertion", "operation", "observation", "exception", "timeout", "allocation", "infrastructure", "cleanup", "sanitizer"}
NUMBERS = {"schema", "line", "actual", "expected", "status", "flags", "errno", "cutoffNs", "sequence", "queued", "modules"}
DIGESTS = {"sourceDigest", "evidenceDigest", "typeDigest"}


def Validate(Value):
    if not isinstance(Value, dict) or Value.get("category") not in CATEGORIES:
        return None
    Result = {"category": Value["category"]}
    for Key, Field in Value.items():
        if Key in NUMBERS and type(Field) is int and -(2**64) <= Field <= 2**64:
            Result[Key] = Field
        elif Key in DIGESTS and isinstance(Field, str) and re.fullmatch(r"[a-f0-9]{0,64}", Field):
            Result[Key] = Field
        elif Key == "source" and isinstance(Field, str) and re.fullmatch(r"[A-Za-z0-9_.-]{0,128}\.(cs|cpp|hpp|c|h|py|ps1)", Field):
            Result[Key] = Field
        elif Key == "exceptionType" and isinstance(Field, str) and re.fullmatch(r"[A-Za-z0-9_.+`]{1,192}", Field):
            Result[Key] = Field
    return Result


def Root():
    return Path(os.environ.get("CARBONLUAU_CI_ROOT", str(Path(os.environ.get("RUNNER_TEMP", tempfile.gettempdir())) / "carbonluau-ci-diagnostics")))


def Bash():
    if sys.platform != "win32":
        return "bash"
    # Match Actions' Git Bash choice. PATH's bash.exe may instead launch WSL.
    Git = shutil.which("git")
    if Git:
        Directory = Path(Git).resolve().parent
        for Candidate in (Directory.parent / "bin" / "bash.exe", Directory / "bash.exe"):
            if Candidate.is_file():
                return str(Candidate)
    raise FileNotFoundError(2, "Git Bash executable unavailable")


def Run(Arguments):
    Directory = Root() / uuid.uuid4().hex
    Directory.mkdir(parents=True)
    Events, Tail = [], deque(maxlen=32)
    FirstFailure = None
    Digest = hashlib.sha256()
    Bytes = ConsoleBytes = 0
    Started = time.monotonic()
    Environment = dict(os.environ, CARBONLUAU_DIAGNOSTICS_DIR=str(Directory))
    Script = None
    Exit = 127
    try:
        if Arguments.command:
            Command = Arguments.command
        elif Arguments.shell == "pwsh" or (Arguments.shell == "auto" and sys.platform == "win32"):
            # Match GitHub's built-in pwsh error/exit behavior. This private script
            # is removed, never uploaded, and does not alter the original source.
            Script = Directory / "step.ps1"
            Script.write_text("$ErrorActionPreference = 'stop'\n" + Path(Arguments.script).read_text(encoding="utf-8-sig") +
                              "\nif (Test-Path -LiteralPath variable:\\LASTEXITCODE) { exit $LASTEXITCODE }\n", encoding="utf-8")
            Command = ["pwsh", "-NoLogo", "-NoProfile", "-NonInteractive", "-File", str(Script)]
        else:
            Command = [Bash(), "--noprofile", "--norc", "-e", "-o", "pipefail", Path(Arguments.script).resolve().as_posix()]
        with subprocess.Popen(Command, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, env=Environment) as Child:
            Pending = b""
            while True:
                Chunk = Child.stdout.read1(8192)
                if not Chunk:
                    break
                Bytes += len(Chunk)
                Digest.update(Chunk)
                if ConsoleBytes < 1024 * 1024:
                    Visible = Chunk[:1024 * 1024 - ConsoleBytes]
                    sys.stdout.buffer.write(Visible)
                    sys.stdout.buffer.flush()
                    ConsoleBytes += len(Visible)
                # Lines exceeding 8 KiB cannot be structured records. Continue
                # draining even after limits, so a noisy child cannot deadlock.
                for Line in (Pending + Chunk).splitlines(keepends=True):
                    if not Line.endswith((b"\n", b"\r")):
                        Pending = Line[-8192:]
                        continue
                    Pending = b""
                    Position = Line.find(MARKER)
                    if len(Line) > 8192:
                        continue
                    Event = None
                    if Position >= 0:
                        try:
                            Event = Validate(json.loads(Line[Position + len(MARKER):]))
                        except (ValueError, UnicodeError):
                            pass
                    # Curated infrastructure signatures and source frames only;
                    # never copy arbitrary error messages into an artifact.
                    if b"No space left on device" in Line or b"not enough space on the disk" in Line:
                        Event = {"category": "infrastructure", "status": 28}
                    elif b"std::bad_alloc" in Line:
                        Event = {"category": "allocation"}
                    elif b"ERROR: AddressSanitizer:" in Line or b"SUMMARY: AddressSanitizer:" in Line or b"ERROR: LeakSanitizer:" in Line:
                        Event = {"category": "sanitizer"}
                    elif re.search(rb'\.(?:cpp|hpp|c|h):\d+:\d+: runtime error:', Line):
                        Event = {"category": "sanitizer"}
                    elif Line.lstrip().startswith(b"AssertionError"):
                        Event = {"category": "assertion"}
                    elif re.search(rb"\berror (?:CS|MSB|C)\d{3,5}\b", Line):
                        Code = re.search(rb"\berror (?:CS|MSB|C)(\d{3,5})\b", Line)
                        Event = {"category": "infrastructure", "status": int(Code[1])}
                    Frame = re.search(rb'([A-Za-z0-9_.-]+\.(?:cs|cpp|hpp|c|h|py|ps1))(?::line |:|\(|\", line )(\d+)', Line)
                    if Frame:
                        if Event is None:
                            Event = {"category": "infrastructure" if b"error:" in Line else "observation"}
                        Event.update(source=Frame[1].decode("ascii"), line=int(Frame[2]))
                        Event = Validate(Event)
                    if Event:
                        if len(Events) < 32:
                            Events.append(Event)
                        Tail.append(Event)
                        if FirstFailure is None and Event["category"] not in ("operation", "observation"):
                            FirstFailure = Event
            Exit = Child.wait()
    except OSError as Error:
        FirstFailure = {"category": "infrastructure", "errno": Error.errno}
    finally:
        Cleanup = None
        if Script is not None:
            try:
                Script.unlink(missing_ok=True)
            except OSError as Error:
                Cleanup = {"category": "cleanup", "errno": Error.errno}
                if Exit == 0:
                    Exit = 126
        Workspace = os.environ.get("GITHUB_WORKSPACE")
        Relative = None
        if Workspace:
            try:
                Candidate = Path.cwd().resolve().relative_to(Path(Workspace).resolve()).as_posix()
                if re.fullmatch(r"[A-Za-z0-9_./-]{1,256}", Candidate):
                    Relative = Candidate
            except ValueError:
                pass
        Summary = {
            "schema": 1, "step": Arguments.step, "exit": Exit, "elapsedSeconds": round(time.monotonic() - Started, 3),
            "outputBytes": Bytes, "consoleTruncated": Bytes > ConsoleBytes, "outputSha256": Digest.hexdigest(),
            "firstFailure": FirstFailure, "cleanupFailure": Cleanup,
            "classification": "success" if Exit == 0 else FirstFailure["category"] if FirstFailure else "unclassified-process-failure",
            "events": Events, "tail": list(Tail),
            "environment": {"platform": sys.platform, "architecture": platform.machine(), "python": platform.python_version(),
                            "cpus": os.cpu_count(), "workspaceRelativeCwd": Relative,
                            "source": os.environ.get("GITHUB_SHA", "") if re.fullmatch("[a-f0-9]{40}", os.environ.get("GITHUB_SHA", "")) else ""},
        }
        # Even a broken evidence sink must not turn a failed child into success.
        try:
            (Directory / "summary.json").write_text(json.dumps(Summary, indent=2) + "\n", encoding="utf-8")
        except OSError:
            print("[CarbonLuau:Diagnostic] summary-unavailable", file=sys.stderr)
            if Exit == 0:
                Exit = 126
    return Exit if Exit >= 0 else 128 - Exit


def Collect():
    # Only summaries produced by this wrapper are eligible, never arbitrary files
    # emitted by tests. Bound both file count and bytes, independently of child IO.
    Destination = Root() / "upload"
    Destination.mkdir(parents=True, exist_ok=True)
    for Index, Source in enumerate(sorted(Root().glob("*/summary.json"))):
        if Index >= 256:
            break
        if Source.stat().st_size <= 65536:
            try:
                Value = json.loads(Source.read_bytes())
                Safe = {}
                for Key in ("schema", "step", "exit", "elapsedSeconds", "outputBytes", "consoleTruncated"):
                    Field = Value.get(Key)
                    if type(Field) in (int, float, bool) and abs(Field) <= 2**64:
                        Safe[Key] = Field
                for Key in ("outputSha256",):
                    if re.fullmatch(r"[a-f0-9]{64}", str(Value.get(Key, ""))):
                        Safe[Key] = Value[Key]
                if Value.get("classification") in CATEGORIES | {"success", "unclassified-process-failure"}:
                    Safe["classification"] = Value["classification"]
                for Key in ("firstFailure", "cleanupFailure"):
                    Safe[Key] = Validate(Value.get(Key))
                for Key in ("events", "tail"):
                    Safe[Key] = [Clean for Event in Value.get(Key, [])[:32] if (Clean := Validate(Event))]
                Environment = Value.get("environment", {})
                Safe["environment"] = {}
                for Key in ("platform", "architecture", "python", "source", "workspaceRelativeCwd"):
                    Field = Environment.get(Key)
                    if isinstance(Field, str) and re.fullmatch(r"[A-Za-z0-9_./-]{1,256}", Field):
                        Safe["environment"][Key] = Field
                if type(Environment.get("cpus")) is int:
                    Safe["environment"]["cpus"] = Environment["cpus"]
                (Destination / (Source.parent.name + ".json")).write_text(json.dumps(Safe, indent=2) + "\n")
            except (ValueError, TypeError, AttributeError):
                # Corruption is recorded without copying the corrupt contents.
                (Destination / (Source.parent.name + ".json")).write_text('{"schema":1,"classification":"infrastructure"}\n')
    JobStatus = os.environ.get("CARBONLUAU_JOB_STATUS", "unknown")
    if JobStatus not in ("success", "failure", "cancelled", "unknown"):
        JobStatus = "unknown"
    (Destination / "collector.json").write_text(json.dumps({"schema": 1, "jobStatus": JobStatus, "limitFiles": 256, "limitBytesPerFile": 65536,
        "runnerSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest()}) + "\n")


if __name__ == "__main__":
    Parser = argparse.ArgumentParser(description=__doc__)
    Parser.add_argument("--collect", action="store_true")
    Parser.add_argument("--shell", choices=("auto", "bash", "pwsh"))
    Parser.add_argument("--step", type=int, default=0)
    Parser.add_argument("script", nargs="?")
    Parser.add_argument("--command", nargs=argparse.REMAINDER)
    Arguments = Parser.parse_args()
    if Arguments.collect:
        Collect()
    else:
        if not Arguments.command and (not Arguments.shell or not Arguments.script):
            Parser.error("supply --command or --shell and script")
        sys.exit(Run(Arguments))
