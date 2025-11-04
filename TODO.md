# TODO - Elites and Pawns True

**Last Updated:** November 4, 2025  
**Current Status:** Milestone 1.5 Complete - Ready for Milestone 2

---

## 🎯 PRIORITY LEVELS

- 🔴 **CRITICAL** - Blocking issues, must fix immediately
- 🟡 **HIGH** - Important features, should do soon
- 🟢 **MEDIUM** - Nice to have, can wait
- 🔵 **LOW** - Polish/optional, do when time allows

---

## 🔴 CRITICAL (None!)

**All blocking issues resolved!** ✅

---

## 🟡 HIGH PRIORITY

### **Quick Wins (1-2 hours)**

#### 1. **Add Crosshair** (15 min)
- [ ] Create simple crosshair sprite (white cross)
- [ ] Add UI Image to center of PlayerHUD_Canvas
- [ ] Size: 32x32 pixels
- [ ] Color: White with slight transparency (200 alpha)

**Location:** `Assets/_Project/Scripts/UI/PlayerHUD.cs`  
**Impact:** Better aiming feedback

---

#### 2. **Create "Head" Tag** (5 min)
- [ ] Edit → Project Settings → Tags & Layers
- [ ] Add tag: "Head"
- [ ] Apply to head collider on Player prefab

**Impact:** Removes console warning, enables headshots

---

#### 3. **Add Spawn Points** (20 min)
- [ ] Create empty GameObjects in scene (4-6 points)
- [ ] Name them "SpawnPoint_1", "SpawnPoint_2", etc.
- [ ] Spread them around the map
- [ ] Update NetworkManager to use them

**Location:** NetworkTest scene  
**Impact:** Players spawn in different locations

---

#### 4. **Add Muzzle Flash** (30 min)
- [ ] Create simple particle system or sprite effect
- [ ] Spawn at firePoint when shooting
- [ ] Auto-destroy after 0.1 seconds
- [ ] Add to BaseWeapon.RpcOnWeaponFired()

**Impact:** Visual feedback when shooting

---

### **Testing & Polish (1 hour)**

#### 5. **Build & Test with Real Client** (30 min)
- [ ] Build executable (File → Build Settings)
- [ ] Test Host (executable) + Client (Unity Editor)
- [ ] Verify all features work in build
- [ ] Check for build-specific issues

**Impact:** Ensures game works outside editor

---

#### 6. **Add Basic Sound Effects** (30 min)
- [ ] Find/create free sound effects:
  - Gunshot (assault rifle)
  - Reload
  - Hit impact
  - Death
- [ ] Add AudioSource to weapons
- [ ] Play sounds in appropriate RPC methods

**Location:** `Assets/_Project/Audio/` (create folder)  
**Impact:** Much better game feel

---

## 🟢 MEDIUM PRIORITY

### **Milestone 2: War Map Prototype** (2-3 weeks)

#### 7. **Create War Map Scene** (2 hours)
- [ ] New scene: "WarMap.unity"
- [ ] 2D or 3D map representation
- [ ] 5 node positions (cities)
- [ ] Visual connections between nodes
- [ ] Camera setup for map view

**Design:** See GDD.md Section 3.2

---

#### 8. **Node System** (4 hours)
- [ ] Create Node.cs script
  - Node type (Minor City, Major City, Factory)
  - Owner faction
  - Connected nodes
  - Battle status
- [ ] Create NodeManager.cs
  - Track all nodes
  - Handle ownership changes
- [ ] Visual indicators for ownership

**Impact:** Foundation for War Map gameplay

---

#### 9. **Token System Backend** (3 hours)
- [ ] Create Token.cs enum (Troops, Light Vehicle, Heavy Vehicle, etc.)
- [ ] Create Squadron.cs class
  - Contains multiple tokens
  - Faction ownership
  - Node location
- [ ] Create TokenManager.cs
  - Track all squadrons
  - Validate deployments
  - Handle token consumption

**Impact:** RTS resource management

---

#### 10. **Basic Deployment UI** (3 hours)
- [ ] Create deployment panel UI
- [ ] Show available tokens for faction
- [ ] Click node to select deployment target
- [ ] Select tokens to deploy
- [ ] "Deploy" button to confirm

**Impact:** Player can interact with War Map

---

#### 11. **Battle Initiation** (2 hours)
- [ ] Detect when nodes are contested
- [ ] Trigger transition to FPS battle
- [ ] Pass squadron data to battle scene
- [ ] Load NetworkTest scene with correct players

**Impact:** Connects War Map to FPS battles

---

### **Additional Weapons** (4 hours)

#### 12. **Add Pistol** (1 hour)
- [ ] Create Pistol WeaponData ScriptableObject
  - Lower damage (20)
  - Faster fire rate
  - Smaller magazine (12 rounds)
- [ ] Create pistol prefab
- [ ] Add to WeaponManager weapons list

---

#### 13. **Add Shotgun** (1.5 hours)
- [ ] Create Shotgun WeaponData
  - High damage per pellet (15)
  - Multiple projectiles per shot (8)
  - Wide spread
  - Slow fire rate
- [ ] Test spread pattern
- [ ] Balance damage falloff

---

#### 14. **Weapon Switching Animation** (1.5 hours)
- [ ] Simple fade in/out or position tween
- [ ] Disable shooting during switch
- [ ] Add weapon equip sound

---

## 🔵 LOW PRIORITY (Polish)

### **Visual Polish**

#### 15. **Hit Markers** (30 min)
- [ ] Show red X on successful hit
- [ ] Flash for 0.2 seconds
- [ ] Position at crosshair center

---

#### 16. **Damage Indicator** (45 min)
- [ ] Red vignette flash when taking damage
- [ ] Direction indicator (red edge of screen)
- [ ] Fade out over 0.5 seconds

---

#### 17. **Kill Feed** (1 hour)
- [ ] Top-right corner UI
- [ ] Show last 5 kills
- [ ] Format: "Player1 [weapon] Player2"
- [ ] Fade out after 5 seconds

---

### **Map & Environment**

#### 18. **Better Test Environment** (2 hours)
- [ ] Add cover objects (walls, crates, buildings)
- [ ] Add lighting (directional light + baked)
- [ ] Add ground textures
- [ ] Create simple skybox

---

#### 19. **Multiple Maps** (per map: 3 hours)
- [ ] Urban map (buildings, streets)
- [ ] Forest map (trees, hills)
- [ ] Desert map (open, dunes, rocks)

---

### **Player Experience**

#### 20. **Player Customization** (2 hours)
- [ ] Name input on join
- [ ] Basic player colors/skins
- [ ] Save preferences locally

---

#### 21. **Settings Menu** (2 hours)
- [ ] Mouse sensitivity slider
- [ ] Volume controls
- [ ] Graphics quality settings
- [ ] Key rebinding

---

#### 22. **Death Camera** (1 hour)
- [ ] Spectate killer for 3 seconds
- [ ] Show "You were killed by [player]"
- [ ] Show respawn countdown

---

## 📋 BACKLOG (Future Milestones)

### **Milestone 3: Faction Diversity** (3-4 weeks)
- RED faction implementation
- GREEN faction implementation
- Faction-specific abilities/weapons
- Balanced gameplay testing

### **Milestone 4: Full Game Loop** (4-5 weeks)
- War Map → Battle → War Map cycle
- Victory conditions
- Match end screen
- Match replay system

### **Milestone 5: Content & Polish** (ongoing)
- More weapons (sniper, SMG, LMG)
- More maps
- Vehicle system
- Advanced RTS features
- Progression system

---

## 🛠️ TECHNICAL DEBT

### **Refactoring Needed** (Low Priority)
- [ ] Move magic numbers to config files
- [ ] Create proper weapon spawn system
- [ ] Refactor PlayerHUD to use events more
- [ ] Add object pooling for projectiles
- [ ] Improve network bandwidth (compress SyncVars)

---

## 📝 NOTES

### **Before Starting Each Task:**
1. Read relevant GDD.md sections
2. Check TDD.md for architecture guidelines
3. Update PROGRESS.md when complete
4. Test thoroughly before committing

### **Testing Checklist (After Major Changes):**
- [ ] Works in Editor (single player)
- [ ] Works with Host + Client
- [ ] No console errors
- [ ] Builds successfully
- [ ] Performance acceptable (60 FPS)

### **Commit Guidelines:**
- Make commits atomic (one feature at a time)
- Write clear commit messages
- Test before committing
- Update documentation

---

## 🎯 SUGGESTED SESSION GOALS

### **Next Session (2-3 hours):**
1. Add crosshair (15 min)
2. Create "Head" tag (5 min)
3. Add spawn points (20 min)
4. Add muzzle flash (30 min)
5. Build and test (30 min)
6. Add basic sounds (30 min)

**Result:** Polished combat experience

---

### **Following Session (3-4 hours):**
Start Milestone 2 - War Map:
1. Create War Map scene
2. Implement node system
3. Create basic deployment UI

**Result:** War Map prototype

---

## 💡 QUICK WINS FOR POLISH

**< 30 minutes each:**
- Add death sound effect
- Add respawn visual effect (fade in)
- Add footstep sounds
- Add health regeneration (slow)
- Add sprint stamina system
- Add weapon sway (slight camera movement)
- Add recoil pattern (camera kick)

---

## 📊 MILESTONE TRACKING

```
Milestone 1:   ████████████████████ 100% ✅
Milestone 1.5: ████████████████████ 100% ✅
Milestone 2:   ░░░░░░░░░░░░░░░░░░░░   0% ⏳
Milestone 3:   ░░░░░░░░░░░░░░░░░░░░   0% ⏳
Milestone 4:   ░░░░░░░░░░░░░░░░░░░░   0% ⏳
```

**MVP Completion:** ~25%

---

*This file is your development roadmap.  
Check items off as you complete them!  
Add new tasks as needed.*

**Last updated by:** Claude  
**Next review:** After Milestone 2 kickoff
