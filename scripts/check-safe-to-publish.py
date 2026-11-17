#!/usr/bin/env python3
"""Stops game files, ROMs, saves and big binaries from being committed to a public repo by accident.

Copy this file to scripts/check-safe-to-publish.py in your project, then run:

    python scripts/check-safe-to-publish.py            # checks every file git tracks
    python scripts/check-safe-to-publish.py --staged   # checks only what you're about to commit

The pre-commit hook in templates/hooks/pre-commit runs the --staged version before every commit, and CI can run the
full check on every push.

What it blocks:
  - file types that are almost always copyrighted game data or personal saves (ROMs, disc images, console
    executables and packages, save files, emulator states);
  - any binary file over MAX_BINARY_MB.

It's a check on names, sizes and contents that look binary, not proof. It catches accidents, not someone who
renames files on purpose.

Intentional exceptions (say, a tiny test save you made yourself) go in a file called .publish-allowlist at the repo
root, one pattern per line, e.g.:

    tests/fixtures/*.sav     # made from scratch for the tests
    docs/images/*.png

Exit code 0 = safe, 1 = something to remove. Each problem says how to fix it.
Only uses the Python standard library.
"""
import fnmatch
import subprocess
import sys
from pathlib import Path

# ---- Settings: change these if your project needs different limits. ----
MAX_BINARY_MB = 5
BLOCKED_EXTENSIONS = {
    # Cartridge ROMs
    ".gb": "Game Boy ROM", ".gbc": "Game Boy Color ROM", ".gba": "Game Boy Advance ROM",
    ".nes": "NES ROM", ".sfc": "SNES ROM", ".smc": "SNES ROM", ".n64": "N64 ROM", ".z64": "N64 ROM",
    ".v64": "N64 ROM", ".nds": "Nintendo DS ROM", ".3ds": "3DS ROM", ".cia": "3DS package",
    ".gen": "Genesis ROM",
    ".sms": "Master System ROM", ".gg": "Game Gear ROM", ".pce": "PC Engine ROM",
    ".xci": "Switch cartridge dump", ".nsp": "Switch package",
    # Disc images and console packages / executables
    ".iso": "disc image", ".cso": "compressed disc image", ".chd": "compressed disc image",
    ".bin": None, ".cue": "disc image track list", ".gcm": "GameCube disc image", ".rvz": "GameCube/Wii disc image",
    ".wbfs": "Wii disc image", ".wad": "Wii package", ".xex": "Xbox 360 executable",
    ".xbe": "Xbox executable", ".god": "Xbox 360 package", ".pkg": "PlayStation package",
    ".pbp": "PSP package", ".elf": None, ".self": "PlayStation executable", ".sprx": "PlayStation module",
    ".prx": "PlayStation module",
    # Saves and emulator states
    ".sav": "save file", ".srm": "save file", ".mcr": "memory card", ".ps2": "memory card",
    ".state": "emulator save state", ".ss0": "emulator save state", ".dsv": "save file",
}
# ".bin" and ".elf" are too common to block by name; they're caught by the binary size limit instead.
BLOCKED_EXTENSIONS = {k: v for k, v in BLOCKED_EXTENSIONS.items() if v}
ALLOWLIST_FILE = ".publish-allowlist"
# -------------------------------------------------------------------------------------


def git(*args: str) -> str:
    return subprocess.run(["git", *args], capture_output=True, text=True, check=True).stdout


def load_allowlist(root: Path) -> list[str]:
    f = root / ALLOWLIST_FILE
    if not f.exists():
        return []
    patterns = []
    for line in f.read_text(encoding="utf-8").splitlines():
        line = line.split("#", 1)[0].strip()
        if line:
            patterns.append(line)
    return patterns


def looks_binary(path: Path) -> bool:
    """A file is treated as binary if its first 8 KB contain a zero byte (the same test git uses)."""
    try:
        with path.open("rb") as fh:
            return b"\0" in fh.read(8192)
    except OSError:
        return False


def main() -> int:
    staged = "--staged" in sys.argv
    try:
        root = Path(git("rev-parse", "--show-toplevel").strip())
        if staged:
            # Added, copied, modified or renamed files that are staged for the next commit.
            files = git("diff", "--cached", "--name-only", "--diff-filter=ACMR").splitlines()
        else:
            files = git("ls-files").splitlines()
    except (subprocess.CalledProcessError, FileNotFoundError):
        print("This needs to run inside a git repo, with git installed.")
        return 1

    allow = load_allowlist(root)
    problems = []
    for name in files:
        if any(fnmatch.fnmatch(name, pat) for pat in allow):
            continue
        p = root / name
        if not p.is_file():
            continue
        kind = BLOCKED_EXTENSIONS.get(p.suffix.lower())
        if kind:
            problems.append(f"{name}: looks like a {kind}. Game data and saves can't go in a public repo.")
            continue
        size_mb = p.stat().st_size / (1024 * 1024)
        if size_mb > MAX_BINARY_MB and looks_binary(p):
            problems.append(f"{name}: a {size_mb:.1f} MB binary file (limit {MAX_BINARY_MB} MB).")

    if problems:
        print("Not safe to publish:")
        for pr in problems:
            print(f"  - {pr}")
        print(
            "\nHow to fix: remove the file from git (git rm --cached <file>) and add it to .gitignore, so users "
            "supply their own copy. If it's yours and safe to share (e.g. a test file you made), add its path to "
            f"{ALLOWLIST_FILE} with a comment saying why."
        )
        return 1
    print(f"Safe to publish: checked {len(files)} file(s).")
    return 0


if __name__ == "__main__":
    sys.exit(main())
