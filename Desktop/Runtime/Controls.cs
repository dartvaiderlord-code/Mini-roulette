using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using R = Raylib_cs;
using N = System.Numerics;

namespace UnityEngine
{
    public enum RuntimeInitializeLoadType
    {
        AfterSceneLoad
    }

    public class RuntimeInitializeOnLoadMethodAttribute : Attribute
    {
        public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType stage)
        {
        }
    }

    public static class JsonUtility
    {
        static readonly JsonSerializerOptions options = new()
        {
            IncludeFields = true
        };
        public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, options);
        public static T FromJson<T>(string json)
        {
            try
            {
                return JsonSerializer.Deserialize<T>(json, options);
            }
            catch (JsonException e)
            {
                throw new ArgumentException("Invalid room message", e);
            }
        }
    }

    public enum RuntimePlatform
    {
        WindowsEditor,
        WindowsPlayer,
        LinuxPlayer
    }

    public static class Application
    {
        public static bool runInBackground, exiting;
        public static int targetFrameRate;
        public static RuntimePlatform platform => OperatingSystem.IsWindows() ? RuntimePlatform.WindowsPlayer : RuntimePlatform.LinuxPlayer;
        public static string streamingAssetsPath => AppContext.BaseDirectory;

        public static void Quit() => exiting = true;
    }

    public static class PlayerPrefs
    {
        static readonly string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CarouselClub");
        static readonly string file = Path.Combine(directory, "settings.json");
        static readonly Dictionary<string, string> values = Read();
        static Dictionary<string, string> Read()
        {
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) ?? new();
            }
            catch (Exception e)when (e is IOException or JsonException or UnauthorizedAccessException)
            {
                return new();
            }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(directory);
                string temp = Path.Combine(directory, "settings-" + Environment.ProcessId + ".tmp");
                File.WriteAllText(temp, JsonSerializer.Serialize(values));
                File.Move(temp, file, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        public static int GetInt(string key) => int.TryParse(GetString(key, ""), out var val) ? val : 0;
        public static string GetString(string key, string fallback) => values.GetValueOrDefault(key, fallback);
        public static void SetInt(string key, int value) => SetString(key, value.ToString());
        public static void SetString(string key, string value) => values[key] = value;
    }

    public class Screen
    {
        public static int width => R.Raylib.GetScreenWidth();
        public static int height => R.Raylib.GetScreenHeight();
    }

    public enum FontStyle
    {
        Normal,
        Bold
    }

    public enum TextClipping
    {
        Clip
    }

    public class RectOffset
    {
    }

    public record struct Rect(float x, float y, float width, float height)
    {
        public bool Contains(Vector3 p) => p.x >= x && p.x < x + width && p.y >= y && p.y < y + height;
    }

    public class Event
    {
        public static readonly Event current = new();
        public Vector3 mousePosition => new(R.Raylib.GetMouseX() / GUI.matrix.s, R.Raylib.GetMouseY() / GUI.matrix.s, 0);
    }

    public class GUIStyleState
    {
        public Color textColor;
        public Object background;
    }

    public class GUIStyle
    {
        public GUIStyle()
        {
        }

        public GUIStyle(GUIStyle original)
        {
        }

        public static GUIStyle none = new();
        public bool richText, wordWrap;
        public int fontSize;
        public FontStyle fontStyle;
        public TextClipping clipping;
        public RectOffset padding;
        public TextAnchor alignment;
        public GUIStyleState normal = new(), focused = new(), hover = new(), active = new();
    }

    public class Texture2D : Object
    {
        public static readonly Texture2D whiteTexture = new();
    }

    public class GUIContent
    {
        public static readonly GUIContent none = new();
    }

    public class GUISkin
    {
        public GUIStyle label = new(), textField = new();
    }

    public struct Matrix4x4
    {
        public float s;
        public static Matrix4x4 Scale(Vector3 v) => new()
        {
            s = v.x
        };
    }

    public static class GUI
    {
        public static readonly GUISkin skin = new();
        public static Matrix4x4 matrix = Matrix4x4.Scale(new Vector3(1, 1, 1));
        public static Color color;
        static Rect? field;
        static bool selectAll;
        static Rect? pressed;
        public static R.Font Regular, Bold;
        public static void BeginFrame()
        {
            if (R.Raylib.IsMouseButtonPressed(R.MouseButton.Left))
            {
                field = null;
                selectAll = false;
                pressed = null;
            }
        }

        public static R.Color Bytes(this Color c) => new((byte)(Math.Clamp(c.r, 0, 1) * 255 + .5f), (byte)(Math.Clamp(c.g, 0, 1) * 255 + .5f), (byte)(Math.Clamp(c.b, 0, 1) * 255 + .5f), (byte)(Math.Clamp(c.a, 0, 1) * 255 + .5f));
        public static void DrawTexture(Rect r, Texture2D texture)
        {
            float s = matrix.s;
            R.Raylib.DrawRectangleRec(new R.Rectangle(r.x * s, r.y * s, r.width * s, r.height * s), color.Bytes());
        }

        public static void Label(Rect r, string text, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text))
                return;
            text = text.Replace('✓', '›');
            float s = matrix.s, size = Math.Max(8, style.fontSize * s);
            var font = style.fontStyle == FontStyle.Bold ? Bold : Regular;
            var dimensions = R.Raylib.MeasureTextEx(font, text, size, 0);
            float x = r.x * s, y = (r.y + r.height * .5f) * s - size * .5f;
            if (style.alignment == TextAnchor.MiddleCenter)
                x += (r.width * s - dimensions.X) * .5f;
            if (style.alignment == TextAnchor.MiddleRight)
                x += r.width * s - dimensions.X;
            R.Raylib.BeginScissorMode((int)(r.x * s), (int)(r.y * s), (int)Math.Ceiling(r.width * s), (int)Math.Ceiling(r.height * s));
            R.Raylib.DrawTextEx(font, text, new N.Vector2(MathF.Round(x), MathF.Round(y)), size, 0, style.normal.textColor.Bytes());
            R.Raylib.EndScissorMode();
        }

        public static bool Button(Rect r, GUIContent content, GUIStyle style)
        {
            if (!r.Contains(Event.current.mousePosition))
                return false;
            if (R.Raylib.IsMouseButtonPressed(R.MouseButton.Left))
                pressed = r;
            return pressed == r && R.Raylib.IsMouseButtonReleased(R.MouseButton.Left);
        }

        public static string TextField(Rect r, string value, int limit, GUIStyle style)
        {
            if (r.Contains(Event.current.mousePosition) && R.Raylib.IsMouseButtonPressed(R.MouseButton.Left))
            {
                field = r;
                selectAll = true;
            }

            if (field == r)
            {
                bool ctrl = R.Raylib.IsKeyDown(R.KeyboardKey.LeftControl) || R.Raylib.IsKeyDown(R.KeyboardKey.RightControl);
                if (ctrl && R.Raylib.IsKeyPressed(R.KeyboardKey.A))
                    selectAll = true;
                if (R.Raylib.IsKeyPressed(R.KeyboardKey.Backspace) || R.Raylib.IsKeyPressedRepeat(R.KeyboardKey.Backspace))
                {
                    value = selectAll || value.Length == 0 ? "" : value[..^1];
                    selectAll = false;
                }

                string input = "";
                if (ctrl && R.Raylib.IsKeyPressed(R.KeyboardKey.V))
                    input = R.Raylib.GetClipboardText_() ?? "";
                for (int c = R.Raylib.GetCharPressed(); c > 0; c = R.Raylib.GetCharPressed())
                    if (!ctrl && c >= 32)
                        input += char.ConvertFromUtf32(c);
                if (input.Length > 0)
                {
                    if (selectAll)
                        value = "";
                    value += new string (input.Where(c => !char.IsControl(c)).Take(Math.Max(0, limit - value.Length)).ToArray());
                    selectAll = false;
                }

                if (R.Raylib.IsKeyPressed(R.KeyboardKey.Enter) || R.Raylib.IsKeyPressed(R.KeyboardKey.Escape))
                    field = null;
            }

            Label(r, value + (field == r && ((int)(R.Raylib.GetTime() * 2) & 1) == 0 ? "|" : ""), style);
            return value;
        }
    }
}
