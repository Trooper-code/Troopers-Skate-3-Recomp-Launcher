#!/usr/bin/env python3
"""Checks a coding agent's report before it's reviewed, so the review starts from a complete report.

Copy this file to scripts/check-report.py in your project, then run:

    python scripts/check-report.py docs/reports/M2-save-format.md

It checks that:
  1. the report has a Status line (done / stopped / partial) and every required section;
  2. a stopped report says why ("## Stopped because");
  3. every finding is labeled confirmed / unverified / wrong, and has evidence;
  4. the Tests section shows a command and its output;
  5. no template placeholders are left (like <MILESTONE> or <claim>).

The coordinator runs it first when reviewing. An agent can run it on its own report before finishing.

Exit code 0 = good, 1 = something to fix. Each problem says how to fix it.
Only uses the Python standard library.
"""
import re
import sys
from pathlib import Path

REQUIRED_SECTIONS = ["Tool versions", "What changed", "Findings", "Tests", "Open questions"]
LABELS = {"confirmed", "unverified", "wrong"}
PLACEHOLDER = re.compile(r"<(MILESTONE|slug|branch|tag|hash|tool|version|file or area|claim|command[^>]*|"
                         r"the summary line[^>]*|which[^>]*|anything[^>]*|docs/research/\.\.\.[^>]*)>", re.I)


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


def table_rows(block: str) -> list[list[str]]:
    rows = []
    for line in block.splitlines():
        s = line.strip()
        if not s.startswith("|"):
            continue
        cells = [c.strip() for c in s.strip("|").split("|")]
        if all(set(c) <= set("-: ") for c in cells):
            continue
        rows.append(cells)
    return rows[1:] if rows else []  # drop the header row


def main() -> int:
    if len(sys.argv) < 2:
        print("Usage: python scripts/check-report.py docs/reports/<MILESTONE>-<slug>.md")
        return 1
    path = Path(sys.argv[1])
    if not path.exists():
        print(f"Can't find {path}. Check the path; reports live in docs/reports/ on the agent's branch.")
        return 1
    text = re.sub(r"<!--.*?-->", "", path.read_text(encoding="utf-8"), flags=re.S)
    secs = sections(text)
    problems = []

    m = re.search(r"\*\*Status:\*\*\s*([a-z]+)", text, re.I)
    status = m.group(1).lower() if m else None
    if status not in {"done", "stopped", "partial"}:
        problems.append("missing or unclear '**Status:**' line. Use exactly one of: done, stopped, partial.")

    for s in REQUIRED_SECTIONS:
        if s.lower() not in secs:
            problems.append(f"missing section '## {s}'.")
    if status == "stopped" and not secs.get("stopped because", "").strip():
        problems.append("status is 'stopped' but '## Stopped because' is missing or empty. Say which stop "
                        "condition, what was tried, and what's needed to continue.")

    findings = table_rows(secs.get("findings", ""))
    if "findings" in secs and not findings:
        problems.append("'## Findings' has no table rows. Use the table: Finding | Status | Evidence. "
                        "If there are truly no findings, write one row saying so, labeled unverified.")
    for row in findings:
        if len(row) < 3:
            problems.append(f"finding row has fewer than 3 columns: {' | '.join(row)}")
            continue
        label = row[1].lower().strip("* ")
        if label not in LABELS:
            problems.append(f"finding '{row[0][:50]}' is labeled '{row[1]}'. Use confirmed, unverified or wrong.")
        if not row[2] or set(row[2]) <= set("-—? "):
            problems.append(f"finding '{row[0][:50]}' has no evidence. Cite a file, address, log, capture or test.")

    tests = secs.get("tests", "")
    if "tests" in secs and "```" not in tests:
        problems.append("'## Tests' has no code block. Paste the command you ran and the summary line it printed.")

    left = sorted({m.group(0) for m in PLACEHOLDER.finditer(text)})
    if left:
        problems.append(f"template placeholders left in: {', '.join(left)}. Replace them with the real values.")

    if problems:
        print(f"{path}: {len(problems)} problem(s)")
        for p in problems:
            print(f"  - {p}")
        return 1
    print(f"{path}: OK ({len(findings)} finding(s), status {status})")
    return 0


if __name__ == "__main__":
    sys.exit(main())
