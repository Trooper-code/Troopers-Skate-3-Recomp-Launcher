Everything goes in the game's own user-data folder: `%APPDATA%\skate3` normally, or the game folder itself in [portable mode](Portable-Installs). `launcher.ini` and the downloaded known-issues list always stay in `%APPDATA%\skate3`, so the launcher can find the game.

| Path | What it is |
|---|---|
| `settings.toml` | The game's config; edited in place, and only the keys you change. |
| `settings.toml.launcher-backup` | One-time copy of your config from before the launcher first changed it. |
| `launcher.ini` | Launcher-only options (keep open, tray, game path, backup count, update and Discord settings). Always in `%APPDATA%\skate3`. |
| `launcher-known-issues.txt` | Cached copy of the known-issues list. Always in `%APPDATA%\skate3`. |
| `launcher-profiles\*.toml` | Your profiles: plain `key = value` files you can share or edit. |
| `launcher-history\settings-<date>.toml` | Last 15 versions of `settings.toml`. |
| `launcher-backups\saves-<date>-<auto/manual/pre-restore>.zip` | Save backups. |
| `launcher-benchmarks.csv` | Benchmark results. |
| `launcher-game-baseline.txt` | Your own game-file hashes, if you saved a baseline. |
| `settings.toml.benchmark-backup` | Only exists while a benchmark is running. |

Next to `skate3.exe`:

| Path | What it is |
|---|---|
| `mods\<mod name>\` | Installed mods. `mods\modlist.txt` is the load order. |
| `mods\.active-game\` | The modded game folder (hard links), rebuilt when your mods change. |
| `recomp-backups\` | The files each recomp update replaced, for rollback (newest 3 kept). |

The launcher also writes:
- The Windows GPU preference value for `skate3.exe` in `HKEY_CURRENT_USER\Software\Microsoft\DirectX\UserGpuPreferences`, only if you change it.
- Desktop shortcuts and bug-report zips, only when you click those buttons.
- Steam's `shortcuts.vdf` and grid artwork, only when you use **Add to Steam**.
- The Windows crash-dump setting for `skate3.exe` (`HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\skate3.exe`), only when you click **Keep crash dumps** and approve the admin prompt.

It **never** modifies the `game` folder. It only replaces `skate3.exe` and the recomp's other files when you install or roll back an update on the Game files page.
