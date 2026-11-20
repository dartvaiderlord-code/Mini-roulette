using System;
using N = System.Numerics;

namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static implicit operator N.Vector3(Vector3 a) => new N.Vector3(a.x, a.y, a.z);
        public static implicit operator Vector3(N.Vector3 a) => new Vector3(a.X, a.Y, a.Z);
        public static Vector3 up => new(0, 1, 0);
        public static Vector3 down => new(0, -1, 0);
        public static Vector3 zero => new();

        public static Vector3 operator +(Vector3 a, Vector3 b) => (N.Vector3)a + (N.Vector3)b;
        public static Vector3 operator -(Vector3 a, Vector3 b) => (N.Vector3)a - (N.Vector3)b;
        public static Vector3 operator -(Vector3 a) => -(N.Vector3)a;
        public static Vector3 operator *(Vector3 a, float b) => (N.Vector3)a * b;
        public static Vector3 operator /(Vector3 a, float b) => (N.Vector3)a / b;
        public float sqrMagnitude => ((N.Vector3)this).LengthSquared();
        public float magnitude => ((N.Vector3)this).Length();
        public Vector3 normalized => magnitude < .000001f ? zero : this / magnitude;

        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => N.Vector3.Lerp(a, b, Math.Clamp(t, 0, 1));
    }

    public struct Quaternion
    {
        public N.Quaternion q;
        public static implicit operator Quaternion(N.Quaternion q) => new Quaternion
        {
            q = q
        };
        public static Quaternion identity => N.Quaternion.Identity;

        public static Quaternion Euler(float x, float y, float z) => N.Quaternion.CreateFromYawPitchRoll(y * Mathf.Deg2Rad, x * Mathf.Deg2Rad, z * Mathf.Deg2Rad);
        public static Quaternion FromToRotation(Vector3 a, Vector3 b)
        {
            N.Vector3 x = a.normalized, y = b.normalized;
            float d = N.Vector3.Dot(x, y);
            if (d < -.99999)
                return Euler(180, 0, 0);
            return N.Quaternion.Normalize(new N.Quaternion(N.Vector3.Cross(x, y), 1 + d));
        }

        public static Quaternion LookRotation(Vector3 v)
        {
            var n = v.normalized;
            return Euler(-MathF.Asin(n.y) / Mathf.Deg2Rad, MathF.Atan2(n.x, n.z) / Mathf.Deg2Rad, 0);
        }

        public static Quaternion Slerp(Quaternion a, Quaternion b, float t) => N.Quaternion.Slerp(a.q, b.q, Math.Clamp(t, 0, 1));
    }

    public class Mathf
    {
        public const float PI = MathF.PI, Deg2Rad = MathF.PI / 180;
        public static float Sin(float x) => MathF.Sin(x);
        public static float Cos(float x) => MathF.Cos(x);
        public static float Exp(float x) => MathF.Exp(x);
        public static float Abs(float x) => MathF.Abs(x);
        public static float Pow(float a, float b) => MathF.Pow(a, b);
        public static float Min(float a, float b) => MathF.Min(a, b);
        public static float Max(float a, float b) => MathF.Max(a, b);
        public static float Lerp(float a, float b, float t) => a + (b - a) * Math.Clamp(t, 0, 1);
    }

    public class Time
    {
        public static float time, deltaTime = 1f / 60;
        public static float realtimeSinceStartup => time;
    }
}
