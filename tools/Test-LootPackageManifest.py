"""Exercise actual D8 packaging JSON integers under available PowerShell runtimes."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import shutil
import subprocess
import tempfile
import io
import zipfile
import sys

sys.dont_write_bytecode = True

Root = Path(__file__).resolve().parent.parent
Manifest = json.loads((Root / "api/loot-items.pinned.json").read_text(encoding="utf-8"))
Spec = importlib.util.spec_from_file_location("PackageManifestSchema", Root / "tools/Generate-LootItems.py")
Schema = importlib.util.module_from_spec(Spec)
Spec.loader.exec_module(Schema)
Shells = [ShellPath for Name in ("powershell.exe", "pwsh") if (ShellPath := shutil.which(Name))]
if not Shells:
    raise SystemExit("[CarbonLuau:LootPackageTest] no PowerShell runtime available")

with tempfile.TemporaryDirectory(prefix="CarbonLuau-LootPackage-") as Directory:
    Fixture = Path(Directory)
    Input = Fixture / "manifest.json"
    Packets = []
    for Index, Shell in enumerate(Shells):
        Output = Fixture / str(Index)
        Command = [Shell, "-NoProfile", "-NonInteractive", "-File", str(Root / "tools/package.ps1"),
                   "-OutputDirectory", str(Output), "-IncludeD8Fixtures", "-IncludeB2Fixtures", "-D8Manifest", str(Input)]
        Input.write_text(json.dumps(Manifest), encoding="utf-8")
        Result = subprocess.run(Command, capture_output=True, text=True, timeout=20)
        assert Result.returncode == 0, Result.stderr
        Packet = Output / "CarbonLuau.cszip"
        Packets.append(Packet.read_bytes())
        Result = subprocess.run(Command, capture_output=True, text=True, timeout=20)
        assert Result.returncode == 0 and Packet.read_bytes() == Packets[-1], "same-runtime package regeneration drift"
        for Key, Invalid in (("Id", True), ("Id", 1.5), ("Id", 2147483648), ("Id", -2147483649),
                             ("Id", 0), ("Stack", False), ("Stack", 1.5), ("Stack", 0), ("Stack", 2147483648)):
            Value = copy.deepcopy(Manifest)
            Value["Definitions"][0][Key] = Invalid
            Input.write_text(json.dumps(Value), encoding="utf-8")
            Packet.unlink(missing_ok=True)
            Result = subprocess.run(Command, capture_output=True, text=True, timeout=20)
            assert Result.returncode != 0 and not Packet.exists(), (Shell, Key, Invalid, Result.stdout)
        Value = copy.deepcopy(Manifest)
        Value["SchemaVersion"] = True
        Input.write_text(json.dumps(Value), encoding="utf-8")
        Result = subprocess.run(Command, capture_output=True, text=True, timeout=20)
        assert Result.returncode != 0 and not Packet.exists(), "boolean schema accepted"
        Malformed = []
        for Label, Mutate in (
            ("root extra field", lambda Value: Value.update(Unknown=True)),
            ("definition extra field", lambda Value: Value["Definitions"][0].update(Unknown=True)),
            ("host set extra field", lambda Value: Value["HostPins"].update(Unknown={})),
            ("host extra field", lambda Value: Value["HostPins"]["Windows"].update(Unknown=True)),
            ("boolean host build", lambda Value: Value["HostPins"]["Windows"].update(RustBuild=True)),
            ("float host build", lambda Value: Value["HostPins"]["Windows"].update(RustBuild=1.5)),
            ("host build mismatch", lambda Value: Value["HostPins"]["Windows"].update(RustBuild=1)),
            ("uppercase host hash", lambda Value: Value["HostPins"]["Windows"].update(AssemblySha256="A" * 64)),
            ("invalid carbon pin", lambda Value: Value["HostPins"]["Windows"].update(Carbon="2.0.0.0/abcdefg")),
            ("invalid mvid", lambda Value: Value["HostPins"]["Windows"].update(Mvid=True)),
            ("nonarray definitions", lambda Value: Value.update(Definitions=Value["Definitions"][0])),
            ("newline name", lambda Value: Value["Definitions"][0].update(Name="wood\n")),
            ("array family", lambda Value: Value["Definitions"][0].update(Family=["Equivalent"])),
            ("duplicate ID", lambda Value: Value["Definitions"][1].update(Id=Value["Definitions"][0]["Id"])),
            ("reversed names", lambda Value: Value["Definitions"].reverse()),
        ):
            Value = copy.deepcopy(Manifest)
            Mutate(Value)
            Malformed.append((Label, json.dumps(Value)))
        CanonicalText = json.dumps(Manifest)
        Needle = '"Name": "' + Manifest["Definitions"][0]["Name"] + '"'
        Malformed += [
            ("duplicate decoded key", CanonicalText.replace(Needle, Needle + ', "Na\\u006de": "ignored"', 1)),
            ("JSON comment", "/* non-JSON */" + CanonicalText),
            ("trailing comma", CanonicalText[:-1] + ",}"),
            ("UTF8 BOM", "\ufeff" + CanonicalText),
        ]
        for Label, Text in Malformed:
            Input.write_text(Text, encoding="utf-8")
            try:
                Schema.ReadManifest(Input)
            except ValueError:
                pass
            else:
                raise AssertionError("negative fixture disagrees with canonical schema: " + Label)
            Result = subprocess.run(Command, capture_output=True, text=True, timeout=20)
            assert Result.returncode != 0 and not Packet.exists(), (Shell, Label, Result.stdout)
        Input.write_text(CanonicalText.replace('"Name"', '"Na\\u006de"'), encoding="utf-8")
        assert Schema.ReadManifest(Input) == Manifest
        Result = subprocess.run(Command, capture_output=True, text=True, timeout=20)
        assert Result.returncode == 0 and Packet.read_bytes() == Packets[-1], "canonical escaped keys changed package snapshot"
    # Framework/Core Deflate implementations can differ; compare exact decompressed sources across runtimes.
    def Sources(Packet):
        with zipfile.ZipFile(io.BytesIO(Packet)) as Archive:
            return {Entry.filename: (Archive.read(Entry), Entry.date_time) for Entry in Archive.infolist()}
    assert all(Sources(Packet) == Sources(Packets[0]) for Packet in Packets), "PowerShell JSON representations changed source bytes"
    print("[CarbonLuau:LootPackageTest] PASS actual canonical JSON packaging and boolean/float/Int32-range rejection; runtimes=" +
          str(len(Shells)) + " same_runtime_byte_determinism=true cross_runtime_exact_sources=true SHA256=" +
          ",".join(hashlib.sha256(Packet).hexdigest() for Packet in Packets))
