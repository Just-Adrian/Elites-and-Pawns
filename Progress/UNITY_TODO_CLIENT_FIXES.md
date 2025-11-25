# Unity Editor TODO - Client Bug Fixes

**Date:** November 22, 2025  
**Status:** Code fixes applied ✅ - Need Unity Editor checks & testing

---

## What I Fixed (Code - Already Done ✅)

### 1. Dead Player Capture Fix ✅
- **File:** `ControlPoint.cs`
- **Changes:**
  - Added `CleanupPlayerLists()` method
  - Calls cleanup in `UpdateCaptureProgress()` and `UpdateContestedState()`
  - Dead/null players are now removed from capture zone lists every frame

### 2. Respawn Teleportation Fix ✅
- **File:** `PlayerHealth.cs`
- **Changes:**
  - Added `RpcSyncPosition()` method to explicitly sync position to clients
  - Modified `Respawn()` to call `RpcSyncPosition()` after teleporting
  - Added extra debug logging with spawn position

---

## What YOU Need to Do (Unity Editor)

### Step 1: Open Unity and Let Scripts Compile

1. Open Unity Editor
2. Wait for scripts to compile (check bottom right corner)
3. Watch Console for any compilation errors
4. **If errors:** Read them carefully and let me know

### Step 2: Check Spawn Points in Scene (CRITICAL!)

**This is the most likely cause of respawn issues!**

1. **Open your multiplayer test scene**
2. **In Hierarchy, search for: "SpawnPoint"**
3. **Check if spawn points exist:**

   **If NO spawn points exist:**
   - You need to create them (see Step 3)
   
   **If spawn points exist:**
   - Select each spawn point
   - In Inspector, check `SpawnPoint` component:
     - **Team Owner:** Must be `Blue` or `Red` (NOT `None`!)
     - **Is Active Spawn Point:** Must be checked ✅
   - Verify positions are far from control point and on opposite sides

### Step 3: Create Spawn Points (If Missing)

**For Blue Team:**
1. Hierarchy → Right-click → Create Empty
2. Name it: `SpawnPoint_Blue`
3. Add Component → `SpawnPoint` script
4. In Inspector:
   - Team Owner: `Blue`
   - Is Active Spawn Point: ✅ checked
5. Position it on blue side of map (e.g., transform.position = (-20, 0, 0))

**For Red Team:**
1. Hierarchy → Right-click → Create Empty
2. Name it: `SpawnPoint_Red`
3. Add Component → `SpawnPoint` script
4. In Inspector:
   - Team Owner: `Red`
   - Is Active Spawn Point: ✅ checked
5. Position it on red side of map (e.g., transform.position = (20, 0, 0))

**Visual Guide:**
```
[Blue Spawn]  ←  20 units  →  [Control Point]  ←  20 units  →  [Red Spawn]
    (-20, 0, 0)                    (0, 0, 0)                     (20, 0, 0)
```

### Step 4: Save Scene

- File → Save (Ctrl+S)
- Verify scene is saved

---

## Testing Checklist

### Test 1: Dead Player Capture (15 minutes)

**Setup:**
1. Start as Host
2. Have another player join as Client
3. Both players should be on opposite teams (Blue vs Red)

**Test Steps:**
1. **Client enters capture zone**
2. **Watch Console:** Should see `[ControlPoint] Blue/Red player entered: PlayerName`
3. **Host kills the Client** (shoot until health = 0)
4. **Watch Console:** Should see `[ControlPoint] Removed dead player from zone`
5. **Verify:** Dead player's team does NOT capture the point
6. **Wait 3 seconds** for respawn
7. **Verify:** Respawned player can capture again

**Expected Results:**
- ✅ Dead player removed from zone list
- ✅ Dead player cannot capture
- ✅ Console shows "Removed dead player" message
- ✅ After respawn, player can capture again

### Test 2: Respawn Teleportation (15 minutes)

**Setup:**
1. Start as Host
2. Have Client join
3. Note where the spawn points are positioned

**Test Steps:**
1. **Kill a player** (shoot until health = 0)
2. **Wait 3 seconds** for respawn
3. **Check Console** (BOTH host and client):
   - Should see: `[PlayerHealth] PlayerName teleported to SpawnPoint_X at position (x, y, z)`
   - Should NOT see: `No spawn point found`
4. **Check Host's view:** Does player appear at spawn point?
5. **Check Client's view:** Does player appear at spawn point?
6. **Verify:** Player is NOT at death location

**Expected Results:**
- ✅ Console shows "teleported to SpawnPoint_X" message
- ✅ Console shows position coordinates
- ✅ Player appears at team's spawn point (not death location)
- ✅ Both host AND client see player at spawn point
- ✅ Player can move and shoot normally after respawn

### Test 3: Combined Test (10 minutes)

**Test the complete death → respawn → capture flow:**

1. **Player enters capture zone and starts capturing**
2. **Kill player while in zone**
3. **Verify:** Capture stops (dead player removed)
4. **Wait 3 seconds** for respawn
5. **Verify:** Player appears at spawn point
6. **Player returns to capture zone**
7. **Verify:** Can capture points again

---

## Common Issues & Solutions

### Issue: "No spawn point found for X faction"

**Problem:** Spawn points don't exist or are misconfigured

**Solutions:**
- [ ] Create spawn points (Step 3)
- [ ] Check Team Owner is set to Blue/Red (not None)
- [ ] Check Is Active Spawn Point is checked ✅

### Issue: Player respawns at death location

**Problem:** Spawn points missing or network sync issue

**Solutions:**
- [ ] Verify spawn points exist and are configured
- [ ] Check Console for "teleported to" message
- [ ] If message appears but still wrong location → Tell me, there might be a deeper issue

### Issue: Dead player still captures points

**Problem:** Code didn't compile or there's a deeper issue

**Solutions:**
- [ ] Check Console for compilation errors
- [ ] Verify ControlPoint.cs compiled successfully
- [ ] Try stopping/restarting Unity Editor completely
- [ ] If still broken → Send me the Console output

### Issue: Compilation errors

**Most likely errors:**
- `'Player' is an ambiguous reference` → Already namespace-qualified, should be fine
- `CleanupPlayerLists` not recognized → Check if file saved correctly

**Solution:**
- [ ] Read the full error message
- [ ] Tell me the error and I'll help fix it

---

## What to Tell Me After Testing

### If Everything Works ✅

Tell me:
- "Both fixes working! Dead players can't capture, respawn teleports correctly."
- We can move on to next milestone!

### If Something Doesn't Work ❌

Tell me:
1. **Which issue:** Dead player capture OR respawn teleport?
2. **What happened:** Describe what you see
3. **Console output:** Copy/paste any relevant messages or errors
4. **Spawn points:** Do they exist? How are they configured?

---

## Quick Diagnosis

```
Dead player still capturing?
  ├─ Check Console for "Removed dead player" message
  │  ├─ YES → Code working, might be timing issue
  │  └─ NO → Code not running, check compilation
  └─ Try: Restart Unity Editor completely

Player not teleporting to spawn?
  ├─ Check Console for "No spawn point found"
  │  ├─ YES → Create spawn points (Step 3)
  │  └─ NO → Check "teleported to SpawnPoint_X" message
  │         ├─ Message exists but wrong location → Network sync issue
  │         └─ No message at all → Respawn() not being called
  └─ Try: Create spawn points, verify Team Owner is Blue/Red
```

---

## Files Modified (For Reference)

- ✅ `Assets/_Project/Scripts/GameModes/ControlPoint.cs` - Dead player cleanup
- ✅ `Assets/_Project/Scripts/Player/PlayerHealth.cs` - Respawn position sync

---

**Next Steps After Testing:**
- If fixes work → Move to Milestone 3 (War Map)
- If fixes fail → Debug together based on Console output

Good luck testing! 🎯
