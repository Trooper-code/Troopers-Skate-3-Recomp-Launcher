# Milestones

Status board for Trooper's Skate 3 Recomp Launcher. Full briefs for the modding milestones (M1–M7) are in
[`docs/tasks/modding-tools.md`](tasks/modding-tools.md); this file tracks status and what's next.

## Next
1. **Antigravity:** M6 archive reader and browser. Prompt: `docs/prompts/M6-archive-reader.md`, branch
   `feature/modding-m6-archive-reader` from `v3.1.2`.
2. **Josh (in parallel, when you have time):** run in-game tests 5 and 6 from
   [research.md § 3](../research.md#3-in-game-tests-what-the-game-accepts) and report crash / works for each.
   They decide how the M7 writer encodes replaced files (D1).
3. **Claude:** review `research/online-blaze` (unmerged research docs) once Josh says whether he wants it on `main`.

## Milestones
| # | Milestone | Status | Tag / PR |
|---|-----------|--------|----------|
| — | v3.0.0 first public release | done | `v3.0.0` |
| — | v3.1.0 mod loader and extras | done | `v3.1.0` |
| — | v3.1.1 portable installs, known-issue warnings | done | `v3.1.1` |
| M1 | Modding: `mod.json` format 1 and Modding tools page | done | PR #9 |
| M2 | Modding: mod packager, validator, drag-and-drop install | done | PR #10 |
| M3 | Modding: create a mod from changed files | done | PR #11 |
| M4 | Modding: test a mod (quick launch) | done | PR #12 |
| — | v3.1.2 audit fixes (includes M1–M4) | done | `v3.1.2` |
| M6 | EB3/BIG4 archive reader and archive browser (read-only) | **next** (prompt written) | |
| M5 | File access log (missing / opened files from VFS log lines) | planned | |
| M7 | Archive writer and texture-pack mods | blocked: needs tests 5/6 and a design review (D1, D2) | |
| M8 | 5C8 build support (detection, version scheme, updates, intro-skip, new options) | planned; facts in [research.md § 5](../research.md#5-the-5c8-build-of-the-recomp) | |
| O1 | Online: self-hosted Blaze server + launcher server list | research only (branch `research/online-blaze`, unmerged) | |

**Done-criteria** for M5–M7 are the "Acceptance" lines in the brief. M8's are to be written with its prompt.

## Progress log
- 2026-11-17: Adopted into the coordinator workflow: added HANDOFF, MILESTONES, DECISIONS, research index, check
  scripts, pre-commit hook, `checks.yml`, and the M6 prompt.
- 2026-11-14: Online research pushed on `research/online-blaze` (ONLINE_RESEARCH.md, ONLINE_PLAN.md). Not reviewed.
- 2026-11-12: Archive format verified on all 51 retail archives; rebuild rules found; in-game crash tests run
  (`research.md` §§ 1–3, PR #13).
- 2026-11: v3.1.2 audit fixes (PR #14). Modding M1–M4 merged (PRs #9–#12).
