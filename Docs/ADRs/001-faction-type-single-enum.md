# ADR 001: FactionType as the Single Canonical Faction Enum

**Status:** Accepted
**Date:** 2026-04-23

## Context

Until April 2026 the project had two parallel enums representing the same concept: a top-level `Team` and the newer `FactionType` in `GameEnums.cs`. The duplication originated in an earlier development phase where FPS code (team assignment, scoring) and RTS code (factions, node ownership) grew independently and never unified their type language.

The cost of the split was cross-layer conversion code at every FPS/RTS boundary — `FactionType ↔ Team` ternaries in `ElitesNetworkManager.OnServerAddPlayer`, `BattleManager`, `BattleIntegration`, `FPSAutoConnect`, `NetworkPlayer`, and `PlayerSpawnHandler`. This friction was going to compound as the WarMap layer came online and needed to hand faction identity across battle boundaries.

## Decision

A single enum, `FactionType` in `ElitesAndPawns.Core.GameEnums`, represents factions project-wide. `Team` is deleted. All new code that needs a faction uses `FactionType`.

## Consequences

**Positive:**
- Zero cross-layer conversion code for new work.
- One authoritative list of factions (`FactionType.None | Blue | Red | Green`).
- SyncVars, RPC parameters, method signatures, and collection generics all speak the same language.

**Costs paid:**
- A 23-file PowerShell-driven refactor was required to rename all `Team` usages (17 WarMap/Core files + 6 Networking files).
- Six redundant self-ternaries remain in the codebase post-refactor (pattern: `faction == FactionType.Blue ? FactionType.Blue : ...`). They compile and are behaviorally correct but are dead identity conversions. Tracked in `TODO.md` for incremental cleanup.
- A small amount of cosmetic damage from regex-driven text replacement mangled Inspector `[Header]` labels and one `Debug.Log` template. Tracked in `TODO.md`.

## Notes — lessons worth remembering

- The mid-refactor audit recorded in `SYSTEM_INVENTORY.md` claimed the orphan `enum Team` declaration was still hiding in one of nine specific WarMap files. This was wrong: `Team` had already been removed from `GameEnums.cs` in an earlier April 2026 cleanup session and was fully undefined project-wide when the refactor script ran. The refactor's only job was renaming usages; there was no orphan declaration left to delete.
- One rewrite rule in the initial PS script (`"Team?"` → `"FactionType?"` as a literal string replacement) corrupted identifiers ending in `Team` when followed by C#'s null-conditional operator (e.g. `OnPlayerJoinedTeam?.Invoke` → `OnPlayerJoinedFactionType?.Invoke`). The rule was removed before the second run. Lesson: literal text replacement is unsafe around punctuation operators; prefer word-boundary regexes for anything involving trailing `?` `.` `,` or `>`.
- Clean-compile verified 2026-04-23. FPS host + KotH smoke-test verified 2026-04-23. FPS client-join and full RTS smoke-test still pending.
