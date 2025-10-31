# Quick Git Commit Script
# Run this in PowerShell after reviewing changes

Write-Host "Adding files to git..." -ForegroundColor Cyan
git add Assets/_Project/
git add GDD.md
git add TDD.md
git add MILESTONE_1_PROGRESS.md

Write-Host "`nCommitting Milestone 1 foundation..." -ForegroundColor Cyan
git commit -m "feat: Milestone 1 foundation - Core systems and project structure

- Created project folder structure (_Project/Scripts/Core, Networking, Player)
- Implemented core game enums (FactionType, GameState, NodeType, GameMode)
- Created reusable Singleton pattern for managers
- Implemented GameManager with state management and scene transitions
- Created NetworkTest scene with ground plane
- Added comprehensive GDD and TDD documentation

Ready for Mirror networking integration.

Refs: Milestone 1 - Network Foundation"

Write-Host "`nGit status:" -ForegroundColor Cyan
git status

Write-Host "`nRecent commits:" -ForegroundColor Cyan
git log --oneline -3

Write-Host "`n✅ Commit complete!" -ForegroundColor Green
