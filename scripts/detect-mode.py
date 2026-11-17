#!/usr/bin/env python3
"""Works out which agent-coordinator mode a repo needs, and which workflow files it's missing.

Run it from anywhere:

    python detect-mode.py path/to/repo
    python detect-mode.py path/to/repo --json

Modes (the same table as the agent-coordinator skill):
  continue - the repo has docs/HANDOFF.md: read it and do its "First thing to do"
  adopt    - the repo has code or docs but no HANDOFF.md: scan it and add only what's missing
  kickoff  - the repo is empty, or holds only the files GitHub creates (README, LICENSE, .gitignore)

It also finds the check scripts' folder (scripts/ at the repo root, or docs/scripts/), and lists the workflow files
that exist and the ones that are missing, plus any existing agent rules files and
planning notes it found. That's the inventory step of "adopt", done without spending tool calls on it.

Exit code 0 always (it reports; it doesn't judge). Only uses the Python standard library.
"""
import json
import sys
from pathlib import Path

# Files a brand-new GitHub repo usually starts with. A repo with only these is still "empty".
STARTER_FILES = {"readme.md", "readme", "readme.txt", "license", "license.md", "license.txt",
                 ".gitignore", ".gitattributes"}
WORKFLOW_FILES = [
    "AGENTS.md",
    "docs/HANDOFF.md",
    "docs/MILESTONES.md",
    "docs/DECISIONS.md",
    "docs/research/INDEX.md",
    "docs/prompts/",
    "docs/reports/TEMPLATE.md",
    "scripts/check-handoff.py",
    "scripts/check-research.py",
    "scripts/check-report.py",
    "scripts/check-prompt.py",
    "scripts/check-safe-to-publish.py",
    "scripts/release-notes.py",
    "scripts/hooks/pre-commit",
    ".github/workflows/checks.yml",
    ".github/workflows/release.yml",
    "CHANGELOG.md",
]
SCRIPT_DIRS = ["scripts", "docs/scripts"]
RULES_FILES = ["AGENTS.md", "CLAUDE.md", "GEMINI.md", ".cursorrules", ".cursor/rules",
               ".github/copilot-instructions.md", ".agent/rules", ".windsurfrules"]
NOTES_FILES = ["RESEARCH.md", "NOTES.md", "TODO.md", "ROADMAP.md", "CHANGELOG.md", "CONTRIBUTING.md", "docs"]


def user_files(repo: Path) -> list[Path]:
    """Every file in the repo except .git internals."""
    return [p for p in repo.rglob("*") if p.is_file() and ".git" not in p.relative_to(repo).parts]


def detect(repo: Path) -> dict:
    files = user_files(repo)
    meaningful = [p for p in files if p.relative_to(repo).as_posix().lower() not in STARTER_FILES]
    if (repo / "docs" / "HANDOFF.md").exists():
        mode = "continue"
    elif meaningful:
        mode = "adopt"
    else:
        mode = "kickoff"
    # The check scripts may live in scripts/ (repo root) or docs/scripts/. Use whichever the repo already has;
    # a repo with neither gets scripts/.
    scripts_dir = next((d for d in SCRIPT_DIRS if any((repo / d).glob("check-*.py"))),
                       next((d for d in SCRIPT_DIRS if (repo / d).is_dir()), SCRIPT_DIRS[0]))

    def exists(rel: str) -> bool:
        if rel.startswith("scripts/"):
            rel = scripts_dir + rel[len("scripts"):]
        return (repo / rel.rstrip("/")).exists()

    def shown(rel: str) -> str:
        return scripts_dir + rel[len("scripts"):] if rel.startswith("scripts/") else rel

    return {
        "mode": mode,
        "files": len(files),
        "scripts_dir": scripts_dir,
        "have": [shown(f) for f in WORKFLOW_FILES if exists(f)],
        "missing": [shown(f) for f in WORKFLOW_FILES if not exists(f)],
        "rules_files": [f for f in RULES_FILES if exists(f)],
        "notes": [f for f in NOTES_FILES if exists(f)],
    }


def main() -> int:
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    repo = Path(args[0] if args else ".").resolve()
    if not repo.is_dir():
        print(f"{repo} isn't a folder. Pass the path to the repo.")
        return 0
    r = detect(repo)
    if "--json" in sys.argv:
        print(json.dumps(r, indent=2))
        return 0
    why = {"continue": "docs/HANDOFF.md exists",
           "adopt": "it has code or docs but no docs/HANDOFF.md",
           "kickoff": "it's empty apart from starter files"}[r["mode"]]
    print(f"Mode: {r['mode']} ({why})")
    print(f"Scripts folder: {r['scripts_dir']}/")
    print(f"Workflow files present: {', '.join(r['have']) or 'none'}")
    print(f"Workflow files missing: {', '.join(r['missing']) or 'none'}")
    if r["rules_files"]:
        print(f"Existing agent rules files: {', '.join(r['rules_files'])} (extend these, don't replace them)")
    if r["notes"]:
        print(f"Planning notes to read: {', '.join(r['notes'])}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
