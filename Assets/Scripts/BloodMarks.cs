using System.Collections.Generic;
using UnityEngine;
using static Carousel.Shapes;

namespace Carousel
{
    // Keep settled stains in small static meshes. Only the last batch of each shade changes.
    internal sealed class BloodMarks
    {
        sealed class Batch
        {
            public readonly List<Vector3> vertices = new List<Vector3>();
            public readonly List<int> triangles = new List<int>();
            public MeshFilter filter;
            public Mesh mesh;
            public int count;
            public bool dirty;
        }

        readonly Transform root;
        readonly List<Batch> batches = new List<Batch>();
        readonly Batch[] open = new Batch[3];
        static readonly int[] Shades = { 0x882439, 0xA82439, 0x731E32 };

        public BloodMarks(Transform root) { this.root = root; }

        public void Stamp(Vector3 at, Vector3 normal, float r, System.Random rng)
        {
            int shade = rng.Next(Shades.Length);
            var batch = open[shade];
            if (batch == null || batch.count == 72)
            {
                var group = Group(root, "Брызги", Vector3.zero);
                var renderer = group.gameObject.AddComponent<MeshRenderer>();
                renderer.sharedMaterials = new[] { Paint(Shades[shade]) };
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                batch = new Batch { filter = group.gameObject.AddComponent<MeshFilter>() };
                open[shade] = batch;
                batches.Add(batch);
            }

            bool wall = normal.y == 0;
            Vector3 u = wall ? V(normal.z, 0, -normal.x) : V(1, 0, 0);
            Vector3 v = wall ? Vector3.up : V(0, 0, -normal.y);
            at += normal * (.004f + (float)rng.NextDouble() * .003f);
            float aspect = .57f + (float)rng.NextDouble() * .57f;
            Patch(at, r, r * aspect, 11);
            if (r > .09f)
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = (float)rng.NextDouble() * 2 * Mathf.PI;
                    float distance = r * (1.1f + (float)rng.NextDouble());
                    Patch(at + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * distance, r * .20f, r * .15f, 6);
                }
                if (wall)
                    Patch(at - v * (r * 1.35f), r * .15f, r * 1.48f, 9);
            }
            batch.dirty = true;
            batch.count++;

            void Patch(Vector3 center, float halfWidth, float halfHeight, int sides)
            {
                int first = batch.vertices.Count;
                batch.vertices.Add(center);
                for (int i = 0; i < sides; i++)
                {
                    float a = i * 2f * Mathf.PI / sides;
                    float rough = .7f + (float)rng.NextDouble() * .5f;
                    batch.vertices.Add(center + u * (Mathf.Cos(a) * halfWidth * rough) + v * (Mathf.Sin(a) * halfHeight * rough));
                    batch.triangles.Add(first);
                    batch.triangles.Add(first + 1 + i);
                    batch.triangles.Add(first + 1 + (i + 1) % sides);
                }
            }
        }

        public void Commit()
        {
            foreach (var batch in batches)
            {
                if (!batch.dirty) continue;
                var mesh = new Mesh { name = "Брызги", vertices = batch.vertices.ToArray(), subMeshCount = 1 };
                mesh.SetTriangles(batch.triangles.ToArray(), 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                batch.filter.sharedMesh = mesh;
                if (batch.mesh) Object.Destroy(batch.mesh);
                batch.mesh = mesh;
                batch.dirty = false;
            }
        }

        public void Clear()
        {
            foreach (var batch in batches)
            {
                if (batch.mesh) Object.Destroy(batch.mesh);
                Object.Destroy(batch.filter.gameObject);
            }
            batches.Clear();
            System.Array.Clear(open, 0, open.Length);
        }
    }
}
