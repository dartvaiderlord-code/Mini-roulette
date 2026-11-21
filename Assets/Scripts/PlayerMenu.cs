using System;
using UnityEngine;

namespace Carousel
{
    public sealed partial class Club
    {
        readonly int[] defaults =
        {
            1,
            'R',
            'F'
        };
        readonly string[] actions =
        {
            "Спуск",
            "Прокрутка",
            "Своя цель (удерживать)"
        };
        readonly int[] keys = new int[3];
        int volume = 85, sensitivity = 100, fov = 72, binding = -1, spinSequence;
        bool options, captured, selfAim;
        float allowInputAt;
        bool LiveView => table != null && table.phase == "match" && table.Find(me).alive;

        void LoadControls()
        {
            for (int i = 0; i < keys.Length; i++)
            {
                int value = PlayerPrefs.GetInt("action" + i);
                keys[i] = value == 0 ? defaults[i] : value;
            }

            int s = PlayerPrefs.GetInt("sensitivity"), f = PlayerPrefs.GetInt("fov");
            sensitivity = Limit(s == 0 ? 100 : s, 20, 250);
            fov = Limit(f == 0 ? 72 : f, 50, 105);
            volume = Limit(PlayerPrefs.GetInt("volume", 85), 0, 100);
            AudioListener.volume = volume / 100f;
            Screen.fullScreen = PlayerPrefs.GetInt("fullscreen", Screen.fullScreen ? 1 : 0) != 0;
            stage.FieldOfView = fov;
        }

        static int Limit(int value, int min, int max) => Math.Min(max, Math.Max(min, value));
        void SaveControls()
        {
            for (int i = 0; i < keys.Length; i++)
                PlayerPrefs.SetInt("action" + i, keys[i]);
            PlayerPrefs.SetInt("sensitivity", sensitivity);
            PlayerPrefs.SetInt("fov", fov);
            PlayerPrefs.SetInt("volume", volume);
            AudioListener.volume = volume / 100f;
            PlayerPrefs.Save();
            stage.FieldOfView = fov;
        }

        void ToggleFullscreen()
        {
            bool next = !Screen.fullScreen;
            Screen.fullScreen = next;
            PlayerPrefs.SetInt("fullscreen", next ? 1 : 0);
            PlayerPrefs.Save();
        }

        void Settings(bool open)
        {
            options = open;
            binding = -1;
            allowInputAt = Time.realtimeSinceStartup + .18f;
            if (!open)
                SaveControls();
            Grab(false);
        }

        void Grab(bool value)
        {
            if (captured == value)
                return;
            captured = value;
            PlayControls.Capture(value);
        }

        void Update()
        {
            PollLink();
            if (PlayControls.FullscreenPressed()) ToggleFullscreen();
            if (captured && !PlayControls.Focused)
                Settings(true);
            if (PlayControls.Key(PlayControls.Escape))
            {
                if (binding >= 0)
                    binding = -1;
                else
                    Settings(!options);
                return;
            }

            if (options && binding >= 0 && Time.realtimeSinceStartup >= allowInputAt)
            {
                int code = PlayControls.NewBinding();
                if (code != 0)
                {
                    int duplicate = Array.IndexOf(keys, code);
                    if (duplicate >= 0)
                        keys[duplicate] = keys[binding];
                    keys[binding] = code;
                    binding = -1;
                    SaveControls();
                }

                return;
            }

            bool play = LiveView && !options && PlayControls.Focused;
            if (!play)
            {
                Grab(false);
                selfAim = false;
                return;
            }

            if (!captured)
            {
                Grab(true);
                allowInputAt = Time.realtimeSinceStartup + .18f;
                return;
            }

            PlayControls.Delta(out float dx, out float dy);
            stage.Look(dx * .00145f * sensitivity, dy * .00145f * sensitivity);
            selfAim = PlayControls.Key(keys[2], true);
            target = selfAim ? me : stage.AimedSeat();
            if (pending || table.turn != me || Time.realtimeSinceStartup < allowInputAt || stage.Acting)
                return;
            if (PlayControls.Key(keys[1]) && !stage.Turning)
            {
                if (table.independentSpin)
                    Action("spin");
                else
                {
                    spin = true;
                    stage.Twirl();
                }
            }
            else if (PlayControls.Key(keys[0]) && target != 0)
                Action("move");
        }

        void OptionsUI()
        {
            float x = (width - 490) / 2, y = (height - 555) / 2;
            Panel(x, y, 490, 555);
            Fill(x + 2, y + 2, 486, 56, Sea);
            Label(x + 22, y + 6, 442, 44, "Настройки", 25, Paper, true);
            for (int i = 0; i < 3; i++)
            {
                float row = y + 75 + i * 43;
                Label(x + 23, row, 260, 36, actions[i], 17);
                if (Button(x + 287, row, 180, 35, binding == i ? "Нажмите кнопку…" : PlayControls.Name(keys[i]), binding == i ? Vermilion : Sea))
                {
                    binding = i;
                    allowInputAt = Time.realtimeSinceStartup + .16f;
                }
            }

            Stepper("Чувствительность", sensitivity + " %", y + 217, 10, 20, 250, ref sensitivity);
            Stepper("Поле зрения", fov + "°", y + 264, 5, 50, 105, ref fov);
            Stepper("Общая громкость", volume + " %", y + 311, 5, 0, 100, ref volume);
            Label(x + 23, y + 359, 230, 36, "Режим экрана · F11", 17);
            if (Button(x + 251, y + 359, 216, 35, Screen.fullScreen ? "Полный экран" : "В окне"))
                ToggleFullscreen();
            ToggleMild(x + 23, y + 418);
            if (Button(x + 285, y + 418, 182, 31, "По умолчанию", Muted))
            {
                Array.Copy(defaults, keys, 3);
                sensitivity = 100;
                fov = 72;
                volume = 85;
                binding = -1;
                SaveControls();
            }

            if (Button(x + 23, y + 488, 218, 44, table == null ? "Назад" : "Продолжить", Sea))
                Settings(false);
            if (table != null && Button(x + 251, y + 488, 216, 44, "Покинуть комнату", Vermilion))
            {
                Settings(false);
                Leave();
            }

            void Stepper(string title, string value, float row, int stepSize, int min, int max, ref int setting)
            {
                Label(x + 23, row, 230, 36, title, 17);
                if (Button(x + 247, row, 45, 35, "−", Muted, setting > min))
                {
                    setting = Limit(setting - stepSize, min, max);
                    SaveControls();
                }

                Label(x + 296, row, 115, 35, value, 19, Dark, true, TextAnchor.MiddleCenter);
                if (Button(x + 420, row, 47, 35, "+", Muted, setting < max))
                {
                    setting = Limit(setting + stepSize, min, max);
                    SaveControls();
                }
            }
        }

        void ViewHUD()
        {
            bool own = table.turn == me;
            Fill(22, 20, 300, 64, Dark);
            Label(36, 25, 266, 22, "КАРУСЕЛЬ  /  РАУНД " + table.round, 15, Gold, true);
            Label(36, 48, 270, 27, own ? "Ваш ход" : "Ходит  ·  " + table.Find(table.turn)?.name, 20, Paper, true);
            float cx = width / 2, cy = height / 2;
            int color = target != 0 && own ? Gold : Paper;
            Fill(cx - 9, cy - 1, 5, 2, color);
            Fill(cx + 4, cy - 1, 5, 2, color);
            Fill(cx - 1, cy - 9, 2, 5, color);
            Fill(cx - 1, cy + 4, 2, 5, color);
            if (target != 0)
                Label(cx - 150, cy + 11, 300, 27, selfAim ? "Своё место" : table.Find(target)?.name, 17, Paper, true, TextAnchor.MiddleCenter);
            Fill(22, height - 63, width - 44, 41, Dark);
            string hint = PlayControls.Name(keys[0]) + "  Спуск     " + PlayControls.Name(keys[1]) + "  Крутить     " + PlayControls.Name(keys[2]) + "  Своя цель     Esc  Меню";
            Label(36, height - 60, width * .64f, 35, hint, 16, Paper);
            Label(width * .59f, height - 60, width * .41f - 38, 35, string.IsNullOrEmpty(notice) ? lastMove : notice, 15, Gold, false, TextAnchor.MiddleRight);
        }
    }
}
