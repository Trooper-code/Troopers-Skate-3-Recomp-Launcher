This page is for people **making** mods. For installing and ordering mods, see [[Mods]].

## How mods work

A mod is a folder in `mods\` (next to `skate3.exe`) that holds **only the game files you change**, laid out the same way as the `game` folder. When you press PLAY with mods enabled, the launcher builds `mods\.active-game`, a copy of the `game` folder made of hard links (so it takes no extra disk space), swaps in the mods' files, and starts the game with `--game_data_root` pointing at it. The real `game` folder is never changed.

```text
mods\
  Retro Shoes\
    mod.json            (optional)
    preview.png         (optional)
    README.txt          (optional)
    data\
      content\
        createacharacter.big
```

- A `game` subfolder, or one wrapper folder (which most zips have), works too.
- **Mods lower in the load order win** when two mods replace the same file.
- Mods **can't** replace `default.xex`, `default.xexp` or the EAWebkit `.xex`/`.xexp` files, because the recomp's code is built against exactly those.
- Files must be the **Xbox 360** versions. PS3 mods use different formats (`.psg` instead of `.rx2`, for example) and won't load.
- Folders whose names start with `.` or `_` are ignored, and `mods\skate2` is reserved for Skate 3 Recompiled+ (see below).
- `mod.json`, `README.txt`, `README.md`, `LICENSE`, `CHANGELOG.md`, `preview.*` and `thumbnail.*` at the root of the mod folder are never linked into the game.

## mod.json (format 1)

Every field is optional, and mods without a `mod.json` still load.

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
| `format` | Format version. Missing means `0` (a v3.1 mod); `1` is current. A higher number shows a "made for a newer launcher" warning but the mod still loads. |
| `name` | Title shown on the Mods page (defaults to the folder name). |
| `version` | Shown next to the title, for example `v1.2.0`. |
| `author` | Shown as "by …" on the mod's row. |
| `description` | Shown when hovering the mod's title. |
| `preview` | An image next to `mod.json`, shown as a thumbnail on the mod's row. Must stay inside the mod folder. |
| `targets.recomp` | `"official"` ([Skate 3 Recomp](https://github.com/mchughalex/skate3recomp)), `"plus"` ([Skate 3 Recompiled+](https://github.com/dougieprishpreed/skate3recomp-plus)) or `"any"`. |
| `targets.min_version` | Lowest recomp version the mod works with, for example `"2.0.2"`. |
| `requires` | Other mods this one needs, by folder name or `name`. |
| `conflicts` | Mods that shouldn't be enabled at the same time, by folder name or `name`. |

When a mod's target or minimum version doesn't match the install, a required mod is missing or disabled, or a conflicting mod is enabled, its row on the Mods page shows a yellow ⚠ line. **These are warnings, not blocks: PLAY still works.** The full spec is in [`docs/mod-format.md`](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/blob/main/docs/mod-format.md).

## The Modding tools page

Turn on **Launcher → Modding tools → Show modding tools** and restart the launcher. A **Modding tools** page appears under Tools. Normal players never see it.

- **Start a new mod:** type a name and press **Create mod folder**. It creates `mods\<name>\` with a format-1 `mod.json`, an empty `data\` folder and a `README.txt`, then opens the folder in Explorer.

- **Check and package a mod:** pick a mod and press **Check mod**. The launcher checks every file (hashing runs in the background, with **Cancel**) and lists what it finds:

  | | What it means |
  |---|---|
  | ✖ **Error** | No usable game files; a protected file (`default.xex`, `default.xexp`, the EAWebkit files); a link or junction inside the mod; or a path that escapes the mod folder or uses names Windows can't store. **Errors block packaging.** |
  | ⚠ **Warning** | A whole original game archive over 20 MB (ship only what you changed); a file identical to the original in `game\` (it does nothing); a PS3 file such as `.psg`; or a new file that doesn't exist in `game\` (probably a wrong path). |
  | ℹ **Info** | No `mod.json`, or one without `name`/`version`; no preview image. |

  Then fix things with the buttons underneath:
  - **Remove identical files** deletes the do-nothing copies, after asking. It only ever deletes inside that mod's folder, and it's greyed out while there are errors.
  - **Add mod.json** writes a format-1 `mod.json`, or fills in a missing name and version without losing what's there.
  - **Package as .zip** writes `<Name>-<version>.zip` to your Desktop with one top-level folder inside, ready to share. It skips junk like `Thumbs.db`, `desktop.ini` and `.git`, and asks before overwriting.

- **Create a mod from changed files:** for when you edit a copy of the game folder directly. Copy `game\` somewhere else, edit the copy, then pick that copy as the **work folder**, type a mod name and press **Compare**. The launcher compares every file with your real `game\` folder (in the background, with **Cancel**) and lists what's **changed** and **new**, all ticked. Untick anything you don't want and press **Create mod**. It copies only the ticked files into `mods\<name>\` at the right paths, adds a `mod.json`, and runs **Check mod** on the result.
  - It never changes the work folder or `game\`.
  - It refuses the real `game\` folder, the `mods` folder and the recomp folder itself. If you pick a copy of the whole recomp folder, it offers to use the `game\` folder inside it.
  - It skips links and junctions, the protected `default.xex` and EAWebkit files, and junk like `Thumbs.db` and `.git`.
  - Unchanged files are left out, and you're warned before creating a mod with more than 50 files or 500 MB.
  - New mods start disabled. Turn them on from the [[Mods]] page.

More tools are on the way: quick test launches, a log of which files the game actually reads, and an archive browser for the game's `.big` files. See [[Roadmap]].

## Skate 3 Recompiled+

[Skate 3 Recompiled+](https://github.com/dougieprishpreed/skate3recomp-plus) is a community fork of the recomp. The launcher detects it (by its `mods\skate2` folder or its `skate3_skate2_enabled` setting) and uses that for `targets.recomp` warnings. The fork keeps its New San Vanelona data in `mods\skate2`, so the launcher hides that folder from the Mods page and never changes it, and you can't name a mod `skate2`.

## Sharing mods

- **Run Check mod first.** It catches the common mistakes below, and **Package as .zip** builds the zip the right way for you.
- **Ship only the files you changed.** Don't include whole game archives you didn't modify: that's sharing EA's files, and it makes the mod huge.
- If you zip it yourself, use **one top-level folder** named after the mod, so **Install from .zip…** (or dragging the zip onto the Mods page) picks it up cleanly.
- Fill in `name`, `version` and `author` in `mod.json`, and add a `preview.png`.
