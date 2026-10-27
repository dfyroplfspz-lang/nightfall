// C# / Assets/Scripts/UI/Menu.cs / Unity 2022.3+ / front end: modes, maps, records, controls
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    /// <summary>
    /// Fog, drifting silhouettes and a flickering lamp: the menu is a place, not a
    /// list of buttons.
    /// </summary>
    public class Backdrop
    {
        readonly Rng _rng;
        float _t;
        readonly float[] _fog;        // x, y, r, speed
        readonly float[] _walkers;    // x, y, speed, phase, scale
        readonly float[] _splats;     // x, y, r
        float _flicker = 1f;

        public Backdrop(int seed = 3)
        {
            _rng = new Rng(seed);

            _fog = new float[14 * 4];
            for (int i = 0; i < 14; i++)
            {
                _fog[i * 4 + 0] = _rng.Range(-200f, S.WIDTH + 200f);
                _fog[i * 4 + 1] = _rng.Range(0f, S.HEIGHT);
                _fog[i * 4 + 2] = _rng.Range(90f, 260f);
                _fog[i * 4 + 3] = _rng.Range(6f, 22f);
            }

            _walkers = new float[7 * 5];
            for (int i = 0; i < 7; i++)
            {
                _walkers[i * 5 + 0] = _rng.Range(-200f, S.WIDTH + 200f);
                _walkers[i * 5 + 1] = _rng.Range(S.HEIGHT * 0.42f, S.HEIGHT * 0.95f);
                _walkers[i * 5 + 2] = _rng.Range(9f, 26f) * _rng.Sign();
                _walkers[i * 5 + 3] = _rng.Range(0f, 6.28f);
                _walkers[i * 5 + 4] = _rng.Range(0.7f, 1.5f);
            }

            _splats = new float[16 * 3];
            for (int i = 0; i < 16; i++)
            {
                _splats[i * 3 + 0] = _rng.RangeInt(0, S.WIDTH);
                _splats[i * 3 + 1] = _rng.RangeInt(0, S.HEIGHT);
                _splats[i * 3 + 2] = _rng.RangeInt(14, 54);
            }
        }

        public void Update(float dt)
        {
            _t += dt;
            for (int i = 0; i < 14; i++)
            {
                _fog[i * 4 + 0] += _fog[i * 4 + 3] * dt;
                if (_fog[i * 4 + 0] > S.WIDTH + 300f)
                {
                    _fog[i * 4 + 0] = -300f;
                    _fog[i * 4 + 1] = _rng.Range(0f, S.HEIGHT);
                }
            }

            for (int i = 0; i < 7; i++)
            {
                _walkers[i * 5 + 0] += _walkers[i * 5 + 2] * dt;
                _walkers[i * 5 + 3] += dt * 5f;
                if (_walkers[i * 5 + 0] < -220f) _walkers[i * 5 + 0] = S.WIDTH + 200f;
                else if (_walkers[i * 5 + 0] > S.WIDTH + 220f) _walkers[i * 5 + 0] = -200f;
            }

            if (_rng.Value < 0.04f) _flicker = _rng.Range(0.55f, 1f);
            _flicker += (1f - _flicker) * Mathf.Min(1f, dt * 4f);
        }

        public void Draw()
        {
            Painter.Rect(0f, 0f, S.WIDTH, S.HEIGHT, S.Col(11, 12, 16));
            Painter.Rect(0f, S.HEIGHT * 0.38f, S.WIDTH, S.HEIGHT * 0.62f, S.Col(16, 17, 22));

            for (int i = 0; i < 16; i++)
                Painter.Circle(_splats[i * 3], _splats[i * 3 + 1], _splats[i * 3 + 2],
                               new Color32(46, 14, 14, 70));

            for (int i = 0; i < 7; i++) Walker(i);

            for (int i = 0; i < 14; i++)
                Painter.Circle(_fog[i * 4], _fog[i * 4 + 1], _fog[i * 4 + 2],
                               new Color32(30, 24, 26, 26));

            Painter.Flip(true);
            Painter.RadialBlob(S.WIDTH * 0.5f, 110f, 430f,
                               new Color(1f, 0.92f, 0.82f, 90f / 255f * _flicker), 9, 1.6f);
            Painter.Flip(false);

            Vignette();
        }

        void Walker(int i)
        {
            float x = _walkers[i * 5 + 0];
            float y = _walkers[i * 5 + 1];
            float sp = _walkers[i * 5 + 2];
            float ph = _walkers[i * 5 + 3];
            float sc = _walkers[i * 5 + 4];

            Color col = S.Col(22, 24, 28);
            float r = 14f * sc;
            float swing = Mathf.Sin(ph) * 7f * sc;
            float facing = sp > 0f ? 1f : -1f;

            Painter.Line(x, y, x + swing, y + 16f * sc, 5f * sc, col);
            Painter.Line(x, y, x - swing, y + 16f * sc, 5f * sc, col);
            Painter.Circle(x, y, r, col);
            Painter.Circle(x + facing * r * 0.5f, y - r * 0.9f, r * 0.6f, col);

            float arm = y - r * 0.2f;
            Painter.Line(x, arm, x + facing * r * 1.8f, arm + swing * 0.4f, 4f * sc, col);
        }

        /// <summary>Soft edge darkening, nested bands so no hard rectangle shows.</summary>
        static void Vignette()
        {
            const int steps = 26, band = 10;
            for (int i = 0; i < steps; i++)
            {
                float a = 150f / 255f * Mathf.Pow((i + 1) / (float)steps, 2.2f);
                int inset = (steps - 1 - i) * band;
                int w = S.WIDTH - inset * 2, h = S.HEIGHT - inset * 2;
                if (w <= 0 || h <= 0) continue;
                Painter.RectOutline(inset, inset, w, h, band + 1, new Color(0f, 0f, 0f, a));
            }
        }
    }

    public class Menu
    {
        readonly SaveData _save;
        readonly Inventory _inv;
        public readonly Armory armory;
        public readonly CaseRoom cases;
        readonly Backdrop _back = new Backdrop();

        public string screen = "main";
        public string modeKey = Modes.All[0].key;
        public string mapKey = Maps.All[0].key;

        readonly Dictionary<string, int> _index = new Dictionary<string, int>
        {
            { "main", 0 }, { "mode", 0 }, { "map", 0 }
        };

        readonly List<Hit> _hits = new List<Hit>();
        float _t;
        Vector2 _mouse = new Vector2(S.WIDTH / 2f, S.HEIGHT / 2f);

        static readonly string[] MainItems = { "PLAY", "ARMORY", "CASES", "RECORDS", "CONTROLS", "QUIT" };

        public Menu(SaveData save, Inventory inv, Rng rng)
        {
            _save = save;
            _inv = inv;
            armory = new Armory(inv);
            cases = new CaseRoom(inv, rng);
        }

        int Index(string s) { int v; return _index.TryGetValue(s, out v) ? v : 0; }
        void SetIndex(string s, int v) { _index[s] = v; }

        int ItemCount()
        {
            if (screen == "main") return MainItems.Length;
            if (screen == "mode") return Modes.All.Count;
            if (screen == "map") return Maps.All.Count;
            return 0;
        }

        public void Update(float dt, Vector2 mouse)
        {
            _t += dt;
            _mouse = mouse;
            _back.Update(dt);

            if (screen == "armory") { armory.Update(dt, mouse); return; }
            if (screen == "cases") { cases.Update(dt, mouse); return; }

            for (int i = 0; i < _hits.Count; i++)
            {
                Hit h = _hits[i];
                if (h.kind == screen && h.index >= 0 && h.rect.Contains(mouse))
                {
                    SetIndex(screen, h.index);
                    break;
                }
            }
        }

        // -------------------------------------------------------------- input
        /// <summary>Action verbs the game reacts to: blip, deny, save, quit, start.</summary>
        public string HandleKey(KeyCode k, out string outMode, out string outMap)
        {
            outMode = null; outMap = null;

            if (screen == "armory" || screen == "cases")
            {
                if (k == KeyCode.Escape || k == KeyCode.Backspace) { screen = "main"; return "blip"; }
                return null;
            }

            if (k == KeyCode.Escape || k == KeyCode.Backspace) return Back();

            if (screen == "records" || screen == "controls")
            {
                if (k == KeyCode.Return || k == KeyCode.Space) screen = "main";
                return null;
            }

            int n = Mathf.Max(1, ItemCount());
            if (k == KeyCode.DownArrow || k == KeyCode.S || k == KeyCode.RightArrow || k == KeyCode.D)
            {
                SetIndex(screen, (Index(screen) + 1) % n);
                return "blip";
            }
            if (k == KeyCode.UpArrow || k == KeyCode.W || k == KeyCode.LeftArrow || k == KeyCode.A)
            {
                SetIndex(screen, (Index(screen) - 1 + n) % n);
                return "blip";
            }
            if (k == KeyCode.Return || k == KeyCode.KeypadEnter || k == KeyCode.Space)
                return Activate(out outMode, out outMap);

            return null;
        }

        public string HandleClick(Vector2 pos, out string outMode, out string outMap)
        {
            outMode = null; outMap = null;

            if (screen == "armory")
            {
                string res = armory.HandleClick(pos);
                if (res == "back") { screen = "main"; return "blip"; }
                if (res == "equip") return "save";
                if (res == "deny") return "deny";
                return res != null ? "blip" : null;
            }
            if (screen == "cases")
            {
                string res = cases.HandleClick(pos);
                if (res == "back") { screen = "main"; return "blip"; }
                if (res == "buy" || res == "roll") return "save";
                if (res == "deny") return "deny";
                return res != null ? "blip" : null;
            }

            for (int i = 0; i < _hits.Count; i++)
                if (_hits[i].kind == "back" && _hits[i].rect.Contains(pos))
                    return Back();

            for (int i = 0; i < _hits.Count; i++)
            {
                Hit h = _hits[i];
                if (h.kind == screen && h.rect.Contains(pos))
                {
                    SetIndex(screen, h.index);
                    return Activate(out outMode, out outMap);
                }
            }
            return null;
        }

        string Back()
        {
            if (screen == "main") return "quit";
            screen = (screen == "mode" || screen == "records" || screen == "controls"
                      || screen == "armory" || screen == "cases") ? "main" : "mode";
            return "blip";
        }

        string Activate(out string outMode, out string outMap)
        {
            outMode = null; outMap = null;

            if (screen == "main")
            {
                string choice = MainItems[Index("main")];
                switch (choice)
                {
                    case "PLAY": screen = "mode"; break;
                    case "ARMORY": screen = "armory"; break;
                    case "CASES": screen = "cases"; break;
                    case "RECORDS": screen = "records"; break;
                    case "CONTROLS": screen = "controls"; break;
                    default: return "quit";
                }
                return "blip";
            }

            if (screen == "mode")
            {
                modeKey = Modes.All[Index("mode")].key;
                screen = "map";
                return "blip";
            }

            if (screen == "map")
            {
                mapKey = Maps.All[Index("map")].key;
                outMode = modeKey;
                outMap = mapKey;
                return "start";
            }

            screen = "main";
            return "blip";
        }

        // --------------------------------------------------------------- draw
        public void Draw()
        {
            _hits.Clear();
            _back.Draw();
            Title();

            switch (screen)
            {
                case "main": DrawMain(); break;
                case "mode": DrawModes(); break;
                case "map": DrawMaps(); break;
                case "records": DrawRecords(); break;
                case "controls": DrawControls(); break;
                case "armory": armory.Draw(); BackButton(armory.hits); break;
                case "cases": cases.Draw(); BackButton(cases.hits); break;
            }

            Studio();
            Cursor();
        }

        void Studio()
        {
            Boot.DrawMark(56f, S.HEIGHT - 58f, 0.23f, 1f);
            Txt.Draw(S.STUDIO, 15, 92f, S.HEIGHT - 66f, S.Col(150, 154, 164), Anchor.MidLeft, true);
            Txt.Draw("nightfall v" + S.VERSION, 13, 92f, S.HEIGHT - 48f,
                     S.Col(96, 100, 110), Anchor.MidLeft);
        }

        void NavBack()
        {
            var rect = new RectI(30, 28, 128, 40);
            bool hovered = rect.Contains(_mouse);
            Ui.Panel(rect, 210f / 255f, 6f, hovered ? Ui.HOT : Ui.EDGE);
            Txt.Draw("< BACK", 18, rect.CenterX, rect.CenterY,
                     hovered ? S.Col(245, 238, 228) : Ui.DIM, Anchor.Center, hovered);
            _hits.Add(new Hit("back", -1, rect));
        }

        void BackButton(List<Hit> sink)
        {
            var rect = new RectI(30, 28, 128, 40);
            bool hovered = rect.Contains(_mouse);
            Ui.Panel(rect, 210f / 255f, 6f, hovered ? Ui.HOT : Ui.EDGE);
            Txt.Draw("< BACK", 18, rect.CenterX, rect.CenterY,
                     hovered ? S.Col(245, 238, 228) : Ui.DIM, Anchor.Center, hovered);
            sink.Add(new Hit("back", 0, rect));
        }

        /// <summary>The lobby is mouse driven, so the pointer has to be visible.</summary>
        void Cursor()
        {
            float x = _mouse.x, y = _mouse.y;
            float pulse = 1f + Mathf.Sin(_t * 6f) * 0.08f;
            var pts = new[]
            {
                new Vector2(x, y),
                new Vector2(x + 15f * pulse, y + 11f * pulse),
                new Vector2(x + 8f * pulse, y + 12f * pulse),
                new Vector2(x + 12f * pulse, y + 20f * pulse),
                new Vector2(x + 8f * pulse, y + 22f * pulse),
                new Vector2(x + 4f * pulse, y + 14f * pulse),
                new Vector2(x, y + 18f * pulse)
            };
            var shadow = new Vector2[pts.Length];
            for (int i = 0; i < pts.Length; i++) shadow[i] = pts[i] + new Vector2(2f, 2f);

            // the arrow is concave, so it is drawn as a fan from the tip
            PolyFan(shadow, S.Col(16, 16, 18));
            PolyFan(pts, S.Col(242, 238, 230));
            Painter.PolygonOutline(pts, 2f, S.Col(190, 52, 46));
        }

        static void PolyFan(Vector2[] pts, Color c)
        {
            for (int i = 1; i < pts.Length - 1; i++)
                Painter.Polygon(new[] { pts[0], pts[i], pts[i + 1] }, c);
        }

        void Title()
        {
            bool small = screen != "main";
            int size = small ? 76 : 108;
            float y = small ? 26f : 62f;
            float jitter = Mathf.Sin(_t * 2.1f) * (small ? 1.4f : 2.6f);
            float cx = S.WIDTH * 0.5f + jitter;

            Txt.DrawShadowed("NIGHTFALL", size, cx, y, Ui.RED, Anchor.MidTop, true, 1f, 4f);

            if (small) return;

            float bottom = y + size * 1.15f;
            Txt.Draw("Z O M B I E   S U R V I V A L", 24, S.WIDTH * 0.5f, bottom + 6f,
                     Ui.DIM, Anchor.MidTop, true);

            float[] drips = { -250f, -176f, -58f, 42f, 152f, 246f };
            for (int i = 0; i < drips.Length; i++)
            {
                float h = 6f + (Mathf.Sin(_t * 1.1f + i * 1.7f) * 0.5f + 0.5f) * 16f;
                float dx = S.WIDTH * 0.5f + drips[i];
                Painter.Line(dx, bottom - 18f, dx, bottom - 18f + h, 3f, S.Col(128, 26, 24));
                Painter.Circle(dx, bottom - 18f + h, 2f, S.Col(128, 26, 24));
            }
        }

        void Item(string text, RectI rect, bool selected, string screenKey, int idx, int size = 30)
        {
            _hits.Add(new Hit(screenKey, idx, rect));
            if (selected)
            {
                Painter.RoundRect(rect.x, rect.y, rect.w, rect.h, 6f,
                                  new Color(90f / 255f, 24f / 255f, 22f / 255f, 120f / 255f));
                Painter.RoundRectOutline(rect.x, rect.y, rect.w, rect.h, 6f, 2f,
                                         S.WithA(Ui.HOT, 180f / 255f));
                float pulse = Mathf.Sin(_t * 7f) * 0.5f + 0.5f;
                Painter.Circle(rect.x + 18, rect.CenterY, 6f,
                               new Color((180f + 60f * pulse) / 255f, 40f / 255f, 36f / 255f, 1f));
            }
            Txt.Draw(text, size, rect.x + 38, rect.CenterY,
                     selected ? S.Col(250, 240, 230) : Ui.DIM, Anchor.MidLeft, selected);
        }

        // ------------------------------------------------------------ screens
        void DrawMain()
        {
            const int top = 276;
            for (int i = 0; i < MainItems.Length; i++)
            {
                var rect = new RectI(S.WIDTH / 2 - 190, top + i * 58, 380, 48);
                Item(MainItems[i], rect, i == Index("main"), "main", i);
            }

            Txt.Draw("best " + _save.bestScore + "   ·   skins " + _inv.owned.Count +
                     "   ·   cases " + _inv.CaseCount() + "   ·   scrap " + _inv.scrap,
                     18, S.WIDTH / 2f, S.HEIGHT - 56f, Ui.DIM, Anchor.MidBottom);
            Txt.Draw("click to choose  ·  keys work too", 15, S.WIDTH / 2f, S.HEIGHT - 28f,
                     S.Col(96, 100, 110), Anchor.MidBottom);
        }

        void DrawModes()
        {
            NavBack();
            Txt.Draw("SELECT MODE", 26, S.WIDTH / 2f, 112f, Ui.HOT, Anchor.MidTop, true);

            var list = new RectI(90, 160, 420, 400);
            Ui.Panel(list, 186f / 255f);

            for (int i = 0; i < Modes.All.Count; i++)
            {
                GameMode m = Modes.All[i];
                var rect = new RectI(list.x + 14, list.y + 16 + i * 74, list.w - 28, 64);
                Item(m.name, rect, i == Index("mode"), "mode", i, 26);
                Txt.Draw(m.tagline, 15, rect.x + 38, rect.CenterY + 18,
                         S.Col(120, 124, 134), Anchor.MidLeft);
            }

            GameMode sel = Modes.All[Index("mode")];
            var det = new RectI(540, 160, S.WIDTH - 540 - 90, 400);
            Ui.Panel(det, 186f / 255f);

            Txt.Draw(sel.name, 40, det.x + 30, det.y + 26, S.Col(250, 240, 230), Anchor.TopLeft, true);
            Txt.Draw(sel.tagline, 19, det.x + 30, det.y + 76, Ui.HOT);

            float y = det.y + 128;
            for (int i = 0; i < sel.rules.Length; i++)
            {
                Painter.Circle(det.x + 38, y + 9f, 4f, Ui.RED);
                Txt.Draw(sel.rules[i], 19, det.x + 56, y, S.Col(210, 214, 222));
                y += 34f;
            }

            RecordEntry rec = _save.Record(_save.modes, sel.key);
            Txt.Draw("PERSONAL BEST", 15, det.x + 30, det.Bottom - 86, Ui.DIM);
            Txt.Draw(rec.score + " pts   ·   wave " + rec.wave + "   ·   " + rec.kills + " kills",
                     22, det.x + 30, det.Bottom - 62, S.Col(240, 232, 220), Anchor.TopLeft, true);

            Txt.Draw("enter - choose map        esc - back", 15, S.WIDTH / 2f, S.HEIGHT - 34f,
                     S.Col(110, 114, 124), Anchor.MidBottom);
        }

        void DrawMaps()
        {
            NavBack();
            Txt.Draw("SELECT MAP", 26, S.WIDTH / 2f, 112f, Ui.HOT, Anchor.MidTop, true);

            const int cols = 3, cw = 250, chh = 198, gap = 20;
            int total = cols * cw + (cols - 1) * gap;
            int x0 = (S.WIDTH - total) / 2;
            const int y0 = 150;

            for (int i = 0; i < Maps.All.Count; i++)
            {
                MapDef mp = Maps.All[i];
                int cx = x0 + (i % cols) * (cw + gap);
                int cy = y0 + (i / cols) * (chh + gap);
                var rect = new RectI(cx, cy, cw, chh);
                bool selected = i == Index("map");
                _hits.Add(new Hit("map", i, rect));

                Ui.Panel(rect, 200f / 255f, 8f, selected ? Ui.HOT : Ui.EDGE);
                var thumb = new RectI(cx + 11, cy + 11, cw - 22, chh - 70);
                Maps.DrawPreview(mp, thumb);
                if (selected) Painter.RectOutline(thumb.x, thumb.y, thumb.w, thumb.h, 2f, Ui.HOT);

                Txt.Draw(mp.name, 20, cx + cw / 2f, cy + chh - 48,
                         selected ? S.Col(250, 240, 230) : Ui.DIM, Anchor.MidTop, selected);
                Txt.Draw(mp.tagline, 14, cx + cw / 2f, cy + chh - 26,
                         S.Col(120, 124, 134), Anchor.MidTop);
            }

            MapDef cur = Maps.All[Index("map")];
            RecordEntry rec = _save.Record(_save.maps, cur.key);
            var bar = new RectI(x0, y0 + 2 * (chh + gap) + 10, total, 56);
            Ui.Panel(bar, 170f / 255f);

            Txt.Draw(cur.name + "  —  " + cur.tagline, 19, bar.x + 20, bar.CenterY,
                     S.Col(230, 226, 220), Anchor.MidLeft);
            Txt.Draw("best " + rec.score + " pts · wave " + rec.wave, 18,
                     bar.Right - 20, bar.CenterY, Ui.HOT, Anchor.MidRight, true);
            Txt.Draw("enter - drop in        esc - back", 15, S.WIDTH / 2f, S.HEIGHT - 28f,
                     S.Col(110, 114, 124), Anchor.MidBottom);
        }

        void DrawRecords()
        {
            NavBack();
            Txt.Draw("RECORDS", 26, S.WIDTH / 2f, 112f, Ui.HOT, Anchor.MidTop, true);

            var rect = new RectI(188, 152, S.WIDTH - 376, 440);
            Ui.Panel(rect, 186f / 255f);

            int[] cols = { rect.x + 30, rect.x + 340, rect.x + 490, rect.x + 630 };
            Txt.Draw("MODE", 15, cols[0], rect.y + 18, Ui.DIM);
            Txt.Draw("SCORE", 15, cols[1], rect.y + 18, Ui.DIM);
            Txt.Draw("WAVE", 15, cols[2], rect.y + 18, Ui.DIM);
            Txt.Draw("KILLS", 15, cols[3], rect.y + 18, Ui.DIM);

            float y = rect.y + 46;
            for (int i = 0; i < Modes.All.Count; i++)
            {
                GameMode m = Modes.All[i];
                RecordEntry r = _save.Record(_save.modes, m.key);
                Txt.Draw(m.name, 20, cols[0], y, S.Col(236, 230, 222), Anchor.TopLeft, true);
                Txt.Draw(r.score.ToString(), 20, cols[1], y, Ui.HOT);
                Txt.Draw(r.wave.ToString(), 20, cols[2], y, S.Col(210, 214, 222));
                Txt.Draw(r.kills.ToString(), 20, cols[3], y, S.Col(210, 214, 222));
                y += 36f;
            }

            y += 10f;
            Painter.Line(rect.x + 24, y, rect.Right - 24, y, 1f, S.Col(60, 64, 72));
            y += 12f;

            Txt.Draw("MAP", 15, cols[0], y, Ui.DIM);
            Txt.Draw("SCORE", 15, cols[1], y, Ui.DIM);
            Txt.Draw("WAVE", 15, cols[2], y, Ui.DIM);
            y += 26f;

            for (int i = 0; i < Maps.All.Count; i++)
            {
                MapDef mp = Maps.All[i];
                RecordEntry r = _save.Record(_save.maps, mp.key);
                Txt.Draw(mp.name, 18, cols[0], y, S.Col(206, 210, 218));
                Txt.Draw(r.score.ToString(), 18, cols[1], y, S.Col(190, 180, 150));
                Txt.Draw(r.wave.ToString(), 18, cols[2], y, S.Col(180, 184, 192));
                y += 26f;
            }

            Txt.Draw("click BACK or press esc", 15, S.WIDTH / 2f, S.HEIGHT - 28f,
                     S.Col(110, 114, 124), Anchor.MidBottom);
        }

        static readonly string[,] ControlRows =
        {
            { "WASD / Arrows", "move" },
            { "Mouse", "aim" },
            { "Left click / hold", "shoot" },
            { "R", "reload" },
            { "1-4 / Q,E / wheel", "switch weapon" },
            { "Space", "dash (brief invulnerability)" },
            { "F", "flashlight on / off" },
            { "P / Esc", "pause" },
            { "M", "mute" },
            { "F11", "fullscreen" }
        };

        void DrawControls()
        {
            NavBack();
            Txt.Draw("CONTROLS", 26, S.WIDTH / 2f, 112f, Ui.HOT, Anchor.MidTop, true);

            var rect = new RectI(320, 168, S.WIDTH - 640, 396);
            Ui.Panel(rect, 186f / 255f);

            float y = rect.y + 28;
            for (int i = 0; i < ControlRows.GetLength(0); i++)
            {
                Txt.Draw(ControlRows[i, 0], 20, rect.x + 34, y, Ui.HOT, Anchor.TopLeft, true);
                Txt.Draw(ControlRows[i, 1], 20, rect.x + 270, y, S.Col(220, 224, 230));
                y += 36f;
            }

            Txt.Draw("click BACK or press esc", 15, S.WIDTH / 2f, S.HEIGHT - 28f,
                     S.Col(110, 114, 124), Anchor.MidBottom);
        }
    }
}
