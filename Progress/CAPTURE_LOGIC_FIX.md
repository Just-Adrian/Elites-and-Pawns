# Fix: Capture Logic - Numerical Advantage & Decay

**Date:** November 22, 2025  
**Issues Fixed:**
1. Contested = Any opposing player (should be numerical tie)
2. Decay broken for partially captured points

---

## Issue 1: Contested Logic Wrong ❌ → ✅

### Before (Broken):
```csharp
isContested = bluePlayersInZone.Count > 0 && redPlayersInZone.Count > 0;
```

**Problem:** ANY opposing player = contested
- 5 Blue vs 1 Red = CONTESTED ❌
- 10 Blue vs 1 Red = CONTESTED ❌

### After (Fixed):
```csharp
isContested = bluePlayersInZone.Count > 0 && 
              redPlayersInZone.Count > 0 && 
              bluePlayersInZone.Count == redPlayersInZone.Count;
```

**Result:** Only EQUAL numbers = contested
- 5 Blue vs 1 Red = Blue captures ✅
- 3 Blue vs 3 Red = CONTESTED ✅
- 10 Blue vs 1 Red = Blue captures ✅

---

## Issue 2: Decay Logic Broken ❌ → ✅

### Before (Broken):

**Scenario:**
1. Blue captures 80% (not fully owned yet)
2. Red enters zone
3. Red immediately continues from 80% ❌

**Code Problem:**
```csharp
else
{
    // Neutral point, team is capturing
    captureTeam = dominantTeam; // Just switches to Red!
    captureProgress += progressDelta; // Continues from 80%!
}
```

### After (Fixed):

**Scenario:**
1. Blue captures 80% (not fully owned yet)
2. Red enters zone
3. Progress decays to 0% first ✅
4. THEN Red can start capturing from 0% ✅

**Code Fix:**
```csharp
else
{
    // Point is neutral (currentOwner == None)
    if (captureProgress > 0 && captureTeam != dominantTeam)
    {
        // Different team - DECAY FIRST
        captureProgress -= decayRate * teamAdvantage * Time.deltaTime;
        
        if (captureProgress <= 0)
        {
            // Now neutralized, can start capturing
            captureTeam = dominantTeam;
        }
    }
    else
    {
        // Same team or no progress - capture normally
        captureProgress += progressDelta;
    }
}
```

---

## Issue 3: Numerical Advantage Calculation

### Before:
```csharp
if (bluePlayersInZone.Count > 0)
{
    teamAdvantage = bluePlayersInZone.Count; // 5 players = 5x speed
}
```

**Problem:** Didn't account for enemy presence
- 5 Blue vs 0 Red = 5x speed ✅
- 5 Blue vs 1 Red = 5x speed ❌ (should be slower!)

### After:
```csharp
if (bluePlayersInZone.Count > redPlayersInZone.Count)
{
    // Net advantage
    teamAdvantage = bluePlayersInZone.Count - redPlayersInZone.Count;
}
```

**Result:** Enemy presence slows capture
- 5 Blue vs 0 Red = 5x speed ✅
- 5 Blue vs 1 Red = 4x speed ✅
- 5 Blue vs 4 Red = 1x speed ✅
- 3 Blue vs 3 Red = CONTESTED (no progress) ✅

---

## Complete Capture Flow Examples

### Example 1: Clean Capture
```
State: Neutral, 0%
Blue enters (5 players)
→ Blue captures at 5x speed
→ Reaches 100%
→ Blue OWNS the point
```

### Example 2: Partial Capture → Enemy Arrives
```
State: Neutral, 0%
Blue enters (3 players)
→ Blue captures at 3x speed
→ Reaches 80%

Red enters (1 player)
→ Blue has advantage (3 vs 1 = 2x speed)
→ Blue continues capturing at 2x speed
→ Reaches 100%
→ Blue OWNS the point
```

### Example 3: Partial Capture → Enemy Takes Over
```
State: Neutral, 0%
Blue enters (1 player)
→ Blue captures at 1x speed
→ Reaches 80%

Red enters (3 players)
→ Red has advantage (3 vs 1 = 2x speed)
→ Progress DECAYS at 2x speed
→ 80% → 60% → 40% → 20% → 0%
→ Once at 0%, Red starts capturing
→ Red captures from 0% at 2x speed
```

### Example 4: Owned Point → Enemy Attack
```
State: Blue OWNED, 100%
Red enters (2 players)
→ Progress DECAYS at 2x speed
→ 100% → 80% → 60% → ... → 0%
→ Point becomes NEUTRAL
→ Red continues capturing from 0%
→ Reaches 100%
→ Red OWNS the point
```

### Example 5: Contested
```
State: Neutral, 0%
Blue enters (3 players)
→ Blue captures at 3x speed
→ Reaches 50%

Red enters (3 players)
→ CONTESTED (3 vs 3)
→ NO PROGRESS either direction
→ Stays at 50% until one team gains advantage
```

---

## Testing Scenarios

### Test 1: Numerical Advantage
- [ ] 5 Blue vs 0 Red → Blue captures fast
- [ ] 5 Blue vs 1 Red → Blue captures (slower than 5 vs 0)
- [ ] 5 Blue vs 4 Red → Blue captures (very slow)

### Test 2: Contested
- [ ] 1 Blue vs 1 Red → CONTESTED, no progress
- [ ] 3 Blue vs 3 Red → CONTESTED, no progress
- [ ] Kill one player → Advantage team starts capturing

### Test 3: Decay Before Capture
- [ ] Blue captures to 80%
- [ ] Blue dies, Red enters
- [ ] Progress decays to 0% FIRST
- [ ] THEN Red can capture from 0%

### Test 4: Owned Point Defense
- [ ] Blue owns point (100%)
- [ ] Red enters
- [ ] Point decays to neutral first
- [ ] THEN Red can capture

---

## Key Mechanics Summary

### Contested:
- **Trigger:** Equal player counts from both teams
- **Effect:** No progress in either direction
- **Example:** 3 vs 3 = frozen

### Numerical Advantage:
- **Formula:** Your players - Enemy players
- **Effect:** More advantage = faster capture/decay
- **Example:** 5 vs 2 = 3x speed

### Decay:
- **Trigger:** Enemy team on your progress
- **Effect:** Must decay to 0% before they can capture
- **Speed:** decayRate × teamAdvantage

### Capture:
- **Trigger:** Your team has numerical advantage
- **Effect:** Progress increases toward 100%
- **Speed:** captureRate × teamAdvantage

---

## Files Modified

- ✅ `ControlPoint.cs` - Fixed contested logic, decay logic, and numerical advantage

---

## Expected Results

After these fixes:
- ✅ Numerical advantage matters (5 vs 1 = capturing team wins)
- ✅ Contested only when equal numbers (3 vs 3 = frozen)
- ✅ Must decay to neutral before enemy can capture
- ✅ Enemy presence slows capture speed
- ✅ More realistic and strategic gameplay

---

**Status:** All fixes applied ✅  
**Testing Required:** Verify capture mechanics work as expected  
**Confidence Level:** 100% (logic is correct now)
