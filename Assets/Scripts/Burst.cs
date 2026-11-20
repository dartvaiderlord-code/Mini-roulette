using System.Collections.Generic;
using UnityEngine;
using static Carousel.Shapes;

namespace Carousel
{
    public sealed class Burst
    {
        class Piece
        {
            public Transform t;
            public Vector3 velocity, spin, size;
            public float age, life, delay;
            public int hops;
            public bool fluid;
        }

        readonly List<Piece> pieces = new List<Piece>();
        readonly Transform root;
        public Burst(Transform parent)
        {
            root = Group(parent, "Следы попаданий", Vector3.zero);
        }

        static float Range(System.Random r, float a, float b) => Mathf.Lerp(a, b, (float)r.NextDouble());
        void Toss(Transform t, Vector3 v, bool fluid, System.Random rng)
        {
            pieces.Add(new Piece { t = t, velocity = v, spin = V(Range(rng, -280, 280), Range(rng, -240, 240), Range(rng, -180, 180)), size = t.localScale, life = Range(rng, 5, 9), fluid = fluid, delay = fluid ? Range(rng, 0, .32f) : 0 });
            if (fluid)
                t.gameObject.SetActive(false);
        }

        public void Fragment(Transform part, Vector3 origin, System.Random rng)
        {
            var outward = (part.position - origin).normalized;
            part.SetParent(root, true);
            Toss(part, outward * Range(rng, 1.1f, 3.2f) + Vector3.up * Range(rng, 1.5f, 3.4f), false, rng);
        }

        public void Fountain(Vector3 origin, System.Random rng)
        {
            for (int i = 0; i < 66; i++)
            {
                float size = Range(rng, .032f, .106f);
                int color = i % 3 == 0 ? 0x93233E : 0xC93249;
                var drop = Ball(root, origin, V(size, size * Range(rng, 1.3f, 2.6f), size), color);
                float angle = Range(rng, 0, Mathf.PI * 2), spread = Range(rng, .3f, 2.6f);
                Toss(drop, V(Mathf.Cos(angle) * spread, Range(rng, 2.2f, 5.7f), Mathf.Sin(angle) * spread), true, rng);
            }

            for (int i = 0; i < 6; i++)
            {
                var clump = Group(root, "Мягкий фрагмент", origin);
                for (int j = 0; j < 4; j++)
                    Ball(clump, V((j % 2) * .055f, (j / 2) * .04f, Range(rng, -.045f, .045f)), V(.112f, .075f, .095f), j % 2 == 0 ? 0xBD6572 : 0xE99098);
                Toss(clump, V(Range(rng, -1, 1), Range(rng, .7f, 1.8f), Range(rng, -1, 1)), false, rng);
            }
        }

        public void Tick(float dt)
        {
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                var p = pieces[i];
                if (p.delay > 0)
                {
                    p.delay -= dt;
                    if (p.delay > 0)
                        continue;
                    p.t.gameObject.SetActive(true);
                }
                else if (p.fluid && !p.t.gameObject.activeSelf)
                    p.t.gameObject.SetActive(true);
                p.age += dt;
                if (p.age >= p.life)
                {
                    Object.Destroy(p.t.gameObject);
                    pieces.RemoveAt(i);
                    continue;
                }

                if (p.hops < 3)
                {
                    Vector3 prev = p.t.position;
                    p.velocity += Vector3.down * (9.8f * dt);
                    var next = prev + p.velocity * dt;
                    float deck = Stage.TableHeight;
                    bool over = next.x * next.x + next.z * next.z < Stage.TableRadius * Stage.TableRadius;
                    float ground = over && prev.y >= deck - .002f ? deck : 0;
                    if (next.y <= ground && p.velocity.y <= 0)
                    {
                        next.y = ground + .001f;
                        if (p.fluid)
                        {
                            p.size = V(p.size.x * 2.4f, .011f, p.size.z * 2);
                            p.t.rotation = Quaternion.identity;
                            p.hops = 3;
                        }
                        else
                        {
                            p.velocity = V(p.velocity.x * .59f, -p.velocity.y * .29f, p.velocity.z * .59f);
                            p.spin *= .54f;
                            p.hops++;
                        }
                    }

                    p.t.position = next;
                    if (p.hops < 3)
                        p.t.Rotate(p.spin * dt, Space.World);
                }

                p.t.localScale = p.size * Mathf.Min(1, (p.life - p.age) * 1.7f);
            }
        }

        public void Clear()
        {
            foreach (var p in pieces)
                Object.Destroy(p.t.gameObject);
            pieces.Clear();
        }
    }
}
