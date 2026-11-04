### Is this official?

No. It's an unofficial companion app by Trooper-code, not affiliated with the Skate 3 Recomp developers or EA.

### Does it include the game?

No. The launcher contains **no game files and no recomp files**. You need your own copy of the [Skate 3 Recomp](https://github.com/mchughalex/skate3recomp) and the game. The launcher can download official recomp releases from GitHub for you (see [[Game Files and Recomp Updates]]), but never the game.

### My browser or antivirus says the download is a virus

That's a false positive. The exe isn't code-signed and each version is brand new, so download scanners don't trust it yet. What it does (downloading and unpacking recomp updates, editing Steam's shortcut file, making hard links, starting the game) also looks like installer behaviour to them.

Every release and every build is made by GitHub Actions straight from the public source. To check a download is exactly what GitHub built:

1. Keep the file (in Chrome, open the downloads list and choose to keep it).
2. In PowerShell, run `Get-FileHash "$env:USERPROFILE\Downloads\<file>.zip"`.
3. Compare the hash with the **digest** shown on the GitHub page you downloaded it from (the Actions run's Artifacts list).

You can also upload it to [VirusTotal](https://www.virustotal.com). One or two hits from lesser-known scanners is a typical false positive.

### Windows says "Windows protected your PC"

Click **More info → Run anyway**. It's SmartScreen being cautious about an unsigned exe.

### Will it mess up my game or my saves?

It's built not to. It **never** changes the `game` folder (mods use a hard-link copy), it backs up your saves every time you press PLAY, and every restore backs up your current saves first. `settings.toml` is snapshotted before every change. See [[Files the Launcher Creates]].

### Does it work with Skate 3 Recompiled+?

Mostly, yes. The launcher detects Recompiled+ and protects its `mods\skate2` folder. Recompiled+-only settings aren't on the settings pages yet, but you can edit them on the [[Advanced Page]]. Updates check `mchughalex/skate3recomp` by default; you can change **Release source** on the [[Launcher Page]].

### Does it work with portable installs?

Yes, since v3.1.1. It follows `portable.txt` and the `saves` folder the same way the recomp does. See [[Portable Installs]].

### Will there be multiplayer?

**No, not planned.** Networking is beyond the scope of this launcher. If someone builds multiplayer for the recomp, the launcher will probably add support for their tool.

### Can I use PS3 mods?

No. The recomp runs the Xbox 360 version, and PS3 mods use different file formats.

### Does it work on Linux or Steam Deck?

The launcher is a Windows app. **Add to Steam** on the [[Launcher Page]] makes it work in Steam's Big Picture mode, and it can be driven with a controller. Running it under Proton or Wine isn't officially supported.

### Where do I report a bug?

Use **Logs → Report on GitHub…** in the launcher. It asks whether the problem is with the game or the launcher, then opens a pre-filled issue in the right project. Or open an issue [here](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/issues) directly.
