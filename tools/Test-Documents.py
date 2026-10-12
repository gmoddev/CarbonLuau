#!/usr/bin/env python3
"""Check retained documentation bodies, delegated rules and local fragments.

The coverage manifest is derived from the approval baseline. No old Git objects
or network are needed, so shallow final-source CI checkouts can run this check.
"""
from collections import defaultdict
import hashlib
import json
from pathlib import Path
import re
import sys
from urllib.parse import unquote

Root = Path(__file__).resolve().parents[1]

def Require(Condition, Message):
    if not Condition:
        raise ValueError(Message)

def Semantic(Text):
    Text = re.sub(r'<a\s+id="[^"]+"></a>\s*', "", Text)
    Text = re.sub(r"\]\(([^)]+)\)", "](<target>)", Text)
    return Text.replace("\r\n", "\n").strip()

def Digest(Text):
    return hashlib.sha256(Text.encode("utf-8")).hexdigest()

def Slug(Title):
    Title = re.sub(r"<[^>]+>", "", Title).strip().lower()
    return re.sub(r"[^\w\- ]", "", Title).replace(" ", "-")

def Anchors(Text):
    Values = set()
    Duplicate = set()
    Counts = defaultdict(int)
    Fence = None
    for Line in Text.splitlines():
        Mark = re.match(r"^\s{0,3}(`{3,}|~{3,})", Line)
        if Mark:
            if Fence is None: Fence = Mark.group(1)[0]
            elif Fence == Mark.group(1)[0]: Fence = None
            continue
        if Fence: continue
        for Explicit in re.findall(r'<a\s+(?:id|name)=["\x27]([^"\x27]+)["\x27]', Line):
            if Explicit in Values: Duplicate.add(Explicit)
            Values.add(Explicit)
        Match = re.match(r"^#{1,6}\s+(.+?)(?:\s+#+)?$", Line)
        if Match:
            Base = Slug(Match.group(1))
            Count = Counts[Base]
            Counts[Base] += 1
            Anchor = Base + (f"-{Count}" if Count else "")
            if Anchor in Values: Duplicate.add(Anchor)
            Values.add(Anchor)
    return Values, Duplicate

def CheckLink(Source, Raw, Texts):
    Raw = Raw.strip().strip("<>")
    if Raw.startswith(("http://", "https://", "mailto:", "codex://")):
        return
    PathPart, _, Fragment = Raw.partition("#")
    Target = (Source.parent / unquote(PathPart)).resolve() if PathPart else Source.resolve()
    Require(Target.is_relative_to(Root), f"Link escapes repository: {Source.relative_to(Root)} -> {Raw}")
    Require(Target.exists(), f"Missing target: {Source.relative_to(Root)} -> {Raw}")
    if Fragment and Target.suffix == ".md":
        Text = Texts.get(Target) or Target.read_text(encoding="utf-8-sig")
        Values, _ = Anchors(Text)
        Require(unquote(Fragment) in Values, f"Missing fragment: {Source.relative_to(Root)} -> {Raw}")

def CheckCoverage(Texts):
    Meta = json.loads((Root / "docs/evidence/DocumentationCoverage.json").read_text(encoding="utf-8"))
    Require(len(Meta["OriginalDocuments"]) == 171, "Approval-baseline inventory changed")
    Require(len(Meta["Spans"]) == 97, "Retained evidence/criteria inventory changed")
    Require(len(Meta["Deleted"]) == 2, "Unexpected deletion list")
    for Span in Meta["Spans"]:
        Target = Root / Span["Destination"]
        Text = Texts.get(Target.resolve()) or Target.read_text(encoding="utf-8")
        Name = Path(Span["Source"]).name
        Pattern = r"<!-- BEGIN RECORD " + re.escape(Name) + r" -->\s*([\s\S]*?)\s*<!-- END RECORD " + re.escape(Name) + r" -->"
        Records = re.findall(Pattern, Text)
        Matches = [Record for Record in Records if Digest(Semantic(Record)) == Span["SemanticSha256"]]
        Require(len(Matches) == 1, f"Source span missing/changed/duplicated: {Span['Source']} ({Span['Kind']})")
        External = sorted(re.findall(r"https?://[^\s)>]+", Matches[0]))
        Require(Digest("\n".join(External)) == Span["ExternalUrlsSha256"], f"Immutable source/evidence URLs changed: {Span['Source']}")
    Register = (Root / "docs/Invariants.md").read_text(encoding="utf-8")
    Prefix = Register[:Register.index("## Decision register")]
    Require(Digest(Semantic(Prefix)) == Meta["ProtectedGlobalRulesSemanticSha256"], "Global I1-I12 rules changed")
    for Relative in Meta["BaselinePublicApiPaths"]:
        Require((Root / Relative).is_file(), f"Public API URL removed: {Relative}")
    for Relative in Meta["Deleted"]:
        Require(not (Root / Relative).exists(), f"Superseded checkpoint remains: {Relative}")
    return len(Meta["Spans"])

def SelfTest():
    Require(Slug("D20 — World/Entity Foundation 1") == "d20--worldentity-foundation-1", "Decision slug")
    Values, Duplicate = Anchors('# Title\n## Same\n## Same\n```lua\n# Fake\n```\n<a id="explicit"></a>\n')
    Require({"title", "same", "same-1", "explicit"} == Values and not Duplicate, "Heading/fence/explicit anchors")
    Require(Anchors('# Title\n<a id="title"></a>\n')[1] == {"title"}, "Duplicate-anchor negative")
    for Raw in ("does-not-exist.md", "docs/api/Types/Player.md#giveitem"):
        try:
            CheckLink(Root / "README.md", Raw, {})
        except ValueError:
            pass
        else:
            raise ValueError("Missing file/removed fragment negative was accepted")
    Require(Digest(Semantic('text [link](one.md)\n')) == Digest(Semantic('text [link](two.md)\n')), "Mechanical link normalization")
    Require(Digest(Semantic('old rule\n')) != Digest(Semantic('weakened rule\n')), "Rule-change negative")

def Main():
    SelfTest()
    Paths = sorted((Root / "docs").rglob("*.md")) + [Root / "AICONTEXT.md", Root / "README.md", Root / "CHANGELOG.md"]
    Texts = {Path.resolve(): Path.read_text(encoding="utf-8-sig") for Path in Paths}
    Links = 0
    for Source, Text in Texts.items():
        _, Duplicate = Anchors(Text)
        Require(not Duplicate, f"Duplicate anchors in {Source.relative_to(Root)}: {sorted(Duplicate)}")
        for Match in re.finditer(r"\]\(([^)]+)\)", Text):
            CheckLink(Source, Match.group(1), Texts)
            Links += 1
    Spans = CheckCoverage(Texts)
    print(f"[CarbonLuau:Documents] PASS {len(Paths)} documents, {Links} links/fragments, {Spans} retained spans, I1-I12, stable API paths and negative fixtures")

if __name__ == "__main__":
    try:
        Main()
    except (ValueError, OSError, KeyError) as Error:
        print("[CarbonLuau:Documents] FAIL " + str(Error), file=sys.stderr)
        raise SystemExit(1)
