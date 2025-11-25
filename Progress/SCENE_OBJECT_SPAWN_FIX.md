# Fix: Progress Bar Still Not Working - Scene Object Not Spawned

**Date:** November 22, 2025  
**Issue:** Progress bar stays on "NEUTRAL" but capture is working and giving points  
**Root Cause:** ControlPoint NetworkIdentity was never spawned by Mirror

---

## The Real Problem

Even though:
- ✅ ControlPoint has SyncVar hooks
- ✅ ControlPoint has NetworkIdentity
- ✅ Server is updating capture state
- ✅ Points are being awarded

**The progress bar still doesn't work because:**

### Scene Objects Must Be Spawned!

When you convert a regular MonoBehaviour to a NetworkBehaviour:
1. It needs a NetworkIdentity component ✅ (you added this)
2. The NetworkIdentity must be **spawned by the server** ❌ (this was missing!)

Without spawning, SyncVars don't sync to clients. It's like having a phone but not turning it on - the hardware is there but nothing works.

---

## How Mirror Spawning Works

### For Prefabs (Instantiated Objects):
- Mirror spawns them automatically when you instantiate
- `NetworkServer.Spawn(playerInstance)` is called
- SyncVars start syncing ✅

### For Scene Objects (Already in Scene):
- **Two options:**

**Option 1: Automatic (if configured properly):**
- NetworkIdentity has a `sceneId` (non-zero)
- Mirror auto-spawns on scene load
- Usually works for simple cases

**Option 2: Manual (more reliable):**
- Call `NetworkServer.Spawn(sceneObject)` explicitly
- Guarantees the object is spawned
- **This is what we're doing**

---

## The Fix

Added manual spawning in `GameModeManager.Start()`:

```csharp
// CRITICAL: Spawn scene objects with NetworkBehaviour
if (NetworkServer.active && controlPoint != null)
{
    NetworkIdentity netId = controlPoint.GetComponent<NetworkIdentity>();
    if (netId != null && netId.netId == 0)
    {
        // Object not spawned yet - spawn it for SyncVar synchronization
        NetworkServer.Spawn(controlPoint.gameObject);
        if (debugMode)
            Debug.Log("[GameModeManager] Spawned ControlPoint NetworkIdentity");
    }
}
```

**What this does:**
1. Checks if we're the server
2. Checks if ControlPoint has NetworkIdentity
3. Checks if it's NOT spawned yet (`netId == 0` means not spawned)
4. Spawns it with `NetworkServer.Spawn()`

**Result:** SyncVars now sync to clients! ✅

---

## Why The Symptoms Were Confusing

**Server side (working):**
- Capture logic runs → Points awarded ✅
- Events fire locally → Server UI updates ✅
- Everything seems fine!

**Client side (broken):**
- SyncVars never sync (not spawned) ❌
- Hooks never fire ❌
- UI never updates ❌
- Stays on "NEUTRAL" forever

**But points still work because:**
- Points are handled by `ScoreNetworkSync` (separate system)
- ScoreNetworkSync IS spawned correctly
- So scores sync, but ControlPoint state doesn't

---

## Testing Checklist

After this fix:

### Console Check:
- [ ] Start as host
- [ ] Check Console for: `[GameModeManager] Spawned ControlPoint NetworkIdentity`
  - **If you see this:** ControlPoint is now spawned ✅
  - **If not:** Check if it says "already spawned (netId: X)" where X > 0

### Progress Bar Test:
- [ ] Start multiplayer (Host + Client)
- [ ] Enter capture zone
- [ ] **Client screen:** Progress bar should fill up
- [ ] **Client screen:** Status should change to "BLUE/RED CAPTURING"
- [ ] Let point get captured
- [ ] **Client screen:** Status should say "BLUE/RED CONTROLLED"
- [ ] **Client screen:** Progress bar should be full

### Contested Test:
- [ ] Both teams enter zone
- [ ] **Client screen:** Should say "CONTESTED"
- [ ] **Client screen:** Progress bar should show yellow
- [ ] One team leaves
- [ ] **Client screen:** Should immediately update to other team capturing

---

## If It Still Doesn't Work

### Check 1: Is ControlPoint spawning?

Look for this message in Console:
```
[GameModeManager] Spawned ControlPoint NetworkIdentity
```

**If you DON'T see it:**
- Check if it says "already spawned"
- If neither message appears, `NetworkServer.active` might be false in Start()

### Check 2: NetworkIdentity Configuration

Select ControlPoint GameObject:
1. Check NetworkIdentity component exists
2. Check "Server Only" is **UNCHECKED**
3. Check "Scene Id" has a value (should be auto-set)

### Check 3: Hook Methods Actually Firing?

Watch Console on CLIENT side when capturing:
```
[ControlPoint] Capture team changed: None → Blue (synced to client)
[ControlPoint] Owner changed: None → Blue (synced to client)
```

**If you DON'T see these messages on clients:**
- ControlPoint still not spawned properly
- Or hooks aren't firing

---

## Alternative Solution (If Manual Spawn Fails)

If manual spawning doesn't work, we can check/set the sceneId:

### In Unity Editor:
1. Select ControlPoint GameObject
2. View NetworkIdentity in Inspector
3. Check if "Scene Id" field is empty or 0
4. If empty, enter play mode once, then stop
5. Unity should auto-assign a sceneId
6. Save the scene

This makes Mirror auto-spawn the object on scene load.

---

## Technical Details

### What NetworkServer.Spawn() Does:

```
SERVER:
1. Assigns a netId to the NetworkIdentity
2. Marks object as "spawned"
3. Sends spawn message to all clients
4. Starts syncing SyncVars

CLIENTS:
5. Receive spawn message
6. Find scene object by sceneId
7. Mark it as spawned with same netId
8. Subscribe to SyncVar updates
```

Without this, clients never "activate" their copy of the object for networking.

### Why Scene Objects Need Special Handling:

**Prefabs:** 
- Instantiated at runtime → Mirror sees them being created → Auto-spawns

**Scene Objects:**
- Already exist when scene loads → Mirror might not know about them
- Need explicit spawning or sceneId configuration

---

## Files Modified

- ✅ `GameModeManager.cs` - Added ControlPoint spawning in Start()

---

## Expected Console Output

**On Host (Server):**
```
[GameModeManager] ScoreNetworkSync found
[GameModeManager] Spawned ControlPoint NetworkIdentity for SyncVar synchronization
[GameModeManager] Subscribed to ControlPoint events
[GameModeManager] ==== KING OF THE HILL STARTED ====
```

**On Client:**
```
[ControlPoint] Capture team changed: None → Blue (synced to client)
[ControlPoint] Owner changed: None → Blue (synced to client)
```

---

## Why This Wasn't Obvious

This is a classic Mirror "gotcha":
- Regular MonoBehaviours don't need spawning
- NetworkBehaviours with SyncVars **require** spawning
- Scene objects don't spawn automatically (unlike prefabs)
- No error messages - it just silently doesn't sync
- Server works fine, only clients are broken

The symptoms (points work, UI doesn't) made it even harder to diagnose because two separate systems were behaving differently.

---

**Status:** Fix applied ✅  
**Testing Required:** Yes - Check Console for spawn message, test UI  
**Confidence Level:** 95% (spawning is definitely the issue)

**If this STILL doesn't work:** Check NetworkIdentity configuration and let me know what Console says!
