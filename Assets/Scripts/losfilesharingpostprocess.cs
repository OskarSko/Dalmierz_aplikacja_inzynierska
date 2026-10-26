#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

public static class IosFileSharingPostprocess
{
    [PostProcessBuild(100)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.iOS) return;

        string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
        var plist = new PlistDocument();
        plist.ReadFromFile(plistPath);

        PlistElementDict root = plist.root;
        root.SetBoolean("UIFileSharingEnabled", true);
        root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);

        plist.WriteToFile(plistPath);
        UnityEngine.Debug.Log("[iOS] Wlaczono udostepnianie plikow - CSV widoczne w aplikacji Pliki");
    }
}
#endif