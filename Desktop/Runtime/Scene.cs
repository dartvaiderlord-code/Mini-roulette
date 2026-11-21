using System;
using System.Collections.Generic;
using System.Linq;
using N = System.Numerics;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public static implicit operator bool (Object o) => o != null;
        public static void Destroy(Object o)
        {
            if (o is GameObject g)
                g.Remove();
            if (o is Mesh m)
                Carousel.Desktop.SceneRenderer.Release(m);
        }

        public static T FindObjectOfType<T>()
            where T : Component => GameObject.all.Select(o => o.GetComponent<T>()).FirstOrDefault(c => c != null);
    }

    public class Component : Object
    {
        public GameObject gameObject;
        public Transform transform => gameObject.transform;

        public T GetComponent<T>()
            where T : Component => gameObject.GetComponent<T>();
    }

    public class MonoBehaviour : Component
    {
    }

    public enum PrimitiveType
    {
        Cube,
        Cylinder,
        Sphere
    }

    public enum Space
    {
        Self,
        World
    }

    // Row-vector matrices use local * parent for hierarchy composition.
    public class Transform : Component
    {
        public List<Transform> children = new();
        public Transform parent;
        public Vector3 localPosition, localScale = new(1, 1, 1);
        public Quaternion localRotation = Quaternion.identity;
        public N.Matrix4x4 local => N.Matrix4x4.CreateScale((N.Vector3)localScale) * N.Matrix4x4.CreateFromQuaternion(localRotation.q) * N.Matrix4x4.CreateTranslation(localPosition);
        public N.Matrix4x4 world => parent == null ? local : local * parent.world;

        public Vector3 position
        {
            get => world.Translation;
            set
            {
                if (parent == null)
                    localPosition = value;
                else
                {
                    N.Matrix4x4.Invert(parent.world, out var inv);
                    localPosition = N.Vector3.Transform(value, inv);
                }
            }
        }

        public Quaternion rotation { get => parent == null ? localRotation : N.Quaternion.Multiply(parent.rotation.q, localRotation.q); set => localRotation = parent == null ? value : N.Quaternion.Inverse(parent.rotation.q) * value.q; }
        public Vector3 forward => N.Vector3.Transform(N.Vector3.UnitZ, rotation.q);

        public void SetParent(Transform p, bool keep)
        {
            var m = world;
            parent?.children.Remove(this);
            parent = p;
            p?.children.Add(this);
            if (keep)
            {
                if (p != null)
                {
                    N.Matrix4x4.Invert(p.world, out var inv);
                    m = m * inv;
                }

                N.Matrix4x4.Decompose(m, out var scale, out var rot, out var pos);
                localScale = scale;
                localRotation = rot;
                localPosition = pos;
            }
        }

        public void LookAt(Vector3 v)
        {
            rotation = Quaternion.LookRotation(v - position);
        }

        public void Rotate(float x, float y, float z) => Rotate(new Vector3(x, y, z), Space.Self);
        public void Rotate(Vector3 v, Space space)
        {
            var d = Quaternion.Euler(v.x, v.y, v.z);
            if (space == Space.World)
                rotation = d.q * rotation.q;
            else
                localRotation = localRotation.q * d.q;
        }

        public int childCount => children.Count;

        public Transform GetChild(int i) => children[i];
    }

    public class GameObject : Object
    {
        public readonly static List<GameObject> all = new();
        public Dictionary<Type, Component> parts = new();
        public Transform transform;
        public string primitive;
        public bool activeSelf = true;
        public bool active => activeSelf && (transform.parent == null || transform.parent.gameObject.active);

        public GameObject(string name)
        {
            this.name = name;
            transform = AddComponent<Transform>();
            all.Add(this);
        }

        public T AddComponent<T>()
            where T : Component, new()
        {
            var c = new T()
            {
                gameObject = this
            };
            parts[typeof(T)] = c;
            if (c is TextMesh)
                AddComponent<MeshRenderer>();
            return c;
        }

        public T GetComponent<T>()
            where T : Component
        {
            if (parts.TryGetValue(typeof(T), out var component))
                return (T)component;
            if (typeof(T) == typeof(Renderer) && parts.TryGetValue(typeof(MeshRenderer), out component))
                return (T)component;
            return null;
        }

        public void Remove()
        {
            foreach (var ch in transform.children.ToArray())
                ch.gameObject.Remove();
            transform.parent?.children.Remove(transform);
            all.Remove(this);
            activeSelf = false;
        }

        public static GameObject CreatePrimitive(PrimitiveType k)
        {
            var g = new GameObject(k.ToString())
            {
                primitive = k.ToString()
            };
            g.AddComponent<MeshRenderer>();
            return g;
        }

        public void SetActive(bool a) => activeSelf = a;
    }

    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static Color white => new(1, 1, 1);

        public static Color Lerp(Color a, Color b, float t) => new(Mathf.Lerp(a.r, b.r, t), Mathf.Lerp(a.g, b.g, t), Mathf.Lerp(a.b, b.b, t), Mathf.Lerp(a.a, b.a, t));
    }

    public struct Color32
    {
        byte r, g, b, a;
        public Color32(byte r, byte g, byte b, byte a)
        {
            this.r = r;
            this.g = g;
            this.b = b;
            this.a = a;
        }

        public static implicit operator Color(Color32 v) => new(v.r / 255f, v.g / 255f, v.b / 255f, v.a / 255f);
    }

    public class Material : Object
    {
        public Color color;
        public Material()
        {
        }

        public Material(Material m)
        {
        }
    }

    public class Resources
    {
        public static T Load<T>(string key)
            where T : new() => new();
        public static T GetBuiltinResource<T>(string key)
            where T : new() => new();
    }

    public class Renderer : Component
    {
        public Rendering.ShadowCastingMode shadowCastingMode = Rendering.ShadowCastingMode.On;
        public Material sharedMaterial;
        public Material[] sharedMaterials;
    }

    public class MeshRenderer : Renderer
    {
    }

    public class Collider : Component
    {
    }

    public class MeshFilter : Component
    {
        public Mesh sharedMesh;
    }

    public class Mesh : Object
    {
        public Vector3[] vertices;
        public int subMeshCount;
        public int[][] tris = new int[2][];
        public void SetTriangles(int[] t, int i) => tris[i] = t;
        public void RecalculateNormals()
        {
        }

        public void RecalculateBounds()
        {
        }
    }

    public enum TextAnchor
    {
        MiddleCenter,
        MiddleLeft,
        MiddleRight
    }

    public enum TextAlignment
    {
        Center
    }

    public class Font
    {
        public Material material;
    }

    public class TextMesh : Component
    {
        public string text;
        public TextAnchor anchor;
        public Font font;
        public bool richText;
        public TextAlignment alignment;
        public int fontSize;
        public float characterSize;
        public Color color;
    }

    public enum CameraClearFlags
    {
        SolidColor
    }

    public class Camera : Component
    {
        public float depth, fieldOfView, nearClipPlane, farClipPlane;
        public CameraClearFlags clearFlags;
        public Color backgroundColor;
    }

    public enum LightType
    {
        Directional,
        Point
    }

    public enum LightShadows
    {
        Soft
    }

    public class Light : Component
    {
        public LightType type;
        public Color color;
        public float intensity, range;
        public LightShadows shadows;
    }

    public class RenderSettings
    {
        public static Rendering.AmbientMode ambientMode;
        public static Color ambientSkyColor, ambientEquatorColor, ambientGroundColor;
    }
}

namespace UnityEngine.Rendering
{
    public enum ShadowCastingMode { Off, On }

    public enum AmbientMode
    {
        Trilight
    }
}
