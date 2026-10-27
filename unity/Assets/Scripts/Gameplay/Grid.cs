// C# / Assets/Scripts/Gameplay/Grid.cs / Unity 2022.3+ / spatial hash for the horde
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    /// <summary>
    /// Rebuilt every frame from the live zombies. Separation and bullet sweeps both
    /// query it, which is what keeps a hundred zombies off the frame budget.
    /// </summary>
    public class Grid
    {
        readonly int _cell;
        readonly Dictionary<long, List<Zombie>> _cells = new Dictionary<long, List<Zombie>>();
        readonly Stack<List<Zombie>> _pool = new Stack<List<Zombie>>();
        readonly List<Zombie> _result = new List<Zombie>(64);
        readonly HashSet<Zombie> _seen = new HashSet<Zombie>();

        public Grid(int cell) { _cell = Mathf.Max(16, cell); }

        static long Key(int gx, int gy)
        {
            return ((long)gx << 32) ^ (uint)gy;
        }

        int CellOf(float v) { return Mathf.FloorToInt(v / _cell); }

        public void Build(List<Zombie> zombies)
        {
            foreach (var kv in _cells)
            {
                kv.Value.Clear();
                _pool.Push(kv.Value);
            }
            _cells.Clear();

            for (int i = 0; i < zombies.Count; i++)
            {
                Zombie z = zombies[i];
                long k = Key(CellOf(z.pos.x), CellOf(z.pos.y));
                List<Zombie> bucket;
                if (!_cells.TryGetValue(k, out bucket))
                {
                    bucket = _pool.Count > 0 ? _pool.Pop() : new List<Zombie>(8);
                    _cells[k] = bucket;
                }
                bucket.Add(z);
            }
        }

        /// <summary>Shared buffer — copy it if you need to keep the result.</summary>
        public List<Zombie> Query(Vector2 pos, float radius)
        {
            _result.Clear();
            int x0 = CellOf(pos.x - radius), x1 = CellOf(pos.x + radius);
            int y0 = CellOf(pos.y - radius), y1 = CellOf(pos.y + radius);
            for (int gx = x0; gx <= x1; gx++)
            {
                for (int gy = y0; gy <= y1; gy++)
                {
                    List<Zombie> bucket;
                    if (!_cells.TryGetValue(Key(gx, gy), out bucket)) continue;
                    for (int i = 0; i < bucket.Count; i++) _result.Add(bucket[i]);
                }
            }
            return _result;
        }

        /// <summary>Everything near the segment a..b, deduplicated.</summary>
        public List<Zombie> QuerySegment(Vector2 a, Vector2 b, float pad)
        {
            _result.Clear();
            _seen.Clear();
            int x0 = CellOf(Mathf.Min(a.x, b.x) - pad), x1 = CellOf(Mathf.Max(a.x, b.x) + pad);
            int y0 = CellOf(Mathf.Min(a.y, b.y) - pad), y1 = CellOf(Mathf.Max(a.y, b.y) + pad);
            for (int gx = x0; gx <= x1; gx++)
            {
                for (int gy = y0; gy <= y1; gy++)
                {
                    List<Zombie> bucket;
                    if (!_cells.TryGetValue(Key(gx, gy), out bucket)) continue;
                    for (int i = 0; i < bucket.Count; i++)
                        if (_seen.Add(bucket[i])) _result.Add(bucket[i]);
                }
            }
            return _result;
        }
    }
}
