#!/usr/bin/env python3
"""Checks docs/HANDOFF.md before it's committed, so the next chat starts from a small, accurate file.

Copy this file to scripts/check-handoff.py in your project, then run:

    python scripts/check-handoff.py              # checks docs/HANDOFF.md
    python scripts/check-handoff.py path/to/HANDOFF.md

It checks that:
  1. the file is within its size budget (every new chat pays for every line);
  2. every required section is there;
  3. it ends with the agent-summary line (the placeholder, a pasted summary, or a report path);
  4. every repo path it mentions in backticks or links actually exists, so pointers to the research docs,
     prompts and reports aren't stale. Paths with <placeholders> or wildcards are skipped.
  5. (a note, not a failure) lessons marked "(x2)" or more have been hit twice, so they should become a rule in
     AGENTS.md or a check script instead of staying a lesson.

Exit code 0 = good, 1 = something to fix. Each problem says how to fix it.
Only uses the Python standard library.
"""
import re
import sys
from pathlib import Path

# ---- Settings: change these if your project needs different limits or sections. ----
MAX_LINES = 120          # the budget is ~100 lines; this leaves a little room
MAX_BYTES = 10 * 1024    # ~10 KB
REQUIRED_SECTIONS = [
    "Setup",
    "Tool versions",
    "How it works now",
    "Where it stops",
    "In progress",
    "Rules",
    "First thing to do",
    "Lessons",
]
# A path is only checked if it starts with one of these (so tool names and commands aren't mistaken for files).
PATH_PREFIXES = ("docs/", "scripts/", "src/", "tests/", "AGENTS.md", "README", "RESEARCH.md", "CHANGELOG.md")
# -------------------------------------------------------------------------------------


def find_repo_root(start: Path) -> Path:
    """Walks up from the handoff file until it finds the folder that contains .git."""
    for p in [start, *start.parents]:
        if (p / ".git").exists():
            return p
    return start.parent.parent  # docs/HANDOFF.md -> repo root, if there's no .git (e.g. a copied folder)


def main() -> int:
    handoff = Path(sys.argv[1] if len(sys.argv) > 1 else "docs/HANDOFF.md").resolve()
    if not handoff.exists():
        print(f"Can't find {handoff}. Run this from the repo root, or pass the path to HANDOFF.md.")
        return 1

    raw = handoff.read_bytes()
    text = raw.decode("utf-8", errors="replace")
    lines = text.splitlines()
    root = find_repo_root(handoff.parent)
    problems = []

    # 1. Size budget
    if len(lines) > MAX_LINES:
        problems.append(
            f"{len(lines)} lines (budget {MAX_LINES}). Move history to MILESTONES.md and findings to the research "
            "docs, then shorten 'How it works now' and merge similar Lessons."
        )
    if len(raw) > MAX_BYTES:
        problems.append(f"{len(raw)} bytes (budget {MAX_BYTES}). Same fix as above.")

    # 2. Required sections (## headings, case-insensitive)
    headings = {m.group(1).strip().lower() for m in re.finditer(r"^##\s+(.+?)\s*$", text, re.M)}
    for section in REQUIRED_SECTIONS:
        if section.lower() not in headings:
            problems.append(f"missing section '## {section}'. Add it (see templates/HANDOFF.md).")

    # 3. Ends with the agent-summary line: it must come after the last required section (normally Lessons).
    #    A pasted summary may contain its own "## " headings, so look from the last *required* section onward.
    required = {s.lower() for s in REQUIRED_SECTIONS}
    last_required = max(
        (i for i, l in enumerate(lines) if l.startswith("## ") and l[3:].strip().lower() in required), default=-1
    )
    last_block = "\n".join(lines[last_required + 1:]).lower()
    # Accepts "[Paste <any agent>'s latest summary here]", a pasted "... summary (unreviewed):", or a report path.
    if not (
        re.search(r"\[paste .{0,60}summary here", last_block)
        or "summary (unreviewed)" in last_block
        or "docs/reports/" in last_block
    ):
        problems.append(
            "doesn't end with the agent-summary line. Put "
            "\"[Paste the coding agent's latest summary here]\" back as the last line."
        )

    # 4. Referenced paths exist
    candidates = set(re.findall(r"`([^`\s]+)`", text)) | set(re.findall(r"\]\(([^)\s#]+)", text))
    for c in sorted(candidates):
        c = c.rstrip(".,:;")
        if not c.startswith(PATH_PREFIXES) or re.search(r"[<>*?{}]", c) or "://" in c:
            continue
        if c.startswith("docs/reports/") and c != "docs/reports/TEMPLATE.md":
            continue  # agent reports live on the agent's branch until it's merged
        if not (root / c).exists():
            problems.append(
                f"points to `{c}`, which doesn't exist. Fix the path, or write that file/section first."
            )

    # 5. Lessons seen twice or more should become rules (a warning, not a failure).
    #    Mark a repeat by adding "(x2)", "(x3)"... to the end of the lesson's line.
    promote = []
    in_lessons = False
    for line in lines:
        if line.startswith("## "):
            in_lessons = line[3:].strip().lower() == "lessons"
            continue
        m = re.search(r"\((?:x|×)\s*(\d+)\)\s*$", line.strip(), re.I) if in_lessons else None
        if m and int(m.group(1)) >= 2:
            promote.append(line.strip().lstrip("-* ").strip())

    rel = handoff.relative_to(root) if handoff.is_relative_to(root) else handoff
    for lesson in promote:
        print(f"Note: lesson seen again, promote it to a rule in AGENTS.md or a check script: {lesson}")
    if problems:
        print(f"{rel}: {len(problems)} problem(s)")
        for p in problems:
            print(f"  - {p}")
        return 1
    print(f"{rel}: OK ({len(lines)} lines, {len(raw)} bytes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
