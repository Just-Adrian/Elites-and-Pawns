# Documentation Cleanup Script
Write-Host "=== Cleaning Up Redundant Documentation ===" -ForegroundColor Cyan

$filesToDelete = @(
    "HUD_FIX_SUMMARY.md",
    "UI_TROUBLESHOOTING.md", 
    "NEW_SESSION_BRIEF.md",
    "SESSION_SUMMARY.md",
    "MILESTONE_1_SUMMARY.txt",
    "MILESTONE_1_PROGRESS.md",
    "MILESTONE_1_COMPLETE.md",
    "WEAPON_SYSTEM_SETUP.md",
    "PROJECTILE_SYSTEM_CHANGES.md",
    "MIRROR_SETUP_GUIDE.md"
)

foreach ($file in $filesToDelete) {
    if (Test-Path $file) {
        Remove-Item $file -Force
        Write-Host "✓ Deleted: $file" -ForegroundColor Green
    } else {
        Write-Host "- Not found: $file" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "=== Cleanup Complete! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Updated documentation files will be created next..." -ForegroundColor Cyan
