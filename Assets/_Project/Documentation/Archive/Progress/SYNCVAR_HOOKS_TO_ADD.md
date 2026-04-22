# SyncVar Hooks to Add to ControlPoint.cs

**Location:** Add these methods at the END of the ControlPoint class, just before the final closing braces `}` 

**Add after the `ResetPoint()` method:**

```csharp
        #region SyncVar Hooks
        
        /// <summary>
        /// Called on all clients when currentOwner changes
        /// </summary>
        private void OnOwnerChanged(Core.FactionType oldOwner, Core.FactionType newOwner)
        {
            if (debugMode)
            {
                Debug.Log($"[ControlPoint] Owner changed: {oldOwner} → {newOwner}");
            }
            
            // Fire event on clients
            if (newOwner != oldOwner && newOwner != Core.FactionType.None)
            {
                OnPointCaptured?.Invoke(newOwner);
            }
        }
        
        /// <summary>
        /// Called on all clients when captureTeam changes
        /// </summary>
        private void OnCaptureTeamChanged(Core.FactionType oldTeam, Core.FactionType newTeam)
        {
            if (debugMode)
            {
                Debug.Log($"[ControlPoint] Capture team changed: {oldTeam} → {newTeam}");
            }
        }
        
        /// <summary>
        /// Called on all clients when captureProgress changes
        /// </summary>
        private void OnProgressChanged(float oldProgress, float newProgress)
        {
            // Fire progress event on clients
            OnCaptureProgressChanged?.Invoke(newProgress, captureTeam);
        }
        
        /// <summary>
        /// Called on all clients when isContested changes
        /// </summary>
        private void OnContestedChanged(bool oldContested, bool newContested)
        {
            if (debugMode)
            {
                Debug.Log($"[ControlPoint] Contested state changed: {oldContested} → {newContested}");
            }
            
            // Fire contested event on clients
            OnContestedStateChanged?.Invoke(newContested);
        }
        
        #endregion
```

**Where to add:**
1. Open `ControlPoint.cs`
2. Scroll to the very bottom
3. Find the `ResetPoint()` method
4. After its closing brace `}`, add the code above
5. Make sure it's BEFORE the final `}` that closes the ControlPoint class
6. Make sure it's BEFORE the final `}` that closes the namespace

**The structure should look like:**
```csharp
namespace ElitesAndPawns.GameModes
{
    public class ControlPoint : NetworkBehaviour
    {
        // ... existing code ...
        
        public void ResetPoint()
        {
            // ... existing code ...
        }
        
        // ADD THE SYNCVAR HOOKS HERE
        #region SyncVar Hooks
        // ... the 4 hook methods ...
        #endregion
        
    }  // <-- Class closing brace
}  // <-- Namespace closing brace
```
