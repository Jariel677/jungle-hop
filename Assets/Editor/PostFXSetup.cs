using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Editor bootstrap: locates the post-processing package's PostProcessResources
/// asset and saves a reference to it in <c>Assets/Resources</c> so the runtime
/// camera setup can load it.
/// </summary>
[InitializeOnLoad]
public static class PostFXSetup
{
    const string HolderPath = "Assets/Resources/PPResourcesHolder.asset";

    static PostFXSetup()
    {
        EditorApplication.delayCall += Ensure;
    }

    static void Ensure()
    {
        if (File.Exists(HolderPath)) return;

        string[] guids = AssetDatabase.FindAssets("t:PostProcessResources");
        if (guids.Length == 0) return;

        PostProcessResources res = AssetDatabase.LoadAssetAtPath<PostProcessResources>(
            AssetDatabase.GUIDToAssetPath(guids[0]));
        if (res == null) return;

        if (!Directory.Exists("Assets/Resources")) Directory.CreateDirectory("Assets/Resources");

        PPResourcesHolder holder = ScriptableObject.CreateInstance<PPResourcesHolder>();
        holder.resources = res;
        AssetDatabase.CreateAsset(holder, HolderPath);
        AssetDatabase.SaveAssets();
        Debug.Log("[PostFXSetup] PostProcessResources holder created.");
    }
}
