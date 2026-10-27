// C# / Assets/Scripts/Gameplay/Particles.cs / Unity 2022.3+ / blood, sparks, smoke, floating text
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    public struct Particle
    {
        public Vector2 pos, vel;
        public float life, maxLife, radius, drag;
        public Color color;
        public bool shrink, glow;
    }

    public struct FloatText
    {
        public Vector2 pos, vel;
        public float life, maxLife;
        public string text;
        public Color color;
        public int size;
    }

    public class ParticleManager
    {
        const int MaxParticles = 1400;

        readonly Arena _world;
        readonly Rng _rng;
        readonly List<Particle> _items = new List<Particle>(MaxParticles);
        readonly List<FloatText> _texts = new List<FloatText>(64);

        public ParticleManager(Arena world, int seed)
        {
            _world = world;
            _rng = new Rng(seed ^ 0x7f4a7c15);
        }

        public List<FloatText> Texts { get { return _texts; } }

        public void Clear() { _items.Clear(); _texts.Clear(); }

        Vector2 RandUnit()
        {
            float a = _rng.Range(0f, MathUtil.TAU);
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        void Add(Vector2 pos, Vector2 vel, float life, float radius, Color color,
                 float drag = 3.2f, bool shrink = true, bool glow = false)
        {
            if (_items.Count >= MaxParticles) return;
            _items.Add(new Particle
            {
                pos = pos, vel = vel, life = life, maxLife = life,
                radius = radius, color = color, drag = drag, shrink = shrink, glow = glow
            });
        }

        // -------------------------------------------------------- presets
        public void Blood(Vector2 pos, Vector2? direction = null, int amount = 10,
                          float power = 200f, Color? color = null)
        {
            Color c = color ?? (Color)S.C_BLOOD;
            for (int i = 0; i < amount; i++)
            {
                Vector2 d = direction.HasValue
                    ? MathUtil.Rotate(direction.Value, _rng.Range(-45f, 45f))
                    : RandUnit();
                Add(pos, d * (_rng.Range(0.25f, 1f) * power), _rng.Range(0.25f, 0.6f),
                    _rng.Range(2.5f, 5.5f), c, 4f);
            }
        }

        public void Gore(Vector2 pos, int amount = 22, Color? color = null)
        {
            Color c = color ?? (Color)S.C_BLOOD;
            for (int i = 0; i < amount; i++)
                Add(pos, RandUnit() * _rng.Range(60f, 420f), _rng.Range(0.3f, 0.8f),
                    _rng.Range(3f, 8f), c, 3f);
            _world.Splat(pos, 10, 26f, c, 4, 12);
        }

        public void Muzzle(Vector2 pos, Vector2 direction, float power = 1f)
        {
            int n = (int)(6f * power);
            for (int i = 0; i < n; i++)
            {
                Vector2 d = MathUtil.Rotate(direction, _rng.Range(-16f, 16f));
                Add(pos, d * _rng.Range(180f, 520f) * power, _rng.Range(0.05f, 0.14f),
                    _rng.Range(2f, 5f) * power, S.C_YELLOW, 8f, true, true);
            }
            for (int i = 0; i < 3; i++)
            {
                Vector2 d = MathUtil.Rotate(direction, _rng.Range(-40f, 40f));
                Add(pos, d * _rng.Range(30f, 90f), _rng.Range(0.25f, 0.5f),
                    _rng.Range(4f, 8f), S.Col(74, 74, 78), 2f);
            }
        }

        public void Sparks(Vector2 pos, int amount = 8, Color? color = null)
        {
            Color c = color ?? S.Col(240, 214, 140);
            for (int i = 0; i < amount; i++)
                Add(pos, RandUnit() * _rng.Range(80f, 340f), _rng.Range(0.1f, 0.3f),
                    _rng.Range(1.5f, 3f), c, 5f, true, true);
        }

        public void Smoke(Vector2 pos, int amount = 6)
        {
            for (int i = 0; i < amount; i++)
                Add(pos, RandUnit() * _rng.Range(10f, 60f), _rng.Range(0.5f, 1.2f),
                    _rng.Range(5f, 12f), S.Col(62, 62, 66), 1.4f, false);
        }

        public void Explosion(Vector2 pos)
        {
            Color[] cols = { S.C_ORANGE, S.C_YELLOW, S.Col(240, 120, 50) };
            for (int i = 0; i < 46; i++)
                Add(pos, RandUnit() * _rng.Range(120f, 700f), _rng.Range(0.2f, 0.6f),
                    _rng.Range(3f, 9f), _rng.Pick(cols), 3.4f, true, true);
            Smoke(pos, 16);
        }

        public void Text(Vector2 pos, string txt, Color? color = null, int size = 20)
        {
            if (_texts.Count >= 64) _texts.RemoveAt(0);
            _texts.Add(new FloatText
            {
                pos = pos,
                vel = new Vector2(_rng.Range(-14f, 14f), -56f),
                life = 0.7f, maxLife = 0.7f,
                text = txt, color = color ?? (Color)S.C_WHITE, size = size
            });
        }

        // ----------------------------------------------------------- loop
        public void Update(float dt)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
            {
                Particle p = _items[i];
                p.life -= dt;
                if (p.life <= 0f) { _items.RemoveAt(i); continue; }
                p.vel *= Mathf.Max(0f, 1f - p.drag * dt);
                p.pos += p.vel * dt;
                _items[i] = p;
            }

            for (int i = _texts.Count - 1; i >= 0; i--)
            {
                FloatText t = _texts[i];
                t.life -= dt;
                if (t.life <= 0f) { _texts.RemoveAt(i); continue; }
                t.pos += t.vel * dt;
                t.vel.y += 42f * dt;
                _texts[i] = t;
            }
        }

        public void Draw(Vector2 cam)
        {
            float left = cam.x - 40f, top = cam.y - 40f;
            float right = cam.x + S.WIDTH + 40f, bottom = cam.y + S.HEIGHT + 40f;

            // all the glows first, then all the cores: two batches for the whole
            // system instead of a blend-mode flip per particle
            bool anyGlow = false;
            for (int i = 0; i < _items.Count; i++)
                if (_items[i].glow) { anyGlow = true; break; }

            if (anyGlow)
            {
                Painter.Flip(true);
                for (int i = 0; i < _items.Count; i++)
                {
                    Particle p = _items[i];
                    if (!p.glow) continue;
                    if (p.pos.x < left || p.pos.x > right || p.pos.y < top || p.pos.y > bottom) continue;

                    float t = Mathf.Max(0f, p.life / p.maxLife);
                    float r = p.radius * (p.shrink ? t : 1f);
                    if (r < 0.6f) continue;

                    Painter.Circle(p.pos.x - cam.x, p.pos.y - cam.y, r * 2f,
                                   S.WithA(p.color, 190f / 255f * t));
                }
                Painter.Flip(false);
            }

            for (int i = 0; i < _items.Count; i++)
            {
                Particle p = _items[i];
                if (p.pos.x < left || p.pos.x > right || p.pos.y < top || p.pos.y > bottom) continue;

                float t = Mathf.Max(0f, p.life / p.maxLife);
                float r = p.radius * (p.shrink ? t : 1f);
                if (r < 0.6f) continue;

                Painter.Circle(p.pos.x - cam.x, p.pos.y - cam.y, r, p.color);
            }
        }

        /// <summary>Floating damage numbers and pickup names, drawn in the IMGUI pass.</summary>
        public void DrawText(Vector2 cam)
        {
            for (int i = 0; i < _texts.Count; i++)
            {
                FloatText t = _texts[i];
                float a = Mathf.Min(1f, (t.life / t.maxLife) * 2.2f);
                Txt.Draw(t.text, t.size, t.pos.x - cam.x, t.pos.y - cam.y, t.color,
                         Anchor.MidTop, true, a);
            }
        }
    }
}
