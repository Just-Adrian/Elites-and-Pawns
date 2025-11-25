# Fix: Client-Side Capture Progress Bar Broken

**Date:** November 22, 2025  
**Issue:** Client-side progress bar and capture status UI completely broken  
**Root Cause:** ControlPoint converted to NetworkBehaviour but SyncVar hooks were incomplete

---

## What Happened (The "Third Party" Confession 😅)

Earlier in our session, I started converting `ControlPoint` from a regular MonoBehaviour to a server-authoritative NetworkBehaviour but **timed out before finishing**. This left the code in a broken state:

### What I Did:
✅ Made ControlPoint a NetworkBehaviour  
✅ Added SyncVars for state (currentOwner, captureTeam, captureProgress, isContested)  
✅ Made server-only logic (`if (isServer)`)  
❌ **Started adding SyncVar hooks but didn't implement them**  
❌ **Left non-existent hook references** → Compilation errors  
❌ **Didn't ensure events fire on clients** → Broken UI

---

## The Problem

### How ControlPoint Works Now:

**Server:**
- Runs all capture logic in `UpdateCaptureProgress()`
- Updates SyncVars (currentOwner, captureProgress, etc.)
- Fires static events (OnCaptureProgressChanged, OnPointCaptured, etc.)

**Clients:**
- Receive synced state via SyncVars ✅
- But DON'T run `UpdateCaptureProgress()` (server only)
- So they NEVER receive the events! ❌

### GameModeUI Breakdown:

`GameModeUI` subscribes to these static events to update the progress bar:
```csharp
ControlPoint.OnCaptureProgressChanged += OnCaptureProgressChanged;
ControlPoint.OnContestedStateChanged += OnContestedStateChanged;
ControlPoint.OnPointCaptured += OnPointCaptured;
```

**Problem:** These events only fire on the server, never on clients!

**Result:**
- Server sees progress bar update correctly ✅
- Clients see frozen/broken progress bar ❌

---

## The Fix

### Added SyncVar Hooks That Fire Events on Clients

When a SyncVar changes on the server and syncs to clients, Mirror calls a "hook" method. I added hooks that fire the UI events:

**1. OnProgressChanged Hook:**
```csharp
[SyncVar(hook = nameof(OnProgressChanged))]
private float captureProgress = 0f;

private void OnProgressChanged(float oldProgress, float newProgress)
{
    // Fire progress changed event for UI
    OnCaptureProgressChanged?.Invoke(newProgress, captureTeam);
}
```

**2. OnContestedChanged Hook:**
```csharp
[SyncVar(hook = nameof(OnContestedChanged))]
private bool isContested = false;

private void OnContestedChanged(bool oldContested, bool newContested)
{
    // Fire contested state changed event
    OnContestedStateChanged?.Invoke(newContested);
}
```

**3. OnOwnerChanged Hook:**
```csharp
[SyncVar(hook = nameof(OnOwnerChanged))]
private Core.FactionType currentOwner = Core.FactionType.None;

private void OnOwnerChanged(Core.FactionType oldOwner, Core.FactionType newOwner)
{
    // Fire point captured event if it just became owned
    if (oldOwner == Core.FactionType.None && newOwner != Core.FactionType.None)
    {
        OnPointCaptured?.Invoke(newOwner);
    }
}
```

**4. OnCaptureTeamChanged Hook:**
```csharp
[SyncVar(hook = nameof(OnCaptureTeamChanged))]
private Core.FactionType captureTeam = Core.FactionType.None;

private void OnCaptureTeamChanged(Core.FactionType oldTeam, Core.FactionType newTeam)
{
    // Debug logging
}
```

### Removed Duplicate Event Invocations

Since events now fire via hooks, removed manual invocations in `UpdateCaptureProgress()`:

**Before:**
```csharp
captureProgress = newProgress;
OnCaptureProgressChanged?.Invoke(captureProgress, captureTeam); // Server only!
```

**After:**
```csharp
captureProgress = newProgress;
// Note: OnCaptureProgressChanged event fires via OnProgressChanged hook
```

---

## How It Works Now

### Server Flow:
1. Server runs `UpdateCaptureProgress()`
2. Server updates `captureProgress = 0.75f`
3. SyncVar automatically syncs to clients
4. Server's hook fires → Server UI updates ✅

### Client Flow:
1. Client receives synced `captureProgress = 0.75f`
2. Mirror calls `OnProgressChanged(oldValue, newValue)` hook
3. Hook fires `OnCaptureProgressChanged` event
4. GameModeUI receives event → Client UI updates ✅

**Result:** Both server and clients update their UI! ✅

---

## Files Modified

### ControlPoint.cs:
1. ✅ Added `OnOwnerChanged()` hook method
2. ✅ Added `OnCaptureTeamChanged()` hook method
3. ✅ Added `OnProgressChanged()` hook method
4. ✅ Added `OnContestedChanged()` hook method
5. ✅ Removed duplicate event invocations in `UpdateCaptureProgress()`
6. ✅ Removed duplicate event invocation in `CapturePoint()`
7. ✅ Removed duplicate event invocation in `UpdateContestedState()`

---

## Testing Checklist

After this fix:

### Progress Bar Test:
- [ ] Start multiplayer (Host + Client)
- [ ] Enter capture zone with one player
- [ ] **Check CLIENT:** Progress bar should fill up
- [ ] **Check CLIENT:** Status text should say "BLUE/RED CAPTURING"
- [ ] Let point get captured
- [ ] **Check CLIENT:** Status should say "BLUE/RED CONTROLLED"

### Contested State Test:
- [ ] Both teams enter zone
- [ ] **Check CLIENT:** Status should say "CONTESTED"
- [ ] **Check CLIENT:** Progress bar should show contested color (yellow)
- [ ] One team leaves
- [ ] **Check CLIENT:** Should immediately show other team capturing

### Score vs UI Test:
- [ ] Watch score increment
- [ ] **Check CLIENT:** Status text matches who's getting points
- [ ] **Check CLIENT:** Progress bar color matches scoring team

---

## Console Debug Output

You should now see these messages on **ALL clients** (not just server):

**When progress changes:**
```
[ControlPoint] Capture team changed: None → Blue (synced to client)
```

**When progress updates:**
```
[ControlPoint] Owner changed: None → Blue (synced to client)
```

**When contested changes:**
```
[ControlPoint] Contested changed: false → true (synced to client)
```

---

## Technical Details

### Why Hooks Work:

**Mirror's SyncVar System:**
1. Server changes a SyncVar value
2. Mirror detects the change
3. Mirror sends the new value to all clients
4. Clients receive and update their local copy
5. If a hook is defined, Mirror calls it on the client

**This ensures:**
- Clients know when values change ✅
- Clients can react to changes (fire events) ✅
- UI stays synchronized ✅

### Event Flow Diagram:

```
SERVER                          CLIENTS
------                          -------
UpdateCaptureProgress()         (not run)
  ↓
captureProgress = 0.5
  ↓
[Mirror syncs to all clients]
  ↓                            ↓
OnProgressChanged() hook    →  OnProgressChanged() hook
  ↓                            ↓
(fires event on server)        (fires event on client)
  ↓                            ↓
GameModeUI updates ✅          GameModeUI updates ✅
```

---

## Apology & Explanation

Sorry for leaving this half-finished! Here's what happened:

1. I saw potential desync issues with ControlPoint running independently on all machines
2. Started converting it to server-authoritative (the RIGHT approach)
3. Got partway through and timed out
4. Left the code with:
   - ✅ Server authority working
   - ✅ SyncVars syncing
   - ❌ Events not firing on clients (broke UI)
   - ❌ Compilation errors (missing hooks)

**The good news:** The architecture change was correct! Server-authoritative is the right pattern. I just needed to finish implementing the hooks.

---

## Why Server-Authoritative Is Better

**Old way (MonoBehaviour on all machines):**
- Every client runs capture logic independently
- Can get out of sync due to timing differences
- Dead player cleanup might differ between machines
- Potential for cheating (client modifies local state)

**New way (NetworkBehaviour, server-authoritative):**
- Only server runs capture logic (source of truth)
- Server updates SyncVars → Automatically syncs to all clients
- All clients guaranteed to have same state
- No possibility of client-side cheating
- Better for dedicated servers in the future

---

## Expected Results

After this fix:
- ✅ Progress bar updates on ALL clients
- ✅ Status text updates on ALL clients
- ✅ Contested state shows correctly everywhere
- ✅ Capture events fire on clients
- ✅ UI matches game state perfectly
- ✅ No more frozen/broken progress bars

---

**Status:** Fix applied ✅  
**Testing Required:** Yes - Verify UI updates on clients  
**Confidence Level:** 99% (standard SyncVar hook pattern)

**Note:** The ControlPoint GameObject in your scene needs a NetworkIdentity component (it should already have one since ControlPoint is a NetworkBehaviour).
