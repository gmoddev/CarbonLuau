"""Actual pinned LSP behind the production snapshot supervisor; no in-process VM."""
import hashlib
import json
import os
from pathlib import Path
import queue
import subprocess
import sys
import threading
import unittest

Host = Path(os.environ["CARBONLUAU_TOOLING_HOST"])
Protocol = {"Name": "CarbonLuau.Tooling", "Major": 1, "Minor": 0}
Pin = json.loads((Host.parent / "language-server.json").read_text())
Api = json.loads((Host.parent / "tooling-metadata.json").read_text())["Api"]["Version"]


class Session:
    def __init__(self):
        self.Process = subprocess.Popen([str(Host), "--analysis-stdio"], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                        stderr=subprocess.PIPE, creationflags=0x08000000 if sys.platform == "win32" else 0)
        self.Responses = queue.Queue()
        self.Id = 0
        self.Revision = ""

        def Read():
            try:
                while True:
                    Header = self.Process.stdout.readline()
                    if not Header:
                        raise RuntimeError("Supervisor exited: " + self.Process.stderr.read().decode())
                    Length = int(Header.decode().removeprefix("Content-Length: "))
                    assert self.Process.stdout.read(2) == b"\r\n"
                    self.Responses.put(json.loads(self.Process.stdout.read(Length)))
            except Exception as Error:
                self.Responses.put(Error)
        threading.Thread(target=Read, daemon=True).start()

    def Request(self, Method, Params):
        self.Id += 1
        Body = json.dumps({"Protocol": Protocol, "Id": self.Id, "Method": Method, "Params": Params,
                           "ProjectRevision": self.Revision}, separators=(",", ":")).encode()
        self.Process.stdin.write(b"Content-Length: " + str(len(Body)).encode() + b"\r\n\r\n" + Body)
        self.Process.stdin.flush()
        Result = self.Responses.get(timeout=40)
        if isinstance(Result, Exception):
            raise Result
        assert Result["Id"] == self.Id, Result
        return Result

    def Snapshot(self, Files, Trusted=True):
        Snapshot = {"Folders": [{"Id": "0", "Files": [{"Path": Name, "Text": Text} for Name, Text in Files.items()]}]}
        Canonical = json.dumps(Snapshot, ensure_ascii=False, separators=(",", ":"), sort_keys=True)
        self.Revision = "sha256:" + hashlib.sha256((Canonical + "\n" + Api + "\n" + Pin["PackVersion"]).encode()).hexdigest()
        return self.Request("snapshot", {"Trusted": Trusted, "Snapshot": Snapshot})

    def Language(self, Path="init.luau", Operation="textDocument/diagnostic", Position=None):
        Params = {"Trusted": True, "Folder": "0", "Path": Path, "Operation": Operation}
        if Position is not None:
            Params["Position"] = Position
        return self.Request("language", Params)

    def Close(self):
        self.Process.stdin.close()
        self.Process.wait(timeout=10)
        self.Process.stdout.close()
        self.Process.stderr.close()


class AnalysisTests(unittest.TestCase):
    def setUp(self):
        self.Session = Session()

    def tearDown(self):
        self.Session.Close()

    def test_normal_typefunction_and_api(self):
        Value = self.Session.Snapshot({"init.luau": "type function Identity(T) return T end\ntype N = Identity<number>\nlocal X: N = 1\nlocal P = game:GetService('Players')\nreturn X, P"})
        self.assertIn("Result", Value, Value)
        Value = self.Session.Language()
        self.assertIn("Result", Value, Value)
        self.assertEqual(Value["Result"]["Value"]["items"], [], Value)

    def test_config_excluded(self):
        Value = self.Session.Snapshot({"init.luau": "return 1", ".config.luau": "while true do end"})
        self.assertIn("Result", Value, Value)
        self.assertEqual(Value["Result"]["Withheld"], ["0\0.config.luau"])
        self.assertIn("Result", self.Session.Language())

    def test_persistence_recursive_value_and_callbacks(self):
        Source = """--!strict
local Store = game:GetService("DataStoreService"):GetDataStore("Types")
local Value: PersistedValue = { Version = 1, Enabled = false, Nested = {"Text", 2} }
Store:SetAsync("Key", Value, function(Saved: boolean?, ErrorCode: string?) end)
Store:GetAsync("Key", function(Found: PersistedValue?, ErrorCode: string?) end)
Store:RemoveAsync("Key", function(Removed: boolean?, ErrorCode: string?) end)
return Store
"""
        self.assertIn("Result", self.Session.Snapshot({"init.luau": Source}))
        Value = self.Session.Language()
        self.assertIn("Result", Value, Value)
        self.assertEqual(Value["Result"]["Value"]["items"], [], Value)
        Invalid = Source.replace('local Value: PersistedValue = { Version = 1, Enabled = false, Nested = {"Text", 2} }',
                                 'local Value: PersistedValue = function() end')
        self.assertIn("Result", self.Session.Snapshot({"init.luau": Invalid}))
        Value = self.Session.Language()
        self.assertIn("Result", Value, Value)
        self.assertTrue(Value["Result"]["Value"]["items"], Value)

    def test_untrusted(self):
        self.assertEqual(self.Session.Snapshot({"init.luau": "while true do end"}, False)["Error"]["Code"], "TrustRequired")

    def test_require_mapping(self):
        Value = self.Session.Snapshot({"init.luau": 'local M = require("foo")\nlocal N: number = M\nreturn N', "modules/foo.luau": 'return "bad"'})
        self.assertIn("Result", Value, Value)
        Value = self.Session.Language()
        self.assertIn("Result", Value, Value)
        self.assertTrue(Value["Result"]["Value"]["items"], Value)

    def test_unsafe_require_withheld(self):
        for Source in ['return require("../../escape")', 'return require(Name)', 'local R = require\nreturn R("x")']:
            Value = self.Session.Snapshot({"init.luau": Source})
            self.assertIn("Result", Value, Value)
            self.assertEqual(Value["Result"]["Admitted"], [], Value)


if __name__ == "__main__":
    unittest.main()
