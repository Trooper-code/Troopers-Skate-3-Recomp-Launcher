1. If `skate3.exe` hasn't been found, the launcher asks you to locate it (once).
2. Unsaved changes are saved. If the game is somehow already running, you're warned first, because the game rewrites `settings.toml` when it exits.
3. Your saves are backed up (if **Back up saves before every launch** is on), and old automatic backups are trimmed.
4. If any mods are enabled, the modded game folder is built (or reused if nothing changed). Anything the mod loader wants you to know is shown before the game starts.
5. The game starts. If Discord presence is on, it's switched on.
6. The launcher then does one of these:
   - **Keep launcher open** on: stays open, or goes to the tray if **Use system tray** is on. It comes back when the game exits.
   - Keep open off, but tray or Discord presence on: hides in the background (in the tray if that's on) so presence and crash detection keep working, then **closes itself when the game exits**. Restoring it from the tray cancels the auto-close.
   - Otherwise: closes straight away, exactly like the old launcher.

PLAY greys out and reads **RUNNING** while `skate3.exe` is open.
