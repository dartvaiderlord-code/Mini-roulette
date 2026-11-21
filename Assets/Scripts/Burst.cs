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
            public Vector3 velocity, spin;
            public float radius, bleed, nextDrip;
            public int bounces;
            public bool fluid;
            public System.Random rng;
        }

        class Jet
        {
            public Transform source;
            public readonly Transform[] stream = new Transform[9];
            public Vector3 forward;
            public System.Random rng;
            public int emitted;
            public float age;
        }

        readonly List<Piece> moving = new List<Piece>();
        readonly List<Transform> remains = new List<Transform>();
        readonly Stack<Transform> drops = new Stack<Transform>();
        readonly List<Jet> jets = new List<Jet>();
        readonly Transform root;
        readonly BloodMarks marks;
        public bool Landed { get; private set; }
        float accumulator, nextCommit;
        public Burst(Transform parent)
        {
            root = Group(parent, "Следы попаданий", Vector3.zero);
            marks = new BloodMarks(root);
        }

        static float Range(System.Random rng, float a, float b) => Mathf.Lerp(a, b, (float)rng.NextDouble());
        static Vector3 Scatter(System.Random rng, float side, float low, float high) =>
            V(Range(rng, -side, side), Range(rng, low, high), Range(rng, -side, side));

        void Drop(Vector3 at, Vector3 velocity, float diameter, System.Random rng)
        {
            var t = drops.Count > 0 ? drops.Pop() : Ball(root, at, Vector3.zero, 0xB62E43);
            t.gameObject.SetActive(true);
            t.position = at;
            t.localScale = V(diameter, diameter * 1.7f, diameter);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, velocity.normalized);
            moving.Add(new Piece { t = t, velocity = velocity, fluid = true, radius = diameter, rng = rng });
        }

        void Toss(Transform t, Vector3 velocity, System.Random rng)
        {
            remains.Add(t);
            moving.Add(new Piece { t = t, velocity = velocity, spin = Scatter(rng, 250, -280, 280),
                rng = rng, bleed = Range(rng, 1.6f, 3.3f), radius = Range(rng, .065f, .17f) });
        }

        public void Fragment(Transform part, Vector3 origin, System.Random rng)
        {
            var outward = (part.position - origin).normalized;
            part.SetParent(root, true);
            Toss(part, outward * Range(rng, 2.0f, 5.0f) + Scatter(rng, .9f, 1.3f, 4.2f), rng);
        }

        public void Fountain(Transform neck, Vector3 facing, System.Random rng)
        {
            var jet = new Jet { source = neck, forward = facing, rng = rng };
            for (int i = 0; i < jet.stream.Length; i++)
                jet.stream[i] = Disc(root, neck.position, .02f, .04f, i < 5 ? 0xA52339 : 0xBA3044);
            jets.Add(jet);
            for (int i = 0; i < 21; i++)
            {
                float size = Range(rng, .065f, .16f);
                var piece = Group(root, "Фрагмент", neck.position);
                Ball(piece, Vector3.zero, V(size, size * .74f, size * 1.35f), i % 2 == 0 ? 0xAD4D60 : 0x8C3142);
                Ball(piece, V(size * .34f, size * .2f, 0), V(size * .66f, size * .64f, size), 0xCE7280);
                Toss(piece, Scatter(rng, 4.9f, 2.2f, 6), rng);
            }
        }

        // The connected column follows the wound as the body falls. It tapers into
        // individual ballistic drops as pressure diminishes.
        static void ShapeJet(Jet jet)
        {
            float pulse = .5f + .5f * Mathf.Sin(jet.age * 12.2f);
            float pressure = Mathf.Min(1, jet.age * 12) * Mathf.Min(1, (4.25f - jet.age) * 2.1f);
            pressure = Mathf.Max(0, pressure) * (1 - jet.age * .081f);
            var side = V(jet.forward.z, 0, -jet.forward.x);
            var origin = jet.source.position;
            var launch = Vector3.up * (4.1f + pulse * 1.8f) + jet.forward * 1.2f + side * (.62f * Mathf.Sin(jet.age * 4.4f));
            var prev = origin;
            for (int i = 0; i < jet.stream.Length; i++)
            {
                float fraction = (i + 1f) / jet.stream.Length;
                float t = fraction * (.35f + pulse * .075f) * pressure;
                var point = origin + launch * t + Vector3.down * (4.9f * t * t);
                var segment = jet.stream[i];
                var direction = point - prev;
                float diameter = (.080f + .027f * pulse) * pressure * (1 - .86f * fraction);
                segment.position = (prev + point) * .5f;
                if (direction.sqrMagnitude > .000001f)
                    segment.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
                segment.localScale = V(diameter, direction.magnitude * .52f, diameter);
                prev = point;
            }
        }

        void Emit(Jet jet)
        {
            const float duration = 4.25f;
            var rng = jet.rng;
            float t = jet.age / duration, pulse = .5f + .5f * Mathf.Sin(jet.age * 13);
            int index = jet.emitted++;
            float angle = index * 2.4f + Range(rng, -.3f, .3f);
            var radial = V(Mathf.Cos(angle), 0, Mathf.Sin(angle));
            Vector3 v;
            if (index % 4 == 0)
                v = radial * Range(rng, 8, 13) + Vector3.up * Range(rng, 4, 9);
            else if (index % 5 == 0)
                v = radial * Range(rng, .9f, 3.4f) + Vector3.up * Range(rng, 9.6f, 12);
            else
                v = jet.forward * (1.2f + pulse) + radial * Range(rng, .3f, 2.6f) + Vector3.up * (3.4f + 4.3f * pulse);
            v *= 1f - .53f * t;
            Drop(jet.source.position + Vector3.up * .018f, v, Range(rng, .029f, .088f), rng);
        }

        // Test the swept segment, retaining the first surface even on slow frames.
        static bool Contact(Vector3 from, Vector3 to, out Vector3 point, out Vector3 normal)
        {
            Vector3 delta = to - from, hit = to, axis = Vector3.zero;
            float first = 1.001f;
            void Plane(float start, float step, float level, Vector3 n, float radius = 0, int panel = 0)
            {
                if (step == 0 || step * (n.x + n.y + n.z) >= 0) return;
                float t = (level - start) / step;
                if (t < 0 || t > 1 || t >= first) return;
                var p = from + delta * t;
                if (panel == 1 && p.y > 2.09f || panel == 2 && p.y <= 2.09f) return;
                if (radius > 0 && p.x * p.x + p.z * p.z > radius * radius) return;
                first = t; hit = p; axis = n;
            }

            Plane(from.y, delta.y, .003f, Vector3.up);
            Plane(from.y, delta.y, .07f, Vector3.up, 2.23f);
            Plane(from.y, delta.y, Stage.TableHeight + .012f, Vector3.up, Stage.TableRadius);
            Plane(from.y, delta.y, 1.084f, Vector3.up, .34f);
            Plane(from.y, delta.y, 5.985f, Vector3.down);
            for (int side = -1; side <= 1; side += 2)
            {
                var inward = V(-side, 0, 0);
                Plane(from.x, delta.x, side * 6.88f, inward, 0, 1);
                Plane(from.x, delta.x, side * 6.90f, inward, 0, 2);
            }
            Plane(from.z, delta.z, -8.19f, V(0, 0, 1), 0, 1);
            Plane(from.z, delta.z, -8.30f, V(0, 0, 1), 0, 2);
            Plane(from.z, delta.z, 5.17f, V(0, 0, -1), 0, 1);
            Plane(from.z, delta.z, 5.30f, V(0, 0, -1), 0, 2);
            point = hit; normal = axis;
            return first <= 1;
        }

        void Simulate(float dt)
        {
            for (int i = jets.Count - 1; i >= 0; i--)
            {
                var jet = jets[i];
                jet.age += dt;
                int due = 28 + (int)(Mathf.Min(jet.age, 4.25f) * 52);
                while (jet.emitted < due) Emit(jet);
                if (jet.age >= 4.25f)
                {
                    foreach (var segment in jet.stream) Object.Destroy(segment.gameObject);
                    jets.RemoveAt(i);
                }
                else ShapeJet(jet);
            }
            for (int i = moving.Count - 1; i >= 0; i--)
            {
                var p = moving[i];
                var at = p.t.position;
                if (!p.fluid && p.bleed > 0)
                {
                    p.bleed -= dt;
                    p.nextDrip -= dt;
                    if (p.nextDrip <= 0)
                    {
                        p.nextDrip = Range(p.rng, .23f, .48f);
                        Drop(at + Vector3.up * .07f, p.velocity * .18f + Scatter(p.rng, .38f, .1f, .7f), Range(p.rng, .021f, .050f), p.rng);
                    }
                }
                if (p.bounces >= 3)
                {
                    if (p.bleed <= 0) moving.RemoveAt(i);
                    continue;
                }
                p.velocity += Vector3.down * (9.8f * dt);
                var next = at + p.velocity * dt;
                if (Contact(at, next, out var hit, out var n))
                {
                    marks.Stamp(hit, n, p.radius * Range(p.rng, 1.5f, 3.5f), p.rng);
                    if (p.fluid)
                    {
                        p.t.gameObject.SetActive(false);
                        drops.Push(p.t);
                        moving.RemoveAt(i);
                        continue;
                    }
                    if (p.bounces == 0) Landed = true;
                    float along = p.velocity.x * n.x + p.velocity.y * n.y + p.velocity.z * n.z;
                    p.velocity = (p.velocity - n * (1.45f * along)) * .54f;
                    next = hit + n * .017f;
                    p.spin *= .6f;
                    if (n.y > 0 && ++p.bounces >= 3) p.velocity = Vector3.zero;
                }
                p.t.position = next;
                if (!p.fluid) p.t.Rotate(p.spin * dt, Space.World);
            }
        }

        public void Tick(float dt)
        {
            Landed = false;
            const float step = 1f / 60;
            accumulator += dt;
            while (accumulator >= step)
            {
                Simulate(step);
                accumulator -= step;
            }
            nextCommit -= dt;
            if (nextCommit <= 0)
            {
                marks.Commit();
                nextCommit = .075f;
            }
        }

        public void Clear()
        {
            foreach (var p in moving)
                if (p.fluid) Object.Destroy(p.t.gameObject);
            foreach (var t in drops) Object.Destroy(t.gameObject);
            foreach (var t in remains) Object.Destroy(t.gameObject);
            drops.Clear();
            remains.Clear();
            moving.Clear();
            foreach (var jet in jets)
                foreach (var segment in jet.stream) Object.Destroy(segment.gameObject);
            jets.Clear();
            marks.Clear();
            accumulator = nextCommit = 0;
        }
    }
}
