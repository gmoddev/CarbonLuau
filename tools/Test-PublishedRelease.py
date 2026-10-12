"""Read-only release/source audit. Never loads binaries or extracts ZIP entries.

This audits the immutable v0.7.0 bundle format (source-relative documentation).
Future bundles with rewritten navigation require their own packaging checks.
Pass a directory of already downloaded assets. This is identity/content evidence,
not live-host, durability, binary reproducibility, or publisher-signing evidence.
"""
import argparse
import hashlib
import io
import json
from pathlib import Path, PurePosixPath
import re
import stat
import subprocess
import tarfile
import xml.etree.ElementTree as ET
import zipfile


def Require(Condition, Message):
    if not Condition:
        raise ValueError(Message)


def Digest(Data):
    return hashlib.sha256(Data).hexdigest()


def TextBytes(Data):
    # Git normalizes tracked text; packaged working-tree text may use CRLF.
    return Data.replace(b"\r\n", b"\n")


def ReadZip(Data):
    Require(len(Data) <= 32 * 1024 * 1024, "Archive compressed-byte audit limit")
    with zipfile.ZipFile(io.BytesIO(Data)) as Archive:
        Entries = Archive.infolist()
        Require(len(Entries) <= 512, "Archive entry audit limit")
        Require(sum(Entry.file_size for Entry in Entries) <= 32 * 1024 * 1024,
                "Archive expanded-byte audit limit")
        Result = {}
        for Entry in Entries:
            Name = Entry.orig_filename
            Parts = Name.split("/")
            Require(Name and not Name.startswith("/") and "\\" not in Name
                    and ":" not in Name and all(Part not in ("", ".", "..") for Part in Parts),
                    "Unsafe archive path")
            Require(Name not in Result, "Duplicate archive entry")
            Require(not Entry.flag_bits & 1 and not stat.S_ISLNK(Entry.external_attr >> 16),
                    "Encrypted/symlink archive entry")
            with Archive.open(Entry) as Stream:
                Payload = Stream.read(32 * 1024 * 1024 + 1)
            Require(len(Payload) == Entry.file_size, "Archive expanded-byte mismatch")
            Result[Name] = Payload
        return Result


def Git(Root, *Arguments):
    return subprocess.check_output(["git", "-C", str(Root), *Arguments])


def TaggedSources(Root, Tag):
    Require(re.fullmatch(r"v\d+\.\d+\.\d+", Tag), "Expected version tag")
    Require(Git(Root, "cat-file", "-t", Tag).strip() == b"tag", "Tag is not annotated")
    Revision = Git(Root, "rev-parse", Tag + "^{commit}").decode().strip()
    Raw = Git(Root, "archive", "--format=tar", Tag, "release.json", "src/CarbonLuau",
              "src/CarbonLuau.Core", "docs", "examples", "api", "generated", "LICENSE",
              "THIRD_PARTY_NOTICES.md", "native/third_party/luau/LICENSE.txt",
              "native/third_party/luau/lua_LICENSE.txt")
    with tarfile.open(fileobj=io.BytesIO(Raw)) as Archive:
        Sources = {Entry.name: Archive.extractfile(Entry).read()
                   for Entry in Archive if Entry.isfile()}
    return Revision, Sources


def Audit(Assets, Revision, Sources):
    Manifest = json.loads(Sources["release.json"])
    ExpectedAssets = {f"CarbonLuau-v{Manifest['releaseVersion']}-{Rid}.{Suffix}"
                      for Rid in ("win-x64", "linux-x64")
                      for Suffix in ("zip", "sha256", "provenance.json")}
    Require(set(Assets) == ExpectedAssets, "Unexpected release asset set")
    Core = ET.fromstring(Sources["src/CarbonLuau.Core/SharedSources.props"])
    Production = {Name: Data for Name, Data in Sources.items()
                  if Name.startswith("src/CarbonLuau/") and Name.endswith(".cs")}
    Prefix = "$(MSBuildThisFileDirectory)"
    for Entry in Core.iter("Compile"):
        Include = Entry.attrib["Include"]
        Require(Include.startswith(Prefix), "Core include prefix")
        Relative = Include[len(Prefix):]
        Require(".." not in PurePosixPath(Relative).parts and Relative.endswith(".cs"),
                "Core include path")
        Name = "src/CarbonLuau.Core/" + Relative
        Production[Name] = Sources[Name]
    Flat = {PurePosixPath(Name).name: Data for Name, Data in Production.items()}
    Require(len(Flat) == len(Production), "Duplicate flattened C# source")
    Report = []
    for Rid in ("win-x64", "linux-x64"):
        Stem = f"CarbonLuau-v{Manifest['releaseVersion']}-{Rid}"
        ArchiveData = Assets[Stem + ".zip"]
        Require(Assets[Stem + ".sha256"].decode().strip() == Digest(ArchiveData) + "  " + Stem + ".zip",
                "ZIP checksum mismatch")
        Bundle = ReadZip(ArchiveData)
        Provenance = json.loads(Assets[Stem + ".provenance.json"])
        Require(json.loads(Bundle["PROVENANCE.json"]) == Provenance, "Embedded provenance mismatch")
        Expected = {Key: Manifest[Key] for Key in ("releaseVersion", "tag", "packageVersion",
                                                    "nativeAbi", "packageSchema", "luauRevision")}
        Expected.update(api=Manifest["apiName"] + " " + Manifest["apiVersion"],
                        providerProtocol=Manifest["providerProtocolName"] + "/" + Manifest["providerProtocolVersion"],
                        rid=Rid, sourceRevision=Revision, sourceState="committed", storageProtocol=1,
                        sqliteVersion="3.53.4",
                        sqliteSourceId="2026-07-24 19:02:57 bf7c7f30031888f4e796e429ab3978879485813aaca6f641c7b33e4e09459bcc",
                        sqliteCompileOptions=["THREADSAFE=0", "OMIT_LOAD_EXTENSION", "OMIT_WAL"])
        for Key, Value in Expected.items():
            Require(Provenance.get(Key) == Value, "Provenance identity mismatch: " + Key)
        Windows = Rid == "win-x64"
        Directory = f"carbon/data/CarbonLuau/native/{Rid}/"
        Payloads = {"packageSha256": "carbon/plugins/CarbonLuau.cszip",
                    "nativeSha256": Directory + ("carbonluau_native.dll" if Windows else "libcarbonluau_native.so"),
                    "compilerSha256": Directory + ("carbonluau_compiler.exe" if Windows else "carbonluau_compiler"),
                    "storageSha256": Directory + ("carbonluau_storage.exe" if Windows else "carbonluau_storage")}
        for Key, Name in Payloads.items():
            Require(Digest(Bundle[Name]) == Provenance.get(Key), "Payload hash mismatch: " + Key)
        Package = ReadZip(Bundle[Payloads["packageSha256"]])
        Require(set(Package) == set(Flat), "Production package source/fixture mismatch")
        for Name, Data in Flat.items():
            Require(TextBytes(Package[Name]) == TextBytes(Data), "Tagged production source mismatch: " + Name)
        Reference = {Name: Data for Name, Data in Sources.items()
                     if (Name.startswith("docs/api/") and Name.endswith(".md"))
                     or (Name.startswith("examples/") and (Name.endswith(".luau") or Name.endswith("/addon.json")))
                     or Name in ("api/carbonluau-api.json", "generated/carbonluau.d.luau")}
        for Name, Data in Reference.items():
            Require(Name in Bundle and TextBytes(Bundle[Name]) == TextBytes(Data), "Tagged reference mismatch: " + Name)
        Aliases = {"INSTALLATION.md": "docs/Installation.md",
                   "RELEASE-NOTES.md": "docs/releases/0.7.0.md",
                   "GUI.md": "docs/api/Gui.md", "GUI-REFERENCE.md": "docs/api/Gui-Reference.md",
                   "LICENSE": "LICENSE", "THIRD_PARTY_NOTICES.md": "THIRD_PARTY_NOTICES.md",
                   "LUAU-LICENSE.txt": "native/third_party/luau/LICENSE.txt",
                   "LUA-LICENSE.txt": "native/third_party/luau/lua_LICENSE.txt"}
        for Name, Source in Aliases.items():
            Require(TextBytes(Bundle[Name]) == TextBytes(Sources[Source]), "Tagged document alias mismatch: " + Name)
        Allowed = set(Reference) | set(Payloads.values()) | {"PROVENANCE.json", "INSTALLATION.md",
                  "RELEASE-NOTES.md", "GUI.md", "GUI-REFERENCE.md", "LICENSE", "THIRD_PARTY_NOTICES.md",
                  "LUAU-LICENSE.txt", "LUA-LICENSE.txt"}
        Require(set(Bundle) == Allowed, "Unexpected/missing bundle content")
        if not Windows:
            with zipfile.ZipFile(io.BytesIO(ArchiveData)) as Archive:
                for Key in ("compilerSha256", "storageSha256"):
                    Entry = Archive.getinfo(Payloads[Key])
                    Require(Entry.create_system == 3 and (Entry.external_attr >> 16) & 0o777 == 0o755,
                            "Linux worker executable metadata")
        Report.append(f"{Rid}: {len(Bundle)} entries, {len(Package)} production sources, SHA256={Digest(ArchiveData)}")
    return Report


def SelfTest():
    def Fixture(Names):
        Stream = io.BytesIO()
        with zipfile.ZipFile(Stream, "w") as Archive:
            for Name in Names:
                Entry = zipfile.ZipInfo(Name)
                Entry.filename = Name  # Preserve malformed spelling on Windows.
                Archive.writestr(Entry, b"test")
        return Stream.getvalue()
    Require(ReadZip(Fixture(["valid/file.cs"])) == {"valid/file.cs": b"test"}, "Valid ZIP fixture")
    for Names in (["../escape"], ["/absolute"], ["C:/drive"], ["a\\b"], ["a//b"], ["./a"],
                  [f"file{Index}" for Index in range(513)]):
        try:
            ReadZip(Fixture(Names))
        except ValueError:
            continue
        raise ValueError("Unsafe ZIP fixture accepted")
    print("[CarbonLuau:PublishedRelease] PASS offline ZIP negative fixtures")


def Main():
    Parser = argparse.ArgumentParser(description=__doc__)
    Parser.add_argument("--assets", type=Path)
    Parser.add_argument("--tag", default="v0.7.0", choices=("v0.7.0",))
    Arguments = Parser.parse_args()
    SelfTest()
    if Arguments.assets:
        Root = Path(__file__).resolve().parent.parent
        Revision, Sources = TaggedSources(Root, Arguments.tag)
        Assets = {}
        for File in Arguments.assets.iterdir():
            if File.is_file() and File.name.startswith("CarbonLuau-"):
                Require(File.stat().st_size <= 32 * 1024 * 1024, "Asset byte audit limit")
                Assets[File.name] = File.read_bytes()
        for Line in Audit(Assets, Revision, Sources):
            print("[CarbonLuau:PublishedRelease] PASS " + Line)
        for Name in ("LICENSE", "THIRD_PARTY_NOTICES.md", "native/third_party/luau/LICENSE.txt",
                     "native/third_party/luau/lua_LICENSE.txt"):
            Changed = dict(Sources)
            Changed[Name] = b""
            try:
                Audit(Assets, Revision, Changed)
            except ValueError as Error:
                Require("Tagged document alias mismatch" in str(Error), "Wrong attribution rejection")
            else:
                raise ValueError("Attribution content mismatch accepted")
        print("[CarbonLuau:PublishedRelease] PASS four attribution-content negative fixtures")
        print("[CarbonLuau:PublishedRelease] Tagged source " + Revision + "; no binary execution/live evidence")


if __name__ == "__main__":
    try:
        Main()
    except (ValueError, KeyError, OSError, zipfile.BadZipFile, subprocess.CalledProcessError) as Error:
        raise SystemExit("[CarbonLuau:PublishedRelease] FAIL " + str(Error))
