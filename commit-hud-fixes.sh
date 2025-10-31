#!/bin/bash
# Commit HUD and Projectile Fixes

echo "=== Committing HUD and Projectile Fixes ==="

# Stage all changes
git add -A

# Show what's being committed
echo ""
echo "=== Files to commit ==="
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

echo ""
echo "=== Committed! ==="
echo ""
echo "Ready to push. Run: git push"
