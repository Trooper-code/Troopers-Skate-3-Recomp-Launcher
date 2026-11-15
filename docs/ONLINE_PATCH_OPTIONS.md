# Online Patch Options: Bringing 5C8's Online Fix to Official `mchughalex` Users

**Question:** The `5C8/skate3recomp` build can reach a custom Blaze server; the official `mchughalex/skate3recomp` build cannot. Can Trooper's Skate 3 Recomp Launcher give official-build users the same online fix? If so, what is the best way, and what would it cost?

**Comparison baseline:**
- Official recomp: `mchughalex/skate3recomp` release `v2.0.2` (commit `f6e0ae8`), pinning `mchughalex/rexglue-skate3` branch `skate3-sdk-clean` at `v0.8.1.19` (commit `7eb0faf`).
- 5C8 recomp: `5C8/skate3recomp` release `v1.0.9-skate3.20261111` (commit `4034fe7`), pinning `5C8/rexglue-skate3` at `v0.8.2.95-skate3.20261029` (commit `e948aee`).

*Line numbers in section 1A re-checked against `5C8/rexglue-skate3` `e948aee` and `mchughalex/rexglue-skate3` `7eb0faf` on 2026-11-14.*

**Shipped binary mapping (verified from build files):**
- **`rexruntime.dll`**: Built from `rexglue-skate3` via `add_library(rexruntime SHARED ${REXSYSTEM_SOURCES})` (`mchughalex/rexglue-skate3` `7eb0faf`, `src/system/CMakeLists.txt:48`) plus `target_sources(rexruntime PRIVATE ${REXKERNEL_SOURCES})` (`src/kernel/CMakeLists.txt:67`). All `src/kernel/xam/*` and `src/system/*` changes ship inside `rexruntime.dll`.
- **`skate3.exe`**: Built from `skate3recomp` via `add_executable(skate3 WIN32 ${SKATE3_SOURCES})` (`mchughalex/skate3recomp` `f6e0ae8`, `CMakeLists.txt:276`; `5C8/skate3recomp` `4034fe7`, `CMakeLists.txt:308`). `src/skate3_online.cpp` (`4034fe7`, `CMakeLists.txt:260`) and `src/skate3_app_common.cpp` ship inside `skate3.exe`.

---

## 1. Exact Changes 5C8 Made for Online Play

### A. SDK Changes (`5C8/rexglue-skate3` `e948aee` vs `mchughalex/rexglue-skate3` `7eb0faf`) -> `rexruntime.dll`

| Area | What 5C8 changed (`file:line`) | Official `7eb0faf` state | Approx. size | Dependencies |
|---|---|---|---|---|
| **`XNetDnsLookup` + `net_host_overrides`** | Added `net_host_overrides` string cvar (`src/kernel/xam/xam_net.cpp:60-66`), `HostOverrideFor()` parser (`981-1018`), and async `getaddrinfo()` resolution on a detached `std::thread` in `NetDll_XNetDnsLookup_entry` (`1020-1080`). | `NetDll_XNetDnsLookup_entry` unconditionally sets `dns->status = 1` (`src/kernel/xam/xam_net.cpp:509-523`). | ~115 lines | Standalone (`ws2tcpip.h` / `netdb.h`). |
| **`XNetGetTitleXnAddr`** | Added `LocalIPv4()` UDP-probe helper (`src/kernel/xam/xam_net.cpp:219-256`) and updated `NetDll_XNetGetTitleXnAddr_entry` to return real LAN IPv4, `wPortOnline = 3074`, unique `abEnet` MAC, and `ETHERNET\|STATIC\|GATEWAY\|DNS\|ONLINE` flags (`849-878`). | Returns `127.0.0.1`, `0xCC` MAC, and `XNET_GET_XNADDR_STATIC` (`src/kernel/xam/xam_net.cpp:435-457`). | ~70 lines | Standalone. |
| **`XNADDR` conversions** | Implemented `NetDll_XNetXnAddrToInAddr_entry` (copies `xn_addr->ina`, `src/kernel/xam/xam_net.cpp:901-908`) and `NetDll_XNetInAddrToXnAddr_entry` (zeroes `XNADDR`, stores `ina` & `inaOnline`, `911-920`). | Both stubs return `1` (`src/kernel/xam/xam_net.cpp:478-488`). | ~20 lines | Depends on `XNetGetTitleXnAddr` returning a real IP. |
| **`XNetConnect` / `XNetGetConnectStatus` / Keys / LSP** | Implemented `XNetCreateKey` (random via `FillRandom`), `XNetRegisterKey`/`UnregisterKey` (`0`), `XNetConnect` (`0`), `XNetGetConnectStatus` (`2` = `CONNECTED`), `XNetServerToInAddr` (`WSAEINVAL` so titles fall back to DNS) (`src/kernel/xam/xam_net.cpp:945-970`, exports at `1620-1626`). | All `REX_EXPORT_STUB`, including `XNetConnect` (`src/kernel/xam/xam_net.cpp:1030-1043`). | ~35 lines | `XNetCreateKey` uses `FillRandom` (`206-214`). |
| **QoS** | Added `NetDll_XNetQosLookup_entry` returning `0x2726` (`WSAEINVAL`) so QoS probes skip cleanly (`src/kernel/xam/xam_net.cpp:973-975`, export at `1627`). | `REX_EXPORT_STUB` (`src/kernel/xam/xam_net.cpp:1040`). | ~5 lines | Standalone. |
| **Live sign-in & privileges** | Flipped `user_live_signed_in` default to `true` and added `DefaultGamertag()` / `DefaultXuid()` (`0x0009...` per-machine FNV-1a XUID) (`src/system/xam/user_profile.cpp:32`, `74-112`). Updated `XamUserCheckPrivilege_entry` to grant privileges when `is_live_signed_in()` is true (`src/kernel/xam/xam_user.cpp:435`). Added `XGISessionJoinRemote` (`0x000B0013`) returning `X_E_SUCCESS` (`src/kernel/xam/apps/xgi_app.cpp:149-158`). | `user_live_signed_in` defaults to `false` (`user_profile.cpp:30`), fixed default XUID/name (`58-68`), `XamUserCheckPrivilege` always sets `*out_value = 0` (`xam_user.cpp:434`), `0x000B0013` unhandled. | ~75 lines | Standalone. On the official build the launcher could set `user_live_signed_in = true`, but can't fix `XamUserCheckPrivilege` without an SDK change. On 5C8 each profile sets `user_live_signed_in` (`5C8/skate3recomp` `src/skate3_user_settings.cpp:187-193`), so a `settings.toml` value is overridden. |
| **Overlapped sockets, `WSAWaitForMultipleEvents`, `XNetRandom`, `XSocket` byte-order** | Added `FillRandom` for `XNetRandom` (`xam_net.cpp:206-214, 585-591`), `NativeSocketError()` (`261-303`), `OverlappedReceive` + `PollOverlappedReceives` (`340-472`) for `WSARecvFrom`/`WSARecv`/`WSAGetOverlappedResult` (`650-704`), fixed `WSAWaitForMultipleEvents` wait-type (`wait_all ? 0 : 1`) and `-10000` tick conversion (`753-788`), added `getpeername`/`getsockname`/`getsockopt` (`1532-1570`). Fixed `XSocket::SendTo` (`htons`/`htonl`) and `RecvFrom` (`ntohs`) byte-swapping, added `Bind` ephemeral UDP fallback and `Connect` 5s timeout (`src/system/xsocket.cpp:124-352`, `include/rex/system/xsocket.h:145`). | `XNetRandom` writes `0xBB`; `WSARecv` is stubbed and `WSARecvFrom` returns `-1`; `WSAWaitForMultipleEvents` passes raw `wait_all` and ms; `XSocket::SendTo`/`RecvFrom` byte-swap ports/IPs on little-endian hosts. | ~650 lines | Required for DirtySock TCP/UDP and P2P gameplay. |
| **Module unload key fix** | `recompiled_key_by_module_path_` map in `KernelState::LoadUserModule` / `UnloadUserModule` (`src/system/kernel_state.cpp:719, 778-788`, `include/rex/system/kernel_state.h:376`) so dynamically loaded modules (e.g., EAWebKit) can be unloaded and reloaded cleanly. | `UnloadUserModule` looks up `module->path()` directly, leaving the module registered. | ~15 lines | Standalone. |

### B. App Changes (`5C8/skate3recomp` `4034fe7` vs `mchughalex/skate3recomp` `f6e0ae8`) -> `skate3.exe`

| Area | What 5C8 changed (`file:line`) | Official `f6e0ae8` state | Approx. size | Dependencies |
|---|---|---|---|---|
| **Forced-insecure-transport hook** | `ForceInsecureTransport` + `REX_FUNC(rex_Sk8_Net_Core_EnablePresentation_QAAX_N_Z_8272C7E0)` zeroes `*(config + 488)` so the game selects `-nosecure` instead of SSL/LSP (`src/skate3_online.cpp:38-57`), paired with symbol name `"0x8272C7E0"` in `config/skate3_function_names.toml:2358`. | Not present (`src/skate3_online.cpp` does not exist; `0x8272C7E0` is unnamed). | ~25 lines + 1 TOML line | Needed so retail doesn't hang on "Connecting to EA Nation..." waiting for SSL/LSP. |
| **LIVE logon & XMsg overrides** | Overrides `__imp__XNetLogonGetTitleID` (`src/skate3_online.cpp:82-87`), `__imp__XNetLogonGetMachineID` (`90-98`), `__imp__XMsgStartIORequest` for `XLIVEBASE 0x00058009` (`XContentGetMarketplaceCounts` -> 0 offers, `196-229`), and `__imp__XMsgInProcessCall` for `XLIVEBASE 0x00058007` (`XOnlineGetServiceInfo` -> `gosredirector.ea.com` IP + `:42100`, `409-433`). | `XNetLogonGetTitleID` is a stub returning garbage; `0x00058009` fails; `0x00058007` is hard-wired to `ERROR_CONNECTION_INVALID` (`0x80151802`, `xlivebase_app.cpp:46` in `7eb0faf`). | ~120 lines | `XOnlineGetServiceInfo` calls `ResolveOverride("gosredirector.ea.com")` reading `net_host_overrides` from the SDK. |
| **`NetDll_connect` IPv4 literal remap** | Overrides `__imp__NetDll_connect` to remap hardcoded `10.10.237.194` (EA's 360 web/SOAP/EAWebKit IP) via `net_host_overrides` (`src/skate3_online.cpp:283-309`, `src/skate3_app_common.cpp:125-128`). | Not present (game tries to connect to `10.10.237.194` directly). | ~30 lines | Depends on `net_host_overrides` cvar in SDK. |
| **`XStringVerify` profanity bypass** | `REX_FUNC(sub_82F39B60)` returns `S_OK` for all strings (`src/skate3_online.cpp:175-194`). | Not present (`XLIVEBASE 0x5000C` fails, causing Team Name checks to fail). | ~20 lines | Standalone. |
| **EAWebKit `webcache` overlay** | `Skate3BaseApp::InstallWebcacheOverlay()` mounts `cache_root()/webcache` for `d:\webcache\` and `webcache\` (`src/skate3_app_common.cpp:1344-1370`). | Not present (in-game EAWebKit pages render black). | ~30 lines | Standalone. |
| **Club/Team crash guards** | `REX_FUNC(sub_82F0E988)` guards invalid `this` pointer (`src/skate3_online.cpp:734-748`). *(Note: `SkipStaleTeamInterfaceCall` and `GuardNull*` at `459-703` are compiled in `skate3_online.cpp`, though their `[[midasm_hook]]` entries are not present in `manifests/skate3.toml.in` at `4034fe7`.)* | Not present. | ~15 lines active | Standalone. |

---

## 2. Delivery Options (A–F) Evaluation

| Option | How it works | Effort | Fragility across updates | `AGENTS.md` compliance | Licence / Legal |
|---|---|---|---|---|---|
| **A. Upstream PR to `mchughalex`** | Port SDK fixes (`rexglue-skate3`) and app hooks (`skate3recomp`) upstream with 5C8's agreement. Launcher just writes `net_host_overrides` & `user_live_signed_in` to `settings.toml`. | **M–L** (PR review + cleanup of debug traces) | **Low** (maintained upstream) | **Compliant** (launcher stays 1 `.exe`, edits `settings.toml` only) | **SDK (BSD-3-Clause): Allowed.** **App (`skate3_online.cpp`): Needs 5C8's permission** (or clean-room reimplementation by `mchughalex`). |
| **B. Launcher supports 5C8 as 2nd flavour** | Extend `RecompInstaller` (`src/GameServices.cs:265-300`) to offer `5C8/skate3recomp` (`skate3recomp-Windows-x64.zip` already matches `Latest()`). Online = "use 5C8 build". | **S** (launcher-side repo picker + `settings.toml` writer) | **Low** (uses 5C8's own releases) | **Compliant** (same `RecompInstaller` pattern already used for official releases) | **Allowed** to download from GitHub Releases directly to user's PC (launcher does not ship recomp binaries). Asking 5C8 first is good etiquette. |
| **C. Binary patch on official `skate3.exe` / `rexruntime.dll`** | Launcher copies official binaries and applies byte patches at fixed offsets with SHA-256 checks. | **XL / Impractical** | **Extreme** (breaks on every `v2.0.x` build) | **Borderline / Violates spirit** (embedding machine-code blobs derived from recomp code) | **Not allowed** for `skate3.exe` patches without `mchughalex` & `5C8` permission; SDK-only patches would still be huge x64 code caves. |
| **D. Separate repo publishing patched official builds** | Fork `mchughalex/skate3recomp`, apply 5C8's patches, publish Windows zips for `RecompInstaller` to download. | **M** (build + maintain fork) | **Medium** (rebase on every `v2.0.x` tag) | **Compliant** on launcher side | **Not allowed** without explicit permission from both `mchughalex` and `5C8` (neither `skate3recomp` repo has an open-source `LICENSE`). |
| **E. Hook / proxy DLL next to `skate3.exe`** | Ship a proxy DLL (e.g., `winhttp.dll` / `rexruntime` wrapper) that detours `rexruntime.dll` exports and `skate3.exe` functions. | **L–XL** | **High** (internal `skate3.exe` functions need signature scans) | **Breaks `AGENTS.md:7` & `9`** ("One `.exe`, no extra DLLs", never contains recomp code) | **Not allowed** if embedding `5C8/skate3recomp` hook logic without permission. |
| **F. Existing config switch / Windows `hosts` file** | Write `user_live_signed_in = true` or edit `C:\Windows\System32\drivers\etc\hosts`. | **S** | **N/A (Does not work)** | `hosts` breaks `AGENTS.md:46` (requires UAC admin) | **Allowed**, but **technically impossible** on official `v2.0.2`. |

### Why Option C (Binary Patch) Is Not Viable
Replacing `NetDll_XNetDnsLookup_entry` (`7eb0faf`, `xam_net.cpp:509-523`) is not a byte flip or `NOP`: it requires adding ~115 lines of C++ (`std::thread`, `getaddrinfo`, `net_host_overrides` TOML cvar registration), plus ~650 lines of overlapped socket/byte-order fixes in `rexruntime.dll` and ~225 lines of `REX_FUNC` / `__imp__*` hooks in `skate3.exe`. Authoring and relocating that much raw x64 machine code as byte patches at fixed offsets is brittle and unmaintainable.

### Why Option F (`hosts` File / Config Switch) Cannot Work on Official `v2.0.2`
1. `net_host_overrides` does not exist in `mchughalex/rexglue-skate3` (`7eb0faf`) or `mchughalex/skate3recomp` (`f6e0ae8`).
2. `NetDll_XNetDnsLookup_entry` (`7eb0faf`, `xam_net.cpp:509-523`) never calls Windows DNS or `getaddrinfo()` — it unconditionally returns `dns->status = 1`.
3. Even if DNS worked, `XamUserCheckPrivilege_entry` (`7eb0faf`, `xam_user.cpp:434`) always denies privileges (`*out_value = 0`), `XOnlineGetServiceInfo` (`0x00058007`, `xlivebase_app.cpp:46`) returns `ERROR_CONNECTION_INVALID`, `10.10.237.194` is a hardcoded IPv4 literal, and `ForceInsecureTransport` is absent.

---

## 3. Licences and Permission

| Repository | Licence Found | Source / Evidence | What is allowed |
|---|---|---|---|
| **`mchughalex/rexglue-skate3`** (`7eb0faf`) | **BSD 3-Clause** | `LICENSE:1-29` (Tom Clay 2026; derived from Xenia, Ben Vanik 2022) | Forking, modifying, and redistributing source or binaries (`rexruntime.dll`), provided the BSD-3-Clause copyright notice and disclaimer are retained. |
| **`5C8/rexglue-skate3`** (`e948aee`) | **BSD 3-Clause** | `LICENSE:1-29` (identical Xenia/ReXGlue BSD-3-Clause text) | Porting 5C8's SDK changes (`xam_net.cpp`, `xsocket.cpp`, `xam_user.cpp`, `user_profile.cpp`, `xgi_app.cpp`, `kernel_state.cpp`) upstream to `mchughalex/rexglue-skate3` **is allowed** under BSD-3-Clause (retain copyright/notices). |
| **`mchughalex/skate3recomp`** (`f6e0ae8`) | **None (All Rights Reserved by default)** | No `LICENSE` file in repo; GitHub API returns no SPDX licence; `README.md` has no licence grant. | **Cannot** redistribute modified `mchughalex/skate3recomp` builds or source forks (Options D/C) without `mchughalex`'s permission. |
| **`5C8/skate3recomp`** (`4034fe7`) | **None (All Rights Reserved by default)** | No `LICENSE` file in repo; `THIRD_PARTY_NOTICES.md:8-9` states: *"This snapshot does not impose a new blanket license on upstream files that did not have one."* | **Cannot** copy `src/skate3_online.cpp` or `src/skate3_app_common.cpp` into an upstream PR (Option A) or another repo (Option D) without **5C8's explicit permission** (or `mchughalex` writing a clean-room equivalent). **Downloading 5C8's own GitHub releases directly to the user's PC via the launcher (Option B) is allowed**, as the launcher neither copies nor redistributes 5C8's code or binaries. |

---

## 4. Recommendation

1. **Short-term (immediate, zero recomp-code risk): Pursue Option B** — add `5C8/skate3recomp` as an optional second recomp flavour in `RecompInstaller` (`src/GameServices.cs:265-300`), clearly labelled as 5C8's online-enabled fork, and have the launcher write `net_host_overrides` to `settings.toml`. Live sign-in on 5C8 comes from the selected profile in `profiles\profiles.toml` (`5C8/skate3recomp` `src/skate3_user_settings.cpp:187-193`), so the launcher should turn it on there, not in `settings.toml`, where 5C8 would override it. It obeys every `AGENTS.md` rule, requires zero binary patching, and works today.
2. **Long-term (best for official-build users): Pursue Option A** — ask `5C8` and `mchughalex` if they are open to upstreaming the BSD-3-Clause `rexglue-skate3` networking fixes and either porting (with `5C8`'s permission) or reimplementing the `skate3_online.cpp` hooks in `mchughalex/skate3recomp`.

---

## 5. Draft Message to `5C8` and `mchughalex`

> Hey 5C8 and mchughalex — huge thanks to both of you for your work on Skate 3 Recomp!
>
> I'm building the open-source Windows launcher for it ([Troopers-Skate-3-Recomp-Launcher](https://github.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher)), and I've been researching how to help players connect to community Blaze servers cleanly via `settings.toml` (`net_host_overrides` + `user_live_signed_in`).
>
> 5C8, your `rexglue-skate3` networking/XNet fixes and `skate3_online.cpp` hooks work great. Two quick questions:
> 1. **@5C8:** Would you be okay with your `rexglue-skate3` networking changes (with full credit) and the `skate3_online.cpp` / `webcache` fixes being proposed as an upstream PR to `mchughalex/rexglue-skate3` and `mchughalex/skate3recomp`?
> 2. **@mchughalex:** If 5C8 is on board, would you be open to reviewing an upstream PR for `net_host_overrides` + online XNet/Live support on the official build?
>
> In the meantime, 5C8, would you also be alright with the launcher offering `5C8/skate3recomp` as an optional downloadable build alongside `mchughalex/skate3recomp`?
