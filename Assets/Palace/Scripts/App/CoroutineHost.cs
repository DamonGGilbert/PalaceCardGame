using System.Collections;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// One hidden object that lasts for the whole session, for code that isn't on a GameObject but needs
    /// coroutines (menu pop-ins that outlive their screen, the notification permission request).
    /// Its own file, named after the class, so Unity can find the script in built games.
    /// </summary>
    public sealed class CoroutineHost : MonoBehaviour
    {
        private static CoroutineHost instance;

        public static Coroutine Run(IEnumerator routine)
        {
            if (instance == null) // also true after the object is destroyed (e.g. leaving play mode)
            {
                var go = new GameObject("CoroutineHost");
                DontDestroyOnLoad(go);
                instance = go.AddComponent<CoroutineHost>();
            }
            return instance.StartCoroutine(routine);
        }
    }
}
