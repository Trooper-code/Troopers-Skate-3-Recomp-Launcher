# Task: Modding tools for Trooper's Skate 3 Recomp Launcher

**For:** an AI coding agent (written for Google Antigravity, but any agent works) or a human contributor.
**Read first:** [`AGENTS.md`](../../AGENTS.md). Every rule there applies, especially **C# 5 only**, **no new assembly references**, and **never guess recomp facts**.
**Owner / reviewer:** Josh (Trooper-code). Claude reviews each pull request before it's merged.

---

## Goal

Give Skate 3 modders a toolkit inside the launcher that makes it fast to **make, test, package and share** mods, and lays the groundwork for **texture packs** (mods that change single files *inside* the game's `.big` archives).

The work is split into **milestones**. Each milestone is **one branch and one pull request**. Don't start a milestone until the previous one is merged, and don't mix milestones in one PR.

| Milestone | Branch | Depends on game archives? |
|---|---|---|
| M1 · mod.json v1 and the Modding tools page | `feature/modding-m1-page` | No |
| M2 · Mod packager and validator | `feature/modding-m2-packager` | No |
| M3 · Create mod from changes | `feature/modding-m3-from-changes` | No |
| M4 · Test this mod (quick launch) | `feature/modding-m4-test-launch` | No |
| M5 · File access log | `feature/modding-m5-file-log` | No |
| M6 · EB3 archive reader and archive browser (read-only) | `feature/modding-m6-archive-reader` | Yes |
| M7 · Archive writer and entry-level mods (texture packs) | **Not yet.** Needs a design review with Josh and Claude first | Yes |

## Ground rules for this task

- **Work only on the milestone's branch.** Never commit to `main`. Open a PR into `main` when the milestone is done.
- **Read before you write.** Before changing a file, read the parts you'll touch: `src/GameServices.cs` (`Mods`, `ModInfo`, `GameLaunch`, `Benchmarks`, `FileUtil`, `MiniJson`), `src/LauncherPagesV31.cs` (the Mods and Benchmark pages show the house style for pages), `src/LauncherPages.cs` (`BuildToolPages`, `MkButton`, `MkToggle`, `Row`, `Flash`).
- **Edit, don't rewrite.** These files are large. Change only the lines you need. Never resend a whole file with sections removed. Check your diff for deleted lines you didn't mean to delete.
- **Put new code in new files**: services in `src/ModdingServices.cs`, UI in `src/LauncherPagesModding.cs` (`partial class MainForm`). `build.cmd` compiles every `.cs` automatically.
- **Players come first.** The Modding tools page is **hidden unless turned on** (M1). Nothing in this task may change what a normal player sees or how PLAY works, except where a milestone says so.
- **Never write into `game\`**, never delete or change the **`mods\skate2`** folder (Skate 3 Recompiled+ keeps New San Van data there), and never redistribute game files.
- **No code from other repositories.** The archive format facts in M6 come from studying the Skate 3 Recompiled+ fork (`dougieprishpreed/skate3recomp-plus`, `tools/world_archive.py`, `tools/inspect_sfil.py`). That repo has **no license**: use the facts written here, write your own C#, and **don't copy its code**.
- **Every PR description must include:** what changed; where each recomp fact came from (file and line); what you tested and how; and a **"Needs testing on Windows"** list. The CI build on the PR must be green.

---

## M1 · mod.json v1 and the Modding tools page

### 1a. Define `mod.json` format 1

Write the spec in a new file **`docs/mod-format.md`** and make the loader understand it. The format is **additive**: everything that works today keeps working, and every field is optional.

```json
{
  "format": 1,
  "name": "Retro Shoes",
  "version": "1.2.0",
  "author": "skatefan",
  "description": "Brings back the classic shoe colours.",
  "preview": "preview.png",
  "targets": {
    "recomp": "any",
    "min_version": "2.0.2"
  },
  "requires": ["Another Mod Folder Name"],
  "conflicts": ["Some Other Mod"]
}
```

| Field | Meaning |
|---|---|
| `format` | Format version. Missing = 0 (a v3.1 mod). Newer than the launcher understands → show "made for a newer launcher" and still load it. |
| `name`, `version`, `author`, `description` | As today. |
| `preview` | Image next to `mod.json`, shown on the mod's row. |
| `targets.recomp` | `"official"`, `"plus"` (Skate 3 Recompiled+) or `"any"` (default). |
| `targets.min_version` | Lowest recomp version the mod works with. Compare with `Updates.Compare` against `RecompVersion.Current(...)`. |
| `requires` / `conflicts` | Other mods by folder name or `name`. |

Loader changes (`Mods.Read` / `ModInfo` in `src/GameServices.cs`): read the new fields with `MiniJson`. On the Mods page, show a ⚠ line on the mod's row when `targets` doesn't match, a required mod is missing or disabled, or a conflicting mod is enabled. These are **warnings, not blocks**: PLAY still works.

**Detecting Recompiled+:** treat the install as Recompiled+ when a `mods\skate2` folder exists next to `skate3.exe` **or** `settings.toml` contains the key `skate3_skate2_enabled`. Put this in one helper (e.g. `RecompFlavor.Detect(gameExe)`) so later work can reuse it.

### 1b. The Modding tools page

- Add a toggle **"Show modding tools"** to the Launcher page (`launcher.ini` key `modding_tools`, default **off**), with the help text "Adds a Modding tools page for making and testing mods."
- When on, add a page **Modding tools** to the TOOLS section (glyph `"\uE90F"`, Repair) with the blurb "Make, test and package mods." Turning the toggle on or off can show "Restart the launcher to show or hide the page." rather than rebuilding the sidebar live.
- M1's page only needs one card, **"Start a new mod"**: a name box and a **Create mod folder** button. It creates `mods\<name>\` with a `mod.json` (format 1, name filled in, version `0.1.0`), an empty `data\` folder and a short `README.txt` explaining the layout, then opens the folder. It refuses names that already exist, `skate2`, or names starting with `.` or `_`.

**Acceptance:** a v3.1 mod with no `mod.json` still loads exactly as before; a mod with `targets.recomp: "plus"` shows a warning on an official install; the page is invisible until the toggle is on.

---

## M2 · Mod packager and validator

A card on the Modding tools page: pick a mod (drop-down of installed mods), press **Check mod**, see the results, and **Package as .zip** when it's ready.

**Checks** (in a `ModValidator` class, so the CLI in a later milestone can reuse it):

| Severity | Check |
|---|---|
| Error | No usable files (same rule the loader uses). |
| Error | Contains `default.xex`, `default.xexp`, `data/webkit/EAWebkit.xex` or `.xexp` (`GameFiles.Protected`). |
| Error | A path escapes the mod folder, or contains characters Windows can't store. |
| Warning | **A whole original game archive.** A file whose path matches a file in `game\` and is larger than 20 MB. Text: "This is a whole game archive (N MB). Sharing it means sharing EA's files, and it makes the mod huge. Once texture packs are supported, ship only the files you changed." |
| Warning | **Identical to the original.** The file's SHA-256 equals the game file it replaces, so it does nothing. Offer to remove it. Use `FileUtil.Sha256`, hashing off the UI thread with progress. |
| Warning | **PS3 file types.** Any `.psg` file, or other formats known to be PS3-only. Text: "This looks like a PS3 mod. The recomp needs Xbox 360 files." |
| Warning | New files that don't exist in `game\` (allowed, but probably a wrong path). List them. |
| Info | No `mod.json`, or `mod.json` missing `name`/`version`: offer **Add mod.json**. |
| Info | No preview image. |

**Package as .zip** writes `<Name>-<version>.zip` to the user's Desktop (like the bug report zip), containing **one top-level folder** named after the mod with `mod.json`, the preview, the readme and the files. Use `System.IO.Compression.ZipArchive` (already referenced). Refuse to package while there are errors.

**Also:** dropping a `.zip` or a folder onto the **Mods** page installs it (same code as **Install from .zip…**). This is the one change to the normal Mods page in this task.

**Acceptance:** validating a mod containing a copy of `default.xex` shows an error; a mod containing an unchanged copy of a game file shows the "identical" warning; the zip opens in Explorer with one folder inside and installs again with **Install from .zip…**.

---

## M3 · Create mod from changes

For modders who edit a copy of the game folder directly.

- Card **"Create a mod from changed files"**: pick a **work folder**, a full copy of `game\` that the modder has edited, and a new mod name.
- Compare every file in the work folder against `game\` (the folder next to `skate3.exe`, `FileUtil.GameRoot(gameExe)`): first by size, then SHA-256 for same-size files. Run it in the background with progress and **Cancel**.
- Show the result: N changed, N new, N unchanged (skipped). The modder ticks which changed/new files to include.
- **Create mod** copies only the ticked files into `mods\<name>\` with the same relative paths, writes a format 1 `mod.json`, and runs the M2 validator on the result.
- Never modify the work folder or `game\`. Refuse if the work folder **is** `game\`, or is inside `mods\`.

**Acceptance:** with a work folder where one file was changed and one added, the new mod contains exactly those two files at the right paths.

---

## M4 · Test this mod (quick launch)

A **Test** button next to each mod on the Modding tools page (not on the normal Mods page): start the game with **only that mod** (plus mods it `requires`) and go **straight to gameplay**.

- **One mod only:** add an overload such as `Mods.Prepare(string gameExe, IList<ModInfo> only)` that builds `mods\.active-game` from just the given mods. Don't change what normal PLAY loads. Keep the existing safety checks (`default.xex`, Title Update, NTFS, hard-link errors).
- **Straight to gameplay:** reuse the demo-path automation the benchmark uses (`Benchmarks.Run` in `src/GameServices.cs`). Temporarily set in `settings.toml`: `skate3_demo_path = true`, `skate3_demo_path_signed_in = false` (signed-out boot, so the career save can't be touched) and `skate3_intro_movie_skip = true`. Facts: `skate3recomp/src/skate3_demo_path.cpp` lines 28–47.
- **Restore settings:** move the benchmark's backup/restore (`BackupConfig`, `RestoreConfig`, `RecoverConfig`, the `settings.toml.benchmark-backup` file and the "did not exist" marker) into a shared helper, and use it for both. After the game exits (the modder closes it normally, so don't kill it), put `settings.toml` back exactly. `RecoverConfig` must still run at startup.
- Show "Testing <mod>…" while it runs, and when the game exits: "Test session finished. Your settings were restored."

**Acceptance:** after a test session `settings.toml` is byte-for-byte what it was; a normal PLAY afterwards loads the user's usual mod set; the career save's modified time doesn't change.

---

## M5 · File access log

Shows which game files the game opened, or tried to open and couldn't, during a session. This is how modders find out which file controls what.

- **Facts:** the recomp's file system logs a **warning** for every file it can't find: `VFS: entry not found for '<path>' on device '<device>'` and `VFS: entry not found for '<path>' (via symlink '<link>') on device '<device>'` (`rexglue-skate3/src/filesystem/virtual_file_system.cpp` lines 302–305). Successful opens are logged only at **trace** level, as `VFS resolved '<path>' on device '<device>' -> '<target>'` (lines 294–297). The log level is the global setting `log_level` (`trace`, `debug`, `info`, …; `rexglue-skate3/src/core/logging.cpp` line 35). There's no per-category level. Verify all of this against the current source before building on it.
- **Missing files:** always available. Parse the newest session's log (`LogTools.Files`, `LogTools.ReadAll`) for the "entry not found" lines and list the unique paths with how often each was requested.
- **Opened files:** add a **"Log every file the game opens"** checkbox to the M4 test launch. When ticked, also set `log_level = 'trace'` for that session only (restored with the rest of the settings), then list the unique resolved paths. Warn in the UI that trace logging is slow and makes big logs, so it's for test sessions only. Find out from the source whether the 5 MB log rotation (`log_max_file_size_mb`) loses lines, and read rotated files (`skate3_NNN.1.log`, …) too.
- Card UI: a search box, a **Missing / Opened** switch, a list (reuse `LogView` or a simple owner-drawn list), and **Copy list**.

**Acceptance:** after a normal session, the missing-files list shows paths like `d:\fileserver.ini`; after a test session with the checkbox ticked, the opened-files list includes `default.xex`, and `log_level` is back to what it was.

---

## M6 · EB3 archive reader and archive browser (read-only)

The foundation for texture packs. **Read-only**: nothing in this milestone writes an archive or changes game files.

### Format facts (all integers big-endian)

These were checked against the retail archives by the Recompiled+ fork's tools. **Verify each one against real files** (Josh's game folder) with a test harness before building UI on it.

**EB3 header**, 48 bytes from offset 0:

| Offset | Size | Field |
|---|---|---|
| 0 | 4 | Magic `45 42 00 03` (`EB\0\3`) |
| 4 | 4 | `count`: number of files |
| 8 | 4 | `family`: `0x00100400` (world archives) or `0x00100600` (db/audio archives). Byte 1 is the **alignment shift**: `shift = (family >> 8) & 0xFF`, i.e. 4 (16-byte blocks) or 6 (64-byte blocks) |
| 12 | 4 | `names_at`: offset of the names table |
| 16 | 4 | `names_size`: size of names table + directory table |
| 20 | 4 | `metadata`: `name_width = metadata >> 24`, `dir_width = (metadata >> 16) & 0xFF`, `dir_count = metadata & 0xFFFF` |
| 24 | 4 | reserved, must be 0 |
| 28 | 4 | total archive size; must equal the file length |
| 32 | 16 | (rest of the 48-byte header) |

Reject anything else (other families, non-zero reserved, size mismatch) with a clear error.

**File index**: `count` records of 16 bytes starting at offset 48. Record *i*:

| Offset in record | Field |
|---|---|
| 0 | `block`: file offset = `block << shift` |
| 4 | `stored_compressed`: non-zero → the file is compressed and this is its stored size |
| 8 | `length`: the stored size if `stored_compressed == 0`, otherwise the **expanded** size |
| 12 | `hash`: path hash (below) |

Stored size = `stored_compressed != 0 ? stored_compressed : length`. Validate `offset >= names_at + names_size` and `offset + stored <= file length`.

**Storage-mode bytes**: one byte per file at `align16(48 + count * 16)`: 0 = raw, 1 = RefPack, 2–4 = chunked, anything else = no data. Retail archives write zeros here. (This is how the game reads it, per the fork's notes; the reader can use it as a cross-check, and the writer in M7 must write it.)

**Names table** at `names_at`: `count` slots of `name_width` bytes. Each slot: a 2-byte directory index, then the file name, NUL-terminated.

**Directory table** at `align16(names_at + count * name_width)`: `dir_count` slots of `dir_width` bytes, each a NUL-terminated directory path.

**Full path** = `directory + "/" + file name`.

**Path hash** (djb2): start at 5381; for each byte of the ASCII path, `hash = (hash * 33 + byte) mod 2^32`. Before hashing, turn `\` into `/` and drop a leading `./`. Records are **sorted by hash, ascending and unique**. Check this: a mismatch means the reader is wrong.

**BIG4 / BIGF** (older format, used by Skate 2): magic `BIG4` or `BIGF`; at offset 8, `count` and `end` (u32 each); records from offset 16: `offset` (u32), `size` (u32), NUL-terminated name. Support reading it, since Recompiled+ players have Skate 2 archives.

**Compressed entries:**
- **RefPack** (EA's LZ-style compression) starts with bytes `10 FB`. It's publicly documented (search "RefPack" / "QFS compression"). Implement the decoder from a public description, and cite it in the PR.
- **chunkref**: starts with the ASCII `chunkref`, then five u32: `version` (2), `expanded size`, `block size` (`0x20000`), `chunk count`, `record width` (16). Then `chunk count` records of 16 bytes from offset 32; in each record, the u32 at +8 is the chunk's stored size and the u32 at +12 its codec (2 = RefPack). Chunk data starts at `32 + 16 * count`, back to back; each chunk is RefPack-compressed and the decoded chunks joined must equal `expanded size`.

### What to build

- **`EbArchive`** (in `src/ModdingServices.cs`): `Open(path)` → list of entries (path, offset, stored size, expanded size, compressed?, hash); `ReadEntry(entry)` → bytes, decompressed when needed. Use `FileStream` with seeks, never `File.ReadAllBytes` on a whole archive (some are 800 MB+).
- **A test harness** (not committed to `src/`; describe it in the PR) that opens **every** `.big` in a real game folder, checks every hash, the sort order, every range, and that every compressed entry decodes to its expanded size. Report the counts.
- **Archive browser card** on the Modding tools page: pick an archive from `game\` (drop-down), see its files in a searchable list (path, size, compressed), and **Extract** selected files to a folder the user picks, keeping their paths.
- **Credit** in `README.md` (Credits section): "Archive format research: the EB3/BIG4 layout, path hash and RefPack/chunkref decoding used by the launcher's archive tools are based on the format notes in [Skate 3 Recompiled+](https://github.com/dougieprishpreed/skate3recomp-plus) by dougieprishpreed, itself built on [Skate 3 Recomp](https://github.com/mchughalex/skate3recomp) by mchughalex. The launcher's code is its own C# implementation; no code from either project is included."

**Acceptance:** the harness opens every retail archive with zero hash or range errors and decodes every compressed entry; the browser lists `data/big/db.big` and extracts a file whose bytes match a second, independent extraction.

---

## M7 · Archive writer and entry-level mods (texture packs). Not yet

Don't start this without a written design that Josh and Claude have reviewed. Open questions to answer in that design:

- How a mod declares "replace this file inside this archive" (folder layout like `mods\<mod>\archives\data\content\createacharacter.big\<inner path>`, or a manifest in `mod.json`).
- Rebuilding the archive in `mods\.active-game` (never `game\`), with the storage-mode bytes, sorted hashes and alignment, and caching it so it's only rebuilt when mods change.
- Byte-for-byte round-trip test: reading and rewriting an unmodified archive must reproduce the original, or the design must explain why not.
- Base-change detection for inner files (the mod records the hash of each original inner file).

---

## Out of scope for this task

- Texture decoding/encoding and previews (Xbox 360 texture formats need research first).
- A separate command-line exe. Put shared logic in `ModdingServices.cs` so a CLI can be added later without rewriting it.
- Anything that changes the game's code, the recomp, or `game\`.
- Networking or multiplayer.
