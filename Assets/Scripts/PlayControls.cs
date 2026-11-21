using System;
using UnityEngine;

#if CAROUSEL_PORTABLE
using R = Raylib_cs;
#endif
namespace Carousel
{
    // Store portable button codes so that the in-game binding names are identical on both clients.
    internal static class PlayControls
    {
        public const int Escape = 27;
        public static bool Focused
        {
            get
            {
#if CAROUSEL_PORTABLE
                return R.Raylib.IsWindowFocused();
#else
                return Application.isFocused;
#endif
            }
        }

        public static void Capture(bool value)
        {
#if CAROUSEL_PORTABLE
            if(value) R.Raylib.DisableCursor(); else R.Raylib.EnableCursor();
#else
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;
#endif
        }

        public static bool Key(int code, bool held = false)
        {
#if CAROUSEL_PORTABLE
            if (code>=1 && code<=3)
            {
                var button=(R.MouseButton)(code-1);
                return held?R.Raylib.IsMouseButtonDown(button):R.Raylib.IsMouseButtonPressed(button);
            }
            var key=code==Escape?R.KeyboardKey.Escape:(R.KeyboardKey)code;
            return held?R.Raylib.IsKeyDown(key):R.Raylib.IsKeyPressed(key);
#else
            if (code >= 1 && code <= 3)
                return held ? Input.GetMouseButton(code - 1) : Input.GetMouseButtonDown(code - 1);
            KeyCode key = (KeyCode)(code >= 'A' && code <= 'Z' ? code + 32 : code);
            return held ? Input.GetKey(key) : Input.GetKeyDown(key);
#endif
        }

        public static void Delta(out float x, out float y)
        {
#if CAROUSEL_PORTABLE
            var v=R.Raylib.GetMouseDelta(); x=v.X; y=v.Y;
#else
            x = Input.GetAxisRaw("Mouse X") * 10;
            y = -Input.GetAxisRaw("Mouse Y") * 10;
#endif
        }

        public static int NewBinding()
        {
            for (int i = 1; i <= 3; i++)
                if (Key(i))
                    return i;
            if (Key(32))
                return 32;
            for (int i = '0'; i <= 'Z'; i++)
                if ((i <= '9' || i >= 'A') && Key(i))
                    return i;
            return 0;
        }

        public static string Name(int code)
        {
            switch (code)
            {
                case 1:
                    return "ЛКМ";
                case 2:
                    return "ПКМ";
                case 3:
                    return "СКМ";
                case 32:
                    return "Пробел";
                default:
                    return ((char)code).ToString();
            }
        }
    }
}
