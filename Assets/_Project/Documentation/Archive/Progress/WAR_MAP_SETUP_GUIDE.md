# War Map Integration - Setup Guide

## Overview

The War Map system provides the strategic layer for "Elites and Pawns True", connecting the RTS and FPS gameplay through a 5-node territorial control system with a token-based economy.

## Architecture

### Core Components

1. **WarMapNode.cs**
   - Represents individual territories on the map
   - Tracks control percentage and faction ownership
   - Handles battle states and token generation
   - Node types: Capital, Strategic, Resource, Standard

2. **WarMapManager.cs**
   - Central controller for the war map
   - Manages turn-based strategic gameplay
   - Handles battle initiation and resolution
   - Checks victory conditions
   - Network-synchronized state management

3. **TokenSystem.cs**
   - Bridges RTS and FPS layers
   - Manages faction resources
   - Processes battle costs and rewards
   - Handles token generation cycles

4. **WarMapUI.cs**
   - Displays faction status and node information
   - Provides strategic action controls
   - Shows battle status and results
   - Handles player interactions with the map

5. **BattleIntegration.cs**
   - Connects FPS battles to war map
   - Tracks battle statistics
   - Calculates battle results
   - Awards token rewards for FPS performance

## Scene Setup

### 1. Create War Map Scene

Create a new scene called "WarMap" with the following structure:

```
WarMap (Scene)
├── NetworkManager
│   └── ElitesNetworkManager (with WarMapManager component)
├── TokenSystem
│   └── GameObject with TokenSystem component
├── UI
│   ├── Canvas
│   │   ├── WarMapPanel
│   │   │   ├── NodeContainer (for node prefabs)
│   │   │   └── ConnectionContainer (for node connection lines)
│   │   ├── FactionInfoPanel
│   │   │   ├── BlueTokensText
│   │   │   ├── RedTokensText
│   │   │   └── GreenTokensText
│   │   ├── NodeDetailsPanel
│   │   │   ├── NodeNameText
│   │   │   ├── NodeControlBar
│   │   │   ├── AttackButton
│   │   │   ├── FortifyButton
│   │   │   └── ReinforceButton
│   │   └── BattlePanel
│   │       ├── BattleLocationText
│   │       ├── BattleFactionsText
│   │       └── JoinBattleButton
│   └── EventSystem
└── BattleIntegration (if using same scene for coordination)
```

### 2. Create Node Prefab

Create a prefab for war map nodes:

```
WarMapNode (Prefab)
├── NodeIcon (UI Image)
├── NodeNameText (Text)
├── ControlBar (UI Image - Filled type)
├── ContestedIndicator (GameObject)
├── BattleIndicator (GameObject)
└── WarMapNode.cs (Component)
```

### 3. Configure Network Objects

Add to your NetworkManager's registered spawnable prefabs:
- WarMapNode prefab
- Any other war map network objects

## Token System Configuration

### Token Generation
- Base tokens per cycle: 100
- Cycle duration: 60 seconds
- Node token generation:
  - Capital: 20 base + 1.5x multiplier
  - Strategic: 15 base + 1.25x multiplier
  - Resource: 25 base + 2x multiplier
  - Standard: 10 base + 1x multiplier

### Battle Costs
- Attack: 100 tokens
- Fortify: 75 tokens
- Reinforce: 50 tokens

### FPS Rewards
- Kill: 10 tokens
- Capture: 25 tokens
- Victory: 100 tokens
- Participation: 20 tokens

## Integration with FPS Battles

### 1. Modify NetworkTest Scene

Add BattleIntegration component to the scene:

```csharp
// In your battle scene's NetworkManager or GameModeManager
void Start()
{
    // Create BattleIntegration if it doesn't exist
    if (BattleIntegration.Instance == null)
    {
        GameObject battleInt = new GameObject("BattleIntegration");
        battleInt.AddComponent<BattleIntegration>();
    }
}
```

### 2. Connect Score System

Modify ScoreNetworkSync.cs to notify BattleIntegration:

```csharp
// In ScoreNetworkSync.cs
public static event System.Action<Team, int> OnScoreChanged;

private void UpdateBlueScore(int points)
{
    blueScore += points;
    OnScoreChanged?.Invoke(Team.Blue, blueScore);
    
    // Check for battle end
    if (BattleIntegration.Instance != null)
    {
        // BattleIntegration will check end conditions
    }
}
```

### 3. Connect Player Events

Modify PlayerHealth.cs to report kills:

```csharp
// Add to PlayerHealth.cs
public static event System.Action<string, string, Team> OnPlayerDeath;

[Server]
public void TakeDamage(float damage, string attackerId, Team attackerTeam)
{
    // ... existing damage code ...
    
    if (currentHealth <= 0 && !isDead)
    {
        OnPlayerDeath?.Invoke(netId.ToString(), attackerId, attackerTeam);
        Die();
    }
}
```

## Scene Flow

### 1. War Map → Battle
```csharp
// When starting a battle from war map
void StartBattle(int nodeID, Team attacker, Team defender)
{
    // Save battle parameters
    PlayerPrefs.SetInt("BattleNodeID", nodeID);
    PlayerPrefs.SetInt("AttackingFaction", (int)attacker);
    PlayerPrefs.SetInt("DefendingFaction", (int)defender);
    
    // Load battle scene
    NetworkManager.singleton.ServerChangeScene("NetworkTest");
}
```

### 2. Battle → War Map
```csharp
// When battle ends
void ReturnToWarMap(BattleResult result)
{
    // Save results
    SaveBattleResults(result);
    
    // Return to war map
    NetworkManager.singleton.ServerChangeScene("WarMap");
}
```

## Victory Conditions

The war ends when a faction achieves one of:
1. **Territory Control**: Control 4 of 5 nodes with 80%+ control
2. **Economic Victory**: Accumulate 5000 tokens
3. **Elimination**: Enemy factions have no nodes

## Dedicated Server Preparation

The system is designed for future dedicated server deployment:

### War Map Server
- Runs WarMapManager and TokenSystem
- Handles strategic decisions
- Manages faction resources
- Coordinates battle instances

### Battle Servers
- Run individual FPS matches
- Report results back to War Map Server
- Can run multiple simultaneous battles

### Data Persistence Layer
```csharp
// Future implementation structure
public interface IWarMapPersistence
{
    void SaveWarState(WarState state);
    WarState LoadWarState();
    void SaveBattleResult(int nodeID, BattleResult result);
    List<BattleResult> GetBattleHistory(int nodeID);
}
```

## Testing the System

### 1. Single Player Test
```csharp
// Add to WarMapManager for testing
[ContextMenu("Simulate Battle Victory")]
void TestBattleVictory()
{
    if (warMapNodes.Count > 0)
    {
        var result = new BattleResult
        {
            WinnerFaction = Team.Blue,
            LoserFaction = Team.Red,
            ControlChange = 25f,
            TokensWon = 200
        };
        
        EndBattle(0, result);
    }
}
```

### 2. Token Generation Test
```csharp
[ContextMenu("Force Token Cycle")]
void ForceTokenCycle()
{
    ProcessTokenCycle();
}
```

### 3. Victory Condition Test
```csharp
[ContextMenu("Test Victory Conditions")]
void TestVictory()
{
    // Give Blue 4 nodes
    for (int i = 0; i < 4; i++)
    {
        warMapNodes[i].SetControl(Team.Blue, 100f);
    }
    CheckVictoryConditions();
}
```

## UI Prefab Setup Guide

### Node Visual Setup
1. Create UI Panel (200x100 size)
2. Add Image component for node icon
3. Add filled Image for control bar
4. Add Text for node name
5. Add Button component for selection
6. Configure colors for each faction

### Connection Line Setup
1. Create LineRenderer prefab
2. Set width to 2-3 pixels
3. Use UI shader
4. Set sorting order below nodes

## Next Development Steps

1. **Immediate Tasks**
   - Create WarMap scene with UI
   - Create node prefabs
   - Test token generation
   - Test battle initiation flow

2. **Phase 2 Integration**
   - Connect battle scene transitions
   - Implement result persistence
   - Add player faction assignment
   - Create faction selection lobby

3. **Phase 3 Polish**
   - Add visual effects for battles
   - Implement node animations
   - Add sound effects
   - Create victory cinematics

## Common Issues & Solutions

### Issue: Nodes not connecting
**Solution**: Ensure ConnectToNodes() is called in Start(), not Awake()

### Issue: Tokens not syncing
**Solution**: Check that TokenSystem has NetworkIdentity and is spawned

### Issue: Battle results not applying
**Solution**: Verify BattleIntegration is saving results before scene change

### Issue: UI not updating
**Solution**: Ensure UI components subscribe to events in Start()

## Performance Considerations

- Limit simultaneous battles to 2-3
- Use object pooling for UI elements
- Cache node references
- Batch token updates

## Debug Commands

Add these to WarMapManager for testing:

```csharp
[Header("Debug")]
[SerializeField] private bool debugMode = true;

[ContextMenu("Add 1000 Tokens to Blue")]
void Debug_AddBlueTokens()
{
    TokenSystem.Instance.AddTokens(Team.Blue, 1000, "Debug");
}

[ContextMenu("Start Test Battle")]
void Debug_StartBattle()
{
    StartBattle(warMapNodes[2], Team.Blue);
}

[ContextMenu("Win War for Red")]
void Debug_RedVictory()
{
    EndWar(Team.Red);
}
```

## Architecture Notes

The system is designed with separation of concerns:
- **WarMapManager**: Strategic game logic
- **TokenSystem**: Economic layer
- **BattleIntegration**: FPS-RTS bridge
- **WarMapUI**: Presentation layer

This separation allows for:
- Easy migration to dedicated servers
- Independent testing of components
- Flexible UI implementation
- Scalable architecture

## Conclusion

The War Map system provides a robust strategic layer that seamlessly integrates with the FPS combat. The token economy creates meaningful choices and progression, while the node control system provides clear objectives and victory conditions.

The architecture is prepared for dedicated server deployment, allowing the war map and battles to run on separate servers when scaling is needed.