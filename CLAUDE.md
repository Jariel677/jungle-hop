# unity-subway-surfer-project

A Unity 6 (6000.0.75f1 LTS) 3D endless-runner game — Subway Surfers style:
the player auto-runs forward, swipes/keys to switch lanes, jump, and roll while
dodging obstacles and collecting coins. Built through a Claude ↔ Unity closed loop.

## The closed loop

Claude Code is wired to the running Unity Editor via the **MCP for Unity** bridge
(`com.coplaydev.unity-mcp`). The MCP server is configured in `.mcp.json` (stdio transport).

**For the loop to work, the Unity Editor must be open with this project.** The bridge
runs inside the Editor; when the Editor is closed, the MCP tools cannot reach Unity.

Iteration cycle for adding a game feature:

1. **Change** — edit/create C# scripts and scenes (`manage_script`, `apply_text_edits`,
   `manage_scene`, `manage_gameobject`, or normal file edits under `Assets/`).
2. **Compile** — call `refresh_unity` so the Editor recompiles.
3. **Inspect** — call `read_console` to catch compile errors / runtime logs.
4. **Test** — call `manage_editor` to enter Play mode; `run_tests` for Edit/Play mode tests.
5. **Fix & repeat** — read console, correct, refresh, re-test.

## Key MCP tools

- `manage_script` / `apply_text_edits` / `create_script` — C# scripts
- `manage_scene` / `manage_gameobject` / `manage_components` — scene graph
- `manage_asset` / `manage_material` / `manage_prefabs` — assets
- `read_console` — Editor console (errors, warnings, logs)
- `refresh_unity` — trigger recompile / asset refresh
- `manage_editor` — Play/pause/stop, editor state
- `run_tests` — run Edit/Play mode tests
- `manage_packages` — add/remove Unity packages

Use `batch_execute` to group multiple operations (10–100x faster).

## Project layout

- `Assets/Scripts/` — gameplay C# (player controller, spawner, score, etc.)
- `Assets/Scenes/` — scenes (`Main.unity` is the gameplay scene)
- `Assets/Prefabs/` — player, obstacles, coins, track segments
- `Packages/manifest.json` — package dependencies (includes the MCP bridge)
- `ProjectSettings/` — Unity project settings
- `Library/`, `Temp/`, `Logs/`, `obj/` — generated, git-ignored

## Game milestones (build in this order)

1. Track: looping/recycling ground segments scrolling toward the player.
2. Player: forward auto-run, 3-lane switching, jump, roll/slide.
3. Obstacles: spawn on lanes; collision ends the run.
4. Coins: spawn, collect, score counter.
5. Speed ramp, UI (score / game-over / restart), audio, polish.

## Conventions

- C# scripts under `Assets/Scripts/`, scenes under `Assets/Scenes/`.
- Render pipeline: Built-in (default). Switch via `manage_packages` if URP is needed.
- After any script change, always `refresh_unity` then `read_console` before claiming success.
