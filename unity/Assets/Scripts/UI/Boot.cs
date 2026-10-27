// C# / Assets/Scripts/UI/Boot.cs / Unity 2022.3+ / studio sting, loading screen, deploy card
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    public static class Boot
    {
        public const string STUDIO = S.STUDIO;
        public const string STUDIO_SUB = "presents";

        public static readonly string[] Tips =
        {
            "the torch is the only thing that shows you a zombie",
            "F kills the light - sometimes that is the smart move",
            "dash goes through a bite, not through a wall",
            "shotgun pellets spread wide: let them get close",
            "cases drop from zombies and after every fifth wave",
            "duplicate skins turn into scrap, scrap buys cases",
            "brutes shrug off knockback - don't try to kite them in a corner",
            "the rifle punches through two bodies in a line",
            "runners are fast but thin - the pistol is enough",
            "adrenaline stacks with sprint: use it to reposition, not to fight"
        };

        /// <summary>Studio badge: heavy BG monogram, red slash, drawn from scratch.</summary>
        public static void DrawMark(float cx, float cy, float scale, float alpha, float grow = 1f)
        {
            float r = 220f * scale * grow;
            float x = cx - r * 0.5f;
            float y = cy - r * 0.5f;
            float rad = r * 0.22f;

            Painter.RoundRect(x, y, r, r, rad, new Color32(22, 23, 28, (byte)(255 * alpha)));
            Painter.RoundRect(x + r * 0.04f, y + r * 0.04f, r * 0.92f, r * 0.46f, r * 0.18f,
                              new Color32(38, 40, 48, (byte)(255 * alpha)));

            // the red slashes, clipped to the badge by simply keeping them inside it
            Color slash = S.WithA(S.C_RED, 0.95f * alpha);
            Painter.Line(x + r * 0.85f, y + r, x + r * 0.98f, y, r * 0.10f, slash);
            Painter.Line(x + r * 0.685f, y + r, x + r * 0.845f, y,
                         r * 0.05f, S.WithA(S.C_RED, 0.55f * alpha));

            Painter.RoundRectOutline(x, y, r, r, rad, Mathf.Max(3f, r * 0.035f),
                                     S.WithA(S.C_RED, alpha));

            Txt.Draw("BG", Mathf.Max(8, (int)(r * 0.45f)), cx, y + r * 0.46f,
                     S.Col(240, 238, 234), Anchor.Center, true, alpha);
            Txt.Draw("STUDIO", Mathf.Max(6, (int)(r * 0.10f)), cx, y + r * 0.78f,
                     S.Col(150, 154, 164), Anchor.Center, true, alpha);
        }

        // ---------------------------------------------------------- splash
        public class Splash
        {
            public const float DURATION = 3.1f;
            public float t;
            public bool done;
            bool _played;
            readonly ProcAudio _audio;

            public Splash(ProcAudio audio) { _audio = audio; }

            public void Skip() { done = true; }

            public void Update(float dt)
            {
                t += dt;
                if (t > 0.45f && !_played && _audio != null)
                {
                    _played = true;
                    _audio.Play("wave", 0.35f);
                }
                if (t >= DURATION) done = true;
            }

            public void Draw()
            {
                Painter.Rect(0f, 0f, S.WIDTH, S.HEIGHT, S.Col(6, 6, 8));

                float fadeIn = Mathf.Min(1f, t / 0.9f);
                float fadeOut = t < DURATION - 0.7f ? 1f : Mathf.Max(0f, (DURATION - t) / 0.7f);
                float a = fadeIn * fadeOut;

                float cx = S.WIDTH * 0.5f;
                float cy = S.HEIGHT * 0.5f - 30f;

                Painter.Flip(true);
                Painter.RadialBlob(cx, cy - 10f, 320f, new Color(1f, 0.85f, 0.75f, 60f / 255f * a));
                Painter.Flip(false);

                float grow = 0.86f + 0.14f * Mathf.Min(1f, t / 0.8f);
                DrawMark(cx, cy - 30f, 1f, a, grow);

                Txt.Draw(STUDIO, 44, cx, cy + 92f, S.Col(236, 232, 226), Anchor.MidTop, true, a);
                Txt.Draw(STUDIO_SUB, 17, cx, cy + 144f, S.Col(128, 124, 120), Anchor.MidTop, false, a);

                float w = 200f * Mathf.Clamp01((t - 0.5f) / 1.1f) * a;
                Painter.Rect(cx - w * 0.5f, cy + 178f, w, 2f, S.Col(150, 36, 32));

                if (t > 1.4f)
                    Txt.Draw("click to skip", 14, cx, S.HEIGHT - 40f,
                             S.Col(70, 70, 76), Anchor.MidBottom);
            }
        }

        // ---------------------------------------------------------- loader
        public class Step
        {
            public string label;
            public Action run;
            public Step(string label, Action run) { this.label = label; this.run = run; }
        }

        /// <summary>Runs one task per frame so the bar and the mascot keep animating.</summary>
        public class Loader
        {
            readonly List<Step> _steps;
            readonly Rng _rng;
            readonly float[] _fog;      // x, y, r, speed per blob
            public int index;
            public float t;
            public string label;
            public string tip;
            float _shown;

            public Loader(List<Step> steps, int seed = 11)
            {
                _steps = steps;
                _rng = new Rng(seed);
                label = steps.Count > 0 ? steps[0].label : "ready";
                tip = Tips[(int)(_rng.NextUInt() % (uint)Tips.Length)];

                _fog = new float[12 * 4];
                for (int i = 0; i < 12; i++)
                {
                    _fog[i * 4 + 0] = _rng.Range(-200f, S.WIDTH + 200f);
                    _fog[i * 4 + 1] = _rng.Range(0f, S.HEIGHT);
                    _fog[i * 4 + 2] = _rng.Range(90f, 240f);
                    _fog[i * 4 + 3] = _rng.Range(8f, 26f);
                }
            }

            public bool Done { get { return index >= _steps.Count && _shown > 0.35f; } }

            public float Progress
            {
                get { return _steps.Count == 0 ? 1f : Mathf.Min(1f, index / (float)_steps.Count); }
            }

            /// <summary>Execute at most one pending task.</summary>
            public void RunOne()
            {
                if (index >= _steps.Count) return;
                Step s = _steps[index];
                label = s.label;
                try { if (s.run != null) s.run(); }
                catch (Exception e) { Debug.LogWarning("loader step failed: " + e.Message); }
                index++;
                label = index < _steps.Count ? _steps[index].label : "ready";
            }

            public void Update(float dt)
            {
                t += dt;
                _shown += dt;
                for (int i = 0; i < 12; i++)
                {
                    _fog[i * 4 + 0] += _fog[i * 4 + 3] * dt;
                    if (_fog[i * 4 + 0] > S.WIDTH + 300f)
                    {
                        _fog[i * 4 + 0] = -300f;
                        _fog[i * 4 + 1] = _rng.Range(0f, S.HEIGHT);
                    }
                }
            }

            public void Draw(string title = "NIGHTFALL", string sub = null)
            {
                Painter.Rect(0f, 0f, S.WIDTH, S.HEIGHT, S.Col(10, 11, 15));
                for (int i = 0; i < 12; i++)
                    Painter.Circle(_fog[i * 4 + 0], _fog[i * 4 + 1], _fog[i * 4 + 2],
                                   new Color32(28, 22, 24, 30));

                float cx = S.WIDTH * 0.5f;
                Txt.Draw(title, 92, cx, 170f, S.C_RED, Anchor.MidTop, true);
                Txt.Draw(sub ?? STUDIO, 18, cx, 272f, S.Col(132, 136, 146), Anchor.MidTop, true);

                float bx = cx - 300f, by = 420f, bw = 600f, bh = 16f;
                Painter.RoundRect(bx, by, bw, bh, 4f, S.Col(22, 23, 28));
                float fw = bw * Progress;
                if (fw > 0f)
                {
                    Painter.RoundRect(bx, by, fw, bh, 4f, S.Col(196, 54, 48));
                    Painter.Rect(bx + fw - 3f, by, 3f, bh, S.Col(236, 120, 100));
                }
                Painter.RoundRectOutline(bx, by, bw, bh, 4f, 2f, S.Col(82, 86, 96));

                string dots = new string('.', 1 + ((int)(t * 3f)) % 3);
                Txt.Draw(label + dots, 19, bx, by - 30f, S.Col(226, 222, 216));
                Txt.Draw((int)(Progress * 100f) + "%", 19, bx + bw, by - 30f,
                         S.C_YELLOW, Anchor.TopRight, true);

                Txt.Draw(tip, 17, cx, S.HEIGHT - 90f, S.Col(140, 144, 154), Anchor.MidBottom);
                Txt.Draw("TIP", 13, cx, S.HEIGHT - 112f, S.Col(96, 100, 110), Anchor.MidBottom, true);

                // shambling loader mascot
                float mx = bx + bw * Progress;
                float my = by - 58f;
                float ph = t * 7f;
                Color col = S.Col(150, 36, 32);
                Painter.Circle(mx, my, 7f, col);
                for (int s = -1; s <= 1; s += 2)
                    Painter.Line(mx, my + 6f, mx + s * 5f + Mathf.Sin(ph) * 3f * s, my + 16f, 3f, col);
            }
        }

        /// <summary>One frame shown while the arena bakes, so the drop never looks frozen.</summary>
        public static void DrawDeploy(MapDef mdef, GameMode mode, string tip = null)
        {
            float cx = S.WIDTH * 0.5f;
            Painter.Rect(0f, 0f, S.WIDTH, S.HEIGHT, S.Col(9, 10, 13));

            Txt.Draw("DEPLOYING", 64, cx, 210f, S.C_RED, Anchor.MidTop, true);
            Txt.Draw(mdef.name, 34, cx, 300f, S.Col(240, 236, 228), Anchor.MidTop, true);
            Txt.Draw(mdef.tagline, 18, cx, 344f, S.Col(140, 144, 154), Anchor.MidTop);
            Txt.Draw(mode.name, 20, cx, 392f, S.C_YELLOW, Anchor.MidTop, true);

            float bx = cx - 220f, by = 452f, bw = 440f, bh = 10f;
            Painter.RoundRect(bx, by, bw, bh, 3f, S.Col(24, 25, 30));
            Painter.RoundRect(bx + 4f, by + 2f, bw - 8f, bh - 4f, 3f, S.Col(196, 54, 48));
            Painter.RoundRectOutline(bx, by, bw, bh, 3f, 2f, S.Col(82, 86, 96));

            Txt.Draw(tip ?? Tips[0], 17, cx, S.HEIGHT - 90f,
                     S.Col(140, 144, 154), Anchor.MidBottom);
        }
    }
}
