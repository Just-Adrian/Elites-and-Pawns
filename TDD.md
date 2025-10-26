# Elites and Pawns True - Technical Design Document (TDD)

**Version**: 1.0  
**Date**: October 26, 2025  
**Status**: Initial Technical Design  
**Lead Designer**: Adrian (Just-Adrian)  
**Technical Lead**: Claude  
**Based on**: GDD v1.0

---

## Executive Summary

This Technical Design Document outlines the technical architecture, systems, and implementation strategy for **Elites and Pawns True**. It serves as the blueprint for development, detailing how the RTS and FPS layers integrate, network architecture, data structures, and code organization.

**Key Technical Decisions**:
- **Engine**: Unity 6000.2.8f1 with URP
- **Networking**: Mirror Networking
- **Architecture**: Server-authoritative multiplayer
- **Target**: 8v8 battles, 60 FPS, <100ms latency

---

## 1. System Architecture Overview

### 1.1 High-Level Architecture

```
┌─────────────────────────────────────────────────────┐
│                  GAME CLIENT                        │
│  ┌──────────────┐           ┌──────────────┐      │
│  │  RTS Layer   │◄─────────►│  FPS Layer   │      │
│  │  (War Map)   │  Seamless │  (Combat)    │      │
│  │              │  Transition│              │      │
│  └──────┬───────┘           └──────┬───────┘      │
│         │                           │               │
│         └───────────┬───────────────┘               │
│                     │                               │
│              ┌──────▼──────┐                       │
│              │   Network   │                       │
│              │   Manager   │                       │
│              └──────┬──────┘                       │
└─────────────────────┼───────────────────────────────┘
                      │
                 ┌────▼─────┐
                 │ INTERNET │
                 └────┬─────┘
                      │
┌─────────────────────▼───────────────────────────────┐
│                  GAME SERVER                        │
│  ┌──────────────────────────────────────────────┐  │
│  │           Server Authority                    │  │
│  │  • Token Management                          │  │
│  │  • War Map State                             │  │
│  │  • Battle Orchestration                      │  │
│  │  • Player Validation                         │  │
│  └──────────────────────────────────────────────┘  │
│                                                     │
│  ┌──────────────┐  ┌──────────────┐              │
│  │  War Map     │  │  Battle      │              │
│  │  Controller  │  │  Controller  │              │
│  └──────────────┘  └──────────────┘              │
└─────────────────────────────────────────────────────┘
```

### 1.2 Core Systems

1. **War Map System** - RTS strategic layer
2. **Battle System** - FPS tactical layer  
3. **Token Management** - Resource economy
4. **Network System** - Multiplayer synchronization
5. **Player System** - Authentication, progression
6. **UI System** - HUD, menus, war map interface

---

## 2. Project Structure

### 2.1 Directory Organization

```
Assets/
├── _Project/                          # Main project folder
│   ├── Scripts/
│   │   ├── Core/                      # Core systems
│   │   │   ├── GameManager.cs
│   │   │   ├── NetworkManager.cs
│   │   │   └── SceneLoader.cs
│   │   ├── WarMap/                    # RTS Layer
│   │   │   ├── WarMapManager.cs
│   │   │   ├── Node.cs
│   │   │   ├── Squadron.cs
│   │   │   ├── TokenManager.cs
│   │   │   └── SupplyLine.cs
│   │   ├── Battle/                    # FPS Layer
│   │   │   ├── BattleManager.cs
│   │   │   ├── SpawnManager.cs
│   │   │   ├── ObjectiveManager.cs
│   │   │   └── GameModes/
│   │   │       └── ControlPoints.cs
│   │   ├── Player/                    # Player systems
│   │   │   ├── PlayerController.cs
│   │   │   ├── PlayerHealth.cs
│   │   │   ├── PlayerWeapons.cs
│   │   │   └── PlayerAbilities.cs
│   │   ├── Factions/                  # Faction-specific
│   │   │   ├── FactionData.cs
│   │   │   └── Blue/
│   │   │       ├── BlueFaction.cs
│   │   │       └── DeployableTurret.cs
│   │   ├── Networking/                # Network code
│   │   │   ├── NetworkPlayer.cs
│   │   │   ├── NetworkTransforms.cs
│   │   │   └── SyncVars/
│   │   ├── UI/                        # User Interface
│   │   │   ├── WarMapUI.cs
│   │   │   ├── BattleHUD.cs
│   │   │   └── MainMenu.cs
│   │   └── Utilities/                 # Helper scripts
│   │       ├── Singleton.cs
│   │       ├── ObjectPool.cs
│   │       └── Extensions.cs
│   ├── Scenes/
│   │   ├── Core/
│   │   │   ├── MainMenu.unity
│   │   │   └── WarMap.unity
│   │   ├── Battles/
│   │   │   └── ControlPoints_MVP.unity
│   │   └── Testing/
│   │       └── NetworkTest.unity
│   ├── Prefabs/
│   │   ├── Player/
│   │   │   └── BluePlayer.prefab
│   │   ├── Deployables/
│   │   │   └── Turret.prefab
│   │   ├── UI/
│   │   └── Network/
│   │       └── NetworkManager.prefab
│   ├── Materials/
│   │   └── Factions/
│   │       ├── Blue/
│   │       ├── Red/
│   │       └── Green/
│   ├── Settings/
│   │   └── InputActions.inputactions
│   └── Resources/                     # Runtime loaded assets
│       └── FactionData/
└── Packages/                          # Unity packages
```

### 2.2 Namespace Organization

```csharp
// Core namespaces
ElitesAndPawns.Core
ElitesAndPawns.WarMap
ElitesAndPawns.Battle
ElitesAndPawns.Player
ElitesAndPawns.Factions
ElitesAndPawns.Networking
ElitesAndPawns.UI
ElitesAndPawns.Utilities
```

---

## 3. Core Systems Design

### 3.1 Game Manager (Singleton)

**Responsibilities**:
- Overall game state management
- Scene transitions (Main Menu → War Map → Battle)
- Player session management
- Global settings

**Key States**:
```csharp
public enum GameState
{
    MainMenu,
    WarMapView,      // RTS layer
    BattleLoading,   // Transition
    InBattle,        // FPS layer
    PostBattle,      // Results screen
    Paused
}
```

**Implementation**:
```csharp
public class GameManager : Singleton<GameManager>
{
    public GameState CurrentState { get; private set; }
    public FactionType PlayerFaction { get; set; }
    
    public void TransitionToWarMap()
    public void TransitionToBattle(Node node)
    public void ReturnToWarMap(BattleResult result)
}
```

---

### 3.2 War Map System (RTS Layer)

#### 3.2.1 WarMapManager

**Responsibilities**:
- Manage all nodes and their states
- Handle squadron deployments
- Process token allocation
- Update supply lines
- Synchronize map state across network

**Key Data**:
```csharp
public class WarMapManager : NetworkBehaviour
{
    [SyncVar] public float WarTimer;
    [SyncVar] public int RedControlledCities;
    [SyncVar] public int BlueControlledCities;
    [SyncVar] public int GreenControlledCities;
    
    public List<Node> AllNodes;
    public List<Squadron> ActiveSquadrons;
    
    // Update real-time (called every frame)
    public void UpdateWarMap()
    
    // Server-only: Process squadron movements
    [Server]
    public void ProcessSquadronDeployment(Squadron squad, Node target)
    
    // Check victory conditions
    public FactionType CheckVictoryCondition()
}
```

#### 3.2.2 Node

**Represents**: A location on the war map (city, resource point, etc.)

```csharp
public enum NodeType
{
    MajorCity,      // Victory condition
    ResourcePoint,  // Generates tokens
    StrategicPoint, // Tactical bonuses
    SupplyHub       // Reinforcement routing
}

public class Node : NetworkBehaviour
{
    [SyncVar] public string NodeName;
    [SyncVar] public NodeType Type;
    [SyncVar] public FactionType Controller;
    [SyncVar] public int TokensPresent;
    [SyncVar] public bool BattleActive;
    
    public List<Node> ConnectedNodes;
    public Vector2 MapPosition; // For UI display
    
    // When battle starts at this node
    [Server]
    public void StartBattle(Squadron attacker, Squadron defender)
    
    // When battle concludes
    [Server]
    public void ResolveBattle(FactionType winner)
}
```

#### 3.2.3 Squadron

**Represents**: A group of tokens deployed to a node

```csharp
public class Squadron : NetworkBehaviour
{
    [SyncVar] public string SquadronID;
    [SyncVar] public FactionType Faction;
    [SyncVar] public int TroopTokens;
    [SyncVar] public Node CurrentNode;
    [SyncVar] public Node TargetNode;
    [SyncVar] public float MovementProgress; // 0.0 to 1.0
    
    [Server]
    public void DeployToNode(Node target)
    
    [Server]
    public void ConsumeToken() // When player spawns in FPS
    
    public int GetRemainingSpawns()
}
```

#### 3.2.4 Token Manager

**Responsibilities**:
- Track global token pool per faction
- Generate tokens over time
- Validate token expenditure
- Prevent token exploits

```csharp
public class TokenManager : NetworkBehaviour
{
    // Server-authoritative token counts
    [SyncVar] private int redTokens;
    [SyncVar] private int blueTokens;
    [SyncVar] private int greenTokens;
    
    public int TokenGenerationRate = 10; // per minute
    
    [Server]
    public bool TrySpendTokens(FactionType faction, int amount)
    
    [Server]
    public void GenerateTokens() // Called periodically
    
    [Server]
    public int GetAvailableTokens(FactionType faction)
}
```

---

### 3.3 Battle System (FPS Layer)

#### 3.3.1 BattleManager

**Responsibilities**:
- Initialize battle from node data
- Manage objectives and win conditions
- Track team scores
- Handle match timer (15 minutes)
- Report results back to War Map

```csharp
public class BattleManager : NetworkBehaviour
{
    [SyncVar] public Node BattleNode;
    [SyncVar] public Squadron AttackingSquad;
    [SyncVar] public Squadron DefendingSquad;
    [SyncVar] public float MatchTimer; // 900 seconds (15 min)
    [SyncVar] public int AttackerScore;
    [SyncVar] public int DefenderScore;
    
    public GameMode CurrentGameMode; // Control Points for MVP
    
    [Server]
    public void InitializeBattle(Node node, Squadron attacker, Squadron defender)
    
    [Server]
    public void UpdateMatchTimer()
    
    [Server]
    public void CheckWinConditions()
    
    [Server]
    public void EndBattle(FactionType winner)
}
```

#### 3.3.2 SpawnManager

**Responsibilities**:
- Handle player spawning
- Consume tokens on spawn
- Manage spawn points
- Enforce spawn rules (no spawning on enemies, etc.)

```csharp
public class SpawnManager : NetworkBehaviour
{
    public List<Transform> AttackerSpawnPoints;
    public List<Transform> DefenderSpawnPoints;
    public float SpawnProtectionTime = 3f;
    
    [Server]
    public void SpawnPlayer(NetworkPlayer player, FactionType faction)
    {
        // 1. Check if faction has tokens available
        // 2. Consume one token from squadron
        // 3. Instantiate player at spawn point
        // 4. Apply spawn protection
    }
    
    [Server]
    public Transform GetSpawnPoint(FactionType faction)
}
```

#### 3.3.3 Objective Manager (Control Points MVP)

**Responsibilities**:
- Manage capture points
- Track capture progress
- Award points for holding objectives
- Update UI indicators

```csharp
public class ObjectiveManager : NetworkBehaviour
{
    public List<CapturePoint> CapturePoints;
    [SyncVar] public int PointsPerSecond = 1;
    
    [Server]
    public void UpdateObjectives()
    {
        // For each capture point:
        // - Check which team is capturing
        // - Update capture progress
        // - Award points if controlled
    }
}

public class CapturePoint : NetworkBehaviour
{
    [SyncVar] public FactionType Controller;
    [SyncVar] public float CaptureProgress; // -1.0 to 1.0
    [SyncVar] public int PlayersNearby_Attacker;
    [SyncVar] public int PlayersNearby_Defender;
    
    public float CaptureRadius = 10f;
    public float CaptureRate = 0.1f; // per second per player
    
    [Server]
    public void UpdateCapture()
}
```

---

### 3.4 Player System

#### 3.4.1 NetworkPlayer (Mirror)

**Responsibilities**:
- Network identity and authority
- Synchronize player state
- Handle input and commands

```csharp
public class NetworkPlayer : NetworkBehaviour
{
    [SyncVar] public string PlayerName;
    [SyncVar] public FactionType Faction;
    [SyncVar] public int PlayerID;
    
    // Components
    private PlayerController controller;
    private PlayerHealth health;
    private PlayerWeapons weapons;
    private PlayerAbilities abilities;
    
    public override void OnStartLocalPlayer()
    {
        // Setup local player (camera, input, UI)
    }
}
```

#### 3.4.2 PlayerController

**Responsibilities**:
- Movement (WASD)
- Jumping
- Crouching
- Camera control

```csharp
public class PlayerController : NetworkBehaviour
{
    [Header("Movement")]
    public float MoveSpeed = 5f;
    public float JumpForce = 5f;
    public float Gravity = -9.81f;
    
    private CharacterController characterController;
    private Vector3 velocity;
    
    private void Update()
    {
        if (!isLocalPlayer) return;
        
        HandleMovement();
        HandleJump();
        ApplyGravity();
    }
    
    [Client]
    private void HandleMovement()
    {
        // Read input from Input System
        // Move character
        // Send movement to server via Command
    }
    
    [Command]
    private void CmdMove(Vector3 movement)
    {
        // Server validates and applies movement
    }
}
```

#### 3.4.3 PlayerHealth

**Responsibilities**:
- Health tracking
- Damage handling
- Death and respawn

```csharp
public class PlayerHealth : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnHealthChanged))]
    private float health = 100f;
    
    public float MaxHealth = 100f;
    
    [Server]
    public void TakeDamage(float damage, NetworkPlayer attacker)
    {
        health -= damage;
        
        if (health <= 0)
        {
            Die(attacker);
        }
    }
    
    [Server]
    private void Die(NetworkPlayer killer)
    {
        // Award kill credit
        // Trigger respawn after delay
        RpcPlayDeathEffect();
    }
    
    [ClientRpc]
    private void RpcPlayDeathEffect()
    {
        // Death animation, sound, etc.
    }
    
    private void OnHealthChanged(float oldHealth, float newHealth)
    {
        // Update health UI
    }
}
```

#### 3.4.4 PlayerWeapons

**Responsibilities**:
- Weapon handling
- Shooting mechanics
- Ammo management
- Weapon switching (if multiple weapons)

```csharp
public class PlayerWeapons : NetworkBehaviour
{
    public Weapon CurrentWeapon; // Assault Rifle for MVP
    
    [SyncVar] private int currentAmmo;
    [SyncVar] private int reserveAmmo;
    
    private void Update()
    {
        if (!isLocalPlayer) return;
        
        if (Input.GetButton("Fire1"))
        {
            TryShoot();
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            TryReload();
        }
    }
    
    [Client]
    private void TryShoot()
    {
        if (currentAmmo <= 0) return;
        
        // Raycast for hit detection
        // Visual feedback (muzzle flash, tracer)
        // Send to server
        CmdShoot(hitPoint, hitNormal);
    }
    
    [Command]
    private void CmdShoot(Vector3 hitPoint, Vector3 hitNormal)
    {
        // Server validates shot
        // Apply damage if hit player
        // Spawn bullet impact effect
    }
}
```

#### 3.4.5 PlayerAbilities (BLUE Faction)

**Responsibilities**:
- Faction-specific abilities
- Cooldown management
- Deployable placement

```csharp
public class PlayerAbilities : NetworkBehaviour
{
    public FactionAbility[] Abilities;
    
    // BLUE MVP: Deployable Turret
    public GameObject TurretPrefab;
    public float TurretCooldown = 30f;
    private float turretCooldownTimer;
    
    private void Update()
    {
        if (!isLocalPlayer) return;
        
        turretCooldownTimer -= Time.deltaTime;
        
        if (Input.GetKeyDown(KeyCode.Q) && turretCooldownTimer <= 0)
        {
            TryDeployTurret();
        }
    }
    
    [Client]
    private void TryDeployTurret()
    {
        // Check placement validity (ground check, clear space)
        // Show placement preview
        CmdDeployTurret(placementPosition, placementRotation);
    }
    
    [Command]
    private void CmdDeployTurret(Vector3 position, Quaternion rotation)
    {
        // Server spawns turret
        GameObject turret = Instantiate(TurretPrefab, position, rotation);
        NetworkServer.Spawn(turret);
        
        turretCooldownTimer = TurretCooldown;
        RpcTurretDeployed(); // Visual/audio feedback
    }
}
```

---

### 3.5 Faction System (BLUE MVP)

#### 3.5.1 FactionData (ScriptableObject)

**Stores**: Static faction data

```csharp
[CreateAssetMenu(fileName = "FactionData", menuName = "Elites/Faction")]
public class FactionData : ScriptableObject
{
    public string FactionName;
    public FactionType Type;
    public Color FactionColor;
    
    [Header("Stats")]
    public float MovementSpeedMultiplier = 1.0f;
    public float HealthMultiplier = 1.0f;
    public float DamageMultiplier = 1.0f;
    
    [Header("Abilities")]
    public FactionAbility[] Abilities;
    
    [Header("Weapons")]
    public Weapon[] StartingWeapons;
}
```

#### 3.5.2 DeployableTurret

**BLUE Faction Ability**:

```csharp
public class DeployableTurret : NetworkBehaviour
{
    [SyncVar] public NetworkPlayer Owner;
    [SyncVar] private float health = 100f;
    
    public float DetectionRadius = 15f;
    public float FireRate = 2f; // shots per second
    public float Damage = 10f;
    public float Lifetime = 60f; // 1 minute
    
    private float nextFireTime;
    private NetworkPlayer currentTarget;
    
    [Server]
    private void Update()
    {
        // Lifetime countdown
        Lifetime -= Time.deltaTime;
        if (Lifetime <= 0)
        {
            DestroySelf();
            return;
        }
        
        // Find enemy targets
        currentTarget = FindNearestEnemy();
        
        // Shoot at target
        if (currentTarget != null && Time.time >= nextFireTime)
        {
            ShootAtTarget(currentTarget);
            nextFireTime = Time.time + (1f / FireRate);
        }
    }
    
    [Server]
    private NetworkPlayer FindNearestEnemy()
    {
        // Scan for enemies within radius
        // Prioritize closest
    }
    
    [Server]
    private void ShootAtTarget(NetworkPlayer target)
    {
        // Apply damage
        target.GetComponent<PlayerHealth>().TakeDamage(Damage, Owner);
        
        // Visual effect
        RpcPlayShootEffect(target.transform.position);
    }
    
    [Server]
    public void TakeDamage(float damage)
    {
        health -= damage;
        if (health <= 0)
        {
            DestroySelf();
        }
    }
    
    [Server]
    private void DestroySelf()
    {
        RpcPlayDestroyEffect();
        NetworkServer.Destroy(gameObject);
    }
    
    [ClientRpc]
    private void RpcPlayShootEffect(Vector3 targetPos)
    {
        // Laser beam, sound effect, etc.
    }
    
    [ClientRpc]
    private void RpcPlayDestroyEffect()
    {
        // Explosion, sparks, etc.
    }
}
```

---

## 4. Network Architecture (Mirror)

### 4.1 Network Topology

**Model**: Server-Authoritative Client-Server

```
Client 1 ──┐
Client 2 ──┤
Client 3 ──┼──► Dedicated Server (Authority)
Client 4 ──┤
...        │
Client 16 ─┘
```

**Why Server-Authoritative**:
- Prevents cheating (all game logic on server)
- Token validation secured
- Consistent game state
- Required for competitive PvP

### 4.2 Network Manager Setup

```csharp
using Mirror;

public class ElitesNetworkManager : NetworkManager
{
    [Header("Elites Configuration")]
    public int MaxPlayers = 16; // 8v8
    public GameObject PlayerPrefab;
    
    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // Spawn player
        GameObject player = Instantiate(PlayerPrefab);
        NetworkServer.AddPlayerForConnection(conn, player);
        
        // Assign to faction (balance teams)
        FactionType faction = GetBalancedFaction();
        player.GetComponent<NetworkPlayer>().Faction = faction;
    }
    
    private FactionType GetBalancedFaction()
    {
        // Count players per faction
        // Assign to least populated
        // MVP: Only BLUE faction
        return FactionType.Blue;
    }
    
    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        // Handle player leaving mid-battle
        // Return their unused tokens to pool?
        base.OnServerDisconnect(conn);
    }
}
```

### 4.3 Network Synchronization Strategy

#### 4.3.1 SyncVars (State)
Use for:
- Player health
- Token counts
- Node ownership
- Match timer
- Scores

**Example**:
```csharp
[SyncVar(hook = nameof(OnHealthChanged))]
private float health;
```

#### 4.3.2 Commands (Client → Server)
Use for:
- Player actions (shoot, deploy, move)
- RTS commands (deploy squadron)
- Purchases/spawns

**Example**:
```csharp
[Command]
private void CmdShoot(Vector3 direction)
{
    // Server validates and executes
}
```

#### 4.3.3 ClientRpc (Server → All Clients)
Use for:
- Visual effects
- Sound effects
- Non-gameplay cosmetics

**Example**:
```csharp
[ClientRpc]
private void RpcPlayExplosion(Vector3 position)
{
    // All clients play explosion VFX
}
```

#### 4.3.4 TargetRpc (Server → Specific Client)
Use for:
- Personal notifications
- UI updates
- Client-specific feedback

**Example**:
```csharp
[TargetRpc]
private void TargetNotifyLowAmmo(NetworkConnection target)
{
    // Only this player sees low ammo warning
}
```

### 4.4 Network Optimization

#### 4.4.1 Update Rates
```csharp
// In NetworkManager
sendRate = 30; // 30 updates per second (balance responsiveness/bandwidth)
```

#### 4.4.2 Interest Management
- Players only receive updates for nearby objects
- War Map visible to all (low update rate)
- Battle updates only during active battle

#### 4.4.3 Object Pooling
- Reuse bullet impact effects
- Reuse UI elements
- Reduces instantiation overhead

---

## 5. Data Structures

### 5.1 Save Data (Player Progression - Post-MVP)

```csharp
[System.Serializable]
public class PlayerData
{
    public string PlayerID;
    public string Username;
    public int Level;
    public int TotalKills;
    public int TotalDeaths;
    public int WarsWon;
    public int WarsLost;
    public Dictionary<FactionType, int> FactionExperience;
    
    // Serialize to JSON
    public string ToJson()
    {
        return JsonUtility.ToJson(this);
    }
    
    // Deserialize from JSON
    public static PlayerData FromJson(string json)
    {
        return JsonUtility.FromJson<PlayerData>(json);
    }
}
```

### 5.2 War Map State

```csharp
[System.Serializable]
public class WarMapState
{
    public float WarTimer;
    public List<NodeState> Nodes;
    public List<SquadronState> Squadrons;
    public Dictionary<FactionType, int> TokenPools;
    
    public byte[] Serialize()
    {
        // Serialize to byte array for network transmission
    }
    
    public static WarMapState Deserialize(byte[] data)
    {
        // Deserialize from byte array
    }
}

[System.Serializable]
public class NodeState
{
    public string NodeID;
    public FactionType Controller;
    public int TokensPresent;
    public bool BattleActive;
}
```

### 5.3 Battle Results

```csharp
[System.Serializable]
public class BattleResult
{
    public Node BattleNode;
    public FactionType Winner;
    public FactionType Loser;
    public int AttackerScore;
    public int DefenderScore;
    public int TokensRemaining_Attacker;
    public int TokensRemaining_Defender;
    public float BattleDuration;
    
    public Dictionary<int, PlayerBattleStats> PlayerStats;
}

[System.Serializable]
public class PlayerBattleStats
{
    public int PlayerID;
    public int Kills;
    public int Deaths;
    public int ObjectivesCaptured;
    public int DamageDealt;
    public int DamageTaken;
}
```

---

## 6. Scene Management

### 6.1 Scene Flow

```
MainMenu.unity
    ↓ (Player clicks "Play")
WarMap.unity (RTS Layer)
    ↓ (Player joins battle at node)
Additive Load: ControlPoints_MVP.unity (FPS Layer)
    ↓ (Battle ends after 15 min or token depletion)
Unload Battle Scene
    ↓ (Return to War Map)
WarMap.unity (Updated state)
```

### 6.2 Additive Scene Loading

**Why Additive**:
- Keep War Map loaded in memory
- Faster transitions
- Maintain network connections

**Implementation**:
```csharp
public class SceneLoader : MonoBehaviour
{
    public void LoadBattleScene(string sceneName, Node node)
    {
        StartCoroutine(LoadBattleAsync(sceneName, node));
    }
    
    private IEnumerator LoadBattleAsync(string sceneName, Node node)
    {
        // Show loading screen
        UIManager.Instance.ShowLoadingScreen();
        
        // Load battle scene additively
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(
            sceneName, 
            LoadSceneMode.Additive
        );
        
        while (!asyncLoad.isDone)
        {
            // Update loading bar
            float progress = asyncLoad.progress;
            UIManager.Instance.UpdateLoadingBar(progress);
            yield return null;
        }
        
        // Initialize battle
        BattleManager.Instance.InitializeBattle(node);
        
        // Hide loading screen
        UIManager.Instance.HideLoadingScreen();
    }
    
    public void UnloadBattleScene(string sceneName)
    {
        SceneManager.UnloadSceneAsync(sceneName);
    }
}
```

---

## 7. Input System

### 7.1 Input Actions

**Using Unity's Input System**:

```
InputActions.inputactions
├── Player
│   ├── Move (WASD)
│   ├── Jump (Space)
│   ├── Shoot (Mouse0)
│   ├── Aim (Mouse1)
│   ├── Reload (R)
│   ├── Ability1 (Q) - Deploy Turret
│   ├── Crouch (Ctrl)
│   └── Sprint (Shift)
├── UI
│   ├── Pause (Esc)
│   ├── Scoreboard (Tab)
│   └── Map (M)
└── WarMap
    ├── SelectNode (Mouse0)
    ├── DeploySquadron (Mouse1)
    └── PanCamera (WASD or Mouse Edge)
```

### 7.2 Input Handling

```csharp
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    private PlayerInputActions inputActions;
    private PlayerController controller;
    private PlayerWeapons weapons;
    
    private void Awake()
    {
        inputActions = new PlayerInputActions();
        controller = GetComponent<PlayerController>();
        weapons = GetComponent<PlayerWeapons>();
    }
    
    private void OnEnable()
    {
        inputActions.Player.Enable();
        
        // Bind actions
        inputActions.Player.Move.performed += OnMove;
        inputActions.Player.Jump.performed += OnJump;
        inputActions.Player.Shoot.performed += OnShoot;
        inputActions.Player.Ability1.performed += OnAbility1;
    }
    
    private void OnDisable()
    {
        inputActions.Player.Disable();
    }
    
    private void OnMove(InputAction.CallbackContext context)
    {
        Vector2 input = context.ReadValue<Vector2>();
        controller.SetMoveInput(input);
    }
    
    private void OnJump(InputAction.CallbackContext context)
    {
        controller.Jump();
    }
    
    private void OnShoot(InputAction.CallbackContext context)
    {
        weapons.Shoot();
    }
    
    private void OnAbility1(InputAction.CallbackContext context)
    {
        GetComponent<PlayerAbilities>().UseAbility(0); // Turret
    }
}
```

---

## 8. UI System

### 8.1 UI Hierarchy

```
Canvas (Screen Space - Overlay)
├── MainMenuUI
│   ├── Title
│   ├── PlayButton
│   ├── SettingsButton
│   └── QuitButton
├── WarMapUI
│   ├── MapView
│   ├── TokenCounter
│   ├── NodeInfo Panel
│   ├── Squadron List
│   └── DeployButton
├── BattleHUD
│   ├── HealthBar
│   ├── AmmoCounter
│   ├── AbilityCooldown
│   ├── ObjectiveIndicators
│   ├── Minimap
│   ├── TeamScore
│   ├── MatchTimer
│   └── KillFeed
└── PostBattleUI
    ├── VictoryDefeat Banner
    ├── ScoreBreakdown
    ├── PlayerStats
    └── ContinueButton
```

### 8.2 Key UI Components

#### 8.2.1 War Map UI
```csharp
public class WarMapUI : MonoBehaviour
{
    public Text TokenCountText;
    public GameObject NodeInfoPanel;
    public Transform SquadronListContainer;
    
    private WarMapManager warMapManager;
    
    public void UpdateTokenDisplay(int tokens)
    {
        TokenCountText.text = $"Tokens: {tokens}";
    }
    
    public void DisplayNodeInfo(Node node)
    {
        NodeInfoPanel.SetActive(true);
        // Populate node details
    }
    
    public void OnDeploySquadronButton(Node targetNode)
    {
        // Create squadron, assign tokens, send to node
        warMapManager.DeploySquadron(targetNode);
    }
}
```

#### 8.2.2 Battle HUD
```csharp
public class BattleHUD : MonoBehaviour
{
    public Slider HealthBar;
    public Text AmmoText;
    public Image AbilityCooldownFill;
    public Text TeamScoreText;
    public Text MatchTimerText;
    public Transform KillFeedContainer;
    
    public void UpdateHealth(float current, float max)
    {
        HealthBar.value = current / max;
    }
    
    public void UpdateAmmo(int current, int reserve)
    {
        AmmoText.text = $"{current} / {reserve}";
    }
    
    public void UpdateAbilityCooldown(float remaining, float total)
    {
        AbilityCooldownFill.fillAmount = remaining / total;
    }
    
    public void UpdateMatchTimer(float timeRemaining)
    {
        int minutes = Mathf.FloorToInt(timeRemaining / 60);
        int seconds = Mathf.FloorToInt(timeRemaining % 60);
        MatchTimerText.text = $"{minutes:00}:{seconds:00}";
    }
    
    public void AddKillFeedEntry(string killerName, string victimName)
    {
        // Create kill notification
        // Auto-remove after 5 seconds
    }
}
```

---

## 9. Performance Optimization

### 9.1 Target Metrics
- **FPS**: Minimum 60 on mid-range hardware
- **Network**: <100ms latency, <5% packet loss
- **Memory**: <4GB RAM usage
- **Load Times**: <10s scene transitions

### 9.2 Optimization Strategies

#### 9.2.1 Graphics
- **LOD System**: Multiple detail levels for players/objects
- **Occlusion Culling**: Don't render what camera can't see
- **Batching**: Combine meshes where possible
- **Texture Atlasing**: Reduce draw calls
- **Low-Poly Models**: Performance over fidelity

#### 9.2.2 Physics
- **Layer Collision Matrix**: Only check relevant collisions
- **Fixed Update Rate**: 50 Hz (balance accuracy/performance)
- **Simple Colliders**: Boxes/Spheres over Mesh Colliders
- **Raycasting**: Limit per frame, use LayerMasks

#### 9.2.3 Networking
- **Update Rate**: 30 Hz for player updates
- **Interest Management**: Only sync nearby objects
- **Compression**: Compress large data transfers
- **Lag Compensation**: Client-side prediction + reconciliation

#### 9.2.4 Code
- **Object Pooling**: Reuse frequently spawned objects
- **Caching**: Store GetComponent results
- **Avoid in Update**: Don't search/allocate in Update loop
- **Profiling**: Regular performance audits

### 9.3 Profiling Tools
- Unity Profiler (CPU, GPU, Memory, Network)
- Frame Debugger (Rendering pipeline)
- Mirror Network Statistics

---

## 10. Development Milestones

### 10.1 Milestone 1: Network Foundation (Week 1-2)
**Goals**:
- Mirror installed and configured
- Basic player spawning
- Movement synchronized
- Simple shooting works

**Deliverables**:
- NetworkManager setup
- Player prefab with NetworkIdentity
- Basic movement replication
- Simple raycast shooting

**Test**: 2 players can join, move, and shoot each other

### 10.2 Milestone 2: War Map Prototype (Week 3-4)
**Goals**:
- Visual war map with 5 nodes
- Token system backend
- Squadron deployment UI
- Node state tracking

**Deliverables**:
- WarMapManager script
- Node prefabs (visual representation)
- Token allocation UI
- Squadron movement visualization

**Test**: Can deploy squadrons, tokens are consumed, nodes update

### 10.3 Milestone 3: FPS Battle Core (Week 5-6)
**Goals**:
- Control Points game mode
- Spawn system with token consumption
- Basic win conditions
- Match timer (15 min)

**Deliverables**:
- BattleManager script
- SpawnManager with token integration
- 3 capture points
- Score tracking
- Post-battle results

**Test**: 8v8 battle with token spawning, capturing points awards victory

### 10.4 Milestone 4: BLUE Faction (Week 7-8)
**Goals**:
- BLUE faction identity (color, stats)
- Deployable turret ability
- Turret AI and combat
- Ability cooldown UI

**Deliverables**:
- FactionData ScriptableObject
- DeployableTurret prefab
- Turret targeting and shooting
- Cooldown indicator

**Test**: BLUE players can deploy turrets that shoot enemies

### 10.5 Milestone 5: MVP Polish (Week 9-10)
**Goals**:
- UI polish (HUD, War Map)
- Sound effects (shooting, explosions, UI)
- Basic visual effects (muzzle flash, hit impacts)
- Bug fixing and stability

**Deliverables**:
- Polished UI with faction colors
- Sound design pass
- VFX for key events
- Stability improvements

**Test**: Full playthrough feels complete and fun

### 10.6 Milestone 6: Playtest & Iterate (Week 11-12)
**Goals**:
- Internal playtests
- Balance adjustments
- Bug fixes
- Performance optimization

**Deliverables**:
- Playtest feedback document
- Balance changes based on data
- Bug fix patch
- Performance improvements

**Test**: External playtest with target metrics achieved

---

## 11. Testing Strategy

### 11.1 Unit Tests
**Tools**: Unity Test Framework

**Test Coverage**:
- Token calculations (spend, generate, validate)
- Win condition logic
- Squadron movement calculations
- Capture point progress

**Example**:
```csharp
[Test]
public void TokenManager_SpendTokens_DeductsCorrectly()
{
    TokenManager tm = new TokenManager();
    tm.AddTokens(FactionType.Blue, 100);
    
    bool success = tm.TrySpendTokens(FactionType.Blue, 10);
    
    Assert.IsTrue(success);
    Assert.AreEqual(90, tm.GetAvailableTokens(FactionType.Blue));
}
```

### 11.2 Integration Tests
**Test**:
- RTS → FPS transition
- Token consumption on spawn
- Battle results affecting War Map
- Network synchronization

### 11.3 Playtesting
**Types**:
1. **Solo Testing**: Developer tests alone (bot enemies?)
2. **Internal Playtest**: 4-8 testers, structured feedback
3. **Closed Beta**: 50-100 players, balance data collection
4. **Open Beta**: Public testing, stress test servers

**Metrics to Track**:
- Average match duration (target: 15 min)
- Win rates per faction (target: 45-55%)
- Player retention (day 1, week 1)
- Crash reports and bugs
- Network performance (latency, packet loss)

---

## 12. Deployment & DevOps

### 12.1 Version Control (Git)
**Branch Strategy**:
```
main          - Stable releases
develop       - Integration branch
feature/*     - New features
bugfix/*      - Bug fixes
hotfix/*      - Urgent production fixes
```

**Commit Standards**:
```
type(scope): Brief description

- Detailed point 1
- Detailed point 2

Refs: #IssueNumber
```

### 12.2 Build Pipeline
1. Local development (Unity Editor)
2. Commit to feature branch
3. Merge to develop
4. Automated build (dedicated server + client)
5. Deploy to test server
6. Playtest and validate
7. Merge to main (release)

### 12.3 Server Hosting

**MVP**:
- Self-hosted dedicated server (PC/VM)
- Manual deployment
- Up to 16 players (8v8)

**Post-MVP**:
- Cloud hosting (AWS, Google Cloud, Azure)
- Auto-scaling based on load
- Geographic regions (NA, EU, Asia)
- Matchmaking service

**Server Requirements (Per Instance)**:
- CPU: 2-4 cores
- RAM: 4-8 GB
- Network: 100 Mbps+ (5-10 Mbps per player)
- Storage: Minimal (<1 GB)

---

## 13. Security Considerations

### 13.1 Anti-Cheat Measures
**Server-Side Validation**:
- All gameplay logic on server
- Validate player positions (speed hacks)
- Validate shots (aimbot detection)
- Validate token spending (economy exploits)

**Client-Side**:
- Obfuscate code (post-MVP)
- Encrypt network traffic
- Detect memory manipulation tools

### 13.2 Token Security
**Critical**:
- Never trust client token counts
- Server is source of truth
- Validate all token operations
- Log suspicious activity

```csharp
[Command]
private void CmdSpawnPlayer()
{
    // Bad: Trust client
    // if (clientTokenCount > 0) { Spawn(); }
    
    // Good: Check server-side
    if (TokenManager.Instance.TrySpendTokens(player.Faction, 1))
    {
        SpawnManager.Instance.SpawnPlayer(player);
    }
    else
    {
        TargetRpcNotifyInsufficientTokens();
    }
}
```

### 13.3 Account Security (Post-MVP)
- Password hashing (bcrypt)
- Rate limiting (prevent brute force)
- Email verification
- Two-factor authentication (optional)

---

## 14. Localization (Post-MVP)

### 14.1 Text Localization
**Tools**: Unity Localization Package

**Languages** (Priority):
1. English (default)
2. Spanish
3. French
4. German
5. Russian (large FPS/RTS audience)

### 14.2 Implementation
```csharp
using UnityEngine.Localization;

public class LocalizedText : MonoBehaviour
{
    public LocalizedString localizedString;
    private Text textComponent;
    
    private void Start()
    {
        textComponent = GetComponent<Text>();
        localizedString.StringChanged += UpdateText;
    }
    
    private void UpdateText(string value)
    {
        textComponent.text = value;
    }
}
```

---

## 15. Analytics & Telemetry (Post-MVP)

### 15.1 Key Metrics to Track
**Engagement**:
- Daily Active Users (DAU)
- Session length
- Matches played per session
- Retention (D1, D7, D30)

**Balance**:
- Faction win rates
- Weapon usage rates
- Ability usage rates
- Average match duration
- Token economy (generation vs. spending)

**Technical**:
- Crash reports
- Average FPS
- Network latency distribution
- Load times

**Monetization** (If Live Service):
- Conversion rate
- Average revenue per user (ARPU)
- Lifetime value (LTV)

### 15.2 Implementation
**Tools**: Unity Analytics, Custom Backend

```csharp
public class AnalyticsManager : Singleton<AnalyticsManager>
{
    public void TrackEvent(string eventName, Dictionary<string, object> parameters)
    {
        // Send to analytics service
        UnityEngine.Analytics.Analytics.CustomEvent(eventName, parameters);
    }
    
    public void TrackMatchEnd(BattleResult result)
    {
        TrackEvent("match_end", new Dictionary<string, object>
        {
            { "winner", result.Winner.ToString() },
            { "duration", result.BattleDuration },
            { "attacker_score", result.AttackerScore },
            { "defender_score", result.DefenderScore },
            { "tokens_remaining", result.TokensRemaining_Attacker }
        });
    }
}
```

---

## 16. Known Technical Challenges

### 16.1 Challenge: Token Synchronization
**Problem**: Keeping token counts consistent across all clients

**Solution**:
- Server is authoritative
- Use SyncVars for counts
- Validate all operations server-side
- Update clients via callbacks

### 16.2 Challenge: Seamless Scene Transition
**Problem**: Loading battle scene while maintaining network connection

**Solution**:
- Additive scene loading
- Keep War Map scene loaded (but hidden)
- Don't destroy network objects during transition
- Show loading screen to mask load time

### 16.3 Challenge: Real-Time War Map Updates
**Problem**: All players need to see map changes instantly

**Solution**:
- Server broadcasts map state changes
- Use SyncVars for node ownership
- Event system for UI updates
- Efficient network bandwidth usage (only send deltas)

### 16.4 Challenge: 8v8 Network Performance
**Problem**: 16 players + projectiles + effects = lots of network traffic

**Solution**:
- Interest management (only sync nearby objects)
- Reduce update rate for distant objects
- Use object pooling (reduce spawn messages)
- Compress data where possible
- Client-side prediction for local player

### 16.5 Challenge: Deployable Turret AI
**Problem**: Server must run AI for multiple turrets

**Solution**:
- Simple AI (raycast to nearest enemy)
- Limit max turrets per team (e.g., 5)
- Optimize raycasts (LayerMask, limited distance)
- Turret lifetime (auto-despawn after 60s)

---

## 17. Future Technical Considerations

### 17.1 Mobile RTS (Post-MVP)
**Challenges**:
- Touch input for War Map
- Cross-platform networking (mobile ↔ PC)
- UI scaling for various screen sizes
- Performance on lower-end devices

**Solutions**:
- Separate mobile build (RTS only)
- Shared backend/server
- Responsive UI framework
- LOD and culling aggressive on mobile

### 17.2 Console Ports (Post-MVP)
**Challenges**:
- Controller input for FPS
- Console certification requirements
- Platform-specific networking (PSN, Xbox Live)
- Different performance profiles

**Solutions**:
- Unity Input System (multi-platform)
- Early console dev kit access
- Platform SDK integration
- Separate optimization pass per console

### 17.3 Matchmaking Service
**Challenges**:
- Balancing teams by skill
- Minimizing queue times
- Geographic server selection
- Handling disconnections

**Solutions**:
- ELO/MMR rating system
- Queue with backfill (start 6v6, fill to 8v8)
- Ping-based matchmaking
- Reconnect feature (rejoin in-progress battle)

---

## 18. Documentation Standards

### 18.1 Code Documentation
**All public methods and classes should have XML documentation**:

```csharp
/// <summary>
/// Deploys a squadron to the target node.
/// </summary>
/// <param name="squadron">The squadron to deploy.</param>
/// <param name="targetNode">The destination node.</param>
/// <returns>True if deployment successful, false otherwise.</returns>
[Server]
public bool DeploySquadron(Squadron squadron, Node targetNode)
{
    // Implementation
}
```

### 18.2 Architecture Decision Records (ADR)
**Document major technical decisions**:

```markdown
# ADR 001: Use Mirror Networking

## Context
Need multiplayer networking for FPS/RTS hybrid game.

## Decision
Use Mirror Networking instead of Unity's Netcode or Photon.

## Rationale
- Free and open-source
- Proven in production games
- Excellent documentation
- Active community
- Easy to learn

## Consequences
- Must host dedicated servers (not P2P)
- Need to learn Mirror API
- Limited to Unity engine
```

### 18.3 API Documentation (Post-MVP)
**If exposing APIs for mods/tools**:
- OpenAPI/Swagger specification
- Example code snippets
- Versioning strategy

---

## 19. Risk Mitigation

### 19.1 Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Mirror network issues | Medium | High | Start early, prototype networking first, have fallback (Netcode) |
| Performance with 16 players | Medium | High | Early performance testing, scalability tests, LOD systems |
| Token synchronization bugs | Medium | High | Extensive unit tests, server-side validation, logging |
| Scene transition failures | Low | Medium | Robust error handling, fallback to main menu |
| Deployable AI performance | Low | Medium | Limit max turrets, optimize AI, simple targeting |

### 19.2 Schedule Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| Scope creep | High | High | Strict MVP definition, backlog for post-MVP features |
| Underestimated complexity | Medium | High | Add buffer time (20-30%), prioritize ruthlessly |
| Solo dev burnout | Medium | High | Milestone-based dev, regular breaks, celebrate wins |
| Third-party dependency issues | Low | Medium | Keep Mirror updated, have alternative solutions researched |

---

## 20. Success Criteria (Technical)

### 20.1 MVP Launch Checklist

**Performance**:
- [ ] 60 FPS on mid-range PC (GTX 1060, i5, 16GB RAM)
- [ ] <100ms network latency (same region)
- [ ] <10s scene load times
- [ ] <5% crash rate

**Functionality**:
- [ ] 8v8 battles stable
- [ ] Token system prevents over-spawning
- [ ] War Map updates in real-time
- [ ] Battles last ~15 minutes
- [ ] Capture Points mode works correctly
- [ ] BLUE turrets deploy and function
- [ ] Winners/losers determined correctly
- [ ] Players can rejoin War Map after battle

**Network**:
- [ ] No duplication exploits
- [ ] No severe desync issues
- [ ] Graceful handling of disconnects
- [ ] Server can handle 16 players

**Quality**:
- [ ] No game-breaking bugs
- [ ] No major exploits
- [ ] UI is functional and clear
- [ ] Controls feel responsive

---

## 21. Post-MVP Technical Roadmap

### 21.1 Phase 3: RED & GREEN Factions
**Technical Work**:
- Environmental destruction system (voxel-based? mesh deformation?)
- Grappling hook physics
- Stealth/visibility system
- New abilities and their network synchronization

### 21.2 Phase 4: Additional Content
**Technical Work**:
- Vehicle physics and networking
- New game modes (King of Hill, CTF)
- Larger war maps (performance considerations)
- Player progression database

### 21.3 Phase 5: Live Service Features
**Technical Work**:
- Dedicated server infrastructure (cloud hosting)
- Matchmaking service
- Anti-cheat integration
- Analytics dashboard
- Community features (clans, leaderboards)
- Seasonal content pipeline

---

## Appendix A: Technology Stack Summary

| Component | Technology | Version |
|-----------|------------|---------|
| Engine | Unity | 6000.2.8f1 |
| Rendering | Universal Render Pipeline (URP) | 17.2.0 |
| Networking | Mirror | Latest (TBD during setup) |
| Input | Unity Input System | 1.14.2 |
| Physics | Unity Physics | Built-in |
| UI | Unity UI (uGUI) | 2.0.0 |
| Version Control | Git | 2.43.0 |
| IDE | Rider / Visual Studio | Latest |
| Language | C# | 9.0+ |

---

## Appendix B: Glossary of Technical Terms

- **Server-Authoritative**: Server validates all gameplay logic (prevents cheating)
- **SyncVar**: Mirror variable that automatically synchronizes across network
- **Command**: Client-to-server RPC (Remote Procedure Call)
- **ClientRpc**: Server-to-all-clients RPC
- **TargetRpc**: Server-to-specific-client RPC
- **Additive Scene Loading**: Loading scene without unloading current scene
- **LOD (Level of Detail)**: Multiple quality versions of 3D model, swap based on distance
- **Occlusion Culling**: Don't render objects blocked by other objects
- **Object Pooling**: Reuse objects instead of destroying/creating
- **Interest Management**: Only send network updates for nearby/relevant objects
- **Raycasting**: Shooting invisible ray to detect collisions (used for bullets)

---

## Appendix C: External Resources

### C.1 Mirror Networking
- **Documentation**: https://mirror-networking.gitbook.io/docs/
- **Discord**: https://discord.gg/N9QVxbM
- **GitHub**: https://github.com/vis2k/Mirror

### C.2 Unity Resources
- **Unity Learn**: https://learn.unity.com/
- **URP Documentation**: https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest
- **Input System**: https://docs.unity3d.com/Packages/com.unity.inputsystem@latest

### C.3 Game Development
- **Game Programming Patterns**: https://gameprogrammingpatterns.com/
- **Multiplayer Game Programming**: Architecture and best practices
- **Unity Optimization**: Performance best practices

---

**Document Status**: Complete v1.0  
**Next Steps**: Begin Milestone 1 - Network Foundation  
**Approval**: Adrian (Project Manager)

---

*This document will be updated as technical decisions are made and implementation progresses.*
