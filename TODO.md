# TODO - Elites and Pawns True

**Last Updated:** November 4, 2025  
**Current Focus:** Milestone 2 - Teams + War Map Integration

---

## 🎯 MILESTONE 2: BLUE VS RED + WAR MAP

**Goal:** Complete game loop from war map to battle and back  
**Timeline:** 3-4 weeks (25-30 hours)  
**Teams:** Blue vs Red (Green comes later)

---

## 🔴 PHASE 1: TEAM FOUNDATION (Week 1 - Priority)

### **Goal:** Blue vs Red teams with King of the Hill gamemode

#### **1.1 Team System** (2 hours) 🔴 HIGH PRIORITY
- [ ] Create TeamManager.cs
  - Track Blue team players
  - Track Red team players
  - Assign players to teams on join (balance teams)
  - Team scoring system
- [ ] Update NetworkPlayer faction assignment
  - Auto-assign to least populated team
  - SyncVar for team membership
- [ ] Team identification
  - Team-colored materials (Blue/Red)
  - Apply to player renderer on spawn
  - Update nameplate with team color

**Files to modify:**
- `ElitesNetworkManager.cs` - Team balancing
- `NetworkPlayer.cs` - Team assignment
- Create: `TeamManager.cs`

---

#### **1.2 Team Spawn System** (1 hour) 🔴 HIGH PRIORITY
- [ ] Create spawn point system
  - SpawnPoint.cs with team assignment
  - Create 4-6 spawn points per team in NetworkTest scene
  - Position Blue spawns on one side, Red on opposite
- [ ] Update player spawning
  - ElitesNetworkManager picks spawn based on team
  - Spawn at random point within team area

**Files to modify:**
- `ElitesNetworkManager.cs` - Spawn logic
- Create: `SpawnPoint.cs`
- Scene: NetworkTest.unity - Add spawn points

---

#### **1.3 Team HUD** (1 hour) 🟡 MEDIUM PRIORITY
- [ ] Show team indicator
  - Add team name/color to HUD
  - Position: top-center
  - "BLUE TEAM" or "RED TEAM"
- [ ] Optional: Team roster
  - List of team members
  - Show player count (4v4, 3v5, etc.)

**Files to modify:**
- `PlayerHUD.cs` - Add team display

---

#### **1.4 King of the Hill Gamemode** (3-4 hours) 🔴 HIGH PRIORITY
- [ ] Create ControlPoint.cs
  - Trigger zone in center of map
  - Detect players in zone
  - Track capturing team
  - Capture progress (0-100%)
  - Contested state (both teams present)
- [ ] Create GameModeManager.cs
  - Track control point ownership
  - Team scores
  - Win condition (hold for 180 seconds OR reach 300 points)
  - Game timer
- [ ] Control Point UI
  - Capture progress bar
  - Current owner indicator
  - Team scores (top of screen)
  - Timer
- [ ] Victory/Defeat Screen
  - Show winning team
  - Show final scores
  - "Return to War Map" button (placeholder)
  - "Rematch" option

**Files to create:**
- `ControlPoint.cs`
- `GameModeManager.cs`
- `GameModeUI.cs`
- Update: NetworkTest.unity - Add control point

---

### **Phase 1 Deliverable:**
✅ Functional Blue vs Red team battles  
✅ King of the Hill gamemode working  
✅ Teams are balanced and spawn separately  
✅ Victory/defeat conditions functional  

**Test:** 4 players (2 Blue, 2 Red) fight over control point

---

## 🟡 PHASE 2: WAR MAP FOUNDATION (Week 2)

### **Goal:** Basic war map that tracks node ownership

#### **2.1 War Map Scene** (3 hours) 🔴 HIGH PRIORITY
- [ ] Create new scene: WarMap.unity
- [ ] Camera setup (top-down or isometric view)
- [ ] Create 5 node GameObjects
  - Position them like a simple map
  - Label: HomeBase_Blue, City_A, City_B, City_C, HomeBase_Red
  - Visual connections (lines between nodes)
- [ ] Basic UI layout
  - Node info panel
  - Deploy button (placeholder)
  - "Start Battle" button

**Files to create:**
- Scene: `WarMap.unity`
- `WarMapCamera.cs` - Camera controls

---

#### **2.2 Node System** (3 hours) 🔴 HIGH PRIORITY
- [ ] Create Node.cs
  - NodeType enum (HomeBase, City, Factory)
  - Owner (Blue/Red/Neutral)
  - Connected nodes list
  - Battle status (Peaceful/Active/Concluded)
  - Click detection
- [ ] Create NodeManager.cs (NetworkBehaviour)
  - Track all 5 nodes
  - SyncList of node states
  - Update node ownership (server-authoritative)
  - Check victory condition (control 3/5 non-home nodes)
- [ ] Visual feedback
  - Node color changes with ownership
  - Highlight on hover
  - Show battle status (icon/effect)

**Files to create:**
- `Node.cs`
- `NodeManager.cs`
- `NodeVisualizer.cs` - Handle node appearance

---

#### **2.3 Battle Context System** (2 hours) 🟡 MEDIUM PRIORITY
- [ ] Create BattleContext.cs (static/singleton)
  - Store: which node is being fought over
  - Store: attacking team, defending team
  - Accessible from both scenes
- [ ] Track battle results
  - Winner faction
  - Player performance stats (optional)
- [ ] Pass data between scenes
  - War Map → Battle: Set battle context
  - Battle → War Map: Return result

**Files to create:**
- `BattleContext.cs`

---

#### **2.4 Scene Transition** (2-3 hours) 🔴 HIGH PRIORITY
- [ ] War Map → Battle Scene
  - Click node to select
  - "Attack" button initiates battle
  - Load NetworkTest scene
  - Pass battle context
  - Spawn players for both teams
- [ ] Battle → War Map
  - Battle ends (victory screen)
  - "Return to War Map" button
  - Load WarMap scene
  - Update node ownership based on result
- [ ] Handle multiplayer scene transitions
  - Server controls scene loading
  - All clients load together
  - Maintain network connection

**Files to modify:**
- `ElitesNetworkManager.cs` - Scene management
- `GameModeManager.cs` - Battle completion
- Create: `SceneTransitionManager.cs`

---

### **Phase 2 Deliverable:**
✅ War map with 5 nodes visible  
✅ Nodes show ownership (Blue/Red/Neutral)  
✅ Can click node to start battle  
✅ Battle result updates node ownership  

**Test:** Start on war map, click node, battle loads, win/lose updates map

---

## 🟢 PHASE 3: WAR MAP INTEGRATION (Week 3)

### **Goal:** Complete game loop working smoothly

#### **3.1 Simplified Deployment** (4 hours) 🟡 MEDIUM PRIORITY
- [ ] Starting state
  - Blue owns HomeBase_Blue + 1 adjacent city
  - Red owns HomeBase_Red + 1 adjacent city
  - 1 city is neutral
- [ ] Attack mechanics (simplified)
  - Can only attack adjacent nodes
  - Click adjacent node to attack
  - No token costs (simplified for MVP)
  - Attacking initiates battle
- [ ] Basic token backend
  - Each team has "troops" count (placeholder)
  - Decrease on attack (optional)
  - Increase over time (optional)

**Files to create:**
- `DeploymentManager.cs`
- `DeploymentUI.cs`

---

#### **3.2 Battle Initiation Flow** (2 hours) 🔴 HIGH PRIORITY
- [ ] Select attackable node
  - Highlight adjacent nodes
  - Show "Attack" button when valid node selected
- [ ] Both teams join battle
  - Attacking team = offensive spawn
  - Defending team = defensive spawn
- [ ] Battle loads with context
  - KOTH objective at attacked node
  - Correct team spawns

**Files to modify:**
- `NodeManager.cs` - Node selection
- `BattleContext.cs` - Store attacker/defender

---

#### **3.3 Victory Conditions** (2 hours) 🟡 MEDIUM PRIORITY
- [ ] Battle result handling
  - Attackers win = capture node
  - Defenders win = keep node, repel attack
  - Update NodeManager with result
- [ ] War victory check
  - Check if one team controls 3/5 nodes (or 4/5)
  - Show war victory screen
  - Option to restart campaign
- [ ] Persistent war state
  - Track which nodes are owned
  - Save/load war map state (optional)

**Files to modify:**
- `NodeManager.cs` - Victory checking
- `GameModeManager.cs` - Battle result reporting
- Create: `WarVictoryScreen.cs`

---

#### **3.4 Match Flow Polish** (2 hours) 🟢 LOW PRIORITY
- [ ] Main menu scene
  - "Start War" button → War Map
  - "Quick Battle" button → Battle scene directly
  - Settings (optional)
- [ ] Post-battle summary
  - Show battle stats
  - Show war map impact
  - Highlight captured/defended node
- [ ] Smooth transitions
  - Loading screens between scenes
  - Fade in/out effects
  - Network reconnection handling

**Files to create:**
- Scene: `MainMenu.unity`
- `MainMenuManager.cs`
- `LoadingScreen.cs`

---

### **Phase 3 Deliverable:**
✅ Complete game loop functional  
✅ Can play multiple battles in sequence  
✅ War map updates after each battle  
✅ War victory condition works  

**Test:** Full campaign - attack nodes, win battles, capture map

---

## 📋 QUICK WINS (Can do anytime)

### **Visual Polish** (30 min each)
- [ ] Add crosshair to HUD
- [ ] Add muzzle flash effect
- [ ] Add hit markers (red X on hit)
- [ ] Add damage indicator (red vignette)
- [ ] Death camera (spectate killer)

### **Audio** (1 hour total)
- [ ] Gunshot sounds
- [ ] Reload sounds  
- [ ] Hit sounds
- [ ] Death sounds
- [ ] Footstep sounds

### **Environment** (2 hours)
- [ ] Add cover objects to battle map
- [ ] Better lighting
- [ ] Ground textures
- [ ] Simple skybox

---

## 🔵 BACKLOG (Post-MVP)

### **After Milestone 2:**
- [ ] Add Green faction (3rd team)
- [ ] More battle maps (urban, forest, desert)
- [ ] More gamemodes (Team Deathmatch, Capture the Flag)
- [ ] Complex token system (costs, resources, types)
- [ ] Multiple simultaneous battles
- [ ] Larger war map (10+ nodes)
- [ ] Vehicles
- [ ] More weapons
- [ ] Player progression/unlocks

---

## 📊 MILESTONE 2 PROGRESS TRACKER

```
Phase 1: Teams + KOTH        [░░░░░░░░░░░░░░░░░░░░]   0%
Phase 2: War Map Foundation  [░░░░░░░░░░░░░░░░░░░░]   0%
Phase 3: Integration         [░░░░░░░░░░░░░░░░░░░░]   0%

Overall Milestone 2:         [░░░░░░░░░░░░░░░░░░░░]   0%
```

---

## 🎯 IMMEDIATE NEXT STEPS

### **Next Session (2-3 hours):**
**Focus:** Start Phase 1 - Team System

1. Create TeamManager.cs (30 min)
2. Update player faction assignment (30 min)
3. Add team-colored materials (30 min)
4. Create spawn point system (1 hour)
5. Test Blue vs Red teams (30 min)

**Goal:** Players spawn on different teams with different colors

---

### **Session After That (3-4 hours):**
**Focus:** King of the Hill gamemode

1. Create ControlPoint.cs
2. Create GameModeManager.cs
3. Add control point to scene
4. Implement capture mechanics
5. Add victory/defeat conditions

**Goal:** Functional KOTH gamemode

---

## 📝 NOTES

### **Design Decisions:**
- **2 teams only** for MVP (Blue vs Red)
- **Simple token system** (just track count, no complex resource management)
- **One battle at a time** (no simultaneous battles)
- **5-node war map** (small, manageable)
- **KOTH only** for now (other modes later)

### **Testing Strategy:**
- Test each phase independently
- Phase 1: Need 4+ players for team balance testing
- Phase 2: Can test solo (war map visualization)
- Phase 3: Need 4+ players for full game loop

### **Success Criteria:**
- [ ] Teams are balanced (equal players)
- [ ] KOTH is fun and functional
- [ ] War map is clear and intuitive
- [ ] Scene transitions work smoothly
- [ ] Node ownership updates correctly
- [ ] War victory condition triggers
- [ ] No critical bugs or crashes

---

## 🚀 READY TO START!

**Current Status:** All Milestone 1 complete, ready for Milestone 2  
**Next Task:** Implement TeamManager.cs  
**Estimated Time to MVP:** 3-4 weeks

---

*This file is your development roadmap.  
Update progress as you complete each task!*

**Last updated:** November 4, 2025  
**Next review:** End of Phase 1 (Team System complete)
