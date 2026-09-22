"""Offline development pack assembly. Does not publish or download executable code."""
import argparse
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import zipfile

Root = Path(__file__).resolve().parents[1]
Parser = argparse.ArgumentParser()
Parser.add_argument("--platform", required=True, choices=["win32-x64", "linux-x64", "darwin-x64", "darwin-arm64"])
Parser.add_argument("--publish", type=Path, required=True)
Parser.add_argument("--analysis", type=Path, required=True)
Parser.add_argument("--launcher", type=Path, required=True)
Parser.add_argument("--preview-native", type=Path)
Parser.add_argument("--preview-launcher", type=Path)
Parser.add_argument("--lsp-archive", type=Path, required=True)
Parser.add_argument("--extension", type=Path, required=True)
Parser.add_argument("--source-revision", help="Explicit base commit for worker source archives without Git metadata")
Args = Parser.parse_args()
Pin = json.loads((Root / "tooling/language-server.json").read_text())
Release = json.loads((Root / "release.json").read_text())
if hashlib.sha256(Args.lsp_archive.read_bytes()).hexdigest() != Pin["UpstreamAssets"][Args.platform]["Sha256"]:
    raise SystemExit("Upstream language-server archive checksum mismatch")
Target = Args.extension.resolve() / "tooling" / Args.platform
Target.mkdir(parents=True, exist_ok=True)
HostName = "carbonluau-tooling.exe" if Args.platform == "win32-x64" else "carbonluau-tooling"
ServerName = "luau-lsp.exe" if Args.platform == "win32-x64" else "luau-lsp"
if not (Args.publish / HostName).is_file():
    raise SystemExit("Missing self-contained tooling executable")
Files = {}
for File in Args.publish.iterdir():
    if File.is_file() and File.suffix != ".pdb":
        shutil.copyfile(File, Target / File.name)
        Files[File.name] = None
for File in [Args.analysis, Args.launcher, Root / "generated/carbonluau.d.luau", Root / "generated/carbonluau-docs.json"]:
    shutil.copyfile(File, Target / File.name)
    Files[File.name] = None
for File, Name in [(Root / "LICENSE", "CarbonLuau-LICENSE.txt"),
                   (Root / "native/third_party/luau/LICENSE.txt", "Luau-LICENSE.txt"),
                   (Root / "native/third_party/luau/lua_LICENSE.txt", "Lua-LICENSE.txt"),
                   (Root / "tooling/THIRD_PARTY_NOTICES.txt", "Tooling-THIRD-PARTY-NOTICES.txt")]:
    shutil.copyfile(File, Target / Name)
    Files[Name] = None
if bool(Args.preview_native) != bool(Args.preview_launcher):
    raise SystemExit("Supply both preview native bridge and launcher")
if Args.preview_native:
    if Args.platform not in Pin["PreviewContainmentProfiles"]:
        raise SystemExit("Preview is not supported by this platform profile")
    for File in [Args.preview_native, Args.preview_launcher]:
        shutil.copyfile(File, Target / File.name)
        Files[File.name] = None
    (Target / Args.preview_launcher.name).chmod(0o755)
with zipfile.ZipFile(Args.lsp_archive) as Archive:
    Matches = [Name for Name in Archive.namelist() if Path(Name).name == ServerName]
    if len(Matches) != 1:
        raise SystemExit("Unexpected upstream archive layout")
    (Target / ServerName).write_bytes(Archive.read(Matches[0]))
    Files[ServerName] = None
for Name in [HostName, ServerName, Args.launcher.name]:
    (Target / Name).chmod(0o755)
for Name in Files:
    Files[Name] = hashlib.sha256((Target / Name).read_bytes()).hexdigest()
SemanticRevision = Args.source_revision or subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=Root, text=True).strip()
if len(SemanticRevision) != 40 or any(Value not in "0123456789abcdef" for Value in SemanticRevision):
    raise SystemExit("Invalid semantic base revision")
Manifest = {**Pin, "Platform": Args.platform, "ApiVersion": Release["apiVersion"], "PackageSchema": Release["packageSchema"],
            "SemanticRevision": SemanticRevision, "ToolingBuildId": "sha256:" + hashlib.sha256(json.dumps(Files, sort_keys=True).encode()).hexdigest(),
            "Provenance": {"Kind": "LocalDevelopmentBuild", "ShippingAttestation": False},
            "RuntimeLuauRevision": Release["luauRevision"], "Protocol": {"Name": "CarbonLuau.Tooling", "Major": 1, "Minor": 0},
            "ApiMetadataSchema": 1, "PreviewPlanSchema": 1, "Host": HostName, "LanguageServer": ServerName,
            "AnalysisLauncher": Args.launcher.name, "AnalysisContainmentProfile": Pin["AnalysisContainmentProfiles"][Args.platform],
            "AnalysisConfigurationAndTransformSourceSha256": hashlib.sha256((Root / "src/CarbonLuau.Tooling/AnalysisSnapshot.cs").read_bytes()).hexdigest(),
            "AnalysisSupervisorSourceSha256": hashlib.sha256((Root / "src/CarbonLuau.Tooling/AnalysisProcess.cs").read_bytes()).hexdigest(),
            "Definitions": "carbonluau.d.luau", "Documentation": "carbonluau-docs.json", "Files": dict(sorted(Files.items()))}
Manifest["PreviewQualified"] = bool(Args.preview_native and Pin["PreviewQualified"])
if Args.preview_native:
    Manifest.update({"PreviewNative": Args.preview_native.name, "PreviewLauncher": Args.preview_launcher.name,
                     "PreviewContainmentProfile": Pin["PreviewContainmentProfiles"][Args.platform]})
(Target / "pack.json").write_text(json.dumps(Manifest, indent=2) + "\n", encoding="utf-8", newline="\n")
print("[CarbonLuau:ToolingPack] Provisioned local development pack:", Target)
