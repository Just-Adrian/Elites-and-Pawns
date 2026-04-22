# Elites and Pawns True — Project Constitution

**Tier 1 — always loaded. Keep under 200 lines.**
Area references, feature specs, and decision records are loaded on demand via the `@pointers` at the bottom.

---

## Why

A multiplayer FPS/RTS hybrid where a persistent War Map drives tactical FPS battles: a contested node on the strategic map spawns an FPS fight; the battle result flows back to map control. Inspired by Heroes & Generals, Deathgarden, Darktide.

## What

- **Engine:** Unity 6 (6000.2.8f1), URP
- **Networking:** Mirror + KCP Transport. Server-authoritative. No P2P.
- **Language:** C# 9+
- **VCS:** Git (local; remote pending)
- **Project root:** `C:\Users\Adrian\Elites and Pawns True`

### Folder map (canonical)

```
Assets/_Project/Scripts/
├── Core/         GameManager, Singleton, GameEnums, SimpleTeamManager, SpawnPoint
├── Networking/   ElitesNetworkManager, NetworkPlayer, PlayerSpawnHandler,
│                 DedicatedServerLauncher, ClientBattleRedirector, FPSAutoConnect,
│                 FPSPlayerSetup
├── Player/       PlayerController, PlayerHealth, PlayerHitbox
├── Weapons/      BaseWeapon, ProjectileWeapon, Projectile, WeaponData, WeaponManager,
│                 ProjectilePhysicsSettings
├── GameModes/    ControlPoint, GameModeManager, GameModeUI, ScoreNetworkSync,
│                 GameModeCanvasSetup
├── UI/           PlayerHUD, LocalPlayerCanvas, HUDDebugger
├── WarMap/       17 scripts — RTS layer (written, not yet integration-tested)
├── Debug/        NetworkManagerDebug, TeamSystemDebugger
└── Editor/       WarMapNodePrefabCreator
```

### Current state (Apr 2026)

- **FPS layer stable** (Milestones 1 & 1.5): movement, projectile weapons, health/respawn, Blue/Red team assignment, King of the Hill — verified Nov 2025.
- **WarMap layer written, not yet brought up**: 17 scripts, never run end-to-end. PowerShell refactor `Team` → `FactionType` pending.
- **Factions (design):** three — RED (Destroyers), BLUE (Architects), GREEN (Hunters). Implemented: Blue, Red. Green is next — not deferred to post-MVP.

See @PROGRESS.md for status and @TODO.md for next actions.

## How

### Namespaces (canonical)

`ElitesAndPawns.Core` · `.Networking` · `.Player` · `.Weapons` · `.GameModes` · `.UI` · `.WarMap` · `.Debug` · `.Editor`

Folder name = namespace suffix. No exceptions.

### Non-negotiable rules

1. **Factions are represented ONLY by `FactionType` in `GameEnums.cs`.** Never introduce a parallel `Team` enum. Never re-add the nested `NodeType` that was removed — `NodeType` lives on `WarMapNode`. → @Docs/ADRs/001-faction-type-single-enum.md
2. **`ControlPoint` is a `MonoBehaviour`, not a `NetworkBehaviour`.** Network sync is delegated to a dedicated `ScoreNetworkSync` component. Converting `ControlPoint` to `NetworkBehaviour` was tried and broke UI — do not re-propose. → @Docs/ADRs/002-controlpoint-monobehaviour.md
3. **Pass gameplay values as RPC parameters, not by reading SyncVars inside the RPC body.** SyncVars may be stale on clients when the RPC fires. → @Docs/ADRs/003-syncvar-vs-rpc-timing.md
4. **The WarMap scene must use `ElitesNetworkManager`**, never Unity's base `NetworkManager`. Silent-fallback trap: `OnServerAddPlayer` custom init is bypassed otherwise. → @Docs/ADRs/004-scene-network-manager.md
5. **Unity Inspector settings can't be fixed by code.** Image fill type, Canvas render mode, layer collision matrix, prefab component wiring — these require manual Editor steps on a checklist. → @Docs/ADRs/005-inspector-checklist-gates.md
6. **Register scene objects explicitly; do not auto-detect via `FindObjectsByType` on startup.** Execution-order traps. → @Docs/ADRs/006-explicit-node-registration.md
7. **Server-authoritative, always.** Flow is: client input → `[Command]` → server validates → `[SyncVar]` or `[ClientRpc]` → client visual. Never trust a client-reported value for gameplay state.

### Working style

- **Discuss before implementing.** No code or file changes without explicit approval. Adrian sets direction; Claude implements.
- **Research over reinvention.** If a non-obvious design decision comes up, check for prior art and scientific consensus before proposing a solution.
- **Push back, don't yes-man.** If a proposal is weak, say so with reasoning.
- **Milestone-gated commits.** Test, then commit at milestone boundaries with a descriptive message.
- **Incremental edits** preferred over full-file rewrites.
- **Script size:** 150–500 lines per C# file; split at natural boundaries when approaching 400.
- **Multi-file refactors:** write a PowerShell script and run it locally. Do not loop MCP writes.
- **Keep Tier 2 docs current.** When code changes invalidate a claim in a Tier 2 reference doc, update the doc in the same session as the code change.

### Before reading deeper

For area-specific work, load only the relevant Tier 2 doc; do not preload all of them. For broad "what exists, what calls what" questions, start at @Docs/SYSTEM_INVENTORY.md — it also contains Mirror usage rules and covers the networking + WarMap layers.

ADRs 001–006 referenced in the non-negotiable rules above are forward references; the behavioral rules themselves are binding regardless of whether the ADR file exists yet. ADRs will be written opportunistically when a session actually proposes re-doing a broken solution.

---

## Further reading

### Tier 2 — area references (load on demand)

- @Docs/SYSTEM_INVENTORY.md — prescriptive "what exists, use this not that", Mirror usage rules, cross-cutting patterns
- @Docs/GDD.md — game design (three factions, war map concept, MVP scope)
- @Docs/PROGRESSION.md — future player progression spec (unscheduled)

### Tier 2 — decision records (load when touching related code; created opportunistically)

- @Docs/ADRs/001-faction-type-single-enum.md
- @Docs/ADRs/002-controlpoint-monobehaviour.md
- @Docs/ADRs/003-syncvar-vs-rpc-timing.md
- @Docs/ADRs/004-scene-network-manager.md
- @Docs/ADRs/005-inspector-checklist-gates.md
- @Docs/ADRs/006-explicit-node-registration.md

### Tier 3 — living state

- @PROGRESS.md — what's working, what's broken, verified dates
- @TODO.md — next actions, prioritized; includes WarMap bring-up plan

---

*If a section grows past what fits here, extract it to a Tier 2 doc and replace the content with an @pointer. Keep this file under 200 lines.*
