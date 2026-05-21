using UnityEditor;

namespace UnityGameSetup
{
    /// <summary>
    /// Keeps the Claude &lt;-&gt; Unity closed loop wired up.
    ///
    /// Sets the "MCP for Unity" EditorPrefs so the package's own HTTP auto-start
    /// handler brings the bridge up on every Editor launch — no manual UI steps.
    /// Idempotent and safe to keep in the project.
    /// </summary>
    [InitializeOnLoad]
    public static class MCPBootstrap
    {
        // Unity is launched by launchd and does not inherit the user shell PATH,
        // so the MCP package needs the explicit uvx location to start the server.
        private const string UvxPath = "/Users/waynenolettegmail.com/.local/bin/uvx";

        static MCPBootstrap()
        {
            // Persisted EditorPrefs — these make the HTTP bridge auto-start every launch.
            EditorPrefs.SetBool("MCPForUnity.UseHttpTransport", true);
            EditorPrefs.SetBool("MCPForUnity.AutoStartOnLoad", true);
            if (System.IO.File.Exists(UvxPath))
                EditorPrefs.SetString("MCPForUnity.UvxPath", UvxPath);
        }
    }
}
