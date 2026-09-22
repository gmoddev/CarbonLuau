"""Black-box tests against the actual static host and packaged native parser."""
import base64
import ctypes
import hashlib
import io
import json
import os
from pathlib import Path
import subprocess
import sys
import unittest
import zipfile

Root = Path(__file__).resolve().parents[2]
Host = Path(os.environ.get("CARBONLUAU_TOOLING_HOST", Root / "src/CarbonLuau.Tooling/bin/Debug/net10.0/carbonluau-tooling.dll"))
Command = ["dotnet", str(Host), "--stdio"] if Host.suffix == ".dll" else [str(Host), "--stdio"]
Platform = {"win32": "win32", "linux": "linux", "darwin": "darwin"}[sys.platform] + "-" + ("arm64" if os.uname().machine == "arm64" else "x64" if sys.platform != "win32" else "x64") if sys.platform != "win32" else "win32-x64"
Protocol = {"Name": "CarbonLuau.Tooling", "Major": 1, "Minor": 0}
Api = "0.4.0-experimental"
Pack = json.loads((Root / "tooling/language-server.json").read_text())["PackVersion"]
if sys.platform == "win32":
    ctypes.windll.kernel32.SetErrorMode(0x8003)


def Json(Value):
    return json.dumps(Value, ensure_ascii=False, separators=(",", ":"))


def Frame(Value):
    Body = Json(Value).encode()
    return b"Content-Length: " + str(len(Body)).encode() + b"\r\n\r\n" + Body


def Request(Id, Method, Params=None):
    Result = {"Protocol": Protocol, "Id": Id, "Method": Method, "Params": Params or {}}
    if Method in ("validateProject", "resolveProjectGraph"):
        Body = json.dumps(Params, ensure_ascii=False, separators=(",", ":"), sort_keys=True)
        Result["ProjectRevision"] = "sha256:" + hashlib.sha256((Body + "\n" + Api + "\n" + Pack).encode()).hexdigest()
    return Result


def Initialize(**Changes):
    Params = {"ExtensionVersion": "0.0.1", "ApiVersion": Api, "PackageSchema": 1, "Platform": Platform,
              "PackVersion": Pack, "Capabilities": ["StaticAnalysis", "Metadata"]}
    Params.update(Changes)
    return Request(1, "initialize", Params)


def Run(Requests=None, Raw=None):
    Process = subprocess.run(Command, input=Raw if Raw is not None else b"".join(Frame(Item) for Item in Requests),
                             capture_output=True, timeout=30, creationflags=0x08000000 if sys.platform == "win32" else 0)
    Results = []
    Output = Process.stdout
    while Output:
        Header, Output = Output.split(b"\r\n\r\n", 1)
        Length = int(Header.removeprefix(b"Content-Length: "))
        Results.append(json.loads(Output[:Length]))
        Output = Output[Length:]
    return Results, Process.returncode, Process.stderr


def Files(**Values):
    return [{"Path": Path, "Text": Text} for Path, Text in Values.items()]


def Manifest(Id, **Fields):
    return Json({"schema": 1, "id": Id, "version": "1.0.0", **Fields})


def Inspect(Folders):
    Responses, Code, Error = Run([Initialize(), Request(2, "validateProject", {"Folders": Folders}), Request(3, "shutdown")])
    if Code or "Error" in Responses[1]:
        raise AssertionError((Responses, Code, Error))
    return Responses[1]["Result"]


class HostTests(unittest.TestCase):
    def test_handshake_metadata_shutdown(self):
        Values, Code, Error = Run([Initialize(), Request(2, "getMetadata"), Request(3, "shutdown"), Request(4, "getMetadata")])
        self.assertEqual((Code, Error, len(Values)), (0, b"", 3))
        self.assertEqual(Values[0]["Result"]["RuntimeLuauRevision"], "c6b830185af962c82003f86784e2fe036357c830")
        Members = {Member["Id"] for Member in Values[1]["Result"]["Catalog"]["Members"]}
        self.assertIn("Player.TakeItem", Members)
        self.assertIn("Player.Teleport", Members)
        self.assertIn("Player.GiveItem", Members)
        self.assertFalse(any("TextBox" in Member for Member in Members))

    def test_version_negotiation(self):
        for Changes, Code in [({"ApiVersion": "99"}, "UnsupportedApi"), ({"PackageSchema": 2}, "UnsupportedSchema"),
                              ({"PackVersion": "wrong"}, "IncompatiblePack"), ({"Platform": "other"}, "UnsupportedPlatform"),
                              ({"Capabilities": ["Preview"]}, "UnsupportedCapability")]:
            with self.subTest(Changes=Changes):
                Values, Exit, _ = Run([Initialize(**Changes)])
                self.assertEqual(Exit, 0)
                self.assertEqual(Values[0]["Error"]["Code"], Code)
        Value = Initialize()
        Value["Protocol"] = {**Protocol, "Major": 2}
        self.assertEqual(Run([Value])[0][0]["Error"]["Code"], "IncompatibleProtocol")
        self.assertEqual(Run([Request(1, "getMetadata")])[0][0]["Error"]["Code"], "NotInitialized")
        self.assertEqual(Run([Initialize(), Request(2, "preview")])[0][1]["Error"]["Code"], "InvalidRequest")

    def test_invalid_frames(self):
        for Raw in [b"x" * 4097, b"Content-Length: 8388609\r\n\r\n", b"Content-Length: 2\r\nContent-Length: 2\r\n\r\n{}",
                    b"Content-Length: 2\r\n\r\n\xff\xff", b"Content-Length: 14\r\n\r\n{\"Id\":1,\"Id\":2}",
                    Frame({"Id": 1.5}), Frame({"Id": 0}), Frame({"Id": 2147483648}), b"Content-Length: 2\r\n\r\n{"]:
            with self.subTest(Raw=Raw[:80]):
                Values, Code, Error = Run(Raw=Raw)
                self.assertNotEqual(Code, 0)
                self.assertEqual(Values, [])
                self.assertLess(len(Error), 1500)

    def test_stale_and_unsafe(self):
        Params = {"Folders": [{"Id": "A", "Files": Files(**{"../init.luau": ""})}]}
        self.assertEqual(Run([Initialize(), Request(2, "validateProject", Params)])[0][1]["Error"]["Code"], "InvalidRequest")
        Value = Request(2, "validateProject", {"Folders": []})
        Value["ProjectRevision"] = "sha256:" + "0" * 64
        self.assertEqual(Run([Initialize(), Value])[0][1]["Error"]["Code"], "RevisionMismatch")

    def test_packages_dependencies_and_ownership(self):
        Folders = [{"Id": "A", "Files": Files(**{
            "addon.json": Manifest("consumer", dependencies={"required": ["economy"], "optional": ["absent"]}),
            "init.luau": 'local A = require("@economy")\nlocal B = require("@economy/api/shop")\nrequire("@economy/internal/database")\nrequire("@undeclared")\nrequire("local")\nrequire("@absent")',
            "local.luau": "return 1"})}, {"Id": "B", "Files": Files(**{
            "addon.json": Manifest("economy", main="api/shop", publicModules=["api/public"]),
            "init.luau": "error('MUST NEVER EXECUTE')", "api/shop.luau": 'return require("internal/database")',
            "api/public.luau": "return {}", "internal/database.luau": "return 1"})}]
        Result = Inspect(Folders)
        Codes = [Item["Code"] for Item in Result["Diagnostics"]]
        self.assertIn("Require6", Codes)
        self.assertIn("Require3", Codes)
        self.assertIn("Require4", Codes)
        self.assertEqual(len(Result["Imports"]), 3)  # main is not automatically public under @id/path
        self.assertIn({"B"}, [{Item["TargetFolder"]} for Item in Result["Imports"] if Item["Path"] == "api/shop.luau"])
        Folders.append({"Id": "C", "Files": Folders[1]["Files"]})
        Result = Inspect(Folders)
        self.assertTrue(any(Item["Code"] == "AmbiguousDependency" for Item in Result["Diagnostics"]))
        self.assertFalse(any(Item["Folder"] == "A" and Item["TargetFolder"] != "A" for Item in Result["Imports"]))

    def test_detection_and_shadowed_require(self):
        Result = Inspect([{"Id": "A", "Files": Files(**{
            "nested/addon.json": Manifest("nested"), "nested/init.luau": "",
            "init.luau": 'require("shared")\nlocal require = function(_: string) return 1 end\nrequire("not/a/module")',
            "modules/shared.luau": "return 3"})}, {"Id": "B", "Files": Files(**{"standalone.luau": "print(1)"})}])
        self.assertEqual({Project["Kind"] for Project in Result["Projects"]}, {"Addon", "Root", "Standalone"})
        self.assertEqual(len(Result["Imports"]), 1)
        self.assertEqual(Result["Diagnostics"], [])

    def test_package_failures(self):
        for ManifestText, Extra in [(Manifest("bad", schema=2), {}), ('{"schema":1,"schema":1}', {}),
                                    (Manifest("UPPERCASE"), {}), (Manifest("bad", publicModules=["missing"]), {}),
                                    (Manifest("bad"), {"../bad.luau": ""}), (Manifest("bad"), {"large.luau": "x" * 65537})]:
            with self.subTest(Manifest=ManifestText):
                try:
                    Result = Inspect([{"Id": "A", "Files": Files(**{"addon.json": ManifestText, "init.luau": "", **Extra})}])
                    self.assertTrue(Result["Diagnostics"])
                except AssertionError as Error:
                    if "../bad.luau" not in Extra:
                        raise Error

    def test_archive(self):
        Buffer = io.BytesIO()
        with zipfile.ZipFile(Buffer, "w") as Archive:
            Archive.writestr("addon.json", Manifest("archive"))
            Archive.writestr("init.luau", "error('never')")
        Result = Inspect([{"Id": "A", "Files": [{"Path": "example.claddon", "Archive": base64.b64encode(Buffer.getvalue()).decode()}]}])
        self.assertEqual(Result["Projects"][0]["Kind"], "Archive")
        self.assertEqual(Result["Diagnostics"], [])


if __name__ == "__main__":
    unittest.main()
