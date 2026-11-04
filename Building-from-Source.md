
| File | Contents |
|---|---|
| `src/TroopersSkate3Launcher.cs` | Main window, the settings catalog, presets, save/PLAY logic, tray, Discord hand-off, command line. |
| `src/LauncherPages.cs` | The Advanced, Profiles, Saves & DLC, Controller, Logs and Launcher pages and their controls. |
| `src/LauncherServices.cs` | Background services: paths and prefs, GPU detection (Vulkan), save backups, log parsing, update check, Discord IPC, shortcuts, system info, XInput/DirectInput. |
| `src/LauncherPagesV31.cs` | The Game files, Mods and Benchmark pages, the Photo Mode extras, the Steam / driver / crash-report cards, controller navigation and the What's new dialog. |
| `src/GameServices.cs` | v3.1 background services: mod loader, recomp installer and rollback, game-file checks, benchmark runner, Steam `shortcuts.vdf` writer, driver check, crash dumps. |
| `src/LauncherPagesModding.cs` | The Modding tools page, the Launcher-page modding toggle card, and Mods-page preview thumbnails and warning rows. |
| `src/ModdingServices.cs` | Modding background services: `mod.json` format 1 metadata, Recompiled+ flavor detection, compatibility/dependency warnings, and new-mod scaffolding. |
| `src/build.cmd` | Builds the exe with the C# compiler that ships with Windows. |

Double-click **`src\build.cmd`** (or run it from a terminal). It uses `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, so no Visual Studio or .NET SDK is needed, and writes `bin\Trooper's Skate 3 Recomp Launcher.exe`. The code is C# 5 so that built-in compiler accepts it.

The exe gets the Skate 3 icon only if `src\skate3.ico` exists, or if `skate3.exe` is one folder above `src`, in which case the icon is extracted automatically. The icon is EA artwork, so it isn't committed.

**Releases:** put the release notes in `.github/release-notes/vX.Y.Z.md`, then push the tag. Pushing a tag like `v3.0.1` makes GitHub Actions build the exe on Windows, zip it with the README and LICENSE, and create a **draft** GitHub release with those notes and the zip attached — review it on the Releases page and press Publish. Every push and pull request is also built, and the zip is attached to the run as an artifact.


## Checking your changes without Windows

Mono's compiler at the same language level catches most mistakes:

```
mcs -langversion:5 -target:winexe -out:bin/launcher.exe \
    -r:System.Windows.Forms.dll -r:System.Drawing.dll -r:System.Core.dll -r:System.IO.Compression.dll src/*.cs
```

`mcs` can't handle the apostrophe in the real exe name, so build to another name. Windows is still the real target: fonts, DPI, icons, hard links and the registry behave differently, and the CI build on `windows-latest` is the source of truth.

See [[Contributing]] for the rules every change has to follow.
