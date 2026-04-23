# Elites and Pawns True — Project Status

**Revised:** April 23, 2026
**Last Active Development:** April 2026
**Engine:** Unity 6 + Mirror Networking (KCP Transport)

---

## What's Working (Milestone 1 + 1.5 — Verified Nov 2025; re-verified partially 2026-04-23)

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

**2026-04-23 re-verification:** host + shoot + KotH capture confirmed after project compile was restored. Client-join not yet re-tested.

## What's In Progress (War Map — Compiles, Not Yet Validated)

Seventeen WarMap scripts were written in late November 2025 attempting to bridge the RTS and FPS layers. The code compiles clean project-wide as of 2026-04-23 but has never been exercised end-to-end and has known issues observed during the smoke-test session. Key systems written:

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
- **2026-04-23: Completed `Team → FactionType` unification refactor.** 23-file PowerShell-driven rewrite (17 WarMap/Core + 6 Networking files). Restored clean project compile after multi-month latent breakage caused by the earlier `Team` enum deletion. See @Docs/ADRs/001-faction-type-single-enum.md.

## Known Remaining Issues

- `NodeOccupancy.RefreshOccupancyData()` runs every frame doing full squad enumeration — needs event-driven approach
- WarMap scripts reference `DedicatedServerLauncher` events — needs verification
- `Something.unity` is an unused throwaway scene (can be deleted)
- Several unused field warnings (non-critical, listed in compilation output)

**Surfaced 2026-04-23, awaiting next-session triage:**
- FPS client-join not yet re-verified post-refactor (host-only smoke-test done)
- RTS: uncertain whether per-player squad assignment is functional — needs dedicated test
- RTS: node faction-color rendering not working — all nodes appear gray regardless of `ControllingFaction`
- Six redundant self-ternaries and a handful of mangled strings remain from the refactor — tracked in @TODO.md under "Post-refactor cleanup"

---

*For next actions, see @TODO.md.*
