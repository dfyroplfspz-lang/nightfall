// C# / Assets/Scripts/Core/Skins.cs / Unity 2022.3+ / cosmetics, rarities, cases, inventory
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    public enum Rarity { Common, Rare, Epic, Legend }

    public static class Rarities
    {
        public static readonly Rarity[] Order =
            { Rarity.Common, Rarity.Rare, Rarity.Epic, Rarity.Legend };

        public static Color Col(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return S.Col(88, 150, 220);
                case Rarity.Epic: return S.Col(168, 96, 226);
                case Rarity.Legend: return S.Col(234, 170, 56);
                default: return S.Col(150, 156, 166);
            }
        }

        public static string Name(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return "RARE";
                case Rarity.Epic: return "EPIC";
                case Rarity.Legend: return "LEGENDARY";
                default: return "COMMON";
            }
        }

        public static int Scrap(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return 20;
                case Rarity.Epic: return 55;
                case Rarity.Legend: return 150;
                default: return 8;
            }
        }
    }

    public class Skin
    {
        public string key;
        public string name;
        public bool isPlayer;        // false means it is a weapon finish
        public Rarity rarity;
        public Color body;
        public Color trim;
        public Color accent;
        public string pattern = "plain";   // plain | stripe | camo | hex | glow | splatter
    }

    public class Case
    {
        public string key;
        public string name;
        public string tagline;
        public Color color;
        public float[] weights;      // indexed by Rarities.Order
        public int price;            // scrap
    }

    public static class Skins
    {
        static Skin P(string key, string name, Rarity r, Color body, Color trim,
                      Color accent, string pattern = "plain")
        {
            return new Skin { key = key, name = name, isPlayer = true, rarity = r,
                              body = body, trim = trim, accent = accent, pattern = pattern };
        }

        static Skin W(string key, string name, Rarity r, Color body, Color trim,
                      Color accent, string pattern = "plain")
        {
            return new Skin { key = key, name = name, isPlayer = false, rarity = r,
                              body = body, trim = trim, accent = accent, pattern = pattern };
        }

        public static readonly List<Skin> PlayerSkins = new List<Skin>
        {
            P("survivor",  "SURVIVOR",    Rarity.Common, S.Col(198,204,214), S.Col(108,116,130), S.Col(226,198,170)),
            P("ranger",    "RANGER",      Rarity.Common, S.Col(138,146,112), S.Col( 84, 92, 66), S.Col(214,190,150), "stripe"),
            P("medic",     "FIELD MEDIC", Rarity.Common, S.Col(226,228,230), S.Col(198, 60, 56), S.Col(226,198,170)),
            P("engineer",  "ENGINEER",    Rarity.Common, S.Col(196,150, 70), S.Col(104, 78, 36), S.Col(226,198,170), "stripe"),
            P("swat",      "SWAT",        Rarity.Rare,   S.Col( 72, 80, 96), S.Col( 34, 38, 48), S.Col(120,200,255)),
            P("hazmat",    "HAZMAT",      Rarity.Rare,   S.Col(228,206, 84), S.Col(120,108, 40), S.Col(150,230,190), "stripe"),
            P("urbancamo", "URBAN CAMO",  Rarity.Rare,   S.Col(132,136,142), S.Col( 70, 74, 80), S.Col(210,200,180), "camo"),
            P("cultist",   "CULTIST",     Rarity.Epic,   S.Col(122, 36, 42), S.Col( 60, 18, 22), S.Col(240,120, 90), "splatter"),
            P("ghost",     "GHOST",       Rarity.Epic,   S.Col(206,226,236), S.Col(120,150,168), S.Col(120,240,255), "glow"),
            P("neon",      "NEON RUNNER", Rarity.Epic,   S.Col( 40, 44, 60), S.Col(230, 60,180), S.Col( 90,230,255), "glow"),
            P("gold",      "GOLD PLATED", Rarity.Legend, S.Col(226,178, 62), S.Col(140,102, 24), S.Col(255,232,160), "glow"),
            P("ember",     "EMBER",       Rarity.Legend, S.Col( 52, 40, 38), S.Col(226, 96, 34), S.Col(255,180, 90), "glow")
        };

        public static readonly List<Skin> WeaponSkins = new List<Skin>
        {
            W("steel",   "STEEL",   Rarity.Common, S.Col( 92, 96,104), S.Col( 54, 56, 62), S.Col(250,226,150)),
            W("desert",  "DESERT",  Rarity.Common, S.Col(176,150,104), S.Col(104, 88, 60), S.Col(250,226,150), "stripe"),
            W("forest",  "FOREST",  Rarity.Common, S.Col( 82,104, 70), S.Col( 46, 58, 40), S.Col(230,226,160), "camo"),
            W("urban",   "URBAN",   Rarity.Common, S.Col(122,126,132), S.Col( 62, 64, 70), S.Col(250,226,150), "camo"),
            W("carbon",  "CARBON",  Rarity.Rare,   S.Col( 48, 50, 56), S.Col( 26, 28, 32), S.Col(190,220,255), "hex"),
            W("crimson", "CRIMSON", Rarity.Rare,   S.Col(138, 42, 40), S.Col( 66, 20, 20), S.Col(255,150,110), "stripe"),
            W("ice",     "ICE",     Rarity.Rare,   S.Col(184,212,230), S.Col( 96,134,160), S.Col(170,240,255), "hex"),
            W("venom",   "VENOM",   Rarity.Epic,   S.Col( 62, 70, 54), S.Col(126,220, 90), S.Col(160,255,120), "splatter"),
            W("royal",   "ROYAL",   Rarity.Epic,   S.Col( 78, 52,132), S.Col( 40, 26, 72), S.Col(220,190,255), "stripe"),
            W("magma",   "MAGMA",   Rarity.Epic,   S.Col( 62, 44, 40), S.Col(226,100, 36), S.Col(255,190, 90), "glow"),
            W("goldgun", "GOLD",    Rarity.Legend, S.Col(226,178, 62), S.Col(140,102, 24), S.Col(255,240,180), "glow"),
            W("spectre", "SPECTRE", Rarity.Legend, S.Col( 26, 30, 38), S.Col(120,240,255), S.Col(140,250,255), "glow")
        };

        public const string DefaultPlayer = "survivor";
        public const string DefaultWeapon = "steel";

        public static readonly List<Skin> All = BuildAll();
        static readonly Dictionary<string, Skin> ByKey = BuildIndex();

        static List<Skin> BuildAll()
        {
            var list = new List<Skin>();
            list.AddRange(PlayerSkins);
            list.AddRange(WeaponSkins);
            return list;
        }

        static Dictionary<string, Skin> BuildIndex()
        {
            var d = new Dictionary<string, Skin>();
            foreach (var s in All) d[s.key] = s;
            return d;
        }

        public static Skin Get(string key)
        {
            Skin s;
            if (key != null && ByKey.TryGetValue(key, out s)) return s;
            return ByKey[DefaultPlayer];
        }

        public static readonly List<Case> Cases = new List<Case>
        {
            new Case { key = "supply", name = "SUPPLY CASE",
                       tagline = "whatever was left in the crate",
                       color = S.Col(150,156,166),
                       weights = new[] { 66f, 25f, 7.5f, 1.5f }, price = 100 },
            new Case { key = "military", name = "MILITARY CASE",
                       tagline = "someone's ready bag",
                       color = S.Col(88,150,220),
                       weights = new[] { 38f, 42f, 16f, 4f }, price = 220 },
            new Case { key = "vault", name = "VAULT CASE",
                       tagline = "sealed, heavy, worth it",
                       color = S.Col(234,170,56),
                       weights = new[] { 10f, 40f, 36f, 14f }, price = 450 }
        };

        public static Case GetCase(string key)
        {
            foreach (var c in Cases) if (c.key == key) return c;
            return Cases[0];
        }

        /// <summary>Deeper waves bias floor drops toward better crates.</summary>
        public static string DropCaseKey(Rng rng, int wave)
        {
            float r = rng.Value + Mathf.Min(0.35f, wave * 0.012f);
            if (r > 1.12f) return "vault";
            if (r > 0.82f) return "military";
            return "supply";
        }
    }

    /// <summary>Owned skins, unopened cases and scrap. Serialises into the save file.</summary>
    public class Inventory
    {
        public HashSet<string> owned = new HashSet<string>();
        public Dictionary<string, int> cases = new Dictionary<string, int>();
        public int scrap;
        public string equippedPlayer = Skins.DefaultPlayer;
        public string equippedWeapon = Skins.DefaultWeapon;

        public Inventory()
        {
            owned.Add(Skins.DefaultPlayer);
            owned.Add(Skins.DefaultWeapon);
        }

        public Skin PlayerSkin { get { return Skins.Get(equippedPlayer); } }
        public Skin WeaponSkin { get { return Skins.Get(equippedWeapon); } }

        public bool Has(string key) { return owned.Contains(key); }

        public int CaseCount(string key = null)
        {
            if (key == null)
            {
                int n = 0;
                foreach (var kv in cases) n += kv.Value;
                return n;
            }
            int v;
            return cases.TryGetValue(key, out v) ? v : 0;
        }

        public void AddCase(string key, int n = 1)
        {
            int cur;
            cases.TryGetValue(key, out cur);
            cases[key] = cur + n;
        }

        public bool Equip(Skin skin)
        {
            if (!owned.Contains(skin.key)) return false;
            if (skin.isPlayer) equippedPlayer = skin.key;
            else equippedWeapon = skin.key;
            return true;
        }

        public bool BuyCase(Case c)
        {
            if (scrap < c.price) return false;
            scrap -= c.price;
            AddCase(c.key);
            return true;
        }

        public struct OpenResult
        {
            public Skin skin;
            public bool duplicate;
            public int scrapGained;
            public bool ok;
        }

        public OpenResult OpenCase(Case c, Rng rng)
        {
            var res = new OpenResult();
            if (CaseCount(c.key) <= 0) return res;

            cases[c.key] = cases[c.key] - 1;
            if (cases[c.key] <= 0) cases.Remove(c.key);

            Rarity rarity = Rarities.Order[rng.PickWeighted(c.weights)];
            var pool = new List<Skin>();
            foreach (var s in Skins.All) if (s.rarity == rarity) pool.Add(s);
            if (pool.Count == 0) pool = Skins.All;

            Skin skin = rng.Pick(pool);
            res.skin = skin;
            res.ok = true;
            res.duplicate = owned.Contains(skin.key);
            if (res.duplicate)
            {
                res.scrapGained = Rarities.Scrap(skin.rarity);
                scrap += res.scrapGained;
            }
            else
            {
                owned.Add(skin.key);
            }
            return res;
        }

        /// <summary>Reel of skins for the opening animation, winner parked near the end.</summary>
        public List<Skin> RollStrip(Skin winner, Rng rng, int length = 42)
        {
            var strip = new List<Skin>(length);
            for (int i = 0; i < length; i++) strip.Add(rng.Pick(Skins.All));
            strip[length - 6] = winner;
            return strip;
        }
    }
}
