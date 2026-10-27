// C# / Assets/Scripts/Gameplay/Pickup.cs / Unity 2022.3+ / floor drops the player walks over
using UnityEngine;

namespace Nightfall
{
    public class Pickup
    {
        public Vector2 pos;
        public string kind;            // medkit | ammo | adrenaline | weapon | case
        public string weaponKey;
        public string caseKey = "supply";
        public float radius = 16f;
        public float life;             // negative means it never expires
        public float bob;
        public bool alive = true;

        public Pickup(Vector2 p, string kind, string weaponKey = null, float life = 38f)
        {
            pos = p;
            this.kind = kind;
            this.weaponKey = weaponKey;
            this.life = life;
            bob = (p.x + p.y) * 0.013f;
        }

        public Color Color
        {
            get
            {
                switch (kind)
                {
                    case "medkit": return S.C_RED;
                    case "ammo": return S.C_YELLOW;
                    case "adrenaline": return S.C_CYAN;
                    case "weapon": return S.C_ORANGE;
                    default: return S.Col(150, 210, 235);
                }
            }
        }

        public bool Update(float dt)
        {
            bob += dt * 3f;
            if (life >= 0f)
            {
                life -= dt;
                if (life <= 0f) alive = false;
            }
            return alive;
        }

        /// <summary>Message for the floating text, or null when it cannot be used.</summary>
        public string Apply(Player player, GameApp game)
        {
            switch (kind)
            {
                case "medkit":
                {
                    if (player.hp >= player.maxHp) return null;
                    float got = player.Heal(35f);
                    return "+" + (int)got + " HP";
                }
                case "ammo":
                {
                    Weapon w = player.weapon;
                    if (w.spec.infinite)
                    {
                        // the pistol never needs ammo, so top up whatever is lowest
                        Weapon best = null;
                        foreach (var kv in player.weapons)
                        {
                            if (kv.Value.spec.infinite) continue;
                            if (best == null || kv.Value.reserve < best.reserve) best = kv.Value;
                        }
                        if (best != null) w = best;
                    }
                    int gained = w.AddAmmo(Mathf.Max(8, w.spec.mag * 2));
                    if (gained <= 0) return null;
                    return "+" + gained + " " + w.spec.ShortName;
                }
                case "adrenaline":
                    player.speedBoost = 9f;
                    player.damageBoost = 9f;
                    return "ADRENALINE";

                case "case":
                    game.inventory.AddCase(caseKey);
                    game.inventoryDirty = true;
                    return Skins.GetCase(caseKey).name;

                case "weapon":
                {
                    string key = weaponKey ?? Weapons.Order[1 + (int)(game.rng.NextUInt() % 3u)];
                    bool fresh = player.GiveWeapon(key);
                    WeaponSpec spec = Weapons.Get(key);
                    return fresh ? spec.name : "+AMMO " + spec.ShortName;
                }
            }
            return null;
        }

        public void Draw(Vector2 cam)
        {
            float off = Mathf.Sin(bob) * 3f;
            float x = pos.x - cam.x;
            float y = pos.y - cam.y + off;

            bool blink = life >= 0f && life < 6f && ((int)(life * 6f)) % 2 == 0;
            if (blink) return;

            Painter.Flip(true);
            Painter.RadialBlob(x, y, 34f, new Color(1f, 1f, 1f, 110f / 255f));
            Painter.Flip(false);

            Color col = Color;
            switch (kind)
            {
                case "medkit":
                    Painter.RoundRect(x - 11f, y - 9f, 22f, 18f, 3f, S.Col(238, 238, 238));
                    Painter.Rect(x - 2f, y - 6f, 4f, 12f, col);
                    Painter.Rect(x - 7f, y - 2f, 14f, 4f, col);
                    break;

                case "ammo":
                    Painter.RoundRect(x - 12f, y - 8f, 24f, 16f, 2f, S.Col(74, 62, 32));
                    Painter.RoundRectOutline(x - 12f, y - 8f, 24f, 16f, 2f, 2f, col);
                    for (int i = -5; i <= 5; i += 5)
                        Painter.Rect(x + i - 1f, y - 4f, 3f, 8f, col);
                    break;

                case "adrenaline":
                    Painter.RoundRect(x - 3f, y - 10f, 6f, 18f, 2f, S.Col(210, 230, 240));
                    Painter.RoundRect(x - 3f, y - 2f, 6f, 10f, 2f, col);
                    Painter.Line(x, y - 14f, x, y - 10f, 3f, S.Col(230, 240, 250));
                    break;

                case "case":
                    Painter.RoundRect(x - 16f, y - 12f, 32f, 24f, 4f, S.Col(54, 48, 42));
                    Painter.RoundRectOutline(x - 16f, y - 12f, 32f, 24f, 4f, 2f, col);
                    Painter.Line(x - 16f, y - 12f, x + 16f, y + 12f, 2f, col);
                    Painter.Line(x + 16f, y - 12f, x - 16f, y + 12f, 2f, col);
                    Painter.RoundRect(x - 5f, y - 4f, 10f, 8f, 2f, col);
                    break;

                default:   // weapon crate
                    Painter.RoundRect(x - 15f, y - 11f, 30f, 22f, 3f, S.Col(96, 70, 38));
                    Painter.RoundRectOutline(x - 15f, y - 11f, 30f, 22f, 3f, 2f, col);
                    Painter.Line(x - 9f, y + 3f, x + 9f, y + 3f, 3f, col);
                    Painter.Line(x + 4f, y - 3f, x + 9f, y + 3f, 3f, col);
                    break;
            }
        }

        /// <summary>Chance-based loot when a zombie dies. Null means nothing dropped.</summary>
        public static Pickup RollDrop(Vector2 pos, Rng rng, Player player, int wave)
        {
            float r = rng.Value;
            if (r < 0.055f) return new Pickup(pos, "medkit");
            if (r < 0.17f) return new Pickup(pos, "ammo");
            if (r < 0.195f) return new Pickup(pos, "adrenaline");
            if (r < 0.215f && wave >= 2)
            {
                var missing = new System.Collections.Generic.List<string>();
                for (int i = 1; i < Weapons.Order.Length; i++)
                    if (!player.Owns(Weapons.Order[i])) missing.Add(Weapons.Order[i]);

                string key;
                if (missing.Count > 0) key = rng.Pick(missing);
                else key = Weapons.Order[1 + (int)(rng.NextUInt() % 3u)];
                return new Pickup(pos, "weapon", key);
            }
            return null;
        }
    }
}
