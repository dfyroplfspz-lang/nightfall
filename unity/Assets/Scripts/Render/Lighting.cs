// C# / Assets/Scripts/Render/Lighting.cs / Unity 2022.3+ / the night overlay and the torch
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    /// <summary>
    /// The arena is pitch black apart from what the torch picks out. The overlay is a
    /// screen-space grid whose vertex alpha is the darkness left after every light is
    /// taken into account, so the beam gets a smooth gradient with no render texture
    /// and no exotic blend mode.
    /// </summary>
    public static class Lighting
    {
        const int CELL = 16;
        const int GX = S.WIDTH / CELL;    // 80
        const int GY = S.HEIGHT / CELL;   // 45

        static readonly float[] _alpha = new float[(GX + 1) * (GY + 1)];

        // torch shape, matching the nested wedges of the original
        const float ConeNear = 260f;
        const float ConeSpan = 560f;
        const float HalfNear = 32f;       // degrees at the muzzle
        const float HalfFar = 15f;        // degrees at maximum reach

        public struct PointLight
        {
            public Vector2 pos;
            public float radius;
            public float falloff;
        }

        static readonly List<PointLight> _points = new List<PointLight>(32);

        public static void Begin() { _points.Clear(); }

        public static void AddPoint(Vector2 screenPos, float radius, float falloff = 1.4f)
        {
            if (radius < 8f) return;
            if (screenPos.x + radius < 0f || screenPos.x - radius > S.WIDTH) return;
            if (screenPos.y + radius < 0f || screenPos.y - radius > S.HEIGHT) return;
            _points.Add(new PointLight { pos = screenPos, radius = radius, falloff = falloff });
        }

        /// <summary>
        /// Darkness left at one screen point, 0 (lit) to darkness (black).
        /// </summary>
        static float Sample(float x, float y, float darkness, Vector2 torchOrigin,
                            Vector2 aim, bool torchOn)
        {
            float best = darkness;

            if (torchOn)
            {
                float dx = x - torchOrigin.x;
                float dy = y - torchOrigin.y;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > 1e-3f && d <= ConeNear + ConeSpan)
                {
                    float t = MathUtil.Clamp((d - ConeNear) / ConeSpan, 0f, 1f);
                    float half = HalfFar + (HalfNear - HalfFar) * (1f - t);
                    float cosLimit = Mathf.Cos(half * Mathf.Deg2Rad);
                    float cosA = (dx * aim.x + dy * aim.y) / d;
                    if (cosA >= cosLimit)
                    {
                        float a = darkness * (0.22f + 0.74f * t);
                        if (a < best) best = a;
                    }
                }
            }

            for (int i = 0; i < _points.Count; i++)
            {
                PointLight p = _points[i];
                float dx = x - p.pos.x;
                float dy = y - p.pos.y;
                float d2 = dx * dx + dy * dy;
                float r2 = p.radius * p.radius;
                if (d2 >= r2) continue;
                float d = Mathf.Sqrt(d2) / p.radius;
                float a = Mathf.Pow(d, p.falloff) * darkness;
                if (a < best) best = a;
            }

            return best;
        }

        /// <summary>Emit the overlay. Call inside a normal (non-additive) Painter batch.</summary>
        public static void Draw(float darkness, Vector2 torchOrigin, Vector2 aim, bool torchOn)
        {
            float d255 = darkness / 255f;

            for (int gy = 0; gy <= GY; gy++)
            {
                float y = gy * CELL;
                int row = gy * (GX + 1);
                for (int gx = 0; gx <= GX; gx++)
                    _alpha[row + gx] = Sample(gx * CELL, y, d255, torchOrigin, aim, torchOn);
            }

            for (int gy = 0; gy < GY; gy++)
            {
                int r0 = gy * (GX + 1);
                int r1 = r0 + (GX + 1);
                float y0 = gy * CELL, y1 = y0 + CELL;

                for (int gx = 0; gx < GX; gx++)
                {
                    float a00 = _alpha[r0 + gx], a10 = _alpha[r0 + gx + 1];
                    float a11 = _alpha[r1 + gx + 1], a01 = _alpha[r1 + gx];
                    if (a00 <= 0.002f && a10 <= 0.002f && a11 <= 0.002f && a01 <= 0.002f) continue;

                    float x0 = gx * CELL, x1 = x0 + CELL;
                    Painter.QuadShaded(
                        new Vector2(x0, y0), new Vector2(x1, y0),
                        new Vector2(x1, y1), new Vector2(x0, y1),
                        new Color(0f, 0f, 0f, a00), new Color(0f, 0f, 0f, a10),
                        new Color(0f, 0f, 0f, a11), new Color(0f, 0f, 0f, a01));
                }
            }
        }
    }

    /// <summary>
    /// Keeps the logical 1280x720 intact at any window size: the game is rendered into
    /// a centred box and the rest of the screen stays black, so mouse coordinates and
    /// layout never change, in a window or in fullscreen.
    /// </summary>
    public static class Letterbox
    {
        public static float scale = 1f;
        public static float offX, offY;

        public static void Recalculate()
        {
            float sw = Mathf.Max(1, Screen.width);
            float sh = Mathf.Max(1, Screen.height);
            scale = Mathf.Min(sw / S.WIDTH, sh / S.HEIGHT);
            offX = (sw - S.WIDTH * scale) * 0.5f;
            offY = (sh - S.HEIGHT * scale) * 0.5f;
        }

        public static void ApplyTo(Camera cam)
        {
            Recalculate();
            float sw = Mathf.Max(1, Screen.width);
            float sh = Mathf.Max(1, Screen.height);
            cam.rect = new Rect(offX / sw, offY / sh,
                                S.WIDTH * scale / sw, S.HEIGHT * scale / sh);
        }

        /// <summary>Unity screen coordinates (y up) to the virtual 1280x720 space (y down).</summary>
        public static Vector2 ToVirtual(Vector3 screenPos)
        {
            float x = (screenPos.x - offX) / Mathf.Max(0.0001f, scale);
            float y = (Screen.height - screenPos.y - offY) / Mathf.Max(0.0001f, scale);
            return new Vector2(Mathf.Clamp(x, 0f, S.WIDTH), Mathf.Clamp(y, 0f, S.HEIGHT));
        }

        /// <summary>Matrix that puts IMGUI into the same virtual space.</summary>
        public static Matrix4x4 GuiMatrix()
        {
            return Matrix4x4.TRS(new Vector3(offX, offY, 0f), Quaternion.identity,
                                 new Vector3(scale, scale, 1f));
        }
    }
}
