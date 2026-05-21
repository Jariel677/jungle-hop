using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

/// <summary>
/// One-click iOS build configuration. Run <c>Tools ▸ Subway Runner ▸ Configure
/// iOS</c> once, then add your Apple Team ID and build via File ▸ Build Settings.
/// </summary>
public static class BuildConfig
{
    const string BundleId = "com.indierunner.subwayrunner";

    [MenuItem("Tools/Subway Runner/Configure iOS")]
    public static void ConfigureIOS()
    {
        PlayerSettings.companyName = "Indie Runner";
        PlayerSettings.productName = "Subway Runner";
        PlayerSettings.bundleVersion = "1.0";

        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, BundleId);

        // Portrait phone game.
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
        PlayerSettings.allowedAutorotateToPortrait = true;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = false;
        PlayerSettings.allowedAutorotateToLandscapeRight = false;

        // iOS specifics.
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.iOS.targetOSVersionString = "13.0";
        PlayerSettings.iOS.appleEnableAutomaticSigning = true;
        PlayerSettings.iOS.buildNumber = "1";
        PlayerSettings.statusBarHidden = true;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);

        AssetDatabase.SaveAssets();
        Debug.Log("[BuildConfig] iOS player settings configured. " +
                  "Set your Apple Team ID in Project Settings ▸ Player ▸ iOS before building.");
    }

    [MenuItem("Tools/Subway Runner/Switch Platform to iOS")]
    public static void SwitchToIOS()
    {
        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
        Debug.Log("[BuildConfig] Active build target switched to iOS.");
    }
}
