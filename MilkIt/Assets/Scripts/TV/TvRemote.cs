using UnityEngine;

namespace MilkIt.TV
{
    public class TvRemote : MonoBehaviour
    {
        [Tooltip("The TV this remote controls. Empty = the first TV found in the scene.")]
        public TvScreen tv;

        void Start()
        {
            if (tv == null) tv = FindAnyObjectByType<TvScreen>();
            if (tv == null) Debug.LogWarning($"{name}: no TvScreen in the scene.", this);
        }

        public void Power()
        {
            if (tv != null) tv.TogglePower();
        }

        public void Next()
        {
            if (tv != null) tv.NextClip();
        }

        public void Mute()
        {
            if (tv != null) tv.ToggleMute();
        }
    }
}
