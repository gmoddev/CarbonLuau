"""Check local public Markdown destinations and GitHub-style fragments."""
from collections import defaultdict
from pathlib import Path
import re
import sys
from urllib.parse import unquote, urlsplit


def Anchors(Text):
    Values = set()
    Counts = defaultdict(int)
    Fence = None
    for Line in Text.splitlines():
        Mark = re.match(r"^\s{0,3}(`{3,}|~{3,})", Line)
        if Mark:
            if Fence is None:
                Fence = Mark[1][0]
            elif Fence == Mark[1][0]:
                Fence = None
            continue
        if Fence:
            continue
        Values.update(re.findall(r'<a\s+(?:id|name)=["\x27]([^"\x27]+)["\x27]', Line))
        Match = re.match(r"^#{1,6}\s+(.+?)(?:\s+#+)?$", Line)
        if Match:
            Title = re.sub(r"<[^>]+>", "", Match[1]).strip().lower()
            Base = re.sub(r"[^\w\- ]", "", Title).replace(" ", "-")
            Number = Counts[Base]
            Counts[Base] += 1
            Values.add(Base + ("-" + str(Number) if Number else ""))
    return Values


def Check(Root):
    Root = Root.resolve()
    Documents = [Root / "README.md"] + sorted((Root / "docs").rglob("*.md"))
    Count = 0
    for Source in Documents:
        Text = Source.read_text(encoding="utf-8-sig")
        for Match in re.finditer(r"\]\(([^)\s]+)\)", Text):
            Raw = Match[1]
            Url = urlsplit(Raw)
            if Url.scheme or Url.netloc:
                if Url.scheme not in ("https", "http", "mailto"):
                    raise ValueError("unsupported documentation URL")
                continue
            Target = (Source.parent / unquote(Url.path)).resolve() if Url.path else Source
            if not Target.is_relative_to(Root) or not Target.exists():
                raise ValueError("missing/confined public documentation target: " + str(Source.relative_to(Root)))
            if Url.fragment and Target.suffix == ".md" and unquote(Url.fragment) not in Anchors(Target.read_text(encoding="utf-8-sig")):
                raise ValueError("missing public documentation fragment: " + str(Source.relative_to(Root)))
            Count += 1
    print("[CarbonLuau:PublicDocuments] PASS documents=" + str(len(Documents)) + " links=" + str(Count))


if __name__ == "__main__":
    Check(Path(__file__).resolve().parents[2])
