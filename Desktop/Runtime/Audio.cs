using System;
using System.Collections.Generic;
using Raylib_cs;

namespace UnityEngine
{
    public class AudioListener : Component
    {
    }

    public class AudioSource : Component
    {
        public float spatialBlend;
        public void PlayOneShot(AudioClip clip, float volume)
        {
            if (!clip.loaded)
                return;
            Raylib.SetSoundVolume(clip.sound, volume);
            Raylib.PlaySound(clip.sound);
        }
    }

    public class AudioClip
    {
        internal Sound sound;
        internal bool loaded;
        int sampleRate;
        static readonly List<AudioClip> clips = new();
        public static AudioClip Create(string name, int samples, int channels, int rate, bool stream) => new()
        {
            sampleRate = rate
        };
        public unsafe void SetData(float[] data, int offset)
        {
            if (!Raylib.IsAudioDeviceReady())
                return;
            fixed (float* ptr = data)
                sound = Raylib.LoadSoundFromWave(new Wave { SampleCount = (uint)data.Length, SampleRate = (uint)sampleRate, SampleSize = 32, Channels = 1, Data = ptr });
            loaded = true;
            clips.Add(this);
        }

        internal static void Shutdown()
        {
            foreach (var clip in clips)
                Raylib.UnloadSound(clip.sound);
            clips.Clear();
        }
    }
}
