using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class ShaderDIYStyles
    {
        static GUIStyle _panel;
        static GUIStyle _header;
        static GUIStyle _previewTitle;
        static GUIStyle _tiny;
        static GUIStyle _error;
        static GUIStyle _headerBar;
        static GUIStyle _rowLabel;
        static GUIStyle _iconButton;
        static Texture _settingsIcon;
        static bool _init;
        static Color _line;
        static Color _headerBg;
        static Color _rowBg;
        static Color _footerBg;

        public static Color PanelBg = new Color(0.17f, 0.17f, 0.17f, 1f);
        public static Color InnerBg = new Color(0.2f, 0.2f, 0.2f, 1f);

        public static GUIStyle Panel
        {
            get { Init(); return _panel; }
        }

        public static GUIStyle Header
        {
            get { Init(); return _header; }
        }

        public static GUIStyle PreviewTitle
        {
            get { Init(); return _previewTitle; }
        }

        public static GUIStyle Tiny
        {
            get { Init(); return _tiny; }
        }

        public static GUIStyle Error
        {
            get { Init(); return _error; }
        }

        public static GUIStyle HeaderBar
        {
            get { Init(); return _headerBar; }
        }

        public static GUIStyle RowLabel
        {
            get { Init(); return _rowLabel; }
        }

        public static GUIStyle IconButton
        {
            get { Init(); return _iconButton; }
        }

        public static Texture SettingsIcon
        {
            get { Init(); return _settingsIcon; }
        }

        public static Color HeaderBg
        {
            get { Init(); return _headerBg; }
        }

        public static Color RowBg
        {
            get { Init(); return _rowBg; }
        }

        public static Color FooterBg
        {
            get { Init(); return _footerBg; }
        }

        static void Init()
        {
            if (_init)
                return;
            _init = true;
            _line = new Color(0.32f, 0.32f, 0.32f, 1f);
            _headerBg = new Color(0.13f, 0.13f, 0.13f, 1f);
            _rowBg = new Color(0.22f, 0.22f, 0.22f, 1f);
            _footerBg = new Color(0.14f, 0.14f, 0.14f, 1f);
            _settingsIcon = LoadSettingsIcon();

            _panel = new GUIStyle("window")
            {
                padding = new RectOffset(6, 6, 6, 6),
                margin = new RectOffset(2, 2, 2, 2)
            };

            _header = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft
            };

            _previewTitle = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleCenter
            };

            _tiny = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true
            };

            _error = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                normal = { textColor = new Color(1f, 0.45f, 0.35f) }
            };

            _headerBar = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(8, 4, 0, 0)
            };

            _rowLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                alignment = TextAnchor.MiddleLeft,
                fontSize = 11
            };

            _iconButton = new GUIStyle("button")
            {
                padding = new RectOffset(3, 3, 3, 3),
                margin = new RectOffset(0, 0, 0, 0)
            };
        }

        static Texture LoadSettingsIcon()
        {
            var names = new[]
            {
                "d_Settings", "Settings", "d_SettingsIcon", "SettingsIcon",
                "d_MoreOptions", "_Popup", "d__Popup"
            };
            for (int i = 0; i < names.Length; i++)
            {
                var c = EditorGUIUtility.IconContent(names[i]);
                if (c != null && c.image != null)
                    return c.image;
            }
            return CreateGearTexture();
        }

        static Texture2D CreateGearTexture()
        {
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear
            };
            var clear = new Color(0, 0, 0, 0);
            var col = new Color(0.85f, 0.85f, 0.85f, 1f);
            var px = new Color[s * s];
            float cx = (s - 1) * 0.5f;
            float cy = (s - 1) * 0.5f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = x - cx;
                    float dy = y - cy;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float tooth = 0.55f * Mathf.Cos(ang * 6f);
                    bool ring = r > 3.1f && r < 6.2f + tooth;
                    bool hub = r < 2.1f;
                    px[y * s + x] = (ring && !hub) ? col : clear;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }

        public static void DrawBox(Rect r)
        {
            Init();
            EditorGUI.DrawRect(r, PanelBg);
            DrawBorder(r, _line);
        }

        public static void DrawBorder(Rect r, Color c)
        {
            EditorGUI.DrawRect(new Rect(r.x, r.y, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x, r.yMax - 1, r.width, 1), c);
            EditorGUI.DrawRect(new Rect(r.x, r.y, 1, r.height), c);
            EditorGUI.DrawRect(new Rect(r.xMax - 1, r.y, 1, r.height), c);
        }
    }
}
