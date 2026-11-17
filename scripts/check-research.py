#!/usr/bin/env python3
"""Checks the research docs: which confirmed facts need re-checking, and whether the research index is accurate.

Copy this file to scripts/check-research.py in your project, then run from the repo root:

    python scripts/check-research.py

1. VERSION DRIFT. A confirmed fact can say which tag and tool versions it was proven on, with a stamp:

       Save header is 0x40 bytes. [confirmed @ m3-p1 | xenia-canary=1.2.3, dotnet=8.0.1]

   This compares every stamp with the "## Tool versions" table in docs/HANDOFF.md. If a tool has changed version
   since the fact was proven, the fact is listed so the next agent prompt re-checks it. Facts confirmed before a
   tool update are the ones most likely to have quietly broken.

2. RESEARCH INDEX. docs/research/INDEX.md lists each topic and where it lives, as links:

       | Save header layout | [save-format.md § Header](save-format.md#header) |

   This checks that every link points to a file that exists, and to a heading that exists in it. It also lists
   research docs the index doesn't mention.

Exit code 0 = nothing to do, 1 = facts to re-check or index entries to fix. Use --warn-only to always exit 0.
Only uses the Python standard library.
"""
import re
import sys
from pathlib import Path

# ---- Settings ----
HANDOFF = Path("docs/HANDOFF.md")
RESEARCH_DIR = Path("docs/research")
EXTRA_RESEARCH_FILES = [Path("research.md")]  # this repo keeps its research in research.md (template default: RESEARCH.md)
INDEX = RESEARCH_DIR / "INDEX.md"
# ------------------

STAMP = re.compile(r"\[confirmed\s*@\s*([^\]|]+?)\s*(?:\|\s*([^\]]*))?\]", re.I)


def norm_tool(name: str) -> str:
    return re.sub(r"[`*\s]", "", name).lower()


def tool_versions(handoff: Path) -> dict[str, str]:
    """Reads the 'Tool | Version' rows under '## Tool versions' in HANDOFF.md."""
    if not handoff.exists():
        return {}
    versions, in_section = {}, False
    for line in handoff.read_text(encoding="utf-8").splitlines():
        if line.startswith("## "):
            in_section = line[3:].strip().lower() == "tool versions"
            continue
        if in_section and line.strip().startswith("|"):
            cells = [c.strip() for c in line.strip().strip("|").split("|")]
            if len(cells) < 2 or set(cells[0]) <= set("-: ") or cells[0].lower() == "tool":
                continue
            versions[norm_tool(cells[0])] = cells[1].strip("` ")
    return versions


def github_slug(heading: str) -> str:
    """How GitHub turns a heading into a #link: lowercase, drop punctuation, spaces become hyphens."""
    s = heading.strip().lower()
    s = re.sub(r"[^\w\- ]", "", s)
    return s.replace(" ", "-")


def headings_of(path: Path) -> set[str]:
    return {github_slug(m.group(1)) for m in re.finditer(r"^#{1,6}\s+(.+?)\s*#*\s*$",
                                                         path.read_text(encoding="utf-8"), re.M)}


def main() -> int:
    warn_only = "--warn-only" in sys.argv
    research = sorted(RESEARCH_DIR.rglob("*.md")) if RESEARCH_DIR.exists() else []
    research += [p for p in EXTRA_RESEARCH_FILES if p.exists()]
    research = [p for p in research if p != INDEX]
    issues = 0

    # 1. Version drift
    current = tool_versions(HANDOFF)
    stale, stamped = [], 0
    for doc in research:
        for n, line in enumerate(doc.read_text(encoding="utf-8").splitlines(), 1):
            for m in STAMP.finditer(line):
                stamped += 1
                tag, pins = m.group(1).strip(), (m.group(2) or "")
                for pin in filter(None, (p.strip() for p in pins.split(","))):
                    if "=" not in pin:
                        continue
                    tool, ver = (x.strip() for x in pin.split("=", 1))
                    now = current.get(norm_tool(tool))
                    if now and now != ver:
                        stale.append(f"{doc}:{n}: proven on {tag} with {tool} {ver}; now {now}. Re-check it.")
    if not current:
        print(f"Version drift: skipped (no '## Tool versions' table found in {HANDOFF}).")
    elif stale:
        issues += len(stale)
        print(f"Version drift: {len(stale)} confirmed fact(s) were proven on older tool versions:")
        for s in stale:
            print(f"  - {s}")
        print("  Fix: add re-checking these to the next agent prompt, then update each stamp.")
    else:
        print(f"Version drift: none ({stamped} stamped fact(s) checked).")

    # 2. Research index
    if not INDEX.exists():
        if research:
            print(f"Research index: {INDEX} doesn't exist yet. Create it from templates/research/INDEX.md.")
        return 0 if warn_only else (1 if issues else 0)
    text = INDEX.read_text(encoding="utf-8")
    bad, linked = [], set()
    for target in re.findall(r"\]\(([^)\s]+)\)", text):
        if "://" in target:
            continue
        file_part, _, anchor = target.partition("#")
        f = (INDEX.parent / file_part).resolve() if file_part else INDEX.resolve()
        linked.add(f)
        if not f.exists():
            bad.append(f"links to {file_part}, which doesn't exist. Fix the path or remove the entry.")
        elif anchor and anchor.lower() not in headings_of(f):
            bad.append(f"links to {target}, but {file_part} has no heading for #{anchor}. "
                       "Fix the anchor (it's the heading in lowercase, spaces as hyphens).")
    unlisted = [p for p in research if p.resolve() not in linked]
    if bad:
        issues += len(bad)
        print(f"Research index: {len(bad)} broken entr{'y' if len(bad) == 1 else 'ies'}:")
        for b in bad:
            print(f"  - {b}")
    else:
        print("Research index: every entry resolves.")
    if unlisted:
        print("Research index: not listed yet (add a line for each):")
        for p in unlisted:
            print(f"  - {p}")
    return 0 if warn_only or not issues else 1


if __name__ == "__main__":
    sys.exit(main())
