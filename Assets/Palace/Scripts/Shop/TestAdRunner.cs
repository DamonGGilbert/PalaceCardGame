using System;
using System.Collections;
using TMPro;
using UnityEngine;

namespace Palace
{
    /// <summary>
    /// Plays the stand-in "TEST AD" countdown used until a real ad network is connected (see RewardedAds).
    /// Its own file, named after the class, so Unity can find the script in built games.
    /// </summary>
    public sealed class TestAdRunner : MonoBehaviour
    {
        public void Run(TextMeshProUGUI text, GameObject root, Action<bool> finished) => StartCoroutine(Play(text, root, finished));

        private IEnumerator Play(TextMeshProUGUI text, GameObject root, Action<bool> finished)
        {
            for (int s = 3; s > 0; s--)
            {
                text.text = "TEST AD\n<size=32>(a real advert would play here)</size>\n" + s;
                yield return new WaitForSecondsRealtime(1f);
            }
            Destroy(root);
            finished?.Invoke(true);
        }
    }
}
