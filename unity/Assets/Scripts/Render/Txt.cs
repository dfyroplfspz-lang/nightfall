// C# / Assets/Scripts/Render/Txt.cs / Unity 2022.3+ / IMGUI text in the virtual 1280x720 space
using System.Collections.Generic;
using UnityEngine;

namespace Nightfall
{
    /// <summary>Anchors mirror pygame's Rect attribute names so layout code ports literally.</summary>
    public enum Anchor
    {
        TopLeft, TopRight, MidTop, Center, MidLeft, MidRight,
        MidBottom, BottomLeft, BottomRight
    }

    /// <summary>
    /// Shapes go through GL during the camera's render, text through IMGUI afterwards.
    /// To keep the drawing code in one place, Draw() queues a label while the shapes
    /// are being emitted and Flush() paints the whole queue in OnGUI, so text always
    /// lands on top of the panel it belongs to.
    /// </summary>
    public static class Txt
    {
        struct Item
        {
            public string text;
            public int size;
            public float x, y;
            public Color color;
            public Anchor anchor;
            public bool bold;
            public float shadow;
        }

        static Font _font;
        static readonly Dictionary<int, GUIStyle> _styles = new Dictionary<int, GUIStyle>();
        static readonly GUIContent _content = new GUIContent();
        static readonly List<Item> _queue = new List<Item>(256);

        public static Font TheFont
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    if (_font == null) _font = Font.CreateDynamicFontFromOSFont("Arial", 16);
                }
                return _font;
            }
        }

        public static GUIStyle Style(int size, bool bold)
        {
            int key = size * 2 + (bold ? 1 : 0);
            GUIStyle st;
            if (_styles.TryGetValue(key, out st)) return st;

            st = new GUIStyle
            {
                font = TheFont,
                fontSize = size,
                fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                alignment = TextAnchor.UpperLeft,
                richText = false,
                wordWrap = false,
                clipping = TextClipping.Overflow
            };
            st.normal.textColor = Color.white;
            _styles[key] = st;
            return st;
        }

        public static Vector2 Measure(string text, int size, bool bold)
        {
            if (string.IsNullOrEmpty(text)) return Vector2.zero;
            _content.text = text;
            return Style(size, bold).CalcSize(_content);
        }

        public static float Width(string text, int size, bool bold)
        {
            return Measure(text, size, bold).x;
        }

        static Vector2 Offset(Vector2 sz, Anchor a)
        {
            switch (a)
            {
                case Anchor.TopRight: return new Vector2(-sz.x, 0f);
                case Anchor.MidTop: return new Vector2(-sz.x * 0.5f, 0f);
                case Anchor.Center: return new Vector2(-sz.x * 0.5f, -sz.y * 0.5f);
                case Anchor.MidLeft: return new Vector2(0f, -sz.y * 0.5f);
                case Anchor.MidRight: return new Vector2(-sz.x, -sz.y * 0.5f);
                case Anchor.MidBottom: return new Vector2(-sz.x * 0.5f, -sz.y);
                case Anchor.BottomLeft: return new Vector2(0f, -sz.y);
                case Anchor.BottomRight: return new Vector2(-sz.x, -sz.y);
                default: return Vector2.zero;
            }
        }

        /// <summary>Queue one label. Nothing is painted until Flush().</summary>
        public static void Draw(string text, int size, float x, float y, Color color,
                                Anchor anchor = Anchor.TopLeft, bool bold = false,
                                float alpha = 1f, float shadow = 0f)
        {
            if (string.IsNullOrEmpty(text) || alpha <= 0.004f) return;
            color.a *= Mathf.Clamp01(alpha);
            _queue.Add(new Item
            {
                text = text, size = size, x = x, y = y,
                color = color, anchor = anchor, bold = bold, shadow = shadow
            });
        }

        public static void DrawShadowed(string text, int size, float x, float y, Color color,
                                        Anchor anchor = Anchor.TopLeft, bool bold = false,
                                        float alpha = 1f, float shadow = 3f)
        {
            Draw(text, size, x, y, color, anchor, bold, alpha, shadow);
        }

        /// <summary>Throw away everything queued so far — used before a full-screen overlay.</summary>
        public static void DropPending() { _queue.Clear(); }

        public static void Flush()
        {
            if (_queue.Count == 0) return;
            Color old = GUI.color;

            for (int i = 0; i < _queue.Count; i++)
            {
                Item it = _queue[i];
                GUIStyle st = Style(it.size, it.bold);
                _content.text = it.text;
                Vector2 sz = st.CalcSize(_content);
                Vector2 off = Offset(sz, it.anchor);

                if (it.shadow > 0f)
                {
                    Color sh = new Color(0f, 0f, 0f, it.color.a);
                    GUI.color = sh;
                    st.normal.textColor = sh;
                    GUI.Label(new Rect(it.x + off.x + it.shadow, it.y + off.y + it.shadow,
                                       sz.x + 4f, sz.y + 2f), _content, st);
                }

                GUI.color = it.color;
                st.normal.textColor = it.color;
                GUI.Label(new Rect(it.x + off.x, it.y + off.y, sz.x + 4f, sz.y + 2f), _content, st);
            }

            GUI.color = old;
            _queue.Clear();
        }
    }
}
