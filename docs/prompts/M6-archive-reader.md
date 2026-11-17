# M6-archive-reader: EB3/BIG4 archive reader and archive browser (read-only)

## Read first
- `AGENTS.md` (standing rules, including C# 5 only, no new references, and your agent-specific section)
- `docs/HANDOFF.md`, sections "How it works now" and "Tool versions"
- `docs/MILESTONES.md`, row M6
- `docs/research/INDEX.md`, then `research.md` §§ 1 and 2 (the confirmed archive layout; it wins over the brief)
- `docs/tasks/modding-tools.md`, section "M6 · EB3 archive reader and archive browser (read-only)"
- `docs/DECISIONS.md` D3 (our own C#, credit the research) and D4 (why M6 is next)
- Code you'll touch: `src/ModdingServices.cs`, `src/LauncherPagesModding.cs`, and for house style the Mods page in
  `src/LauncherPagesV31.cs`; helpers `FileUtil`, `MiniJson` in `src/GameServices.cs`

## Goal
The launcher can open every `.big` archive in a retail game folder, list its files, and extract any of them with
the exact original bytes (decompressed), proven by a harness that checks every archive.

## Branch
`feature/modding-m6-archive-reader`, branched from tag `v3.1.2` (or `main` if it has moved on; say which).

## Known facts
All integers are big-endian. Source for everything below: `research.md` § 1, from a read-only Python checker run on
Josh's retail install (`E:\Skate3Recomp-Windows\game`, Xbox 360 Skate 3, Title Update 3) on 2026-11-12. The C#
reader must reproduce these numbers exactly.

| Fact | Status | Evidence |
|------|--------|----------|
| 51 `.big` files, 4.89 GB: 50 EB3, 1 BIG4 (`data/audio/music/overlays.big`) | confirmed | research.md § 1 table |
| 45,198 entries in the EB3 archives; 0 hash, sort or range errors | confirmed | research.md § 1 table |
| 16,262 compressed entries: 6,431 RefPack + 9,831 chunkref; all decode to their exact expanded size | confirmed | research.md § 1 table |
| Header 48 bytes: `EB` @0, version 3 (u16) @2, count @4, flags u16 @8 (always `0x0010`), block shift u8 @10 (4 or 6), names offset @12, names+folders size @16, name width u8 @20, folder width u8 @21, folder count u16 @22, 0 @24, total size @28 = file length, zeros @32..47 | confirmed | research.md § 1 "Header" |
| Index: 16-byte records from 48: block, stored size, length, hash. Offset = block << shift. Stored size 0 → raw and `length` is its size; else `length` is the expanded size | confirmed | research.md § 1 "Index" |
| Storage-mode bytes: one per file at `48 + align16(count × 16)`: 0 raw, 1 RefPack, 2 chunkref. **Decode by this byte, never by sniffing**: two raw files in `miscload.big` start with RefPack-like bytes | confirmed | research.md § 1 "Storage-mode bytes", "Gotchas" |
| Names: per file, u16 folder index + NUL-terminated name, `name width` bytes each; folder table at the next 16-byte boundary; full path `folder/name` | confirmed | research.md § 1 "Names" |
| Path hash: djb2 (start 5381, `h = h*33 + byte`, 32-bit) over the full path with `/`, case as stored; records sorted ascending, unique | confirmed | research.md § 1 "Path hash" |
| RefPack: header `10 FB` under mask `0x3EFF`; no trailing bytes after the stop code in retail | confirmed | research.md § "Compression" |
| chunkref: `chunkref` + 5×u32 (version 2, expanded size, piece size `0x20000`, piece count, alignment 16). Pieces **interleaved**: cursor = 28; per piece, data at `align(cursor + 8, alignment)`, the 8 bytes before it = u32 stored size + u32 method (2 RefPack, 4 raw), next cursor = end of data | confirmed | research.md § "Compression"; the brief's older "table at offset 32" layout is **wrong** for multi-piece entries |
| One `parkassets.big` chunkref entry has leftover bytes in the padding before its second piece; readers skip padding | confirmed | research.md § 2 last paragraphs |
| BIG4/BIGF: magic, then at offset 8 `count` and `end` (u32 each); records from 16: offset u32, size u32, NUL-terminated name | unverified | from the brief; not re-checked in research.md (docs/tasks/modding-tools.md M6) |
| Zero-length files share the next file's offset (two in `miscload.big`) | confirmed | research.md § 2 |

## Tasks
1. **Harness first** (test-driven). Write `tests/ArchiveCheck/ArchiveCheck.cs` plus a `tests/ArchiveCheck/build.cmd`
   that compiles it together with `src\*.cs` (except the `Main` entry point clash: give the harness its own entry
   class and pass `/main:`). Usage: `ArchiveCheck.exe "<game folder>" [--hash-out <file>]`. It opens **every**
   `.big` under the folder **read-only**, checks every hash, sort order, range, mode byte and decoded size, and
   prints the counts in the same shape as the research.md § 1 table. `--hash-out` writes `archive<TAB>path<TAB>sha256`
   of every decoded entry. This folder is committed but **not** part of `src\build.cmd`.
2. **`EbArchive`** in `src/ModdingServices.cs`: `Open(path)` → entries (path, offset, stored size, expanded size,
   mode, hash); `ReadEntry(entry)` → decoded bytes, and a `CopyEntryTo(entry, Stream)` that doesn't need the whole
   archive in memory. Use `FileStream` with seeks; never `File.ReadAllBytes` on an archive (some are 800 MB+).
   Clear plain-English errors for anything that doesn't match the layout (wrong magic, size mismatch, unknown mode).
3. **RefPack decoder** written from a **public description** (cite the page in the report and in a code comment).
   **chunkref** decoder using the interleaved layout above. **BIG4/BIGF** reader.
4. Run the harness on Josh's game folder (read-only). Then cross-check with an **independent** extractor: run
   SkateGM's MIT reader (`the-schwilliam/SkateGM`, `exporter/tools/vendor/skate3_ui/big.py` + `refpack.py`) on at
   least `data/big/fetexture.big`, `data/big/db.big` and one chunkref-heavy archive (e.g.
   `data/content/createacharacter.big`), and compare SHA-256 of every entry with `--hash-out`. Run it from a
   scratch folder outside the repo; don't commit SkateGM's code.
5. **Archive browser card** on the Modding tools page (`src/LauncherPagesModding.cs`): a drop-down of the `.big`
   files under `game\`, a searchable list (path, size, compressed?), and **Extract** for the selected files to a
   folder the user picks, keeping inner paths (check every output path with `FileUtil.Inside`). Opening and
   extracting run off the UI thread with progress, per AGENTS.md house conventions.
6. **Docs:** README Modding tools section (the new card), the Credits line from the brief, a line in the next
   release's notes (`.github/release-notes/v3.2.0.md`, create it), and stamp the research.md § 1 facts your harness
   confirms: `[confirmed @ feature/modding-m6-archive-reader | csc=4.0.30319, game=TU3]`. Fix anything it
   disproves, labeled **wrong**, with the evidence. Confirm or correct the BIG4 row and add it to research.md § 1.

## Tools
- superpowers `test-driven-development`: the harness and its expected counts exist before the reader is done.
- superpowers `systematic-debugging`: if any count or hash differs from research.md, find the root cause before
  changing the reader. A mismatch means the reader is wrong until proven otherwise.
- superpowers `verification-before-completion`: paste the harness's real output into the report.
- Not REA (no binary reverse engineering in this task) and not `/swarm` (one linear piece of work).

## Task-specific constraints
- Read-only on the game folder: open archives with `FileAccess.Read, FileShare.Read`. Nothing in this milestone
  writes an archive.
- No code copied from 5C8, Recompiled+ or SkateGM (D3). Reading their code for facts is fine.
- Don't change normal PLAY, the Mods page, or `mods\skate2`.

## Stop and report if
- The AGENTS.md stop conditions are met, or:
- The harness's totals differ from the research.md § 1 table and you can't explain why after 3 attempts.
- The SkateGM cross-check disagrees on any entry's SHA-256.
- You need an assembly reference beyond the four in `build.cmd` (e.g. for zlib: retail never uses it).

## Acceptance checks
- `src\build.cmd` builds with no errors, and the CI build on the PR is green.
- `tests\ArchiveCheck\build.cmd` builds; `ArchiveCheck.exe "E:\Skate3Recomp-Windows\game"` reports 51 archives
  (50 EB3 + 1 BIG4), 45,198 EB3 entries, 16,262 compressed entries decoded (6,431 RefPack, 9,831 chunkref), 0 errors.
  Its full output is in the report.
- SHA-256 of every entry in the three cross-checked archives matches SkateGM's reader.
- In the launcher: the browser lists `data/big/db.big`, and an extracted file's SHA-256 matches the harness's
  `--hash-out` line for it.
- Every finding in the report cites its evidence.

## Report
Write `docs/reports/M6-archive-reader.md` using `docs/reports/TEMPLATE.md`, check it with
`python scripts/check-report.py docs/reports/M6-archive-reader.md`, commit it on the branch, push the branch, and
reply with its path plus a 3-line summary. List anything that needs Josh to look at the UI under
"Needs testing on Windows".
