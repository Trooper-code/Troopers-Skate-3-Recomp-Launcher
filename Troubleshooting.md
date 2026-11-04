| Problem | Fix |
|---|---|
| "skate3.exe not found" | Keep the launcher in the game folder, or use **Launcher → Change game location…**. |
| My changes were undone | The game rewrites `settings.toml` when it exits. Close the game *before* saving; the launcher warns you if it's running. |
| Game starts on the integrated GPU | With Vulkan: **Audio & System → Graphics card (Vulkan)**. With Direct3D 12: **Launcher → Windows graphics preference → High performance**. |
| Graphics glitches after a driver or game update | **Launcher → Clear shader cache.** |
| The game crashed | **Logs** page: check **Last session**, filter to **Errors**, then **Copy bug report** or **Save bug report (.zip)**. |
| Lost or broken career | **Saves & DLC → Backups → Restore** (your current saves are backed up first). |
| Discord presence doesn't show | The Discord *desktop* app must be running, the Application ID must be right, and Discord → Settings → Activity Privacy → "Share your detected activities" must be on. Use **Test connection**. |
| The game won't boot after an update | **Game files → Roll back.** |
| "Title Update 3 staged" is marked ⚠ | Start the game once with an internet connection; it downloads and installs the update itself. |
| A mod doesn't show up in-game | Check it's enabled and lower in the list than any mod that replaces the same file, and that it's an Xbox 360 mod. |
| "Mods need NTFS" | Move the whole Skate 3 Recomp folder to an NTFS drive (most internal drives are). |
| The launcher edits settings the game ignores | You're probably using a portable install with launcher v3.1.0 or older. Update to v3.1.1, which follows `portable.txt` and the `saves` folder. |
| The benchmark says the game didn't reach gameplay | Make sure the game boots normally first (Game files page), then try the Spawn point route. |
| Shadows swim or cast in the wrong place | You're on Vulkan with Full-resolution SSAO on or a non-default Static sun shadow resolution. Switch **Graphics API** to **Direct3D 12**. See [Known issues](Warnings-and-Known-Issues#known-recomp-issues). |
| GPU list says "GPU 0 / 1 / 2" | Neither the Vulkan driver nor a game log could be read. Run the game once with Vulkan so its log lists your GPUs. |
| My browser or antivirus flags the download | A false positive on an unsigned exe. See [[FAQ]] for how to check the file. |
