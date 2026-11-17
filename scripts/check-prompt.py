#!/usr/bin/env python3
"""Checks an agent prompt before it's handed to the coding agent, so the agent can run it without asking.

Copy this file to scripts/check-prompt.py in your project, then run:

    python scripts/check-prompt.py docs/prompts/M2-save-format.md

It checks that:
  1. every required section is there;
  2. "Read first" lists AGENTS.md first (the agent's standing rules);
  3. the branch names both a branch and the tag to start from;
  4. every known fact is labeled confirmed or unverified, with evidence;
  5. there are acceptance checks and a report path in docs/reports/;
  6. no template placeholders are left (like <MILESTONE> or <...>).

Exit code 0 = good, 1 = something to fix. Each problem says how to fix it.
Only uses the Python standard library.
"""
import re
import sys
from pathlib import Path

REQUIRED_SECTIONS = ["Read first", "Goal", "Branch", "Known facts", "Tasks", "Stop and report if",
                     "Acceptance checks", "Report"]
PLACEHOLDER = re.compile(r"<(MILESTONE|slug|milestone|last-good-tag|topic|N|\.\.\.|test command|"
                         r"One or two sentences[^>]*|Only what[^>]*|Task-specific[^>]*|Any other tool[^>]*|"
                         r"section|D#|e\.g\.[^>]*)>")


def sections(text: str) -> dict[str, str]:
    out, name, buf = {}, None, []
    for line in text.splitlines():
        if line.startswith("## "):
            if name:
                out[name] = "\n".join(buf)
            name, buf = line[3:].strip().lower(), []
        elif name:
            buf.append(line)
    if name:
        out[name] = "\n".join(buf)
    return out


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: python scripts/check-prompt.py docs/prompts/<MILESTONE>-<slug>.md")
        return 1
    path = Path(sys.argv[1])
    if not path.exists():
        print(f"Can't find {path}. Prompts live in docs/prompts/.")
        return 1
    text = re.sub(r"<!--.*?-->", "", path.read_text(encoding="utf-8"), flags=re.S)
    secs = sections(text)
    problems = []

    for s in REQUIRED_SECTIONS:
        if s.lower() not in secs:
            problems.append(f"missing section '## {s}'.")

    items = [l.strip() for l in secs.get("read first", "").splitlines() if l.strip().startswith(("-", "*", "1."))]
    if "read first" in secs and (not items or "AGENTS.md" not in items[0]):
        problems.append("'Read first' must list AGENTS.md first, so the agent loads its standing rules.")

    branch = secs.get("branch", "")
    if "branch" in secs and not (re.search(r"`[^`]+`", branch) and re.search(r"\btag\b", branch, re.I)):
        problems.append("'Branch' needs the branch name in backticks and the tag to branch from.")

    for line in secs.get("known facts", "").splitlines():
        s = line.strip()
        if not s.startswith("|"):
            continue
        cells = [c.strip() for c in s.strip("|").split("|")]
        if all(set(c) <= set("-: ") for c in cells) or cells[0].lower() == "fact":
            continue
        if len(cells) < 3 or cells[1].lower().strip("* ") not in {"confirmed", "unverified"}:
            problems.append(f"known fact '{cells[0][:50]}' must be labeled confirmed or unverified.")
        elif not cells[2]:
            problems.append(f"known fact '{cells[0][:50]}' has no evidence.")

    if "acceptance checks" in secs and not secs["acceptance checks"].strip():
        problems.append("'Acceptance checks' is empty. Say which tests must pass.")
    if "report" in secs and "docs/reports/" not in secs["report"]:
        problems.append("'Report' must say where the report goes: docs/reports/<MILESTONE>-<slug>.md.")

    left = sorted({m.group(0) for m in PLACEHOLDER.finditer(text)})
    if left:
        problems.append(f"template placeholders left in: {', '.join(left)}. Replace them with the real values.")

    if problems:
        print(f"{path}: {len(problems)} problem(s)")
        for p in problems:
            print(f"  - {p}")
        return 1
    print(f"{path}: OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
