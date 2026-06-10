# Dev notes — build & test loop

Practical workflow for this project, including how to verify changes without
clicking around the Unity UI.

## Headless compile check (fast, exactly what the editor compiles)

Replays Unity's own Roslyn invocation against the current scripts — catches every
compile error in ~1s, no editor focus needed:

```bash
U=/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app/Contents
SRC=Library/Bee/artifacts/200b0aE.dag/Assembly-CSharp.rsp   # Unity-generated
RSP=/tmp/ac_check.rsp
sed -e 's#-out:"[^"]*"#-out:"/tmp/ac_check.dll"#' \
    -e 's#-refout:"[^"]*"#-refout:"/tmp/ac_check.ref.dll"#' \
    -e '/UnityAdditionalFile/d' "$SRC" > "$RSP"
"$U/NetCoreRuntime/dotnet" "$U/DotNetSdkRoslyn/csc.dll" "@$RSP" 2>&1 | grep ': error'
```

(The `200b0aE.dag` hash is Unity's current build dir; if it changes, re-find with
`find Library/Bee -name Assembly-CSharp.rsp`.)

## MCP bridge for live editor control (Play mode, console)

The `com.coplaydev.unity-mcp` package defaults to **HTTP transport on port 8080**,
which collides with the `atlas` project's Vite dev server. Use **stdio** instead:

- `.mcp.json` is configured for stdio via `uvx` (`mcpforunityserver==9.6.8`).
- For an **interactive Claude Code session** to get the Unity tools, restart it so
  the corrected `.mcp.json` is read (MCP servers attach at startup).
- To force stdio at editor launch (so the bridge comes up regardless of the HTTP
  default):

```bash
defaults write com.unity3d.UnityEditor5.x MCPForUnity.UseHttpTransport -int 0
defaults write com.unity3d.UnityEditor5.x MCPForUnity.ResumeStdioAfterReload -int 1
open -na "/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app" --args \
  -projectPath "$(pwd)" \
  -executeMethod MCPForUnity.Editor.McpCiBoot.StartStdioForCi
```

The stdio bridge publishes `~/.unity-mcp/unity-mcp-status-*.json` once up.

## Reading runtime errors without the bridge

Entering Play mode triggers a domain reload that briefly drops the bridge, so the
most reliable way to check for runtime exceptions is the editor log directly:

```bash
grep -niE "Exception|NullReference|error CS" ~/Library/Logs/Unity/Editor.log | tail
```

The game itself is **fully procedural** — `GameManager.BuildWorld()` constructs the
world, player, camera, lights and post-FX at runtime; `Main.unity` is nearly empty.
