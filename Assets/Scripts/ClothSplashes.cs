using System;
using System.Collections.Generic;
using UnityEngine;
using static Carousel.Shapes;

namespace Carousel
{
    // Clip small patches to the surface of the shirt and arms. Sharing the parent
    // transform makes the stains follow the player when he leans or falls.
    internal static class ClothSplashes
    {
        struct Spot { public Vector3 center; public float radius; }

        public static Mesh Apply(Transform shirt, Vector3 source, bool direct, System.Random rng)
        {
            var original = shirt.GetComponent<MeshFilter>().sharedMesh;
            var vertices = original.vertices;
            var candidates = new List<int>();
            // The model importer expands each face into three consecutive corners.
            Vector3 localSource = shirt.InverseTransformPoint(source);
            for (int i = 0; i < vertices.Length; i += 3)
            {
                Vector3 a = vertices[i], b = vertices[i+1], c = vertices[i+2];
                Vector3 center = (a+b+c)/3, n = Cross(b-a, c-a);
                if (n.sqrMagnitude > .0000006f && Dot(n, localSource-center) > 0)
                    candidates.Add(i);
            }
            if (candidates.Count == 0) return null;

            int count = direct ? 29 : Math.Max(5, (int)(23 - (shirt.position-source).magnitude * 3));
            var spots = new Spot[count];
            for (int k = 0; k < count; k++)
            {
                int i = candidates[rng.Next(candidates.Count)];
                spots[k] = new Spot { center = (vertices[i]+vertices[i+1]+vertices[i+2])/3,
                    radius = (direct ? .038f : .019f) + (float)rng.NextDouble() * (direct ? .11f : .061f) };
            }

            var corners = new List<Vector3>();
            var faces = new List<int>();
            for (int i = 0; i < vertices.Length; i += 3)
            {
                Vector3 a = vertices[i], b = vertices[i+1], c = vertices[i+2];
                Vector3 center = (a+b+c)/3;
                float extent = Mathf.Max((a-center).magnitude, Mathf.Max((b-center).magnitude,(c-center).magnitude));
                Vector3 n = Cross(b-a,c-a).normalized;
                foreach (var spot in spots)
                {
                    float reach = extent + spot.radius;
                    if ((spot.center-center).sqrMagnitude > reach*reach) continue;
                    PaintFace(a,b,center,spot,n); PaintFace(b,c,center,spot,n); PaintFace(c,a,center,spot,n);
                }
            }
            if (corners.Count == 0) return null;
            var mesh = new Mesh { name = "Брызги на ткани", vertices = corners.ToArray(), subMeshCount = 1 };
            mesh.SetTriangles(faces.ToArray(), 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var node = Group(shirt, mesh.name, Vector3.zero);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = node.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { Paint(direct ? 0x781F32 : 0x92273B) };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return mesh;

            void PaintFace(Vector3 a, Vector3 b, Vector3 c, Spot s, Vector3 n)
            {
                float fa = s.radius-(a-s.center).magnitude;
                float fb = s.radius-(b-s.center).magnitude;
                float fc = s.radius-(c-s.center).magnitude;
                if (fa<0 && fb<0 && fc<0) return;
                int first = corners.Count;
                Edge(c,a,fc,fa); Edge(a,b,fa,fb); Edge(b,c,fb,fc);
                for (int j=first+2; j<corners.Count; j++)
                {
                    faces.Add(first); faces.Add(j-1); faces.Add(j);
                }
                void Edge(Vector3 from, Vector3 to, float f, float t)
                {
                    if ((f<0) != (t<0)) corners.Add(Vector3.Lerp(from,to,f/(f-t)) + n*.002f);
                    if (t>=0) corners.Add(to + n*.002f);
                }
            }
        }

        static float Dot(Vector3 a, Vector3 b) => a.x*b.x + a.y*b.y + a.z*b.z;
        static Vector3 Cross(Vector3 a, Vector3 b) => V(a.y*b.z-a.z*b.y, a.z*b.x-a.x*b.z, a.x*b.y-a.y*b.x);
    }
}
