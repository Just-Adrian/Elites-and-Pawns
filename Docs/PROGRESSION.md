# Player Progression System - Integration Guide

## Overview

This document outlines how to integrate a player progression system into Elites and Pawns True. The current architecture supports fixed squad configurations, but is designed to be extended for unlockable squad slots, types, and upgrades.

---

## Current Architecture

### How Squads Work Now

```
Player joins RTS server
    ↓
ElitesNetworkManager.OnServerAddPlayer()
    ↓
PlayerSquadManager.Initialize(faction, name, startNode)
    ↓
CreateInitialSquads() → Creates 3 identical Infantry squads
```

Each player currently gets:
- **3 squad slots** (fixed)
- **8 max manpower per squad** (fixed)
- **All Infantry type** (no variation)

---

## Progression Integration Points

### 1. Player Profile System

**New Files to Create:**
```
Assets/_Project/Scripts/Progression/
├── PlayerProfile.cs          # Player's unlocks and loadouts
├── SquadLoadout.cs           # Individual squad configuration
├── SquadType.cs              # Enum for squad types
├── ProgressionManager.cs     # Handles XP, levels, unlocks
└── ProfilePersistence.cs     # Save/load to database
```

**PlayerProfile.cs Structure:**
```csharp
namespace ElitesAndPawns.Progression
{
    [Serializable]
    public class PlayerProfile
    {
        public string PlayerId;           // Unique identifier (Steam ID, etc.)
        public string DisplayName;
        public int Level;                 // Player level (1-50+)
        public int Experience;            // Current XP
        public int UnlockedSquadSlots;    // 1-5 based on level
        public List<SquadLoadout> SquadLoadouts;
        public List<string> UnlockedUpgrades;
        public Dictionary<string, int> Statistics; // Kills, wins, etc.
    }
}
```

### 2. Squad Types

**SquadType.cs:**
```csharp
namespace ElitesAndPawns.Progression
{
    public enum SquadType
    {
        Infantry,      // Default: 8 manpower, balanced stats
        Mechanized,    // 6 manpower, +50% movement speed
        Armor,         // 4 manpower, +100% battle effectiveness, -30% speed
        Specialist     // 3 manpower, special abilities (recon, sabotage)
    }
    
    public static class SquadTypeData
    {
        public static int GetMaxManpower(SquadType type) => type switch
        {
            SquadType.Infantry => 8,
            SquadType.Mechanized => 6,
            SquadType.Armor => 4,
            SquadType.Specialist => 3,
            _ => 8
        };
        
        public static float GetMovementModifier(SquadType type) => type switch
        {
            SquadType.Infantry => 1.0f,
            SquadType.Mechanized => 1.5f,
            SquadType.Armor => 0.7f,
            SquadType.Specialist => 1.2f,
            _ => 1.0f
        };
    }
}
```

### 3. Squad Loadout

**SquadLoadout.cs:**
```csharp
namespace ElitesAndPawns.Progression
{
    [Serializable]
    public class SquadLoadout
    {
        public string LoadoutId;
        public string DisplayName;        // "Alpha Squad", "Tank Platoon"
        public SquadType Type;
        public List<string> Upgrades;     // Applied upgrade IDs
        
        // Computed values (from type + upgrades)
        public int EffectiveMaxManpower;
        public float EffectiveMovementSpeed;
        public float EffectiveBattleImpact;
    }
}
```

### 4. Upgrade System

**Potential Upgrades:**
| Upgrade ID | Name | Effect | Unlock Level |
|------------|------|--------|--------------|
| `veteran_bonus` | Veteran Training | +10% battle effectiveness | 5 |
| `extra_supplies` | Extra Supplies | +2 max manpower | 10 |
| `rapid_deploy` | Rapid Deployment | -20% travel time | 15 |
| `fortification` | Fortification | +25% defense when stationary | 20 |
| `medic_support` | Medic Support | Slower manpower drain in battle | 25 |
| `recon_intel` | Recon Intel | Reveal enemy squad positions | 30 |

---

## Files to Modify

### ElitesNetworkManager.cs
**Location:** `Assets/_Project/Scripts/Networking/ElitesNetworkManager.cs`

**Changes:**
1. Load `PlayerProfile` before spawning player
2. Pass profile to `PlayerSquadManager.Initialize()`

```csharp
public override void OnServerAddPlayer(NetworkConnectionToClient conn)
{
    // ... existing code ...
    
    // PROGRESSION: Load player profile from database
    // PlayerProfile profile = await ProfilePersistence.LoadProfile(playerId);
    // if (profile == null) profile = PlayerProfile.CreateDefault(playerId);
    
    if (player.TryGetComponent<PlayerSquadManager>(out var squadManager))
    {
        // CURRENT: Fixed initialization
        squadManager.Initialize(team, displayName, startingNode);
        
        // PROGRESSION: Pass profile for customized squads
        // squadManager.Initialize(team, displayName, startingNode, profile);
    }
}
```

### PlayerSquadManager.cs
**Location:** `Assets/_Project/Scripts/WarMap/PlayerSquadManager.cs`

**Changes:**
1. Add `Initialize()` overload accepting `PlayerProfile`
2. Create squads from loadouts instead of fixed count
3. Apply upgrades to squad stats

```csharp
// PROGRESSION: Add this overload
[Server]
public void Initialize(Team faction, string displayName, int startingNodeId, PlayerProfile profile)
{
    playerFaction = faction;
    playerDisplayName = displayName;
    
    // Create squads from player's loadouts
    for (int i = 0; i < profile.SquadLoadouts.Count && i < profile.UnlockedSquadSlots; i++)
    {
        CreateSquadFromLoadout(profile.SquadLoadouts[i], startingNodeId, i);
    }
    
    isManagerInitialized = true;
}

[Server]
private void CreateSquadFromLoadout(SquadLoadout loadout, int startingNodeId, int index)
{
    var squad = new Squad(
        netId,
        playerDisplayName,
        playerFaction,
        index,
        startingNodeId,
        loadout.EffectiveMaxManpower,
        loadout.Type
    );
    
    // Apply upgrade effects
    foreach (var upgradeId in loadout.Upgrades)
    {
        ApplyUpgradeToSquad(squad, upgradeId);
    }
    
    syncedSquads.Add(squad.ToSyncData());
}
```

### Squad.cs
**Location:** `Assets/_Project/Scripts/WarMap/Squad.cs`

**Changes:**
1. Add `SquadType Type` field
2. Add modifier fields for movement/effectiveness
3. Update constructor and sync methods

```csharp
// ADD these fields
public SquadType Type;
public float MovementSpeedModifier = 1.0f;
public float BattleEffectivenessModifier = 1.0f;

// MODIFY constructor
public Squad(uint ownerNetId, string ownerName, Team faction, int index, 
             int startNode, int maxManpower, SquadType type = SquadType.Infantry)
{
    // ... existing code ...
    Type = type;
    MovementSpeedModifier = SquadTypeData.GetMovementModifier(type);
}
```

### SquadSyncData (in Squad.cs)
**Changes:**
1. Add `SquadType` to sync struct
2. Add modifier fields

```csharp
public struct SquadSyncData
{
    // ... existing fields ...
    public int Type;  // Cast from SquadType enum
    public float MovementSpeedModifier;
    public float BattleEffectivenessModifier;
}
```

---

## Persistence Options

### Option A: PlayFab (Recommended for indie)
- Free tier available
- Built-in authentication
- Cloud saves, leaderboards, analytics

### Option B: Firebase
- Google ecosystem
- Real-time database
- Good for mobile

### Option C: Custom Backend
- Full control
- More development time
- Use with dedicated game servers

### Option D: Steam Cloud (if Steam-only)
- Simple integration
- Limited to Steam players
- Good for single-player progression

---

## UI Requirements

### New UI Screens Needed:
1. **Loadout Editor** - Configure squad types and upgrades
2. **Progression Screen** - Show level, XP, unlocks
3. **Squad Selection** - Choose which squads to deploy
4. **Post-Battle Summary** - XP gained, progression updates

### Integration with Existing UI:
- `WarMapUI` - Show squad types in squad panel
- `BattleUI` - Display squad type icons
- `PlayerHUD` - Show progression XP bar (optional)

---

## Implementation Order

### Phase 1: Foundation (1-2 weeks)
1. Create `SquadType` enum and data
2. Add `Type` field to `Squad` class
3. Update sync data structures
4. Test with hardcoded types

### Phase 2: Profiles (2-3 weeks)
1. Create `PlayerProfile` and `SquadLoadout` classes
2. Implement local save/load (JSON files)
3. Add profile-based initialization
4. Create basic loadout UI

### Phase 3: Persistence (2-3 weeks)
1. Choose and integrate backend (PlayFab/Firebase/Custom)
2. Implement authentication
3. Cloud save/load profiles
4. Handle offline mode

### Phase 4: Progression (2-3 weeks)
1. Implement XP and leveling system
2. Create unlock progression
3. Add upgrade system
4. Balance and tune

### Phase 5: Polish (1-2 weeks)
1. Full UI implementation
2. Tutorials and onboarding
3. Analytics integration
4. Bug fixes and balancing

---

## Quick Reference: Key Files

| File | Purpose | Progression Changes |
|------|---------|---------------------|
| `PlayerSquadManager.cs` | Squad creation | Load from profile |
| `Squad.cs` | Squad data | Add type and modifiers |
| `ElitesNetworkManager.cs` | Player spawning | Load profile first |
| `BattleManager.cs` | FPS battle logic | Read squad types for tickets |
| `WarMapManager.cs` | RTS logic | Apply type modifiers to movement |

---

## Notes

- All progression data should be **server-authoritative** to prevent cheating
- Consider **seasonal resets** for competitive modes
- **Free-to-play**: Progression can gate content, not power (cosmetics, squad skins)
- **Premium**: Faster progression, not exclusive power

---

*Last Updated: November 2024*
*Document Location: Assets/_Project/Documentation/PROGRESSION_INTEGRATION.md*
