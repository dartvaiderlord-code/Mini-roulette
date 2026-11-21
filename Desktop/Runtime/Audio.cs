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
        AudioClip current;
        public bool isPlaying => current != null && current.loaded && Raylib.IsSoundPlaying(current.sound);

        public void PlayOneShot(AudioClip clip, float volume)
        {
            if (!clip.loaded)
                return;
            current = clip;
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

        public static AudioClip FromFile(string name)
        {
            var clip = new AudioClip();
            if (!Raylib.IsAudioDeviceReady())
                return clip;
            string path = System.IO.Path.Combine(AppContext.BaseDirectory, "Resources", "Audio", name + ".ogg");
            clip.sound = Raylib.LoadSound(path);
            clip.loaded = Raylib.IsSoundValid(clip.sound);
            if (clip.loaded)
                clips.Add(clip);
            return clip;
        }

        internal static void Shutdown()
        {
            foreach (var clip in clips)
                Raylib.UnloadSound(clip.sound);
            clips.Clear();
        }
    }
}
