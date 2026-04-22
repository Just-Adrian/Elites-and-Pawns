# TODO — Elites and Pawns True

**Last reviewed:** 2026-04-22
*If this date is more than 2 weeks old, re-evaluate items below before acting.*

### Maintenance rules

- Delete completed items; don't strikethrough. Git keeps history.
- Keep under ~120 lines. If it's longer, promote items: to @PROGRESS.md (if state), to an ADR (if a recurring decision), or delete.
- Update "Last reviewed" whenever meaningful changes are made.
- Prune completed items from TODO in the same session that PROGRESS.md gets a milestone update.

---

## Immediate next action

- [ ] Run `refactor-team-to-factiontype.ps1` at project root and verify Unity compiles clean.

Everything below blocks on this.

---

## After the refactor (ordered)

- [ ] Manually simplify three conversion spots the script can't handle:
  - `BattleManager.cs` line ~233: `Team playerFaction = player.Faction == FactionType.Blue ? ...` → `FactionType playerFaction = player.Faction;`
  - `BattleManager.cs` line ~357: `FactionType winnerFaction = winner == ...` → `FactionType winnerFaction = winner;`
  - `BattleIntegration.cs` line ~170: `(Team)(int)(killer?.Faction ?? ...)` → `killer?.Faction ?? FactionType.None`
- [ ] Confirm compile-clean in Unity Editor.
- [ ] Project-wide search for `enum Team` — the now-orphaned declaration will surface. Delete it.
- [ ] Delete the `FactionType → Team` conversion block in `ElitesNetworkManager.OnServerAddPlayer`.
- [ ] Confirm compile-clean again; run FPS smoke test (host + client join, shoot, KotH point capture).
- [ ] Git commit: `refactor: unify Team → FactionType across WarMap layer`
- [ ] Write ADR 001 (FactionType as canonical) — now reflects reality, not intent.
- [ ] Shrink the "FactionType vs Team" section in @Docs/SYSTEM_INVENTORY.md to one sentence + ADR 001 pointer.
- [ ] Begin WarMap bring-up (plan below).

---

## WarMap bring-up plan

**Status:** 17 scripts written Nov 2025, never run end-to-end.
**Goal:** validate the existing pipeline — squad movement → capture detection → FPS battle trigger → result feedback — before adding new features or rewriting.
**Strategy:** minimum-viable bring-up first; fix only what's blocking; overhaul only after it runs at all.

### Phase 1 — Prep (verification, no code changes yet)

- [ ] Open `WarMap.unity` scene. Verify it uses `ElitesNetworkManager` (Rule 4 in Constitution), not base `NetworkManager`.
- [ ] Verify scene has required managers wired in: `WarMapManager`, `TokenSystem`, `NodeOccupancy`, `CaptureController`. All need `NetworkIdentity`.
- [ ] Verify `WarMapTestHarness` is present with its prefab references set (player prefab, node prefab).
- [ ] Check `DedicatedServerLauncher` event subscriptions still resolve (compile-time check: does `WarMapManager.Start()` subscribe cleanly).

### Phase 2 — First run (expect failures; don't fix everything, triage first)

- [ ] Launch WarMap scene as Host via Unity Editor. Don't worry about dedicated server yet.
- [ ] Observe: do nodes spawn? Do they connect? Does `WarMapTestHarness` debug GUI appear?
- [ ] Attempt simplest possible flow via test harness: create one squad, move it between two nodes, confirm `NodeOccupancy` updates.
- [ ] Write down every error in console. Don't fix yet — build a triage list.

### Phase 3 — Triage and fix (order items by: blocks bring-up > perf > cosmetic)

- [ ] Fix any blockers from Phase 2 triage list, one at a time, smallest fix that works.
- [ ] `NodeOccupancy.RefreshOccupancyData()` runs `FindObjectsByType` every frame — convert to event-driven. Only fix if Phase 2 shows perf problems; otherwise defer.
- [ ] Delete unused throwaway scene `Something.unity` if still present.
- [ ] Clean up unused-field warnings (non-critical, from compilation output).

### Phase 4 — End-to-end smoke test

- [ ] Trigger a contested node from the test harness (move opposing squads to same node).
- [ ] Confirm `CaptureController` detects contested state and calls `WarMapManager.StartBattle`.
- [ ] Confirm `BattleSceneBridge` loads the FPS scene additively (local, not via `DedicatedServerLauncher`).
- [ ] Play the FPS battle, end it, confirm result flows back to `WarMapManager` and updates node control.
- [ ] If any of the above fails: stop, document the exact failure point, scope a fix. Do not patch over silently.

### Phase 5 — Decide: overhaul or continue

Once the pipeline runs end-to-end (even ugly), pause and decide:
- Is the architecture usable as-is for GREEN faction + progression work, or does it need overhaul first?
- If overhaul: which specific pattern needs changing? (FPSLauncher/DedicatedServerLauncher consolidation is a known candidate.)
- Document the decision in an ADR before acting on it.

---

## Future (not scheduled)

One-liners so these don't get forgotten. Do not start without an explicit scope decision.

- GREEN faction implementation (not deferred to post-MVP per Constitution; schedule after WarMap Phase 4 passes).
- Player progression system (see @Docs/PROGRESSION.md).
- Battle-result IPC between FPS server and RTS server (currently missing; `DedicatedServerLauncher` can spawn but FPS server can't report back).
- Consolidate `WarMap/FPSLauncher` and `Networking/DedicatedServerLauncher` into a single launcher with topology mode — see SYSTEM_INVENTORY "FPS process launching" section.
