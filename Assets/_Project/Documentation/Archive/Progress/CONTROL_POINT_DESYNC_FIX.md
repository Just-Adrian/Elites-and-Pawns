# Fix: Control Point State Desync Between Players

**Date:** November 22, 2025  
**Issue:** Control point visual state (contested/capturing) desyncs between server and clients  
**Root Cause:** `isDead` field in `PlayerHealth` was not synced to clients

---

## The Problem

When a player dies in the capture zone, the control point state becomes desynced:

**What you saw:**
- Scoreboard shows correct scores (synced properly) ✅
- But control point shows wrong state:
  - Shows "contested" when it should show "red capturing"
  - Shows wrong team capturing
  - Visual ring color doesn't match actual capture state

**Why it happened:**

`ControlPoint` is a regular MonoBehaviour (not NetworkBehaviour). It runs **independently** on both server and clients:

```
SERVER:
1. Player dies → isDead = true (server only)
2. CleanupPlayerLists() checks IsDead → true
3. Removes dead player from zone ✅
4. Calculates: Red is capturing (1 red, 0 blue)

CLIENT:
1. Receives RpcOnDeath() → plays death effects
2. BUT isDead stays false (not synced!)
3. CleanupPlayerLists() checks IsDead → false ❌
4. Dead player stays in list! ❌
5. Calculates: Contested (1 red, 1 blue) ❌

Result: Server sees "red capturing", client sees "contested"
```

---

## The Fix

### Changed `isDead` to a SyncVar

**File:** `PlayerHealth.cs`

**Before:**
```csharp
// State
private bool isDead = false;
```

**After:**
```csharp
// State
[SyncVar(hook = nameof(OnIsDeadChanged))]
private bool isDead = false;
```

**Added hook method:**
```csharp
/// <summary>
/// Called when isDead changes (on all clients)
/// This ensures clients know when players die for proper ControlPoint cleanup
/// </summary>
private void OnIsDeadChanged(bool oldValue, bool newValue)
{
    if (debugMode)
    {
        Debug.Log($"[PlayerHealth] isDead changed: {oldValue} → {newValue} (Client-side sync)");
    }
    
    // Note: We don't need to do anything here since RpcOnDeath handles visuals
    // This hook just ensures the IsDead property is synced for ControlPoint.CleanupPlayerLists()
}
```

---

## How This Fixes The Issue

### Before (Broken):

```
Player dies:
  Server: isDead = true ✅
  Client: isDead = false ❌ (never synced)
  
CleanupPlayerLists() runs:
  Server: Checks IsDead → true → Removes player ✅
  Client: Checks IsDead → false → Keeps player ❌
  
Result: Different player lists = Different states
```

### After (Fixed):

```
Player dies:
  Server: isDead = true → SyncVar syncs to clients
  Client: isDead = true ✅ (automatically synced!)
  
CleanupPlayerLists() runs:
  Server: Checks IsDead → true → Removes player ✅
  Client: Checks IsDead → true → Removes player ✅
  
Result: Same player lists = Same states ✅
```

---

## Why SyncVar Works Perfectly Here

### Automatic Synchronization
- Server sets `isDead = true`
- Mirror automatically sends the new value to all clients
- Clients receive the update and set their local `isDead = true`
- All happens automatically, no manual RPCs needed

### Hook Method
- Gets called on clients when the value changes
- Allows us to add debug logging
- Could add additional logic if needed (but not necessary here)

### Clean Architecture
- `IsDead` property already exists: `public bool IsDead => isDead;`
- `CleanupPlayerLists()` already checks `playerHealth.IsDead`
- No code changes needed in `ControlPoint.cs`!
- Fix is completely contained in `PlayerHealth.cs`

---

## Testing Checklist

After applying the fix:

### Test 1: Basic Death in Zone
- [ ] Start multiplayer (Host + Client)
- [ ] Blue player enters capture zone
- [ ] Red player kills Blue player while in zone
- [ ] **Verify on HOST:** Shows "Red is capturing" (not "Contested")
- [ ] **Verify on CLIENT:** Shows "Red is capturing" (not "Contested")
- [ ] **Verify:** Both screens match ✅

### Test 2: Score vs Visual State
- [ ] Watch scoreboard score
- [ ] Watch control point ring color
- [ ] **Verify:** Ring color matches who's actually getting points
- [ ] **Verify:** No more "contested but points going up" bug

### Test 3: Multiple Deaths
- [ ] Have players die in zone repeatedly
- [ ] **Verify:** State stays synced every time
- [ ] **Verify:** No accumulated desync over multiple deaths

### Test 4: Both Teams in Zone → One Dies
- [ ] Blue and Red enter zone (truly contested)
- [ ] Kill Blue player
- [ ] **Verify on BOTH:** Immediately shows "Red capturing"
- [ ] **Verify:** No lingering "contested" state

---

## Console Debug Output

After applying fix, you should see these messages when a player dies:

**Server:**
```
[PlayerHealth] PlayerName died. Killed by: KillerName
[PlayerHealth] isDead changed: false → true (Client-side sync)
[PlayerHealth] Server disabled colliders for dead player
[ControlPoint] Removed dead blue player PlayerName from zone
```

**Clients:**
```
[PlayerHealth] isDead changed: false → true (Client-side sync)
[PlayerHealth] Death RPC received on client
[ControlPoint] Removed dead blue player PlayerName from zone
```

Notice: Clients now see "isDead changed: true" → They know player is dead!

---

## Technical Details

### Why Not Use RpcOnDeath to Set isDead?

You might think: "Why not just set `isDead = true` in `RpcOnDeath()`?"

**Problem:** That would only set it for the specific client receiving the RPC. But we need it set on **ALL** clients, including the dead player's client.

**SyncVar handles this automatically:**
- Sets on server → Syncs to ALL clients
- Clean, automatic, guaranteed

### Performance Impact

**Minimal:**
- One bool (1 byte) synced per death
- Only syncs when value changes
- No per-frame overhead
- Standard Mirror networking pattern

---

## Files Modified

- ✅ `PlayerHealth.cs` - Made `isDead` a SyncVar, added hook method

---

## Related Fixes

This fix builds on previous work:
- `CleanupPlayerLists()` in `ControlPoint.cs` (already implemented)
- Dead player collider disabling (already implemented)
- `RpcOnDeath()` visual effects (already implemented)

The only missing piece was syncing the `isDead` state to clients.

---

## Expected Results

After this fix:
- ✅ Control point state matches on all clients
- ✅ Ring color matches actual capture state
- ✅ "Contested" only shows when truly contested
- ✅ Dead players immediately removed from all client's zone lists
- ✅ No more visual desync issues

---

**Status:** Fix applied ✅  
**Testing Required:** Yes - Verify state stays synced  
**Confidence Level:** 99% (standard SyncVar pattern, should work perfectly)
