Bug reports, ideas and pull requests are welcome.

- **Bugs and ideas:** open an [issue](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/issues). For crashes, **Logs → Report on GitHub…** fills in most of the details for you.
- **A new recomp bug:** add a rule to [`known-issues.txt`](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/blob/main/known-issues.txt) (the format is documented at the top of the file). The launcher downloads this list, so a merged rule warns every player without a new release.
- **A bad GPU driver:** add it to [`known-driver-issues.txt`](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/blob/main/known-driver-issues.txt).

## Before you write code

Read [**AGENTS.md**](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/blob/main/AGENTS.md). It applies to AI coding agents and human contributors alike. The short version:

1. **C# 5 only.** The build uses the compiler that ships with Windows. No string interpolation, `?.`, `nameof`, `=>` members, `out var` or tuples.
2. **No NuGet packages or extra DLLs.** Only the .NET Framework assemblies Windows already has.
3. **Never guess recomp facts.** Every setting name, key name and log line comes from the recomp's source. Say where you found it (file and line) in the PR.
4. **Don't surprise players.** Only rewrite the settings that changed, never write into `game\`, back up before replacing saves, and confirm anything destructive.
5. **Branch and PR.** Work on `feature/<name>` or `fix/<name>`, never on `main`. The CI build on the PR must be green.

## Pull requests

Every PR description should say:

- what changed and why,
- where each recomp fact came from (file and line),
- what you tested and how,
- a **"Needs testing on Windows"** list for anything that needs the real game.

Update the README (the full user guide) and this wiki when you change how something works. See [[Building from Source]] to build and test locally.
