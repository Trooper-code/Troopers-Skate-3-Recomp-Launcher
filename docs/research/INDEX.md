# Research index

One line per topic, pointing to where it lives. Look things up here before searching the docs or the code, and add a
line for every new research topic or section. Check it with `python scripts/check-research.py`.

Most research lives in [`research.md`](../../research.md) at the repo root (kept there so existing links still work).

| Topic | Where | Status |
|-------|-------|--------|
| EB3 `.big` archive layout (header, index, mode bytes, names, hash) | [research.md § 1](../../research.md#1-the-games-archives-eb3-big-files) | confirmed (Python checker, all 51 retail archives, 2026-11-12) |
| RefPack and chunkref compression | [research.md § Compression](../../research.md#compression) | confirmed (16,262 of 16,262 entries decoded, Python) |
| Archive gotchas (don't sniff compression; which files are compressed) | [research.md § Gotchas](../../research.md#gotchas) | confirmed |
| Corrections to the M6 brief | [research.md § Corrections](../../research.md#corrections-to-the-m6-brief-docstasksmodding-toolsmd) | confirmed |
| Rebuilding an archive byte for byte | [research.md § 2](../../research.md#2-rebuilding-an-archive) | confirmed (Python prototype, 50 archives' tables; 3 full archives SHA-256 identical) |
| In-game archive tests (what the game accepts) | [research.md § 3](../../research.md#3-in-game-tests-what-the-game-accepts) | partly: crashes confirmed; cause unverified; tests 5 and 6 not run |
| How other projects change game files (Recompiled+, Jared's fork, SkateGM) | [research.md § 4](../../research.md#4-how-other-projects-change-game-files) | unverified (read from their source) |
| 5C8 build of the recomp | [research.md § 5](../../research.md#5-the-5c8-build-of-the-recomp) | unverified (read from source) |
| Online play, Blaze server, `net_host_overrides` | [research.md § 6](../../research.md#6-online-play-and-multiplayer) | unverified; more on unmerged branch `research/online-blaze` |
| Licences of related projects | [research.md § 7](../../research.md#7-sources-and-licences) | confirmed (licence files checked) |
| Open research questions | [research.md § 8](../../research.md#8-open-questions) | open |
| `mod.json` format 1 | [mod-format.md](../mod-format.md) | confirmed (shipped in Modding M1) |
| Modding milestone briefs M1–M7 (incl. M5 VFS log-line facts) | [tasks/modding-tools.md](../tasks/modding-tools.md) | M1–M4 done; M5 facts unverified |
| Where each recomp fact comes from (cvars, key binds, paths) | [AGENTS.md § The hard rules](../../AGENTS.md#the-hard-rules) | confirmed (rule 3 lists the source files) |
