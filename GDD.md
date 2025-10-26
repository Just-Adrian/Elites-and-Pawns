# Elites and Pawns True - Game Design Document (GDD)

**Version**: 1.0  
**Date**: October 26, 2025  
**Status**: Initial Design Phase  
**Lead Designer**: Adrian (Just-Adrian)  
**Technical Lead**: Claude

---

## Executive Summary

**Elites and Pawns True** is an asymmetrical multiplayer game combining FPS combat with RTS strategy in a post-apocalyptic setting. Players engage in a persistent war across a map of interconnected nodes, managing resources and troops at the strategic level while personally fighting battles at the tactical level.

**Core Hook**: "Command armies on the war map, then BE the soldier fighting for control on the ground."

---

## 1. Game Overview

### 1.1 High Concept
- **Genre**: Asymmetrical FPS/RTS Hybrid, PvP
- **Setting**: Post-Apocalyptic Factional Warfare
- **Player Count**: 8v8 (16 players total)
- **Platform**: PC (primary), Console & Mobile RTS (future)
- **Monetization**: TBD (potential live service model)

### 1.2 Inspiration & References
- **Heroes and Generals**: FPS/RTS integration, war map mechanics
- **Deathgarden**: RED vs GREEN asymmetrical feel
- **Darktide**: BLUE faction cohesion and tactics

### 1.3 Design Pillars
1. **Strategic Depth**: Meaningful RTS decisions that impact FPS battles
2. **Asymmetrical Balance**: Three distinct faction playstyles
3. **Seamless Integration**: Smooth transition between RTS and FPS
4. **Performance First**: Optimize for smooth gameplay over graphics
5. **Accessible Chaos**: Easy to learn, hard to master, always fun

---

## 2. The Three Factions (Conclaves)

### 2.1 RED Conclave: "The Destroyers"
**Philosophy**: Overwhelming force and environmental destruction

**Strengths**:
- High damage output
- Environmental destruction capabilities
- Tanky units and vehicles
- Area denial weapons

**Weaknesses**:
- Slower movement
- Resource intensive
- Less tactical flexibility
- Vulnerable to hit-and-run

**Playstyle**: Brute force, attrition warfare, hold and fortify

**Example Units**:
- Heavy assault troops
- Siege vehicles
- Flamethrower units
- Demolition specialists

**RTS Focus**: Territory control, resource accumulation, overwhelming assault

### 2.2 BLUE Conclave: "The Architects"
**Philosophy**: Teamwork, tactical equipment, and environmental creation

**Strengths**:
- Deployable structures (turrets, barricades, drones)
- Team buffs and synergies
- Defensive capabilities
- Strategic equipment (scanners, jammers)

**Weaknesses**:
- Setup time required
- Vulnerable during deployment
- Moderate individual combat power
- Equipment can be destroyed

**Playstyle**: Tactical control, zone defense, coordinated assaults

**Example Units**:
- Engineer troops
- Support specialists
- Drone operators
- Tactical coordinators

**RTS Focus**: Strategic positioning, support networks, fortification

### 2.3 GREEN Conclave: "The Hunters"
**Philosophy**: Mobility, precision, and guerrilla tactics

**Strengths**:
- High mobility (grapple hooks, parkour)
- Long-range weaponry
- Stealth capabilities
- Quick objective captures

**Weaknesses**:
- Fragile (low health/armor)
- Lower sustained DPS
- Resource dependent
- Vulnerable when caught

**Playstyle**: Hit-and-run, flanking, precision strikes

**Example Units**:
- Scout snipers
- Infiltrators
- Recon specialists
- Saboteurs

**RTS Focus**: Rapid deployment, multi-front attacks, supply line disruption

---

## 3. Core Gameplay Systems

### 3.1 The War Map (RTS Layer)

**Concept**: Persistent strategic map divided into nodes representing territories

**Node Types**:
1. **Major Cities**: Victory condition nodes (need majority to win)
2. **Resource Points**: Generate tokens/resources
3. **Strategic Points**: Provide tactical bonuses
4. **Supply Lines**: Connect nodes, enable reinforcement

**RTS Mechanics**:
- **Tokens**: Represent troops (finite resource)
- **Squadron Management**: Assign tokens to squadrons
- **Deployment**: Send squadrons to attack/defend nodes
- **Supply Lines**: Maintain connections for reinforcement
- **Battle Outcomes**: Affect map control and token availability

**Strategic Considerations**:
- Over-committing leaves other areas vulnerable
- Supply line disruption can isolate forces
- Token conservation vs. aggressive expansion
- Timing attacks with other squadrons

### 3.2 Node Battles (FPS Layer)

**Transition**: When squadrons engage, players join the FPS battle

**Token Usage**: 
- Players spawn using available tokens from their squadron
- Running out of tokens = no more spawns = potential loss
- Encourages strategic token management from RTS layer

**Game Modes** (MVP: Control Points):
- **Capture Points**: Control multiple objectives simultaneously
- Future: King of the Hill, Capture the Flag, Mixed Modes

**FPS Core Loop**:
```
Spawn → Navigate → Engage → Use Abilities → Complete Objectives → Die/Succeed → Respawn
```

**Victory Conditions**:
- Achieve objective goals
- Deplete enemy tokens
- Time-based scoring (15-minute matches)

### 3.3 Faction-Specific Mechanics

**RED**:
- Destructible environment (create new paths/sightlines)
- Heavy weapon emplacements
- Explosive area denial
- Regenerating armor

**BLUE** (MVP Focus):
- Deployable turrets and drones
- Buildable cover and structures
- Team buff zones
- Shared vision systems

**GREEN**:
- Grappling hooks and vertical mobility
- Enhanced long-range accuracy
- Cloaking/stealth mechanics
- Faster objective interaction

---

## 4. Core Gameplay Loop

### 4.1 Strategic Layer (RTS)
```
1. Assess War Map (Real-time)
   ↓
2. Allocate Tokens to Squadrons
   ↓
3. Deploy Squadrons to Nodes
   ↓
4. Monitor Active Battles
   ↓
5. Adjust Strategy Based on Outcomes
   ↓
(Return to Step 1)
```

### 4.2 Tactical Layer (FPS)
```
1. Join Node Battle
   ↓
2. Spawn with Token Cost
   ↓
3. Navigate to Objectives
   ↓
4. Engage Enemy Players
   ↓
5. Use Faction Abilities (BLUE: Deploy Turrets)
   ↓
6. Complete/Contest Objectives
   ↓
7. Die or Succeed
   ↓
8. Respawn (if tokens available)
   ↓
(Continue until Battle Ends - 15min target)
```

### 4.3 Meta Loop (Campaign/Season)
```
1. War Begins (Map Reset)
   ↓
2. Factions Compete for Territory
   ↓
3. Major Cities Change Hands
   ↓
4. One Faction Achieves Majority Control
   ↓
5. War Ends - Victory/Defeat
   ↓
6. Rankings, Rewards, Stats
   ↓
7. New War Begins
```

---

## 5. Technical Requirements

### 5.1 Technology Stack
- **Engine**: Unity 6000.2.8f1
- **Networking**: Mirror Networking
- **Render Pipeline**: Universal Render Pipeline (URP)
- **Input**: Unity Input System

### 5.2 Performance Targets
- **FPS Target**: 60+ FPS on mid-range hardware
- **Network Latency**: <100ms for responsive combat
- **Load Times**: <10s for node transitions
- **Visual Style**: Stylized/low-poly for performance
- **Player Count**: 8v8 (16 players per battle)

### 5.3 Art Direction
- **Style**: Semi-silly, crude but charming
- **Graphics**: Performance > Fidelity
- **Post-Apocalyptic Aesthetic**: Makeshift, cobbled-together look
- **Faction Visual Identity**: Clear color coding (RED/BLUE/GREEN)
- **UI**: Clean, functional, minimal visual noise

---

## 6. Development Phases

### Phase 1: Foundation ✅ COMPLETE
- Project setup
- Documentation
- Version control

### Phase 2: Prototype - Core Systems (CURRENT)
**Goal**: Prove the FPS/RTS concept works

**MVP Specifications**:
- **Networking**: Mirror (free, proven, community support)
- **War Map**: Real-time updates (5 nodes total)
- **Player Count**: 8v8 battles
- **Match Length**: 15 minutes target
- **Token Types**: Troops only (no vehicles yet)
- **Faction**: BLUE only (deployables easiest to implement)
- **Game Mode**: Control Points

**Deliverables**:
- Simple war map (5 nodes: 1 major city, 4 support)
- Basic token system
- Control Points FPS mode
- BLUE faction with deployable turret
- Network architecture foundation

**Success Criteria**:
- Players can interact with war map
- Tokens affect FPS spawns
- FPS battles feel fun
- 15-minute matches feel complete
- Technical feasibility validated

### Phase 3: Faction Diversity
**Goal**: Implement asymmetrical gameplay

**Deliverables**:
- RED faction (destruction, heavy weapons)
- GREEN faction (mobility, long-range)
- Unique abilities per faction
- Balance testing framework
- Faction-specific equipment

### Phase 4: Content & Polish
**Goal**: Full feature set and content

**Deliverables**:
- Multiple game modes (King of Hill, CTF, Mixed)
- Larger war map (15-20 nodes)
- Vehicles (tanks, transports)
- Player progression system
- Visual polish
- Sound design

### Phase 5: Beta & Launch
**Goal**: Stable release

**Deliverables**:
- Server infrastructure
- Matchmaking
- Anti-cheat
- Community features
- Marketing materials

---

## 7. Minimum Viable Product (MVP)

### 7.1 MVP Scope
To validate the core concept, the MVP includes:

**RTS Layer**:
- 5-node war map (1 major city, 4 supporting nodes)
- Basic token system (troops only)
- Simple squadron deployment
- Real-time map updates

**FPS Layer**:
- Single game mode: Control Points (3 capture points)
- One faction: BLUE (deployable turret ability)
- Basic weapons (assault rifle, pistol)
- 8v8 battles (16 players)
- 15-minute match duration

**Networking**:
- Mirror Networking integration
- Basic matchmaking (fill server)
- Server-authoritative gameplay
- Token synchronization

**Persistence**:
- Basic player accounts
- Win/loss tracking
- Simple leaderboard

**Polish**:
- Placeholder art (colored blocks/primitives)
- Basic sound effects
- Functional UI (no fancy animations)

### 7.2 MVP Exclusions (Post-MVP)
- Multiple factions (RED/GREEN come later)
- Complex game modes (King of Hill, CTF)
- Player progression/unlocks
- Vehicles
- Advanced abilities (multiple deployables)
- Mobile version
- Full war map (20+ nodes)
- Environmental destruction

---

## 8. Risk Assessment

### 8.1 Technical Risks
| Risk | Impact | Mitigation |
|------|--------|-----------|
| Networking complexity | High | Start with Mirror (proven), small player count (8v8) |
| FPS/RTS integration | High | Prototype early, keep systems modular |
| Token system exploits | Medium | Server-side validation, anti-cheat measures |
| Performance with 16 players | Medium | LOD systems, occlusion culling, performance-first art |
| State synchronization | High | Robust state management, regular testing |

### 8.2 Design Risks
| Risk | Impact | Mitigation |
|------|--------|-----------|
| Asymmetrical balance | High | Extensive playtesting, data analytics, iterative balance |
| Learning curve too steep | Medium | Tutorial system, tooltips, gradual complexity |
| RTS layer boring | Medium | Make it snappy, focus on meaningful decisions |
| FPS layer generic | Medium | Faction abilities must feel unique and impactful |
| Token scarcity frustrating | Medium | Balance spawn costs, provide feedback |
| 15-min matches too long/short | Medium | Playtesting, adjustable match timers |

### 8.3 Scope Risks
| Risk | Impact | Mitigation |
|------|--------|-----------|
| Feature creep | High | Strict MVP scope, post-MVP roadmap |
| Solo dev burnout | High | Milestone-based development, realistic timelines |
| Content demands | Medium | Procedural elements where possible, focus on replayability |
| Network costs | Medium | Community hosting, P2P options, optimize server load |

---

## 9. Success Metrics

### 9.1 Technical Success
- [ ] 60+ FPS on target hardware
- [ ] <100ms network latency
- [ ] <5% crash rate
- [ ] <10s scene load times
- [ ] 16 players stable in FPS battles

### 9.2 Gameplay Success
- [ ] Average match duration: 15 minutes (target)
- [ ] Token system creates meaningful decisions
- [ ] War map affects FPS outcomes
- [ ] Players engage with both RTS and FPS layers
- [ ] Matches feel complete (not too rushed/slow)

### 9.3 Player Success
- [ ] 70%+ tutorial completion rate
- [ ] Average session length: 60+ minutes
- [ ] 30%+ day-1 retention
- [ ] 10%+ week-1 retention
- [ ] Positive feedback on core concept

---

## 10. Answered Design Questions

### 10.1 Core Decisions ✅
- [x] **Network Solution**: Mirror Networking (free, proven, good docs)
- [x] **War Map**: Real-time updates
- [x] **Player Count**: 8v8 (16 total per battle)
- [x] **Match Length**: 15 minutes target
- [x] **Token Types**: Troops only in MVP (vehicles post-MVP)

### 10.2 Future Decisions (Post-MVP)
- [ ] Progression system design
- [ ] Monetization model (if live service)
- [ ] Seasonal content cadence
- [ ] Cross-platform priorities (console, mobile RTS)
- [ ] Community features (clans, voice chat, etc.)
- [ ] Environmental destruction implementation (RED faction)
- [ ] Vehicle types and balance

---

## 11. Next Steps

### Immediate (This Week)
1. ✅ Complete GDD
2. ⏳ Create Technical Design Document (TDD)
3. ⏳ Set up Mirror Networking in Unity project
4. ⏳ Create basic project structure (folders, namespaces)
5. ⏳ Prototype simple war map visualization

### Short Term (Next 2 Weeks)
1. Implement basic token system (backend)
2. Create war map UI (5 nodes, connections)
3. Build simple FPS scene (Control Points)
4. Test token → spawn connection
5. Implement BLUE deployable turret

### Medium Term (Next Month)
1. Functional MVP with BLUE faction
2. Basic multiplayer working (8v8)
3. First internal playtest
4. Iterate based on feedback
5. Balance token costs and match pacing

---

## 12. Design Philosophy

### "Fun First, Graphics Last"
- Prioritize responsive gameplay over visual fidelity
- Stylized art allows for creative freedom and performance
- Crude aesthetic fits post-apocalyptic makeshift theme
- Performance enables smooth 8v8 combat

### "Depth Through Simplicity"
- Each system should be easy to understand but create emergent complexity
- Faction abilities should have clear counters
- Token system creates risk/reward without complex economy
- War map should be readable at a glance

### "Player Agency"
- Players should feel their decisions matter (RTS layer)
- Players should feel their skill matters (FPS layer)
- Losses should feel recoverable, victories should feel earned
- Both casual and hardcore players can contribute

### "Iterate Rapidly"
- Build MVP quickly to test core concept
- Fail fast on bad ideas
- Let playtesting drive design
- Stay flexible on details, firm on pillars

---

## Appendix A: Terminology

- **War Map**: Strategic overview showing all nodes and their status
- **Node**: Territory/location on the war map; site of FPS battles
- **Major City**: Victory condition node; controlling majority wins war
- **Token**: Resource representing troops (vehicles in future)
- **Squadron**: Group of tokens deployed to a node
- **Conclave**: Faction in the post-apocalyptic world (RED/BLUE/GREEN)
- **Deploy**: Send squadron from one node to another (RTS action)
- **Spawn**: Use a token to enter FPS battle as a soldier
- **Control Points**: FPS game mode - capture and hold objectives
- **MVP**: Minimum Viable Product - first playable version

---

## Appendix B: Inspiration Deep Dive

### Heroes and Generals (RTS/FPS Integration)
- War map with interconnected battles
- Resource management affects tactical layer
- Players can engage at strategic OR tactical level
- Persistence across matches

**What We're Taking**: Core concept of RTS affecting FPS availability

**What We're Changing**: Asymmetrical factions, faster pace (15min), modern netcode

### Deathgarden (Asymmetry)
- Vastly different playstyles (Hunter vs Scavengers)
- Power imbalance compensated by numbers/objectives
- Unique abilities define gameplay

**What We're Taking**: Asymmetrical design philosophy, faction identity

**What We're Changing**: Three-way balance, RTS layer, team-based combat

### Darktide (Cohesion)
- Team coordination essential for success
- Support abilities enhance team effectiveness
- Clear roles within squad

**What We're Taking**: BLUE faction teamwork focus, ability synergies

**What We're Changing**: PvP focus, optional coordination (not mandatory)

---

**Document Status**: Approved v1.0  
**Decisions Finalized**: Networking (Mirror), Map (Real-time), Players (8v8), Duration (15min), Tokens (Troops)  
**Next Document**: Technical Design Document (TDD)

---

*This is a living document. Update as design evolves through prototyping and playtesting.*
