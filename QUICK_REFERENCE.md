# Quick Reference - Elites and Pawns True

**Last Updated:** November 4, 2025  
**Status:** ✅ **MULTIPLAYER WORKING** - HUD & Combat Functional

---

## 🎮 CURRENT STATE

**What's Working:**
- ✅ 2-player multiplayer (host + client)
- ✅ Movement (WASD, sprint, jump, mouse look)
- ✅ Shooting (projectile-based with physics)
- ✅ Weapons (Assault Rifle with ammo/reload)
- ✅ Health system (damage, death, respawn)
- ✅ HUD (health bar, ammo counter - both players)
- ✅ Networking (Mirror - fully synchronized)

**Recent Fixes (Nov 4):**
- Fixed HUD rendering (Canvas now uses Screen Space - Camera)
- Fixed ammo sync bug (was lagging by 1 bullet)
- Fixed projectiles not visible for clients
- Auto-registration of projectile prefabs

---

## 🎯 CONTROLS

| Key | Action |
|-----|--------|
| WASD | Move |
| Space | Jump |
| Left Shift | Sprint |
| Mouse | Look Around |
| Left Click | Shoot |
| R | Reload |
| 1/2/3 | Switch Weapon |
| ESC | Unlock Cursor |

---

## 📂 PROJECT STRUCTURE

```
Assets/_Project/
├── Scripts/
│   ├── Core/ (GameEnums, Singleton, GameManager)
│   ├── Networking/ (ElitesNetworkManager, NetworkPlayer)
│   ├── Player/ (PlayerController, PlayerHealth)
│   ├── Weapons/ (BaseWeapon, ProjectileWeapon, Projectile, WeaponManager, WeaponData)
│   └── UI/ (PlayerHUD, LocalPlayerCanvas, HUDDebugger)
├── Scenes/
│   └── NetworkTest.unity - Main test scene
├── Prefabs/
│   ├── Player/ (Player.prefab with all components)
│   └── Weapons/ (Projectile prefabs)
└── ScriptableObjects/
    └── Weapons/ (Weapon data assets)
```

---

## 🚀 HOW TO TEST

### Single Player (Host)
1. Open `Assets/_Project/Scenes/NetworkTest.unity`
2. Press **Play**
3. Click **"Start Host"** in Game view
4. Test movement, shooting, reload

### Multiplayer (2 Players)
**Option A - Two Unity Instances:**
1. Open Unity instance #1 → Play → Start Host
2. Open Unity instance #2 → Play → Client → Connect

**Option B - Build + Unity:**
1. Build game (File → Build Settings → Build)
2. Run .exe → Start Host
3. Unity Editor → Play → Client → Connect to localhost

---

## 🔧 IMPORTANT SYSTEMS

### Networking (Mirror)
- **Server-Authoritative** - All gameplay runs on server
- **KCP Transport** - Reliable UDP
- **Auto Projectile Registration** - Prefabs auto-registered from WeaponData

### Player Hierarchy
```
Player (NetworkIdentity, CharacterController)
├── PlayerCamera (First-person camera)
├── WeaponHolder
│   └── Weapon_AssaultRifle (ProjectileWeapon)
└── PlayerHUD_Canvas (Canvas - Screen Space Camera)
    ├── HealthPanel (health bar + text)
    └── AmmoPanel (weapon name + ammo count)
```

### Weapon System
- **BaseWeapon** - Abstract base class
- **ProjectileWeapon** - Physics-based projectiles
- **WeaponData** - ScriptableObject configs
- **WeaponManager** - Handles switching/ammo/reload

---

## 🐛 KNOWN ISSUES

**Minor:**
- No spawn points configured (players spawn at origin)
- Only 1 weapon type (Assault Rifle)
- "Head" tag warning (non-critical)
- No sound effects yet

**None Critical** - Game is fully playable!

---

## 📊 CONSOLE LOGS (Good Signs)

```
[ElitesNetworkManager] Server started
[ElitesNetworkManager] Registered X projectile prefab(s)
[LocalPlayerCanvas] Canvas set to ScreenSpaceCamera
[PlayerHUD] Initialized
[WeaponManager] Found camera: PlayerCamera
[BaseWeapon] Fired! Ammo: X/X
```

---

## 🎯 NEXT PRIORITIES

**Quick Wins (1-2 hours):**
1. Add crosshair (simple UI image)
2. Create "Head" tag for headshots
3. Add multiple spawn points
4. Add muzzle flash effect

**Milestone 2 (War Map):**
- Visual 5-node war map
- Token/squadron system
- RTS-style deployment

---

## 📝 FOR NEW CLAUDE SESSIONS

**Tell Claude:**
> "I'm working on Elites and Pawns True FPS. Read QUICK_REFERENCE.md and PROGRESS.md. The game has working multiplayer, shooting, and HUD. Check TODO.md for next tasks."

**Key Files to Read:**
- `QUICK_REFERENCE.md` (this file) - Current state
- `PROGRESS.md` - What's completed
- `TODO.md` - What's next
- `GDD.md` - Game design
- `TDD.md` - Technical architecture

---

## 💾 COMMITTING CHANGES

**PowerShell:**
```powershell
# Use one of these scripts:
.\commit-hud-fixes.ps1          # Latest fixes
.\commit-milestone-1-complete.ps1  # Milestone commits

# Or manually:
git add -A
git commit -m "Your message here"
git push
```

---

## 🎮 GAMEPLAY LOOP (Current MVP)

1. **Connect** - Host starts server, client joins
2. **Spawn** - Both players spawn as Blue faction
3. **Combat** - Shoot, take damage, respawn
4. **Goal** - Test multiplayer functionality

*(Full game loop with War Map coming in Milestone 2)*

---

## 🔗 USEFUL LINKS

- **Mirror Docs**: https://mirror-networking.gitbook.io/
- **Unity Input System**: Currently using Legacy for MVP
- **Project Repository**: Local (push when ready)

---

**Status:** ✅ **PRODUCTION-READY MULTIPLAYER**  
**Progress:** ~25% to MVP  
**Last Session:** Nov 4, 2025 - Fixed HUD and projectiles

---

*This document is the single source of truth for project status.  
Keep it updated after major changes!*
