using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using static Carousel.Shapes;

namespace Carousel
{
    // Shared subset of Wavefront OBJ used by the two desktop players. Objects are independently
    // attachable. Their vertices stay in the authored coordinate system when the pivot is centered.
    internal sealed class ModelLibrary
    {
        static readonly Dictionary<string, ModelLibrary> models = new Dictionary<string, ModelLibrary>();
        static readonly Dictionary<string, Material> metals = new Dictionary<string, Material>();
        readonly Dictionary<string, Part> parts = new Dictionary<string, Part>();

        sealed class Part
        {
            public Vector3 pivot;
            public Mesh mesh;
            public string[] finishes;
        }

        public static ModelLibrary Load(string name)
        {
            if (!models.TryGetValue(name, out var model))
            {
                var path = Path.Combine(Application.streamingAssetsPath, "Models", name + ".obj");
                models.Add(name, model = new ModelLibrary(path));
            }
            return model;
        }

        ModelLibrary(string path)
        {
            var positions = new List<Vector3>();
            var local = new List<Vector3>();
            var groups = new Dictionary<string, List<int>>();
            List<int> triangles = null;
            string name = null;
            void Finish()
            {
                if (name == null) return;
                var pivot = Vector3.zero;
                foreach (var point in local) pivot += point;
                pivot /= local.Count;
                for (int i = 0; i < local.Count; i++) local[i] -= pivot;
                var mesh = new Mesh { name = name, vertices = local.ToArray(), subMeshCount = groups.Count };
                var finishes = new List<string>();
                foreach (var entry in groups)
                {
                    mesh.SetTriangles(entry.Value.ToArray(), finishes.Count);
                    finishes.Add(entry.Key);
                }
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                parts.Add(name, new Part { mesh = mesh, pivot = pivot, finishes = finishes.ToArray() });
                local.Clear();
                groups.Clear();
                triangles = null;
            }

            foreach (string line in File.ReadLines(path))
            {
                var fields = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (fields.Length == 0) continue;
                switch (fields[0])
                {
                    case "o":
                        Finish();
                        name = fields[1];
                        break;
                    case "v":
                        positions.Add(V(Number(fields[1]), Number(fields[2]), Number(fields[3])));
                        break;
                    case "usemtl":
                        if (!groups.TryGetValue(fields[1], out triangles))
                            groups.Add(fields[1], triangles = new List<int>());
                        break;
                    case "f":
                        // Duplicate facet corners so Unity and raylib produce matching flat normals.
                        for (int i = 1; i <= 3; i++)
                        {
                            triangles.Add(local.Count);
                            local.Add(positions[int.Parse(fields[i], CultureInfo.InvariantCulture) - 1]);
                        }
                        break;
                }
            }
            Finish();
        }

        static float Number(string value) => float.Parse(value, CultureInfo.InvariantCulture);

        static Material FinishMaterial(string finish, Func<string, int, int> palette)
        {
            int split = finish.LastIndexOf('_');
            string kind = finish.Substring(0, split);
            int color = int.Parse(finish.Substring(split + 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            if (palette != null) color = palette(kind, color);
            if (kind != "steel" && kind != "blue" && kind != "edge" && kind != "brass")
                return Paint(color);
            if (!metals.TryGetValue(finish, out var metal))
            {
                metal = new Material(Paint(color)) { color = Ink(color) };
#if CAROUSEL_PORTABLE
                metal.sheen = .78f;
#else
                metal.SetFloat("_Metallic", .63f);
                metal.SetFloat("_Glossiness", .58f);
#endif
                metals.Add(finish, metal);
            }
            return metal;
        }

        public Transform Attach(string name, Transform parent, Func<string, int, int> palette = null)
        {
            var part = parts[name];
            var at = Group(parent, name, part.pivot);
            at.gameObject.AddComponent<MeshFilter>().sharedMesh = part.mesh;
            at.gameObject.AddComponent<MeshRenderer>().sharedMaterials =
                Array.ConvertAll(part.finishes, finish => FinishMaterial(finish, palette));
            return at;
        }

        public void Repaint(string name, Transform target, Func<string, int, int> palette)
        {
            target.GetComponent<MeshRenderer>().sharedMaterials =
                Array.ConvertAll(parts[name].finishes, finish => FinishMaterial(finish, palette));
        }

        public static void Release()
        {
            foreach (var model in models.Values)
                foreach (var part in model.parts.Values)
                    UnityEngine.Object.Destroy(part.mesh);
            models.Clear();
            foreach (var metal in metals.Values) UnityEngine.Object.Destroy(metal);
            metals.Clear();
        }

        public void AttachHead(Transform head, Func<string, int, int> palette)
        {
            foreach (var name in parts.Keys)
                if (name.StartsWith("head_", StringComparison.Ordinal))
                    Attach(name, head, palette);
        }
    }
}
