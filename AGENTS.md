# AGENTS.md — working on Trooper's Skate 3 Recomp Launcher

Read this before changing anything. It applies to every AI coding agent (Gemini, Claude, Codex, Copilot…) and to human contributors.

## What this is

A portable Windows launcher, settings editor, mod loader and toolbox for the [Skate 3 Recomp](https://github.com/mchughalex/skate3recomp) PC port. One `.exe`, no installer, no extra DLLs. Players drop it next to `skate3.exe`.

The launcher **edits the recomp's config and data files and starts the game**. It never contains game files or recomp code, and it never modifies the `game` folder.

## The hard rules

1. **C# 5 only.** `src/build.cmd` compiles with `csc.exe` from `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319`, the compiler that ships with Windows. It only understands C# 5. **These do not compile:**

   | Not allowed (C# 6+) | Write instead |
   |---|---|
   | `$"Hello {name}"` | `"Hello " + name` |
   | `a?.b`, `a?[i]` | `a != null ? a.b : null` |
   | `int X => 5;` / `void F() => G();` | `int X { get { return 5; } }` / `void F() { G(); }` |
   | `public int X { get; } = 5;` | a field set in the constructor |
   | `nameof(x)` | `"x"` |
   | `out var x` / `out int x` inline | declare `int x;` first |
   | `catch (E e) when (…)` | `catch (E e) { if (!…) throw; … }` |
   | tuples `(a, b)`, `var (a, b) = …` | a small class, or `KeyValuePair` |
   | local functions | a private method or a lambda |
   | `using static`, `default` literal, pattern matching (`is X x`, `switch` on types), `throw` expressions, `??=` | the C# 5 equivalent |

   Lambdas, LINQ, `var`, anonymous methods (`delegate { … }`), object/collection initialisers and generics are all fine.

2. **Only .NET Framework 4.5 assemblies that ship with Windows.** `build.cmd` references `System.Windows.Forms`, `System.Drawing`, `System.Core` and `System.IO.Compression`. No NuGet packages, no extra DLLs, no `System.Web.Extensions`. JSON is parsed with `MiniJson` (`src/GameServices.cs`); HTTP goes through `Http` / `HttpWebRequest`. If you think you need a new reference, ask first.

3. **Never guess recomp facts.** Setting (cvar) names, their defaults and ranges, key-bind names, command-line flags, log lines, file names inside `game\`: every one in this codebase was taken from the recomp's source (`mchughalex/skate3recomp` and its SDK `mchughalex/rexglue-skate3`, branch `skate3-sdk-clean`). A wrong cvar name compiles fine and silently does nothing. **Look it up in the source and say where you found it** in the PR (file and line). Useful places:
   - cvars: search for `REXCVAR_DEFINE_` in `skate3recomp/src/*.cpp`
   - key binds and their names: `rexglue-skate3/src/ui/keybinds.cpp` (names are case-sensitive: `End`, `F6`, `Ctrl+Shift+B`)
   - paths, `portable.txt`, `saves`, `--game_data_root`: `skate3recomp/src/skate3_app_common.cpp`
   - Title Update 3 hashes: `skate3recomp/src/skate3_title_update_installer.cpp`
   - demo-path automation (used by the benchmark): `skate3recomp/src/skate3_demo_path.cpp`

4. **Don't touch the player's data in surprising ways.**
   - `settings.toml`: only rewrite the keys that changed (`ConfigFile.Save`), and snapshot it first (`Profiles.SnapshotHistory()`). The game rewrites this file when it exits, so warn if it's running (`IsGameRunning()`).
   - Never write into `game\`. Mods go through the hard-link copy in `mods\.active-game` (`Mods.Prepare`).
   - Saves: always back up before replacing (`SaveData.Backup("pre-restore")`).
   - Anything destructive (delete, restore, install, rollback) needs a confirmation (`DarkDialog.Ask`) and must be refused while the game runs (`GameRunningGuard`).
   - Files from zips: check every path stays inside the target folder (`FileUtil.Inside`).

5. **Stay portable.** No registry writes outside `HKCU` without an explicit admin prompt the user asked for (see `CrashDumps.Enable`). No installers, services or background processes that outlive the launcher.

## Repository layout

| Path | What's in it |
|---|---|
| `src/TroopersSkate3Launcher.cs` | Entry point and command line (`Program`), the main window (`MainForm`), the **settings catalog** (`BuildSettings`), presets, save/PLAY logic, conflict warnings (`ComputeWarnings`), tray, Discord hand-off, and the custom controls (`Theme`, `Card`, `ToggleSwitch`, `Slider`, `DarkCombo`, `PillButton`, `NavItem`, …). |
| `src/LauncherPages.cs` | v3.0 tool pages: Advanced, Profiles, Saves & DLC, Controller, Logs, Launcher, plus page building blocks (`StackPage`, `ToolCard`, `ItemRow`) and helpers (`MkButton`, `MkToggle`, `Row`, `Flash`, `When`). |
| `src/LauncherPagesV31.cs` | v3.1+ pages: Game files, Mods, Benchmark, Photo Mode extras, Steam / driver / crash-report cards, controller navigation, What's new, `DarkDialog`. |
| `src/LauncherServices.cs` | Background services with no UI: `Paths` (incl. portable mode), `LauncherPrefs`, profiles, GPU detection, `SaveData`, `LogTools`, `Updates`, Discord IPC, shortcuts, `SysInfo`, XInput / DirectInput. |
| `src/GameServices.cs` | v3.1+ services: `Mods` (loader), `GameLaunch`, `RecompInstaller` (update / rollback), `GameFiles` (checks, reference hashes), `Benchmarks`, `SteamShortcut` + `BinaryVdf`, `DriverCheck`, `CrashDumps`, `KnownIssues`, `MiniJson`, `FileUtil`. |
| `src/build.cmd` | The build. Compiles every `src\*.cs` into `bin\Trooper's Skate 3 Recomp Launcher.exe`. |
| `.github/workflows/build.yml` | CI: builds on `windows-latest` with `build.cmd` for every push and PR. A `v*` tag also packages the zip and creates a **draft** release using `.github/release-notes/<tag>.md`. |
| `.github/release-notes/vX.Y.Z.md` | Release notes, one file per version. |
| `known-issues.txt` | Recomp bugs the launcher warns about. Downloaded by the launcher from `main`, so editing it changes live warnings for every player. |
| `known-driver-issues.txt` | Known-bad GPU drivers, same idea. |
| `docs/screenshots/` | README screenshots, 1180×840 PNG. |

New code: put services (no UI) in `GameServices.cs` or a new `src/<Name>Services.cs`; put pages in a new `src/LauncherPages<Name>.cs` as `partial class MainForm`. `build.cmd` picks up every `.cs` automatically.

## House conventions

**UI**
- Everything is owner-drawn and dark. Use `Theme` colours and fonts only (`Theme.Bg`, `Theme.Card`, `Theme.Text`, `Theme.Muted`, `Theme.Accent`, `Theme.Warn`, `Theme.Good`; `Theme.Base`, `Theme.Bold`, `Theme.Small`, `Theme.Mono`). No stock-styled WinForms controls.
- Scale every pixel value with `D(px)` (or `Dpi.S` outside `MainForm`).
- A tool page is a `StackPage` of `ToolCard`s, registered with `AddPage(name, blurb, glyph, "TOOLS", false, page)`, and refreshed in `info.OnShow`. Call it from `BuildToolPages()` in `LauncherPages.cs`.
- Card contents are laid out in `card.Body = (w, top) => { …; return bottom; }`. Lay rows out with `Row(x, y, controls…)`, `ToggleLine`, `StackRows`.
- Buttons: `MkButton(text, glyph, ButtonStyle.Primary|Secondary|Ghost, tooltip)`. Glyphs are Segoe MDL2 code points written as escapes (`""` = folder). Icon-only buttons need a tooltip.
- Toggles stored in `launcher.ini`: `MkToggle("pref_key", default, onChanged)`.
- Status messages: `Flash(text, Theme.Good|Warn|Accent)`. Confirmations and choices: `DarkDialog.Ask(this, heading, message, "Primary button", "Cancel")` returns the button index (−1 if closed).
- Background work: `ThreadPool.QueueUserWorkItem`, then update the UI with `Ui(delegate { … })`. Never block the UI thread on network or big file work.
- After changing a card's contents, call `RelayoutCurrent()` (or `currentPage.Page.Relayout()`).

**Settings (cvars)**
- A setting is declared once in `BuildSettings` with `B(key, label, help, default)`, `C(key, label, help, defaultLiteral, values, labels)` or `S(key, label, help, def, min, max, step, isFloat, suffix)`. Values are **TOML literals** (`"true"`, `"4096"`, `"'vulkan'"`). The page, save logic, reset button, profiles and Advanced search all come from that one entry.
- Read on-screen values with `Val(key)` / `On(key)` / `Num(key, def)`. Compare literals with `Toml.Norm`.
- Settings that conflict get a warning in `ComputeWarnings` / `ComputeV31Warnings`: `w[key] = "message";`. Known **recomp bugs** go in `known-issues.txt` instead of code (format documented at the top of that file).
- Help text is plain English for players, not developer notes.

**Launcher-only options** go in `launcher.ini` through `LauncherPrefs.Read/Write/Bool/Int/SetBool`. It always lives in `%APPDATA%\skate3` (`Paths.Prefs`), even in portable mode.

**Paths:** never hard-code `%APPDATA%\skate3`. Use `Paths.UserDir`, `Paths.Config`, `Paths.SavesRoot`, `Paths.Cache`, `Paths.Dlc`, etc. They follow the recomp's portable rules after `Paths.Resolve(gameExe)`.

**Errors:** a failed backup, log read or network call must never stop the game from launching. Catch it, carry on, and tell the user if it matters.

**Text style** (UI, README, release notes): short, plain, friendly; second person ("your saves"); no jargon a player wouldn't know; name buttons in **bold** exactly as they appear.

## Building and testing

- **Windows:** double-click `src\build.cmd` (or run it in a terminal). Output: `bin\Trooper's Skate 3 Recomp Launcher.exe`.
- **Linux / macOS / CI-less check:** Mono's compiler at the same language level catches most mistakes:
  ```
  mcs -langversion:5 -target:winexe -out:bin/launcher.exe \
      -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Core.dll -r:System.IO.Compression.dll src/*.cs
  ```
  (mcs can't handle the apostrophe in the real exe name, so build to another name.) Mono can also run the UI for a rough look (`mono bin/launcher.exe`), but Windows is the real target: fonts, DPI, icons, hard links and the registry behave differently.
- **CI is the source of truth.** Every PR is built on `windows-latest`. A PR isn't ready until that build is green.
- There's no unit-test project. For logic that can be tested without the game (parsers, file formats, path rules), write a small throwaway test harness compiled together with `src/*.cs` and describe what you checked in the PR. Format code (archives, VDF, JSON) should round-trip byte-for-byte where possible.
- Anything that needs the real game (launching, mods, benchmark, installs) must be listed under **"Needs testing on Windows"** in the PR, and not claimed as tested.

## Workflow

1. **Branch from `main`**: `feature/<short-name>` or `fix/<short-name>`. Never push to `main` directly.
2. **One focused change per PR.** Don't reformat or reorganise code you aren't changing.
3. Keep **CRLF** line endings in `.cs` and `.cmd` files (see `.gitattributes`). Markdown is LF.
4. **PR description:** what changed and why; where each recomp fact came from (file + line); what you tested and how; "Needs testing on Windows".
5. **Docs in the same PR:** update `README.md` when behaviour or pages change (the README is the user guide, so keep its sections in sync), and add a line to the next version's release notes.

**Releases** (the maintainer does these):
1. Bump the version in **three** places: `AssemblyVersion` and `AssemblyFileVersion` (top of `TroopersSkate3Launcher.cs`) and `MainForm.LauncherVersion`.
2. Add a `WhatsNew…` list in `LauncherPagesV31.cs` and wire it into `ShowWhatsNew`.
3. Write `.github/release-notes/vX.Y.Z.md`.
4. Merge to `main`, then push the tag `vX.Y.Z`. CI builds `TroopersSkate3Launcher-vX.Y.Z-win.zip` and creates a draft release; review it and publish.

## Things to know before you start

- **Mods folder:** every folder in `mods\` (except names starting with `.` or `_`) is treated as a mod. The Skate 3 Recompiled+ fork keeps its New San Van data in `mods\skate2\`, so the loader must treat that folder as reserved. **As of v3.1.1 it doesn't yet** (planned fix for v3.1.2): don't build anything that enables, changes or deletes `mods\skate2`.
- **Protected game files:** mods can never replace `default.xex`, `default.xexp` or the EAWebkit `.xex/.xexp` files (`GameFiles.Protected`).
- **Benchmark:** temporarily writes demo-path settings into `settings.toml` and **must** restore it (`Benchmarks.RestoreConfig`), including after a crash (`RecoverConfig` on startup).
- **Game data root:** a modded launch passes `--game_data_root "<mods\.active-game>"`; a normal launch scrubs any leftover `game_data_root` from `settings.toml` (`Mods.ScrubConfig`).
- **Archives:** the game's `.big` files are EA `EB3` archives (header `EB\0\3`), not classic BIG. Format notes exist in the Skate 3 Recompiled+ fork (`tools/world_archive.py`), which has **no license**: read it for format facts, write our own C#, don't copy its code.
- **Licensing:** this repo is MIT. Never add code from repositories without a compatible license, and never add game files, EA artwork (including the Skate 3 icon) or recomp binaries.
