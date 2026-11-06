using System.Drawing;
using System.Windows.Forms;

namespace CouchGamingNinja
{
    public static class Theme
    {
        public static readonly Color Ground = Color.FromArgb(0x0F, 0x11, 0x17);
        public static readonly Color Side = Color.FromArgb(0x15, 0x18, 0x21);
        public static readonly Color Card = Color.FromArgb(0x16, 0x1A, 0x24);
        public static readonly Color Panel = Color.FromArgb(0x1A, 0x1E, 0x29);
        public static readonly Color Line = Color.FromArgb(0x34, 0x3B, 0x4F);
        public static readonly Color Button = Color.FromArgb(0x2A, 0x31, 0x42);
        public static readonly Color Text = Color.FromArgb(0xE8, 0xEA, 0xF0);
        public static readonly Color Muted = Color.FromArgb(0x9A, 0xA1, 0xB2);
        public static readonly Color Accent = Color.FromArgb(0x7F, 0xE3, 0xB5);
        public static readonly Color Warn = Color.FromArgb(0xF5, 0xB4, 0x55);
        public static readonly Color Bad = Color.FromArgb(0xF0, 0x6A, 0x8A);

        public static Font Ui(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font("Segoe UI", size, style, GraphicsUnit.Point);
        }

        public static Font Head(float size)
        {
            return new Font("Segoe UI Semibold", size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Button MakeButton(string text, bool primary)
        {
            var b = new Button
            {
                Text = text,
                FlatStyle = FlatStyle.Flat,
                BackColor = primary ? Accent : Button,
                ForeColor = primary ? Ground : Text,
                Font = primary ? Head(11) : Ui(10.5f),
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(14, 8, 14, 8),
                Cursor = Cursors.Hand,
                UseVisualStyleBackColor = false
            };
            b.FlatAppearance.BorderSize = primary ? 0 : 1;
            b.FlatAppearance.BorderColor = Line;
            return b;
        }

        public static Label MakeLabel(string text, float size, Color color, bool bold = false)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                ForeColor = color,
                BackColor = Color.Transparent,
                UseMnemonic = false,
                Font = bold ? Head(size) : Ui(size)
            };
        }

        public static Color CoverColor(string key)
        {
            var palette = new[] { Color.FromArgb(0x2B, 0x3F, 0x6B), Color.FromArgb(0x5B, 0x2B, 0x6B), Color.FromArgb(0x4A, 0x3A, 0x24), Color.FromArgb(0x24, 0x34, 0x3A), Color.FromArgb(0x3A, 0x24, 0x2E), Color.FromArgb(0x24, 0x3A, 0x2C) };
            int h = 0;
            foreach (char c in key ?? "") h = h * 31 + c;
            return palette[(h & 0x7fffffff) % palette.Length];
        }
    }
}
