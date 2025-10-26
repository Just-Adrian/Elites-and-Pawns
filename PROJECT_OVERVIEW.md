# Elites and Pawns True - Project Overview

## Project Information
- **Project Name**: Elites and Pawns True
- **Unity Version**: 6000.2.8f1 (Unity 6)
- **Render Pipeline**: Universal Render Pipeline (URP) v17.2.0
- **Template**: URP Blank Template
- **Organization**: bitrixer

## Current Status
This is a fresh Unity 6 project using the URP Blank template with Vibe Unity integration for automated development workflows.

## Technology Stack

### Core Unity Packages
- **Unity Input System** (v1.14.2) - Modern input handling
- **Universal Render Pipeline** (v17.2.0) - Graphics rendering
- **AI Navigation** (v2.0.9) - Pathfinding and navigation
- **Visual Scripting** (v1.9.7) - Node-based scripting
- **Timeline** (v1.8.9) - Cutscenes and animations
- **UGUI** (v2.0.0) - UI system
- **Test Framework** (v1.6.0) - Unit testing
- **Multiplayer Center** (v1.0.0) - Multiplayer setup tools

### Development Tools
- **Vibe Unity** - Claude-Code integration for automated scene creation
- **Rider IDE** (v3.0.38) - Primary IDE support
- **Visual Studio** (v2.0.23) - Secondary IDE support

## Project Structure

```
Elites and Pawns True/
├── Assets/
│   ├── Scenes/
│   │   └── SampleScene.unity - Default sample scene
│   ├── Settings/ - Project settings assets
│   └── InputSystem_Actions.inputactions - Input action definitions
├── Packages/
│   ├── manifest.json - Package dependencies
│   └── com.ricoder.vibe-unity/ - Vibe Unity automation package
├── ProjectSettings/ - Unity project configuration
├── .vibe-unity/ - Vibe Unity automation files
│   └── commands/ - JSON command files for scene automation
├── claude-compile-check.sh - Compilation validation script
└── CLAUDE.md - AI development instructions

```

## Development Workflow

### Vibe Unity Integration
The project uses Vibe Unity for automated Unity development through Claude-Code:

1. **Compilation Validation**
   - Script: `./claude-compile-check.sh`
   - Validates C# code compiles before proceeding
   - Exit codes: 0=success, 1=errors, 2=timeout

2. **Scene Creation**
   - JSON commands in `.vibe-unity/commands/`
   - Automatic processing by file watcher
   - Results logged in `.vibe-unity/commands/logs/latest.log`

3. **Supported Components** (v2.0.0)
   - ✅ UI: Canvas, Button, Text, Image, ScrollView, TextMeshPro
   - ✅ 3D: Cube, Sphere, Plane, Cylinder, Capsule, Camera, Light
   - ⚠️ Partial: Rigidbody, Colliders
   - ❌ Missing: ParticleSystem, custom scripts, animations

## Git Repository
- **Branch**: master
- **Initial Commit**: Project initialization with Vibe Unity integration
- **.gitignore**: Standard Unity exclusions (Library, Temp, Obj, Builds, Logs)

## Platform Targets
Currently configured for:
- **Primary**: Standalone (PC)
- **Mobile**: Android (IL2CPP backend)
- **Graphics APIs**: 
  - Android: Vulkan, OpenGL ES 3.0
  - iOS: Metal

## Next Steps / TODO
- [ ] Define game concept and mechanics
- [ ] Create initial gameplay scenes
- [ ] Develop core gameplay systems
- [ ] Implement player character controller
- [ ] Set up UI/UX framework
- [ ] Create art pipeline and style guide
- [ ] Establish testing procedures

## Development Guidelines
1. Always run compilation check after C# modifications
2. Use JSON commands for scene automation
3. Verify changes in Unity Editor before committing
4. Follow naming conventions for assets and scripts
5. Document major architectural decisions
6. Keep commits atomic and well-described

---
*Last Updated: October 26, 2025*
*Senior Developer: Claude (AI Assistant)*
*Project Manager: Adrian*
