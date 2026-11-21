using System;
using System.IO;
using UnityEngine;
using static Carousel.Shapes;

namespace Carousel
{
    public sealed partial class Club : MonoBehaviour
    {
        Stage stage;
        RoomLink link;
        TableState table;
        int me, target, watch, lastShot, bots = 1;
        bool net, host, spin, pending, mild;
        string nickname, address, portText = "7777", notice = "", lastMove = "";
        float waitingSince;
        GUIStyle font, field;
        float width, height;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Launch()
        {
            if (!FindObjectOfType<Club>())
                new GameObject("Клуб Карусель").AddComponent<Club>();
        }

        void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            nickname = PlayerPrefs.GetString("name", "Игрок");
            address = PlayerPrefs.GetString("server", "127.0.0.1");
            mild = PlayerPrefs.GetInt("mild") == 1;
            stage = gameObject.AddComponent<Stage>();
            stage.Setup();
            LoadControls();
            stage.Mild = mild;
            Preview();
        }

        void Preview()
        {
            stage.Reseat(new[] { new Seat { id = -4, alive = true }, new Seat { id = -3, alive = true }, new Seat { id = -2, alive = true } });
        }

        void Connect()
        {
            int port = 7777;
            if (net && (!int.TryParse(portText, out port) || port < 1 || port > 65535))
            {
                notice = "Введите порт от 1 до 65535";
                return;
            }

            if (net && !host && string.IsNullOrWhiteSpace(address))
            {
                notice = "Введите адрес сервера";
                return;
            }

            string exe = null;
            if (!net || host)
            {
                bool windows = Application.platform == RuntimePlatform.WindowsEditor || Application.platform == RuntimePlatform.WindowsPlayer;
                exe = Path.Combine(Application.streamingAssetsPath, "Host", windows ? "carousel-windows-amd64.exe" : "carousel-linux-amd64");
                if (!File.Exists(exe))
                {
                    notice = "Отсутствует локальный игровой сервер";
                    return;
                }
            }

            notice = "";
            PlayerPrefs.SetString("name", nickname);
            PlayerPrefs.SetString("server", address);
            PlayerPrefs.Save();
            waitingSince = Time.realtimeSinceStartup;
            link = new RoomLink();
            link.Open(address.Trim(), port, exe, net ? 0 : bots, JsonUtility.ToJson(new Command { type = "hello", name = nickname }));
        }

        void Leave(string reason = "")
        {
            link?.Dispose();
            link = null;
            table = null;
            me = target = watch = lastShot = spinSequence = 0;
            options = false;
            binding = -1;
            Grab(false);
            pending = spin = false;
            lastMove = "";
            notice = reason;
            Preview();
        }

        void OnDestroy()
        {
            Grab(false);
            link?.Dispose();
            if (stage) stage.ReleaseCast();
            ModelLibrary.Release();
        }

        void PollLink()
        {
            if (link == null)
                return;
            while (link.Lines.TryDequeue(out var line))
            {
                Envelope e;
                try
                {
                    e = JsonUtility.FromJson<Envelope>(line);
                }
                catch (ArgumentException)
                {
                    Leave("Сервер прислал неизвестный формат данных");
                    return;
                }

                if (e == null)
                {
                    Leave("Пустой ответ сервера");
                    return;
                }

                if (e.type == "error")
                {
                    if (table == null)
                    {
                        Leave(e.message);
                        return;
                    }

                    notice = e.message;
                    pending = false;
                    continue;
                }

                var s = e.state;
                if (e.type != "state" || s == null || s.players == null || s.players.Length < 1 || s.players.Length > 5 || Array.Exists(s.players, p => p == null) || s.Find(e.me) == null)
                {
                    Leave("Ответ несовместим с клиентом");
                    return;
                }

                bool rebuild = table == null || table.players.Length != s.players.Length || s.phase == "lobby" && table.phase != "lobby";
                if (!rebuild)
                    for (int i = 0; i < s.players.Length; i++)
                        if (s.players[i].id != table.players[i].id)
                            rebuild = true;
                if (rebuild)
                    stage.Reseat(s.players);
                bool eventArrived = s.shot != null && s.shot.seq > lastShot;
                if (eventArrived)
                {
                    lastShot = s.shot.seq;
                    var actor = s.Find(s.shot.actor);
                    var victim = s.Find(s.shot.target);
                    lastMove = (actor?.name ?? "Игрок") + " → " + (victim?.name ?? "игрок") + (s.shot.hit ? "  •  попадание" : "  •  пусто");
                }

                if (s.phase == "lobby")
                {
                    lastMove = "";
                    lastShot = 0;
                }

                me = e.me;
                table = s;
                pending = false;
                notice = "";
                if (s.Find(target) == null || !s.Find(target).alive)
                    target = 0;
                if (watch != 0 && (s.Find(watch) == null || !s.Find(watch).alive))
                    watch = 0;
                stage.Watch(watch);
                stage.Show(s, eventArrived);
                stage.ViewAs(LiveView ? me : 0);
                if (s.spinSeq > spinSequence)
                    stage.Twirl();
                spinSequence = s.spinSeq;
                if (eventArrived)
                {
                    target = 0;
                    spin = false;
                }
            }

            if (link.Failures.TryDequeue(out var failure))
            {
                Leave(failure);
                return;
            }

            if (table == null && Time.realtimeSinceStartup - waitingSince > 12)
                Leave("Сервер не ответил за 12 секунд");
        }

        void Action(string type)
        {
            pending = true;
            notice = "";
            link.Send(new Command { type = type, target = target, spin = spin, revision = table.revision });
        }

        const int Paper = 0xF8EACB, Dark = 0x243B3D, Muted = 0x566969, Sea = 0x2C6B69, Vermilion = 0xA93727, Gold = 0xEDB953;
        void RectFill(float x, float y, float w, float h, Color c)
        {
            Color before = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);
            GUI.color = before;
        }

        void Fill(float x, float y, float w, float h, int color)
        {
            RectFill(x, y, w, h, Ink(color));
        }

        void Panel(float x, float y, float w, float h)
        {
            RectFill(x + 4, y + 5, w, h, new Color(.07f, .11f, .11f, .35f));
            Fill(x, y, w, h, Dark);
            Fill(x + 2, y + 2, w - 4, h - 4, Paper);
        }

        void Label(float x, float y, float w, float h, string txt, int size, int color = Dark, bool bold = false, TextAnchor align = TextAnchor.MiddleLeft)
        {
            font.fontSize = size;
            font.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            font.alignment = align;
            font.normal.textColor = Ink(color);
            GUI.Label(new Rect(x, y, w, h), txt, font);
        }

        bool Button(float x, float y, float w, float h, string txt, int background = Sea, bool enabled = true)
        {
            var r = new Rect(x, y, w, h);
            Color c = Ink(enabled ? background : 0x747D78);
            if (enabled && r.Contains(Event.current.mousePosition))
                c = Color.Lerp(c, Color.white, .14f);
            RectFill(x, y, w, h, c);
            Label(x + 3, y, w - 6, h, txt, 17, 0xFFF7E5, true, TextAnchor.MiddleCenter);
            return enabled && GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        string Input(float x, float y, float w, string text, int limit)
        {
            Fill(x, y, w, 40, Dark);
            Fill(x + 2, y + 2, w - 4, 36, 0xFFF7E5);
            return GUI.TextField(new Rect(x + 8, y, w - 16, 40), text, limit, field);
        }

        void ToggleMild(float x, float y)
        {
            if (Button(x, y, 171, 31, mild ? "Эффекты · мягкие" : "Эффекты · полные", Dark))
            {
                mild = !mild;
                stage.Mild = mild;
                PlayerPrefs.SetInt("mild", mild ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        void OnGUI()
        {
            if (font == null)
            {
                font = new GUIStyle(GUI.skin.label)
                {
                    richText = false,
                    wordWrap = false,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset()
                };
                field = new GUIStyle(GUI.skin.textField)
                {
                    fontSize = 18,
                    alignment = TextAnchor.MiddleLeft,
                    richText = false,
                    normal =
                    {
                        textColor = Ink(Dark),
                        background = null
                    },
                    focused =
                    {
                        textColor = Ink(Dark),
                        background = null
                    }
                };
                field.hover.background = null;
                field.active.background = null;
                field.hover.textColor = field.active.textColor = Ink(Dark);
            }

            float scale = Mathf.Min(Screen.width / 1180f, Screen.height / 710f);
            var matrix = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(V(scale, scale, 1));
            width = Screen.width / scale;
            height = Screen.height / scale;
            if (options)
                OptionsUI();
            else if (table == null)
                Menu();
            else if (LiveView)
                ViewHUD();
            else
                MatchUI();
            if (stage.Pulse > 0 && !mild)
            {
                float a = stage.Pulse * (.24f + .09f * Mathf.Sin(stage.Pulse * 16));
                RectFill(0, 0, width, height, new Color(.76f, .08f, .12f, a));
            }

            if (stage.Flash > 0 && !mild)
                RectFill(0, 0, width, height, new Color(1f, .91f, .71f, stage.Flash * .56f));

            GUI.matrix = matrix;
        }

        void Menu()
        {
            float x = 34, y = (height - 514) / 2;
            Panel(x, y, 367, 514);
            Fill(x + 2, y + 2, 363, 101, Sea);
            Label(x + 22, y + 12, 320, 26, "ИГРОВОЙ КЛУБ", 15, Paper, true);
            Label(x + 20, y + 34, 330, 61, "КАРУСЕЛЬ", 49, Paper, true);
            x += 22;
            y += 117;
            if (link != null)
            {
                Label(x, y, 319, 48, "Подключение…", 27, Dark, true);
                if (Button(x, y + 62, 319, 43, "Отмена"))
                    Leave();
                return;
            }

            if (Button(x, y, 154, 36, "Соло", net ? Muted : Sea))
            {
                net = false;
                notice = "";
            }

            if (Button(x + 162, y, 158, 36, "По сети", net ? Sea : Muted))
            {
                net = true;
                notice = "";
            }

            Label(x, y + 47, 320, 24, "Имя за столом", 16, Dark, true);
            nickname = Input(x, y + 74, 320, nickname, 16);
            if (net)
            {
                if (Button(x, y + 130, 156, 34, "Подключиться", host ? Muted : Sea))
                    host = false;
                if (Button(x + 162, y + 130, 158, 34, "Создать LAN", host ? Sea : Muted))
                    host = true;
                if (host)
                    Label(x, y + 174, 220, 30, "Порт комнаты", 17, Dark, true);
                else
                {
                    Label(x, y + 174, 209, 25, "IP или домен", 16, Dark, true);
                    address = Input(x, y + 201, 211, address, 120);
                }

                if (!host)
                    Label(x + 220, y + 174, 94, 25, "Порт", 16, Dark, true);
                portText = Input(host ? x : x + 220, y + 201, host ? 320 : 100, portText, 5);
            }
            else
            {
                Label(x, y + 133, 315, 28, "Соперники за столом", 17, Dark, true);
                if (Button(x, y + 175, 47, 45, "−", Muted, bots > 1))
                    bots--;
                Label(x + 56, y + 175, 202, 45, bots + " " + (bots == 1 ? "бот" : "бота"), 24, Dark, true, TextAnchor.MiddleCenter);
                if (Button(x + 273, y + 175, 47, 45, "+", Muted, bots < 4))
                    bots++;
            }

            if (Button(x, y + 256, 320, 49, net ? (host ? "Открыть комнату" : "Занять место") : "Начать партию", Vermilion))
                Connect();
            Label(x, y + 310, 320, 45, notice, 14, Vermilion);
            if (Button(x, y + 346, 187, 31, "Настройки", Dark))
                Settings(true);
            if (Button(x + 222, y + 346, 98, 31, "Выход", Muted))
                Application.Quit();
        }

        void MatchUI()
        {
            var self = table.Find(me);
            bool lobby = table.phase == "lobby", end = table.phase == "end";
            Fill(22, 20, 343, 68, Dark);
            Label(38, 24, 307, 25, "КАРУСЕЛЬ", 15, Gold, true);
            Label(38, 46, 307, 31, lobby ? "Комната" : end ? "Партия окончена" : "Раунд " + table.round, 23, Paper, true);
            if (Button(width - 306, 20, 171, 31, "Настройки", Dark))
                Settings(true);
            if (Button(width - 126, 20, 102, 31, "Выйти", Vermilion))
            {
                Leave();
                return;
            }

            int n = table.players.Length;
            float y = height - 190;
            Panel(22, y, width - 44, 166);
            string title = lobby ? "Участники  ·  " + n + " / 5" : end ? "Победитель  ·  " + (table.Find(table.winner)?.name ?? "—") : "Наблюдение";
            Label(42, y + 9, width * .55f, 38, title, 26, Dark, true);
            Label(width * .5f, y + 14, width * .5f - 44, 28, string.IsNullOrEmpty(notice) ? lastMove : notice, 16, Muted, false, TextAnchor.MiddleRight);
            float left = 42;
            for (int i = 0; i < n; i++)
            {
                var p = table.players[i];
                bool spectate = !self.alive && !lobby;
                string suffix = !p.connected ? " (вышел)" : !p.alive ? " ×" : p.id == me ? " · вы" : p.bot ? " · бот" : "";
                bool picked = spectate && watch == p.id;
                bool canSelect = p.alive && spectate;
                if (Button(left + i * 181, y + 54, 174, 37, (picked ? "✓ " : "") + p.name + suffix, picked ? Vermilion : p.alive ? Sea : Muted, !lobby && canSelect))
                {
                    watch = p.id;
                    stage.Watch(watch);
                }
            }

            if (lobby)
            {
                Label(left, y + 105, 740, 36, net && host ? "Адрес для друзей  ·  " + link.LocalAddresses + "   порт " + portText : "Начать партию может ведущий комнаты", 18);
                if (me == table.host && Button(width - 256, y + 104, 211, 40, "Начать партию", Vermilion, n >= 2 && !pending))
                    Action("start");
            }
            else if (end)
            {
                Label(left, y + 105, 560, 36, "За столом остался один участник", 18);
                if (me == table.host && Button(width - 256, y + 104, 211, 40, "Вернуться в лобби", Sea, !pending))
                    Action("lobby");
            }
            else if (!self.alive)
            {
                Label(left, y + 106, 690, 32, "Выберите участника, чтобы следить за ним", 18);
                if (Button(width - 256, y + 104, 211, 40, "Общий план", Sea))
                {
                    watch = 0;
                    stage.Watch(0);
                }
            }
        }
    }
}
