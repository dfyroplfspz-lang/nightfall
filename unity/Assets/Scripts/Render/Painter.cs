// C# / Assets/Scripts/Render/Painter.cs / Unity 2022.3+ / immediate-mode vector drawing in pixel space
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    /// <summary>
    /// The original game draws with circles, lines and polygons, so the port keeps
    /// vector primitives and pushes them straight to the GPU through GL. Everything
    /// is in a virtual 1280x720 pixel space with y growing downward, exactly like
    /// the source, and the camera letterboxes that onto the real window.
    ///
    /// Call Begin() once, emit shapes, call End(). All shapes inside one batch share
    /// a single GL.Begin(TRIANGLES), so a full frame is a handful of draw calls.
    /// </summary>
    public static class Painter
    {
        static Material _mat;
        static Material _addMat;
        static Material _texMat;
        static bool _inBatch;
        static bool _additive;

        public static float GlobalAlpha = 1f;

        // --------------------------------------------------------------- setup
        public static void EnsureMaterials()
        {
            if (_mat != null) return;

            Shader vc = Shader.Find("Nightfall/VertexColor");
            if (vc == null) vc = Resources.Load<Shader>("Shaders/VertexColor");
            Shader va = Shader.Find("Nightfall/VertexColorAdd");
            if (va == null) va = Resources.Load<Shader>("Shaders/VertexColorAdd");
            Shader tx = Shader.Find("Nightfall/TexturedQuad");
            if (tx == null) tx = Resources.Load<Shader>("Shaders/TexturedQuad");

            _mat = new Material(vc) { hideFlags = HideFlags.HideAndDontSave };
            _addMat = new Material(va) { hideFlags = HideFlags.HideAndDontSave };
            _texMat = new Material(tx) { hideFlags = HideFlags.HideAndDontSave };
        }

        /// <summary>Start a batch. additive=true for glows and muzzle flashes.</summary>
        public static void Begin(bool additive = false)
        {
            EnsureMaterials();
            if (_inBatch) End();
            _additive = additive;
            (additive ? _addMat : _mat).SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, S.WIDTH, S.HEIGHT, 0f);
            GL.Begin(GL.TRIANGLES);
            _inBatch = true;
        }

        public static void End()
        {
            if (!_inBatch) return;
            GL.End();
            GL.PopMatrix();
            _inBatch = false;
        }

        /// <summary>Switch blend mode without losing the pixel matrix.</summary>
        public static void Flip(bool additive)
        {
            if (_inBatch && _additive == additive) return;
            End();
            Begin(additive);
        }

        // ------------------------------------------------------------ plumbing
        static void C(Color c)
        {
            if (GlobalAlpha < 0.999f) c.a *= GlobalAlpha;
            GL.Color(c);
        }

        static void V(float x, float y) { GL.Vertex3(x, y, 0f); }

        static void Tri(Color c, float ax, float ay, float bx, float by, float cx, float cy)
        {
            C(c); V(ax, ay);
            C(c); V(bx, by);
            C(c); V(cx, cy);
        }

        static void Quad(Color c, Vector2 a, Vector2 b, Vector2 d, Vector2 e)
        {
            Tri(c, a.x, a.y, b.x, b.y, d.x, d.y);
            Tri(c, a.x, a.y, d.x, d.y, e.x, e.y);
        }

        /// <summary>
        /// Quad with an independent colour at each corner. The night overlay is one
        /// big grid of these, which is how the torch gets a smooth gradient without
        /// a render texture or a custom blend mode.
        /// </summary>
        public static void QuadShaded(Vector2 a, Vector2 b, Vector2 c, Vector2 d,
                                      Color ca, Color cb, Color cc, Color cd)
        {
            C(ca); V(a.x, a.y);
            C(cb); V(b.x, b.y);
            C(cc); V(c.x, c.y);

            C(ca); V(a.x, a.y);
            C(cc); V(c.x, c.y);
            C(cd); V(d.x, d.y);
        }

        static int SegmentsFor(float radius)
        {
            if (radius < 3f) return 6;
            if (radius < 8f) return 10;
            if (radius < 20f) return 16;
            if (radius < 60f) return 26;
            if (radius < 160f) return 40;
            return 64;
        }

        // -------------------------------------------------------------- shapes
        public static void Rect(float x, float y, float w, float h, Color c)
        {
            if (w <= 0f || h <= 0f) return;
            Tri(c, x, y, x + w, y, x + w, y + h);
            Tri(c, x, y, x + w, y + h, x, y + h);
        }

        public static void Rect(RectI r, Color c) { Rect(r.x, r.y, r.w, r.h, c); }

        public static void RectOutline(float x, float y, float w, float h, float t, Color c)
        {
            if (w <= 0f || h <= 0f || t <= 0f) return;
            t = Mathf.Min(t, Mathf.Min(w, h) * 0.5f);
            Rect(x, y, w, t, c);
            Rect(x, y + h - t, w, t, c);
            Rect(x, y + t, t, h - t * 2f, c);
            Rect(x + w - t, y + t, t, h - t * 2f, c);
        }

        public static void RectOutline(RectI r, float t, Color c)
        {
            RectOutline(r.x, r.y, r.w, r.h, t, c);
        }

        public static void Circle(float cx, float cy, float r, Color c)
        {
            if (r <= 0.3f) return;
            int n = SegmentsFor(r);
            float step = MathUtil.TAU / n;
            float px = cx + r, py = cy;
            for (int i = 1; i <= n; i++)
            {
                float a = step * i;
                float nx = cx + Mathf.Cos(a) * r;
                float ny = cy + Mathf.Sin(a) * r;
                Tri(c, cx, cy, px, py, nx, ny);
                px = nx; py = ny;
            }
        }

        public static void CircleOutline(float cx, float cy, float r, float t, Color c)
        {
            if (r <= 0.3f || t <= 0f) return;
            float inner = Mathf.Max(0f, r - t);
            int n = SegmentsFor(r);
            float step = MathUtil.TAU / n;
            float pco = Mathf.Cos(0f), psi = Mathf.Sin(0f);
            for (int i = 1; i <= n; i++)
            {
                float a = step * i;
                float co = Mathf.Cos(a), si = Mathf.Sin(a);
                Quad(c,
                     new Vector2(cx + pco * inner, cy + psi * inner),
                     new Vector2(cx + pco * r, cy + psi * r),
                     new Vector2(cx + co * r, cy + si * r),
                     new Vector2(cx + co * inner, cy + si * inner));
                pco = co; psi = si;
            }
        }

        public static void Line(Vector2 a, Vector2 b, float width, Color c)
        {
            Vector2 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) { Circle(a.x, a.y, width * 0.5f, c); return; }
            Vector2 n = new Vector2(-d.y, d.x) / len * (width * 0.5f);
            Quad(c, a - n, a + n, b + n, b - n);
            // round the joints so chained lines do not show gaps
            if (width > 3f)
            {
                Circle(a.x, a.y, width * 0.5f, c);
                Circle(b.x, b.y, width * 0.5f, c);
            }
        }

        public static void Line(float ax, float ay, float bx, float by, float width, Color c)
        {
            Line(new Vector2(ax, ay), new Vector2(bx, by), width, c);
        }

        /// <summary>Convex polygon as a triangle fan — every model in the game is convex.</summary>
        public static void Polygon(IList<Vector2> pts, Color c)
        {
            if (pts.Count < 3) return;
            for (int i = 1; i < pts.Count - 1; i++)
                Tri(c, pts[0].x, pts[0].y, pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y);
        }

        /// <summary>Fan over the first <paramref name="count"/> points of a reusable buffer.</summary>
        public static void Polygon(Vector2[] pts, int count, Color c)
        {
            if (count < 3) return;
            for (int i = 1; i < count - 1; i++)
                Tri(c, pts[0].x, pts[0].y, pts[i].x, pts[i].y, pts[i + 1].x, pts[i + 1].y);
        }

        public static void PolygonOutline(Vector2[] pts, int count, float t, Color c)
        {
            if (count < 2) return;
            for (int i = 0; i < count; i++)
                Line(pts[i], pts[(i + 1) % count], t, c);
        }

        public static void PolygonOutline(IList<Vector2> pts, float t, Color c)
        {
            if (pts.Count < 2) return;
            for (int i = 0; i < pts.Count; i++)
                Line(pts[i], pts[(i + 1) % pts.Count], t, c);
        }

        public static void Polyline(IList<Vector2> pts, float t, Color c, bool closed = false)
        {
            if (pts.Count < 2) return;
            int last = closed ? pts.Count : pts.Count - 1;
            for (int i = 0; i < last; i++)
                Line(pts[i], pts[(i + 1) % pts.Count], t, c);
        }

        public static void RoundRect(float x, float y, float w, float h, float rad, Color c)
        {
            if (w <= 0f || h <= 0f) return;
            rad = Mathf.Clamp(rad, 0f, Mathf.Min(w, h) * 0.5f);
            if (rad <= 0.5f) { Rect(x, y, w, h, c); return; }

            Rect(x + rad, y, w - rad * 2f, h, c);
            Rect(x, y + rad, rad, h - rad * 2f, c);
            Rect(x + w - rad, y + rad, rad, h - rad * 2f, c);

            Corner(x + rad, y + rad, rad, 180f, c);
            Corner(x + w - rad, y + rad, rad, 270f, c);
            Corner(x + w - rad, y + h - rad, rad, 0f, c);
            Corner(x + rad, y + h - rad, rad, 90f, c);
        }

        public static void RoundRect(RectI r, float rad, Color c)
        {
            RoundRect(r.x, r.y, r.w, r.h, rad, c);
        }

        static void Corner(float cx, float cy, float r, float startDeg, Color c)
        {
            int n = Mathf.Max(3, SegmentsFor(r) / 4);
            float a0 = startDeg * Mathf.Deg2Rad;
            float step = (Mathf.PI * 0.5f) / n;
            float px = cx + Mathf.Cos(a0) * r, py = cy + Mathf.Sin(a0) * r;
            for (int i = 1; i <= n; i++)
            {
                float a = a0 + step * i;
                float nx = cx + Mathf.Cos(a) * r;
                float ny = cy + Mathf.Sin(a) * r;
                Tri(c, cx, cy, px, py, nx, ny);
                px = nx; py = ny;
            }
        }

        public static void RoundRectOutline(float x, float y, float w, float h, float rad,
                                            float t, Color c)
        {
            if (w <= 0f || h <= 0f || t <= 0f) return;
            rad = Mathf.Clamp(rad, 0f, Mathf.Min(w, h) * 0.5f);
            if (rad <= 0.5f) { RectOutline(x, y, w, h, t, c); return; }

            Rect(x + rad, y, w - rad * 2f, t, c);
            Rect(x + rad, y + h - t, w - rad * 2f, t, c);
            Rect(x, y + rad, t, h - rad * 2f, c);
            Rect(x + w - t, y + rad, t, h - rad * 2f, c);

            Arc(x + rad, y + rad, rad, t, 180f, 90f, c);
            Arc(x + w - rad, y + rad, rad, t, 270f, 90f, c);
            Arc(x + w - rad, y + h - rad, rad, t, 0f, 90f, c);
            Arc(x + rad, y + h - rad, rad, t, 90f, 90f, c);
        }

        public static void RoundRectOutline(RectI r, float rad, float t, Color c)
        {
            RoundRectOutline(r.x, r.y, r.w, r.h, rad, t, c);
        }

        static void Arc(float cx, float cy, float r, float t, float startDeg, float sweepDeg, Color c)
        {
            int n = Mathf.Max(3, SegmentsFor(r) / 4);
            float inner = Mathf.Max(0f, r - t);
            float a0 = startDeg * Mathf.Deg2Rad;
            float step = (sweepDeg * Mathf.Deg2Rad) / n;
            float pco = Mathf.Cos(a0), psi = Mathf.Sin(a0);
            for (int i = 1; i <= n; i++)
            {
                float a = a0 + step * i;
                float co = Mathf.Cos(a), si = Mathf.Sin(a);
                Quad(c,
                     new Vector2(cx + pco * inner, cy + psi * inner),
                     new Vector2(cx + pco * r, cy + psi * r),
                     new Vector2(cx + co * r, cy + si * r),
                     new Vector2(cx + co * inner, cy + si * inner));
                pco = co; psi = si;
            }
        }

        /// <summary>
        /// Soft round blob used for shadows and glows: a bright core fading to nothing,
        /// built from concentric rings so it needs no texture.
        /// </summary>
        public static void RadialBlob(float cx, float cy, float r, Color core, int rings = 7,
                                      float falloff = 1.4f)
        {
            if (r <= 1f) return;
            for (int i = rings; i >= 1; i--)
            {
                float t = i / (float)rings;
                float a = core.a * Mathf.Pow(1f - t, falloff * 0.55f);
                Color c = core;
                c.a = a / rings * 2.2f;
                Circle(cx, cy, r * t, c);
            }
        }

        // ----------------------------------------------------------- textures
        /// <summary>
        /// Blit a texture rect in pixel space. Used for the baked arena floor and the
        /// map previews, which are real textures rather than vector shapes.
        /// </summary>
        public static void TexturedQuad(Texture tex, Rect dest, Rect uv, Color tint)
        {
            EnsureMaterials();
            bool was = _inBatch;
            bool wasAdd = _additive;
            if (was) End();

            _texMat.mainTexture = tex;
            _texMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, S.WIDTH, S.HEIGHT, 0f);
            GL.Begin(GL.QUADS);
            GL.Color(tint);
            GL.TexCoord2(uv.xMin, uv.yMax); GL.Vertex3(dest.xMin, dest.yMin, 0f);
            GL.TexCoord2(uv.xMax, uv.yMax); GL.Vertex3(dest.xMax, dest.yMin, 0f);
            GL.TexCoord2(uv.xMax, uv.yMin); GL.Vertex3(dest.xMax, dest.yMax, 0f);
            GL.TexCoord2(uv.xMin, uv.yMin); GL.Vertex3(dest.xMin, dest.yMax, 0f);
            GL.End();
            GL.PopMatrix();

            if (was) Begin(wasAdd);
        }
    }
}
