# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A netplay mod for the Unity IL2CPP game **Megabonk**. It lets up to 6 players play together. The solution has three net6.0 projects:

- `src/plugin`: the BepInEx 6 (IL2CPP, bleeding edge) plugin that is loaded into the game. Assembly name `MegabonkTogether`.
- `src/server`: an ASP.NET Core matchmaking server (WebSocket) plus a LiteNetLib UDP rendezvous/relay server.
- `src/common`: MemoryPack message and model types shared by both. Changing a type here changes the wire format for both the plugin and the server.

## Commands

```bash
dotnet build MegabonkTogether.sln                                   # everything
dotnet build src/plugin/MegabonkTogether.Plugin.csproj -c Release   # plugin only (what CI builds)
dotnet run --project src/server/MegabonkTogether.Server.csproj      # server on http://127.0.0.1:5000, UDP 5678
```

- **Build variants** (compile constants read in `AutoUpdaterService`): set the env var `THUNDERSTORE_BUILD=true` to define `THUNDERSTORE`, or `PROTON_BUILD=true` to define `PROTON`. Both turn off automatic update downloads. `scripts/proton/build.sh` is the Linux/Proton build. If no `Directory.Build.props` exists, it writes one that sets `MegabonkPath`.
- **Server in Docker**: run `docker build .` from the repo root. The container listens on 5432 (HTTP/WS) and 5678/udp. The UDP port is hard-coded as `RENDEZVOUS_PORT` in `RendezVousServer.cs`.
- **Point the mod at a local server**: in `<game>/BepInEx/config/MegabonkTogether.cfg`, set `[Network] ServerUrl = ws://127.0.0.1:5000`.
- **No tests or linter.** The server references xunit, but no tests exist. A clean build currently gives about 77 warnings and 0 errors. Treat 0 errors as the bar and don't try to clear the existing warnings.

## Game assembly references (important)

The plugin compiles against game and Unity interop DLLs. Where they come from depends on the environment:

- If the MSBuild property or env var `MegabonkPath` points to a game install where `BepInEx/interop` exists, references resolve from there. **Debug builds then copy the output into `<game>/BepInEx/plugins/MegabonkTogether`** (skipped when `CI`/`GITHUB_ACTIONS` is set). Delete that copy after local testing.
- Otherwise the build uses the committed `src/plugin/stripped-libs/`. These are reference-only copies produced by `src/plugin/strip-dlls.bat`, which needs the `assembly-publicizer` tool with `--strip-only`. CI always builds this way.
- To reference a new game DLL: add the `<Reference>` to the csproj, add a line to `strip-dlls.bat`, and commit the *stripped* output. If you skip this, CI breaks. **Never commit the unstripped originals.**

## Architecture

### Network topology

1. **Matchmaking (WebSocket `/ws`)**: `Program.cs` routes by query string: `?random` or `?friendlies&role=Host|Client&code=&name=`, always with `enabledSharedExperience=`. `WebSocketHandler` runs the random queue (2–6 players, a short wait window) and friendlies rooms (`Pools/RoomCodePool`), and gives out connection IDs (`Pools/ConnectionIdPool`). Random matchmaking keeps shared-experience players and regular players in separate queues. Messages are the `IWsMessage` MemoryPack union.
2. **Peer connection (UDP 5678)**: `RendezVousServer` handles NAT-punch introductions between the host and each client. When punching fails or a peer is IPv6, it falls back to relay mode, where game packets are wrapped in `RelayEnvelope` and forwarded by the server.
3. **In-game traffic is host-authoritative P2P.** One player is the host ("server mode", `ISynchronizationService.IsServerMode()` / `IUdpClientService.IsHost()`). Clients send to the host. The host applies each message locally and rebroadcasts it to everyone else.

The server deserializes `IWsMessage`, `RelayEnvelope`, and some `IGameNetworkMessage` types, so changes to `src/common` usually mean redeploying the server too.

### Lifecycle of a synchronized game event (plugin)

Following one message end to end, for example `GoldChanged`, shows the pattern used for nearly everything:

1. **Message type**: `src/common/Messages/GameNetworkMessages/X.cs` is a `[MemoryPackable] partial class X : IGameNetworkMessage`. Register it with a new `[MemoryPackUnion(n, typeof(X))]` tag in `GameNetworkMessage.cs`. Append new tags and never renumber existing ones.
2. **Interception**: a Harmony patch in `src/plugin/Patches/` hooks the game method. It returns early to vanilla behaviour unless `synchronizationService.HasNetplaySessionStarted()`, and it skips sending when `Plugin.CAN_SEND_MESSAGES` is false. Then it calls `SynchronizationService.OnX(...)`.
3. **Send**: `SynchronizationService` builds the message and calls `udpClientService.SendToAllClients(...)` if it is the host, or `SendToHost(...)` if not. Pick the `DeliveryMethod` on purpose: `ReliableOrdered` for gameplay-critical interactions, `Unreliable` for high-frequency state.
4. **Receive**: `UdpClientService.HandleMessage` has **two `switch` blocks**: a client branch that only dispatches, and a host branch that dispatches and then relays with `SendToAllClientsExcept(peerId, ownerId, msg)`. A new message needs a case in both.
5. **Dispatch**: the static `EventManager` declares one `event` per message type. `OnX(msg)` marshals the invoke onto the Unity main thread through `MainThreadDispatcher.Enqueue`. Subscriptions (`SubscribeXEvents`) are made in the `SynchronizationService` constructor.
6. **Apply**: the handler changes game state inside `Plugin.CAN_SEND_MESSAGES = false; ...; = true;`. This stops the patches from step 2 from re-broadcasting the change (echo suppression).

Other static gates on `Plugin` (`CAN_SPAWN_PICKUPS`, `CAN_SPAWN_CHESTS`, `CAN_ENEMY_EXPLODE`, `CAN_DAMAGE_ENEMIES`, …) follow the same idea for host-authoritative spawning. On clients, patches block the game's own spawn calls, and the flag is flipped to true only while applying a spawn that came from the host.

### Entity identity

Synchronized game objects carry netplay IDs attached with MonoMod `DynamicData.For(gameObject).Set("netplayId", id)`. Owner and target use `"ownerId"` and `"targetId"`. Patches read them back with `.Get<uint?>("netplayId")`. The manager services (`PlayerManagerService`, `EnemyManagerService`, `ProjectileManagerService`, `PickupManagerService`, `ChestManagerService`, `SpawnedObjectManagerService`, `FinalBossOrbManagerService`) map IDs to live objects.

### Plugin bootstrapping and runtime

- `Plugin.Load()` does all the setup, in this order:
  1. Registers every custom `MonoBehaviour` with `ClassInjector.RegisterTypeInIl2Cpp<T>()`. **A new Il2Cpp-visible component must be added here**, or `AddComponent` fails.
  2. Builds a `Microsoft.Extensions.Hosting` DI container. All services are singletons behind interfaces.
  3. Runs `harmony.PatchAll()`.
  4. Creates `DontDestroyOnLoad` singleton GameObjects (`MainThreadDispatcher`, `NetworkHandler`, `NetPlayersDisplayer`, `CameraSwitcher`, `NotificationQueueManager`).
- Patches are `internal static` classes that resolve services into `static readonly` fields through `Plugin.Services.GetService<T>()`.
- `Scripts/NetworkHandler.Update()` is the network tick. It polls UDP and sends state snapshots: lobby/player at 60 Hz, enemies at 40 Hz, projectiles and tumbleweeds at 20 Hz. Remote entities are smoothed by the interpolators in `Scripts/Snapshot/`. Positions and rotations go over the wire as the quantized types in `common/Models` (see `Helpers/Quantizer.cs`), so bandwidth stays low.
- `SynchronizationService` (~4.5k lines) is the central state machine: lobby, loading, start, portal, and game over. It holds most of the send and apply logic. Expect to edit it for almost any sync change.
- **Two gameplay modes**: regular, and Shared Experience (`IsSharedExperienceEnabled()`). Shared Experience shares XP, gold, and interactions and pauses everyone during encounters, with `EncounterService` deciding when an encounter can close. Code often branches on this mode.
- `GameBalanceService` scales spawn credits, chest rates, XP, lamp charge time, and the enemy cap by player count (see `NETPLAY_CHANGES.md`).
- During netplay, patches suppress saves (unless `AllowSavesDuringNetplay` is set), Steam achievements and stats, and leaderboard uploads.
- Mod-specific UI strings live in `LocalizationService.LocalizationOverrides`, keyed by `("MegabonkTogether", key)`. `Plugin.StartNotification` uses them.

## Language version gotcha

The plugin uses `LangVersion latest`, so collection expressions like `[]` are already used there. The server is pinned to C# 10, and `common` gets the net6.0 default, which is also C# 10. Don't use newer syntax in `src/server` or `src/common`.

## Releasing

1. Bump `<Version>` in `src/plugin/MegabonkTogether.Plugin.csproj`. It becomes `MyPluginInfo.PLUGIN_VERSION`, which the auto-updater compares with the latest GitHub release tag.
2. Add an entry to `src/plugin/CHANGELOG.toml`, which is shown in-game after an update.
3. Add the same entry to the changelog section in `README.md`.

Pushing a tag triggers `build-plugin.yml`, which creates the GitHub release with the zip. It also triggers the Thunderstore publish. When `build-plugin.yml` succeeds, the Proton and server (win-x64 single-file) workflows run and attach their zips to the same release.
