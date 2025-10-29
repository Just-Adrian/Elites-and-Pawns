# Quick Start - New Session Instructions

## 📌 Project Context

**Project**: Elites and Pawns True  
**Type**: Asymmetrical FPS/RTS Hybrid (like Heroes & Generals)  
**Engine**: Unity 6000.2.8f1 with URP  
**Networking**: Mirror  
**Current Status**: Milestone 1 Complete (Working multiplayer)

---

## 🎯 What's Working Now

✅ **Multiplayer Foundation** - 2 players can connect and play  
✅ **Movement System** - WASD, sprint, jump (synchronized)  
✅ **First-Person Camera** - Mouse look with cursor lock  
✅ **Health System** - Damage, death, respawn (networked)  
✅ **Faction System** - Blue faction implemented (MVP)  

**Test Scene**: `Assets/_Project/Scenes/NetworkTest.unity`  
**Play Mode**: Press Play → Click "Start Host" in Game view

---

## 📂 Project Structure

```
Assets/_Project/
├── Scripts/
│   ├── Core/ (GameManager, GameEnums, Singleton)
│   ├── Networking/ (ElitesNetworkManager, NetworkPlayer)
│   └── Player/ (PlayerController, PlayerHealth)
├── Scenes/ (NetworkTest.unity)
└── Prefabs/ (Player.prefab)
```

**Namespaces**: `ElitesAndPawns.Core`, `.Networking`, `.Player`

---

## 📖 Key Documents

**Must Read:**
- **GDD.md** - Game design (3 factions, RTS/FPS hybrid, MVP scope)
- **TDD.md** - Technical architecture (networking, systems, milestones)
- **MILESTONE_1_COMPLETE.md** - What we've built so far

**Reference:**
- **ROADMAP.md** - Development phases
- **WORKFLOW.md** - Development workflow

---

## 🎮 Game Design (Quick Summary)

### Three Factions
- **BLUE (MVP)**: The Architects - Deployables, teamwork, tactical
- **RED**: The Destroyers - Heavy damage, environmental destruction
- **GREEN**: The Hunters - Mobile, long-range, fragile

### Core Loop
1. **War Map (RTS)**: Deploy tokens/squadrons to nodes
2. **Node Battles (FPS)**: Fight 8v8 battles using those tokens
3. **Victory**: Control majority of major cities

### MVP Scope (Current)
- Blue faction only
- Control Points game mode
- 8v8 battles, 15-minute matches
- 5-node war map
- Troops only (no vehicles yet)

---

## 🚀 Next Steps (Milestone 2)

### Immediate Tasks
1. **Shooting Mechanics** - Raycast shooting, hit detection
2. **Weapons** - Assault rifle with ammo/reload
3. **Combat UI** - Health bar, ammo counter, crosshair
4. **Damage System** - Connect shooting to PlayerHealth

### Then
- War Map prototype (5 nodes)
- Token system
- Squadron deployment
- Basic RTS controls

---

## 🔧 Development Context

**Senior Developer (Claude)**: That's me! I:
- Write production-ready C# code
- Follow the TDD architecture
- Use Mirror networking best practices
- Document everything with XML comments
- Test and validate before committing

**Project Manager (Adrian)**: You! You:
- Define requirements and priorities
- Review and approve features
- Test functionality
- Make design decisions

---

## 💻 Code Standards

- ✅ Use proper namespaces (`ElitesAndPawns.*`)
- ✅ XML documentation on all public methods
- ✅ Debug logging with toggle flags
- ✅ Server-authoritative for networking
- ✅ Follow existing naming conventions

---

## 🛠️ Common Commands

**Testing Multiplayer:**
```
1. Open NetworkTest scene
2. Press Play
3. Click "Start Host"
4. Move with WASD, look with mouse
```

**Commit Changes:**
```powershell
.\commit-milestone-1-complete.ps1  # If you haven't yet
git push
```

---

## 📝 Typical Session Start

When starting a new session, I should:

1. **Understand the task** - Ask clarifying questions
2. **Reference TDD** - Check architecture before coding
3. **Create files** - Write clean, documented code
4. **Test approach** - Validate with compilation check if needed
5. **Update docs** - Keep progress tracking current

**Questions I might ask:**
- What's the priority for this session?
- Any blockers from last session?
- Should I focus on a specific milestone task?

---

## ⚠️ Important Notes

- **Mirror is installed** - Don't reinstall
- **Input System**: Set to "Both" mode (supports legacy Input)
- **Network**: Server-authoritative (security first)
- **Testing**: Always test after major changes
- **Git**: Commit frequently with clear messages

---

## 🎯 Current Milestone Goals

**Milestone 2**: Combat Systems (Weeks 5-6)
- Shooting mechanics
- Basic weapons (assault rifle)
- Combat UI
- Damage integration

**Estimated Time**: 2-3 weeks for full Milestone 2

---

## 🔗 Quick Links

- **Mirror Docs**: https://mirror-networking.gitbook.io/
- **Unity Input System**: Using legacy for MVP
- **Git Repo**: Local only (push to remote when ready)

---

**Last Session End**: Milestone 1 complete, ready to commit  
**Next Task**: Commit Milestone 1, then start Milestone 2  
**Status**: ✅ Production-ready networking foundation

---

## 🗣️ How to Work With Me

**Good Request Example:**
```
"Add shooting mechanics to PlayerController. 
Raycast-based, left mouse button to shoot, 
deal 10 damage on hit, show muzzle flash. 
Use server-authoritative damage."
```

**I'll Respond With:**
1. Confirm understanding
2. Outline approach
3. Implement code
4. Test/validate
5. Update documentation

---

**Ready to continue building!** 🚀

*Created: October 28, 2025*  
*Use this to brief Claude in new sessions*
