# `mod.json` Format Specification (Format 1)

Every mod in `mods\<Mod Name>\` can include an optional `mod.json` file at the root of the mod folder. The format is **additive**: every field is optional, and mods without a `mod.json` (or v3.1 mods without a `format` field) continue to load without changes.

## Example

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

## Folder layout

A mod folder mirrors the game's `game\` directory:

```text
mods\
  Retro Shoes\
    mod.json
    preview.png
    README.txt
    data\
      content\
        createacharacter.big
```

Documentation and metadata files at the root of the mod folder (`mod.json`, `README.txt`, `README.md`, `LICENSE`, `CHANGELOG.md`, `preview.*`, `thumbnail.*`) are ignored by the file linker and never linked into `mods\.active-game`.

## Fields

| Field | Type | Default | Meaning |
|---|---|---|---|
| `format` | integer | `0` | Format version. Missing means `0` (a v3.1 mod). `1` is the current format. If `format` is newer than the launcher understands (`> 1`), the launcher shows a *"made for a newer launcher"* warning on the mod's row and still loads the mod. |
| `name` | string | folder name | Display title shown on the Mods page. |
| `version` | string | `""` | Mod version string (for example `"1.2.0"`). Shown next to the title as `v1.2.0`. |
| `author` | string | `""` | Author or team name, shown as `by <author>` on the mod's row. |
| `description` | string | `""` | Short summary shown in the tooltip when hovering the mod's title. |
| `preview` | string | `""` | Relative filename of an image next to `mod.json` (for example `"preview.png"` or `"preview.jpg"`), shown as a thumbnail on the mod's row. Must stay inside the mod folder. |
| `targets.recomp` | string | `"any"` | Which recomp build the mod is made for: `"official"` ([`mchughalex/skate3recomp`](https://github.com/mchughalex/skate3recomp)), `"plus"` ([Skate 3 Recompiled+](https://github.com/dougieprishpreed/skate3recomp-plus)), or `"any"`. |
| `targets.min_version` | string | `""` | Lowest recomp version the mod works with (for example `"2.0.2"`), compared against the installed recomp version. |
| `requires` | array of strings | `[]` | Other mods this mod depends on, matched case-insensitively by folder name or `name` in `mod.json`. |
| `conflicts` | array of strings | `[]` | Other mods that should not be enabled at the same time, matched case-insensitively by folder name or `name` in `mod.json`. |

## Compatibility checks and warnings

On the **Mods** page, the launcher checks each mod's metadata and shows a yellow `⚠` warning line on the mod's row when:

- `format` is higher than `1` (*"Made for a newer launcher (mod.json format N)"*).
- `targets.recomp` is `"plus"` on an official Skate 3 Recomp install, or `"official"` on a Skate 3 Recompiled+ install.
- `targets.min_version` is newer than the installed recomp version.
- Any mod listed in `requires` is missing or disabled.
- Any mod listed in `conflicts` is currently enabled (or another enabled mod lists this mod in its `conflicts`).

These checks are **warnings, not blocks**: **PLAY** still works.

## Checking and packaging

On the **Modding tools** page (**Check and package a mod**), **Check mod** validates an installed mod before you share it:

- **Errors (block packaging):** no usable game files, protected executables (`default.xex`, `default.xexp`, `data/webkit/EAWebkit.xex`, `data/webkit/EAWebkit.xexp`), or paths that escape the mod folder or use characters/names Windows cannot store.
- **Warnings:** whole original game archives over 20 MB, files whose SHA-256 hash is identical to the file in `game\` (**Remove identical files** can delete the copies inside the mod folder), PS3-only formats (such as `.psg`), and new files that do not exist in `game\`.
- **Info:** missing `mod.json` or missing `name`/`version` (**Add mod.json** fills them in), and missing preview image.

**Package as .zip** writes `<Name>-<version>.zip` to your Desktop with a single top-level folder named after the mod containing `mod.json`, any preview/readme files, and the mod's game files, ready to install via **Install from .zip…** or by dragging the `.zip` onto the **Mods** page.
