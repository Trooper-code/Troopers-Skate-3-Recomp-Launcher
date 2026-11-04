## What you need

- **Windows 10 or 11.** The launcher runs on the .NET Framework that every Windows 10/11 PC already has, so there's nothing else to install.
- **Your own copy of the [Skate 3 Recomp](https://github.com/mchughalex/skate3recomp)**, with `skate3.exe` in a folder you can write to. (The launcher can install and update the recomp's files for you once it's set up. It never provides the game itself.)
- For mods: the recomp folder must be on an **NTFS** drive. Most internal drives are.

## Install

1. Download `TroopersSkate3Launcher-vX.Y.Z-win.zip` from [**Releases**](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/releases).
2. Extract `Trooper's Skate 3 Recomp Launcher.exe` into your Skate 3 Recomp folder, next to `skate3.exe` (a subfolder of it works too).
3. Run it.

The launcher finds the game automatically. If it can't, **PLAY** asks you to locate `skate3.exe` once and remembers it. You can change it later under **Launcher → Change game location…**.

## First run

On startup the launcher quietly checks your install. If something's missing (the game files aren't installed, Title Update 3 isn't staged, and so on), it opens the **Game files** page with a checklist telling you what to do. See [[Game Files and Recomp Updates]].

Then:

1. Change whatever you like. Pick a **Quality preset** in the header if you want a quick start. Cards with unsaved changes get an orange strip down their left edge.
2. Press **PLAY**. That saves your changes, backs up your saves and starts the game. ([[What Happens When You Press PLAY]] has the details.)

## "Windows protected your PC" or a virus warning

The launcher isn't code-signed, and every version is a brand-new file nobody has downloaded before. So:

- **SmartScreen** may show "Windows protected your PC" on the first launch. Click **More info → Run anyway**.
- **Your browser or antivirus** may flag the download. The launcher downloads and unpacks recomp updates, edits Steam's shortcut file and starts the game, which looks like installer behaviour to scanners.

Every release is built by GitHub Actions straight from the public source, so you can check exactly what's in it. See [[FAQ]] for how to check a download's SHA-256.

## Updating the launcher

Download the new zip and replace the old exe. Your settings, profiles, backups and mods aren't stored in the exe, so they carry over. After an update, a **What's new** pop-up shows the changes once.

## Uninstalling

Delete the exe. If you want to remove everything it made too, see [[Files the Launcher Creates]].
