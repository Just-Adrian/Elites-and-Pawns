# UI Troubleshooting Guide

**Problem:** Health and Ammo UI not showing

---

## 🔍 QUICK DIAGNOSIS

### **Check 1: Can you see a test Image?**

1. Select PlayerHUD_Canvas
2. Add Component → UI → Image
3. Name it "TestImage"
4. Set:
   - Color: Bright Red (255, 0, 0, 255)
   - Width: 400, Height: 400
   - Position: (0, 0) - Center
5. Press Play

**Result:**
- ✅ Can see red square = Canvas works, UI setup issue
- ❌ Can't see anything = Canvas config issue

---

### **Check 2: Is Canvas using Overlay mode?**

1. Select PlayerHUD_Canvas
2. Canvas component → **Render Mode**
3. Should be: **Screen Space - Overlay**

**If NOT Overlay:**
- Change to Screen Space - Overlay
- Save prefab
- Test again

---

### **Check 3: Are references assigned?**

1. Stop Play mode
2. Open Player prefab
3. Select PlayerHUD_Canvas
4. Look at PlayerHUD component

**Should see:**
```
Health Display:
  Health Text: HealthText (not "None")
  Health Bar: HealthBar_Fill (not "None")
  Health Panel: HealthPanel (not "None")

Ammo Display:
  Ammo Text: AmmoText (not "None")
  Weapon Name Text: WeaponNameText (not "None")
  Ammo Panel: AmmoPanel (not "None")
```

**If any are "None":**
- Drag the objects from Hierarchy
- Save prefab (Ctrl+S)

---

### **Check 4: Is Canvas enabled at runtime?**

1. Press Play
2. In Hierarchy, expand spawned Player
3. Select PlayerHUD_Canvas
4. Look at checkbox next to name

**Should be:** ☑ (checked)

**If unchecked:**
- LocalPlayerCanvas not working
- Try removing LocalPlayerCanvas component
- Let Canvas stay enabled always

---

## 🔧 SOLUTIONS

### **Solution 1: Force Overlay Mode**

```
1. Select PlayerHUD_Canvas
2. Canvas → Render Mode → Screen Space - Overlay
3. Remove LocalPlayerCanvas component
4. Save prefab
5. Test
```

This is the simplest, most reliable mode.

---

### **Solution 2: Make Text Super Obvious**

```
1. Select HealthText
2. Set:
   - Font Size: 72 (HUGE)
   - Color: RED (255, 0, 0, 255)
   - Position: (0, 0) CENTER of screen
   - Text: "TEST"
3. Test
```

If you STILL can't see it, something is very wrong.

---

### **Solution 3: Add Debug Logging**

Add to PlayerHUD.cs Start() method:

```csharp
private void Start()
{
    Debug.Log("=== PLAYERHUD DIAGNOSTICS ===");
    Canvas canvas = GetComponent<Canvas>();
    Debug.Log($"Canvas enabled: {canvas.enabled}");
    Debug.Log($"Canvas renderMode: {canvas.renderMode}");
    
    Debug.Log($"HealthPanel active: {healthPanel?.activeSelf}");
    Debug.Log($"HealthText exists: {healthText != null}");
    
    if (healthText != null)
    {
        Debug.Log($"HealthText.text: '{healthText.text}'");
        Debug.Log($"HealthText.fontSize: {healthText.fontSize}");
        Debug.Log($"HealthText.color: {healthText.color}");
    }
    
    // ... rest of Start() code
}
```

Check console for clues.

---

### **Solution 4: Check Frame Debugger**

```
1. Window → Analysis → Frame Debugger
2. Press Play
3. Click "Enable" in Frame Debugger
4. Look through draw calls
5. Search for "Canvas" or "UI"
```

**If Canvas is NOT in the list:**
- Canvas not rendering at all
- Check Canvas layer is "UI"
- Check Camera culling mask includes "UI"

**If Canvas IS in the list:**
- Rendering but maybe off-screen
- Check RectTransform positions

---

## 💡 COMMON ISSUES

### **Issue: Text is invisible**
**Check:**
- Font size > 20
- Color alpha = 255 (not 0)
- Text field not empty

### **Issue: Canvas not rendering**
**Check:**
- Canvas layer = "UI"
- Camera Culling Mask includes "UI"
- Canvas enabled = true

### **Issue: UI off-screen**
**Check:**
- Position values reasonable (-1000 to 1000)
- Anchors set correctly
- Width/Height not zero

---

## 🚀 FASTEST FIX

**If you just want SOMETHING working:**

1. Delete PlayerHUD_Canvas
2. Create: UI → Canvas
3. Render Mode: **Screen Space - Overlay**
4. Add: UI → Text
5. Set:
   - Text: "HEALTH: 100"
   - Font Size: 36
   - Color: White
   - Position: Top-Left (100, -50)
6. Add PlayerHUD script to Canvas
7. Drag Text into healthText slot
8. Test

This gives you a basic health display to build from.

---

## 📞 IF STILL NOT WORKING

Provide these for help:
1. Screenshot of Canvas Inspector
2. Screenshot of PlayerHUD component
3. Console logs with diagnostics
4. Can you see test red Image? (yes/no)
5. What render mode is Canvas set to?
