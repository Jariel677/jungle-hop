# Unity + Claude Code — Game Dev Setup Guide

Hand this file to **Claude Code** and it will install Unity, the AI bridge, and
everything needed to build games through a tight **Claude ↔ Unity closed loop**
(Claude writes C#, compiles, reads the console, enters Play mode, and screenshots
the result — all on its own).

This was written from a real, verified end-to-end setup on **macOS (Apple Silicon)**.

---

## How to use this file

**If you are a human:**

1. Install **Claude Code** (the CLI, the desktop app, or the VS Code / JetBrains
   extension) — <https://docs.anthropic.com/en/docs/claude-code>.
2. Create an empty folder for your game, e.g. `~/Documents/GitHub/my-game`.
3. Open Claude Code **in that folder** and copy this entire file into the chat with:
   > "Follow this setup guide exactly. Do every step. Stop and tell me only when
   > you need me to sign in or create an account."
4. When Claude asks you to sign into Unity (Step 4), do it — it takes ~1 minute.
5. When it finishes, you'll have a working Unity project you can build games in
   by talking to Claude.

**If you are the AI agent:** execute the steps below in order. Steps marked
**🧑 HUMAN** require the user; pause and ask them. Everything else is yours to do.
Use absolute paths everywhere. Read the **Gotchas** section before you start —
they will save you.

---

## What you end up with

- **Unity Hub** + **Unity 6 LTS** Editor installed and licensed.
- A Unity project wired to the **MCP for Unity** bridge (`com.coplaydev.unity-mcp`).
- An HTTP MCP server that **auto-starts** every time the Editor opens.
- `.mcp.json` so any Claude Code session in the project can drive Unity.
- A verified closed loop: edit C# → `refresh_unity` → `read_console` →
  `manage_editor` (Play) → `manage_camera` (screenshot).

---

## Assumptions

- **macOS on Apple Silicon** (M1/M2/M3/M4). Notes for Intel Macs are inline.
- Internet access; ~10 GB free disk.
- Tools used: `curl`, `hdiutil`, `git`. Homebrew is **not** required.

---

## Step 0 — Environment recon

Check what already exists so you don't reinstall:

```bash
echo "node:";   which node && node --version
echo "python3:"; which python3 && python3 --version
echo "git:";    git --version
echo "uv:";     (which uv && uv --version) || echo "uv NOT installed"
echo "Unity Hub:"; ls -d "/Applications/Unity Hub.app" 2>/dev/null || echo "NOT installed"
echo "Editors:"; ls "/Applications/Unity/Hub/Editor" 2>/dev/null || echo "none"
```

---

## Step 1 — Install `uv` (Python runtime for the MCP server)

The MCP server runs via `uvx`. Install `uv` (no admin needed):

```bash
curl -LsSf https://astral.sh/uv/install.sh | sh
"$HOME/.local/bin/uv" --version    # verify — installs to ~/.local/bin
```

`uvx` will be at `$HOME/.local/bin/uvx`. **Remember this absolute path** — you need
it in Step 7.

---

## Step 2 — Install Unity Hub

Download the **architecture-correct** dmg, mount it, copy the app, clear quarantine:

```bash
# Apple Silicon:
URL="https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup-arm64.dmg"
# Intel Mac: use https://public-cdn.cloud.unity3d.com/hub/prod/UnityHubSetup-x64.dmg
# If a URL 404s, get the current link from https://unity.com/download

curl -L --fail -o /tmp/UnityHub.dmg "$URL"
hdiutil attach /tmp/UnityHub.dmg -nobrowse -quiet
VOL=$(ls -d "/Volumes/Unity Hub"* | head -1)
rm -rf "/Applications/Unity Hub.app"
cp -R "$VOL/Unity Hub.app" /Applications/
hdiutil detach "$VOL" -quiet
xattr -dr com.apple.quarantine "/Applications/Unity Hub.app"
```

---

## Step 3 — Install the Unity Editor (6 LTS)

> **🔑 GOTCHA — `ELECTRON_RUN_AS_NODE`:** Unity Hub is an Electron app. IDE/agent
> shells often export `ELECTRON_RUN_AS_NODE=1`, which makes the Hub crash with
> `Cannot find module '--headless'`. **Always** strip it with `env -u`.

Define a helper, then list and install the editor:

```bash
HUB="/Applications/Unity Hub.app/Contents/MacOS/Unity Hub"
hub() { env -u ELECTRON_RUN_AS_NODE -u ELECTRON_NO_ATTACH_CONSOLE "$HUB" -- --headless "$@"; }

hub editors -r -a arm64            # list available releases (use x86_64 on Intel)
```

Pick the newest **`6000.0.xx` LTS** (Unity 6.0 Long-Term Support). Install it
(this is a multi-GB download — run it in the background and wait for it):

```bash
hub install --version 6000.0.75f1 -a arm64    # substitute the version you saw
```

The Editor lands at `/Applications/Unity/Hub/Editor/<version>/Unity.app`.

> **🔑 GOTCHA — Rosetta 2:** The macOS Unity Editor needs Rosetta 2 **even on
> Apple Silicon**. Install it or `-createProject` silently fails:
>
> ```bash
> softwareupdate --install-rosetta --agree-to-license
> ```

---

## Step 4 — 🧑 HUMAN: activate a Unity license

Unity won't open a project without a license. **Ask the user to do this:**

1. Run `open -a "Unity Hub"`.
2. In Unity Hub: click the **account icon** (top-left) → **Sign in** (create a
   free Unity ID at <https://id.unity.com> if needed).
3. **Settings (gear) → Licenses → Add → Get a free personal license.**
4. Confirm a **Unity Personal** license now appears.

A free Personal license is fine for most projects. Wait for the user to confirm
before continuing.

---

## Step 5 — Create the Unity project

```bash
UNITY="/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app/Contents/MacOS/Unity"
PROJ="$HOME/Documents/GitHub/my-game"     # absolute path; see TCC gotcha below

"$UNITY" -batchmode -createProject "$PROJ" -quit -logFile /tmp/unity-create.log
ls "$PROJ"     # expect: Assets/ Library/ Packages/ ProjectSettings/
```

> **🔑 GOTCHA — macOS `~/Documents` privacy (TCC):** macOS sandboxes `~/Documents`.
> An agent can usually read/write **specific files** it created there, but
> `ls`/`git` on folders it didn't create get `Operation not permitted`. Safest:
> keep the project where the agent's own working directory is, and **run Claude
> Code from inside the project folder** (it gets full access to its workspace).

---

## Step 6 — Install the MCP for Unity bridge

Add the bridge package to `Packages/manifest.json` — insert this line as the
first entry inside `"dependencies"`:

```json
"com.coplaydev.unity-mcp": "https://github.com/CoplayDev/unity-mcp.git?path=/MCPForUnity#main",
```

Resolve & compile it once (downloads the package):

```bash
"$UNITY" -batchmode -projectPath "$PROJ" -quit -logFile /tmp/unity-resolve.log
grep -i "coplaydev" "$PROJ/Packages/packages-lock.json"   # confirm it resolved
```

---

## Step 7 — Auto-start the bridge (`MCPBootstrap.cs`)

The bridge runs in HTTP mode and auto-starts on every Editor launch — driven by
three EditorPrefs. Create **`Assets/Editor/MCPBootstrap.cs`** with the contents
from **Appendix A**. It sets:

- `MCPForUnity.UseHttpTransport = true`
- `MCPForUnity.AutoStartOnLoad = true`
- `MCPForUnity.UvxPath = <$HOME>/.local/bin/uvx`

> **🔑 GOTCHA — `uvx` not on PATH:** Unity is launched by `launchd` and does
> **not** inherit your shell PATH, so it can't find `uvx`. The `UvxPath` pref
> (set by `MCPBootstrap.cs`) gives it the absolute location. The Appendix A
> script derives `$HOME` automatically, so it works for any user.

---

## Step 8 — Configure Claude Code (`.mcp.json`)

Create **`.mcp.json`** in the **project root** so any Claude Code session opened
there can reach Unity:

```json
{
  "mcpServers": {
    "unityMCP": {
      "type": "http",
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

---

## Step 9 — Launch and verify the closed loop

Open the Editor (use `open` so it launches detached, with a clean environment):

```bash
open -na "/Applications/Unity/Hub/Editor/6000.0.75f1/Unity.app" --args -projectPath "$PROJ"
```

Wait ~60–90 s for first import + compile. Then verify the bridge is up:

```bash
# HTTP MCP server should be listening:
nc -z 127.0.0.1 8080 && echo "MCP server UP on :8080" || echo "not up yet — wait"
# A plain GET returning HTTP 406 is CORRECT (the MCP endpoint rejects non-MCP GETs):
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:8080/mcp
```

Confirm in the Editor log that Unity registered with the server:

```bash
grep -a "Plugin registered\|Bridge started" \
  "$HOME/Library/Application Support/UnityMCP/Logs/unity_mcp_server.log"
```

**Final verification** — from a Claude Code session opened in the project folder
(it will pick up `.mcp.json` and ask you to approve the `unityMCP` server),
ask Claude to call `read_console`. If it returns Unity console entries, the
loop works.

> First launch may log transient `[WebSocket] ... not initialised` warnings
> while the domain reloads — those are benign.

---

## Gotchas — quick reference

| # | Gotcha | Fix |
|---|--------|-----|
| 1 | Unity Hub CLI crashes: `Cannot find module '--headless'` | `env -u ELECTRON_RUN_AS_NODE` before the Hub binary |
| 2 | `-createProject` silently fails on Apple Silicon | `softwareupdate --install-rosetta --agree-to-license` |
| 3 | `Operation not permitted` on `~/Documents` folders | Run Claude Code **inside** the project folder; use absolute paths |
| 4 | Unity can't start the MCP server (`uvx` not found) | Set `MCPForUnity.UvxPath` EditorPref (Appendix A does this) |
| 5 | MCP tools return "No Unity Editor instances found" | The **Editor must be open** with the project — the bridge dies when it closes |
| 6 | Unity Hub dmg URL 404s | Use the `-arm64` / `-x64` variant, or grab the link from unity.com/download |
| 7 | `GET /mcp` returns HTTP 406 | That's correct — the MCP endpoint only accepts MCP POSTs |

---

## The build workflow — how to actually make games

Once set up, **keep the Unity Editor open** and open Claude Code **in the project
folder**. Approve the `unityMCP` server when prompted. Then iterate:

1. **Change** — write/edit C# under `Assets/` (normal file edits, or the
   `manage_script` / `create_script` MCP tools).
2. **Compile** — `refresh_unity`.
3. **Inspect** — `read_console` (catch compile + runtime errors).
4. **Run** — `manage_editor` action `play` / `stop`.
5. **See it** — `manage_camera` action `screenshot`.
6. **Fix & repeat.**

**Useful MCP tools:** `manage_script`, `apply_text_edits`, `create_script`,
`manage_scene`, `manage_gameobject`, `manage_components`, `manage_asset`,
`manage_material`, `manage_prefabs`, `manage_packages`, `read_console`,
`refresh_unity`, `manage_editor`, `manage_camera`, `run_tests`. Use
`batch_execute` to group operations (much faster).

**A reliable project pattern:** keep a single bootstrap scene with one
`GameManager` GameObject; have `GameManager.Awake()` build the rest of the
world from code. This minimizes fragile scene-file editing — Claude controls
everything through C#.

---

## Appendix A — `Assets/Editor/MCPBootstrap.cs`

```csharp
using UnityEditor;

namespace UnityGameSetup
{
    /// <summary>
    /// Keeps the Claude <-> Unity bridge wired up: sets the MCP for Unity
    /// EditorPrefs so the HTTP bridge auto-starts on every Editor launch.
    /// Idempotent and safe to keep in the project.
    /// </summary>
    [InitializeOnLoad]
    public static class MCPBootstrap
    {
        static MCPBootstrap()
        {
            EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
            EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);

            // Unity (launched by launchd) doesn't inherit the shell PATH, so the
            // MCP package needs the absolute uvx location. Derived from $HOME.
            string uvx = System.IO.Path.Combine(
                System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
                ".local", "bin", "uvx");
            if (System.IO.File.Exists(uvx))
                EditorPrefs.SetString("MCPForUnity.UvxPath", uvx);
        }
    }
}
```

## Appendix B — `.mcp.json` (project root)

```json
{
  "mcpServers": {
    "unityMCP": {
      "type": "http",
      "url": "http://localhost:8080/mcp"
    }
  }
}
```

## Appendix C — optional: drive Unity from a plain script

A Claude Code session in the project gets the MCP tools natively via `.mcp.json`.
If you ever need to call the bridge from a standalone script (e.g. from another
folder), this minimal client works — `python3 mcp.py <tool> '<json-args>'`:

```python
#!/usr/bin/env python3
"""Minimal MCP HTTP client for the Unity bridge."""
import json, sys, urllib.request, base64

URL = "http://127.0.0.1:8080/mcp"


class Client:
    def __init__(self):
        self.sid = None
        self.rid = 0

    def _post(self, body, timeout):
        headers = {"Content-Type": "application/json",
                   "Accept": "application/json, text/event-stream",
                   "MCP-Protocol-Version": "2025-06-18"}
        if self.sid:
            headers["Mcp-Session-Id"] = self.sid
        req = urllib.request.Request(URL, data=json.dumps(body).encode(),
                                     headers=headers, method="POST")
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            sid = resp.headers.get("Mcp-Session-Id")
            if sid:
                self.sid = sid
            ctype = resp.headers.get("Content-Type", "")
            raw = resp.read().decode("utf-8", "replace")
        return ctype, raw

    def rpc(self, method, params=None, notify=False, timeout=240):
        body = {"jsonrpc": "2.0", "method": method}
        if not notify:
            self.rid += 1
            body["id"] = self.rid
        if params is not None:
            body["params"] = params
        ctype, raw = self._post(body, timeout)
        if notify:
            return None
        if "text/event-stream" in ctype:
            for line in raw.splitlines():
                if line.startswith("data:"):
                    try:
                        obj = json.loads(line[5:].strip())
                    except json.JSONDecodeError:
                        continue
                    if obj.get("id") == self.rid:
                        return obj
            return None
        return json.loads(raw)

    def handshake(self):
        self.rpc("initialize", {"protocolVersion": "2025-06-18", "capabilities": {},
                                "clientInfo": {"name": "cli", "version": "1"}})
        self.rpc("notifications/initialized", notify=True)


def main():
    tool = sys.argv[1]
    args = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
    c = Client()
    c.handshake()
    r = c.rpc("tools/call", {"name": tool, "arguments": args})
    res = (r or {}).get("result", {})
    for block in res.get("content", []):
        if block.get("type") == "text":
            print(block.get("text", ""))
        elif block.get("type") == "image":
            with open("/tmp/unity_shot.png", "wb") as f:
                f.write(base64.b64decode(block.get("data", "")))
            print("[image saved: /tmp/unity_shot.png]")


if __name__ == "__main__":
    main()
```

---

## Appendix D — versions verified

| Component | Version |
|-----------|---------|
| macOS | 14+ (Apple Silicon) |
| Unity Hub | 3.18 |
| Unity Editor | 6000.0.75f1 (Unity 6.0 LTS) |
| uv / uvx | 0.11+ |
| MCP bridge | `com.coplaydev.unity-mcp` (`#main`) |
| MCP server | `mcpforunityserver` (via `uvx`) |

Newer versions generally work — when in doubt, use the latest Unity 6.0 LTS and
the bridge's `#main` branch.

---

*Generated from a verified end-to-end Claude Code + Unity setup.*
