// C# / Assets/Scripts/UI/Armory.cs / Unity 2022.3+ / skin locker and case opening
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    public struct Hit
    {
        public string kind;
        public int index;
        public string key;
        public RectI rect;

        public Hit(string kind, int index, RectI rect, string key = null)
        {
            this.kind = kind; this.index = index; this.rect = rect; this.key = key;
        }
    }

    public static class Ui
    {
        public static readonly Color PANEL = S.Col(10, 11, 15);
        public static readonly Color EDGE = S.Col(74, 78, 88);
        public static readonly Color DIM = S.Col(146, 152, 162);
        public static readonly Color HOT = S.Col(226, 188, 72);
        public static readonly Color RED = S.Col(214, 64, 58);

        public static void Panel(RectI r, float alpha = 190f / 255f, float radius = 8f,
                                 Color? edge = null, float width = 2f)
        {
            Painter.RoundRect(r.x, r.y, r.w, r.h, radius, S.WithA(PANEL, alpha));
            Painter.RoundRectOutline(r.x, r.y, r.w, r.h, radius, width,
                                     S.WithA(edge ?? EDGE, 210f / 255f));
        }

        /// <summary>Little top-down avatar so a player skin can be judged at a glance.</summary>
        public static void DrawPlayerBust(RectI rect, Skin skin, bool lit = true)
        {
            float cx = rect.CenterX, cy = rect.CenterY;
            float r = Mathf.Min(rect.w, rect.h) / 4f;
            Color body = lit ? skin.body : S.Mul(skin.body, 0.45f);
            Color trim = lit ? skin.trim : S.Mul(skin.trim, 0.45f);
            Color acc = lit ? skin.accent : S.Mul(skin.accent, 0.45f);

            if (skin.pattern == "glow" && lit)
            {
                Painter.Flip(true);
                Painter.Circle(cx, cy, r * 3f, S.WithA(acc, 70f / 255f));
                Painter.Flip(false);
            }

            for (int s = -1; s <= 1; s += 2)
                Painter.Line(cx + s * r * 0.55f, cy + r * 0.2f,
                             cx + s * r * 0.75f, cy + r * 1.5f, 5f, trim);

            Painter.Circle(cx, cy, r + 2f, S.Mul(trim, 0.55f));
            Painter.Circle(cx, cy, r, body);

            if (skin.pattern == "stripe")
                Painter.Line(cx - r, cy, cx + r, cy, 4f, trim);
            else if (skin.pattern == "camo")
            {
                Painter.Circle(cx - 4f, cy - 3f, 4f, trim);
                Painter.Circle(cx + 3f, cy + 3f, 5f, trim);
                Painter.Circle(cx + 2f, cy - 5f, 3f, trim);
            }
            else if (skin.pattern == "splatter")
            {
                Color sp = S.Mul(trim, 0.7f);
                Painter.Circle(cx - 5f, cy + 2f, 3f, sp);
                Painter.Circle(cx + 4f, cy - 4f, 2f, sp);
                Painter.Circle(cx + 1f, cy + 5f, 3f, sp);
            }

            Painter.CircleOutline(cx, cy, r, 2f, S.Mul(trim, 0.4f));
            Painter.Circle(cx, cy - r - 4f, 6f, acc);
        }

        public static void DrawSkinTile(RectI rect, Skin skin, bool owned, bool equipped,
                                        bool hover, string weaponKey = "rifle")
        {
            Color rc = Rarities.Col(skin.rarity);
            Color edge = (owned || hover) ? rc : S.Col(60, 62, 70);
            Panel(rect, 200f / 255f, 8f, edge, equipped ? 3f : 2f);

            Painter.RoundRect(rect.x + 4, rect.Bottom - 30, rect.w - 8, 4f, 2f, rc);

            var art = new RectI(rect.x + 8, rect.y + 10, rect.w - 16, rect.h - 56);
            if (skin.isPlayer) DrawPlayerBust(art, skin, owned);
            else GunModels.DrawIcon(art, weaponKey, skin, owned);

            Txt.Draw(skin.name, 15, rect.CenterX, rect.Bottom - 26,
                     owned ? S.Col(240, 236, 230) : S.Col(110, 114, 122),
                     Anchor.MidTop, owned);

            if (equipped)
                Txt.Draw("EQUIPPED", 12, rect.CenterX, rect.y + 6, HOT, Anchor.MidTop, true);
            else if (!owned)
                Txt.Draw("LOCKED", 12, rect.CenterX, rect.y + 6, S.Col(120, 60, 56),
                         Anchor.MidTop, true);
        }
    }

    // ------------------------------------------------------------------ armory
    public class Armory
    {
        readonly Inventory _inv;
        public string tab = "player";
        int _hover = -1;
        public readonly List<Hit> hits = new List<Hit>();
        float _t;
        string _toast = "";
        float _toastT;

        public Armory(Inventory inv) { _inv = inv; }

        List<Skin> Current { get { return tab == "player" ? Skins.PlayerSkins : Skins.WeaponSkins; } }

        public void Update(float dt, Vector2 mouse)
        {
            _t += dt;
            _toastT = Mathf.Max(0f, _toastT - dt);
            _hover = -1;
            for (int i = 0; i < hits.Count; i++)
            {
                if (!hits[i].rect.Contains(mouse)) continue;
                if (hits[i].kind == "skin") _hover = hits[i].index;
                break;
            }
        }

        /// <summary>Returns one of: back, blip, equip, deny, or null.</summary>
        public string HandleClick(Vector2 pos)
        {
            for (int i = 0; i < hits.Count; i++)
            {
                Hit h = hits[i];
                if (!h.rect.Contains(pos)) continue;

                if (h.kind == "tab") { tab = h.key; return "blip"; }
                if (h.kind == "back") return "back";
                if (h.kind == "skin")
                {
                    Skin skin = Current[h.index];
                    if (_inv.Has(skin.key))
                    {
                        _inv.Equip(skin);
                        _toast = skin.name + " EQUIPPED";
                        _toastT = 1.6f;
                        return "equip";
                    }
                    _toast = "LOCKED - open cases to find it";
                    _toastT = 1.8f;
                    return "deny";
                }
            }
            return null;
        }

        public void Draw()
        {
            hits.Clear();
            Txt.Draw("ARMORY", 26, S.WIDTH / 2f, 104f, Ui.HOT, Anchor.MidTop, true);

            string[] keys = { "player", "weapon" };
            string[] labels = { "SURVIVOR SKINS", "WEAPON FINISHES" };
            for (int i = 0; i < 2; i++)
            {
                var rect = new RectI(S.WIDTH / 2 - 310 + i * 310, 140, 300, 40);
                bool active = tab == keys[i];
                Ui.Panel(rect, 200f / 255f, 6f, active ? Ui.HOT : Ui.EDGE);
                Txt.Draw(labels[i], 18, rect.CenterX, rect.CenterY,
                         active ? S.Col(245, 238, 228) : Ui.DIM, Anchor.Center, active);
                hits.Add(new Hit("tab", i, rect, keys[i]));
            }

            List<Skin> skins = Current;
            const int cols = 6, tw = 182, th = 168, gap = 14;
            int total = cols * tw + (cols - 1) * gap;
            int x0 = (S.WIDTH - total) / 2;
            const int y0 = 198;

            for (int i = 0; i < skins.Count; i++)
            {
                int cx = x0 + (i % cols) * (tw + gap);
                int cy = y0 + (i / cols) * (th + gap);
                var rect = new RectI(cx, cy, tw, th);
                Skin sk = skins[i];
                bool owned = _inv.Has(sk.key);
                bool equipped = (sk.isPlayer ? _inv.equippedPlayer : _inv.equippedWeapon) == sk.key;
                Ui.DrawSkinTile(rect, sk, owned, equipped, _hover == i);
                hits.Add(new Hit("skin", i, rect));
            }

            var info = new RectI(x0, y0 + 2 * (th + gap) + 8, total, 56);
            Ui.Panel(info, 170f / 255f);
            int ownedN = 0;
            for (int i = 0; i < Skins.All.Count; i++) if (_inv.Has(Skins.All[i].key)) ownedN++;

            Txt.Draw("COLLECTED  " + ownedN + " / " + Skins.All.Count, 19,
                     info.x + 20, info.CenterY, S.Col(230, 226, 220), Anchor.MidLeft, true);
            Txt.Draw("SCRAP  " + _inv.scrap, 19, info.CenterX, info.CenterY,
                     Ui.HOT, Anchor.Center, true);
            Txt.Draw(_toastT > 0f ? _toast : "click a skin to equip",
                     _toastT > 0f ? 18 : 16, info.Right - 20, info.CenterY,
                     _toastT > 0f ? S.Col(240, 230, 210) : Ui.DIM, Anchor.MidRight);
        }
    }

    // --------------------------------------------------------------- case room
    public class CaseRoom
    {
        const int CARD_W = 128, CARD_H = 150, TRACK_X = 60, PAD = 10;

        readonly Inventory _inv;
        readonly Rng _rng;
        public readonly List<Hit> hits = new List<Hit>();

        public int sel;
        public string state = "idle";      // idle | rolling | result
        List<Skin> _strip = new List<Skin>();
        float _offset, _target, _rollT;
        const float RollLen = 4.2f;
        Skin _result;
        bool _dup;
        int _gained;
        string _toast = "";
        float _toastT, _flash, _t;

        public CaseRoom(Inventory inv, Rng rng)
        {
            _inv = inv;
            _rng = rng ?? new Rng(99);
        }

        public void Update(float dt, Vector2 mouse)
        {
            _t += dt;
            _toastT = Mathf.Max(0f, _toastT - dt);
            _flash = Mathf.Max(0f, _flash - dt * 1.6f);

            if (state == "rolling")
            {
                _rollT += dt;
                float k = MathUtil.Clamp(_rollT / RollLen, 0f, 1f);
                float eased = 1f - Mathf.Pow(1f - k, 3.4f);
                _offset = _target * eased;
                if (k >= 1f) { state = "result"; _flash = 1f; }
            }
        }

        /// <summary>Returns one of: back, blip, buy, roll, deny, or null.</summary>
        public string HandleClick(Vector2 pos)
        {
            if (state == "rolling") return null;

            for (int i = 0; i < hits.Count; i++)
            {
                Hit h = hits[i];
                if (!h.rect.Contains(pos)) continue;

                if (h.kind == "case")
                {
                    sel = h.index;
                    if (state == "result") state = "idle";
                    return "blip";
                }
                if (h.kind == "open") return Open();
                if (h.kind == "buy")
                {
                    Case c = Skins.Cases[sel];
                    if (_inv.BuyCase(c))
                    {
                        _toast = "BOUGHT " + c.name;
                        _toastT = 1.6f;
                        return "buy";
                    }
                    _toast = "NOT ENOUGH SCRAP";
                    _toastT = 1.6f;
                    return "deny";
                }
                if (h.kind == "back") return "back";
            }

            if (state == "result") state = "idle";
            return null;
        }

        string Open()
        {
            Case c = Skins.Cases[sel];
            if (_inv.CaseCount(c.key) <= 0)
            {
                _toast = "NO CASES OF THAT KIND";
                _toastT = 1.6f;
                return "deny";
            }

            Inventory.OpenResult res = _inv.OpenCase(c, _rng);
            if (!res.ok) return "deny";

            _result = res.skin;
            _dup = res.duplicate;
            _gained = res.scrapGained;
            _strip = _inv.RollStrip(res.skin, _rng);

            int winIndex = _strip.Count - 6;
            const float step = CARD_W + PAD;
            float centrePull = (S.WIDTH / 2f) - TRACK_X - PAD - CARD_W / 2f;
            _target = winIndex * step - centrePull + _rng.Range(-CARD_W * 0.3f, CARD_W * 0.3f);
            _offset = 0f;
            _rollT = 0f;
            state = "rolling";
            return "roll";
        }

        public void Draw()
        {
            hits.Clear();
            Txt.Draw("CASES", 26, S.WIDTH / 2f, 104f, Ui.HOT, Anchor.MidTop, true);

            if (state == "rolling" || state == "result") { DrawRoll(); return; }

            const int cw = 300, ch = 250, gap = 30;
            int n = Skins.Cases.Count;
            int total = n * cw + (n - 1) * gap;
            int x0 = (S.WIDTH - total) / 2;
            const int y0 = 170;

            for (int i = 0; i < n; i++)
            {
                Case c = Skins.Cases[i];
                var rect = new RectI(x0 + i * (cw + gap), y0, cw, ch);
                bool selected = i == sel;
                Ui.Panel(rect, 205f / 255f, 8f, selected ? c.color : Ui.EDGE, selected ? 3f : 2f);
                hits.Add(new Hit("case", i, rect));

                DrawCrate(new RectI(rect.x + 70, rect.y + 24, 160, 120), c.color, selected);
                Txt.Draw(c.name, 20, rect.CenterX, rect.y + 156,
                         selected ? S.Col(244, 238, 228) : Ui.DIM, Anchor.MidTop, selected);
                Txt.Draw(c.tagline, 14, rect.CenterX, rect.y + 180,
                         S.Col(128, 132, 142), Anchor.MidTop);

                int owned = _inv.CaseCount(c.key);
                Txt.Draw("x" + owned, 24, rect.CenterX, rect.Bottom - 34,
                         owned > 0 ? Ui.HOT : S.Col(110, 114, 122), Anchor.MidTop, true);
            }

            Case cur = Skins.Cases[sel];
            var bar = new RectI(x0, y0 + ch + 22, total, 62);
            Ui.Panel(bar, 180f / 255f);
            Txt.Draw("SCRAP  " + _inv.scrap, 20, bar.x + 22, bar.CenterY,
                     Ui.HOT, Anchor.MidLeft, true);

            string odds = "";
            for (int i = 0; i < Rarities.Order.Length; i++)
            {
                if (i > 0) odds += "  ";
                odds += Rarities.Name(Rarities.Order[i]).Substring(0, 4) + " " + (int)cur.weights[i] + "%";
            }
            Txt.Draw(odds, 15, bar.CenterX, bar.CenterY, Ui.DIM, Anchor.Center);

            var openBtn = new RectI(bar.Right - 330, bar.y + 12, 150, 38);
            var buyBtn = new RectI(bar.Right - 170, bar.y + 12, 150, 38);
            bool has = _inv.CaseCount(cur.key) > 0;
            Ui.Panel(openBtn, 220f / 255f, 6f, has ? Ui.HOT : S.Col(70, 70, 76));
            Txt.Draw("OPEN", 19, openBtn.CenterX, openBtn.CenterY,
                     has ? S.Col(250, 240, 220) : S.Col(110, 112, 118), Anchor.Center, true);

            bool afford = _inv.scrap >= cur.price;
            Ui.Panel(buyBtn, 220f / 255f, 6f, afford ? S.Col(120, 190, 110) : S.Col(70, 70, 76));
            Txt.Draw("BUY " + cur.price, 18, buyBtn.CenterX, buyBtn.CenterY,
                     afford ? S.Col(235, 245, 230) : S.Col(110, 112, 118), Anchor.Center, true);

            hits.Add(new Hit("open", 0, openBtn));
            hits.Add(new Hit("buy", 0, buyBtn));

            Txt.Draw(_toastT > 0f ? _toast : "cases drop from zombies and after every 5th wave",
                     _toastT > 0f ? 18 : 16, S.WIDTH / 2f, bar.Bottom + 18,
                     _toastT > 0f ? S.Col(240, 226, 200) : Ui.DIM, Anchor.MidTop);
        }

        void DrawCrate(RectI rect, Color color, bool lit)
        {
            Color baseCol = S.Mul(color, lit ? 0.85f : 0.5f);

            if (lit)
            {
                Painter.Flip(true);
                Painter.RadialBlob(rect.CenterX, rect.CenterY, rect.w * 0.7f,
                                   S.WithA(color, 46f / 255f));
                Painter.Flip(false);
            }

            Painter.RoundRect(rect.x, rect.y, rect.w, rect.h, 6f, S.Col(46, 40, 34));
            Painter.RoundRect(rect.x + 7, rect.y + 7, rect.w - 14, rect.h - 14, 4f, S.Col(70, 60, 48));
            Painter.RoundRectOutline(rect.x, rect.y, rect.w, rect.h, 6f, 4f, baseCol);
            Painter.Line(rect.x, rect.y, rect.Right, rect.Bottom, 3f, baseCol);
            Painter.Line(rect.Right, rect.y, rect.x, rect.Bottom, 3f, baseCol);
            Painter.RoundRect(rect.CenterX - 16, rect.CenterY - 12, 32, 24, 4f, baseCol);
        }

        void DrawRoll()
        {
            int cy = S.HEIGHT / 2 - 40;
            var track = new RectI(TRACK_X, cy - CARD_H / 2 - 14,
                                  S.WIDTH - TRACK_X * 2, CARD_H + 28);
            Ui.Panel(track, 210f / 255f);

            const float step = CARD_W + PAD;
            for (int i = 0; i < _strip.Count; i++)
            {
                float x = track.x + PAD + i * step - _offset;
                if (x < track.x - step || x > track.Right + step) continue;

                var rect = new RectI((int)x, cy - CARD_H / 2, CARD_W, CARD_H);
                Skin sk = _strip[i];
                Color rc = Rarities.Col(sk.rarity);
                Ui.Panel(rect, 230f / 255f, 6f, rc);

                var art = new RectI(rect.x + 6, rect.y + 8, rect.w - 12, rect.h - 48);
                if (sk.isPlayer) Ui.DrawPlayerBust(art, sk);
                else GunModels.DrawIcon(art, "rifle", sk);

                Txt.Draw(sk.name, 13, rect.CenterX, rect.Bottom - 28,
                         S.Col(236, 232, 226), Anchor.MidTop);
                Painter.RoundRect(rect.x + 6, rect.Bottom - 10, rect.w - 12, 4f, 2f, rc);
            }

            // curtains instead of a scissor rect, so cards never bleed past the panel
            Painter.Rect(0f, track.y - 2f, track.x + 4f, track.h + 4f, S.Col(11, 12, 16));
            Painter.Rect(track.Right - 4f, track.y - 2f, S.WIDTH - track.Right + 4f,
                         track.h + 4f, S.Col(11, 12, 16));
            Ui.Panel(track, 0f, 8f);

            float mid = S.WIDTH / 2f;
            Painter.Line(mid, track.y - 10f, mid, track.Bottom + 10f, 3f, S.Col(236, 72, 64));
            Painter.Polygon(new[]
            {
                new Vector2(mid - 10f, track.y - 10f),
                new Vector2(mid + 10f, track.y - 10f),
                new Vector2(mid, track.y + 6f)
            }, S.Col(236, 72, 64));

            if (state == "result" && _result != null)
            {
                Skin sk = _result;
                Color rc = Rarities.Col(sk.rarity);
                var card = new RectI(S.WIDTH / 2 - 230, track.Bottom + 26, 460, 112);
                Ui.Panel(card, 225f / 255f, 8f, rc, 3f);

                Txt.Draw(Rarities.Name(sk.rarity), 16, card.CenterX, card.y + 12,
                         rc, Anchor.MidTop, true);
                Txt.Draw(sk.name, 30, card.CenterX, card.y + 34, rc, Anchor.MidTop, true);

                string sub = _dup
                    ? "duplicate  +" + _gained + " scrap"
                    : (sk.isPlayer ? "new survivor skin" : "new weapon finish");
                Txt.Draw(sub, 17, card.CenterX, card.y + 74,
                         _dup ? Ui.DIM : Ui.HOT, Anchor.MidTop);
                Txt.Draw("click anywhere to continue", 15, S.WIDTH / 2f, card.Bottom + 16,
                         Ui.DIM, Anchor.MidTop);

                if (_flash > 0f)
                    Painter.Rect(0f, 0f, S.WIDTH, S.HEIGHT, S.WithA(rc, 90f / 255f * _flash));
            }
        }
    }
}
