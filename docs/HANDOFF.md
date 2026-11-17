# Trooper's Skate 3 Recomp Launcher handoff

Portable C# 5 WinForms launcher, settings editor, mod loader and modding toolbox for the Skate 3 Recomp PC port.
Josh tests on Windows; Antigravity builds; Claude coordinates (writes prompts, reviews, merges, tags).

## Setup
| What | Where |
|------|-------|
| Repo | `https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher` |
| Coding agent | Google Antigravity (rules: `AGENTS.md`, incl. its Antigravity section) |
| Game install / test data (never committed) | `E:\Skate3Recomp-Windows` (retail X360 Skate 3, Title Update 3; `game\` is read-only to us) |
| Build | `src\build.cmd` → `bin\Trooper's Skate 3 Recomp Launcher.exe`; CI: `.github/workflows/build.yml` |
| Release notes | `.github/release-notes/vX.Y.Z.md` (one file per version; this repo has no changelog file) |
| Research | `research.md` (repo root), indexed in `docs/research/INDEX.md` |

## Tool versions
| Tool | Version |
|------|---------|
| csc | 4.0.30319 (.NET Framework, C# 5) |
| dotnet-framework-target | 4.5 |
| launcher | 3.1.2 |
| game | TU3 (Xbox 360 retail, Title Update 3) |
| recomp | official mchughalex/skate3recomp; exact version not recorded yet (Josh) |

## How it works now
- `v3.1.2` (main, `b57c321`) is the latest release: settings editor, profiles, saves, controller, logs, mod loader,
  benchmark, and the hidden-by-default Modding tools page (M1–M4: new mod, check/package, create from changes,
  test launch).
- The EB3 archive format and rebuild rules are confirmed on all retail archives with a Python prototype
  (`research.md` §§ 1–2). No archive code exists in the launcher yet.
- Key decisions D1–D4 are in `docs/DECISIONS.md` (rebuild archives into `mods\.active-game`, folder-layout
  declarations, own C#, reader first).

## Where it stops
In-game archive tests (`research.md` § 3): every rebuilt `fetexture.big` that grew ~6× crashes as the main menu
opens; the byte-identical control works. Tests 5 (size only) and 6 (real recompression) aren't run yet.

## In progress
- Antigravity: `docs/prompts/M6-archive-reader.md` on branch `feature/modding-m6-archive-reader`.
  Report (when done): `docs/reports/M6-archive-reader.md` on that branch.
- Josh: in-game tests 5 and 6.
- Unreviewed: branch `research/online-blaze` (online research docs, 2026-11-14).

## Rules
- C# 5 only, four framework references only, never guess recomp facts (AGENTS.md hard rules).
- Never write into `game\`; never touch `mods\skate2`; never commit game files or code from unlicensed repos.
- CRLF for `.cs`/`.cmd`, LF for `.md` (`.gitattributes`).
- Antigravity never merges or tags. Claude merges and tags after Josh tests. Releases follow AGENTS.md "Releases".
- Install the commit guard once: `cp scripts/hooks/pre-commit .git/hooks/pre-commit`.

## First thing to do
1. Read `docs/MILESTONES.md` (Next) and `research.md` §§ 1–3.
2. Read the M6 report from its branch: `git fetch origin feature/modding-m6-archive-reader` then
   `git show origin/feature/modding-m6-archive-reader:docs/reports/M6-archive-reader.md`.
3. Run `python scripts/check-report.py` on it; check its tool versions against the table above.
4. Re-run what you can (mcs build per AGENTS.md; the harness needs the game, so check its pasted output against
   research.md § 1 totals). Spot-check the RefPack/chunkref code against the documented layout.
5. List bugs, then gaps, then nitpicks.
6. Give Josh test steps. Merge and tag if it works; follow-up prompt if not.

## Lessons
- The brief's chunkref layout was wrong for multi-piece entries; research.md wins over the brief.
- Don't detect compression by sniffing bytes; use the storage-mode byte.
- "Raw files are safe because the game reads raw files" was wrong for `fetexture.big`: test archive changes in game.
- AGENTS.md said `mods\skate2` wasn't reserved yet; the code already skipped it. Check docs against code.

[Paste the coding agent's latest summary here, or leave this line if its report is in docs/reports/]

---
