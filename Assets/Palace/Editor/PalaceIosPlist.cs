#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace Palace.EditorTools
{
    /// <summary>
    /// iPhone builds: adds the sentence iOS shows when the game first asks to use the local network
    /// (Same Wi-Fi games). Without it, iOS blocks Wi-Fi play entirely. Online play doesn't need it.
    /// </summary>
    public static class PalaceIosPlist
    {
        private const string LocalNetworkReason =
            "Shedhead uses your local network to find and join card games with friends on the same Wi-Fi.";

        [PostProcessBuild(100)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString("NSLocalNetworkUsageDescription", LocalNetworkReason);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
