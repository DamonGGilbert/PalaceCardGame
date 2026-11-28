using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Tiny hidden object that performs cloud save's delayed upload (see CloudProgress.ScheduleUpload).
    /// Its own file, named after the class, so Unity can find the script in built games.
    /// </summary>
    public sealed class CloudProgressRunner : MonoBehaviour
    {
        private static CloudProgressRunner instance;

        public static void Ensure()
        {
            if (instance != null) return; // also re-creates it after the object is destroyed (e.g. leaving play mode)
            var go = new GameObject("CloudProgress");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<CloudProgressRunner>();
        }

        private void Update() => CloudProgress.TickUpload();
    }
}
