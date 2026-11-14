# Research notes

What we've learned about the game's files, other recomp projects and online play while building the launcher's modding tools. Everything here was checked against real files or real source code, and each section says how. Where something is still a guess, it says so.

Last updated: 2026-11-12.

- [1. The game's archives (EB3 `.big` files)](#1-the-games-archives-eb3-big-files)
- [2. Rebuilding an archive](#2-rebuilding-an-archive)
- [3. In-game tests: what the game accepts](#3-in-game-tests-what-the-game-accepts)
- [4. How other projects change game files](#4-how-other-projects-change-game-files)
- [5. The 5C8 build of the recomp](#5-the-5c8-build-of-the-recomp)
- [6. Online play and multiplayer](#6-online-play-and-multiplayer)
- [7. Sources and licences](#7-sources-and-licences)
- [8. Open questions](#8-open-questions)

---

## 1. The game's archives (EB3 `.big` files)

**How this was checked:** a read-only Python checker opened every `.big` in a retail Skate 3 install (Xbox 360, Title Update 3): 51 files, 4.89 GB. Nothing in `game\` was changed.

| What | Result |
|---|---|
| Archives | 50 EB3, 1 older BIG4 (`data/audio/music/overlays.big`) |
| File entries in the EB3 archives | 45,198 |
| Path hashes, sort order, offset and size ranges | 0 errors |
| Compressed entries decoded to their exact expected size | 16,262 of 16,262 (6,431 RefPack, 9,831 chunkref) |

### Layout (all integers big-endian)

**Header, 48 bytes**

| Offset | Size | Field |
|---|---|---|
| 0 | 2 | `EB` |
| 2 | 2 | version, always 3 |
| 4 | 4 | file count |
| 8 | 2 | flags: always `0x0010` in retail |
| 10 | 1 | block shift: 4 (16-byte blocks) in 33 archives, 6 (64-byte blocks) in 17 |
| 11 | 1 | 0 |
| 12 | 4 | offset of the names table |
| 16 | 4 | size of the names table plus the folder table |
| 20 | 1 | name record width |
| 21 | 1 | folder record width |
| 22 | 2 | folder count |
| 24 | 4 | 0 |
| 28 | 4 | total archive size (equals the file length) |
| 32 | 16 | zeros |

**Index:** one 16-byte record per file from offset 48: block number, stored (compressed) size, length, path hash. File offset = block number shifted left by the block shift. If the stored size is 0 the file is raw and `length` is its size; otherwise `length` is the size after decompressing.

**Storage-mode bytes:** one byte per file, starting at `48 + align16(count × 16)`. `0` raw, `1` RefPack, `2` chunkref. They're filled in on every retail archive and always agree with the stored-size field.

**Names:** one record per file at the names offset: a 2-byte folder number, then the file name ending in a zero byte. The folder table starts at the next 16-byte boundary after the names. Full path = `folder/name`.

**Path hash:** djb2. Start at 5381; for each byte of the full path (with `/`), `hash = hash × 33 + byte`, kept to 32 bits. Records are sorted by hash, smallest first, with no duplicates.

### Compression

- **RefPack** is EA's standard LZ compression. It starts with `10 FB` under the mask `0x3EFF`. No retail entry has extra bytes after the stop code.
- **chunkref** splits a file into 128 KB pieces. Header: the text `chunkref`, then five numbers: version (2), full size, piece size (`0x20000`), piece count, alignment (16). **The pieces are interleaved with their headers:** start at byte 28; for each piece, its data starts at `align(position + 8, alignment)`, the 8 bytes just before it hold the piece's stored size and method, and the next piece starts right after its data. Methods seen in retail: 2 (RefPack) and 4 (raw). SkateGM's reader also accepts 0 (raw) and 3 (zlib), which retail never uses.

### Gotchas

- **Don't detect compression by looking at a file's first bytes.** Two raw files in `miscload.big` start with bytes that look like RefPack. Use the storage-mode byte.
- **Which files are compressed depends on the archive and the file type.** World, mission and audio archives are raw throughout (28,934 raw entries in total). In archives that use compression, textures and models (`.rx2`) are **always** compressed; only tiny files and PNG/JPG images are ever raw there.

### Corrections to the M6 brief (`docs/tasks/modding-tools.md`)

1. The storage-mode bytes are **filled in**, not zeros.
2. The chunkref layout in the brief ("a record table at offset 32, data after") is **wrong whenever there's more than one piece**. It fails on all 3,772 multi-piece entries. Use the interleaved layout above.

---

## 2. Rebuilding an archive

**How this was checked:** a throwaway Python prototype regenerated every archive's header and index from its parsed contents, then compared byte by byte.

**Rules that hold for all 50 EB3 archives:**

- The names table starts at the next 16-byte boundary after the storage-mode bytes.
- The folder table starts at the next 16-byte boundary after the names.
- The "names + folders" size field ends on a 16-byte boundary.
- Name record width = longest file name + 3. Folder record width = longest folder name + 1.
- All padding is zeros.
- File data starts at the end of the tables, rounded up to a block boundary.
- Every file starts on a block boundary, with only the padding alignment needs between files.
- Empty files share the offset of the next file (two cases in `miscload.big`).

**Two things can't be worked out and must be copied from the original archive:**

1. **The order files are stored in.** It follows hash order in only 4 archives; 46 follow neither hash nor path order.
2. **Whether the archive is padded to a block boundary after its last file** (22 are, 28 aren't).

**Result:** regenerating with those two copied reproduces all 50 archives' tables exactly. Three were written out in full (`fetexture.big`, `parkassets.big`, `miscload.big`) and match the originals byte for byte (SHA-256).

**chunkref containers** can be rebuilt byte for byte too, with one exception found so far: one entry in `parkassets.big` has a few bytes of leftover data in the padding before its second piece. Readers skip that padding, so it doesn't change any file's contents.

**A real RefPack compressor** (written for the prototype, not borrowed) round-trips on random, all-zero and repetitive data. Recompressing all 108 files in `fetexture.big` gives a 2.26 MB archive against the original 2.17 MB, and every file decompresses back to the original contents.

---

## 3. In-game tests: what the game accepts

Each test mod changed only `data/big/fetexture.big` (menu textures) and/or `data/content/parkassets.big` (park pieces), with every file inside identical in content to the original. Each was run through the launcher's mod loader on its own.

| Test | What changed | Archive size | Result |
|---|---|---|---|
| First test | Both archives, every file stored uncompressed | 12.7 MB + 69.8 MB | **Crash** as the main menu opens |
| Test 1 | Both archives, every file kept compressed but re-encoded as literal-only RefPack (valid, no real compression) | 12.8 MB + 70.5 MB | **Crash**, same point |
| Test 2 | `fetexture.big` only, every file uncompressed | 12.7 MB | **Crash**, same point |
| Control | `fetexture.big` written by our tool with nothing changed (byte-identical) | 2.2 MB | **Works**, into gameplay |
| Test 5 | `fetexture.big`, files untouched but all moved 10 MB further in | 12.7 MB | Not run yet |
| Test 6 | `fetexture.big`, every file recompressed with our real compressor | 2.26 MB | Not run yet |

**What's proven:**

- The mod loader's `mods\.active-game` copy is fine: the control launches and plays.
- A changed `fetexture.big` is what crashes. The crashes end with no error line, at the moment the main menu's textures appear. In a working run the game goes on to load `EAWebkit.xex` and then the world.

**Current guess (not proven):** the game loads this archive into a fixed amount of memory, and the bloated versions (about six times bigger) overflow it. Tests 5 and 6 are built to settle this: test 5 changes only the size, and test 6 changes the encoding while keeping the size down.

**Earlier assumption that turned out wrong:** "storing changed files uncompressed is safe because the game already reads raw files" is not true for `fetexture.big`. Until test 6 is in, the plan is for the writer to compress replaced files the same way the original did.

---

## 4. How other projects change game files

| Project | What it does | How it gets changed files into the game |
|---|---|---|
| [SkateGM](https://github.com/the-schwilliam/SkateGM) | Skate 3's physics and tricks inside Garry's Mod | Reads archives only; never writes them. Its archive reader matched retail on every point we checked. |
| [Skate 3 Recompiled+](https://github.com/dougieprishpreed/skate3recomp-plus) (Doug) | Adds Skate 2's New San Vanelona to Skate 3 | An install step prepares the Skate 2 world under `mods\skate2\` in versioned folders (`world-support-gvr-v1`, `proxy-v1`, `sky-water-v1`, `audio-v1`, `livingworld-v1`…), much of it as **loose files**. C++ changes in his recomp fork register those folders as extra drives in the game's virtual file system and redirect game paths to them. Where he builds an archive, he converts a Skate 2 BIG4 into an EB3 world archive by writing a new index in front of the untouched data; world archives are uncompressed, so he never rewrites a compressed one. Making the city run also takes many engine patches (bigger memory pools, collision grid, texture ID translation), each behind a `skate3_skate2_*` setting. |
| [Jared Therriault's fork](https://github.com/JaredTherriault/skate3recomp), merged into [BadVibrati0ns/skate3recomp-plus](https://github.com/BadVibrati0ns/skate3recomp-plus) | Custom Own The Spot challenges, a challenge editor, achievements, player character replacement, cheats | Mods ship as a small **patch file** (`.mod`/`.delta`): a new archive index plus the changed files, added after the original data. A C++ change to the recomp's file system reads the untouched original archive and lays the patch over it while the game reads. Replaced files are stored **uncompressed**, including inside `db.big`, and he reports it working; those are small data tables, not textures, and the archive only grows slightly. His PNG texture override only repaints his own custom character models; it isn't a general texture-pack system. |

**BadVibrati0ns' own changes** (Nov 2) were merging Jared's fork into Doug's v0.2e and repairing the merge: a code patch that couldn't find its target, both forks hooking the same memory pool allocator, a startup test, Jared's cheats on one **Cheats** page, and a "Mailbox" V-Sync option.

**What this means for the launcher:**

- Jared's patch-file overlay and Doug's drive redirection both need changes to the recomp itself. A launcher can only change files on disk, so it has to rebuild archives (see section 3).
- **The game does look for loose files in some folders before using its archives.** Our logs show "entry not found" lines for loose `.stategraph` files, and the official recomp sets up those folders on purpose (`InstallRecipeOverlay` in `skate3_app_common.cpp`). Whether this covers textures is unknown. The file access log (M5) would show which loose paths the game tries.

---

## 5. The 5C8 build of the recomp

[5C8/skate3recomp](https://github.com/5C8/skate3recomp) is a publication snapshot based on mchughalex's recomp, using its own SDK fork ([5C8/rexglue-skate3](https://github.com/5C8/rexglue-skate3)). Version 1.0.9 at the time of writing.

**Works the same as the official recomp:** the `%APPDATA%\skate3` folder, `portable.txt`, `settings.toml`, `saves`, `--game_data_root`. Every `skate3_` setting the launcher edits exists there under the same name (defaults and ranges not compared yet).

**What the launcher needs to handle:**

- **Detection:** a 5C8 install is currently read as "official".
- **Versions:** 5C8 numbers its releases 1.0.x, while official is at 2.0.x, so version checks and `targets.min_version` would misfire.
- **Updates:** a different release zip name, plus a `title-update\` folder that must stay beside the exe (it holds the bundled Title Update 3 patches).
- **Test mod / Benchmark:** since 1.0.4, 5C8 ignores `skate3_intro_movie_skip` (`docs/CHANGELOG.md`).
- **Not exposed in the launcher yet:**
  - profiles (`profiles\profiles.toml`, `skate3_profiles.cpp`)
  - split screen (`--splitscreen`)
  - VR (`--skate3_vr=true`)
  - the server address
  - the Mods setting `skate3_mod_skater_proportions`
  - custom songs from `game\data\audio\music\custom_songs.txt` (up to 99; a first line of `!list` replaces the list instead of adding to it; `skate3_custom_songs.cpp`)

5C8's `tools/eb_extract.py` confirms the archive layout in section 1 but is simpler: it assumes 64-byte blocks and ignores folders and chunkref.

---

## 6. Online play and multiplayer

**How it works today** (`skate3_online.cpp` in 5C8's recomp, `xam_net.cpp` in its SDK):

- The game signs in and finds matches through an EA **Blaze** server.
- The recomp redirects the game's online hostnames (`gosredirector.ea.com`, `skate3_web`) to a server you choose with the `net_host_overrides` setting. In-game this is **Settings → Online → Server Address**.
- It also forces the connection to be unencrypted and fills in sign-in calls the original Xbox Live code expected.
- The Xbox 360 build sends profile uploads to a fixed address, `10.10.237.194`. 5C8 1.0.7 redirects that too.
- 5C8's community server is `comebacktonewsanvan.com`. Neither recomp repo includes the server itself.

**What that means:**

- **Pure peer-to-peer isn't something a launcher can add.** The game won't go online without a Blaze sign-in, so removing the server means changing the recomp's code or emulating Blaze.
- **Hosting your own server is the realistic route.** [hallofmeat/Skateboard3Server](https://github.com/hallofmeat/Skateboard3Server) is an archived C# server described as compatible with Skate 3. Its licence and whether it works with the Xbox 360 build haven't been checked.
- **A lobby in the launcher** would then be a list of servers plus setting the server address for you. Where that list would live is still open.

---

## 7. Sources and licences

| Source | Licence | How we use it |
|---|---|---|
| Retail game files (your own copy) | EA's | Checked locally, never committed |
| [mchughalex/skate3recomp](https://github.com/mchughalex/skate3recomp) | No licence file | Facts only |
| [5C8/skate3recomp](https://github.com/5C8/skate3recomp), [5C8/rexglue-skate3](https://github.com/5C8/rexglue-skate3) | No blanket licence | Facts only |
| [dougieprishpreed/skate3recomp-plus](https://github.com/dougieprishpreed/skate3recomp-plus) | None | Facts only |
| [BadVibrati0ns/skate3recomp-plus](https://github.com/BadVibrati0ns/skate3recomp-plus) (with Jared Therriault's work) | None | Facts only |
| [the-schwilliam/SkateGM](https://github.com/the-schwilliam/SkateGM) | GPL-3.0 overall; `exporter/tools/vendor/skate3_ui/` has its own MIT licence | Facts only for now; the MIT folder could be ported with credit |

The launcher's own code is MIT, so it never includes code from the projects marked "facts only". The archive reader and writer will be our own C#.

**Credit** for the archive format research: the EB3/BIG4 layout, path hash and RefPack/chunkref decoding are based on format notes in Skate 3 Recompiled+ by dougieprishpreed, SkateGM by the-schwilliam (and the Skate 3 Custom Engine Layer contributors), and 5C8's recomp tools, all built on Skate 3 Recomp by mchughalex. Every fact was then confirmed against retail files.

---

## 8. Open questions

| Question | How we'll answer it |
|---|---|
| Is the `fetexture.big` crash about archive size or about how the files are encoded? | In-game tests 5 and 6 |
| Can a launcher-built archive with properly compressed replacements be used for texture packs? | Test 6, then the same test on a large chunkref archive such as `createacharacter.big` |
| Does the game read loose files instead of archives for textures? | The file access log (M5) |
| Does an existing open-source Blaze server work with the recomp? | Check Skateboard3Server's licence, then a local test |
| Do 5C8's setting defaults and ranges match the official recomp's? | Compare `REXCVAR_DEFINE_` lines in both sources |
