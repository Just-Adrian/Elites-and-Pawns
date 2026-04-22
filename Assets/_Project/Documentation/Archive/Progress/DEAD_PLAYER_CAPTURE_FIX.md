# Fix: Dead Players Can Still Capture Points

**Date:** November 21, 2025  
**Issue:** Dead players remain in capture zone lists and can still capture points  
**Root Cause:** Unity doesn't call `OnTriggerExit` when a collider is disabled while inside a trigger

---

## The Problem

When `PlayerHealth` disables all colliders on death:
```csharp
DisableColliders(); // Disables CharacterController + all colliders
```

Unity **does NOT** automatically fire `OnTriggerExit` for triggers the player was inside. This means:
- Dead players stay in `bluePlayersInZone` and `redPlayersInZone` lists
- They count toward capture progress
- They can capture points while dead ❌

---

## The Solution

Add a cleanup method to `ControlPoint.cs` that filters out dead/null players before counting them.

### Step 1: Add CleanupPlayerLists Method

Add this new method to `ControlPoint.cs` (place it before `UpdateCaptureProgress`):

```csharp
/// <summary>
/// Remove dead or null players from the zone lists.
/// Called every frame to handle players whose colliders were disabled without triggering OnTriggerExit.
/// </summary>
private void CleanupPlayerLists()
{
    // Clean blue players
    bluePlayersInZone.RemoveAll(player => 
    {
        if (player == null)
        {
            if (debugMode) Debug.Log("[ControlPoint] Removed null blue player from zone");
            return true;
        }
        
        var playerHealth = player.GetComponent<Player.PlayerHealth>();
        if (playerHealth != null && playerHealth.IsDead)
        {
            if (debugMode) Debug.Log($"[ControlPoint] Removed dead blue player {player.PlayerName} from zone");
            return true;
        }
        
        return false;
    });
    
    // Clean red players
    redPlayersInZone.RemoveAll(player => 
    {
        if (player == null)
        {
            if (debugMode) Debug.Log("[ControlPoint] Removed null red player from zone");
            return true;
        }
        
        var playerHealth = player.GetComponent<Player.PlayerHealth>();
        if (playerHealth != null && playerHealth.IsDead)
        {
            if (debugMode) Debug.Log($"[ControlPoint] Removed dead red player {player.PlayerName} from zone");
            return true;
        }
        
        return false;
    });
    
    // Also clean the dictionary
    var deadPlayers = new System.Collections.Generic.List<uint>();
    foreach (var kvp in playersInZone)
    {
        var player = System.Array.Find(
            bluePlayersInZone.ToArray().Concat(redPlayersInZone.ToArray()).ToArray(),
            p => p != null && p.netId == kvp.Key
        );
        
        if (player == null)
        {
            deadPlayers.Add(kvp.Key);
        }
    }
    
    foreach (uint netId in deadPlayers)
    {
        playersInZone.Remove(netId);
    }
}
```

### Step 2: Call CleanupPlayerLists in UpdateCaptureProgress

Modify the `UpdateCaptureProgress` method to call cleanup at the start:

**FIND THIS:**
```csharp
private void UpdateCaptureProgress()
{
    // Don't update if contested
    if (isContested)
```

**REPLACE WITH THIS:**
```csharp
private void UpdateCaptureProgress()
{
    // CRITICAL: Remove dead/null players from lists
    CleanupPlayerLists();
    
    // Don't update if contested
    if (isContested)
```

### Step 3: Also Call CleanupPlayerLists in UpdateContestedState

Modify `UpdateContestedState` to also clean up before checking contested status:

**FIND THIS:**
```csharp
private void UpdateContestedState()
{
    bool wasContested = isContested;
    isContested = bluePlayersInZone.Count > 0 && redPlayersInZone.Count > 0;
```

**REPLACE WITH THIS:**
```csharp
private void UpdateContestedState()
{
    // Clean up dead players before checking contested state
    CleanupPlayerLists();
    
    bool wasContested = isContested;
    isContested = bluePlayersInZone.Count > 0 && redPlayersInZone.Count > 0;
```

---

## Why This Works

1. **Every frame**, `Update()` calls `UpdateCaptureProgress()`
2. `UpdateCaptureProgress()` **first** calls `CleanupPlayerLists()`
3. `CleanupPlayerLists()` checks **every** player in the zone lists
4. If a player is dead (`playerHealth.IsDead == true`), they're removed from the list
5. Now capture counting only includes **alive** players ✅

---

## Testing Checklist

After applying the fix:

- [ ] Start a match with 2 players on opposite teams
- [ ] Have one player enter the capture zone
- [ ] Kill that player while they're in the zone
- [ ] **Verify:** Dead player does NOT capture the point
- [ ] **Verify:** Console shows "Removed dead [color] player from zone"
- [ ] Have the dead player respawn
- [ ] **Verify:** Respawned player can capture points again

---

## Additional Notes

### Why Not Fix OnTriggerExit?

You might wonder: "Why not make OnTriggerExit fire when we disable colliders?"

The problem is that `OnTriggerExit` is a Unity engine callback - we can't manually fire it reliably. The cleanup approach is:
- ✅ More reliable
- ✅ Handles all edge cases (null players, destroyed objects, etc.)
- ✅ Runs every frame so it's always up-to-date
- ✅ Minimal performance impact (only runs when there are players in zone)

### Alternative: Event-Based Approach

Another solution would be to have `PlayerHealth` broadcast a death event that `ControlPoint` listens to. But the cleanup approach is simpler and more robust since it also handles:
- Players disconnecting while in zone
- NetworkIdentity being destroyed
- Any other edge cases we haven't thought of

---

## Files Modified

- `ControlPoint.cs` - Added CleanupPlayerLists method, called in UpdateCaptureProgress and UpdateContestedState
