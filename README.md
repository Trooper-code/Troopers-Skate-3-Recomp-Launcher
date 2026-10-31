<div align="center">

# Trooper's Skate 3 Recomp Launcher

**A portable launcher, settings editor and toolbox for the [Skate 3 Recomp](https://github.com/mchughalex/skate3recomp) PC port.**

[![Build](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/actions/workflows/build.yml/badge.svg)](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/actions/workflows/build.yml)
[![Latest release](https://img.shields.io/github/v/release/Trooper-code/Troopers-Skate-3-Recomp-Launcher?include_prereleases&label=download)](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/releases)
[![License: MIT](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
![Windows 10/11](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D4)

![The Graphics page of the launcher](docs/screenshots/01-graphics.png)

</div>

> **Unofficial.** Not affiliated with the Skate 3 Recomp developers or EA. This repo contains **no game files**: you need your own copy of the recomp.

## Why use it?

- **No more hand-editing!! `settings.toml`.** Every option on clean pages with plain-English help, quality presets and a live VRAM estimate. Only the keys you change are rewritten.
- **Catches mistakes before you launch.** Settings that clash (V-Sync + G-Sync, shadow options with the emulated renderer, a typo in the menu combo…) are flagged right on the card.
- **Picks the right GPU.** Lists your Vulkan GPUs with their VRAM, so the game never starts on the CPU's integrated graphics.
- **Protects your career.** Saves are zipped automatically every time you press PLAY, and restores always back up first.
- **Profiles.** Swap between "Ultra 4K", "Ultrawide" and "Potato laptop" in one click, or from a desktop shortcut.
- **Crash help.** A log viewer that highlights errors, tells you if the last session crashed, and builds a bug report in one click.
- **Plus:** controller tester, menu-combo recorder, update check, shader-cache cleaner, system tray, Discord Rich Presence and command-line quick play.
- **One portable `.exe`.** No installer, no extra DLLs. It runs on the .NET Framework every Windows 10/11 PC already has.
## Download

1. Grab `TroopersSkate3Launcher-vX.Y.Z-win.zip` from [**Releases**](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/releases).
2. Extract `Trooper's Skate 3 Recomp Launcher.exe` into your Skate 3 Recomp folder, next to `skate3.exe`.
3. Run it.

v3.0.0 is the **FIRST public release**. See the [release notes](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/releases) and the [Roadmap](#roadmap) for what's coming.

**Windows SmartScreen:** the exe isn't code-signed, so the first launch may show "Windows protected your PC". Click **More info → Run anyway**. Every release is built by GitHub Actions straight from this source.

## Roadmap

This is the first public release. Planned next:

- [ ] **Mod loader:** install, enable, disable and order mods from inside the launcher.
- [ ] **Texture pack support:** add, switch between and manage custom texture packs.

**NOT PLANNED**
-I WILL NOT BE IMPLEMENTING NETWORKING CAPABILITIES. THIS IS BEYOND MY SCOPE OF UNDERSTANDING, AND THEREFORE IS **NOT** PLANNED. HOWEVER IS SOMEONE DOES IT BEFORE ME I WILL MORE THAN LIKELY FIGURE OUT HOW TO INTEGRATE SUPPORT FOR THEIR TOOL INTO MY LAUNCHER.
Suggestions and bug reports are welcome in [Issues](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/issues).

## Screenshots

| | |
|:-:|:-:|
| ![Conflict warning on V-Sync](docs/screenshots/02-display-warning.png)<br>**Conflict warnings** flag settings that fight each other | ![Ultra preset applied](docs/screenshots/12-preset-ultra.png)<br>**Quality presets** with a live VRAM estimate and unsaved-change markers |
| ![Lighting options with warnings](docs/screenshots/03-lighting-warnings.png)<br>**Lighting & Shadows**: every native-renderer enhancement | ![GPU picker](docs/screenshots/05-gpu-picker.png)<br>**GPU picker** and per-setting reset buttons |
| ![Advanced search](docs/screenshots/06-advanced-search.png)<br>**Advanced**: search every setting, edit any raw cvar | ![Profiles page](docs/screenshots/07-profiles.png)<br>**Profiles**, settings history and one-click shortcuts |
| ![Saves page](docs/screenshots/08-saves.png)<br>**Save backups** and one-click restore, plus DLC | ![Controller page](docs/screenshots/09-controller.png)<br>**Controller tester** and menu-combo recorder |
| ![Logs page](docs/screenshots/10-logs.png)<br>**Logs**: crash check, filtered viewer, bug reports | ![Launcher page](docs/screenshots/11-launcher.png)<br>**Launcher tools**: updates, GPU preference, cache, Discord |

<sub>The screenshots were captured on a test machine without the Windows icon fonts, so buttons show text where Windows shows icons.</sub>

---

# Full guide

## Contents

1. [Quick start](#quick-start)
2. [The window at a glance](#the-window-at-a-glance)
3. [Settings pages](#settings-pages)
4. [Features on every settings card](#features-on-every-settings-card)
5. [Quality presets and VRAM estimate](#quality-presets-and-vram-estimate)
6. [Conflict warnings](#conflict-warnings)
7. [Advanced page](#advanced-page)
8. [Profiles page](#profiles-page)
9. [Saves & DLC page](#saves--dlc-page)
10. [Controller page](#controller-page)
11. [Logs page](#logs-page)
12. [Launcher page](#launcher-page)
13. [What happens when you press PLAY](#what-happens-when-you-press-play)
14. [System tray](#system-tray)
15. [Discord Rich Presence](#discord-rich-presence)
16. [Command line and shortcuts](#command-line-and-shortcuts)
17. [Keyboard shortcuts](#keyboard-shortcuts)
18. [Files the launcher creates](#files-the-launcher-creates)
19. [Building from source](#building-from-source)
20. [Troubleshooting](#troubleshooting)

---

## Quick start

1. Put `Trooper's Skate 3 Recomp Launcher.exe` in the same folder as `skate3.exe` (or in a subfolder of it). It finds the game automatically. If it can't, **PLAY** asks you to locate `skate3.exe` once and remembers it.
2. Change whatever you like. Cards with unsaved changes get an orange strip down their left edge.
3. Press **PLAY**. That saves your changes, backs up your saves and starts the game.

---

## The window at a glance

| Area | What it does |
|---|---|
| **Sidebar → Settings** | Display, Graphics, Lighting & Shadows, Controls, Audio & System, Advanced. |
| **Sidebar → Tools** | Profiles, Saves & DLC, Controller, Logs, Launcher. |
| **Update badge** (under the logo) | Appears only when a newer Skate 3 Recomp release is out. Click it to open the release page. |
| **Quick access** (four icon buttons) | Open the settings folder · Open the game's `logs` folder · Open `settings.toml` in Notepad · **Restore backup** (puts `settings.toml` back to how it was before the launcher first changed it). Hover any button for its name. |
| **Keep launcher open** | Leaves the window open after PLAY instead of closing it. |
| **Use system tray** | Minimizing sends the launcher to the tray, and PLAY tucks it into the tray while you play (see [System tray](#system-tray)). |
| **Config file** | Shows the full path of the `settings.toml` being edited, with "Will be created on save" if it doesn't exist yet. |
| **Header** | Page title, the **Quality preset** picker with **Apply**, and the **estimated VRAM** readout. The preset and VRAM readout appear only on settings pages. |
| **Footer** | Status line, **Revert** (throws away unsaved changes and reloads the file), **Save** (Ctrl+S), and **PLAY**, which shows **RUNNING** while the game is open. |

The status line always tells you where things stand: unsaved change count, "Settings saved. Ready to play.", whether the game is running, or that `skate3.exe` couldn't be found. It also tells you how many settings have warnings.

---

## Settings pages

Every option writes the cvar named in brackets to `%APPDATA%\skate3\settings.toml`.

### Display
- **Fullscreen** (`fullscreen`): borderless fullscreen instead of a window.
- **Monitor** (`monitor`): which display the game opens on (Default, Monitor 1–4).
- **V-Sync** (`vsync`).
- **Variable refresh rate (G-Sync / FreeSync)** (`d3d12_allow_variable_refresh_rate_and_tearing`).
- **Frame limiter** (`d3d12_present_frame_limiter`) and **Frame limiter target** (`d3d12_present_frame_limiter_fps`, 30–360 FPS).
- **Automatic game FPS cap** (`skate3_guest_fps_cap_auto`) and **Manual game FPS cap** (`skate3_guest_fps_cap`, Off–360).
- **FPS counter** (`show_fps_counter`).

### Graphics
- **Renderer** (`skate3_native_render_scene`): Native (enhanced) or Emulated (original).
- **Render scale** (`resolution_scale`): 1x / 2x / 3x. This also writes `draw_resolution_scale_x` and `draw_resolution_scale_y`, so the three always match.
- **Anti-aliasing (MSAA)** (`skate3_native_render_scene_msaa`): Off / 2x / 4x / 8x.
- **Field of view** (`skate3_field_of_view`): 40–120°.
- **Draw distance** (`skate3_draw_distance_scale`) and **Level of detail distance** (`skate3_lod_distance_scale`): 1x–5x.
- **Ultrawide** (`skate3_ultrawide`) and **Ultrawide aspect** (`skate3_ultrawide_target_aspect`): Auto, 21:9 (2560×1080), 21:9 (3440×1440), 32:9.
- **Renderer indicator** (`skate3_native_render_mode_indicator`).

### Lighting & Shadows (Native renderer only)
Dynamic shadows, Enhanced shadow resolution, Soft shadows (PCSS), Static sun shadows and their resolution, Ambient occlusion (SSAO) and full-resolution SSAO, Bloom, Volumetric sun shafts, Directional haze, Screen-space reflections (experimental), and Packed HDR targets.

### Controls
- **Controller backend** (`input_backend`): XInput, or SDL for PlayStation, Switch and other pads.
- **Controller rumble** (`hid_rumble_enabled`).
- **Mouse & keyboard mode** (`mnk_mode`), **Capture mouse** (`mnk_capture_mouse`), **Mouse sensitivity** (`mnk_sensitivity`).
- **Settings menu button combo** (`menu_chord`): typed here, or picked and recorded on the [Controller page](#controller-page).
- **Skippable intro movies** (`skate3_intro_movie_skip`).

### Audio & System
- **Mute audio** (`audio_mute`) and **Audio buffer size** (`audio_device_sample_frames`).
- **Game language** (`user_language`): 12 languages.
- **Graphics API** (`gpu_backend`): Auto, Direct3D 12 or Vulkan.
- **Graphics card (Vulkan)** (`vulkan_device`): *new.* Picks which GPU the game uses with Vulkan. The list is read straight from your Vulkan driver, in the same order the game uses, and shows each card's VRAM and whether it is discrete or integrated. For example: `0: AMD Radeon RX 6800 XT (16 GB, discrete)` and `1: AMD Radeon(TM) Graphics (integrated)`. If the driver can't be queried, the list comes from the game's own log instead. "Automatic" leaves the choice to the game and writes nothing.

If `settings.toml` holds a value that isn't in a dropdown's list, the dropdown shows it as **Custom (value)** and leaves it alone.

---

## Features on every settings card

- **Unsaved marker:** an orange strip on the card's left edge until you save.
- **Reset to default** (↺ button next to the setting's name): appears only when the setting differs from its default. Hover it to see the default value; click it to set the setting back. Like any other change, this isn't written until you Save or press PLAY.
- **Warnings:** a yellow ⚠ line under the help text when the setting conflicts with another one (see [Conflict warnings](#conflict-warnings)).
- Scrolling the mouse wheel over a dropdown or slider scrolls the page, not the value, so you can't change something by accident.

---

## Quality presets and VRAM estimate

![Quality presets and VRAM estimate](docs/screenshots/12-preset-ultra.png)

**Low / Medium / High / Ultra** plus **Apply** set render scale, MSAA, draw and LOD distance, every shadow option, SSAO, sun shafts, haze, packed HDR, and turn on the Native renderer, dynamic shadows and bloom. Applying a preset only changes what's on screen; nothing is saved until you press Save or PLAY.

| Preset | Render scale | MSAA | Draw / LOD | Shadow tile | PCSS | Static shadows | SSAO |
|---|---|---|---|---|---|---|---|
| Low | 1x | Off | 1.0x | Game default | Off | Off | Off |
| Medium | 1x | 2x | 1.5x | 2048 | Off | 2048 | Half-res |
| High | 2x | 4x | 2.0x | 2048 | On | 4096 | Half-res |
| Ultra | 3x | 8x | 3.0x | 4096 | On | 8192 | Full-res |

**Est. VRAM** under the preset picker updates live as you change settings. It's a rough model of game assets plus render targets (scaled by render scale, MSAA and HDR format) plus shadow maps, and it's compared against the VRAM of the GPU the game will use. It turns yellow above 90% of that card's memory. Treat it as a guide, not a measurement.

---

## Conflict warnings

![Conflict warnings](docs/screenshots/03-lighting-warnings.png)

The launcher checks your settings against each other and puts a ⚠ line on the affected card:

| Situation | Warning appears on |
|---|---|
| Renderer is **Emulated** | MSAA and every Lighting & Shadows option ("No effect while Renderer is set to Emulated"), and Ultrawide (which needs Native). |
| Dynamic shadows off | Enhanced shadow resolution, Soft shadows (PCSS). |
| Static sun shadows off | Static sun shadow resolution. |
| SSAO off | Full-resolution SSAO. |
| Ultrawide off but an aspect is chosen | Ultrawide aspect. |
| V-Sync on together with VRR | V-Sync (adds latency and judder). |
| Frame limiter off but its target was changed | Frame limiter target. |
| Automatic FPS cap on and a manual cap set | Manual game FPS cap (ignored). |
| Mouse & keyboard mode off | Capture mouse, Mouse sensitivity (if changed). |
| Menu combo uses an unknown button name, or is a single button | Settings menu button combo. |
| Monitor number higher than the number of connected monitors | Monitor. |
| A Vulkan GPU is chosen but Graphics API isn't Vulkan, the index doesn't exist, or it's the integrated GPU while a discrete one exists | Graphics card (Vulkan). |
| Estimated VRAM above 90% of the GPU's memory | Render scale. |

Warnings never block saving. They're there to tell you a setting won't do what you might expect.

---

## Advanced page

![Advanced page](docs/screenshots/06-advanced-search.png)

- **Search every setting:** type part of a name, cvar key or description (Ctrl+F jumps here). Matching settings on other pages appear as buttons; clicking one opens that page, scrolls to the card and highlights it. The raw keys further down are filtered as you type too.
- **Add or override a key:** writes any cvar into `settings.toml` on the next save, for options the game supports that the launcher doesn't list. Text values are quoted for you; `true`/`false`, numbers, quoted strings, arrays and tables are written as typed. Queued keys are listed until you save, and **Clear queued** forgets them. If the key already has a card somewhere, the launcher takes you to that card instead.
- **Open settings.toml in Notepad.**
- **Other keys in settings.toml:** one card for every key in your file that no other page covers. For your file today that's `skate3_draw_distance_stream_probe` and the three `vulkan_allow_present_mode_*` keys. True/false keys get a toggle; everything else gets a raw value box. Note that the game rewrites `settings.toml` when it exits, so keys it doesn't recognise may disappear.

---

## Profiles page

![Profiles page](docs/screenshots/07-profiles.png)

- **Save current settings as a profile:** give it a name such as "Ultra 4K" or "Ultrawide" and press **Save profile** (or Enter). This captures everything on screen, *including unsaved changes*, plus every other key in your file.
- **Your profiles:** one row per profile, with these buttons:
  - **Load:** puts the profile's values on screen, unsaved, so you can review them before saving.
  - **Play:** loads, saves and launches the game in one click.
  - 💾 **Update:** overwrites the profile with what's on screen now.
  - 🔗 **Shortcut:** creates a desktop shortcut `Skate 3 - <name>` that launches straight into the game with that profile (`--profile "<name>" --play`).
  - 🗑 **Delete.**
- **Settings history:** every time the launcher is about to overwrite `settings.toml` (Save, PLAY, restoring the original backup, or a profile shortcut), it first copies the current file into the history folder. The newest 15 are kept. **Load** puts any of them back on screen; Save makes it permanent. This is your "revert to last session" button.
- **Original backup:** **Restore original backup** replaces `settings.toml` with `settings.toml.launcher-backup`, the copy taken the very first time the launcher saved. This happens immediately, and the current file goes into history first.

---

## Saves & DLC page

![Saves & DLC page](docs/screenshots/08-saves.png)

- **Save data:** lists each gamer-profile save folder (for example `B13E07DFF9AB6772`) with its size and when it was last saved. Buttons: **Back up now** and **Open save folder**.
- **Automatic backups:** **Back up saves before every launch** is on by default. It zips every save folder into `launcher-backups` each time you press PLAY or use a quick-play shortcut. **Keep the newest** (5 / 10 / 20 / 50, default 10) applies only to *automatic* backups. Manual and pre-restore backups are never deleted for you. A failed backup never stops the game from launching.
- **Backups:** the newest 20 backups, each with its date, type (Automatic / Manual / Before a restore) and size.
  - **Restore:** refused while the game is running, and asks for confirmation. Before replacing anything it **backs up your current saves** as a "Before a restore" backup, so a restore can always be undone. It only touches the save folders inside the zip and never writes outside `%APPDATA%\skate3`.
  - 🗑 **Delete** a backup.
  - **Open backups folder.**
- **DLC:** lists the files in `%APPDATA%\skate3\dlc`, the folder the game loads DLC packages from. **Add DLC files…** copies the packages you choose into it, and **Open DLC folder** opens it. Only use DLC you legally own.

---

## Controller page

![Controller page](docs/screenshots/09-controller.png)

Live controller polling runs only while this page is open.

- **Xbox controllers (XInput):** pick **Slot 1–4** to see that controller's live state. Both sticks, both triggers and all 14 buttons light up as you press them. The line underneath shows which slots are connected. **Test rumble** buzzes the selected controller for about half a second.
- **Other controllers (DirectInput):** PlayStation, Switch and other HID pads, with live axes (X/Y/Z/R as percentages) and the numbers of any buttons being held. Xbox pads show up here too. To use a non-Xbox pad in-game, set **Controller backend** to SDL.
- **Settings menu button combo:** click button chips (a, b, x, y, lb, rb, l3, r3, back, start, dpad_up/down/left/right) to build the combo, or press **Record from controller** and hold the combo for about a second; recording gives up after 8 seconds. Chips glow green while that button is held on a connected Xbox pad. **Clear** disables the combo. This edits the same `menu_chord` setting as the Controls page, so it's saved with Save or PLAY.

---

## Logs page

![Logs page](docs/screenshots/10-logs.png)

- **Last session:** reads the newest log in the game's `logs` folder and tells you whether the game is running, whether the last session **ended cleanly**, or whether the log **stops without the normal shutdown lines** (usually a crash or forced close). It also shows the file, when it was written, the game version and the error/warning counts.
- **Log viewer:**
  - **Log file** dropdown: every `.log` file, newest first, with its age and size.
  - **All / Warnings / Errors** filter, plus a search box.
  - A colour-coded viewer (errors red, warnings yellow) showing the last 20,000 lines. Select lines and press **Ctrl+C** to copy them.
  - **Refresh**, **Open in Notepad**, **Open logs folder**.
- **Bug reports & cleanup:**
  - **Copy bug report:** puts a ready-to-paste report on the clipboard. It contains the launcher and game versions, Windows version, CPU, RAM, GPU list, monitors, the last session's status, your full `settings.toml`, the last 60 warnings/errors and the last 25 lines of the newest log.
  - **Save bug report (.zip):** writes `Skate3-bug-report-<date>.zip` to your desktop (`report.txt` + `settings.toml` + the newest two logs) and highlights it in Explorer.
  - **Delete old logs:** keeps the newest 3 logs and deletes the rest after telling you how much space that frees. The current log-folder size is shown underneath.

**Crash detection:** if the launcher is still running when the game exits with an error code (because Keep launcher open, the tray or Discord presence is on), it pops up the exit code, how long the game ran, and the last few warning/error lines from the log, then offers to open the Logs page.

---

## Launcher page

![Launcher page](docs/screenshots/11-launcher.png)

- **Updates:** shows your installed Skate 3 Recomp version (read from the game's log, or from `skate3.exe` itself) and the latest release on GitHub.
  - **Check now** and **Open releases page**.
  - **Check when the launcher starts:** on by default, and checks at most once every 12 hours.
  - **Release source:** the GitHub `owner/repo` to check. The default is `mchughalex/skate3recomp`; change it if you follow a fork.
  - The launcher **never downloads or installs anything**. It only tells you an update exists (the sidebar badge) and opens the release page.
- **Windows graphics preference for skate3.exe:** Let Windows decide, Power saving or High performance. This is the same per-app setting as Windows Settings → System → Display → Graphics, and it's what decides the GPU under Direct3D 12. It applies immediately; it's stored in your user registry, so no admin rights are needed.
- **Shader & pipeline cache:** shows the size of the shader cache and of the whole `cache` folder.
  - **Clear shader cache** deletes `cache\nrhi_shaders` and `cache\shaders`.
  - **Clear all cache** deletes everything in `cache`.
  - **Open cache folder.**
  - Both clear buttons are refused while the game is running. The cache rebuilds itself, with a little stutter on the first run. Clearing it is the usual fix after a GPU driver or game update.
- **Desktop shortcuts & command line:**
  - **Launcher shortcut** opens the launcher.
  - **Quick-play shortcut** starts the game straight away with your saved settings, skipping the window; saves are still backed up.
  - The available [command-line options](#command-line-and-shortcuts) are listed here too.
- **Discord Rich Presence:** see [below](#discord-rich-presence).
- **Game location & reset:**
  - Shows the game path, the data folder, the launcher version and the game version.
  - **Change game location…** points the launcher at a different `skate3.exe`.
  - **Reset all settings to defaults** puts every setting back to its default on screen; Revert undoes it, and nothing is written until Save.

---

## What happens when you press PLAY

1. If `skate3.exe` hasn't been found, the launcher asks you to locate it (once).
2. Unsaved changes are saved. If the game is somehow already running, you're warned first, because the game rewrites `settings.toml` when it exits.
3. Your saves are backed up (if **Back up saves before every launch** is on), and old automatic backups are trimmed.
4. The game starts. If Discord presence is on, it's switched on.
5. The launcher then does one of these:
   - **Keep launcher open** on: stays open, or goes to the tray if **Use system tray** is on. It comes back when the game exits.
   - Keep open off, but tray or Discord presence on: hides in the background (in the tray if that's on) so presence and crash detection keep working, then **closes itself when the game exits**. Restoring it from the tray cancels the auto-close.
   - Otherwise: closes straight away, exactly like the old launcher.

PLAY greys out and reads **RUNNING** while `skate3.exe` is open.

---

## System tray

With **Use system tray** on:
- Minimizing hides the window to the tray.
- PLAY tucks the launcher into the tray while you play.
- Double-click the icon to bring the window back.
- Right-click it for: **Open launcher**, **Play**, **Play with profile ▸** (every saved profile), and **Exit launcher**.

---

## Discord Rich Presence

While the game runs, presence shows **"Skating in Skate 3"** on your Discord profile with a timer. The second line shows the profile you launched with, or your render resolution and renderer (for example "Recomp · 1440p · enhanced renderer").

It talks to the Discord desktop app over Discord's local connection; no Discord library or account login is involved. Discord needs an **Application ID**, which you set up once:

1. Go to **discord.com/developers/applications**, click **New Application** and name it **Skate 3**. That name is what friends see ("Playing Skate 3").
2. Copy the **Application ID** into the launcher's **Application ID** box.
3. *(Optional)* Under **Rich Presence → Art Assets**, upload a Skate 3 image with the key **skate3**, or type your own key in **Image asset key**.
4. Turn on **Show Skate 3 on my Discord profile** and press **Test connection**. It confirms the connection (showing your Discord name) and shows a test presence for 15 seconds.

Presence also works from quick-play shortcuts, which wait in the background until the game closes.

---

## Command line and shortcuts

```
"Trooper's Skate 3 Recomp Launcher.exe"                          open the launcher
"Trooper's Skate 3 Recomp Launcher.exe" --profile "Ultra 4K"     open with that profile loaded on screen (unsaved)
"Trooper's Skate 3 Recomp Launcher.exe" --play                   skip the window, start the game with the current settings.toml
"Trooper's Skate 3 Recomp Launcher.exe" --play --profile "Ultra 4K"   write the profile to settings.toml, then start the game
"Trooper's Skate 3 Recomp Launcher.exe" --help                   show these options
```

`-play`, `/play`, `-profile`, `/profile`, `-h` and `/?` work too. Profile names aren't case-sensitive. `--play` still backs up your saves and runs Discord presence. If `skate3.exe` can't be found, it opens the normal window so you can locate it.

The **Profiles** and **Launcher** pages create these shortcuts on your desktop for you, with the Skate 3 icon.

---

## Keyboard shortcuts

| Keys | Action |
|---|---|
| **Ctrl+S** | Save |
| **Ctrl+F** | Jump to Advanced → Search |
| **Ctrl+C** (in the log viewer) | Copy the selected log lines |
| Arrow keys / Home / End / Page Up / Page Down | Adjust the focused slider |
| Left / Right | Move the quality-preset picker |
| Space / Enter | Flip the focused toggle or press the focused button |

---

## Files the launcher creates

Everything goes in `%APPDATA%\skate3`, the game's own data folder:

| Path | What it is |
|---|---|
| `settings.toml` | The game's config; edited in place, and only the keys you change. |
| `settings.toml.launcher-backup` | One-time copy of your config from before the launcher first changed it. |
| `launcher.ini` | Launcher-only options (keep open, tray, game path, backup count, update and Discord settings). |
| `launcher-profiles\*.toml` | Your profiles: plain `key = value` files you can share or edit. |
| `launcher-history\settings-<date>.toml` | Last 15 versions of `settings.toml`. |
| `launcher-backups\saves-<date>-<auto/manual/pre-restore>.zip` | Save backups. |

The launcher also writes:
- The Windows GPU preference value for `skate3.exe` in `HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences`, only if you change it.
- Desktop shortcuts and bug-report zips, only when you click those buttons.

It **never** modifies `skate3.exe`, the `game` folder, or anything outside the places listed here.

---

## Building from source

| File | Contents |
|---|---|
| `src/TroopersSkate3Launcher.cs` | Main window, the settings catalog, presets, save/PLAY logic, tray, Discord hand-off, command line. |
| `src/LauncherPages.cs` | The Advanced, Profiles, Saves & DLC, Controller, Logs and Launcher pages and their controls. |
| `src/LauncherServices.cs` | Background services: paths and prefs, GPU detection (Vulkan), save backups, log parsing, update check, Discord IPC, shortcuts, system info, XInput/DirectInput. |
| `src/build.cmd` | Builds the exe with the C# compiler that ships with Windows. |

Double-click **`src\build.cmd`** (or run it from a terminal). It uses `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`, so no Visual Studio or .NET SDK is needed, and writes `bin\Trooper's Skate 3 Recomp Launcher.exe`. The code is C# 5 so that built-in compiler accepts it.

The exe gets the Skate 3 icon only if `src\skate3.ico` exists, or if `skate3.exe` is one folder above `src`, in which case the icon is extracted automatically. The icon is EA artwork, so it isn't committed.

**Releases:** put the release notes in `.github/release-notes/vX.Y.Z.md`, then push the tag. Pushing a tag like `v3.0.1` makes GitHub Actions build the exe on Windows, zip it with the README and LICENSE, and create a **draft** GitHub release with those notes and the zip attached — review it on the Releases page and press Publish. Every push and pull request is also built, and the zip is attached to the run as an artifact.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| "skate3.exe not found" | Keep the launcher in the game folder, or use **Launcher → Change game location…**. |
| My changes were undone | The game rewrites `settings.toml` when it exits. Close the game *before* saving; the launcher warns you if it's running. |
| Game starts on the integrated GPU | With Vulkan: **Audio & System → Graphics card (Vulkan)**. With Direct3D 12: **Launcher → Windows graphics preference → High performance**. |
| Graphics glitches after a driver or game update | **Launcher → Clear shader cache.** |
| The game crashed | **Logs** page: check **Last session**, filter to **Errors**, then **Copy bug report** or **Save bug report (.zip)**. |
| Lost or broken career | **Saves & DLC → Backups → Restore** (your current saves are backed up first). |
| Discord presence doesn't show | The Discord *desktop* app must be running, the Application ID must be right, and Discord → Settings → Activity Privacy → "Share your detected activities" must be on. Use **Test connection**. |
| GPU list says "GPU 0 / 1 / 2" | Neither the Vulkan driver nor a game log could be read. Run the game once with Vulkan so its log lists your GPUs. |

---

## License

[MIT](LICENSE). Skate 3 is © Electronic Arts; the Skate 3 Recomp is by its own authors. This launcher only edits the recomp's config and data files on your PC.
