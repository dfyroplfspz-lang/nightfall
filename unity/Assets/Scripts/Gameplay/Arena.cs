// C# / Assets/Scripts/Gameplay/Arena.cs / Unity 2022.3+ / one playable arena built from a MapDef
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    /// <summary>
    /// Layout, collision and line of sight for a single run. The floor is baked into
    /// a list of primitives once and culled per frame; blood accumulates as decals.
    /// </summary>
    public class Arena
    {
        public readonly Rng rng;
        public readonly MapDef mdef;
        public readonly Palette palette;
        public readonly Vector2 center;
        public readonly List<Obstacle> obstacles;
        public readonly FloorPaint floor;
        public readonly List<Vector2> spawnPoints = new List<Vector2>();

        const int RCELL = 160;
        readonly Dictionary<long, List<RectI>> _rgrid = new Dictionary<long, List<RectI>>();
        readonly List<RectI> _nearby = new List<RectI>(32);

        public Arena(int seed, MapDef def)
        {
            rng = new Rng(seed);
            mdef = def ?? Maps.All[0];
            palette = mdef.palette;
            center = new Vector2(S.WORLD_W * 0.5f, S.WORLD_H * 0.5f);

            var buildRng = new Rng(seed);
            obstacles = mdef.layout(buildRng);
            BuildRectGrid();

            floor = Maps.BuildFloor(mdef, buildRng);

            MakeSpawnPoints();
        }

        // ------------------------------------------------------- broadphase
        static long Key(int gx, int gy) { return ((long)gx << 32) ^ (uint)gy; }

        void BuildRectGrid()
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                RectI r = obstacles[i].rect;
                for (int gx = r.Left / RCELL; gx <= r.Right / RCELL; gx++)
                {
                    for (int gy = r.Top / RCELL; gy <= r.Bottom / RCELL; gy++)
                    {
                        long k = Key(gx, gy);
                        List<RectI> bucket;
                        if (!_rgrid.TryGetValue(k, out bucket))
                        {
                            bucket = new List<RectI>(4);
                            _rgrid[k] = bucket;
                        }
                        bucket.Add(r);
                    }
                }
            }
        }

        /// <summary>Shared buffer — consume it before the next call.</summary>
        public List<RectI> NearbyRects(Vector2 pos, float radius)
        {
            _nearby.Clear();
            int x0 = Mathf.FloorToInt((pos.x - radius) / RCELL);
            int x1 = Mathf.FloorToInt((pos.x + radius) / RCELL);
            int y0 = Mathf.FloorToInt((pos.y - radius) / RCELL);
            int y1 = Mathf.FloorToInt((pos.y + radius) / RCELL);

            for (int gx = x0; gx <= x1; gx++)
            {
                for (int gy = y0; gy <= y1; gy++)
                {
                    List<RectI> bucket;
                    if (!_rgrid.TryGetValue(Key(gx, gy), out bucket)) continue;
                    // duplicates across cells are harmless: a second resolve is a no-op
                    for (int i = 0; i < bucket.Count; i++) _nearby.Add(bucket[i]);
                }
            }
            return _nearby;
        }

        List<RectI> RectsInBox(Vector2 a, Vector2 b)
        {
            Vector2 mid = (a + b) * 0.5f;
            float rad = Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y)) * 0.5f + 4f;
            return NearbyRects(mid, rad);
        }

        // ----------------------------------------------------------- spawns
        void MakeSpawnPoints()
        {
            const int m = S.SPAWN_MARGIN;
            for (int x = m; x < S.WORLD_W - m; x += 90)
            {
                TryAddSpawn(new Vector2(x, m));
                TryAddSpawn(new Vector2(x, S.WORLD_H - m));
            }
            for (int y = m; y < S.WORLD_H - m; y += 90)
            {
                TryAddSpawn(new Vector2(m, y));
                TryAddSpawn(new Vector2(S.WORLD_W - m, y));
            }
        }

        void TryAddSpawn(Vector2 p)
        {
            if (!Blocked(p, 26f)) spawnPoints.Add(p);
        }

        /// <summary>A ring just outside the player's view, so the horde arrives fast but unseen.</summary>
        public Vector2 PickSpawn(Vector2 playerPos, Rng r)
        {
            const float m = S.WALL + 30;
            for (int i = 0; i < 48; i++)
            {
                float ang = r.Range(0f, 360f);
                float dist = r.Range(S.SPAWN_MIN_DIST, S.SPAWN_MIN_DIST + 300f);
                Vector2 p = playerPos + MathUtil.FromAngle(ang, dist);
                if (p.x <= m || p.x >= S.WORLD_W - m || p.y <= m || p.y >= S.WORLD_H - m) continue;
                if (Blocked(p, 30f)) continue;
                return p;
            }

            if (spawnPoints.Count == 0) return center;
            for (int i = 0; i < 24; i++)
            {
                Vector2 p = r.Pick(spawnPoints);
                if ((p - playerPos).magnitude > S.SPAWN_MIN_DIST * 0.7f)
                    return p + new Vector2(r.Range(-24f, 24f), r.Range(-24f, 24f));
            }
            Vector2 q = r.Pick(spawnPoints);
            return q + new Vector2(r.Range(-24f, 24f), r.Range(-24f, 24f));
        }

        public Vector2 FreePoint(Rng r, Vector2? origin = null, float minD = 0f,
                                 float maxD = 1e9f, float radius = 24f)
        {
            for (int i = 0; i < 60; i++)
            {
                Vector2 p = new Vector2(r.Range(S.WALL + 60, S.WORLD_W - S.WALL - 60),
                                        r.Range(S.WALL + 60, S.WORLD_H - S.WALL - 60));
                if (Blocked(p, radius)) continue;
                if (origin.HasValue)
                {
                    float d = (p - origin.Value).magnitude;
                    if (d < minD || d > maxD) continue;
                }
                return p;
            }
            return center;
        }

        // ---------------------------------------------------------- queries
        public bool Blocked(Vector2 pos, float radius)
        {
            var rects = NearbyRects(pos, radius);
            for (int i = 0; i < rects.Count; i++)
            {
                RectI r = rects[i];
                float cx = MathUtil.Clamp(pos.x, r.Left, r.Right);
                float cy = MathUtil.Clamp(pos.y, r.Top, r.Bottom);
                float dx = pos.x - cx, dy = pos.y - cy;
                if (dx * dx + dy * dy < radius * radius) return true;
            }
            return false;
        }

        static readonly List<RectI> _collideBuf = new List<RectI>(32);

        public bool Collide(ref Vector2 pos, float radius)
        {
            _collideBuf.Clear();
            _collideBuf.AddRange(NearbyRects(pos, radius + 2f));

            bool hit = false;
            for (int i = 0; i < _collideBuf.Count; i++)
                if (MathUtil.ResolveCircleRect(ref pos, radius, _collideBuf[i])) hit = true;

            pos.x = MathUtil.Clamp(pos.x, radius, S.WORLD_W - radius);
            pos.y = MathUtil.Clamp(pos.y, radius, S.WORLD_H - radius);
            return hit;
        }

        public Vector2? RayBlocked(Vector2 a, Vector2 b)
        {
            Vector2? best = null;
            float bestD = float.MaxValue;
            var rects = RectsInBox(a, b);
            for (int i = 0; i < rects.Count; i++)
            {
                Vector2? clip = rects[i].ClipLine(a, b);
                if (!clip.HasValue) continue;
                float d = (clip.Value - a).sqrMagnitude;
                if (d < bestD) { bestD = d; best = clip; }
            }
            return best;
        }

        public bool HasLos(Vector2 a, Vector2 b) { return !RayBlocked(a, b).HasValue; }

        // ----------------------------------------------------------- decals
        public void Splat(Vector2 pos, int amount = 6, float spread = 18f,
                          Color? color = null, int sizeMin = 3, int sizeMax = 9)
        {
            if (Blocked(pos, 4f)) return;
            Color col = color ?? (Color)S.C_BLOOD;
            for (int i = 0; i < amount; i++)
            {
                float x = pos.x + rng.Range(-spread, spread);
                float y = pos.y + rng.Range(-spread, spread);
                if (x < 0f || x >= S.WORLD_W || y < 0f || y >= S.WORLD_H) continue;
                floor.AddDecal(x, y, rng.RangeInt(sizeMin, sizeMax), col);
            }
        }

        public void Scorch(Vector2 pos, float radius = 70f)
        {
            floor.AddDecal(pos.x, pos.y, radius, S.Col(18, 18, 20));
        }

        // ------------------------------------------------------------- draw
        public void Draw(Vector2 cam)
        {
            floor.Draw(cam);

            float left = cam.x - 80f, top = cam.y - 80f;
            float right = cam.x + S.WIDTH + 80f, bottom = cam.y + S.HEIGHT + 80f;

            for (int i = 0; i < obstacles.Count; i++)
            {
                RectI r = obstacles[i].rect;
                if (r.Right < left || r.Left > right || r.Bottom < top || r.Top > bottom) continue;
                Maps.DrawProp(obstacles[i], mdef, cam);
            }
        }
    }
}
