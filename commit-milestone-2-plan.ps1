# Commit Milestone 2 Planning
Write-Host "=== Committing Milestone 2 Development Plan ===" -ForegroundColor Cyan
Write-Host ""

# Stage all changes
Write-Host "Staging documentation updates..." -ForegroundColor Yellow
git add -A

# Show what's being committed
Write-Host ""
Write-Host "=== Files to commit ===" -ForegroundColor Yellow
git status --short

Write-Host ""
Write-Host "Committing..." -ForegroundColor Yellow

# Commit
git commit -m "Plan: Milestone 2 - Teams + War Map Integration

Development plan for next 3-4 weeks (25-30 hours):

Phase 1: Team Foundation (Week 1)
- Team system (Blue vs Red)
- Team spawn points
- King of the Hill gamemode
- Victory/defeat screens

Phase 2: War Map Foundation (Week 2)
- WarMap scene with 5 nodes
- Node ownership system
- Visual feedback
- Scene transitions

Phase 3: Integration (Week 3)
- Simplified deployment
- Battle initiation from map
- Battle results update map
- War victory conditions

Updated files:
- TODO.md - Complete Phase 1/2/3 task breakdown
- PROGRESS.md - Added Milestone 2 tracking
- QUICK_REFERENCE.md - Updated priorities
- MILESTONE_2_PLAN.md - Detailed development plan

Scope: Blue vs Red teams, KOTH gamemode, 5-node war map
Goal: Complete game loop working (Map → Battle → Map)
Timeline: 3-4 weeks to playable campaign"

Write-Host ""
Write-Host "=== Committed Successfully! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Milestone 2 plan documented and ready to implement!" -ForegroundColor Cyan
Write-Host ""
Write-Host "Ready to push with: " -NoNewline
Write-Host "git push" -ForegroundColor Yellow
