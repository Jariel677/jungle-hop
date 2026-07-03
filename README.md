# Jungle Hop

A lane-based 3D endless runner built with **Unity 6 (6000.0.75f1 LTS)**. The player
auto-runs forward, switching between lanes to jump over and slide under obstacles
while collecting coins and chasing a high score.

The entire game is **procedurally constructed at runtime** — no hand-authored scene.
`GameManager.BuildWorld()` assembles the world, player, camera, lighting, and
post-processing on launch, so the project stays lightweight and fully reproducible.

## Features

- Continuous forward auto-run with three-lane switching
- Jump and slide/roll to clear obstacles
- Procedurally spawned obstacles and collectible coins
- Progressive speed ramp for increasing difficulty
- Score tracking with game-over and restart flow
- Self-contained: assets and world are generated in code, not stored in the repo

## Tech stack

| | |
|---|---|
| Engine | Unity 6 (`6000.0.75f1` LTS) |
| Language | C# |
| Render pipeline | Built-in |
| Architecture | Fully procedural world generation at runtime |

## Getting started

**Prerequisites:** Unity Hub with Unity **6000.0.75f1** installed.

1. Clone the repository:
   ```bash
   git clone https://github.com/Jariel677/jungle-hop.git
   ```
2. Open the project folder in Unity Hub (it will match the exact editor version).
3. Open `Assets/Scenes/Main.unity` and press **Play**. The world builds itself on
   entering Play mode.

### Controls

| Action | Input |
|---|---|
| Switch lane | Left / Right (arrow keys) |
| Jump | Up / Space |
| Slide | Down |

## Project structure

```
Assets/
  Scripts/     Gameplay C# (game manager, player controller, spawner, score)
  Scenes/      Main.unity (near-empty; world is built at runtime)
  Prefabs/     Player, obstacles, coins, track segments
Packages/      Package manifest and dependencies
ProjectSettings/  Unity project configuration
```

Generated folders (`Library/`, `Temp/`, `Logs/`, `obj/`) are git-ignored.

## Development

This project is developed through a **Claude Code ↔ Unity Editor** loop via the
[MCP for Unity](https://github.com/CoplayDev/unity-mcp) bridge (`com.coplaydev.unity-mcp`),
configured for stdio transport in `.mcp.json`. With the Unity Editor open on the
project, changes are applied to scripts and scenes, recompiled, inspected through
the Editor console, and exercised in Play mode — all from the CLI.

For a fast, editor-independent compile check, see [`DEV_NOTES.md`](DEV_NOTES.md),
which documents a headless Roslyn compile pass and how to read runtime logs.

## Roadmap

- [x] Recycling track segments scrolling toward the player
- [x] Player auto-run, lane switching, jump, slide
- [x] Obstacle spawning and collision → game over
- [x] Coin spawning, collection, and score
- [ ] Speed ramp, UI polish, audio, and visual effects

## License

No license has been declared yet. All rights reserved by the author until a license
is added.
