// C# / Assets/Scripts/Core/GunModels.cs / Unity 2022.3+ / per weapon top-down models
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    /// <summary>
    /// Each gun is a list of local-space parts (x forward, y lateral). The same
    /// description draws the weapon in the player's hands, as a HUD icon and in the
    /// armory preview, so a skin always looks the same wherever it shows up.
    /// </summary>
    public static class GunModels
    {
        public enum Role { Body, Trim, Dark, Accent, Grip }

        public struct Shape
        {
            public bool isCircle;
            public Vector2[] points;   // polygon
            public Vector2 center;     // circle
            public float radius;
            public Role role;
        }

        static Shape Rect(float x, float y, float w, float h, Role role)
        {
            return new Shape
            {
                isCircle = false,
                points = new[]
                {
                    new Vector2(x, y), new Vector2(x + w, y),
                    new Vector2(x + w, y + h), new Vector2(x, y + h)
                },
                role = role
            };
        }

        static Shape Poly(Vector2[] pts, Role role)
        {
            return new Shape { isCircle = false, points = pts, role = role };
        }

        static Shape Circ(float x, float y, float r, Role role)
        {
            return new Shape { isCircle = true, center = new Vector2(x, y), radius = r, role = role };
        }

        public static readonly Dictionary<string, Shape[]> Models =
            new Dictionary<string, Shape[]>
        {
            { "pistol", new[]
                {
                    Rect(-6f, -3.0f, 20f, 6.0f, Role.Body),    // slide
                    Rect(-6f, -3.0f, 20f, 2.0f, Role.Trim),    // top rib
                    Rect(12f, -2.0f,  7f, 4.0f, Role.Dark),    // barrel
                    Rect(-7f, -4.5f,  6f, 9.0f, Role.Grip),    // frame
                    Circ(17f, 0f, 1.6f, Role.Accent)           // muzzle
                }
            },
            { "smg", new[]
                {
                    Rect(-10f, -3.5f, 30f, 7.0f, Role.Body),
                    Rect(-10f, -3.5f, 30f, 2.2f, Role.Trim),
                    Rect( 18f, -2.2f, 10f, 4.4f, Role.Dark),   // barrel
                    Rect(  2f,  3.0f,  5f, 11.0f, Role.Dark),  // magazine
                    Rect(-16f, -2.2f,  7f, 4.4f, Role.Grip),   // stock
                    Rect(  6f, -5.6f,  9f, 2.0f, Role.Trim),   // rail
                    Circ(27f, 0f, 1.8f, Role.Accent)
                }
            },
            { "shotgun", new[]
                {
                    Rect(-12f, -4.2f, 40f, 8.4f, Role.Body),
                    Rect(-12f, -4.2f, 40f, 2.4f, Role.Trim),
                    Rect( 16f,  2.6f, 14f, 3.4f, Role.Dark),   // pump
                    Rect( 26f, -3.2f, 10f, 6.4f, Role.Dark),   // muzzle block
                    Rect(-20f, -3.4f,  9f, 6.8f, Role.Grip),   // stock
                    Poly(new[] { new Vector2(36f, -3.2f), new Vector2(41f, -1.8f),
                                 new Vector2(41f, 1.8f), new Vector2(36f, 3.2f) }, Role.Trim),
                    Circ(40f, 0f, 2.4f, Role.Accent)
                }
            },
            { "rifle", new[]
                {
                    Rect(-14f, -3.4f, 44f, 6.8f, Role.Body),
                    Rect(-14f, -3.4f, 44f, 2.0f, Role.Trim),
                    Rect( 26f, -2.0f, 16f, 4.0f, Role.Dark),   // long barrel
                    Rect(  4f,  3.2f,  6f, 12.0f, Role.Dark),  // curved mag
                    Rect(-22f, -2.6f,  9f, 5.2f, Role.Grip),   // stock
                    Rect(  2f, -6.0f, 12f, 2.2f, Role.Trim),   // top rail
                    Rect( 30f, -4.6f,  4f, 2.0f, Role.Dark),   // front sight
                    Circ(42f, 0f, 2.0f, Role.Accent)
                }
            }
        };

        static readonly Dictionary<string, float[]> PatternDots = new Dictionary<string, float[]>
        {
            // triples of x, y, radius
            { "camo", new[] { -4f,-1.6f,3.4f,  6f,1.4f,3.0f,  16f,-1.2f,2.6f,
                              -10f,1.0f,2.4f,  24f,1.0f,2.2f,  11f,-2.0f,2.0f } },
            { "hex",  new[] { -6f,0f,1.3f,  0f,-1.4f,1.3f,  6f,1.4f,1.3f,  12f,-1.4f,1.3f,
                              18f,1.4f,1.3f,  24f,-1.4f,1.3f,  30f,1.4f,1.3f } },
            { "splatter", new[] { -2f,2.0f,2.6f,  9f,-2.2f,2.0f,  19f,2.2f,2.4f,  28f,-1.6f,1.8f } }
        };

        static readonly Dictionary<string, float> Lengths = new Dictionary<string, float>
        {
            { "pistol", 19f }, { "smg", 28f }, { "shotgun", 41f }, { "rifle", 42f }
        };

        public static float Length(string key)
        {
            float v;
            return Lengths.TryGetValue(key, out v) ? v : 20f;
        }

        public static float MuzzleOffset(string key) { return Length(key) + 2f; }

        static Color RoleColor(Role role, Skin skin)
        {
            switch (role)
            {
                case Role.Body: return skin.body;
                case Role.Trim: return skin.trim;
                case Role.Dark: return S.Mul(skin.trim, 0.62f);
                case Role.Accent: return skin.accent;
                default: return S.Mul(skin.body, 0.52f);
            }
        }

        static readonly Vector2[] _scratch = new Vector2[16];

        /// <summary>
        /// Draw the weapon model. Returns the muzzle tip in screen space so the caller
        /// can place a flash there.
        /// </summary>
        public static Vector2 Draw(Vector2 origin, float angleDeg, string key, Skin skin,
                                   float scale = 1f, bool shadow = true, bool glow = true)
        {
            Shape[] model;
            if (!Models.TryGetValue(key, out model)) model = Models["pistol"];

            float a = -angleDeg * Mathf.Deg2Rad;   // screen y grows downward
            float cos = Mathf.Cos(a), sin = Mathf.Sin(a);

            if (shadow)
            {
                for (int i = 0; i < model.Length; i++)
                {
                    if (model[i].isCircle) continue;
                    var pts = model[i].points;
                    int n = Mathf.Min(pts.Length, _scratch.Length);
                    for (int j = 0; j < n; j++)
                        _scratch[j] = ToScreen(pts[j].x + 1.5f, pts[j].y + 1.5f, origin, cos, sin, scale);
                    Painter.Polygon(_scratch, n, S.Col(12, 12, 14));
                }
            }

            for (int i = 0; i < model.Length; i++)
            {
                Color col = RoleColor(model[i].role, skin);
                if (model[i].isCircle)
                {
                    Vector2 c = ToScreen(model[i].center.x, model[i].center.y, origin, cos, sin, scale);
                    Painter.Circle(c.x, c.y, Mathf.Max(1f, model[i].radius * scale), col);
                }
                else
                {
                    var pts = model[i].points;
                    int n = Mathf.Min(pts.Length, _scratch.Length);
                    for (int j = 0; j < n; j++)
                        _scratch[j] = ToScreen(pts[j].x, pts[j].y, origin, cos, sin, scale);
                    Painter.Polygon(_scratch, n, col);
                    Painter.PolygonOutline(_scratch, n, Mathf.Max(1f, scale), S.Mul(col, 0.6f));
                }
            }

            float len = Length(key);
            float[] dots;
            if (PatternDots.TryGetValue(skin.pattern, out dots))
            {
                Color col = skin.pattern == "hex" ? skin.accent : S.Mul(skin.trim, 1.35f);
                for (int i = 0; i + 2 < dots.Length; i += 3)
                {
                    if (dots[i] > len) continue;
                    Vector2 p = ToScreen(dots[i], dots[i + 1], origin, cos, sin, scale);
                    Painter.Circle(p.x, p.y, Mathf.Max(1f, dots[i + 2] * scale * 0.6f), col);
                }
            }

            if (skin.pattern == "stripe")
            {
                Vector2 p0 = ToScreen(-10f, -0.6f, origin, cos, sin, scale);
                Vector2 p1 = ToScreen(len - 4f, -0.6f, origin, cos, sin, scale);
                Painter.Line(p0, p1, Mathf.Max(1f, 1.6f * scale), skin.accent);
            }

            if (skin.pattern == "glow" && glow)
            {
                Vector2 tip = ToScreen(len - 2f, 0f, origin, cos, sin, scale);
                float rad = Mathf.Max(3f, 5f * scale);
                Painter.Flip(true);
                Painter.Circle(tip.x, tip.y, rad, S.WithA(skin.accent, 70f / 255f));
                Painter.Flip(false);
            }

            return ToScreen(len + 2f, 0f, origin, cos, sin, scale);
        }

        static Vector2 ToScreen(float px, float py, Vector2 origin, float cos, float sin, float scale)
        {
            float x = px * scale, y = py * scale;
            return new Vector2(origin.x + x * cos - y * sin, origin.y + x * sin + y * cos);
        }

        /// <summary>Small side-on icon for HUD slots and the armory.</summary>
        public static void DrawIcon(RectI rect, string key, Skin skin, bool bright = true)
        {
            float len = Length(key);
            float scale = Mathf.Min(rect.w / (len + 24f), rect.h / 16f);
            float cx = rect.CenterX - len * 0.42f * scale;
            float cy = rect.CenterY;

            Skin use = skin;
            if (!bright)
            {
                use = new Skin
                {
                    key = skin.key, name = skin.name, isPlayer = skin.isPlayer,
                    rarity = skin.rarity,
                    body = S.Mul(skin.body, 0.45f),
                    trim = S.Mul(skin.trim, 0.45f),
                    accent = S.Mul(skin.accent, 0.45f),
                    pattern = "plain"
                };
            }

            Draw(new Vector2(cx, cy), 0f, key, use, scale, false, false);

            if (skin.pattern == "glow" && bright)
                Painter.Circle(cx + len * scale, cy, Mathf.Max(2f, 2.2f * scale), skin.accent);
        }
    }
}
