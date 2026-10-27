// C# / Assets/Scripts/Core/Settings.cs / Unity 2022.3+ / global config and balance
using UnityEngine;

namespace Nightfall
{
    public static class S
    {
        public const string STUDIO = "BIG GAMES STUDIO";
        public const string TITLE = "NIGHTFALL - Zombie Survival  |  Big Games Studio";
        public const string VERSION = "1.0";

        // logical resolution; everything is drawn in this space and letterboxed
        public const int WIDTH = 1280;
        public const int HEIGHT = 720;

        public const int WORLD_W = 2800;
        public const int WORLD_H = 2100;
        public const int WALL = 40;

        // ---- colors --------------------------------------------------------
        public static readonly Color32 C_BG = new Color32(10, 11, 13, 255);
        public static readonly Color32 C_WHITE = new Color32(236, 239, 244, 255);
        public static readonly Color32 C_DIM = new Color32(150, 156, 166, 255);
        public static readonly Color32 C_RED = new Color32(214, 64, 58, 255);
        public static readonly Color32 C_DARKRED = new Color32(120, 24, 22, 255);
        public static readonly Color32 C_BLOOD = new Color32(104, 18, 18, 255);
        public static readonly Color32 C_GREEN = new Color32(120, 190, 90, 255);
        public static readonly Color32 C_YELLOW = new Color32(226, 184, 72, 255);
        public static readonly Color32 C_CYAN = new Color32(96, 198, 212, 255);
        public static readonly Color32 C_ORANGE = new Color32(238, 142, 56, 255);

        // ---- player --------------------------------------------------------
        public const float P_RADIUS = 15f;
        public const float P_SPEED = 245f;
        public const int P_MAX_HP = 100;
        public const float P_DASH_SPEED = 3.1f;
        public const float P_DASH_TIME = 0.20f;
        public const float P_DASH_CD = 1.05f;
        public const float P_HURT_IFRAME = 0.45f;

        // ---- waves ---------------------------------------------------------
        public const int SPAWN_MARGIN = 120;
        public const float SPAWN_MIN_DIST = 640f;
        public const int MAX_ALIVE = 110;

        // ---- night ---------------------------------------------------------
        public const float LIGHT_RADIUS = 360f;
        public const int DARKNESS = 228;

        public const string SAVE_FILE = "nightfall_save.json";

        public static Color Col(byte r, byte g, byte b) { return new Color32(r, g, b, 255); }

        /// <summary>Multiply a colour's rgb, keeping alpha.</summary>
        public static Color Mul(Color c, float f)
        {
            return new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), c.a);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            return new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t,
                             a.b + (b.b - a.b) * t, a.a + (b.a - a.a) * t);
        }

        public static Color Lighter(Color c, int k)
        {
            float f = k / 255f;
            return new Color(Mathf.Clamp01(c.r + f), Mathf.Clamp01(c.g + f), Mathf.Clamp01(c.b + f), c.a);
        }

        public static Color Darker(Color c, int k)
        {
            float f = k / 255f;
            return new Color(Mathf.Clamp01(c.r - f), Mathf.Clamp01(c.g - f), Mathf.Clamp01(c.b - f), c.a);
        }

        public static Color WithA(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}
