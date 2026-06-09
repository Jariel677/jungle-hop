using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

/// <summary>
/// Runtime-loadable holder for the post-processing package's
/// <see cref="PostProcessResources"/> asset (which lives inside the package and
/// can't otherwise be reached at runtime). Populated by the editor script
/// <c>PostFXSetup</c> and loaded from Resources by <c>GameManager</c>.
/// </summary>
public class PPResourcesHolder : ScriptableObject
{
    public PostProcessResources resources;
}
