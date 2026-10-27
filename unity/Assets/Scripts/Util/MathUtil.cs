// C# / Assets/Scripts/Util/MathUtil.cs / Unity 2022.3+ / vector helpers, rects, deterministic rng
using System;
using UnityEngine;

namespace Nightfall
{
    /// <summary>
    /// Axis-aligned rectangle in game pixel space, y growing downward, matching
    /// the original layout maths one to one.
    /// </summary>
    [Serializable]
    public struct RectI
    {
        public int x, y, w, h;

        public RectI(int x, int y, int w, int h) { this.x = x; this.y = y; this.w = w; this.h = h; }

        public int Left { get { return x; } }
        public int Top { get { return y; } }
        public int Right { get { return x + w; } }
        public int Bottom { get { return y + h; } }
        public int CenterX { get { return x + w / 2; } }
        public int CenterY { get { return y + h / 2; } }
        public Vector2 Center { get { return new Vector2(x + w * 0.5f, y + h * 0.5f); } }

        public RectI Inflate(int dw, int dh)
        {
            return new RectI(x - dw / 2, y - dh / 2, w + dw, h + dh);
        }

        public bool Overlaps(RectI o)
        {
            return x < o.Right && o.x < Right && y < o.Bottom && o.y < Bottom;
        }

        public bool Contains(float px, float py)
        {
            return px >= x && px < Right && py >= y && py < Bottom;
        }

        public bool Contains(Vector2 p) { return Contains(p.x, p.y); }

        /// <summary>Clamp this rect so it sits fully inside bounds.</summary>
        public RectI ClampInside(RectI bounds)
        {
            int nx = Mathf.Clamp(x, bounds.x, Mathf.Max(bounds.x, bounds.Right - w));
            int ny = Mathf.Clamp(y, bounds.y, Mathf.Max(bounds.y, bounds.Bottom - h));
            return new RectI(nx, ny, w, h);
        }

        /// <summary>
        /// First intersection of segment a..b with this rect, or null when it misses.
        /// Slab method, returns the entry point in world space.
        /// </summary>
        public Vector2? ClipLine(Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float t0 = 0f, t1 = 1f;
            float[] p = { -d.x, d.x, -d.y, d.y };
            float[] q = { a.x - Left, Right - a.x, a.y - Top, Bottom - a.y };

            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(p[i]) < 1e-7f)
                {
                    if (q[i] < 0f) return null;
                    continue;
                }
                float r = q[i] / p[i];
                if (p[i] < 0f) { if (r > t1) return null; if (r > t0) t0 = r; }
                else { if (r < t0) return null; if (r < t1) t1 = r; }
            }
            return a + d * t0;
        }
    }

    public static class MathUtil
    {
        public const float TAU = 6.28318530718f;

        public static float Clamp(float v, float lo, float hi)
        {
            return v < lo ? lo : (v > hi ? hi : v);
        }

        public static Vector2 Norm(Vector2 v)
        {
            float m = v.magnitude;
            return m > 1e-6f ? v / m : Vector2.zero;
        }

        /// <summary>Rotate clockwise in screen space (y down), degrees — pygame's Vector2.rotate.</summary>
        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        public static Vector2 FromAngle(float degrees, float length)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a) * length, Mathf.Sin(a) * length);
        }

        /// <summary>Screen-space angle in degrees, y down, counter-clockwise positive.</summary>
        public static float AngleOf(Vector2 v)
        {
            return -Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// Push a circle out of a rect along the shallowest axis. Mutates pos.
        /// </summary>
        public static bool ResolveCircleRect(ref Vector2 pos, float radius, RectI r)
        {
            float cx = Clamp(pos.x, r.Left, r.Right);
            float cy = Clamp(pos.y, r.Top, r.Bottom);
            float dx = pos.x - cx;
            float dy = pos.y - cy;
            float d2 = dx * dx + dy * dy;

            if (d2 > radius * radius) return false;

            if (d2 > 1e-6f)
            {
                float d = Mathf.Sqrt(d2);
                pos += new Vector2(dx / d, dy / d) * (radius - d);
                return true;
            }

            // dead centre: leave through the nearest face
            float left = pos.x - r.Left;
            float right = r.Right - pos.x;
            float top = pos.y - r.Top;
            float bottom = r.Bottom - pos.y;
            float min = Mathf.Min(Mathf.Min(left, right), Mathf.Min(top, bottom));
            if (min == left) pos.x = r.Left - radius;
            else if (min == right) pos.x = r.Right + radius;
            else if (min == top) pos.y = r.Top - radius;
            else pos.y = r.Bottom + radius;
            return true;
        }

        /// <summary>
        /// Parameter t in 0..1 where segment a..b first enters a circle, or null.
        /// </summary>
        public static float? SegCircleHit(Vector2 a, Vector2 b, Vector2 c, float radius)
        {
            Vector2 d = b - a;
            Vector2 f = a - c;
            float aa = Vector2.Dot(d, d);
            if (aa < 1e-9f)
                return Vector2.SqrMagnitude(f) <= radius * radius ? (float?)0f : null;

            float bb = 2f * Vector2.Dot(f, d);
            float cc = Vector2.Dot(f, f) - radius * radius;
            float disc = bb * bb - 4f * aa * cc;
            if (disc < 0f) return null;

            disc = Mathf.Sqrt(disc);
            float t1 = (-bb - disc) / (2f * aa);
            float t2 = (-bb + disc) / (2f * aa);
            if (t1 >= 0f && t1 <= 1f) return t1;
            if (t2 >= 0f && t2 <= 1f) return t2;
            if (t1 < 0f && t2 > 1f) return 0f;
            return null;
        }
    }

    /// <summary>
    /// Small deterministic PRNG so a seed reproduces a map exactly, independent of
    /// UnityEngine.Random's global state.
    /// </summary>
    public class Rng
    {
        uint _s;

        public Rng(int seed)
        {
            _s = (uint)seed;
            if (_s == 0u) _s = 2463534242u;
        }

        public uint NextUInt()
        {
            _s ^= _s << 13;
            _s ^= _s >> 17;
            _s ^= _s << 5;
            return _s;
        }

        /// <summary>0 inclusive .. 1 exclusive.</summary>
        public float Value { get { return (NextUInt() >> 8) * (1f / 16777216f); } }

        public float Range(float lo, float hi) { return lo + Value * (hi - lo); }

        /// <summary>lo inclusive .. hi inclusive, matching python's randint.</summary>
        public int RangeInt(int lo, int hi)
        {
            if (hi <= lo) return lo;
            return lo + (int)(NextUInt() % (uint)(hi - lo + 1));
        }

        public bool Chance(float p) { return Value < p; }

        public int Sign() { return (NextUInt() & 1u) == 0u ? -1 : 1; }

        public T Pick<T>(System.Collections.Generic.IList<T> list)
        {
            return list[(int)(NextUInt() % (uint)list.Count)];
        }

        public void Shuffle<T>(System.Collections.Generic.IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = (int)(NextUInt() % (uint)(i + 1));
                T tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
        }

        /// <summary>Weighted pick; weights and items must be the same length.</summary>
        public int PickWeighted(System.Collections.Generic.IList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += weights[i];
            if (total <= 0f) return 0;
            float r = Value * total;
            for (int i = 0; i < weights.Count; i++)
            {
                r -= weights[i];
                if (r <= 0f) return i;
            }
            return weights.Count - 1;
        }
    }
}
