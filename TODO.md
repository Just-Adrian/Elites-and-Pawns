# TODO — Elites and Pawns True

**Last reviewed:** 2026-04-23
*If this date is more than 2 weeks old, re-evaluate items below before acting.*

### Maintenance rules

- Delete completed items; don't strikethrough. Git keeps history.
- Keep under ~120 lines. If it's longer, promote items: to @PROGRESS.md (if state), to an ADR (if a recurring decision), or delete.
- Update "Last reviewed" whenever meaningful changes are made.
- Prune completed items from TODO in the same session that PROGRESS.md gets a milestone update.

---

## Immediate next action

- [ ] Git commit the `Team → FactionType` refactor. Suggested message: `refactor: unify Team → FactionType across WarMap and Networking layers`
- [ ] Delete `refactor-team-to-factiontype.ps1` from project root (`git rm` as part of the commit, or remove and commit separately).

---

## Next session scope (per Adrian 2026-04-23)

Focus: validate what compiles actually works, then connect the two layers.

- [ ] **FPS client-join test.** Host + KotH verified 2026-04-23; client-join still untested. This exercises `OnServerAddPlayer` which is the most-refactored path.
- [ ] **RTS: verify per-player squad assignment.** Does each connected player actually receive independent squads to control, or is it broken?
- [ ] **RTS: fix node faction-color rendering.** All nodes currently appear gray regardless of `ControllingFaction`. Likely in `WarMapNode.UpdateVisuals()` or a material/Inspector wiring issue — triage first, don't assume the fix.
- [ ] **End-to-end RTS ↔ FPS integration test.** Once the above two RTS issues are cleared, walk the full pipeline: squad movement → capture detection → FPS battle trigger → result feedback. See WarMap bring-up Phases 2–4 below.

---

## Post-refactor cleanup (low priority, compiles as-is)

Redundant self-ternaries the PS script left behind. Pattern: `faction == FactionType.Blue ? FactionType.Blue : ...` — behaviorally correct but dead identity conversions. Simplify to direct assignment.

- [ ] `BattleManager.cs` ~L233 — `playerFaction` ternary → `player.Faction`.
- [ ] `BattleManager.cs` ~L357 — `winnerFaction` ternary → direct assignment of `winner`.
- [ ] `BattleManager.cs` ~L425 — `GameModeManager.WinningTeam` ternary → direct assignment.
- [ ] `BattleIntegration.cs` ~L170 — drop the `(FactionType)(int)(...)` cast chain.
- [ ] `ElitesNetworkManager.cs` ~L285 — `team` self-ternary in `OnServerAddPlayer`; also fix stale comment `// Convert FactionType to Team`.
- [ ] `ElitesNetworkManager.cs` ~L405 — `team` self-ternary in `GetFactionStartingNode`.

Regex-mangled strings from the refactor (cosmetic only):

- [ ] `ElitesNetworkManager.cs` — field comment `// FactionType Manager reference` (should be `// Team Manager reference`).
- [ ] `ElitesNetworkManager.cs` — `Debug.Log` template `"FactionType counts - Blue: ..."` (should be `"Team counts - Blue: ..."`).

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

Pre-loaded items observed during the 2026-04-23 post-refactor smoke-test (see "Next session scope" above):

- [ ] RTS: investigate why node faction coloring doesn't render (all nodes gray).
- [ ] RTS: verify per-player squad assignment actually produces independent squads per player.

Other Phase 3 items:

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
