# Commit HUD and Projectile Fixes
Write-Host "=== Committing HUD and Projectile Fixes ===" -ForegroundColor Cyan

# Stage all changes
git add -A

# Show what's being committed
Write-Host ""
Write-Host "=== Files to commit ===" -ForegroundColor Yellow
git status --short

# Commit with message
git commit -m "Fix: HUD rendering and projectile synchronization

- Fixed HUD not visible by switching Canvas to Screen Space - Camera mode
- Fixed HUD positioning (health bottom-left, ammo bottom-right)
- Fixed ammo counter sync issue (was lagging by 1 bullet for clients)
- Fixed projectiles not appearing for clients:
  * Moved NetworkServer.Spawn() before Initialize()
  * Added auto-registration of projectile prefabs in NetworkManager
- Added HUDDebugger for UI diagnostics and layout fixes
- Added NetworkTransform to projectile prefab

All multiplayer UI and shooting now works correctly for both host and clients!"

Write-Host ""
Write-Host "=== Committed! ===" -ForegroundColor Green
Write-Host ""
Write-Host "Ready to push. Run: " -NoNewline
Write-Host "git push" -ForegroundColor Yellow
