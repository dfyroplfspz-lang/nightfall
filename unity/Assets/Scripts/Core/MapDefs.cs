// C# / Assets/Scripts/Core/MapDefs.cs / Unity 2022.3+ / themed arenas, their props and floors
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    public class Palette
    {
        public Color floor, floorAlt, grime, line, wall, wallTop, accent, accent2;
    }

    public class Obstacle
    {
        public RectI rect;
        public string kind;
        /// <summary>Per-prop scatter (hedge leaves), generated once so it never shimmers.</summary>
        public float[] detail;

        public Obstacle(RectI r, string k) { rect = r; kind = k; }
    }

    public class MapDef
    {
        public string key;
        public string name;
        public string tagline;
        public Palette palette;
        public string floorStyle;                  // slabs | hazard | tiles | ward | grass
        public Func<Rng, List<Obstacle>> layout;
        public int ambient;                        // extra brightness in the night overlay
    }

    // ----------------------------------------------------------------- floor bits
    public struct FRect { public float x, y, w, h; public Color c; }
    public struct FCircle { public float x, y, r; public Color c; }
    public struct FLine { public Vector2 a, b; public float w; public Color c; }

    /// <summary>
    /// The baked look of one arena floor. Primitives are generated once when the
    /// arena is built and culled against the camera every frame, which keeps the
    /// vector look of the original without a 2800x2100 texture.
    /// </summary>
    public class FloorPaint
    {
        public Color background;
        public readonly List<FRect> rects = new List<FRect>();
        public readonly List<FCircle> circles = new List<FCircle>();
        public readonly List<FLine> lines = new List<FLine>();
        public readonly List<FCircle> decals = new List<FCircle>();   // blood and scorch

        const int MaxDecals = 2600;

        public void AddRect(float x, float y, float w, float h, Color c)
        {
            rects.Add(new FRect { x = x, y = y, w = w, h = h, c = c });
        }

        public void AddCircle(float x, float y, float r, Color c)
        {
            circles.Add(new FCircle { x = x, y = y, r = r, c = c });
        }

        public void AddLine(Vector2 a, Vector2 b, float w, Color c)
        {
            lines.Add(new FLine { a = a, b = b, w = w, c = c });
        }

        public void AddDecal(float x, float y, float r, Color c)
        {
            if (decals.Count >= MaxDecals) decals.RemoveAt(0);
            decals.Add(new FCircle { x = x, y = y, r = r, c = c });
        }

        public void Draw(Vector2 cam)
        {
            float left = cam.x - 40f, top = cam.y - 40f;
            float right = cam.x + S.WIDTH + 40f, bottom = cam.y + S.HEIGHT + 40f;

            Painter.Rect(0f, 0f, S.WIDTH, S.HEIGHT, background);

            for (int i = 0; i < rects.Count; i++)
            {
                FRect r = rects[i];
                if (r.x > right || r.x + r.w < left || r.y > bottom || r.y + r.h < top) continue;
                Painter.Rect(r.x - cam.x, r.y - cam.y, r.w, r.h, r.c);
            }

            for (int i = 0; i < circles.Count; i++)
            {
                FCircle c = circles[i];
                if (c.x + c.r < left || c.x - c.r > right || c.y + c.r < top || c.y - c.r > bottom) continue;
                Painter.Circle(c.x - cam.x, c.y - cam.y, c.r, c.c);
            }

            for (int i = 0; i < lines.Count; i++)
            {
                FLine l = lines[i];
                float lx = Mathf.Min(l.a.x, l.b.x), rx = Mathf.Max(l.a.x, l.b.x);
                float ly = Mathf.Min(l.a.y, l.b.y), ry = Mathf.Max(l.a.y, l.b.y);
                if (rx < left || lx > right || ry < top || ly > bottom) continue;
                Painter.Line(l.a - cam, l.b - cam, l.w, l.c);
            }

            for (int i = 0; i < decals.Count; i++)
            {
                FCircle c = decals[i];
                if (c.x + c.r < left || c.x - c.r > right || c.y + c.r < top || c.y - c.r > bottom) continue;
                Painter.Circle(c.x - cam.x, c.y - cam.y, c.r, c.c);
            }
        }
    }

    public static class Maps
    {
        // ------------------------------------------------------- layout helpers
        static List<Obstacle> Border()
        {
            int w = S.WORLD_W, h = S.WORLD_H, t = S.WALL;
            return new List<Obstacle>
            {
                new Obstacle(new RectI(0, 0, w, t), "wall"),
                new Obstacle(new RectI(0, h - t, w, t), "wall"),
                new Obstacle(new RectI(0, 0, t, h), "wall"),
                new Obstacle(new RectI(w - t, 0, t, h), "wall")
            };
        }

        static RectI SafeZone()
        {
            return new RectI(S.WORLD_W / 2 - 160, S.WORLD_H / 2 - 160, 320, 320);
        }

        static bool Fits(RectI rect, List<Obstacle> obstacles, int pad, RectI? safe)
        {
            if (safe.HasValue && rect.Overlaps(safe.Value)) return false;
            RectI grown = rect.Inflate(pad, pad);
            for (int i = 0; i < obstacles.Count; i++)
                if (grown.Overlaps(obstacles[i].rect)) return false;
            return true;
        }

        struct KindSizes
        {
            public string kind;
            public Vector2Int[] sizes;
            public KindSizes(string k, Vector2Int[] s) { kind = k; sizes = s; }
        }

        static Vector2Int V(int x, int y) { return new Vector2Int(x, y); }

        static void Scatter(Rng rng, List<Obstacle> obstacles, KindSizes[] kinds,
                            int count, int tries = 900, int pad = 110)
        {
            RectI safe = SafeZone();
            int placed = 0;
            for (int i = 0; i < tries && placed < count; i++)
            {
                KindSizes ks = kinds[(int)(rng.NextUInt() % (uint)kinds.Length)];
                Vector2Int size = ks.sizes[(int)(rng.NextUInt() % (uint)ks.sizes.Length)];
                int x = rng.RangeInt(S.WALL + 40, S.WORLD_W - S.WALL - 40 - size.x);
                int y = rng.RangeInt(S.WALL + 40, S.WORLD_H - S.WALL - 40 - size.y);
                RectI r = new RectI(x, y, size.x, size.y);
                if (Fits(r, obstacles, pad, safe))
                {
                    obstacles.Add(new Obstacle(r, ks.kind));
                    placed++;
                }
            }
        }

        // -------------------------------------------------------------- layouts
        /// <summary>Open car park: long sight lines, scattered hard cover.</summary>
        public static List<Obstacle> LayoutLot(Rng rng)
        {
            var obs = Border();
            Scatter(rng, obs, new[]
            {
                new KindSizes("container", new[] { V(300, 96), V(96, 260), V(220, 96) }),
                new KindSizes("crate", new[] { V(66, 66), V(86, 86) }),
                new KindSizes("car", new[] { V(150, 78), V(78, 150) }),
                new KindSizes("block", new[] { V(130, 120), V(170, 108) })
            }, 32);
            return obs;
        }

        /// <summary>Steel mill: machine rows make corridors you can get pinched in.</summary>
        public static List<Obstacle> LayoutMill(Rng rng)
        {
            var obs = Border();
            const int cols = 4, rows = 3;
            float cw = (S.WORLD_W - S.WALL * 2f) / cols;
            float ch = (S.WORLD_H - S.WALL * 2f) / rows;
            RectI safe = SafeZone();

            for (int cx = 0; cx < cols; cx++)
            {
                for (int cy = 0; cy < rows; cy++)
                {
                    float x0 = S.WALL + cx * cw;
                    float y0 = S.WALL + cy * ch;
                    if (rng.Value < 0.22f) continue;

                    float w, h;
                    if (rng.Value < 0.5f) { w = cw * rng.Range(0.5f, 0.78f); h = rng.Range(70f, 100f); }
                    else { w = rng.Range(70f, 100f); h = ch * rng.Range(0.5f, 0.78f); }

                    RectI r = new RectI(
                        (int)(x0 + (cw - w) / 2f + rng.Range(-24f, 24f)),
                        (int)(y0 + (ch - h) / 2f + rng.Range(-24f, 24f)),
                        (int)w, (int)h);
                    if (r.Overlaps(safe)) continue;
                    obs.Add(new Obstacle(r, "machine"));
                }
            }

            Scatter(rng, obs, new[]
            {
                new KindSizes("barrel", new[] { V(54, 54) }),
                new KindSizes("crate", new[] { V(70, 70) })
            }, 14, 900, 70);
            return obs;
        }

        /// <summary>Platform between two track trenches, pillars down the middle.</summary>
        public static List<Obstacle> LayoutMetro(Rng rng)
        {
            var obs = Border();
            const int trackH = 150, gap = 190;
            int cy = S.WORLD_H / 2;

            for (int s = -1; s <= 1; s += 2)
            {
                int y = cy + s * (gap + trackH / 2) - trackH / 2;
                obs.Add(new Obstacle(new RectI(S.WALL, y, S.WORLD_W - S.WALL * 2, trackH), "track"));
            }

            const int n = 7;
            for (int i = 0; i < n; i++)
            {
                float x = S.WALL + 120 + i * (S.WORLD_W - S.WALL * 2 - 240) / (float)(n - 1);
                obs.Add(new Obstacle(new RectI((int)x - 26, cy - 26, 52, 52), "pillar"));
            }

            for (int i = 0; i < 6; i++)
            {
                float x = S.WALL + 180 + i * (S.WORLD_W - S.WALL * 2 - 360) / 5f;
                int side = (i % 2 == 0) ? -1 : 1;
                int y = cy + side * 120;
                obs.Add(new Obstacle(new RectI((int)x - 55, y - 20, 110, 40), "bench"));
            }

            int[] rowsY = { S.WALL + 80, S.WORLD_H - S.WALL - 160 };
            foreach (int y in rowsY)
                for (int i = 0; i < 4; i++)
                    obs.Add(new Obstacle(new RectI(S.WALL + 200 + i * 520, y, 90, 80), "kiosk"));

            return obs;
        }

        /// <summary>Hospital wing: rooms with doorways, tight angles, bad places to reload.</summary>
        public static List<Obstacle> LayoutWard(Rng rng)
        {
            var obs = Border();
            RectI safe = SafeZone();
            const int cols = 3, rows = 2, t = 26, dw = 120;
            float mw = (S.WORLD_W - S.WALL * 2f) / cols;
            float mh = (S.WORLD_H - S.WALL * 2f) / rows;
            string[] sides = { "n", "s", "e", "w" };

            for (int cx = 0; cx < cols; cx++)
            {
                for (int cy = 0; cy < rows; cy++)
                {
                    int x0 = (int)(S.WALL + cx * mw + 60);
                    int y0 = (int)(S.WALL + cy * mh + 60);
                    RectI room = new RectI(x0, y0, (int)(mw - 170), (int)(mh - 170));
                    if (room.Overlaps(safe)) continue;

                    string door = rng.Pick(sides);

                    AddWardSide(obs, room, t, dw, "n", door);
                    AddWardSide(obs, room, t, dw, "s", door);
                    AddWardSide(obs, room, t, dw, "w", door);
                    AddWardSide(obs, room, t, dw, "e", door);

                    int beds = rng.RangeInt(1, 2);
                    for (int b = 0; b < beds; b++)
                    {
                        int bw, bh;
                        if (rng.Value < 0.5f) { bw = 64; bh = 104; } else { bw = 104; bh = 64; }
                        int bx = rng.RangeInt(room.x + t + 20,
                                              Mathf.Max(room.x + t + 21, room.Right - t - bw - 20));
                        int by = rng.RangeInt(room.y + t + 20,
                                              Mathf.Max(room.y + t + 21, room.Bottom - t - bh - 20));
                        obs.Add(new Obstacle(new RectI(bx, by, bw, bh), "bed"));
                    }
                }
            }

            Scatter(rng, obs, new[]
            {
                new KindSizes("cabinet", new[] { V(60, 96), V(96, 60) })
            }, 8, 900, 60);
            return obs;
        }

        static void AddWardSide(List<Obstacle> obs, RectI room, int t, int dw,
                                string side, string door)
        {
            RectI r;
            switch (side)
            {
                case "n": r = new RectI(room.x, room.y, room.w, t); break;
                case "s": r = new RectI(room.x, room.Bottom - t, room.w, t); break;
                case "w": r = new RectI(room.x, room.y, t, room.h); break;
                default: r = new RectI(room.Right - t, room.y, t, room.h); break;
            }

            if (side != door)
            {
                obs.Add(new Obstacle(r, "ward_wall"));
                return;
            }

            if (side == "n" || side == "s")
            {
                int mid = room.CenterX;
                obs.Add(new Obstacle(new RectI(r.x, r.y, mid - dw / 2 - r.x, t), "ward_wall"));
                obs.Add(new Obstacle(new RectI(mid + dw / 2, r.y,
                                               r.Right - (mid + dw / 2), t), "ward_wall"));
            }
            else
            {
                int mid = room.CenterY;
                obs.Add(new Obstacle(new RectI(r.x, r.y, t, mid - dw / 2 - r.y), "ward_wall"));
                obs.Add(new Obstacle(new RectI(r.x, mid + dw / 2, t,
                                               r.Bottom - (mid + dw / 2)), "ward_wall"));
            }
        }

        /// <summary>Overgrown park: fountain in the middle, booths and hedges for cover.</summary>
        public static List<Obstacle> LayoutPark(Rng rng)
        {
            var obs = Border();
            int cx = S.WORLD_W / 2, cy = S.WORLD_H / 2;
            obs.Add(new Obstacle(new RectI(cx - 150, cy - 150, 300, 300), "fountain"));

            RectI bounds = new RectI(S.WALL + 30, S.WALL + 30,
                                     S.WORLD_W - S.WALL * 2 - 60, S.WORLD_H - S.WALL * 2 - 60);

            for (int i = 0; i < 10; i++)
            {
                float a = MathUtil.TAU * i / 10f + rng.Range(-0.2f, 0.2f);
                float r = rng.Range(430f, 760f);
                int x = (int)(cx + Mathf.Cos(a) * r);
                int y = (int)(cy + Mathf.Sin(a) * r * 0.75f);
                string kind = (i % 3 != 0) ? "booth" : "hedge";
                int w = kind == "booth" ? 120 : 190;
                int h = kind == "booth" ? 90 : 60;
                if (rng.Value < 0.5f) { int tmp = w; w = h; h = tmp; }

                RectI rect = new RectI(x - w / 2, y - h / 2, w, h).ClampInside(bounds);
                if (Fits(rect, obs, 90, SafeZone()))
                    obs.Add(new Obstacle(rect, kind));
            }

            Scatter(rng, obs, new[]
            {
                new KindSizes("tree", new[] { V(74, 74) }),
                new KindSizes("bench", new[] { V(110, 40), V(40, 110) })
            }, 16, 900, 80);
            return obs;
        }

        // ------------------------------------------------------------ map table
        public static readonly List<MapDef> All = new List<MapDef>
        {
            new MapDef {
                key = "lot", name = "PARKING LOT", tagline = "open ground, long sight lines",
                palette = new Palette {
                    floor = S.Col(40,42,46), floorAlt = S.Col(34,36,40), grime = S.Col(27,28,31),
                    line = S.Col(150,142,96), wall = S.Col(56,54,52), wallTop = S.Col(74,71,67),
                    accent = S.Col(96,70,38), accent2 = S.Col(46,74,70) },
                floorStyle = "slabs", layout = LayoutLot },

            new MapDef {
                key = "mill", name = "STEEL MILL", tagline = "machine corridors, nowhere to run",
                palette = new Palette {
                    floor = S.Col(46,44,42), floorAlt = S.Col(38,36,35), grime = S.Col(30,28,27),
                    line = S.Col(188,150,50), wall = S.Col(62,56,50), wallTop = S.Col(84,76,66),
                    accent = S.Col(126,96,44), accent2 = S.Col(70,74,82) },
                floorStyle = "hazard", layout = LayoutMill, ambient = 10 },

            new MapDef {
                key = "metro", name = "METRO STATION", tagline = "two trenches, one platform",
                palette = new Palette {
                    floor = S.Col(52,52,58), floorAlt = S.Col(44,44,50), grime = S.Col(32,32,38),
                    line = S.Col(190,186,170), wall = S.Col(60,60,68), wallTop = S.Col(80,80,90),
                    accent = S.Col(70,90,110), accent2 = S.Col(120,120,130) },
                floorStyle = "tiles", layout = LayoutMetro, ambient = 10 },

            new MapDef {
                key = "ward", name = "ST. MERCY WARD", tagline = "rooms, doorways, bad angles",
                palette = new Palette {
                    floor = S.Col(62,66,64), floorAlt = S.Col(55,59,57), grime = S.Col(42,46,44),
                    line = S.Col(160,180,172), wall = S.Col(72,78,76), wallTop = S.Col(96,104,100),
                    accent = S.Col(150,158,156), accent2 = S.Col(96,126,118) },
                floorStyle = "ward", layout = LayoutWard, ambient = 20 },

            new MapDef {
                key = "park", name = "RIVERSIDE PARK", tagline = "open green, fountain in the middle",
                palette = new Palette {
                    floor = S.Col(38,48,36), floorAlt = S.Col(33,42,31), grime = S.Col(27,34,26),
                    line = S.Col(120,112,86), wall = S.Col(54,58,48), wallTop = S.Col(70,76,62),
                    accent = S.Col(92,66,38), accent2 = S.Col(58,86,54) },
                floorStyle = "grass", layout = LayoutPark, ambient = 8 }
        };

        public static MapDef Get(string key)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].key == key) return All[i];
            return All[0];
        }

        public static int IndexOf(string key)
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].key == key) return i;
            return 0;
        }

        // -------------------------------------------------------------- floors
        public static FloorPaint BuildFloor(MapDef m, Rng rng)
        {
            Palette p = m.palette;
            var f = new FloorPaint { background = p.floor };

            switch (m.floorStyle)
            {
                case "slabs":
                {
                    const int step = 140;
                    for (int y = 0; y < S.WORLD_H; y += step)
                        for (int x = 0; x < S.WORLD_W; x += step)
                            if (((x / step) + (y / step)) % 2 == 0)
                                f.AddRect(x, y, step, step, p.floorAlt);

                    for (int y = 0; y < S.WORLD_H; y += step)
                        f.AddLine(new Vector2(0, y), new Vector2(S.WORLD_W, y), 1f, p.grime);
                    for (int x = 0; x < S.WORLD_W; x += step)
                        f.AddLine(new Vector2(x, 0), new Vector2(x, S.WORLD_H), 1f, p.grime);

                    for (int bay = 0; bay < S.WORLD_W; bay += 520)
                        for (int i = 0; i < 5; i++)
                        {
                            int x = bay + 60 + i * 86;
                            f.AddRect(x, 260, 5, 150, p.line);
                            f.AddRect(x, S.WORLD_H - 360, 5, 150, p.line);
                        }
                    break;
                }
                case "hazard":
                {
                    const int step = 120;
                    for (int y = 0; y < S.WORLD_H; y += step)
                        f.AddRect(0, y, S.WORLD_W, step / 2, p.floorAlt);
                    for (int x = 0; x < S.WORLD_W; x += 24)
                        f.AddLine(new Vector2(x, 0), new Vector2(x, S.WORLD_H), 1f, p.grime);

                    int[] stripes = { S.WALL + 60, S.WORLD_H - S.WALL - 90 };
                    foreach (int y in stripes)
                        for (int x = S.WALL; x < S.WORLD_W - S.WALL; x += 46)
                        {
                            // hazard chevrons, drawn as two triangles worth of rects
                            f.AddLine(new Vector2(x + 12, y), new Vector2(x - 2, y + 30), 9f, p.line);
                            f.AddLine(new Vector2(x + 24, y), new Vector2(x + 10, y + 30), 9f, p.line);
                        }
                    break;
                }
                case "tiles":
                {
                    const int step = 70;
                    for (int y = 0; y < S.WORLD_H; y += step)
                        for (int x = 0; x < S.WORLD_W; x += step)
                        {
                            Color c = (((x / step) + (y / step)) % 2 != 0) ? p.floor : p.floorAlt;
                            f.AddRect(x, y, step - 3, step - 3, c);
                        }
                    int cy = S.WORLD_H / 2;
                    for (int s = -1; s <= 1; s += 2)
                        f.AddRect(0, cy + s * 250 - 10, S.WORLD_W, 20, S.Col(62, 60, 56));
                    break;
                }
                case "ward":
                {
                    const int step = 56;
                    for (int y = 0; y < S.WORLD_H; y += step)
                        for (int x = 0; x < S.WORLD_W; x += step)
                        {
                            Color c = (((x / step) + (y / step)) % 2 != 0) ? p.floor : p.floorAlt;
                            f.AddRect(x, y, step - 2, step - 2, c);
                        }
                    for (int y = 240; y < S.WORLD_H; y += 600)
                        f.AddRect(0, y, S.WORLD_W, 6, p.accent2);
                    break;
                }
                default:   // grass
                {
                    Color[] blobs = { p.floorAlt, p.grime, S.Col(44, 56, 40) };
                    for (int i = 0; i < 2600; i++)
                        f.AddCircle(rng.RangeInt(0, S.WORLD_W), rng.RangeInt(0, S.WORLD_H),
                                    rng.RangeInt(8, 34), rng.Pick(blobs));

                    int cx = S.WORLD_W / 2, cy = S.WORLD_H / 2;
                    // the ring path, as a thick circle outline
                    for (int i = 0; i < 72; i++)
                    {
                        float a0 = MathUtil.TAU * i / 72f;
                        float a1 = MathUtil.TAU * (i + 1) / 72f;
                        f.AddLine(new Vector2(cx + Mathf.Cos(a0) * 420f, cy + Mathf.Sin(a0) * 420f),
                                  new Vector2(cx + Mathf.Cos(a1) * 420f, cy + Mathf.Sin(a1) * 420f),
                                  26f, p.line);
                    }
                    f.AddRect(0, cy - 40, S.WORLD_W, 80, p.line);
                    f.AddRect(cx - 40, 0, 80, S.WORLD_H, p.line);
                    break;
                }
            }

            // universal grime so no floor reads as a flat colour
            Color soft = S.Mix(p.floor, p.grime, 0.38f);
            Color softer = S.Mix(p.floor, p.grime, 0.18f);
            for (int i = 0; i < 520; i++)
                f.AddCircle(rng.RangeInt(0, S.WORLD_W), rng.RangeInt(0, S.WORLD_H),
                            rng.RangeInt(4, 18), rng.Value < 0.5f ? soft : softer);

            for (int i = 0; i < 110; i++)
            {
                float x = rng.RangeInt(0, S.WORLD_W);
                float y = rng.RangeInt(0, S.WORLD_H);
                int segs = rng.RangeInt(2, 5);
                for (int s = 0; s < segs; s++)
                {
                    float nx = x + rng.RangeInt(-60, 60);
                    float ny = y + rng.RangeInt(-60, 60);
                    f.AddLine(new Vector2(x, y), new Vector2(nx, ny), rng.RangeInt(1, 2), soft);
                    x = nx; y = ny;
                }
            }

            return f;
        }

        // --------------------------------------------------------------- props
        public static void DrawProp(Obstacle o, MapDef m, Vector2 cam)
        {
            RectI r = o.rect;
            Palette p = m.palette;
            float x = r.x - cam.x, y = r.y - cam.y;
            float w = r.w, h = r.h;
            float cx = x + w * 0.5f, cy = y + h * 0.5f;

            Painter.RoundRect(x - 2f, y + 8f, w + 18f, h + 18f, 8f, new Color(0f, 0f, 0f, 0.43f));

            switch (o.kind)
            {
                case "wall":
                    Painter.Rect(x, y, w, h, p.wall);
                    Painter.RectOutline(x, y, w, h, 6f, p.wallTop);
                    for (int i = 0; i < Mathf.Max(w, h); i += 90)
                    {
                        if (w > h) Painter.Line(x + i, y, x + i, y + h, 2f, p.grime);
                        else Painter.Line(x, y + i, x + w, y + i, 2f, p.grime);
                    }
                    break;

                case "crate":
                    Painter.RoundRect(x, y, w, h, 4f, p.accent);
                    Painter.RoundRect(x + 5f, y + 5f, w - 10f, h - 10f, 3f, S.Lighter(p.accent, 24));
                    Painter.Line(x, y, x + w, y + h, 3f, S.Darker(p.accent, 26));
                    Painter.Line(x + w, y, x, y + h, 3f, S.Darker(p.accent, 26));
                    Painter.RoundRectOutline(x, y, w, h, 4f, 3f, S.Darker(p.accent, 34));
                    break;

                case "container":
                {
                    Painter.RoundRect(x, y, w, h, 3f, p.accent2);
                    int n = Mathf.Max(3, (int)((w > h ? w : h) / 26f));
                    for (int i = 1; i < n; i++)
                    {
                        if (w > h)
                        {
                            float lx = x + w * i / n;
                            Painter.Line(lx, y + 4f, lx, y + h - 4f, 3f, S.Darker(p.accent2, 18));
                        }
                        else
                        {
                            float ly = y + h * i / n;
                            Painter.Line(x + 4f, ly, x + w - 4f, ly, 3f, S.Darker(p.accent2, 18));
                        }
                    }
                    Painter.RoundRectOutline(x, y, w, h, 3f, 4f, S.Darker(p.accent2, 26));
                    break;
                }

                case "car":
                {
                    Color[] bodies = { S.Col(78,36,36), S.Col(40,52,70), S.Col(62,64,68), S.Col(86,82,56) };
                    Color body = bodies[(r.x * 7 + r.y * 13) % bodies.Length];
                    Painter.RoundRect(x, y, w, h, 12f, body);
                    Painter.RoundRect(x + 13f, y + 13f, w - 26f, h - 26f, 8f, S.Col(38, 44, 52));
                    Painter.RoundRectOutline(x, y, w, h, 12f, 3f, S.Darker(body, 24));
                    break;
                }

                case "block":
                    Painter.RoundRect(x, y, w, h, 2f, S.Col(60, 62, 66));
                    Painter.RoundRect(x + 7f, y + 7f, w - 14f, h - 14f, 2f, S.Col(74, 76, 80));
                    Painter.RoundRectOutline(x, y, w, h, 2f, 3f, S.Col(44, 46, 50));
                    break;

                case "machine":
                {
                    Painter.RoundRect(x, y, w, h, 3f, S.Col(74, 70, 66));
                    Painter.RoundRect(x + 8f, y + 8f, w - 16f, h - 16f, 2f, S.Col(54, 52, 50));
                    for (int i = 0; i < Mathf.Max(w, h) - 20f; i += 34)
                    {
                        if (w > h) Painter.Rect(x + 12f + i, y + 8f, 16f, 7f, p.line);
                        else Painter.Rect(x + 8f, y + 12f + i, 7f, 16f, p.line);
                    }
                    Painter.RoundRectOutline(x, y, w, h, 3f, 3f, S.Col(36, 34, 32));
                    break;
                }

                case "barrel":
                    Painter.Circle(cx, cy, w * 0.5f, S.Col(120, 76, 40));
                    Painter.Circle(cx, cy, w * 0.5f - 6f, S.Col(150, 96, 50));
                    Painter.CircleOutline(cx, cy, w * 0.5f, 3f, S.Col(70, 44, 24));
                    break;

                case "track":
                {
                    Painter.Rect(x, y, w, h, S.Col(24, 24, 27));
                    float[] rails = { y + h / 3f, y + 2f * h / 3f };
                    foreach (float ry in rails)
                        Painter.Rect(x, ry - 4f, w, 8f, S.Col(96, 96, 104));
                    for (float sx = x; sx < x + w; sx += 54f)
                        Painter.Rect(sx, y + 10f, 22f, h - 20f, S.Col(52, 44, 38));
                    Painter.RectOutline(x, y, w, h, 4f, S.Col(16, 16, 18));
                    break;
                }

                case "pillar":
                    Painter.RoundRect(x, y, w, h, 4f, S.Col(86, 86, 94));
                    Painter.RoundRect(x + 7f, y + 7f, w - 14f, h - 14f, 3f, S.Col(64, 64, 72));
                    Painter.RoundRectOutline(x, y, w, h, 4f, 3f, S.Col(40, 40, 46));
                    break;

                case "bench":
                    Painter.RoundRect(x, y, w, h, 5f, S.Col(96, 70, 40));
                    Painter.RoundRectOutline(x, y, w, h, 5f, 3f, S.Col(70, 50, 28));
                    if (w > h)
                        for (float sx = x + 8f; sx < x + w - 8f; sx += 20f)
                            Painter.Line(sx, y + 4f, sx, y + h - 4f, 2f, S.Col(70, 50, 28));
                    else
                        for (float sy = y + 8f; sy < y + h - 8f; sy += 20f)
                            Painter.Line(x + 4f, sy, x + w - 4f, sy, 2f, S.Col(70, 50, 28));
                    break;

                case "kiosk":
                    Painter.RoundRect(x, y, w, h, 4f, S.Col(72, 64, 60));
                    Painter.RoundRect(x + 6f, y + 6f, w - 12f, 22f, 3f, p.accent);
                    Painter.RoundRectOutline(x, y, w, h, 4f, 3f, S.Col(40, 36, 34));
                    break;

                case "ward_wall":
                    Painter.Rect(x, y, w, h, S.Col(116, 122, 120));
                    Painter.Rect(x + 3f, y + 3f, w - 6f, h - 6f, S.Col(142, 148, 146));
                    Painter.RectOutline(x, y, w, h, 2f, S.Col(86, 92, 90));
                    break;

                case "bed":
                    Painter.RoundRect(x, y, w, h, 4f, S.Col(152, 156, 158));
                    Painter.RoundRect(x + 6f, y + 6f, w - 12f, h - 12f, 3f, S.Col(196, 200, 204));
                    if (h > w) Painter.Rect(x + 6f, y + 6f, w - 12f, 26f, S.Col(120, 132, 140));
                    else Painter.Rect(x + 6f, y + 6f, 26f, h - 12f, S.Col(120, 132, 140));
                    Painter.RoundRectOutline(x, y, w, h, 4f, 2f, S.Col(96, 102, 104));
                    break;

                case "cabinet":
                    Painter.RoundRect(x, y, w, h, 3f, S.Col(108, 116, 114));
                    Painter.RoundRect(x + 5f, y + 5f, w - 10f, h - 10f, 2f, S.Col(84, 92, 90));
                    Painter.RoundRectOutline(x, y, w, h, 3f, 2f, S.Col(60, 66, 64));
                    break;

                case "fountain":
                    Painter.Circle(cx, cy, w * 0.5f, S.Col(86, 90, 84));
                    Painter.Circle(cx, cy, w * 0.5f - 16f, S.Col(54, 74, 84));
                    Painter.Circle(cx, cy, w * 0.5f - 40f, S.Col(70, 96, 108));
                    Painter.Circle(cx, cy, 34f, S.Col(104, 108, 100));
                    Painter.CircleOutline(cx, cy, w * 0.5f, 5f, S.Col(64, 68, 62));
                    break;

                case "booth":
                {
                    Painter.RoundRect(x, y, w, h, 4f, S.Col(104, 58, 52));
                    float stripe = Mathf.Max(12f, w / 6f);
                    int idx = 0;
                    for (float sx = x; sx < x + w; sx += stripe, idx++)
                        if (idx % 2 == 0)
                            Painter.Rect(sx, y, Mathf.Min(stripe, x + w - sx), 20f, S.Col(196, 196, 190));
                    Painter.RoundRectOutline(x, y, w, h, 4f, 3f, S.Col(66, 36, 32));
                    break;
                }

                case "hedge":
                {
                    Painter.RoundRect(x, y, w, h, 10f, S.Col(44, 70, 40));
                    if (o.detail == null)
                    {
                        var hr = new Rng(r.x * 73856093 ^ r.y * 19349663 ^ 0x1f3b);
                        int n = Mathf.Min(260, (int)(r.w * r.h / 260f));
                        o.detail = new float[n * 4];
                        for (int i = 0; i < n; i++)
                        {
                            o.detail[i * 4 + 0] = 4f + hr.Value * (r.w - 8f);
                            o.detail[i * 4 + 1] = 4f + hr.Value * (r.h - 8f);
                            o.detail[i * 4 + 2] = hr.RangeInt(4, 9);
                            o.detail[i * 4 + 3] = hr.Value;
                        }
                    }
                    for (int i = 0; i + 3 < o.detail.Length; i += 4)
                        Painter.Circle(x + o.detail[i], y + o.detail[i + 1], o.detail[i + 2],
                                       o.detail[i + 3] < 0.5f ? S.Col(52, 84, 46) : S.Col(38, 62, 36));
                    Painter.RoundRectOutline(x, y, w, h, 10f, 3f, S.Col(30, 48, 28));
                    break;
                }

                case "tree":
                    Painter.Circle(cx, cy, w * 0.5f, S.Col(36, 54, 34));
                    Painter.Circle(cx, cy, w * 0.5f - 8f, S.Col(48, 72, 44));
                    Painter.Circle(cx, cy, 10f, S.Col(58, 42, 28));
                    break;

                default:
                    Painter.RoundRect(x, y, w, h, 3f, S.Col(70, 72, 76));
                    Painter.RoundRectOutline(x, y, w, h, 3f, 3f, S.Col(48, 50, 54));
                    break;
            }
        }

        // ------------------------------------------------- schematic preview
        static readonly Dictionary<string, List<Obstacle>> _previewCache =
            new Dictionary<string, List<Obstacle>>();

        /// <summary>
        /// Layout for the thumbnail, generated once per map. Regenerating it every
        /// frame would run the whole scatter pass five times while the map screen is
        /// open, so it is cached behind the map key.
        /// </summary>
        public static List<Obstacle> PreviewLayout(MapDef m)
        {
            List<Obstacle> obs;
            if (!_previewCache.TryGetValue(m.key, out obs))
            {
                obs = m.layout(new Rng(7));
                _previewCache[m.key] = obs;
            }
            return obs;
        }

        /// <summary>Little top-down sketch for the map select screen.</summary>
        public static void DrawPreview(MapDef m, RectI dest)
        {
            var obs = PreviewLayout(m);
            Palette p = m.palette;
            Painter.Rect(dest.x, dest.y, dest.w, dest.h, p.floor);

            float sx = dest.w / (float)S.WORLD_W;
            float sy = dest.h / (float)S.WORLD_H;

            for (int i = 0; i < obs.Count; i++)
            {
                RectI r = obs[i].rect;
                string k = obs[i].kind;
                Color col;
                if (k == "wall") col = p.wall;
                else if (k == "crate" || k == "bench" || k == "booth" || k == "kiosk") col = p.accent;
                else if (k == "container" || k == "hedge" || k == "tree") col = p.accent2;
                else if (k == "track") col = S.Col(28, 28, 32);
                else col = p.wallTop;

                Painter.Rect(dest.x + r.x * sx, dest.y + r.y * sy,
                             Mathf.Max(2f, r.w * sx), Mathf.Max(2f, r.h * sy), col);
            }

            Painter.Circle(dest.CenterX, dest.CenterY, 4f, S.C_CYAN);
            Painter.RectOutline(dest.x, dest.y, dest.w, dest.h, 2f, S.Col(90, 94, 102));
        }
    }
}
