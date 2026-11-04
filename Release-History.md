Full notes for each version are on the [Releases page](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/releases).

## [v3.1.1](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/releases/tag/v3.1.1) · November 2026

A patch release from play-testing v3.1.0.

- **Known-issue warnings:** settings that trigger a known Skate 3 Recomp bug get a ⚠ warning on their card, with the workaround. The list lives in `known-issues.txt` and updates without a launcher release. See [[Warnings and Known Issues]].
- **Portable installs fixed:** with `portable.txt` or a `saves` folder next to `skate3.exe`, the launcher edits the same `settings.toml` and backs up the same saves the game uses. See [[Portable Installs]].
- **Polish:** status messages stay up for a few seconds, unusable buttons look greyed out, and a finished benchmark is no longer reported as a crash.

## [v3.1.0](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/releases/tag/v3.1.0) · November 2026

The biggest update yet: the launcher now installs, checks, mods and benchmarks the recomp.

- **Mod loader** on a new [[Mods]] page: install, enable and order mods with hard links, never touching the game files.
- **[[Game Files and Recomp Updates]]:** setup checklist, one-click recomp updates with rollback, and a full game-file check.
- **[[Benchmark]]:** the game drives itself to the same spot every run and the launcher records FPS, worst frame and jitter.
- **Photo Mode** page: free-camera speed and rebindable free-camera and screenshot keys.
- Also: controller navigation for the launcher, Add to Steam, a graphics driver check, pre-filled GitHub crash reports with optional crash dumps, and a What's new pop-up.

## [v3.0.0](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/releases/tag/v3.0.0) · October 2026

The first public release (the version number continues from private builds).

- Every setting on clean pages with plain-English help, plus an [[Advanced Page]] with search and raw editing.
- Quality presets with a live VRAM estimate, conflict warnings, and a Vulkan GPU picker.
- [[Profiles]], settings history, automatic save backups, a controller tester and menu-combo recorder, a log viewer with bug reports, shader-cache cleaning, system tray, Discord Rich Presence and command-line quick play.

## In development

- **Modding tools** (behind **Launcher → Show modding tools**): `mod.json` format 1 with previews, compatibility and dependency warnings, Skate 3 Recompiled+ detection, **Start a new mod**, a mod checker with **Package as .zip**, **Create a mod from changed files**, and drag-and-drop installs on the Mods page. See [[Making Mods]].
