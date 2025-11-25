# War Map Unity Implementation Checklist

## 📋 Quick Setup Tasks

### Step 1: Create WarMap Scene
- [ ] Create new scene named "WarMap"
- [ ] Save to Assets/_Project/Scenes/WarMap
- [ ] Add to Build Settings

### Step 2: Setup Core GameObjects
- [ ] Create empty GameObject "NetworkManager"
  - [ ] Add `NetworkManager` component
  - [ ] Add `NetworkManagerHUD` component (for testing)
  - [ ] Add `WarMapManager` component
  - [ ] Add `NetworkIdentity` component
  
- [ ] Create empty GameObject "TokenSystem"
  - [ ] Add `TokenSystem` component
  - [ ] Add `NetworkIdentity` component
  
- [ ] Create empty GameObject "WarMapTestHarness"
  - [ ] Add `WarMapTestHarness` component
  - [ ] Enable `Auto Initialize` and `Show Debug GUI`

### Step 3: Create Node Prefab
- [ ] Create empty GameObject "WarMapNode"
- [ ] Add `WarMapNode` component
- [ ] Add 3D Sphere as child (for visual)
- [ ] Scale to (2, 2, 2)
- [ ] Add `NetworkIdentity` component
- [ ] Save as prefab in Assets/_Project/Prefabs/WarMap/
- [ ] Register in NetworkManager's Spawnable Prefabs

### Step 4: Quick Test
- [ ] Play scene
- [ ] Click "Host" in NetworkManagerHUD
- [ ] Check that 5 sphere nodes appear
- [ ] Use WarMapTestHarness GUI to test:
  - [ ] Add tokens to factions
  - [ ] Simulate battles
  - [ ] Change node control

## 🎯 Integration with FPS Battle

### Step 5: Modify NetworkTest Scene
- [ ] Open NetworkTest scene
- [ ] Add empty GameObject "BattleIntegration"
  - [ ] Add `BattleIntegration` component
  - [ ] Add `NetworkIdentity` component

### Step 6: Update ScoreNetworkSync
- [ ] Open ScoreNetworkSync.cs
- [ ] Add battle end check when score reaches 100:
```csharp
if (blueScore >= 100 || redScore >= 100)
{
    if (BattleIntegration.Instance != null)
    {
        // Battle will end automatically
    }
}
```

### Step 7: Test Battle Flow
- [ ] Start WarMap scene as Host
- [ ] Use test harness to start a battle
- [ ] Manually load NetworkTest scene
- [ ] Play a match to 100 points
- [ ] Check console for battle result logs

## 🎨 Optional: Basic UI Setup

### Step 8: Create UI Canvas
- [ ] Add Canvas to WarMap scene
- [ ] Set Canvas Scaler to Scale With Screen Size
- [ ] Reference Resolution: 1920x1080

### Step 9: Create Token Display
- [ ] Create Panel "TokenDisplay" (top of screen)
- [ ] Add 3 Text elements:
  - [ ] "BlueTokens": "Blue: 0"
  - [ ] "RedTokens": "Red: 0"  
  - [ ] "GreenTokens": "Green: 0"

### Step 10: Hook Up WarMapUI
- [ ] Add `WarMapUI` component to Canvas
- [ ] Link token text references
- [ ] Test token display updates

## ✅ Validation Tests

### Basic Functionality
- [ ] Tokens generate every 60 seconds
- [ ] Nodes can be attacked (costs 100 tokens)
- [ ] Battle results change node control
- [ ] Victory triggers at 4 nodes controlled

### Network Tests
- [ ] Host can modify tokens
- [ ] Clients see token updates
- [ ] Battle state syncs to clients
- [ ] Node control syncs to clients

## 🚀 Advanced Setup (Later)

### Scene Transitions
- [ ] Implement scene loading from WarMap to Battle
- [ ] Pass battle parameters through scene load
- [ ] Return to WarMap after battle ends

### Persistence
- [ ] Replace PlayerPrefs with proper data storage
- [ ] Save war state between sessions
- [ ] Implement match rejoining

### Visual Polish
- [ ] Replace sphere nodes with proper UI panels
- [ ] Add connection lines between nodes
- [ ] Implement node selection highlighting
- [ ] Add battle animation effects

## 📝 Notes

- Start with the TestHarness - it provides immediate functionality
- The sphere visualization is enough for testing
- Full UI can be implemented gradually
- Focus on core gameplay loop first

## 🐛 Common Issues

**Issue:** Nodes don't appear
- **Fix:** Check WarMapTestHarness is active and Auto Initialize is on

**Issue:** Can't attack nodes
- **Fix:** Ensure you have 100+ tokens (use test harness to add)

**Issue:** Tokens don't update
- **Fix:** Check TokenSystem has NetworkIdentity and is spawned

**Issue:** "Must be server" errors
- **Fix:** Always test as Host, not Client

## 💡 Quick Commands

In Play Mode with WarMapTestHarness:
- **G key:** Toggle GUI
- **1-5 keys:** Quick select nodes (if implemented)
- **Space:** Force token cycle (if implemented)

## 📊 Success Metrics

You know it's working when:
1. ✅ 5 nodes appear in scene
2. ✅ Token counter shows values
3. ✅ Can spend tokens to attack
4. ✅ Battle simulation changes node control
5. ✅ Victory triggers at 4 nodes

Start with Step 1 and work through sequentially. The TestHarness will give you a functional system immediately, even without proper UI!