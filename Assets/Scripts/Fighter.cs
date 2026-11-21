using System;
using UnityEngine;
using static Carousel.Shapes;

namespace Carousel
{
    public class Fighter
    {
        public static readonly int[] Colors =
        {
            0xD45032,
            0x277F82,
            0xE5AA36,
            0x6269A8,
            0xA14C64
        };
        static readonly int[] Skin =
        {
            0xC68F72,
            0xAE795D,
            0xD5A885,
            0x976E59,
            0xC99579
        };
        static readonly int[] Hair =
        {
            0x492F28,
            0x261F22,
            0x795040,
            0x302A29,
            0x563C2E
        };
        public readonly Transform Root, Torso, Head, Hand;
        public bool Fallen { get; private set; }

        public readonly int Index;

        readonly Transform ring, restingHand;
        readonly Quaternion lean;
        Transform stain, tableStain;
        float fallTime;
        static readonly int[] Shirts = { 0xA95347, 0x426C70, 0xAB8252, 0x546781, 0x78525E };
        static readonly int[] Trousers = { 0x3E4C54, 0x484D45, 0x414954, 0x504B48, 0x384B50 };
        static readonly float[] HeadWidths = { 1.09f, 1f, .97f, 1.04f, 1.08f };
        static readonly int[] Cuts = { 0, 1, 0, 2, 2 };

        public int Palette(string material, int fallback)
        {
            int skin = Skin[Index], hair = Hair[Index], shirt = Shirts[Index];
            switch (material)
            {
                case "shirt": return shirt;
                case "trim": return Mix(shirt, 0x292C31, .34f);
                case "denim": return Trousers[Index];
                case "denimLight": return Mix(Trousers[Index], 0xBDAC97, .19f);
                case "skin": return skin;
                case "lip": return Mix(skin, 0x955858, .56f);
                case "hair": return hair;
                case "hairLight": return Mix(hair, 0xA69784, .12f);
                case "beard": return Mix(hair, skin, .27f);
                default: return fallback;
            }
        }

        static int Mix(int a, int b, float fraction)
        {
            var tint = Color.Lerp(Ink(a), Ink(b), fraction);
            return ((int)(255 * tint.r) << 16) | ((int)(255 * tint.g) << 8) | (int)(255 * tint.b);
        }

        public Fighter(Transform parent, Vector3 at, int i)
        {
            Index = i;
            Root = Group(parent, "Участник " + (i + 1), at);
            Root.LookAt(new Vector3(0, at.y, 0));
            ring = Disc(Root, V(0, .03f, 0), .78f, .035f, Colors[i]);
            Disc(Root, V(0, .1f, 0), .52f, .12f, 0x435253);
            Disc(Root, V(0, .52f, 0), .42f, .14f, Colors[i]);
            Rod(Root, V(0, .1f, 0), V(0, .5f, 0), .1f, 0x827965);

            var model = ModelLibrary.Load("Fighter");
            model.Attach("lower", Root, Palette);
            Torso = Group(Root, "Корпус", V(0, .76f, 0));
            model.Attach("torso", Torso, Palette);
            restingHand = model.Attach("restingHand", Torso, Palette);
            lean = Quaternion.Euler(55, -12, 26);
            Hand = Group(Torso, "Захват", V(.451f, .341f, .625f));
            Head = Group(Torso, "Голова", V(0, 1.4f, .025f));
            Head.localScale = V(HeadWidths[i], 1, 1);
            model.AttachHead(Head, Palette);
            model.Attach("features", Head, Palette);
            model.Attach("hair" + Cuts[i], Head, Palette);
            if (i == 0 || i == 4) model.Attach("beard0", Head, Palette);
            if (i == 2) model.Attach("beard1", Head, Palette);
        }

        public void HoldProp(bool holding) => restingHand.gameObject.SetActive(!holding);

        public void Drop(bool animated, System.Random rng, Burst burst)
        {
            if (Fallen)
                return;
            Fallen = true;
            if (animated)
            {
                while (Head.childCount > 0)
                    burst.Fragment(Head.GetChild(Head.childCount - 1), Head.position, rng);
                burst.Fountain(Head.position, rng);
            }

            if (animated)
            {
                fallTime = Time.time;
                stain = Group(Root, "Лужа под креслом", V(0, .061f, .21f));
                Ball(stain, Vector3.zero, V(.94f, .016f, .78f), 0x742638);
                for (int i = 0; i < 10; i++)
                {
                    float a = i * Mathf.PI / 5;
                    float radius = .32f + .16f * (float)rng.NextDouble();
                    var lobe = Ball(stain, V(Mathf.Sin(a) * radius, -.001f, Mathf.Cos(a) * radius), V(.34f + .3f * (float)rng.NextDouble(), .014f, .36f), 0x742638);
                    lobe.Rotate(0, i * 36, 0);
                }

                Ball(stain, V(.06f, .006f, .07f), V(.53f, .015f, .39f), 0x8E2D40);
                stain.localScale = V(.08f, 1, .08f);
                tableStain = Group(Root, "Пятно на столе", V(.03f, Stage.TableHeight + .015f - Root.position.y, .81f));
                Ball(tableStain, Vector3.zero, V(.65f,.009f,.31f),0x8D293E);
                for (int i=0;i<5;i++)
                {
                    float offset = (float)rng.NextDouble()*.32f-.16f;
                    var trail = Ball(tableStain,V(offset,-.001f,.12f+i*.045f),V(.19f-i*.018f,.007f,.19f),0x8D293E);
                    trail.Rotate(0,offset*120,0);
                }
                tableStain.localScale = V(.05f,1,.05f);
            }

            Head.gameObject.SetActive(false);
        }

        public void Pose(bool active, float time, Vector3 attention)
        {
            if (stain)
            {
                float elapsed = Mathf.Max(0, time - fallTime);
                float spread = .08f + 1.18f * (1 - Mathf.Exp(-elapsed * .34f));
                stain.localScale = V(spread, 1, spread);
                float seep=.05f+.95f*(1-Mathf.Exp(-elapsed*.52f));
                tableStain.localScale=V(seep,1,seep);
            }

            var rotation = Fallen ? lean : Quaternion.Euler(Mathf.Sin(time * 1.5f + Index) * 1.6f, 0, 0);
            Torso.localRotation = Quaternion.Slerp(Torso.localRotation, rotation, Time.deltaTime * 6);
            if (!Fallen)
            {
                var forward = Root.forward;
                var direction = attention - Root.position;
                float angle = (float)(Math.Atan2(forward.z * direction.x - forward.x * direction.z,
                    forward.x * direction.x + forward.z * direction.z) * 180 / Math.PI);
                float glance = Math.Max(-34, Math.Min(34, angle));
                Head.localRotation = Quaternion.Slerp(Head.localRotation, Quaternion.Euler(0, glance, 0), Time.deltaTime * 4);
            }
            float w = active ? 1.08f + .05f * Mathf.Sin(time * 3) : 1;
            ring.localScale = V(1.56f * w, .0175f, 1.56f * w);
        }

        public void Release()
        {
            UnityEngine.Object.Destroy(Root.gameObject);
        }
    }
}
