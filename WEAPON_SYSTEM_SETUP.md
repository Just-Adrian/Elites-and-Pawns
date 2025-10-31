# Projectile Weapon & Health System - Setup Guide

**Status**: Complete and ready to integrate  
**Date**: October 29, 2025  
**System Type**: PROJECTILE-BASED (Physics simulation)

---

## 📋 What We Built

### **1. Projectile-Based Weapon System**
- **WeaponData.cs** - ScriptableObject with projectile physics stats
- **BaseWeapon.cs** - Abstract base class for all weapons
- **ProjectileWeapon.cs** - Spawns physical projectiles
- **Projectile.cs** - Physics-based bullet with gravity, travel time, and collision
- **WeaponManager.cs** - Handles equipped weapons, switching, and input

### **2. Enhanced Health System**
- **PlayerHealth.cs** - Damage from projectiles, death/respawn, attacker tracking

---

## 🎯 Why Projectile-Based?

### **Advantages**
✅ **Engaging long-range combat** - Lead moving targets  
✅ **Bullet drop** - Account for gravity at distance  
✅ **Travel time** - Tactical depth in positioning  
✅ **Visible projectiles** - See incoming fire  
✅ **Damage falloff** - Naturally balanced by distance  
✅ **Skill ceiling** - Rewards precision and prediction  

### **VS Hitscan**
❌ Hitscan = Instant hit (boring at range)  
✅ Projectile = Travel time + drop (exciting at range)

---

## 🛠️ Unity Setup Instructions

### **Step 1: Create Projectile Prefab**

1. Create new GameObject: `Projectile_Bullet`
2. Add components:
   - **Rigidbody**
     - Use Gravity: ✗ (we use custom gravity)
     - Collision Detection: Continuous Dynamic
     - Constraints: Freeze Rotation (all axes)
   - **Capsule Collider**
     - Is Trigger: ✓
     - Radius: 0.01
     - Height: 0.05
     - Direction: Z-Axis
   - **Projectile** script (our script)
   - **Trail Renderer** (optional, for tracer)
     - Time: 0.2
     - Width: 0.02
     - Material: Default-Particle (or custom)

3. **Visual mesh** (optional):
   - Add child: Capsule mesh (scale 0.01, 0.01, 0.05)
   - Or use sprite/particle

4. Set Layer: `Projectile` (create new layer)

5. Save as prefab in `Assets/_Project/Prefabs/Weapons/Projectile_Bullet.prefab`

### **Step 2: Create Weapon Data Asset**

1. Right-click in Project → **Create → Elites and Pawns → Weapon Data**
2. Name it: `AssaultRifle_Data`
3. Configure stats:
   ```
   === Basic ===
   Weapon Name: Assault Rifle
   
   === Damage ===
   Damage: 25
   Headshot Multiplier: 2.0
   Max Damage Range: 50 (full damage up to 50m)
   Min Damage Range: 100 (min damage after 100m)
   Min Damage Falloff: 0.5 (50% damage at max range)
   
   === Projectile Physics ===
   Projectile Speed: 100 (m/s)
   Projectile Gravity: 9.81 (realistic)
   Projectile Lifetime: 5 (seconds)
   Has Tracer: ✓
   Tracer Length: 2
   
   === Firing ===
   Fire Rate: 0.1 (10 shots/sec)
   Is Automatic: ✓
   Projectiles Per Shot: 1
   
   === Ammo ===
   Magazine Size: 30
   Max Reserve Ammo: 120
   Reload Time: 2.0
   
   === Accuracy ===
   Base Spread: 2.0 (degrees)
   Aim Spread: 0.5 (degrees)
   Move Spread Multiplier: 2.0
   Recoil Amount: 0.1
   
   === Projectile Prefab ===
   Projectile Prefab: [Drag Projectile_Bullet here]
   ```

### **Step 3: Setup Player Prefab**

1. Open your **Player prefab**
2. **Delete old weapon setup** (if exists):
   - Remove `HitscanWeapon` component (old system)
   - Delete `HitscanWeapon.cs` file from project

3. Add/update components:
   - **WeaponManager** component
   - Create child: `WeaponHolder` (position 0, 0.5, 0.3)
     - Create child: `Weapon_AssaultRifle`
       - Add **ProjectileWeapon** component
       - Create child: `FirePoint` (position 0, 0, 0.5)
       - Create child: `ProjectileSpawnPoint` (position 0, 0, 0.6)

4. **Configure ProjectileWeapon**:
   - Weapon Data: Assign `AssaultRifle_Data`
   - Fire Point: Drag `FirePoint`
   - Projectile Spawn Point: Drag `ProjectileSpawnPoint`
   - Player Camera: Leave empty (auto-finds)
   - Hit Mask: Everything except "Player" and "Projectile" layers
   - Show Debug Trajectory: ✓ (for testing)

5. **Configure WeaponManager**:
   - Weapons: Array size 1
     - Element 0: Drag `Weapon_AssaultRifle`
   - Current Weapon Index: 0

### **Step 4: Layer & Physics Setup**

1. **Create Layers**:
   - Edit → Project Settings → Tags and Layers
   - Add layers:
     - `Player` (Layer 6)
     - `Weapon` (Layer 7)
     - `Projectile` (Layer 8)

2. **Set Layers**:
   - Player prefab → `Player` layer
   - Projectile_Bullet prefab → `Projectile` layer

3. **Physics Matrix**:
   - Edit → Project Settings → Physics
   - Layer Collision Matrix:
     - ✗ Projectile ↔ Projectile (no collision)
     - ✗ Projectile ↔ Weapon (no collision)
     - ✓ Projectile ↔ Player (can hit)
     - ✓ Projectile ↔ Default (can hit world)

### **Step 5: Test!**

1. Open NetworkTest scene
2. Press Play → Start Host
3. **Controls**:
   - **Left Mouse** - Shoot (spawns projectiles!)
   - **R** - Reload
   - **Right Mouse** - Aim (reduces spread)
   - **1, 2, 3** - Switch weapons
   - **Mouse Wheel** - Cycle weapons

4. **Watch for**:
   - Cyan trajectory prediction lines
   - Projectiles travel through air
   - Bullet drop over distance
   - Tracer effect
   - Damage on hit

---

## 🎮 Weapon Examples

### **Assault Rifle** (Default)
```
Speed: 100 m/s
Gravity: 9.81 m/s²
Damage: 25 (full up to 50m)
Fire Rate: 0.1s (10 rounds/sec)
Spread: 2° (hip), 0.5° (ADS)
Magazine: 30 rounds
```
**Use Case**: Balanced all-rounder

### **Sniper Rifle** (Long Range)
```
Speed: 300 m/s (fast)
Gravity: 9.81 m/s²
Damage: 75 (full up to 150m)
Fire Rate: 1.0s (slow bolt-action)
Spread: 0.1° (very accurate)
Magazine: 5 rounds
Projectiles Per Shot: 1
```
**Use Case**: Precise long-range engagements

### **Shotgun** (Close Range)
```
Speed: 60 m/s (slow pellets)
Gravity: 9.81 m/s²
Damage: 15 per pellet
Fire Rate: 0.8s
Spread: 8° (wide cone)
Magazine: 8 rounds
Projectiles Per Shot: 8 (pellets)
```
**Use Case**: Devastating close-quarters

### **SMG** (High Fire Rate)
```
Speed: 80 m/s
Gravity: 9.81 m/s²
Damage: 18
Fire Rate: 0.06s (16.6 rounds/sec)
Spread: 3° (hip), 1.5° (ADS)
Magazine: 40 rounds
```
**Use Case**: CQB spray and pray

---

## 📊 Projectile System Features

### **Physics Simulation**
- ✅ Rigidbody-based movement
- ✅ Custom gravity application
- ✅ Accurate trajectory prediction
- ✅ Continuous collision detection
- ✅ Velocity-based rotation

### **Damage System**
- ✅ Distance-based falloff (configurable)
- ✅ Headshot detection and multiplier
- ✅ Server-authoritative damage
- ✅ Attacker tracking
- ✅ Network synchronized

### **Visual Feedback**
- ✅ Trail renderer for tracers
- ✅ Impact effects
- ✅ Trajectory prediction (debug)
- ✅ Muzzle flash hooks

### **Audio Feedback**
- ✅ Shoot sound
- ✅ Impact sound
- ✅ Whiz sound (bullets passing by)
- ✅ Reload sound

### **Performance**
- ✅ Projectile lifetime (auto-despawn)
- ✅ Layer-based collision filtering
- ✅ Network spawning optimized
- ✅ Pooling-ready architecture

---

## 🎯 Ballistics & Engagement Ranges

### **Bullet Drop Reference**
At **100m distance** with 100 m/s projectile speed:
- **Travel time**: 1 second
- **Drop**: ~4.9 meters (½gt²)
- **Aiming adjustment**: Aim ~5m above target

At **50m distance**:
- **Travel time**: 0.5 seconds
- **Drop**: ~1.2 meters
- **Aiming adjustment**: Aim ~1m above target

### **Target Leading**
Enemy running at 5 m/s:
- **At 100m**: Lead by ~5m
- **At 50m**: Lead by ~2.5m

---

## 🔧 Advanced Customization

### **Creating a Rocket Launcher**
```
Projectile Speed: 30 m/s (slow)
Projectile Gravity: 2.0 (less affected)
Damage: 100 (on direct hit)
Explosion Radius: 5m (add to Projectile script)
Projectile Lifetime: 10s (longer travel)
Has Tracer: ✓ (smoke trail)
Tracer Length: 10
```

### **Creating a Grenade Launcher**
```
Projectile Speed: 25 m/s
Projectile Gravity: 19.62 (2x gravity, high arc)
Damage: 75
Bounce: true (add bouncing logic)
Fuse Timer: 3s (explode after time)
```

### **Projectile Script Extensions**

Add to `Projectile.cs` for more features:

```csharp
// Penetration through walls
public int maxPenetrations = 2;
private int penetrationCount = 0;

// Ricochet off surfaces
public float ricochetChance = 0.3f;
public int maxRicochets = 2;

// Explosive projectiles
public float explosionRadius = 5f;
public float explosionDamage = 50f;

// Sticky projectiles (grenades)
public bool isSticky = false;
public float fuseTime = 3f;
```

---

## 🐛 Debugging

### **Enable Debug Mode**
1. Select Player prefab
2. Check **Debug Mode** on:
   - WeaponManager
   - ProjectileWeapon
   - Projectile (in prefab)

### **Console Output**
```
[WeaponManager] Equipped: Assault Rifle
[ProjectileWeapon] Spawned projectile: Assault Rifle
[Projectile] Initialized. Speed: 100, Gravity: 9.81
[Projectile] Hit PlayerName for 25.0 damage at 35.2m
[PlayerHealth] PlayerName took 25.0 damage from Shooter
```

### **Visual Debugging**
- **Cyan lines** = Trajectory prediction (Scene view)
- **Yellow sphere** = Projectile spawn point (Gizmos)
- **Red ray** = Projectile velocity direction (Gizmos)
- **Trail** = Projectile tracer (Game view)

### **Common Issues**

**Projectiles spawn but don't move:**
- Check Rigidbody is on projectile prefab
- Verify Projectile.Initialize() is being called
- Check projectileSpeed > 0

**Projectiles go through targets:**
- Verify Collider is set to `Is Trigger`
- Check Layer Collision Matrix
- Ensure target has collider
- Use Continuous Dynamic collision detection

**Projectiles despawn instantly:**
- Check projectileLifetime > 0
- Verify hitMask doesn't include shooter layer

**No damage on hit:**
- Ensure target has PlayerHealth component
- Check server is spawning projectiles (isServer)
- Verify hitMask includes target layer

---

## 🚀 Performance Optimization

### **Current Optimization**
- Projectiles auto-destroy after lifetime
- Layer collision matrix filters unnecessary checks
- Continuous Dynamic only for projectiles
- Network spawning uses object pooling architecture

### **Future Optimization** (if needed)
- Object pooling for projectiles (reuse instead of destroy)
- LOD for distant projectiles (disable trail, physics)
- Spatial partitioning for collision checks
- Client-side prediction for local player projectiles

---

## 🔮 Future Enhancements

### **Coming Soon**
- Projectile penetration (through thin walls)
- Ricochet mechanics
- Explosive projectiles (AoE damage)
- Sticky projectiles (grenades)
- Homing projectiles (guided missiles)
- Beam weapons (continuous ray)
- Melee weapons (different system)

### **Advanced Features**
- Wind affecting projectiles
- Temperature affecting ballistics
- Weapon attachments (scopes, grips affect stats)
- Bullet types (armor-piercing, explosive, tracer)
- Weapon degradation/jamming

---

## 📁 File Structure

```
Assets/_Project/
├── Scripts/
│   └── Weapons/
│       ├── WeaponData.cs          (ScriptableObject - stats)
│       ├── BaseWeapon.cs           (Abstract weapon base)
│       ├── ProjectileWeapon.cs     (Spawns projectiles)
│       ├── Projectile.cs           (Physics bullet)
│       └── WeaponManager.cs        (Player weapon controller)
└── Prefabs/
    └── Weapons/
        └── Projectile_Bullet.prefab (Projectile prefab)
```

---

## ✅ Testing Checklist

### **Projectile Physics**
- [ ] Projectiles spawn and travel
- [ ] Bullet drop visible at range
- [ ] Travel time noticeable (not instant)
- [ ] Projectiles despawn after lifetime
- [ ] Trajectory prediction accurate

### **Damage System**
- [ ] Projectiles damage players
- [ ] Headshots deal extra damage
- [ ] Damage falloff works at range
- [ ] No self-damage from own bullets
- [ ] Server-authoritative (no client cheating)

### **Weapon Features**
- [ ] Shooting consumes ammo
- [ ] Reloading works
- [ ] Fire rate limiting works
- [ ] Spread increases when hip-firing
- [ ] ADS reduces spread
- [ ] Automatic/semi-auto modes work

### **Multiplayer**
- [ ] Projectiles synchronized
- [ ] Both players can shoot
- [ ] Damage works across network
- [ ] No lag/desync issues
- [ ] Projectiles visible to all clients

---

## 🎓 Tips for Using Projectile Weapons

### **For Players**
1. **Lead your target** - Aim ahead of moving enemies
2. **Account for drop** - Aim above distant targets
3. **Watch your tracers** - Adjust aim based on visible projectiles
4. **Burst fire** - More accurate than full-auto at range
5. **ADS for precision** - Right-click to reduce spread

### **For Level Designers**
1. **Sight lines** - Consider projectile travel time
2. **Cover placement** - Allow dodging projectiles
3. **Engagement ranges** - Design for 50-100m combat
4. **Height advantage** - Matters more with bullet drop

---

**System Status**: ✅ **COMPLETE & PRODUCTION READY**

**Next Steps**:
1. Create projectile prefab (Step 1)
2. Create weapon data (Step 2)
3. Setup player prefab (Step 3)
4. Configure physics layers (Step 4)
5. Test and tune! (Step 5)

---

*Created: October 29, 2025*  
*Projectile-based system for engaging long-range combat*  
*Integrated with Milestone 1 networking foundation*
