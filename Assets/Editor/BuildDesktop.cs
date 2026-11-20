using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace Carousel.Editor
{
    public static class BuildDesktop
    {
        [MenuItem("Carousel/Build Windows x64")]
        public static void Windows()
        {
            if (!File.Exists("Assets/StreamingAssets/Host/carousel-windows-amd64.exe"))
                throw new BuildFailedException("Missing local game server in StreamingAssets/Host");
            PlayerSettings.productName = "Карусель";
            PlayerSettings.companyName = "Carousel Club";
            PlayerSettings.defaultScreenWidth = 1440;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.fullScreenMode = UnityEngine.FullScreenMode.Windowed;
            PlayerSettings.resizableWindow = true;
            Directory.CreateDirectory("Builds/Windows");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Scenes/Club.unity" }, target = BuildTarget.StandaloneWindows64, locationPathName = "Builds/Windows/Carousel.exe", options = BuildOptions.None });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException("Windows player build: " + report.summary.result);
        }
    }
}
