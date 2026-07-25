using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

public static class SteamAppIdBuildCleanup
{
    // steam_appid.txt is a dev-only file that lets the editor talk to Steam; if it
    // ships next to the exe, the game can run outside Steam. Runs after other
    // post-build hooks (e.g. Steamworks.NET's) so it always gets the last word.
    [PostProcessBuild(9999)]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        if (target != BuildTarget.StandaloneWindows
            && target != BuildTarget.StandaloneWindows64
            && target != BuildTarget.StandaloneLinux64
            && target != BuildTarget.StandaloneOSX)
        {
            return;
        }

        string buildDir = Path.GetDirectoryName(pathToBuiltProject);
        if (string.IsNullOrEmpty(buildDir)) return;

        string appIdPath = Path.Combine(buildDir, "steam_appid.txt");
        if (!File.Exists(appIdPath)) return;

        File.Delete(appIdPath);
        Debug.Log("[SteamAppIdBuildCleanup] Removed steam_appid.txt from build output.");
    }
}
