The recomp has two ways to keep its data next to `skate3.exe` instead of in `%APPDATA%\skate3`, and the launcher (v3.1.1+) follows both:

- **`portable.txt`** next to `skate3.exe`: `settings.toml`, the cache and DLC live in the game folder. The launcher edits that `settings.toml` and keeps its profiles, history, backups and benchmark results there too.
- **A `saves` folder** next to `skate3.exe`: your career saves live in `saves\<profile id>\`. Save backups and restores use that folder.

The Launcher page (**Game location & reset**) shows which folders are in use. Save backups remember which layout they came from, because the two are stored differently: a backup made from the normal save folder can't be restored into the portable `saves` folder, and vice versa. If you change the game location to a folder with a different setup, the launcher restarts to load it.
