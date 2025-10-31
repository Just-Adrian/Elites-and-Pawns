# TODO - Next Steps

**Priority: Fix HUD → Test Multiplayer → Add Features**

---

## 🔴 IMMEDIATE - FIX HUD

**Problem:** UI not showing despite scripts working

**Try these in order:**

1. **Simple Test** (5 min)
   - Add UI → Image to PlayerHUD_Canvas
   - Make it HUGE (400x400) and bright red
   - Position: Center (0, 0)
   - Can you see it? YES = setup issue, NO = canvas issue

2. **Change to Overlay** (2 min)
   - Canvas → Render Mode → Screen Space - Overlay
   - This always works, simplest mode

3. **Make Text Obvious** (3 min)
   - HealthText: Font size 72, Color RED, Position (0, 0)
   - If still can't see, add diagnostic logging

4. **Add Diagnostics** (5 min)
   ```csharp
   // Add to PlayerHUD.Start():
   Debug.Log($"Canvas enabled: {GetComponent<Canvas>().enabled}");
   Debug.Log($"HealthText exists: {healthText != null}");
   Debug.Log($"HealthText.text: {healthText?.text}");
   ```

5. **Check Frame Debugger** (5 min)
   - Window → Analysis → Frame Debugger
   - Enable during play
   - Look for Canvas in draw calls

---

## 🟡 HIGH PRIORITY

### **Test Second Client**
- Build game (File → Build Settings)
- Run executable as Client
- Verify weapon spawning works

### **Add "Head" Tag**
- Edit → Project Settings → Tags & Layers
- Add tag: "Head"
- Test headshots

### **Simple Crosshair**
- UI Image in center (32x32)
- White cross sprite

---

## 🟢 MEDIUM PRIORITY

### **Hit Markers**
- Show X when hitting enemy
- Flash for 0.2 seconds

### **Kill Feed**
- Top-right corner
- Show last 5 kills

---

## 📝 NOTES FOR NEXT SESSION

**Start by:**
1. Reading PROGRESS.md
2. Trying HUD fixes above
3. Testing with second client

**Key Info:**
- All scripts compile ✅
- Core gameplay works ✅
- Only UI visibility is the issue ❌
