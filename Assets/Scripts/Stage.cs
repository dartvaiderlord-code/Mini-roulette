using System;
using System.Collections.Generic;
using UnityEngine;
using static Carousel.Shapes;

namespace Carousel
{
    public sealed partial class Stage : MonoBehaviour
    {
        public const float TableRadius = 1.54f, TableHeight = 1.025f;
        const int Cream = 0xFFF0C6, Teal = 0x458D8C, Red = 0xCB5546, Yellow = 0xEFC25F;
        public Camera Eye { get; private set; }
        public float Pulse { get; private set; }

        public bool Mild;
        readonly Dictionary<int, Fighter> cast = new Dictionary<int, Fighter>();
        Transform prop, rotor, flash;
        Light flashLamp;
        float recoil;
        Burst burst;
        AudioSource speaker;
        AudioClip pop, click, whirr;
        int active, focus, propTarget, propActor;
        Impact queued;
        float resolveAt;
        Vector3 cameraBase, gaze;
        float shake, step, spinUntil;
        public void Setup()
        {
            var cam = Group(transform, "Камера", Vector3.zero);
            Eye = cam.gameObject.AddComponent<Camera>();
            Eye.depth = 10;
            Eye.fieldOfView = 41;
            Eye.nearClipPlane = .08f;
            Eye.farClipPlane = 60;
            Eye.clearFlags = CameraClearFlags.SolidColor;
            Eye.backgroundColor = Ink(0xF8D8A2);
            cam.gameObject.AddComponent<AudioListener>();
            speaker = cam.gameObject.AddComponent<AudioSource>();
            speaker.spatialBlend = 0;
            pop = Tone(.32f, 0);
            click = Tone(.085f, 1);
            whirr = Tone(.23f, 2);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Ink(0xEBDBC2);
            RenderSettings.ambientEquatorColor = Ink(0xC6B599);
            RenderSettings.ambientGroundColor = Ink(0x9D9792);
            var light = Group(transform, "Дневной свет", V(-4, 6, -3));
            light.rotation = Quaternion.Euler(48, -29, 0);
            var sun = light.gameObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Ink(0xFFF1D2);
            sun.intensity = 1.07f;
            sun.shadows = LightShadows.Soft;
            Room();
            ToyTable();
            prop = MakeProp();
            flash = Ball(prop, V(0, .067f, .39f), V(.13f, .13f, .24f), Yellow);
            flashLamp = flash.gameObject.AddComponent<Light>();
            flashLamp.type = LightType.Point;
            flashLamp.color = Ink(0xFFC16A);
            flashLamp.range = 2.5f;
            flash.gameObject.SetActive(false);
            burst = new Burst(transform);
            cameraBase = V(0, 3.22f, -6.85f);
            gaze = V(0, 1.26f, .45f);
            cam.position = cameraBase;
            cam.LookAt(gaze);
        }

        static AudioClip Tone(float duration, int kind)
        {
            const int rate = 22050;
            var samples = new float[(int)(duration * rate)];
            var rng = new System.Random(20 + kind);
            for (int i = 0; i < samples.Length; i++)
            {
                float t = (float)i / rate, envelope = Mathf.Exp(-t * (kind == 1 ? 54 : 14));
                float noise = (float)rng.NextDouble() * 2 - 1;
                float wave = kind == 0 ? Mathf.Sin(2 * Mathf.PI * (102 * t - 65 * t * t)) * .72f + noise * .28f : kind == 1 ? noise * .7f + Mathf.Sin(t * 2600) * .3f : noise * Mathf.Pow(Mathf.Abs(Mathf.Sin(t * 67)), 6);
                samples[i] = envelope * wave * .6f;
            }

            var clip = AudioClip.Create("Аркада " + kind, samples.Length, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        void Room()
        {
            Box(transform, V(0, -.16f, 0), V(22, .3f, 22), 0xE8CD99);
            for (int x = -5; x <= 5; x++)
                for (int z = -4; z <= 4; z++)
                    if ((x + z) % 2 == 0)
                        Box(transform, V(x * 1.5f, -.005f, z * 1.5f), V(1.49f, .011f, 1.49f), 0xE4BD8B);
            Box(transform, V(0, 3.3f, 5.4f), V(22, 6.6f, .2f), 0xF0D9A7);
            Box(transform, V(0, 1.0f, 5.24f), V(22, 2, .14f), 0x6A9F9A);
            Box(transform, V(0, 2.02f, 5.13f), V(22, .105f, .15f), Cream);
            for (int i = -12; i <= 12; i++)
                Box(transform, V(i * .7f, .98f, 5.13f), V(.028f, 1.94f, .05f), 0x578C88);
            for (int s = -1; s <= 1; s += 2)
            {
                Box(transform, V(s * 7, 3, 0), V(.2f, 6, 11), 0xF3DEAD);
                Box(transform, V(s * 7, 1, 0), V(.24f, 2, 11), 0x75A4A0);
                for (int j = 0; j < 3; j++)
                {
                    float x = s * (3.65f + j * .41f);
                    var block = Box(transform, V(x, .37f + (j == 1 ? .64f : 0), 3.12f), V(.68f, .7f, .68f), Fighter.Colors[(j + (s + 1)) % 5]);
                    block.Rotate(0, j * 14, 0);
                    Text(block, ((char)('А' + j)).ToString(), V(0, 0, -.51f), .12f, Cream);
                }
            }

            // Painted wooden sun, pennants and a framed club sign on the back wall.
            var sun = Group(transform, "Солнце", V(-4.35f, 3.35f, 5.02f));
            Ball(sun, Vector3.zero, V(1.25f, 1.25f, .11f), Yellow);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6;
                var ray = Box(sun, V(Mathf.Sin(a) * .86f, Mathf.Cos(a) * .86f, 0), V(.14f, .29f, .09f), Yellow);
                ray.localRotation = Quaternion.Euler(0, 0, -i * 30);
            }

            Box(transform, V(0, 3.25f, 5.02f), V(4.65f, 1.17f, .13f), 0xA8805F);
            Box(transform, V(0, 3.28f, 4.93f), V(4.53f, 1.06f, .1f), Teal);
            Text(transform, "К А Р У С Е Л Ь", V(0, 3.33f, 4.85f), .094f, Cream);
            for (int j = 0; j < 19; j++)
            {
                float x = -6.3f + j * .7f, y = 4.33f + .015f * x * x;
                Rod(transform, V(x, y, 4.8f), V(x + .7f, 4.33f + .015f * (x + .7f) * (x + .7f), 4.8f), .014f, 0x796B57);
                var flag = Box(transform, V(x + .25f, y - .14f, 4.79f), V(.25f, .25f, .042f), Fighter.Colors[j % 5]);
                flag.Rotate(0, 0, 45);
            }
        }

        void ToyTable()
        {
            Disc(transform, V(0, .028f, 0), 2.23f, .045f, Teal);
            Disc(transform, V(0, .055f, 0), 2.04f, .02f, 0x87AF9F);
            for (int s = -1; s <= 1; s += 2)
                for (int k = -1; k <= 1; k += 2)
                    Rod(transform, V(s * .94f, .05f, k * .94f), V(s * .75f, .94f, k * .75f), .105f, Yellow);
            Disc(transform, V(0, .91f, 0), TableRadius, .18f, Red);
            Disc(transform, V(0, 1.005f, 0), TableRadius, .04f, 0xF0C76F);
            Disc(transform, V(0, 1.029f, 0), .91f, .011f, 0xEAB862);
            for (int i = 0; i < 42; i++)
            {
                float a = i * Mathf.PI / 21;
                var mark = Box(transform, V(Mathf.Sin(a) * 1.36f, 1.029f, Mathf.Cos(a) * 1.36f), V(.032f, .008f, i % 7 == 0 ? .14f : .075f), Cream);
                mark.Rotate(0, i * 360f / 42, 0);
            }

            Disc(transform, V(0, 1.05f, 0), .34f, .035f, Teal);
            Disc(transform, V(0, 1.073f, 0), .235f, .018f, Cream);
        }

        Transform MakeProp()
        {
            var t = Group(transform, "Аркадный револьвер", V(0, 1.34f, 0));
            var grip = Box(t, V(0, -.095f, -.085f), V(.092f, .23f, .12f), 0x795345);
            grip.Rotate(16, 0, 0);
            Box(t, V(0, .038f, .012f), V(.128f, .13f, .29f), 0x424E54);
            Rod(t, V(0, .067f, .08f), V(0, .067f, .365f), .055f, 0x66777B);
            var hub = Group(t, "Индексатор", V(0, .035f, .016f));
            rotor = hub;
            Rod(hub, V(0, 0, -.068f), V(0, 0, .068f), .089f, 0xB39758);
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3;
                Ball(hub, V(.083f * Mathf.Sin(a), .083f * Mathf.Cos(a), 0), V(.025f, .025f, .1f), 0xDBC989);
            }

            return t;
        }

        public void Reseat(Seat[] seats)
        {
            burst.Clear();
            foreach (var f in cast.Values)
                f.Release();
            cast.Clear();
            focus = 0;
            active = 0;
            propTarget = 0;
            queued = null;
            step = spinUntil = Pulse = shake = 0;
            for (int i = 0; i < seats.Length; i++)
            {
                float a = Mathf.Lerp(-83, 83, (i + .22f) / (seats.Length - .56f)) * Mathf.Deg2Rad;
                var f = new Fighter(transform, V(Mathf.Sin(a) * 2.05f, .03f, Mathf.Cos(a) * 2.05f), i);
                cast.Add(seats[i].id, f);
                if (!string.IsNullOrEmpty(seats[i].name))
                {
                    var badge = Group(f.Root, "Табличка", V(0, 2.64f, 0));
                    badge.rotation = Quaternion.identity;
                    Box(badge, Vector3.zero, V(1.5f, .24f, .05f), 0x293D3F);
                    Box(badge, V(-.7f, 0, -.031f), V(.052f, .22f, .025f), Fighter.Colors[i]);
                    Text(badge, seats[i].name, V(.02f, 0, -.032f), .024f, Cream);
                }

                if (!seats[i].alive)
                    f.Drop(false, null, burst);
            }
        }

        public void Show(TableState state, bool play)
        {
            active = state.turn;
            var shot = state.shot;
            if (play && shot != null)
            {
                ResolveImpact();
                queued = shot;
                propActor = shot.actor;
                propTarget = shot.target;
                resolveAt = Time.time + (shot.spin ? .38f : .18f);
                step = resolveAt + .34f;
                if (shot.spin)
                {
                    spinUntil = resolveAt;
                    speaker.PlayOneShot(whirr, .55f);
                }
            }

            foreach (var p in state.players)
                if (!p.alive && !(queued != null && queued.hit && queued.target == p.id) && cast.TryGetValue(p.id, out var f))
                    f.Drop(false, null, burst);
        }

        void ResolveImpact()
        {
            if (queued == null)
                return;
            var shot = queued;
            queued = null;
            speaker.PlayOneShot(shot.hit ? pop : click, shot.hit ? .8f : .6f);
            if (shot.hit && cast.TryGetValue(shot.target, out var target))
            {
                target.Drop(true, new System.Random(shot.seed), burst);
                recoil = 1;
                Pulse = 1;
                shake = 1;
            }
        }

        public void Watch(int id)
        {
            focus = id;
        }

        void Update()
        {
            if (!Eye)
                return;
            if (queued != null && Time.time >= resolveAt)
                ResolveImpact();
            float dt = Mathf.Min(Time.deltaTime, .05f);
            recoil = Mathf.Max(0, recoil - dt * 4.8f);
            flash.gameObject.SetActive(recoil > .6f);
            if (recoil > .6f)
                flashLamp.intensity = recoil * 2;
            foreach (var f in cast)
                f.Value.Pose(f.Key == active, Time.time);
            burst.Tick(dt);
            Pulse = Mathf.Max(0, Pulse - dt * 1.35f);
            shake = Mathf.Max(0, shake - dt * 2);
            var origin = cameraBase;
            var look = gaze;
            if (focus != 0 && cast.TryGetValue(focus, out var subject))
            {
                look = Vector3.Lerp(look, subject.Torso.position + Vector3.up * .57f, .64f);
                origin += V(look.x * .36f, 0, .82f);
            }

            var wobble = V(Mathf.Sin(Time.time * 67), Mathf.Cos(Time.time * 73), 0) * shake * shake * (Mild ? 0 : .085f);
            Eye.transform.position = Vector3.Lerp(Eye.transform.position, origin, dt * 6) + wobble;
            Eye.transform.rotation = Quaternion.Slerp(Eye.transform.rotation, Quaternion.LookRotation(look - Eye.transform.position), dt * 8);
            if (spinUntil > Time.time)
                rotor.Rotate(0, 0, dt * 1000);
            Vector3 pos = V(0, 1.27f, 0), dir = V(.7f, .04f, .25f);
            if (cast.TryGetValue(Time.time < step ? propActor : active, out var holder))
            {
                pos = holder.Hand.position;
                dir = -holder.Root.position;
                dir.y = 0;
            }

            if (Time.time < step && cast.TryGetValue(propTarget, out var target))
                dir = target.Head.position - pos;
            pos -= prop.forward * (.13f * recoil);
            prop.position = Vector3.Lerp(prop.position, pos, Mathf.Min(1, dt * 13));
            if (dir.sqrMagnitude > .001f)
                prop.rotation = Quaternion.Slerp(prop.rotation, Quaternion.LookRotation(dir), dt * 12);
        }
    }
}
