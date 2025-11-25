# URGENT: Quick Compilation Error Fixes

## The Problem
You have 8 compilation errors because:
1. **WarMapUI.cs** still references the removed turn-based system
2. **WarMapTestHarness.cs** has networking code that doesn't work

## The Solution (2 Files to Fix)

### Option 1: Manual Edits (5 minutes)

#### Fix WarMapUI.cs
Open `Assets/_Project/Scripts/WarMap/WarMapUI.cs`:

1. **Line 132** - DELETE this line:
```csharp
WarMapManager.OnTurnChanged += OnTurnChanged;
```

2. **Line 150** - DELETE this line:
```csharp
WarMapManager.OnTurnChanged -= OnTurnChanged;
```

3. **Line 271** - REPLACE this line:
```csharp
bool isPlayerTurn = (WarMapManager.Instance.CurrentTurn == playerFaction);
```
WITH:
```csharp
bool isPlayerTurn = true; // Real-time: all factions can always act
```

4. **Find and DELETE** the entire `OnTurnChanged` method (around line 430):
```csharp
private void OnTurnChanged(Team newTurn)
{
    Debug.Log($"[WarMapUI] Turn changed to: {newTurn}");
    RefreshDisplay();
}
```

5. **Find and REPLACE** the `UpdateTurnDisplay()` method (around line 213) with:
```csharp
private void UpdateWarStateDisplay()
{
    if (WarMapManager.Instance == null)
        return;
    
    // Display real-time war state instead of turns
    if (currentTurnText != null)
    {
        currentTurnText.text = $"War State: {WarMapManager.Instance.CurrentState}";
        currentTurnText.color = Color.white;
    }
    
    if (turnNumberText != null)
    {
        int activeBattles = WarMapManager.Instance.ActiveBattleCount;
        turnNumberText.text = $"Active Battles: {activeBattles}";
    }
}
```

6. **Line 197** - REPLACE:
```csharp
UpdateTurnDisplay();
```
WITH:
```csharp
UpdateWarStateDisplay();
```

#### Fix WarMapTestHarness.cs
Open `Assets/_Project/Scripts/WarMap/WarMapTestHarness.cs`:

1. **Around line 117-118** - DELETE these two lines:
```csharp
var transport = nmGO.AddComponent<kcp2k.KcpTransport>();
Transport.activeTransport = transport;
```

The `EnsureNetworkManager()` method should look like this:
```csharp
void EnsureNetworkManager()
{
    networkManager = FindObjectOfType<NetworkManager>();
    
    if (networkManager == null)
    {
        Debug.Log("[WarMapTest] No NetworkManager found, creating one...");
        GameObject nmGO = new GameObject("NetworkManager");
        networkManager = nmGO.AddComponent<NetworkManager>();
        
        Debug.Log("[WarMapTest] NetworkManager created");
    }
}
```

### Option 2: Use My Fixed Files

I've created completely fixed versions of both files. If you want to use them:

1. I'll write out the complete fixed files in separate response
2. You can copy/paste them to replace your current files

Which option do you prefer? The manual edits will take about 5 minutes, or I can provide the complete fixed files for you to copy/paste.

## After Fixing

Once fixed, Unity should compile successfully and you can:
1. Press Play
2. See the debug GUI
3. Use all the buttons that weren't working before
4. Test the real-time war map system!

Let me know which approach you'd like and I'll help you get it working!
