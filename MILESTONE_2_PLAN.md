# Milestone 2 Development Plan

**Created:** November 4, 2025  
**Goal:** Teams + War Map Integration  
**Timeline:** 3-4 weeks (25-30 hours)

---

## 🎯 VISION

Transform the current Free-For-All combat into a **strategic team-based campaign** where Blue and Red factions fight for control of a 5-node war map through King of the Hill battles.

---

## 📋 THREE PHASES

### **Phase 1: Team Foundation** (Week 1)
**Time:** 6-8 hours  
**Goal:** Blue vs Red team battles with KOTH

**Deliverables:**
- ✅ Team assignment system (auto-balance)
- ✅ Team-colored players (Blue/Red materials)
- ✅ Team spawn areas (separate locations)
- ✅ King of the Hill gamemode
  - Control point in center
  - Capture mechanics
  - Team scoring
  - Victory/defeat conditions
- ✅ Team HUD indicators

**Test:** 4 players (2v2) battle over control point

---

### **Phase 2: War Map Foundation** (Week 2)
**Time:** 10-12 hours  
**Goal:** Visual war map with node ownership tracking

**Deliverables:**
- ✅ WarMap.unity scene
- ✅ 5 nodes positioned on map:
  - HomeBase_Blue
  - City_A
  - City_B  
  - City_C
  - HomeBase_Red
- ✅ Node system (ownership, connections, status)
- ✅ Visual feedback (node colors change with ownership)
- ✅ Battle context tracking
- ✅ Scene transitions (War Map ↔ Battle)

**Test:** Click node on map, battle loads, return to map

---

### **Phase 3: Integration** (Week 3)
**Time:** 8-10 hours  
**Goal:** Complete game loop functional

**Deliverables:**
- ✅ Simplified deployment (attack adjacent nodes)
- ✅ Battle initiation with correct teams
- ✅ Battle results update node ownership
- ✅ War victory conditions (control 3/5 nodes)
- ✅ Match flow polish
- ✅ Victory/defeat screens show war map impact

**Test:** Full campaign - attack nodes, win battles, capture map

---

## 🎮 GAMEPLAY FLOW (After Milestone 2)

```
War Map
  ↓ Click adjacent enemy node
Select Attackers
  ↓ "Start Battle"
Load Battle Scene
  ↓ King of the Hill
Battle Plays Out
  ↓ One team wins
Battle Ends
  ↓ Return to War Map
Update Node Ownership
  ↓ Check war victory
Continue Campaign or War Won
```

---

## 🔧 TECHNICAL APPROACH

### **Team System**
- `TeamManager.cs` - Track teams, balance players
- SyncVar in `NetworkPlayer.cs` for team assignment
- Team-colored materials applied on spawn
- `SpawnPoint.cs` with team designation

### **King of the Hill**
- `ControlPoint.cs` - Trigger zone, capture logic
- `GameModeManager.cs` - Scoring, win conditions
- Network-synchronized capture progress
- UI showing point status and team scores

### **War Map**
- `Node.cs` - Individual node data
- `NodeManager.cs` (NetworkBehaviour) - Central authority
- `BattleContext.cs` - Static data between scenes
- `SceneTransitionManager.cs` - Handle scene loading

### **Simplified for MVP**
- No complex token costs (just "attack this node")
- No simultaneous battles (one at a time)
- No resource management yet
- Green faction waits for post-MVP

---

## 📊 SCOPE DECISIONS

### **IN SCOPE (MVP)**
✅ 2 teams (Blue vs Red)  
✅ 1 gamemode (King of the Hill)  
✅ 1 battle map  
✅ 5-node war map  
✅ Simple node ownership  
✅ Win condition (control majority)  
✅ Scene transitions  

### **OUT OF SCOPE (Post-MVP)**
❌ Green faction (3rd team)  
❌ Multiple gamemodes  
❌ Complex token system  
❌ Multiple simultaneous battles  
❌ Large war map (10+ nodes)  
❌ Vehicles  
❌ Player progression  

---

## ⏱️ TIMELINE

**Week 1: Teams + KOTH**
- Day 1-2: Team system (4 hours)
- Day 3-4: King of the Hill (4 hours)
- Result: Playable 2v2 team battles

**Week 2: War Map**
- Day 1-2: War map scene + nodes (6 hours)
- Day 3-4: Scene transitions (4 hours)
- Result: Can navigate between map and battle

**Week 3: Integration**
- Day 1-2: Deployment + initiation (6 hours)
- Day 3-4: Victory conditions + polish (4 hours)
- Result: Full game loop working

**Week 4: Testing & Polish**
- Bug fixes
- Balance tweaks
- Visual polish
- Playtesting

---

## ✅ SUCCESS CRITERIA

**Phase 1 Complete When:**
- [ ] Players auto-assigned to teams
- [ ] Teams spawn on opposite sides
- [ ] Control point captures correctly
- [ ] Team that controls point longest wins
- [ ] Victory/defeat screen shows correctly

**Phase 2 Complete When:**
- [ ] War map scene loads
- [ ] 5 nodes visible with ownership colors
- [ ] Can click node to initiate battle
- [ ] Battle scene loads with context
- [ ] Can return to war map after battle

**Phase 3 Complete When:**
- [ ] Can attack adjacent nodes only
- [ ] Battle result updates node ownership
- [ ] Controlling 3/5 nodes triggers war victory
- [ ] Full cycle works: Map → Battle → Map
- [ ] No critical bugs

---

## 🎯 IMMEDIATE NEXT STEPS

### **Session 1 (2-3 hours): Start Phase 1**

1. **Create TeamManager.cs** (30 min)
   - Track Blue/Red team players
   - Auto-balance on join
   - Team scoring system

2. **Update Player Spawning** (1 hour)
   - Assign players to teams
   - Apply team-colored materials
   - Create `SpawnPoint.cs`
   - Add spawn points to scene

3. **Test Team Assignment** (30 min)
   - Spawn 4 players
   - Verify 2 Blue, 2 Red
   - Verify correct colors
   - Verify separate spawns

**Deliverable:** Players spawn on teams with colors

---

### **Session 2 (3-4 hours): KOTH Gamemode**

1. **Create ControlPoint.cs** (1.5 hours)
   - Trigger collider
   - Detect players in zone
   - Capture progress
   - Contested state

2. **Create GameModeManager.cs** (1.5 hours)
   - Track point ownership
   - Team scores
   - Win conditions
   - Game timer

3. **Test KOTH** (1 hour)
   - Place control point in scene
   - Test capture mechanics
   - Verify win conditions
   - Test victory screen

**Deliverable:** Functional King of the Hill gamemode

---

## 📝 NOTES

**Why This Order:**
1. Teams first = foundation for everything
2. KOTH next = playable game immediately
3. War map after = builds on working teams
4. Integration last = connects it all

**Key Risks:**
- Scene transitions with Mirror networking
- Node state synchronization
- Balance (capture times, spawn distances)

**Mitigation:**
- Test scene transitions early
- Use NetworkBehaviour for NodeManager
- Iterate on balance after basic functionality works

---

## 🚀 READY TO START!

**Current Status:** Plan documented, ready to code  
**Next Action:** Implement TeamManager.cs  
**First Milestone:** Teams working (2-3 hours away)

---

*This plan was developed collaboratively between Adrian and Claude*  
*Plan approved: November 4, 2025*  
*Implementation start: November 4, 2025*
