// C# / Assets/Scripts/Gameplay/Player.cs / Unity 2022.3+ / the survivor: movement, aim, shooting
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    public class Player
    {
        public Vector2 pos;
        public Vector2 vel;
        public float radius = S.P_RADIUS;
        public float hp = S.P_MAX_HP;
        public float maxHp = S.P_MAX_HP;
        public Vector2 aim = Vector2.right;
        public bool alive = true;

        public readonly Dictionary<string, Weapon> weapons = new Dictionary<string, Weapon>();
        public string current = "pistol";
        public Skin skin = Skins.Get(Skins.DefaultPlayer);
        public Skin weaponSkin = Skins.Get(Skins.DefaultWeapon);
        public float gunScale = 1f;
        public bool flashlightOn = true;

        public float dashTime;
        public float dashCd;
        public Vector2 dashDir = Vector2.right;
        public float iframe;

        public float walkPhase;
        public float recoil;
        public float speedBoost;     // seconds of adrenaline left
        public float damageBoost;

        public int shots;
        public int hits;
        public int kills;

        public Player(Vector2 start)
        {
            pos = start;
            weapons["pistol"] = new Weapon(Weapons.Get("pistol"));
        }

        public Weapon weapon { get { return weapons[current]; } }
        public bool Dashing { get { return dashTime > 0f; } }

        public bool Owns(string key) { return weapons.ContainsKey(key); }

        /// <summary>True when it is a brand new gun, false when it just topped up ammo.</summary>
        public bool GiveWeapon(string key)
        {
            Weapon w;
            if (weapons.TryGetValue(key, out w))
            {
                w.AddAmmo(w.spec.mag * 2);
                return false;
            }
            weapons[key] = new Weapon(Weapons.Get(key));
            current = key;
            return true;
        }

        public void NextWeapon(int step = 1)
        {
            var owned = new List<string>();
            for (int i = 0; i < Weapons.Order.Length; i++)
                if (weapons.ContainsKey(Weapons.Order[i])) owned.Add(Weapons.Order[i]);
            if (owned.Count < 2) return;

            int idx = owned.IndexOf(current);
            if (idx < 0) idx = 0;
            idx = ((idx + step) % owned.Count + owned.Count) % owned.Count;
            current = owned[idx];
        }

        public void Select(int index)
        {
            if (index < 0 || index >= Weapons.Order.Length) return;
            string key = Weapons.Order[index];
            if (weapons.ContainsKey(key)) current = key;
        }

        public float Heal(float amount)
        {
            float before = hp;
            hp = Mathf.Min(maxHp, hp + amount);
            return hp - before;
        }

        public bool TakeDamage(float amount)
        {
            if (iframe > 0f || Dashing || !alive) return false;
            hp -= amount;
            iframe = S.P_HURT_IFRAME;
            if (hp <= 0f) { hp = 0f; alive = false; }
            return true;
        }

        // ------------------------------------------------------------ input
        public void HandleMove(Vector2 wish, float dt, Arena world)
        {
            wish = MathUtil.Norm(wish);
            float speed = S.P_SPEED * (speedBoost > 0f ? 1.35f : 1f);
            Vector2 target = Dashing ? dashDir * speed * S.P_DASH_SPEED : wish * speed;

            float accel = Dashing ? 30f : 14f;      // snappy but not instant
            vel += (target - vel) * Mathf.Min(1f, accel * dt);
            pos += vel * dt;
            world.Collide(ref pos, radius);

            walkPhase += dt * (wish.sqrMagnitude > 0.01f && !Dashing ? 11f : 2f);
        }

        public bool TryDash(Vector2 wish)
        {
            if (dashCd > 0f || Dashing) return false;
            dashDir = wish.sqrMagnitude > 0f ? MathUtil.Norm(wish) : aim;
            dashTime = S.P_DASH_TIME;
            dashCd = S.P_DASH_CD;
            iframe = Mathf.Max(iframe, S.P_DASH_TIME + 0.08f);
            return true;
        }

        public void AimAt(Vector2 worldMouse)
        {
            Vector2 d = worldMouse - pos;
            if (d.sqrMagnitude > 1f) aim = MathUtil.Norm(d);
        }

        public Vector2 MuzzlePos()
        {
            float reach = radius * 0.35f + GunModels.MuzzleOffset(current) * gunScale;
            return pos + aim * reach - MathUtil.Rotate(aim, 90f) * 1.5f;
        }

        /// <summary>Returns the bullets produced this frame, possibly none.</summary>
        public List<Bullet> Shoot(bool pressedNow, bool held, Rng rng)
        {
            Weapon w = weapon;
            if (!w.spec.auto && !pressedNow) return null;
            if (w.spec.auto && !held) return null;
            if (!w.CanFire()) return null;

            var bullets = w.Fire(MuzzlePos(), aim, rng);
            shots += bullets.Count;
            recoil = Mathf.Min(1f, recoil + 0.55f);
            return bullets;
        }

        // ----------------------------------------------------------- update
        public void Update(float dt)
        {
            dashTime = Mathf.Max(0f, dashTime - dt);
            dashCd = Mathf.Max(0f, dashCd - dt);
            iframe = Mathf.Max(0f, iframe - dt);
            recoil = Mathf.Max(0f, recoil - dt * 4.2f);
            speedBoost = Mathf.Max(0f, speedBoost - dt);
            damageBoost = Mathf.Max(0f, damageBoost - dt);
            foreach (var kv in weapons) kv.Value.Update(dt);
        }

        // ------------------------------------------------------------- draw
        public void Draw(Vector2 cam)
        {
            float x = pos.x - cam.x;
            float y = pos.y - cam.y;
            float ang = MathUtil.AngleOf(aim);
            float r = radius;
            Skin sk = skin;
            Vector2 side = MathUtil.Rotate(aim, 90f);

            Painter.Circle(x - 1f, y + 2f, r + 4f, new Color(0f, 0f, 0f, 0.4f));

            // legs
            float swing = Mathf.Sin(walkPhase) * 8f;
            Color legCol = S.Mul(sk.trim, 0.9f);
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 b = new Vector2(x, y) + side * (s * 7f);
                Vector2 tip = b + aim * (swing * s);
                Painter.Line(b, tip, 6f, legCol);
            }

            Color bodyCol = sk.body;
            if (iframe > 0f && !Dashing && ((int)(iframe * 30f)) % 2 == 0)
                bodyCol = S.Col(255, 170, 170);
            if (Dashing)
                bodyCol = S.Mix(sk.body, S.Col(170, 225, 255), 0.65f);

            Painter.Circle(x, y, r + 2f, S.Mul(sk.trim, 0.55f));
            Painter.Circle(x, y, r, bodyCol);

            // skin pattern on the torso
            if (sk.pattern == "stripe")
            {
                Painter.Line(new Vector2(x, y) - side * (r - 2f),
                             new Vector2(x, y) + side * (r - 2f), 4f, sk.trim);
            }
            else if (sk.pattern == "camo")
            {
                Painter.Circle(x - 5f, y - 4f, 5f, S.Mul(sk.trim, 1.1f));
                Painter.Circle(x + 4f, y + 3f, 6f, S.Mul(sk.trim, 1.1f));
                Painter.Circle(x + 2f, y - 6f, 4f, S.Mul(sk.trim, 1.1f));
            }
            else if (sk.pattern == "splatter")
            {
                Painter.Circle(x - 6f, y + 2f, 3f, S.Mul(sk.trim, 0.7f));
                Painter.Circle(x + 5f, y - 5f, 2f, S.Mul(sk.trim, 0.7f));
                Painter.Circle(x + 1f, y + 6f, 3f, S.Mul(sk.trim, 0.7f));
                Painter.Circle(x + 7f, y + 2f, 2f, S.Mul(sk.trim, 0.7f));
            }
            else if (sk.pattern == "glow")
            {
                Painter.Flip(true);
                Painter.Circle(x, y, r * 2f, S.WithA(sk.accent, 70f / 255f));
                Painter.Flip(false);
            }
            Painter.CircleOutline(x, y, r, 2f, S.Mul(sk.trim, 0.4f));

            // shoulders
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 sp = new Vector2(x, y) + side * (s * (r - 3f)) + aim * 2f;
                Painter.Circle(sp.x, sp.y, 5f, S.Mul(bodyCol, 0.88f));
            }

            // weapon, held slightly off the centre line
            Vector2 hand = new Vector2(x, y) + aim * (r * 0.35f) - side * 1.5f;
            float kick = recoil * 4f;
            GunModels.Draw(new Vector2(hand.x - aim.x * kick, hand.y - aim.y * kick),
                           ang, current, weaponSkin, gunScale);

            // hands on the grip
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 hp = hand + aim * (6f + s * 3f) + side * (s * 3.5f);
                Painter.Circle(hp.x, hp.y, 3f, S.Mul(sk.accent, 0.92f));
            }

            // head
            Vector2 head = new Vector2(x, y) + aim * 3f;
            Painter.Circle(head.x, head.y, 7f, sk.accent);
            Painter.CircleOutline(head.x, head.y, 7f, 1f, S.Mul(sk.trim, 0.5f));
            Vector2 visor = head + aim * 4f;
            Painter.Circle(visor.x, visor.y, 3f, S.Mul(sk.trim, 0.75f));

            if (speedBoost > 0f)
                Painter.CircleOutline(x, y, r + 7f, 2f, S.C_CYAN);
        }
    }
}
