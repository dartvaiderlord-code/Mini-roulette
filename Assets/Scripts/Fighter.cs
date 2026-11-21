using System.Collections.Generic;
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
            0xC97E59,
            0xA86545,
            0xE0A678,
            0x925B44,
            0xD89770
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
        readonly List<Mesh> generated = new List<Mesh>();
        readonly Transform ring;
        readonly Quaternion lean;
        Transform stain, tableStain;
        float fallTime;
        public Fighter(Transform parent, Vector3 at, int i)
        {
            Index = i;
            int shirt = Colors[i], skin = Skin[i], hair = Hair[i];
            Root = Group(parent, "Участник " + (i + 1), at);
            Root.LookAt(new Vector3(0, at.y, 0));
            ring = Disc(Root, V(0, .03f, 0), .78f, .035f, shirt);
            Disc(Root, V(0, .1f, 0), .52f, .12f, 0x435253);
            Disc(Root, V(0, .52f, 0), .42f, .14f, shirt);
            Rod(Root, V(0, .1f, 0), V(0, .5f, 0), .1f, 0x827965);
            for (int s = -1; s <= 1; s += 2)
            {
                Ball(Root, V(.28f * s, .67f, .2f), V(.45f, .46f, .79f), 0x36474B);
                Ball(Root, V(.31f * s, .41f, .45f), V(.38f, .55f, .37f), 0x36474B);
                Box(Root, V(.31f * s, .16f, .56f), V(.4f, .25f, .53f), 0x453B36);
                Box(Root, V(.31f * s, .065f, .57f), V(.42f, .07f, .56f), 0x252E31);
            }

            Torso = Group(Root, "Корпус", V(0, .76f, 0));
            lean = Quaternion.Euler(55, -12, 26);
            Ball(Torso, V(0, .38f, 0), V(.88f, .9f, .59f), shirt);
            Ball(Torso, V(0, .73f, 0), V(1.29f, .78f, .67f), shirt);
            Box(Torso, V(0, .015f, .02f), V(.69f, .14f, .46f), 0x3C3731);
            Box(Torso, V(0, .02f, .255f), V(.19f, .12f, .04f), 0xDCA84B);
            for (int s = -1; s <= 1; s += 2)
            {
                Ball(Torso, V(.27f * s, .81f, .18f), V(.62f, .43f, .34f), shirt);
                var strap = Box(Torso, V(.36f * s, .78f, .29f), V(.105f, .56f, .08f), 0x38484B);
                strap.localRotation = Quaternion.Euler(-12, 0, 13 * s);
                Ball(Torso, V(.62f * s, .8f, 0), V(.54f, .53f, .55f), shirt);
                Ball(Torso, V(.74f * s, .57f, .09f), V(.45f, .57f, .43f), skin);
                var arm = Ball(Torso, V(.72f * s, .35f, .26f), V(.42f, .39f, .59f), skin);
                arm.localRotation = Quaternion.Euler(-12, 14 * s, 0);
                Ball(Torso, V(.66f * s, .36f, .49f), V(.39f, .32f, .37f), 0x4C433D);
                for (int k = 0; k < 3; k++)
                    Box(Torso, V(.57f * s + k * .06f * s, .4f, .64f), V(.044f, .092f, .05f), 0x796552);
            }

            Hand = Group(Torso, "Захват", V(.61f, .5f, .58f));
            Rod(Torso, V(0, .91f, .02f), V(0, 1.18f, .02f), .215f, skin);
            Head = Group(Torso, "Голова", V(0, 1.4f, .025f));
            BuildShell(skin);
            for (int s = -1; s <= 1; s += 2)
            {
                Ball(Head, V(s * .288f, -.015f, -.005f), V(.13f, .19f, .13f), skin);
                Ball(Head, V(s * .111f, .032f, .207f), V(.112f, .065f, .064f), 0xF3DFC0);
                Ball(Head, V(s * .106f, .035f, .238f), V(.043f, .05f, .02f), 0x262A29);
                var brow = Box(Head, V(s * .124f, .098f, .208f), V(.167f, .053f, .071f), hair);
                brow.localRotation = Quaternion.Euler(0, s * 12, s * 12);
                Ball(Head, V(s * .16f, -.065f, .15f), V(.22f, .15f, .15f), skin);
            }

            Ball(Head, V(0, -.021f, .235f), V(.129f, .136f, .128f), skin);
            Box(Head, V(0, -.163f, .19f), V(.16f, .021f, .025f), 0x824B40);
            if (i % 2 == 0)
            {
                for (int j = -1; j <= 1; j++)
                    Ball(Head, V(j * .085f, -.101f, .211f), V(.13f, .068f, .07f), hair);
            }

            Ball(Head, V(0, .238f, -.035f), V(.56f, .178f, .465f), hair);
            if (i == 1 || i == 4)
            {
                Ball(Head, V(0, .255f, -.024f), V(.61f, .19f, .505f), shirt);
                Box(Head, V(0, .218f, .201f), V(.52f, .05f, .22f), shirt);
            }
            else
                for (int j = 0; j < 4; j++)
                {
                    var tuft = Box(Head, V(-.17f + j * .098f, .29f, -.005f), V(.125f, .114f, .31f), hair);
                    tuft.localRotation = Quaternion.Euler(-9, 0, -14);
                }
        }

        // Each wedge is a closed mesh with an exterior skin and two interior cut faces.
        void BuildShell(int skin)
        {
            float[] y =
            {
                -.275f,
                -.19f,
                .065f,
                .22f,
                .28f
            };
            float[] radius =
            {
                .13f,
                .26f,
                .299f,
                .245f,
                .12f
            };
            const int slices = 9;
            for (int band = 0; band < 4; band++)
                for (int j = 0; j < slices; j++)
                {
                    float a = j * Mathf.PI * 2 / slices, b = (j + 1) * Mathf.PI * 2 / slices;
                    Vector3 lo = V(0, y[band], 0), hi = V(0, y[band + 1], 0);
                    Vector3 u = V(Mathf.Sin(a), 0, Mathf.Cos(a) * .78f), v = V(Mathf.Sin(b), 0, Mathf.Cos(b) * .78f);
                    Vector3[] verts =
                    {
                        lo,
                        lo + u * radius[band],
                        lo + v * radius[band],
                        hi,
                        hi + u * radius[band + 1],
                        hi + v * radius[band + 1]
                    };
                    var center = (verts[0] + verts[1] + verts[2] + verts[3] + verts[4] + verts[5]) / 6;
                    for (int k = 0; k < verts.Length; k++)
                        verts[k] -= center;
                    var mesh = new Mesh
                    {
                        name = "Сектор",
                        vertices = verts,
                        subMeshCount = 2
                    };
                    mesh.SetTriangles(new[] { 1, 2, 5, 1, 5, 4, 0, 2, 1, 3, 4, 5 }, 0);
                    mesh.SetTriangles(new[] { 0, 1, 4, 0, 4, 3, 2, 0, 3, 2, 3, 5 }, 1);
                    mesh.RecalculateNormals();
                    mesh.RecalculateBounds();
                    generated.Add(mesh);
                    var t = Group(Head, "Фрагмент", center);
                    t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                    t.gameObject.AddComponent<MeshRenderer>().sharedMaterials = new[]
                    {
                        Paint(skin),
                        Paint(0xA73B43)
                    };
                }
        }

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

        public void Pose(bool active, float time)
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
            float w = active ? 1.08f + .05f * Mathf.Sin(time * 3) : 1;
            ring.localScale = V(1.56f * w, .0175f, 1.56f * w);
        }

        public void Release()
        {
            foreach (var mesh in generated)
                Object.Destroy(mesh);
            Object.Destroy(Root.gameObject);
        }
    }
}
