using System.Collections.Generic;
using UnityEngine;

namespace Carousel
{
    public static class Shapes
    {
        static readonly Dictionary<int, Material> paints = new Dictionary<int, Material>();
        public static Color Ink(int rgb) => new Color32((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb, 255);
        public static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
        public static Material Paint(int rgb)
        {
            if (!paints.TryGetValue(rgb, out var paint) || !paint)
            {
                paint = new Material(Shader.Find("Standard"))
                {
                    color = Ink(rgb)
                };
                paint.SetFloat("_Glossiness", .27f);
                paints[rgb] = paint;
            }

            return paint;
        }

        public static Transform Group(Transform parent, string name, Vector3 at)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            return t;
        }

        static Transform Make(Transform parent, string name, PrimitiveType kind, Vector3 at, Vector3 size, int color)
        {
            var g = GameObject.CreatePrimitive(kind);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = at;
            g.transform.localScale = size;
            g.GetComponent<Renderer>().sharedMaterial = Paint(color);
            var collider = g.GetComponent<Collider>();
            if (collider)
                Object.Destroy(collider);
            return g.transform;
        }

        public static Transform Ball(Transform p, Vector3 at, Vector3 size, int color) => Make(p, "Форма", PrimitiveType.Sphere, at, size, color);
        public static Transform Box(Transform p, Vector3 at, Vector3 size, int color) => Make(p, "Блок", PrimitiveType.Cube, at, size, color);
        public static Transform Disc(Transform p, Vector3 at, float radius, float height, int color) => Make(p, "Диск", PrimitiveType.Cylinder, at, V(radius * 2, height / 2, radius * 2), color);
        public static Transform Rod(Transform p, Vector3 a, Vector3 b, float radius, int color)
        {
            var d = Disc(p, (a + b) * .5f, radius, (b - a).magnitude, color);
            d.localRotation = Quaternion.FromToRotation(Vector3.up, b - a);
            return d;
        }

        public static TextMesh Text(Transform p, string value, Vector3 at, float size, int color)
        {
            var t = Group(p, "Надпись", at);
            var text = t.gameObject.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.GetComponent<MeshRenderer>().sharedMaterial = text.font.material;
            text.richText = false;
            text.alignment = TextAlignment.Center;
            text.fontSize = 72;
            text.characterSize = size;
            text.color = Ink(color);
            return text;
        }
    }
}
