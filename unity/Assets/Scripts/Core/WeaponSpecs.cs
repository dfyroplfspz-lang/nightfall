// C# / Assets/Scripts/Core/WeaponSpecs.cs / Unity 2022.3+ / weapon stats and runtime state
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    public class WeaponSpec
    {
        public string key;
        public string name;
        public float damage;
        public float rpm;
        public int mag;
        public int reserveMax;
        public float reloadTime;
        public float spread;          // degrees of cone
        public int pellets = 1;
        public float speed = 1500f;
        public int pierce = 0;
        public bool auto = true;
        public float knockback = 110f;
        public float shake = 2f;
        public string sound = "pistol";
        public float bulletLen = 16f;
        public Color color = new Color(250f / 255f, 226f / 255f, 150f / 255f, 1f);
        public bool infinite = false;

        public float Interval { get { return 60f / rpm; } }

        /// <summary>First word of the display name, used in pickup messages.</summary>
        public string ShortName
        {
            get
            {
                int i = name.IndexOf(' ');
                return i < 0 ? name : name.Substring(0, i);
            }
        }
    }

    public static class Weapons
    {
        public static readonly string[] Order = { "pistol", "smg", "shotgun", "rifle" };

        public static readonly Dictionary<string, WeaponSpec> All =
            new Dictionary<string, WeaponSpec>
        {
            { "pistol", new WeaponSpec {
                key = "pistol", name = "M9 PISTOL", damage = 26f, rpm = 420f, mag = 14,
                reserveMax = 999, reloadTime = 1.05f, spread = 2.4f, auto = false,
                sound = "pistol", shake = 2f, infinite = true } },

            { "smg", new WeaponSpec {
                key = "smg", name = "MP5 SMG", damage = 15f, rpm = 820f, mag = 34,
                reserveMax = 320, reloadTime = 1.45f, spread = 5.2f,
                sound = "smg", shake = 1.6f, knockback = 70f, bulletLen = 14f } },

            { "shotgun", new WeaponSpec {
                key = "shotgun", name = "870 SHOTGUN", damage = 13f, rpm = 78f, mag = 7,
                reserveMax = 72, reloadTime = 2.1f, spread = 11f, pellets = 9,
                speed = 1250f, sound = "shotgun", shake = 7.5f, knockback = 320f,
                auto = false, bulletLen = 12f,
                color = new Color(1f, 196f / 255f, 120f / 255f, 1f) } },

            { "rifle", new WeaponSpec {
                key = "rifle", name = "AK RIFLE", damage = 42f, rpm = 300f, mag = 30,
                reserveMax = 240, reloadTime = 1.8f, spread = 3f, pierce = 2,
                speed = 1900f, sound = "rifle", shake = 3.4f, knockback = 150f,
                bulletLen = 22f,
                color = new Color(1f, 240f / 255f, 180f / 255f, 1f) } }
        };

        public static WeaponSpec Get(string key)
        {
            WeaponSpec s;
            return All.TryGetValue(key, out s) ? s : All["pistol"];
        }
    }

    /// <summary>One projectile. Keeps its previous position so hits are swept, not sampled.</summary>
    public class Bullet
    {
        public Vector2 pos;
        public Vector2 prev;
        public Vector2 vel;
        public float damage;
        public int pierce;
        public readonly HashSet<object> hits = new HashSet<object>();
        public bool alive = true;
        public Color color;
        public float length;
        public bool friendly;
        public float radius;
        public float life;

        public Bullet(Vector2 p, Vector2 dir, float speed, float dmg, int pierce = 0,
                      bool friendly = true, Color? col = null, float length = 16f,
                      float radius = 3f, float life = 1.6f)
        {
            pos = p;
            prev = p;
            vel = dir * speed;
            damage = dmg;
            this.pierce = pierce;
            this.friendly = friendly;
            color = col ?? new Color(250f / 255f, 226f / 255f, 150f / 255f, 1f);
            this.length = length;
            this.radius = radius;
            this.life = life;
        }

        /// <summary>Advances the bullet; returns the wall hit point when it stops on geometry.</summary>
        public Vector2? Update(float dt, Arena world)
        {
            prev = pos;
            pos += vel * dt;
            life -= dt;
            if (life <= 0f) { alive = false; return null; }

            Vector2? wall = world.RayBlocked(prev, pos);
            if (wall.HasValue) { alive = false; return wall; }
            return null;
        }

        public void Draw(Vector2 cam)
        {
            Vector2 d = MathUtil.Norm(vel);
            Vector2 tail = pos - d * length;
            Painter.Line(pos.x - cam.x, pos.y - cam.y, tail.x - cam.x, tail.y - cam.y, 3f, color);
            Painter.Circle(pos.x - cam.x, pos.y - cam.y, 2f, new Color(1f, 1f, 230f / 255f, 1f));
        }
    }

    /// <summary>Runtime state for one weapon the player owns.</summary>
    public class Weapon
    {
        public WeaponSpec spec;
        public int inMag;
        public int reserve;
        public float cooldown;
        public float reloading;

        public Weapon(WeaponSpec s, int? ammoInMag = null, int? reserveAmount = null)
        {
            spec = s;
            inMag = ammoInMag ?? s.mag;
            int r = reserveAmount ?? (s.infinite ? s.reserveMax : s.mag * 3);
            reserve = Mathf.Min(r, s.reserveMax);
        }

        public int Total { get { return inMag + reserve; } }

        public void Update(float dt)
        {
            cooldown = Mathf.Max(0f, cooldown - dt);
            if (reloading > 0f)
            {
                reloading -= dt;
                if (reloading <= 0f)
                {
                    int need = spec.mag - inMag;
                    int take = Mathf.Min(need, reserve);
                    inMag += take;
                    reserve -= take;
                    reloading = 0f;
                }
            }
        }

        public bool CanFire() { return cooldown <= 0f && reloading <= 0f && inMag > 0; }

        public bool StartReload()
        {
            if (reloading <= 0f && inMag < spec.mag && reserve > 0)
            {
                reloading = spec.reloadTime;
                return true;
            }
            return false;
        }

        public int AddAmmo(int amount)
        {
            int space = spec.reserveMax - reserve;
            int got = Mathf.Min(space, amount);
            reserve += got;
            return got;
        }

        /// <summary>Spends one round and returns the bullets it produced.</summary>
        public List<Bullet> Fire(Vector2 origin, Vector2 direction, Rng rng,
                                 float accuracyBonus = 0f)
        {
            inMag -= 1;
            cooldown = spec.Interval;
            float spread = spec.spread * Mathf.Max(0.25f, 1f - accuracyBonus);

            var outList = new List<Bullet>(spec.pellets);
            for (int i = 0; i < spec.pellets; i++)
            {
                float ang;
                if (spec.pellets > 1)
                {
                    // deterministic fan plus jitter keeps the buckshot pattern readable
                    ang = (i / (float)(spec.pellets - 1) - 0.5f) * spread * 2f + rng.Range(-2f, 2f);
                }
                else
                {
                    ang = rng.Range(-spread, spread);
                }
                Vector2 d = MathUtil.Rotate(direction, ang);
                outList.Add(new Bullet(origin, d, spec.speed * rng.Range(0.95f, 1.05f),
                                       spec.damage, spec.pierce, true, spec.color, spec.bulletLen));
            }
            return outList;
        }
    }
}
