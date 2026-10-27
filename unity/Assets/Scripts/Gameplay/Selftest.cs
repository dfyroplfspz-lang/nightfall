// C# / Assets/Scripts/Gameplay/Selftest.cs / Unity 2022.3+ / headless run of the whole game loop
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Nightfall
{
    /// <summary>
    /// Drives a complete run with scripted input and no window: the same simulation the
    /// editor runs, minus rendering. It is how the game gets checked without opening
    /// Unity, and how a regression shows up as a number instead of a crash in play mode.
    ///
    /// From the editor: NIGHTFALL -> Run self-test.
    /// </summary>
    public static class Selftest
    {
        public class Result
        {
            public string mode, map;
            public int frames, waves, kills, maxZombies, pickupsSeen, bosses, deaths;
            public int shots, hits, bulletsPeak, wallStops;
            public int cases, scrap;
            public readonly HashSet<string> typesSeen = new HashSet<string>();
            public readonly List<string> problems = new List<string>();
            public bool Ok { get { return problems.Count == 0; } }
        }

        /// <summary>One run. frames at a fixed 60 Hz step.</summary>
        public static Result Run(string modeKey, string mapKey, int frames = 1800, int seed = 7)
        {
            var res = new Result { mode = modeKey, map = mapKey, frames = frames };

            GameApp.HeadlessRequested = true;
            GameApp.HeadlessSeed = seed;
            var go = new GameObject("NightfallSelftest");
            GameApp app = go.AddComponent<GameApp>();
            app.BootHeadless(seed);      // no-op when Awake already did it
            GameApp.HeadlessRequested = false;

            app.StartRun(Modes.Get(modeKey), Maps.Get(mapKey));

            var rng = new Rng(seed ^ 0x51ed270b);
            const float dt = 1f / 60f;

            for (int f = 0; f < frames; f++)
            {
                if (app.state == GameState.GameOver)
                {
                    res.deaths++;
                    app.StartRun(null, null);
                }

                var inp = new GameApp.InputFrame { selectSlot = -1 };

                // walk a slow orbit so the arena, its walls and its props all get touched
                float t = f * dt;
                inp.wish = new Vector2(Mathf.Cos(t * 0.7f), Mathf.Sin(t * 0.9f));

                // aim at the nearest zombie, which is what a player does and what
                // actually exercises hits, kills, drops and the wave advance
                Zombie target = Nearest(app);
                if (target != null)
                {
                    Vector2 screen = target.pos - app.CameraPos;
                    inp.mouse = new Vector2(MathUtil.Clamp(screen.x, 0f, S.WIDTH),
                                            MathUtil.Clamp(screen.y, 0f, S.HEIGHT));
                }
                else
                {
                    inp.mouse = new Vector2(S.WIDTH * 0.5f + Mathf.Cos(t * 1.7f) * 560f,
                                            S.HEIGHT * 0.5f + Mathf.Sin(t * 1.3f) * 300f);
                }

                inp.fireHeld = true;
                inp.firePressed = (f % 7) == 0;
                inp.dash = (f % 120) == 30;
                inp.reload = (f % 180) == 60;
                inp.toggleTorch = (f % 900) == 450;
                inp.nextWeapon = (f % 240) == 15;
                if ((f % 300) == 200) inp.selectSlot = rng.RangeInt(0, 3);

                app.SimulateFrame(dt, inp);

                if (app.zombies.Count > res.maxZombies) res.maxZombies = app.zombies.Count;
                for (int i = 0; i < app.zombies.Count; i++)
                {
                    Zombie z = app.zombies[i];
                    res.typesSeen.Add(z.spec.key);
                    if (z.spec.key == "boss") res.bosses = Mathf.Max(res.bosses, 1);
                    Check(res, z.pos, "zombie " + z.spec.key);
                }
                res.pickupsSeen = Mathf.Max(res.pickupsSeen, app.pickups.Count);
                res.bulletsPeak = Mathf.Max(res.bulletsPeak, app.bullets.Count);

                Check(res, app.player.pos, "player");
                if (app.player.hp < 0f) res.problems.Add("player hp went negative");
            }

            res.waves = app.wave;
            res.kills = app.player.kills;
            res.shots = app.player.shots;
            res.hits = app.player.hits;
            res.cases = app.inventory.CaseCount();
            res.scrap = app.inventory.scrap;

            Object.Destroy(go);
            return res;
        }

        static Zombie Nearest(GameApp app)
        {
            Zombie best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < app.zombies.Count; i++)
            {
                Zombie z = app.zombies[i];
                if (!z.alive) continue;
                float d = (z.pos - app.player.pos).sqrMagnitude;
                if (d < bestD) { bestD = d; best = z; }
            }
            return best;
        }

        static void Check(Result res, Vector2 p, string who)
        {
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsInfinity(p.x) || float.IsInfinity(p.y))
            {
                if (res.problems.Count < 8) res.problems.Add(who + " position is not finite");
                return;
            }
            if (p.x < -4f || p.x > S.WORLD_W + 4f || p.y < -4f || p.y > S.WORLD_H + 4f)
                if (res.problems.Count < 8) res.problems.Add(who + " left the arena at " + p);
        }

        /// <summary>Every mode on every map, plus the inventory and save paths.</summary>
        public static string RunAll(int framesPerRun = 900)
        {
            var sb = new StringBuilder();
            int failures = 0;

            sb.AppendLine("NIGHTFALL self-test");
            sb.AppendLine("mode        map     waves kills shots  hits maxZ bul types                  status");

            for (int m = 0; m < Modes.All.Count; m++)
            {
                MapDef map = Maps.All[m % Maps.All.Count];
                Result r = Run(Modes.All[m].key, map.key, framesPerRun, 7 + m);

                var types = new List<string>(r.typesSeen);
                types.Sort();

                sb.AppendLine(string.Format("{0,-11} {1,-7} {2,5} {3,5} {4,5} {5,5} {6,4} {7,3} {8,-22} {9}",
                    r.mode, r.map, r.waves, r.kills, r.shots, r.hits, r.maxZombies,
                    r.bulletsPeak, string.Join(",", types.ToArray()), r.Ok ? "ok" : "FAIL"));

                if (!r.Ok)
                {
                    failures++;
                    for (int i = 0; i < r.problems.Count; i++)
                        sb.AppendLine("    ! " + r.problems[i]);
                }
            }

            sb.AppendLine();
            sb.AppendLine(CheckArenas());
            sb.AppendLine(CheckInventory());
            sb.AppendLine(failures == 0 ? "ALL OK" : failures + " RUN(S) FAILED");
            return sb.ToString();
        }

        /// <summary>Every map builds, has spawn points, and leaves the centre walkable.</summary>
        static string CheckArenas()
        {
            var sb = new StringBuilder("arenas: ");
            for (int i = 0; i < Maps.All.Count; i++)
            {
                MapDef m = Maps.All[i];
                var arena = new Arena(1234 + i, m);
                var rng = new Rng(1234 + i);
                Vector2 start = arena.center;
                if (arena.Blocked(start, S.P_RADIUS + 6f))
                    start = arena.FreePoint(rng, arena.center, 0f, 460f, S.P_RADIUS + 10f);

                bool ok = arena.obstacles.Count > 4
                          && arena.spawnPoints.Count > 20
                          && !arena.Blocked(start, S.P_RADIUS + 2f)
                          && arena.floor.rects.Count + arena.floor.circles.Count > 0;
                sb.Append(m.key).Append(ok ? " ok  " : " FAIL  ");
                if (!ok)
                    sb.Append("[props ").Append(arena.obstacles.Count)
                      .Append(", spawns ").Append(arena.spawnPoints.Count)
                      .Append(", start ").Append(start).Append("] ");
            }
            return sb.ToString();
        }

        /// <summary>Cases roll, duplicates refund, and the save round-trips.</summary>
        static string CheckInventory()
        {
            var inv = new Inventory();
            var rng = new Rng(99);
            inv.scrap = 100000;

            int opened = 0, dupes = 0;
            for (int i = 0; i < 400; i++)
            {
                Case c = Skins.Cases[i % Skins.Cases.Count];
                if (!inv.BuyCase(c)) continue;
                Inventory.OpenResult r = inv.OpenCase(c, rng);
                if (!r.ok) return "inventory: FAIL (case bought but would not open)";
                opened++;
                if (r.duplicate) dupes++;
            }

            if (inv.CaseCount() != 0) return "inventory: FAIL (cases left after opening)";

            var save = new SaveData();
            save.FromInventory(inv);
            Inventory back = save.ToInventory();
            if (back.owned.Count != inv.owned.Count || back.scrap != inv.scrap)
                return "inventory: FAIL (save round-trip lost data)";

            return string.Format("inventory: ok  opened {0}, duplicates {1}, unlocked {2}/{3}, scrap {4}",
                                 opened, dupes, inv.owned.Count, Skins.All.Count, inv.scrap);
        }
    }
}
