# Elites and Pawns True — Project Status

**Revised:** April 22, 2026
**Last Active Development:** November 2025
**Engine:** Unity 6 + Mirror Networking (KCP Transport)

---

## What's Working (Milestone 1 + 1.5 — Verified Nov 2025)

The FPS layer is functional with 2-player multiplayer combat tested on host + client:

- Mirror networking with server-authoritative architecture
- First-person movement (WASD, sprint, jump) with mouse look
- Projectile-based weapon system (BaseWeapon → ProjectileWeapon) with ScriptableObject data
- Ammo, reload, fire rate, spread, damage falloff, headshot detection
- Server-authoritative health with death/respawn (3s delay)
- PlayerHUD showing health bar, ammo counter, weapon name (Screen Space — Camera)
- Blue vs Red team assignment with auto-balancing and spawn separation
- Friendly fire protection
- King of the Hill gamemode with capture mechanics, team scoring, and victory conditions

## What's In Progress (War Map — Incomplete)

Sixteen WarMap scripts were written in late November 2025 attempting to bridge the RTS and FPS layers. The code is architecturally ambitious but was never tested end-to-end. Key systems written:

- **WarMapManager** — 5-node map, battle session management, victory conditions
- **WarMapNode** — Node state, control %, SyncVar hooks, faction adjacency checks
- **TokenSystem** — Faction token economy with production cycles from held territory
- **Squad / PlayerSquadManager** — Per-player squad ownership, movement, manpower, SyncList sync
- **NodeOccupancy** — Tracks which squads are at which nodes, spawn ticket allocation
- **CaptureController** — 60s uncontested capture timer, contested detection, FPS battle triggering
- **BattleManager** — FPS battle instance with spawn ticket consumption and result reporting
- **BattleSceneBridge** — Additive scene loading for concurrent war map + battle
- **BattleParameters** — Data contract between RTS and FPS layers
- **WarMapTestHarness** — Debug GUI for testing the full war map stack

## Project Structure

```
Assets/_Project/
├── Scripts/
│   ├── Core/         GameManager, Singleton, GameEnums, SimpleTeamManager, SpawnPoint
│   ├── Networking/   ElitesNetworkManager, NetworkPlayer, PlayerSpawnHandler,
│   │                 DedicatedServerLauncher, ClientBattleRedirector, FPSAutoConnect
│   ├── Player/       PlayerController, PlayerHealth, PlayerHitbox
│   ├── Weapons/      BaseWeapon, ProjectileWeapon, Projectile, WeaponData, WeaponManager
│   ├── GameModes/    ControlPoint, GameModeManager, GameModeUI, ScoreNetworkSync
│   ├── UI/           PlayerHUD, LocalPlayerCanvas, HUDDebugger
│   ├── WarMap/       16 scripts — RTS layer (written, not yet integration-tested)
│   ├── Debug/        NetworkManagerDebug, TeamSystemDebugger
│   └── Editor/       WarMapNodePrefabCreator
├── Prefabs/          Player, GameModeCanvas, WarMapNode, NetworkPlayer, Projectile_Bullet
├── Scenes/           NetworkTest (FPS)
├── Data/Weapons/     AssaultRifle_Data.asset
└── Documentation/    Architecture docs + archived progress logs
```

## April 2026 Revival — Cleanup Done

Completed cleanup tasks:
- Removed stale commit scripts, .csproj files, backup files, duplicate scripts
- Consolidated documentation (archived Progress/ session logs)
- Updated .gitignore to be comprehensive (covers .bak, .csproj, Builds/, IDE caches, Vibe Unity temp)
- Removed duplicate `Team` enum from GameEnums.cs (unified to `FactionType`)
- Removed duplicate `NodeType` enum from GameEnums.cs (kept WarMapNode.NodeType)
- Deleted duplicate HUDLayoutFixer.cs

## Known Remaining Issues

- `NodeOccupancy.RefreshOccupancyData()` runs every frame doing full squad enumeration — needs event-driven approach
- WarMap scripts reference `DedicatedServerLauncher` events — needs verification
- `Something.unity` is an unused throwaway scene (can be deleted)
- Several unused field warnings (non-critical, listed in compilation output)

---

*For next actions, see @TODO.md.*
