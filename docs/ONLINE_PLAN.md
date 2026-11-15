# Skate 3 Online Implementation Plan

Each milestone is sized for its own feature branch and PR. **Do not start launcher UI work until M1 (Recomp) proves the game can reach a local Blaze server.**

---

## Milestone 1: Recomp Networking & Host Redirect (Upstream PR to `skate3recomp` / `rexglue-skate3`)
- **Repos touched:** `mchughalex/rexglue-skate3` (`skate3-sdk-clean`), `mchughalex/skate3recomp`
- **Scope:**
  1. Implement `NetDll_XNetDnsLookup_entry` (`src/kernel/xam/xam_net.cpp`) using real host DNS (`getaddrinfo`), with a configurable host-override map (e.g. `net_host_overrides` cvar in `skate3.toml` / `settings.toml` mapping `gosredirector.ea.com` and `downloads.skate.online.ea.com` to a target IP).
  2. Implement `NetDll_XNetGetTitleXnAddr_entry` to return the host's active LAN IPv4 instead of `127.0.0.1` when online is enabled.
  3. Test booting `skate3.exe` with `user_live_signed_in = true` pointed at a local `Skateboard3Server` instance to observe:
     - Whether `:42100` uses ProtoSSL or plain TCP.
     - Exact `Authentication` command ID and TDF fields sent by the Xbox 360 TU3 binary.
     - Exact HTTP paths requested on `:80`.
- **Upstream PR:** Send `XNetDnsLookup` + `net_host_overrides` cvar to `mchughalex/rexglue-skate3` and `mchughalex/skate3recomp`.
- **Acceptance checks:**
  - `skate3.exe` resolves `gosredirector.ea.com` without admin rights or `hosts` file edits.
  - `Skateboard3Server` logs an incoming TCP connection on `:42100` from `skate3.exe`.

---

## Milestone 2: Xbox 360 Auth & ProtoSSL Compatibility (Blaze Server + Optional Recomp Hook)
- **Repos touched:** `Skateboard3Server` fork (and `skate3recomp` if ProtoSSL cert bypass is needed)
- **Scope:**
  1. If `:42100` requires ProtoSSL: enable `BlazeSslServerMiddleware` on `:42100` in `Program.cs` (or add a ProtoSSL cert-check bypass hook in `skate3recomp` if the 360 client rejects self-signed certs).
  2. Add an Xbox 360 login handler in `Skateboard3Server.Blaze/Handlers/Authentication/` matching the captured 360 command ID and TDF fields, using `PersonaExternalIdType.Xbox` / `ExternalIdType.Xbox` (gated by `EnableTicketValidation = false` or an `AllowInsecureXboxLogin` flag).
  3. Add `/skate3/webkit/XBL2/...` route aliases in `Skateboard3Server.Web` if the 360 client requests `XBL2` WebKit URLs (confirmed via `Startup.cs` 404 logger).
- **Upstream PR:** Submit Xbox 360 login + `XBL2` web route support to active `Skateboard3Server` fork / upstream.
- **Acceptance checks:**
  - `skate3.exe` completes `ServerInfo` (`:42100`), `PreAuth`, `XboxLogin`, `PostAuth`, and fetches `/skate3/config/XBL2.xml`.
  - In-game EA Nation / Online menus open without "unable to connect" errors.

---

## Milestone 3: Launcher Server Picker & Online Settings (`Troopers-Skate-3-Recomp-Launcher`)
- **Repos touched:** `Trooper-code/Troopers-Skate-3-Recomp-Launcher` (`feature/online-server-picker`)
- **Scope:**
  1. Add an **Online** card/page in the launcher (C# 5, owner-drawn `Theme` UI) with:
     - Server mode: **Off (Offline)** / **Community Server** / **Custom IP** / **Local (`127.0.0.1`)**.
     - Custom server IP/hostname input.
     - Toggle for Xbox Live sign-in (`user_live_signed_in` in `settings.toml`).
  2. Store launcher-side server choice in `launcher.ini` (`LauncherPrefs`) and write `user_live_signed_in` + `net_host_overrides` into `settings.toml` on Save / Play.
  3. Add plain-English firewall & port help text.
  4. Update `README.md` and release notes.
- **Acceptance checks:**
  - Selecting a server mode updates `settings.toml` cleanly without touching unrelated keys.
  - Launching `skate3.exe` connects to the selected server with zero admin prompts.

---

## Milestone 4: Matchmaking & P2P Verification
- **Repos touched:** `mchughalex/rexglue-skate3`, `Skateboard3Server` fork
- **Scope:**
  1. Implement `NetDll_XNetXnAddrToInAddr` / `XNetInAddrToXnAddr` (and `XNetQos*` stubs if required) in `rexglue-sdk` so two PCs can exchange `PairNetworkAddress` via `GameSetupNotification`.
  2. Verify `CreateGame`, `StartMatchmaking`, `UpdatePlayerConnection`, and UDP P2P gameplay between two `skate3.exe` instances on LAN and over WAN.
- **Upstream PR:** Submit XNet P2P address translation fixes to `mchughalex/rexglue-skate3`.
- **Acceptance checks:**
  - Two `skate3.exe` clients matchmake into the same session and skate together in Free Skate / Online Challenge.

---

## Milestone 5 (Optional): One-Click "Host Local Server" in Launcher
- **Repos touched:** `Trooper-code/Troopers-Skate-3-Recomp-Launcher` (`feature/online-local-server`)
- **Scope:**
  1. Add a **Download / Start Local Server** button that fetches a self-contained Windows x64 build of the Xbox-enabled `Skateboard3Server` into a `server\` folder next to the launcher (keeping the launcher a portable single `.exe`).
  2. Generate `appsettings.Development.json` with the user's chosen `PublicHost` / `PublicIp` and manage the server process lifecycle (cleanly stop on launcher exit or button click).
  3. Detect port `80` conflicts (`http.sys` / IIS) and show a plain-English fix if binding fails.
- **Acceptance checks:**
  - User can click **Start Local Server**, launch `skate3.exe` pointed at `127.0.0.1`, and play without installing .NET 6.
