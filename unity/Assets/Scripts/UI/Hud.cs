// C# / Assets/Scripts/UI/Hud.cs / Unity 2022.3+ / bars, ammo, waves, boss bar, crosshair
using UnityEngine;

namespace Nightfall
{
    public static class Hud
    {
        public static void Panel(float x, float y, float w, float h, float alpha = 150f / 255f,
                                 float radius = 6f)
        {
            Painter.RoundRect(x, y, w, h, radius, new Color(10f / 255f, 11f / 255f, 14f / 255f, alpha));
            Painter.RoundRectOutline(x, y, w, h, radius, 2f,
                                     new Color(70f / 255f, 74f / 255f, 82f / 255f, 180f / 255f));
        }

        public static void Draw(GameApp game)
        {
            Player p = game.player;
            const int W = S.WIDTH, H = S.HEIGHT;

            // ---- health ----------------------------------------------------
            Panel(22f, H - 104f, 340f, 82f);
            float frac = MathUtil.Clamp(p.hp / p.maxHp, 0f, 1f);
            float bx = 38f, by = H - 92f, bw = 308f, bh = 24f;
            Painter.RoundRect(bx, by, bw, bh, 3f, S.Col(28, 14, 14));
            Color col = frac > 0.55f ? (Color)S.C_GREEN : (frac > 0.28f ? (Color)S.C_YELLOW : (Color)S.C_RED);
            Painter.RoundRect(bx, by, bw * frac, bh, 3f, col);
            Painter.RoundRectOutline(bx, by, bw, bh, 3f, 2f, S.Col(90, 94, 102));
            Txt.Draw((int)p.hp + " / " + (int)p.maxHp, 17, bx + bw * 0.5f, by + bh * 0.5f,
                     S.C_WHITE, Anchor.Center, true);

            // dash charge
            Txt.Draw("DASH", 13, 38f, H - 60f, S.C_DIM, Anchor.TopLeft, true);
            float dx = 92f, dy = H - 56f, dw = 254f, dh = 10f;
            Painter.RoundRect(dx, dy, dw, dh, 3f, S.Col(24, 26, 30));
            float dfrac = 1f - MathUtil.Clamp(p.dashCd / S.P_DASH_CD, 0f, 1f);
            Color dcol = dfrac >= 1f ? (Color)S.C_CYAN : S.Col(70, 110, 130);
            Painter.RoundRect(dx, dy, dw * dfrac, dh, 3f, dcol);
            Txt.Draw("SPACE", 12, 346f, H - 38f, S.C_DIM, Anchor.TopRight);

            // ---- weapon / ammo --------------------------------------------
            float px0 = W - 362f, py0 = H - 128f, pw = 340f, ph = 116f;
            Panel(px0, py0, pw, ph);
            Weapon w = p.weapon;

            for (int i = 0; i < Weapons.Order.Length; i++)
            {
                string key = Weapons.Order[i];
                bool owned = p.weapons.ContainsKey(key);
                bool active = key == p.current;
                var slot = new RectI((int)(px0 + 14f + i * 78f), (int)(py0 + 44f), 72, 32);

                if (active)
                {
                    Painter.RoundRect(slot.x, slot.y, slot.w, slot.h, 4f, S.Col(58, 50, 20));
                    Painter.RoundRectOutline(slot.x, slot.y, slot.w, slot.h, 4f, 2f, S.C_YELLOW);
                }
                else
                {
                    Painter.RoundRect(slot.x, slot.y, slot.w, slot.h, 4f, S.Col(26, 28, 34));
                }

                GunModels.DrawIcon(slot.Inflate(-8, -8), key, p.weaponSkin, owned);
                Txt.Draw((i + 1).ToString(), 12, slot.x + 4f, slot.y + 2f,
                         active ? (Color)S.C_YELLOW : (owned ? (Color)S.C_DIM : S.Col(70, 72, 78)),
                         Anchor.TopLeft, active);
            }

            Txt.Draw(w.spec.name, 19, px0 + 16f, py0 + 8f, S.C_WHITE, Anchor.TopLeft, true);
            Txt.Draw(p.weaponSkin.name, 13, px0 + pw - 16f, py0 + 10f,
                     p.weaponSkin.accent, Anchor.TopRight, true);

            if (w.reloading > 0f)
            {
                float rf = 1f - MathUtil.Clamp(w.reloading / w.spec.reloadTime, 0f, 1f);
                float rx = px0 + 16f, ry = py0 + 84f, rw = pw - 32f, rh = 24f;
                Painter.RoundRect(rx, ry, rw, rh, 3f, S.Col(26, 26, 30));
                Painter.RoundRect(rx, ry, rw * rf, rh, 3f, S.C_ORANGE);
                Txt.Draw("RELOADING", 14, rx + rw * 0.5f, ry + rh * 0.5f,
                         S.C_WHITE, Anchor.Center, true);
            }
            else
            {
                string res = w.spec.infinite ? "~" : w.reserve.ToString();
                Color ammoCol = w.inMag > 0 ? (Color)S.C_WHITE : (Color)S.C_RED;
                Txt.Draw(w.inMag.ToString(), 36, px0 + 16f, py0 + 110f, ammoCol,
                         Anchor.BottomLeft, true);
                Txt.Draw("/ " + res, 20, px0 + 80f, py0 + 106f, S.C_DIM, Anchor.BottomLeft);
                if (w.inMag == 0)
                    Txt.Draw("PRESS  R", 18, px0 + pw - 16f, py0 + 108f, S.C_ORANGE,
                             Anchor.BottomRight, true);
            }

            // ---- wave / score ----------------------------------------------
            Panel(W * 0.5f - 150f, 14f, 300f, 72f);
            Txt.Draw(game.mode.name, 14, W * 0.5f, 18f, S.C_DIM, Anchor.MidTop, true);
            Txt.Draw("WAVE " + game.wave, 26, W * 0.5f, 34f, S.C_WHITE, Anchor.MidTop, true);

            if (game.mode.timeLimit > 0f)
            {
                float t = Mathf.Max(0f, game.timeLeft);
                Color tc = t < 30f ? (Color)S.C_RED : (Color)S.C_YELLOW;
                Txt.Draw(((int)(t / 60f)) + ":" + ((int)(t % 60f)).ToString("00"), 20,
                         W * 0.5f, 60f, tc, Anchor.MidTop, true);
            }
            else if (game.state == GameState.Break)
            {
                Txt.Draw("NEXT IN " + Mathf.CeilToInt(game.breakTimer), 15, W * 0.5f, 62f,
                         S.C_YELLOW, Anchor.MidTop);
            }
            else
            {
                Txt.Draw("ZOMBIES " + game.ZombiesLeft(), 15, W * 0.5f, 62f,
                         S.C_DIM, Anchor.MidTop);
            }

            Panel(W - 232f, 18f, 210f, 52f);
            Txt.Draw("SCORE", 13, W - 212f, 26f, S.C_DIM);
            Txt.Draw(game.score.ToString(), 24, W - 36f, 38f, S.C_WHITE, Anchor.TopRight, true);

            Panel(22f, 18f, 196f, 52f);
            Txt.Draw("KILLS", 13, 40f, 26f, S.C_DIM);
            Txt.Draw(p.kills.ToString(), 24, 200f, 38f, S.C_WHITE, Anchor.TopRight, true);

            int cases = game.inventory.CaseCount();
            if (cases > 0)
            {
                Panel(22f, 76f, 196f, 34f);
                Txt.Draw("CASES", 13, 40f, 84f, S.C_DIM);
                Txt.Draw(cases.ToString(), 18, 200f, 84f, S.Col(150, 210, 235),
                         Anchor.TopRight, true);
            }

            if (p.speedBoost > 0f)
                Txt.Draw("ADRENALINE " + p.speedBoost.ToString("0.0") + "s", 18,
                         W * 0.5f, 82f, S.C_CYAN, Anchor.MidTop, true);

            Txt.Draw(game.world.mdef.name, 13, W - 236f, 74f, S.C_DIM, Anchor.TopRight);
            DrawBossBar(game);
        }

        static void DrawBossBar(GameApp game)
        {
            Zombie boss = null;
            for (int i = 0; i < game.zombies.Count; i++)
                if (game.zombies[i].spec.key == "boss") { boss = game.zombies[i]; break; }
            if (boss == null) return;

            float bx = S.WIDTH * 0.5f - 260f, by = 86f, bw = 520f, bh = 20f;
            float frac = MathUtil.Clamp(boss.hp / boss.maxHp, 0f, 1f);
            Painter.RoundRect(bx - 2f, by - 2f, bw + 4f, bh + 4f, 3f, S.Col(20, 10, 10));
            Painter.RoundRect(bx, by, bw, bh, 3f, S.C_DARKRED);
            Painter.RoundRect(bx, by, bw * frac, bh, 3f, S.C_RED);
            Painter.RoundRectOutline(bx, by, bw, bh, 3f, 2f, S.Col(120, 124, 130));
            Txt.Draw(boss.spec.name, 18, bx + bw * 0.5f, by - 4f, S.C_WHITE,
                     Anchor.MidBottom, true);
        }

        public static void DrawCrosshair(Vector2 mouse, Player player)
        {
            float spread = 7f + player.recoil * 14f;
            Color col = player.weapon.CanFire() ? (Color)S.C_WHITE : (Color)S.C_RED;
            for (int i = 0; i < 4; i++)
            {
                Vector2 d = MathUtil.Rotate(Vector2.right, i * 90f);
                Painter.Line(mouse + d * spread, mouse + d * (spread + 8f), 2f, col);
            }
            Painter.Circle(mouse.x, mouse.y, 1.5f, col);
        }

        public static void DrawBanner(string text, string sub, float t)
        {
            float a = MathUtil.Clamp(t, 0f, 1f);
            Txt.DrawShadowed(text, 72, S.WIDTH * 0.5f, S.HEIGHT * 0.5f - 40f,
                             S.C_RED, Anchor.Center, true, a, 4f);
            if (!string.IsNullOrEmpty(sub))
                Txt.Draw(sub, 24, S.WIDTH * 0.5f, S.HEIGHT * 0.5f + 24f,
                         S.C_WHITE, Anchor.Center, true, a);
        }
    }
}
