# System Inventory — Elites and Pawns True

**Last reviewed:** 2026-04-23
*If this date is more than a month old, spot-check a few namespaces against reality before trusting claims here.*

**Purpose:** Prescriptive "what exists, use this not that" map. When adding code or debugging, check here first to avoid duplicating existing systems or violating canonical patterns.

**Scope:** Canonical classes and cross-cutting patterns per namespace. Not exhaustive — do not expand into a per-class reference. For *why* specific architectural choices were made, see the ADRs.

---

## Maintenance rules

- **Touch-based updates.** If you change the behavior or contract of a class described here, update its entry in the same session as the code change. This is the primary maintenance mechanism — not periodic audits.
- **Stale-warning self-check.** On loading this file, check the "Last reviewed" date above. If more than a month old, verify a few claims against reality before acting on them, and bump the date when you do.
- **Scope discipline.** Keep entries at the "canonical class + one-line role" level. If this file starts drifting toward listing every public method per class, it will rot. Extract method-level detail into feature specs, not here.

---

## Assembly & compilation

All runtime scripts compile into `ElitesAndPawns.Scripts` (see `Assets/_Project/Scripts/ElitesAndPawns.Scripts.asmdef`). Editor-only scripts compile into a separate Editor assembly. When adding a new top-level namespace folder, update the asmdef references if needed.

---

## Core — `ElitesAndPawns.Core`

Canonical location for cross-cutting types: enums, singletons, game-wide state, team tracking.

- **`GameEnums.cs`** — defines `FactionType`, `GameState`, `GameMode`. `FactionType` is the canonical faction enum project-wide. Never introduce a parallel `Team` enum. (See "FactionType mid-refactor" below for current cleanup state.)
- **`GameManager.cs`** *(singleton)* — game-wide state machine. Inherits `Singleton<GameManager>`. Handles scene transitions (MainMenu → WarMapView → InBattle → PostBattle).
- **`Singleton.cs`** — generic base class for `MonoBehaviour` singletons. Use for any singleton that doesn't need Mirror. Handles thread-safe lazy instantiation, app-quit safety, `DontDestroyOnLoad`.
- **`SimpleTeamManager.cs`** *(singleton)* — runtime team assignment, auto-balance, score tracking. Non-Mirror `MonoBehaviour`. Accessed via `SimpleTeamManager.Instance`. Auto-creates itself if missing. Uses `FactionType` throughout (already clean).
- **`SpawnPoint.cs`** — scene-placed `MonoBehaviour`. Defines team ownership via `FactionType` and provides `GetSpawnPosition()` / `GetSpawnRotation()`. `ElitesNetworkManager.CacheSpawnPoints()` finds these at runtime.
- **`SimpleTeamDebug.cs`** — F1/F2/F3 debug GUI for team state inspection. ⚠ Currently has **no namespace declaration** — in global namespace. Fix during next touch: add `namespace ElitesAndPawns.Core` or move to `Debug/`.

---

## Networking — `ElitesAndPawns.Networking`

Mirror-facing code + cross-process FPS/RTS launch machinery.

- **`ElitesNetworkManager.cs`** — canonical NetworkManager subclass. **Always extend this, never Mirror's base `NetworkManager` directly** (ADR 004). Handles: CLI arg parsing, dedicated-server auto-start in batch mode, projectile prefab auto-registration from WeaponData, team spawn caching, player spawning + faction assignment, `PlayerSquadManager` + `NodeOccupancy` wiring on join. Contains two redundant self-ternaries (leftovers from the `Team → FactionType` refactor) in `OnServerAddPlayer` and `GetFactionStartingNode` — flagged for cleanup in TODO.
- **`NetworkPlayer.cs`** — per-player `NetworkBehaviour` identity. Stores `FactionType Faction` and `PlayerName`. Assigned by `ElitesNetworkManager.OnServerAddPlayer`.
- **`PlayerSpawnHandler.cs`** — per-player spawn logic and respawn choreography.
- **`DedicatedServerLauncher.cs`** *(singleton)* — **RTS-server-side process spawner.** Starts FPS battle server processes (one per active battle) with `-dedicated` and battle parameters. Emits `OnBattleServerReady` / `OnBattleServerStopped`; `WarMapManager` subscribes.
- **`ClientBattleRedirector.cs`** *(singleton)* — **RTS-client-side launcher.** Receives `RpcNotifyBattleServerReady` from server and launches an FPS client process with connection args.
- **`FPSAutoConnect.cs`** — **FPS-build startup logic.** Parses CLI args to decide host/client/dedicated mode and initializes accordingly.
- **`FPSPlayerSetup.cs`** — attached to the FPS player prefab. Handles local player setup (camera, audio listener, CharacterController) and stores `FactionType faction` + `targetNodeId` SyncVars.

---

## Player — `ElitesAndPawns.Player`

FPS-layer player systems.

- **`PlayerController.cs`** — movement (WASD), sprint, jump, mouse look. Uses `CharacterController`.
- **`PlayerHealth.cs`** — server-authoritative health. Exposes `IsDead` SyncVar (read by `ControlPoint` to purge dead players from capture zones). Respawn handled here.
- **`PlayerHitbox.cs`** — hit detection delegate. Headshot multiplier applied here.

---

## Weapons — `ElitesAndPawns.Weapons`

All weapon behavior. `NetworkBehaviour` hierarchy rooted in `BaseWeapon`.

- **`BaseWeapon.cs`** — abstract `NetworkBehaviour`. **Always extend this for new weapons.** Owns ammo SyncVars, the `[Command]CmdFire` / `[Command]CmdReload` / `[ClientRpc]Rpc*` pattern. Subclasses implement `PerformShot(origin, direction)`. Exposes events: `OnAmmoChanged`, `OnWeaponFired`, `OnReloadStarted`, `OnReloadFinished`.
- **`ProjectileWeapon.cs`** — current concrete subclass (Assault Rifle). Spawns physics projectiles.
- **`Projectile.cs`** — physics projectile. Trigger-based damage.
- **`WeaponData.cs`** — `ScriptableObject` config: fire rate, damage, spread, magazine size, reload time, sounds, projectile prefab reference. All weapon tuning lives here.
- **`WeaponManager.cs`** — per-player weapon switching and ammo aggregation. Provides camera reference to `BaseWeapon` via `SetPlayerCamera`.
- **`ProjectilePhysicsSettings.cs`** — shared physics tuning for projectiles (gravity multiplier, drag, lifetime).

---

## GameModes — `ElitesAndPawns.GameModes`

FPS gamemode logic. Currently: King of the Hill via `ControlPoint`.

- **`ControlPoint.cs`** — `MonoBehaviour` (**NOT** `NetworkBehaviour` — ADR 002). Runs independently on all clients for capture-zone detection. Uses `OnTriggerEnter`/`Exit` + per-frame cleanup of dead players (reads `PlayerHealth.IsDead`). Fires static events (`OnPointCaptured`, `OnCaptureProgressChanged`, `OnContestedStateChanged`) that `GameModeManager` listens to.
- **`ScoreNetworkSync.cs`** *(singleton)* — dedicated `NetworkBehaviour` on a scene GameObject with `NetworkIdentity`. Server calls `SetScores` / `AddScore`; SyncVar hooks push changes to clients via the `OnScoreUpdated` event. **Do not sync scores any other way.** This is the companion to `ControlPoint` — the split exists because making `ControlPoint` itself a `NetworkBehaviour` broke UI (ADR 002).
- **`GameModeManager.cs`** — orchestrates the active game mode; listens to `ControlPoint` events and drives `ScoreNetworkSync`.
- **`GameModeUI.cs`** — gamemode-level UI (scoreboard, match timer).
- **`GameModeCanvasSetup.cs`** — scene-setup helper for GameMode canvas wiring.

---

## UI — `ElitesAndPawns.UI`

HUD and per-player canvas logic.

- **`PlayerHUD.cs`** — per-player health/ammo/weapon display. Listens to `PlayerHealth` and `BaseWeapon` events.
- **`LocalPlayerCanvas.cs`** — ensures HUD renders only for the local player. Canvas render mode set to Screen Space — Camera (ADR 005 — Inspector-enforced).
- **`HUDDebugger.cs`** — runtime HUD inspection tool.

---

## WarMap — `ElitesAndPawns.WarMap`

The RTS layer. 17 scripts; **never brought up end-to-end as of 2026-04-22**. Bring-up plan lives in @TODO.md under "WarMap bring-up backlog".

- **`WarMapManager.cs`** *(singleton, NetworkBehaviour)* — owns war state, the 5-node default map, battle sessions, victory conditions. Accessed via `WarMapManager.Instance`. Subscribes to `DedicatedServerLauncher` events and relays battle-server-ready notifications to clients via ClientRpc.
- **`WarMapNode.cs`** — scene-placed `NetworkBehaviour`. One per strategic node. Contains the **nested `NodeType` enum** (do not re-add a duplicate top-level `NodeType`). Same file also defines the namespace-level `BattleResult` class.
- **`Squad.cs`** — **plain C# data class** (NOT a NetworkBehaviour). Paired with the `SquadSyncData` struct used in Mirror SyncLists. This "data class + sync struct + NetworkBehaviour owner" split is the canonical pattern for RTS data types that need replication without carrying a `NetworkIdentity`.
- **`PlayerSquadManager.cs`** — per-player `NetworkBehaviour`. Owns a `SyncList<SquadSyncData>` of the player's squads (currently 3 Infantry by default). Initialized by `ElitesNetworkManager.OnServerAddPlayer`.
- **`NodeOccupancy.cs`** *(singleton, NetworkBehaviour)* — tracks which squads are at which nodes. ⚠ Currently calls `FindObjectsByType` every frame — known perf issue, event-driven refactor pending (see @TODO.md).
- **`CaptureController.cs`** *(singleton, NetworkBehaviour)* — 60s uncontested capture timer. Triggers FPS battles via `WarMapManager` when a node becomes contested.
- **`TokenSystem.cs`** *(singleton, NetworkBehaviour)* — faction token economy. Tokens come from holding territory (not from winning battles). Accessed via `TokenSystem.Instance`.
- **`BattleParameters.cs`** — data contract passed to FPS servers. Defines the `BattleType` enum + `SquadBattleData` class. Knows how to gather squad/ticket data from `NodeOccupancy`.
- **`BattleManager.cs`** *(singleton, NetworkBehaviour)* — FPS-side battle instance. Reads `BattleParameters`, manages spawn-ticket consumption, reports results. Contains two of the three manual-cleanup spots the refactor script can't handle (lines ~233, ~357).
- **`BattleLobby.cs`** *(singleton, NetworkBehaviour)* — pre-battle waiting / countdown. Defines the namespace-level `LobbyState` enum.
- **`BattleSceneBridge.cs`** *(singleton, NetworkBehaviour)* — additive scene loading for the FPS battle scene. Defines the namespace-level `ActiveBattle` class.
- **`BattleIntegration.cs`** *(singleton, NetworkBehaviour)* — glue between FPS kills and squad/manpower updates. Contains one of the three manual-cleanup spots for the refactor (line ~170).
- **`BattleUI.cs`** — FPS-battle UI (score, tickets remaining).
- **`FPSLauncher.cs`** *(singleton)* — ⚠ **editor / local-host path** for launching FPS processes. Overlaps *conceptually* with `Networking/DedicatedServerLauncher` but is a different code path. See "FPS process launching" section below.
- **`WarMapCamera.cs`** *(singleton)* — top-down war-map view camera (pan/zoom/drag).
- **`WarMapUI.cs`** — strategic-layer UI (node selection, squad management panel, drag-drop squad movement).
- **`WarMapTestHarness.cs`** — debug GUI for testing the WarMap stack standalone. Still uses `Team` enum directly.

---

## Debug — `ElitesAndPawns.Debug`

- **`NetworkManagerDebug.cs`** — Mirror network state inspection UI.
- **`TeamSystemDebugger.cs`** — team assignment / scoring inspection UI.

---

## Editor — `ElitesAndPawns.Editor`

Editor-only scripts. Separate Unity editor assembly.

- **`WarMapNodePrefabCreator.cs`** — menu-driven prefab generator for WarMap nodes.

---

## Cross-cutting: Mirror usage rules

Prescriptive rules for any new networked code. These are Mirror framework rules, not project-specific — they apply before, during, and after any refactor.

- **Client-to-server gameplay actions:** `[Command] void CmdFoo(...)`. Server validates, then mutates state. Never mutate gameplay state client-side and expect the server to agree.
- **Server-to-all-clients broadcast (visual/audio feedback, state announcements):** `[ClientRpc] void RpcFoo(...)`.
- **Server-to-one-client personal feedback (failure messages, targeted UI updates):** `[TargetRpc] void TargetFoo(NetworkConnection target, ...)`.
- **Replicated scalar state that changes at runtime:** `[SyncVar]` with a `hook` if clients need to react to changes. Use `[SyncVar(hook = nameof(OnFooChanged))]`.
- **Replicated collections:** `SyncList<T>` (for reference types use a plain struct with `[Serializable]` — see `SquadSyncData`). Avoid custom list synchronization.
- **Server-only methods (called only on server, never from clients):** `[Server]` attribute. Fails loudly if invoked client-side.
- **Client-only methods:** `[Client]` attribute. Symmetric to `[Server]`.
- **SyncVar-vs-RPC staleness rule (ADR 003):** when firing an RPC that needs a value, pass the value as an RPC parameter. Do NOT read it from a SyncVar inside the RPC body — the SyncVar may not have propagated yet when the RPC fires on the client. Pattern:
  ```csharp
  // BAD: reading SyncVar inside RPC body
  [ClientRpc] void RpcWin() { Debug.Log($"{winningFaction} wins!"); }  // winningFaction may be stale

  // GOOD: pass as parameter
  [ClientRpc] void RpcWin(FactionType winner) { Debug.Log($"{winner} wins!"); }
  ```
- **Server-authoritative flow (repeat of Constitution Rule 7, because it's the most-violated rule):** client input → `[Command]` → server validates → `[SyncVar]` or `[ClientRpc]` → client visual. Never trust a client-reported value for gameplay state.
- **NetworkIdentity budget:** every `NetworkBehaviour` on a scene GameObject or prefab needs a `NetworkIdentity`. This has per-frame overhead. For "many instances" RTS entities (like squads), prefer the data-class + SyncList pattern (`Squad` + `SquadSyncData`) over one NetworkBehaviour per entity.

---

## Cross-cutting: FactionType is the canonical faction enum

`FactionType` in `GameEnums.cs` is the single project-wide faction enum. The parallel `Team` enum was removed in a 23-file refactor on 2026-04-23; no `Team` references remain. Any new faction-aware code must use `FactionType`.

See @Docs/ADRs/001-faction-type-single-enum.md for rationale and hard-won lessons from the refactor (including why the mid-refactor audit recorded in earlier versions of this file was partly wrong).

---

## Cross-cutting: FPS process launching (two paths)

Two files in different namespaces both launch FPS processes. They're for different topologies and must not be mixed in a single workflow.

- **`Networking/DedicatedServerLauncher`** — **production / dedicated-server topology.** RTS server spawns one FPS *server* process per active battle on its own machine, then clients receive an RPC (`RpcNotifyBattleServerReady`) and connect to that server as *clients*.
- **`WarMap/FPSLauncher`** — **local / host-and-join topology.** Runs on a client machine. First player to enter a battle launches FPS as *host*; subsequent players launch as *clients* connecting to that host. No server-process spawning on a central machine.

When adding new multiplayer scenarios, decide topology first, then use the matching launcher. A code path that tries to use both will produce dual/confused process launches.

**⚠ This dual-launcher structure is a known smell.** It exists because the project grew into dedicated-server support after the host-join code was already working. During overhaul, consolidating these is a high-value target — see @TODO.md's "Future" section.

---

## When adding new code

1. **Decide the namespace.** Folder = namespace suffix. If a new top-level namespace is truly warranted, update the Constitution's folder map AND this file.
2. **Check for existing canonical patterns** in the relevant namespace above before inventing new ones.
3. **For new faction-aware code:** use `FactionType`. Never `Team`, even if surrounding legacy code uses it.
4. **For new replicated RTS data types:** consider the `Squad` + `SquadSyncData` + owning-`NetworkBehaviour` triplet before making a new `NetworkBehaviour` per instance. `NetworkIdentity` per game-entity scales poorly.
5. **For new weapons:** extend `BaseWeapon`. Don't create a parallel weapon base class.
6. **For new UI that syncs state across clients:** put the sync in a dedicated `NetworkBehaviour` (pattern: `ScoreNetworkSync`), not in the UI class itself (ADR 002).
7. **For new singletons:** first ask whether it really needs to be a singleton. Scene references or events are often better. If it must be a singleton, use `Singleton<T>` for non-Mirror and the in-class pattern (see `WarMapManager`) for NetworkBehaviours.
8. **Update this file.** When your new code changes a canonical contract above, edit the relevant entry in the same session.
