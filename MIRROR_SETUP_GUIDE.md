# Mirror Setup Guide - Complete in Unity

## ✅ Scripts Created (Just Now!)

All networking scripts are ready in your project:
- `ElitesNetworkManager.cs` - Network manager
- `NetworkPlayer.cs` - Player network identity
- `PlayerController.cs` - Movement and camera
- `PlayerHealth.cs` - Health and damage

---

## 🎯 Step-by-Step Unity Setup

### Step 1: Create Network Manager Object

1. Open **NetworkTest** scene (`Assets/_Project/Scenes/NetworkTest.unity`)
2. In Hierarchy, **Right-click → Create Empty**
3. Name it: **NetworkManager**
4. In Inspector, click **Add Component**
5. Search for and add: **ElitesNetworkManager** (our custom script)

**Configure ElitesNetworkManager:**
- Transport: Click **Add Transport** → Select **KCP Transport** (comes with Mirror)
- Network Address: `localhost`
- Max Players Per Team: `8`
- Auto Assign Faction: ✅ Checked
- Debug Mode: ✅ Checked

---

### Step 2: Create Player Prefab

#### 2A: Create the Player GameObject

1. In Hierarchy, **Right-click → 3D Object → Capsule**
2. Name it: **Player**
3. Set Position: `(0, 1, 0)` (spawns above ground)
4. Set Scale: `(1, 1, 1)`

#### 2B: Add Components to Player

**Add these components (click Add Component for each):**

1. **Network Identity** (Mirror component)
   - Local Player Authority: ✅ Checked

2. **Network Transform** (Mirror component)
   - Sync Position: ✅ Checked
   - Sync Rotation: ✅ Checked
   - Sync Scale: ❌ Unchecked

3. **NetworkPlayer** (our script)
   - Debug Mode: ✅ Checked

4. **Character Controller** (Unity built-in)
   - Radius: `0.5`
   - Height: `2`
   - Center: `(0, 0, 0)`

5. **PlayerController** (our script)
   - Move Speed: `5`
   - Sprint Multiplier: `1.5`
   - Jump Force: `5`
   - Gravity: `-9.81`
   - Mouse Sensitivity: `2`
   - Max Look Angle: `80`
   - Ground Check Distance: `0.3`
   - Ground Mask: Select **Default** layer
   - Debug Mode: ✅ Checked

6. **PlayerHealth** (our script)
   - Max Health: `100`
   - Respawn Delay: `3`
   - Debug Mode: ✅ Checked

#### 2C: Add Camera to Player

1. Select the **Player** in Hierarchy
2. **Right-click Player → Camera**
3. Name it: **PlayerCamera**
4. Set Position: `(0, 0.6, 0)` (eye level, adjust as needed)
5. **IMPORTANT**: Disable the camera (uncheck at top of Inspector)
   - It will be enabled automatically for local player only

#### 2D: Create Prefab

1. Create folder: `Assets/_Project/Prefabs/Player`
2. Drag **Player** from Hierarchy → into `Prefabs/Player` folder
3. You now have a **Player.prefab**
4. **Delete Player from Hierarchy** (we only need the prefab)

---

### Step 3: Assign Player Prefab to Network Manager

1. Select **NetworkManager** in Hierarchy
2. In **ElitesNetworkManager** component:
   - Find **Player Prefab** field
   - Drag **Player.prefab** from Project window into this field
3. Find **Spawn Info** section:
   - Player Spawn Method: `Round Robin`

---

### Step 4: Add Spawn Points

1. In Hierarchy, **Right-click → Create Empty**
2. Name it: **SpawnPoints**
3. Create child objects (Right-click SpawnPoints → Create Empty):
   - **Spawn1**: Position `(-5, 0, 0)`
   - **Spawn2**: Position `(5, 0, 0)`
   - **Spawn3**: Position `(0, 0, -5)`
   - **Spawn4**: Position `(0, 0, 5)`

4. Select **NetworkManager**
5. In **ElitesNetworkManager** component:
   - **Start Positions** section → Click **+** four times
   - Drag each **Spawn1**, **Spawn2**, etc. into the slots

---

### Step 5: Configure Scene Settings

1. **Edit → Project Settings → Player**
2. Under **Other Settings**:
   - **Color Space**: Linear (better lighting)
   - **API Compatibility Level**: .NET Standard 2.1

3. **Window → Rendering → Lighting**
4. Click **Generate Lighting** (if needed)

---

### Step 6: Set Ground Layer

1. Select the **Ground** object in Hierarchy
2. Top of Inspector → **Layer** dropdown → Select **Default**
   - (Or create a new "Ground" layer if you prefer)

---

## 🧪 Testing Your Setup

### Test 1: Single Player (Test in Editor)

1. Press **Play** in Unity Editor
2. In **NetworkManager** Inspector:
   - Click **Start Host** button
3. **Expected Result**:
   - You spawn as a blue capsule
   - WASD to move
   - Mouse to look around
   - Spacebar to jump
   - Console shows debug messages

### Test 2: Multiplayer (Two Instances)

#### Option A: ParrelSync (Recommended)
1. Install ParrelSync from Package Manager (GitHub URL)
2. Create a clone project
3. Open both projects
4. In main project: **Start Host**
5. In clone project: **Start Client**

#### Option B: Build and Test
1. **File → Build Settings**
2. Add **NetworkTest** scene to build
3. Click **Build**
4. Run the build exe
5. In exe: Click **Start Host**
6. In Unity Editor: Click **Start Client**

### What You Should See:
- Two blue capsules (one for each player)
- Both players can move independently
- You see the other player moving in real-time
- Console shows connection messages

---

## 🐛 Troubleshooting

### "Can't find ElitesNetworkManager"
- Close and reopen Unity (refresh scripts)
- Check for compile errors in Console

### Player Falls Through Ground
- Make sure Ground has a **Collider** component
- Make sure Player has **Character Controller**
- Check Ground Layer in PlayerController

### Camera Not Working
- Make sure PlayerCamera is disabled on the prefab
- Check that Camera Transform is assigned in PlayerController

### Players Don't See Each Other
- Make sure Player prefab has **Network Identity**
- Make sure Player prefab has **Network Transform**
- Check console for network errors

### Can't Click Start Host/Client
- Make sure Transport is added (KCP Transport)
- Make sure Player Prefab is assigned

---

## ✅ Success Checklist

- [ ] NetworkManager exists in scene with ElitesNetworkManager component
- [ ] Player prefab created with all components
- [ ] PlayerCamera added to prefab (disabled)
- [ ] Player prefab assigned to Network Manager
- [ ] Spawn points created and assigned
- [ ] Ground layer configured
- [ ] Can click "Start Host" and spawn as player
- [ ] WASD movement works
- [ ] Mouse look works
- [ ] Jump works (spacebar)
- [ ] Console shows debug messages
- [ ] (Optional) Two players can connect and see each other

---

## 📝 Next Steps After Setup

Once multiplayer is working, we'll add:
1. **Shooting mechanics** - Raycasting and damage
2. **Team colors** - Visual distinction
3. **Spawn protection** - 3-second invulnerability
4. **UI** - Health bar, ammo counter
5. **War Map** - RTS layer prototype

---

**Take your time with this setup!** Test each step. If you get stuck, let me know exactly where and I'll help troubleshoot.

**Estimated Time**: 15-20 minutes

**Ready to start?** 🚀
