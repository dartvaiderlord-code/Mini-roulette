using System;
using System.IO;
using System.Linq;
using R = Raylib_cs;
using U = UnityEngine;

namespace Carousel
{
    public sealed partial class Stage
    {
        internal void Frame() => Update();
    }

    public sealed partial class Club
    {
        internal Stage DesktopStage => stage;

        internal void StartDesktop() => Awake();
        internal void TickDesktop()
        {
            Update();
            stage.Frame();
        }

        internal void DrawDesktop() => OnGUI();
        internal void StopDesktop() => OnDestroy();
    }
}

namespace Carousel.Desktop
{
    internal static class Program
    {
        static R.Font Font(string name, int[] glyphs)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Resources", name);
            var font = R.Raylib.LoadFontEx(path, 54, glyphs, glyphs.Length);
            R.Raylib.SetTextureFilter(font.Texture, R.TextureFilter.Bilinear);
            return font;
        }

        static void Main()
        {
            R.Raylib.SetConfigFlags(R.ConfigFlags.ResizableWindow | R.ConfigFlags.Msaa4xHint);
            R.Raylib.InitWindow(1280, 800, "Карусель");
            R.Raylib.SetWindowMinSize(900, 550);
            R.Raylib.SetExitKey(R.KeyboardKey.Null);
            R.Raylib.InitAudioDevice();
            R.Raylib.SetTargetFPS(60);
            var glyphs = Enumerable.Range(32, 95).Concat(Enumerable.Range(0x400, 256)).Concat("·—–…→›×−•".Select(c => (int)c)).Distinct().ToArray();
            U.GUI.Regular = Font("LiberationSans-Regular.ttf", glyphs);
            U.GUI.Bold = Font("LiberationSans-Bold.ttf", glyphs);
            var club = new U.GameObject("Клуб").AddComponent<Club>();
            try
            {
                club.StartDesktop();
                using var renderer = new SceneRenderer();
                while (!R.Raylib.WindowShouldClose() && !U.Application.exiting)
                {
                    U.Time.deltaTime = Math.Clamp(R.Raylib.GetFrameTime(), .001f, .1f);
                    U.Time.time = (float)R.Raylib.GetTime();
                    U.GUI.BeginFrame();
                    club.TickDesktop();
                    R.Raylib.BeginDrawing();
                    renderer.Render(club.DesktopStage.Eye);
                    club.DrawDesktop();
                    R.Raylib.EndDrawing();
                }
            }
            finally
            {
                club.StopDesktop();
                U.AudioClip.Shutdown();
                R.Raylib.CloseAudioDevice();
                R.Raylib.UnloadFont(U.GUI.Regular);
                R.Raylib.UnloadFont(U.GUI.Bold);
                R.Raylib.CloseWindow();
            }
        }
    }
}
