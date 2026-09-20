using UnityEngine;

namespace BackyardLegends.Runtime
{
    public static class BackyardLegendsStreetAudio
    {
        private const string SfxRoot = "BackyardLegends/Audio/Sfx/";

        public static AudioClip LoadSfx(string clipName)
        {
            return string.IsNullOrEmpty(clipName)
                ? null
                : Resources.Load<AudioClip>(SfxRoot + clipName);
        }
    }
}
