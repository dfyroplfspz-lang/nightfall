// C# / Assets/Scripts/Gameplay/Zombie.cs / Unity 2022.3+ / zombie types, steering, attacks
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    public class ZombieSpec
    {
        public string key;
        public string name;
        public float hp;
        public float speed;
        public float damage;
        public float radius;
        public int cost;              // wave budget cost
        public int score;
        public float attackCd = 0.85f;
        public Color body = new Color(104f / 255f, 136f / 255f, 86f / 255f, 1f);
        public Color dark = new Color(62f / 255f, 84f / 255f, 54f / 255f, 1f);
        public float knockResist = 1f;
        public bool ranged = false;
        public int minWave = 1;
        public bool elite = false;
    }

    public static class ZombieTypes
    {
        public static readonly Dictionary<string, ZombieSpec> All =
            new Dictionary<string, ZombieSpec>
        {
            { "walker", new ZombieSpec {
                key = "walker", name = "Walker", hp = 58f, speed = 84f, damage = 9f,
                radius = 15f, cost = 1, score = 10 } },

            { "runner", new ZombieSpec {
                key = "runner", name = "Runner", hp = 36f, speed = 178f, damage = 7f,
                radius = 13f, cost = 2, score = 18, attackCd = 0.6f,
                body = S.Col(142, 150, 92), dark = S.Col(84, 92, 56),
                knockResist = 0.7f, minWave = 2 } },

            { "spitter", new ZombieSpec {
                key = "spitter", name = "Spitter", hp = 70f, speed = 78f, damage = 12f,
                radius = 16f, cost = 4, score = 30, attackCd = 2.1f,
                body = S.Col(132, 174, 96), dark = S.Col(70, 104, 58),
                ranged = true, minWave = 4 } },

            { "brute", new ZombieSpec {
                key = "brute", name = "Brute", hp = 380f, speed = 62f, damage = 28f,
                radius = 27f, cost = 6, score = 70, attackCd = 1.25f,
                body = S.Col(118, 92, 78), dark = S.Col(74, 56, 46),
                knockResist = 0.25f, minWave = 5, elite = true } },

            { "boss", new ZombieSpec {
                key = "boss", name = "ABOMINATION", hp = 2200f, speed = 66f, damage = 38f,
                radius = 44f, cost = 0, score = 600, attackCd = 1.1f,
                body = S.Col(150, 72, 68), dark = S.Col(84, 34, 32),
                knockResist = 0.08f, minWave = 5, elite = true } }
        };

        public static ZombieSpec Get(string key)
        {
            ZombieSpec s;
            return All.TryGetValue(key, out s) ? s : All["walker"];
        }

        public static int WaveBudget(int wave, float mult = 1f)
        {
            return Mathf.Max(2, (int)((6f + wave * 3.4f + Mathf.Pow(wave, 1.55f) * 0.55f) * mult));
        }

        public static void WaveScales(int wave, out float hp, out float speed, out float dmg)
        {
            hp = 1f + (wave - 1) * 0.13f;
            speed = Mathf.Min(1f + (wave - 1) * 0.022f, 1.55f);
            dmg = Mathf.Min(1f + (wave - 1) * 0.055f, 2.4f);
        }

        static readonly string[] PoolOrder = { "walker", "runner", "spitter", "brute" };
        static readonly float[] PoolWeights = { 10f, 6f, 3.2f, 2.2f };

        /// <summary>Spec keys to spawn during this wave, shuffled.</summary>
        public static List<string> BuildWave(int wave, Rng rng, float budgetMult = 1f,
                                             int bossEvery = 5)
        {
            int budget = WaveBudget(wave, budgetMult);
            var outList = new List<string>();
            var avail = new List<string>();
            var weights = new List<float>();
            int guard = 0;

            while (budget > 0 && guard < 400)
            {
                guard++;
                avail.Clear();
                weights.Clear();
                for (int i = 0; i < PoolOrder.Length; i++)
                {
                    ZombieSpec s = Get(PoolOrder[i]);
                    if (s.minWave <= wave && s.cost <= budget)
                    {
                        avail.Add(PoolOrder[i]);
                        weights.Add(PoolWeights[i]);
                    }
                }
                if (avail.Count == 0) break;

                string pick = avail[rng.PickWeighted(weights)];
                outList.Add(pick);
                budget -= Get(pick).cost;
            }

            if (bossEvery > 0 && wave % bossEvery == 0) outList.Add("boss");
            rng.Shuffle(outList);
            return outList;
        }
    }

    public class Zombie
    {
        public ZombieSpec spec;
        public Vector2 pos;
        public Vector2 vel;
        public Vector2 knock;
        public float radius;
        public float maxHp;
        public float hp;
        public float speed;
        public float damage;
        public bool alive = true;

        float _attackTimer;
        float _flash;
        float _phase;
        Vector2 _facing = Vector2.right;
        int _avoidDir;
        float _avoidTimer;
        float _growlTimer;
        float _spawnAnim = 0.35f;
        float _bossSpawnTimer = 6f;

        public Zombie(ZombieSpec s, Vector2 p, Rng rng, float hpScale = 1f,
                      float speedScale = 1f, float dmgScale = 1f)
        {
            spec = s;
            pos = p;
            radius = s.radius;
            maxHp = s.hp * hpScale;
            hp = maxHp;
            speed = s.speed * speedScale * rng.Range(0.92f, 1.08f);
            damage = s.damage * dmgScale;
            _attackTimer = rng.Range(0f, 0.5f);
            _phase = rng.Range(0f, MathUtil.TAU);
            _growlTimer = rng.Range(1f, 7f);
        }

        // ----------------------------------------------------------- combat
        public bool Hurt(float amount, Vector2? direction = null, float knockback = 0f)
        {
            hp -= amount;
            _flash = 0.11f;
            if (direction.HasValue && knockback > 0f)
                knock += direction.Value * knockback * spec.knockResist;
            if (hp <= 0f)
            {
                alive = false;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------ brain
        Vector2 Steer(Vector2 target, Arena world, float dt, out float dist)
        {
            Vector2 toT = target - pos;
            dist = toT.magnitude;
            Vector2 d = MathUtil.Norm(toT);

            float look = radius + 34f;
            _avoidTimer -= dt;
            Vector2 probe = pos + d * look;
            if (world.Blocked(probe, radius * 0.9f))
            {
                if (_avoidTimer <= 0f)
                {
                    bool lf = !world.Blocked(pos + MathUtil.Rotate(d, 50f) * look, radius * 0.9f);
                    bool rf = !world.Blocked(pos + MathUtil.Rotate(d, -50f) * look, radius * 0.9f);
                    if (lf && !rf) _avoidDir = 1;
                    else if (rf && !lf) _avoidDir = -1;
                    else _avoidDir = world.rng.Sign();
                    _avoidTimer = 0.5f;
                }
                d = MathUtil.Rotate(d, 62f * _avoidDir);
            }
            return d;
        }

        public bool Update(float dt, Player player, Arena world, Grid grid, GameApp game)
        {
            _flash = Mathf.Max(0f, _flash - dt);
            _spawnAnim = Mathf.Max(0f, _spawnAnim - dt);
            _attackTimer = Mathf.Max(0f, _attackTimer - dt);
            _phase += dt * (6f + speed * 0.02f);

            float dist;
            Vector2 d = Steer(player.pos, world, dt, out dist);
            if (d.sqrMagnitude > 0f) _facing = d;

            Vector2 move = d * speed;

            // separation so the horde does not collapse into one dot
            var near = grid.Query(pos, radius + 34f);
            for (int i = 0; i < near.Count; i++)
            {
                Zombie other = near[i];
                if (ReferenceEquals(other, this) || !other.alive) continue;
                Vector2 diff = pos - other.pos;
                float dd = diff.magnitude;
                float reach = radius + other.radius;
                if (dd > 0.0001f && dd < reach)
                    move += MathUtil.Norm(diff) * (reach - dd) * 9f;
            }

            knock *= Mathf.Max(0f, 1f - 7f * dt);
            vel = move + knock;
            pos += vel * dt;
            world.Collide(ref pos, radius);

            float touch = radius + player.radius + 3f;
            if (spec.ranged)
            {
                if (dist < 560f && _attackTimer <= 0f && world.HasLos(pos, player.pos))
                {
                    _attackTimer = spec.attackCd;
                    Vector2 aim = MathUtil.Norm(player.pos - pos);
                    game.enemyBullets.Add(new Bullet(pos + aim * radius, aim, 430f, damage,
                                                     0, false, S.Col(150, 220, 110), 14f, 5f, 2.4f));
                    game.audio.Play("spit", 0.5f);
                }
                if (dist < 220f) pos -= d * speed * dt * 0.8f;   // keeps its distance
            }
            else if (dist < touch && _attackTimer <= 0f)
            {
                _attackTimer = spec.attackCd;
                if (player.TakeDamage(damage)) game.OnPlayerHurt(damage);
            }

            if (spec.key == "boss")
            {
                _bossSpawnTimer -= dt;
                if (_bossSpawnTimer <= 0f)
                {
                    _bossSpawnTimer = 7f;
                    game.BossSummon(this);
                }
            }

            _growlTimer -= dt;
            if (_growlTimer <= 0f)
            {
                _growlTimer = world.rng.Range(4f, 11f);
                if (dist < 760f)
                    game.audio.Play("growl", MathUtil.Clamp(1f - dist / 900f, 0.12f, 0.5f));
            }

            return alive;
        }

        // ------------------------------------------------------------- draw
        public void Draw(Vector2 cam)
        {
            float x = pos.x - cam.x;
            float y = pos.y - cam.y;
            float r = radius;
            if (_spawnAnim > 0f) r = Mathf.Max(3f, r * (1f - _spawnAnim / 0.5f));

            Color body = _flash > 0f ? S.Col(250, 240, 240) : spec.body;

            // shadow
            Painter.Circle(x - 1f, y + 2f, r + 3f, new Color(0f, 0f, 0f, 0.35f));

            Vector2 facing = _facing;
            Vector2 side = MathUtil.Rotate(facing, 90f);
            float swing = Mathf.Sin(_phase) * (r * 0.55f);

            // shambling legs
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 b = new Vector2(x, y) + side * (s * r * 0.45f);
                Vector2 tip = b + facing * (swing * s);
                Painter.Line(b, tip, Mathf.Max(3f, r * 0.34f), spec.dark);
            }

            // outstretched arms
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 sh = new Vector2(x, y) + side * (s * r * 0.75f);
                Vector2 hand = sh + facing * (r * 1.25f) + side * (s * Mathf.Sin(_phase + s) * 3f);
                Painter.Line(sh, hand, Mathf.Max(3f, r * 0.30f), spec.dark);
                Painter.Circle(hand.x, hand.y, Mathf.Max(2f, r * 0.22f), body);
            }

            Painter.Circle(x, y, r + 2f, spec.dark);
            Painter.Circle(x, y, r, body);

            // top-left highlight and bottom shading give the blob some volume
            if (_flash <= 0f)
            {
                Color hi = S.Mul(body, 1.22f);
                Color lo = S.Mul(body, 0.72f);
                Painter.Circle(x, y + r * 0.28f, r * 0.78f, lo);
                Painter.Circle(x, y, r * 0.92f, body);
                Painter.Circle(x - r * 0.3f, y - r * 0.34f, Mathf.Max(2f, r * 0.34f), hi);
            }
            Painter.CircleOutline(x, y, r, 2f, S.Mul(spec.dark, 0.45f));

            // wounds appear as the thing takes damage
            if (maxHp > 0f && hp < maxHp * 0.6f)
            {
                int n = hp < maxHp * 0.3f ? 3 : 1;
                float[] offs = { -0.35f, 0.2f, 0.3f, -0.25f, 0.1f, 0.4f };
                for (int i = 0; i < n; i++)
                    Painter.Circle(x + offs[i * 2] * r, y + offs[i * 2 + 1] * r,
                                   Mathf.Max(2f, r * 0.17f), S.C_BLOOD);
            }

            Vector2 head = new Vector2(x, y) + facing * (r * 0.42f);
            float hr = Mathf.Max(3f, r * 0.48f);
            Painter.Circle(head.x, head.y, hr,
                           _flash <= 0f ? S.Col(164, 178, 132) : S.Col(255, 250, 250));
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 e = head + facing * hr * 0.5f + side * (s * hr * 0.42f);
                Painter.Circle(e.x, e.y, Mathf.Max(1f, hr * 0.26f), S.Col(210, 46, 40));
            }

            if (spec.elite || hp < maxHp * 0.999f) DrawHpBar(x, y, r);
        }

        void DrawHpBar(float x, float y, float r)
        {
            float w = Mathf.Max(26f, r * 2.4f);
            float h = spec.elite ? 7f : 5f;
            float bx = x - w * 0.5f;
            float by = y - r - 13f;
            float frac = MathUtil.Clamp(hp / maxHp, 0f, 1f);
            Painter.Rect(bx - 1f, by - 1f, w + 2f, h + 2f, S.Col(18, 18, 20));
            Color col = frac > 0.5f ? (Color)S.C_GREEN : (frac > 0.25f ? (Color)S.C_YELLOW : (Color)S.C_RED);
            Painter.Rect(bx, by, w * frac, h, col);
        }
    }
}
