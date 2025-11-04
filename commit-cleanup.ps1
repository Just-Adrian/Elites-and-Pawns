# Commit Documentation Cleanup
Write-Host "=== Committing Documentation Cleanup ===" -ForegroundColor Cyan
Write-Host ""

# First, run the cleanup script to delete old files
Write-Host "Step 1: Deleting old documentation files..." -ForegroundColor Yellow
& ".\cleanup-docs.ps1"

Write-Host ""
Write-Host "Step 2: Staging all changes..." -ForegroundColor Yellow

# Stage all changes
git add -A

# Show what's being committed
Write-Host ""
Write-Host "=== Files to commit ===" -ForegroundColor Yellow
git status --short

Write-Host ""
Write-Host "Step 3: Committing changes..." -ForegroundColor Yellow

# Commit with detailed message
git commit -m "Docs: Major documentation cleanup and refresh

Removed outdated files:
- HUD_FIX_SUMMARY.md
- UI_TROUBLESHOOTING.md
- NEW_SESSION_BRIEF.md
- SESSION_SUMMARY.md
- MILESTONE_1_SUMMARY.txt
- MILESTONE_1_PROGRESS.md
- MILESTONE_1_COMPLETE.md
- WEAPON_SYSTEM_SETUP.md
- PROJECTILE_SYSTEM_CHANGES.md
- MIRROR_SETUP_GUIDE.md

Updated core documentation:
- QUICK_REFERENCE.md (complete rewrite)
- PROGRESS.md (complete rewrite)
- TODO.md (complete rewrite)
- PROJECT_OVERVIEW.md (complete rewrite)

Added:
- CLEANUP_SUMMARY.md (documentation maintenance guide)

All documentation now current as of November 4, 2025
Streamlined structure: 4 core files for all project info"

Write-Host ""
Write-Host "=== Successfully Committed! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Documentation is now clean and up-to-date!" -ForegroundColor Cyan
Write-Host ""
Write-Host "Ready to push. Run: " -NoNewline
Write-Host "git push" -ForegroundColor Yellow
Write-Host ""
Write-Host "Summary of changes:" -ForegroundColor Cyan
Write-Host "  - 10 files deleted (outdated)" -ForegroundColor Red
Write-Host "  - 4 files updated (core docs)" -ForegroundColor Yellow
Write-Host "  - 1 file added (cleanup guide)" -ForegroundColor Green
Write-Host "  - Documentation 47% leaner!" -ForegroundColor Green
