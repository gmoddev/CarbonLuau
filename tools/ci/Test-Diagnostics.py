"""Fresh-process negative evidence, boundedness, isolation and secret exclusion."""
import importlib.util
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
import re

RUNNER = Path(__file__).with_name("Run.py")
SPEC = importlib.util.spec_from_file_location("CiRunner", RUNNER)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class DiagnosticsTests(unittest.TestCase):
    def test_assertion_helper_locations(self):
        Repository = RUNNER.parents[2]
        Seen = {"managed": 0, "native": 0}
        for Source in (Repository / "tests").rglob("*"):
            if Source.suffix not in (".cs", ".cpp", ".hpp") or "live" in Source.parts or "shared" in Source.parts:
                continue
            Text = Source.read_text(encoding="utf-8-sig")
            for Signature in re.finditer(r"\bvoid Check\(bool[^{}]+\{", Text):
                if Source.suffix == ".cs":
                    Seen["managed"] += 1
                    for Attribute in ("CallerArgumentExpression", "CallerFilePath", "CallerLineNumber"):
                        self.assertIn(Attribute, Signature[0], str(Source))
                else:
                    Seen["native"] += 1
                    self.assertIn("__builtin_FILE", Signature[0], str(Source))
                    self.assertIn("__builtin_LINE", Signature[0], str(Source))
        self.assertGreater(Seen["managed"], 0)
        self.assertGreater(Seen["native"], 0)

    def Invoke(self, Root, Command):
        return subprocess.run([sys.executable, str(RUNNER), "--command", *Command],
                              env=dict(os.environ, CARBONLUAU_CI_ROOT=str(Root)), stdout=subprocess.PIPE, stderr=subprocess.PIPE)

    def test_classification_exit_and_secret_exclusion(self):
        with tempfile.TemporaryDirectory(prefix="CarbonLuauCiDiagnostics-") as Directory:
            Root = Path(Directory)
            for Category in MODULE.CATEGORIES:
                Event = dict(schema=1, category=Category, source="Program.cs", line=37, actual=5, expected=0, status=5,
                             message="private-secret-sentinel", command="private-secret-sentinel")
                Code = "import sys; print('[CarbonLuau:Diagnostic] '+" + repr(json.dumps(Event)) + "); print('private-secret-sentinel'); sys.exit(19)"
                Result = self.Invoke(Root, [sys.executable, "-c", Code])
                self.assertEqual(Result.returncode, 19, Result.stderr)
            Summaries = [json.loads(P.read_text()) for P in Root.glob("*/summary.json")]
            self.assertEqual(len(Summaries), len(MODULE.CATEGORIES))
            for Summary in Summaries:
                self.assertNotIn("private-secret-sentinel", json.dumps(Summary))
                self.assertEqual(Summary["events"][0]["actual"], 5)
                self.assertEqual(Summary["events"][0]["expected"], 0)
                self.assertEqual(Summary["events"][0]["source"], "Program.cs")

    def test_noisy_child_and_missing_tool(self):
        with tempfile.TemporaryDirectory(prefix="CarbonLuauCiDiagnostics-") as Directory:
            Root = Path(Directory)
            Code = "import sys; sys.stdout.write('secret'*400000); print(); print('[CarbonLuau:Diagnostic] {\"category\":\"assertion\",\"line\":9}'); sys.exit(23)"
            Result = self.Invoke(Root, [sys.executable, "-c", Code])
            self.assertEqual(Result.returncode, 23)
            self.assertLessEqual(len(Result.stdout), 1024 * 1024)
            Summary = json.loads(next(Root.glob("*/summary.json")).read_text())
            self.assertTrue(Summary["consoleTruncated"])
            self.assertEqual(Summary["firstFailure"]["line"], 9)
            self.assertLess(len(json.dumps(Summary)), 65536)
            Missing = self.Invoke(Root, [str(Root / "nonexistent-tool")])
            self.assertEqual(Missing.returncode, 127)
            self.assertTrue(any(json.loads(P.read_text())["classification"] == "infrastructure" for P in Root.glob("*/summary.json")))

    def test_concurrent_isolation_and_collection(self):
        with tempfile.TemporaryDirectory(prefix="CarbonLuauCiDiagnostics-") as Directory:
            Root = Path(Directory)
            Environment = dict(os.environ, CARBONLUAU_CI_ROOT=str(Root))
            Children = [subprocess.Popen([sys.executable, str(RUNNER), "--command", sys.executable, "-c", "raise SystemExit(7)"], env=Environment) for _ in range(4)]
            self.assertEqual([Child.wait() for Child in Children], [7] * 4)
            self.assertEqual(len(list(Root.glob("*/summary.json"))), 4)
            (Root / "raw-secret.log").write_text("never-upload-this-secret")
            # A future child must not smuggle arbitrary fields through a summary.
            SummaryFile = next(Root.glob("*/summary.json"))
            Summary = json.loads(SummaryFile.read_text())
            Summary["rawError"] = "never-upload-this-secret"
            SummaryFile.write_text(json.dumps(Summary))
            subprocess.run([sys.executable, str(RUNNER), "--collect"], env=Environment, check=True)
            self.assertEqual(len(list((Root / "upload").glob("*.json"))), 5)
            self.assertFalse(any("never-upload-this-secret" in P.read_text() for P in (Root / "upload").iterdir()))

    def test_first_failure_survives_cleanup(self):
        with tempfile.TemporaryDirectory(prefix="CarbonLuauCiDiagnostics-") as Directory:
            Code = "print('[CarbonLuau:Diagnostic] {\"category\":\"timeout\",\"status\":5}'); print('[CarbonLuau:Diagnostic] {\"category\":\"cleanup\",\"status\":7}'); raise SystemExit(19)"
            self.assertEqual(self.Invoke(Path(Directory), [sys.executable, "-c", Code]).returncode, 19)
            Summary = json.loads(next(Path(Directory).glob("*/summary.json")).read_text())
            self.assertEqual(Summary["classification"], "timeout")
            self.assertEqual(Summary["firstFailure"]["status"], 5)
            self.assertEqual(Summary["tail"][-1]["category"], "cleanup")

    def test_curated_infrastructure_and_source_frames(self):
        with tempfile.TemporaryDirectory(prefix="CarbonLuauCiDiagnostics-") as Directory:
            Code = "print('No space left on device [private-secret-sentinel]'); print('File \"private-secret-path/Check.py\", line 47'); raise SystemExit(3)"
            self.assertEqual(self.Invoke(Path(Directory), [sys.executable, "-c", Code]).returncode, 3)
            Summary = json.loads(next(Path(Directory).glob("*/summary.json")).read_text())
            self.assertEqual(Summary["classification"], "infrastructure")
            self.assertEqual(Summary["firstFailure"]["status"], 28)
            self.assertTrue(any(Event.get("source") == "Check.py" and Event.get("line") == 47 for Event in Summary["events"]))
            self.assertNotIn("private-secret", json.dumps(Summary))


if __name__ == "__main__":
    unittest.main()
