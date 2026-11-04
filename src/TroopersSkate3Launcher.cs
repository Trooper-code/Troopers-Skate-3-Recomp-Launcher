// Trooper's Skate 3 Recomp Launcher
// Edits %APPDATA%\skate3\settings.toml (the game's cvar file) through a GUI and launches skate3.exe.
// Build: build.cmd in this folder  (uses the .NET Framework C# compiler that ships with Windows)
// Source files: TroopersSkate3Launcher.cs (window + settings), LauncherPages.cs (tool pages), LauncherServices.cs (background services)

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Trooper's Skate 3 Recomp Launcher")]
[assembly: System.Reflection.AssemblyProduct("Trooper's Skate 3 Recomp Launcher")]
[assembly: System.Reflection.AssemblyVersion("3.1.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("3.1.1.0")]

namespace Skate3Launcher
{
    enum Kind { Bool, Choice, Slider, Text, Raw }

    class Setting
    {
        public string Key;
        public string Label;
        public string Help;
        public Kind Kind;
        public string Default;          // TOML literal used when the key is missing from the file
        public string[] ChoiceValues;   // TOML literals
        public string[] ChoiceLabels;
        public double Min, Max, Step;
        public string Format = "0.##";
        public string Suffix = "";
        public string ZeroLabel;        // slider label shown at 0 (e.g. "Off")
        public string[] Mirrors;        // extra keys always written with the same value
        public bool IsFloat;

        // UI
        public Control Editor;          // the control holding the value (TextBox for Kind.Text)
        public Control Host;            // the control placed on the card
        public Label ValueLabel;
        public Card Card;
        public Label NameLabel;
        public Label WarnLabel;
        public PillButton ResetButton;
        public string Warning;          // current conflict warning shown under the help text
        public bool IsRaw;              // a key from settings.toml the launcher has no dedicated editor for (Advanced page)
    }

    class Category
    {
        public string Name;
        public string Blurb;
        public string Glyph;
        public List<Setting> Settings = new List<Setting>();
    }

    static class Toml
    {
        public static string Bool(bool b) { return b ? "true" : "false"; }

        public static string Float(double d)
        {
            string s = Math.Round(d, 4).ToString("0.0###", CultureInfo.InvariantCulture);
            return s;
        }

        public static string Int(long i) { return i.ToString(CultureInfo.InvariantCulture); }

        public static string Str(string s)
        {
            if (s.IndexOf('\'') < 0 && s.IndexOf('\n') < 0) return "'" + s + "'";
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        public static string Unquote(string lit)
        {
            lit = lit.Trim();
            if (lit.Length >= 2 && lit[0] == '\'' && lit[lit.Length - 1] == '\'') return lit.Substring(1, lit.Length - 2);
            if (lit.Length >= 2 && lit[0] == '"' && lit[lit.Length - 1] == '"')
                return lit.Substring(1, lit.Length - 2).Replace("\\\"", "\"").Replace("\\\\", "\\");
            return lit;
        }

        // True when the text is already a TOML literal (number, bool, quoted string, array or inline table).
        public static bool IsLiteral(string t)
        {
            t = t.Trim();
            if (t == "true" || t == "false") return true;
            double d;
            if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return true;
            if (t.Length >= 2 && ((t[0] == '\'' && t[t.Length - 1] == '\'') || (t[0] == '"' && t[t.Length - 1] == '"'))) return true;
            if (t.Length >= 2 && ((t[0] == '[' && t[t.Length - 1] == ']') || (t[0] == '{' && t[t.Length - 1] == '}'))) return true;
            return false;
        }

        public static double Num(string lit, double fallback)
        {
            double d;
            if (double.TryParse(lit.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out d)) return d;
            return fallback;
        }

        // Normalizes a literal so "4" and "4.0" or "'auto'" and "\"auto\"" compare equal.
        public static string Norm(string lit)
        {
            lit = (lit ?? "").Trim();
            double d;
            if (double.TryParse(lit, NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                return d.ToString("R", CultureInfo.InvariantCulture);
            if (lit.StartsWith("'") || lit.StartsWith("\"")) return "s:" + Unquote(lit);
            return lit.ToLowerInvariant();
        }
    }

    class ConfigFile
    {
        static readonly Regex LineRx = new Regex(@"^\s*([A-Za-z0-9_]+)\s*=\s*(.*?)\s*$");

        public string Path;
        public List<string> Lines = new List<string>();
        public Dictionary<string, string> Values = new Dictionary<string, string>();
        public string NewLine = "\n";

        public ConfigFile(string path) { Path = path; }

        public void Load()
        {
            Lines.Clear();
            Values.Clear();
            if (!File.Exists(Path)) return;
            string text = File.ReadAllText(Path);
            NewLine = text.Contains("\r\n") ? "\r\n" : "\n";
            Lines.AddRange(text.Replace("\r\n", "\n").Split('\n'));
            if (Lines.Count > 0 && Lines[Lines.Count - 1] == "") Lines.RemoveAt(Lines.Count - 1);
            foreach (string line in Lines)
            {
                Match m = LineRx.Match(line);
                if (m.Success && !line.TrimStart().StartsWith("#")) Values[m.Groups[1].Value] = m.Groups[2].Value;
            }
        }

        // Rewrites only the given keys in place; unknown keys and comments are kept untouched.
        public void Save(Dictionary<string, string> updates)
        {
            var pending = new Dictionary<string, string>(updates);
            var output = new List<string>();
            foreach (string line in Lines)
            {
                Match m = LineRx.Match(line);
                if (m.Success && !line.TrimStart().StartsWith("#") && pending.ContainsKey(m.Groups[1].Value))
                {
                    string key = m.Groups[1].Value;
                    output.Add(key + " = " + pending[key]);
                    pending.Remove(key);
                }
                else output.Add(line);
            }
            if (output.Count == 0) output.Add("# Auto-generated cvar configuration");
            foreach (var kv in pending.OrderBy(k => k.Key)) output.Add(kv.Key + " = " + kv.Value);

            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
            string tmp = Path + ".tmp";
            File.WriteAllText(tmp, string.Join(NewLine, output) + NewLine, new UTF8Encoding(false));
            if (File.Exists(Path)) File.Replace(tmp, Path, null);
            else File.Move(tmp, Path);
            Load();
        }
    }

    // ====================================================================== look & feel

    static class Dpi
    {
        public static float F = 1f;
        public static int S(int v) { return (int)Math.Round(v * F); }
        public static float Sf(float v) { return v * F; }
    }

    static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(13, 14, 18);
        public static readonly Color Card = Color.FromArgb(23, 25, 32);
        public static readonly Color CardBorder = Color.FromArgb(35, 38, 47);
        public static readonly Color NavHover = Color.FromArgb(22, 24, 31);
        public static readonly Color NavSel = Color.FromArgb(29, 31, 40);
        public static readonly Color Input = Color.FromArgb(33, 36, 45);
        public static readonly Color InputHover = Color.FromArgb(42, 46, 57);
        public static readonly Color Border = Color.FromArgb(52, 56, 68);
        public static readonly Color Raised = Color.FromArgb(60, 64, 78);
        public static readonly Color Text = Color.FromArgb(242, 243, 247);
        public static readonly Color Muted = Color.FromArgb(142, 148, 164);
        public static readonly Color Subtle = Color.FromArgb(94, 100, 116);
        public static readonly Color Accent = Color.FromArgb(255, 92, 53);
        public static readonly Color AccentHover = Color.FromArgb(255, 116, 82);
        public static readonly Color AccentPress = Color.FromArgb(222, 72, 40);
        public static readonly Color AccentDeep = Color.FromArgb(240, 56, 78);   // gradient end
        public static readonly Color AccentDim = Color.FromArgb(70, 36, 32);
        public static readonly Color Good = Color.FromArgb(74, 222, 128);
        public static readonly Color Warn = Color.FromArgb(251, 191, 36);
        public static readonly Color Error = Color.FromArgb(248, 113, 113);

        public static readonly string IconFace = FontExists("Segoe Fluent Icons") ? "Segoe Fluent Icons" : "Segoe MDL2 Assets";
        // False on systems without the Windows icon fonts (e.g. Wine/Mono): icons are then left out instead of drawing empty boxes.
        public static readonly bool HasIcons = FontExists("Segoe Fluent Icons") || FontExists("Segoe MDL2 Assets");

        public static readonly Font Base = new Font("Segoe UI", 9.75f);
        public static readonly Font Bold = new Font("Segoe UI Semibold", 10.25f);
        public static readonly Font Small = new Font("Segoe UI", 9f);
        public static readonly Font Caption = new Font("Segoe UI Semibold", 7.75f);
        public static readonly Font Brand = new Font("Segoe UI Black", 22f);
        public static readonly Font PageTitle = new Font("Segoe UI Semibold", 19f);
        public static readonly Font Nav = new Font("Segoe UI Semibold", 10.25f);
        public static readonly Font Button = new Font("Segoe UI Semibold", 9.75f);
        public static readonly Font Play = new Font("Segoe UI Black", 13f);
        public static readonly Font IconSm = new Font(IconFace, 10f);
        public static readonly Font IconMd = new Font(IconFace, 12f);
        public static readonly Font IconXs = new Font(IconFace, 7.5f);
        public static readonly Font Mono = new Font(FontExists("Cascadia Mono") ? "Cascadia Mono" : "Consolas", 9.5f);

        static bool FontExists(string name)
        {
            try { return FontFamily.Families.Any(ff => ff.Name.Equals(name, StringComparison.OrdinalIgnoreCase)); }
            catch { return false; }
        }
    }

    static class Gfx
    {
        public const TextFormatFlags Flat = TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix;

        public static GraphicsPath Round(RectangleF r, float radius)
        {
            var p = new GraphicsPath();
            float d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            if (d <= 0.5f) { p.AddRectangle(r); return p; }
            p.AddArc(r.X, r.Y, d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            p.CloseFigure();
            return p;
        }

        public static void FillRound(Graphics g, Brush b, RectangleF r, float radius)
        {
            using (var p = Round(r, radius)) g.FillPath(b, p);
        }

        public static void FillRound(Graphics g, Color c, RectangleF r, float radius)
        {
            using (var b = new SolidBrush(c)) FillRound(g, b, r, radius);
        }

        public static void DrawRound(Graphics g, Color c, RectangleF r, float radius, float width)
        {
            using (var p = Round(r, radius))
            using (var pen = new Pen(c, width)) g.DrawPath(pen, p);
        }

        public static Color Mix(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t), (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        public static Color ParentBack(Control c)
        {
            return c.Parent != null ? c.Parent.BackColor : Theme.Bg;
        }

        // Letter-spaced caps text, used for small section captions.
        public static int SpacedWidth(string s, Font f, int spacing)
        {
            int w = 0;
            foreach (char ch in s) w += CharWidth(ch, f) + spacing;
            return Math.Max(0, w - spacing);
        }

        public static void DrawSpaced(Graphics g, string s, Font f, Color c, int x, int y, int spacing)
        {
            foreach (char ch in s)
            {
                if (ch != ' ') TextRenderer.DrawText(g, ch.ToString(), f, new Point(x, y), c, Flat);
                x += CharWidth(ch, f) + spacing;
            }
        }

        static int CharWidth(char ch, Font f)
        {
            // true advance width: GDI pads single-glyph measurements, so measure the glyph twice and subtract
            string one = ch == ' ' ? "n" : ch.ToString();
            int adv = TextRenderer.MeasureText(one + one, f, Size.Empty, Flat).Width - TextRenderer.MeasureText(one, f, Size.Empty, Flat).Width;
            return ch == ' ' ? adv * 2 / 3 : adv;
        }
    }

    static class Native
    {
        [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
        [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd, string app, string idList);
        [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        public const int WM_MOUSEWHEEL = 0x020A;

        // Dark, seamless title bar on Windows 10/11 (ignored on older systems).
        public static void StyleTitleBar(Form f)
        {
            try
            {
                int on = 1;
                if (DwmSetWindowAttribute(f.Handle, 20, ref on, 4) != 0) DwmSetWindowAttribute(f.Handle, 19, ref on, 4);
                int caption = ColorRef(Theme.Bg), border = ColorRef(Theme.CardBorder), text = ColorRef(Theme.Muted);
                DwmSetWindowAttribute(f.Handle, 35, ref caption, 4);
                DwmSetWindowAttribute(f.Handle, 34, ref border, 4);
                DwmSetWindowAttribute(f.Handle, 36, ref text, 4);
            }
            catch { }
        }

        public static void DarkTheme(Control c, string theme)
        {
            try { SetWindowTheme(c.Handle, theme, null); } catch { }
        }

        static int ColorRef(Color c) { return c.R | (c.G << 8) | (c.B << 16); }
    }

    // ====================================================================== controls

    // Label that word-wraps to MaximumSize.Width. Windows does this natively; Mono's Label doesn't, so it is measured and drawn by hand there.
    class TextLabel : Label
    {
        static readonly bool OnMono = Type.GetType("Mono.Runtime") != null;
        const TextFormatFlags Wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix | TextFormatFlags.TextBoxControl;

        public override Size GetPreferredSize(Size proposed)
        {
            if (!OnMono || MaximumSize.Width <= 0) return base.GetPreferredSize(proposed);
            if (Text.Length == 0) return new Size(0, 0);
            var sz = TextRenderer.MeasureText(Text, Font, new Size(MaximumSize.Width, int.MaxValue), Wrap);
            return new Size(Math.Min(sz.Width + 2, MaximumSize.Width), sz.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!OnMono) { base.OnPaint(e); return; }
            e.Graphics.Clear(Parent != null ? Parent.BackColor : BackColor);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor, Wrap);
        }
    }

    // Base for owner-painted controls: double buffered, repaints on resize.
    class PaintedControl : Control
    {
        public PaintedControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint |
                     ControlStyles.ResizeRedraw | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnTextChanged(EventArgs e) { Invalidate(); base.OnTextChanged(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
    }

    // Scrolling page with dark scrollbars that doesn't jump when a child takes focus.
    class ScrollPanel : Panel
    {
        public ScrollPanel() { DoubleBuffered = true; AutoScroll = true; }
        protected override Point ScrollToControl(Control activeControl) { return DisplayRectangle.Location; }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.DarkTheme(this, "DarkMode_Explorer"); }
    }

    // Rounded setting card. Shows an accent strip on the left while its value is unsaved.
    class Card : Panel
    {
        bool changed;
        public Label Help;
        public Setting Setting;

        public Card()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Theme.Card;
        }

        public bool Changed
        {
            get { return changed; }
            set { if (changed != value) { changed = value; Invalidate(); } }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Gfx.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float rad = Dpi.Sf(10);
            Gfx.FillRound(g, Theme.Card, r, rad);
            Gfx.DrawRound(g, changed ? Gfx.Mix(Theme.CardBorder, Theme.Accent, 0.35f) : Theme.CardBorder, r, rad, 1f);
            if (changed) Gfx.FillRound(g, Theme.Accent, new RectangleF(Dpi.Sf(1), Dpi.S(16), Dpi.Sf(3), Height - Dpi.S(32)), Dpi.Sf(1.5f));
        }
    }

    // On/off switch with a short slide animation.
    class ToggleSwitch : PaintedControl
    {
        bool isChecked, hover;
        float pos;
        readonly Timer anim = new Timer { Interval = 15 };
        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            Size = new Size(Dpi.S(44), Dpi.S(24));
            Cursor = Cursors.Hand;
            TabStop = true;
            anim.Tick += delegate
            {
                float target = isChecked ? 1f : 0f;
                pos += (target - pos) * 0.35f;
                if (Math.Abs(target - pos) < 0.02f) { pos = target; anim.Stop(); }
                Invalidate();
            };
        }

        public bool Checked
        {
            get { return isChecked; }
            set
            {
                if (isChecked == value) return;
                isChecked = value;
                if (IsHandleCreated && Visible) anim.Start();
                else pos = value ? 1f : 0f;
                Invalidate();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        protected override void OnClick(EventArgs e) { Focus(); Checked = !Checked; base.OnClick(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) Checked = !Checked;
            base.OnKeyDown(e);
        }

        protected override void Dispose(bool disposing) { if (disposing) anim.Dispose(); base.Dispose(disposing); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(1f, 1f, Width - 2f, Height - 2f);
            float rad = r.Height / 2;
            Color offFill = hover ? Theme.InputHover : Theme.Input;
            Color onFill = hover ? Theme.AccentHover : Theme.Accent;
            Gfx.FillRound(g, Gfx.Mix(offFill, onFill, pos), r, rad);
            if (pos < 1f) Gfx.DrawRound(g, Gfx.Mix(Theme.Border, onFill, pos), r, rad, 1f);
            if (Focused && ShowFocusCues) Gfx.DrawRound(g, Theme.Text, new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f), rad, 1f);

            float knob = r.Height - Dpi.Sf(8) + (hover ? Dpi.Sf(1) : 0);
            float x0 = r.X + (r.Height - knob) / 2, x1 = r.Right - (r.Height + knob) / 2;
            float x = x0 + (x1 - x0) * pos;
            using (var b = new SolidBrush(Gfx.Mix(Theme.Muted, Color.White, pos)))
                g.FillEllipse(b, x, r.Y + (r.Height - knob) / 2, knob, knob);
        }
    }

    // Rounded slider that replaces the system TrackBar. Mouse wheel scrolls the page instead of changing the value.
    class Slider : PaintedControl
    {
        int min, max = 10, val;
        bool hover, drag;
        public int SmallChange = 1, LargeChange = 1;
        public event EventHandler ValueChanged;

        public Slider()
        {
            Height = Dpi.S(28);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public int Minimum { get { return min; } set { min = value; Value = val; Invalidate(); } }
        public int Maximum { get { return max; } set { max = value; Value = val; Invalidate(); } }

        public int Value
        {
            get { return val; }
            set
            {
                int v = Math.Max(min, Math.Min(max, value));
                if (v == val) return;
                val = v;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }

        int Pad { get { return Dpi.S(10); } }

        void SetFromX(int x)
        {
            float t = (x - Pad) / (float)Math.Max(1, Width - Pad * 2);
            Value = min + (int)Math.Round(Math.Max(0f, Math.Min(1f, t)) * (max - min));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { Focus(); drag = true; Capture = true; SetFromX(e.X); Invalidate(); }
            base.OnMouseDown(e);
        }

        protected override void OnMouseMove(MouseEventArgs e) { if (drag) SetFromX(e.X); base.OnMouseMove(e); }
        protected override void OnMouseUp(MouseEventArgs e) { drag = false; Capture = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData)
            {
                case Keys.Left: case Keys.Right: case Keys.Up: case Keys.Down: return true;
            }
            return base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Left: case Keys.Down: Value -= SmallChange; break;
                case Keys.Right: case Keys.Up: Value += SmallChange; break;
                case Keys.PageDown: Value -= LargeChange; break;
                case Keys.PageUp: Value += LargeChange; break;
                case Keys.Home: Value = min; break;
                case Keys.End: Value = max; break;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float cy = Height / 2f, th = Dpi.Sf(4);
            float left = Pad, right = Width - Pad;
            float frac = max > min ? (val - min) / (float)(max - min) : 0f;
            float x = left + (right - left) * frac;

            Gfx.FillRound(g, Theme.Input, new RectangleF(left, cy - th / 2, right - left, th), th / 2);
            if (x - left > 0.5f)
            {
                var fill = new RectangleF(left, cy - th / 2, x - left, th);
                using (var b = new LinearGradientBrush(new RectangleF(left - 1, 0, right - left + 2, 1), Theme.Accent, Theme.AccentDeep, 0f))
                    Gfx.FillRound(g, b, fill, th / 2);
            }

            bool hot = hover || drag || Focused;
            float outer = Dpi.Sf(hot ? 9.5f : 9f), inner = Dpi.Sf(drag ? 3.5f : hot ? 5f : 4f);
            using (var b = new SolidBrush(Color.FromArgb(70, 0, 0, 0))) g.FillEllipse(b, x - outer, cy - outer + 1, outer * 2, outer * 2);
            using (var b = new SolidBrush(Color.FromArgb(246, 246, 250))) g.FillEllipse(b, x - outer, cy - outer, outer * 2, outer * 2);
            using (var b = new SolidBrush(Theme.Accent)) g.FillEllipse(b, x - inner, cy - inner, inner * 2, inner * 2);
            if (Focused && ShowFocusCues)
                using (var p = new Pen(Theme.Text, 1f)) g.DrawEllipse(p, x - outer - 2, cy - outer - 2, outer * 2 + 4, outer * 2 + 4);
        }
    }

    // Fully owner-painted drop-down list so both the closed box and the list stay dark and rounded.
    class DarkCombo : ComboBox
    {
        bool hover;

        public DarkCombo()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            DrawMode = DrawMode.OwnerDrawFixed;
            FlatStyle = FlatStyle.Flat;
            ItemHeight = Dpi.S(26);
            BackColor = Theme.Input;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.DarkTheme(this, "DarkMode_CFD"); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnDropDown(EventArgs e) { Invalidate(); base.OnDropDown(e); }
        protected override void OnDropDownClosed(EventArgs e) { Invalidate(); base.OnDropDownClosed(e); }
        protected override void OnSelectedIndexChanged(EventArgs e) { Invalidate(); base.OnSelectedIndexChanged(e); }

        // Don't change values while scrolling the page: hand the wheel to the page instead.
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Native.WM_MOUSEWHEEL && !DroppedDown)
            {
                Control p = Parent;
                while (p != null && !(p is ScrollPanel)) p = p.Parent;
                if (p != null) Native.SendMessage(p.Handle, m.Msg, m.WParam, m.LParam);
                return;
            }
            base.WndProc(ref m);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Gfx.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float rad = Dpi.Sf(6);
            Gfx.FillRound(g, hover || DroppedDown ? Theme.InputHover : Theme.Input, r, rad);
            Gfx.DrawRound(g, DroppedDown || Focused ? Theme.Accent : Theme.Border, r, rad, 1f);

            int chevronW = Dpi.S(30);
            string text = SelectedIndex >= 0 ? Items[SelectedIndex].ToString() : "";
            TextRenderer.DrawText(g, text, Font, new Rectangle(Dpi.S(12), 0, Width - Dpi.S(12) - chevronW, Height), Theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            TextRenderer.DrawText(g, Theme.HasIcons ? "" : "\u25BE", Theme.HasIcons ? Theme.IconXs : Theme.Base, new Rectangle(Width - chevronW, 0, chevronW - Dpi.S(6), Height),
                hover || DroppedDown ? Theme.Text : Theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var g = e.Graphics;
            bool hot = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(Theme.Input)) g.FillRectangle(b, e.Bounds);
            if (hot)
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                var hr = new RectangleF(e.Bounds.X + 3, e.Bounds.Y + 1, e.Bounds.Width - 6, e.Bounds.Height - 2);
                Gfx.FillRound(g, Theme.Accent, hr, Dpi.Sf(4));
            }
            TextRenderer.DrawText(g, Items[e.Index].ToString(), Font, new Rectangle(e.Bounds.X + Dpi.S(10), e.Bounds.Y, e.Bounds.Width - Dpi.S(10), e.Bounds.Height),
                hot ? Color.White : Theme.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }
    }

    // Borderless TextBox inside a rounded frame.
    class RoundTextBox : PaintedControl
    {
        public readonly TextBox Box;

        public RoundTextBox()
        {
            Box = new TextBox { BorderStyle = BorderStyle.None, BackColor = Theme.Input, ForeColor = Theme.Text, Font = Theme.Base };
            Box.GotFocus += delegate { Invalidate(); };
            Box.LostFocus += delegate { Invalidate(); };
            Controls.Add(Box);
            Height = Dpi.S(32);
            Cursor = Cursors.IBeam;
        }

        protected override void OnClick(EventArgs e) { Box.Focus(); base.OnClick(e); }

        protected override void OnLayout(LayoutEventArgs e)
        {
            base.OnLayout(e);
            Box.SetBounds(Dpi.S(12), (Height - Box.Height) / 2, Math.Max(10, Width - Dpi.S(24)), Box.Height);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Gfx.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Gfx.FillRound(g, Theme.Input, r, Dpi.Sf(6));
            Gfx.DrawRound(g, Box.Focused ? Theme.Accent : Theme.Border, r, Dpi.Sf(6), 1f);
        }
    }

    enum ButtonStyle { Primary, Secondary, Ghost }

    // Rounded button with an optional icon glyph.
    class PillButton : PaintedControl
    {
        bool hover, down;
        public ButtonStyle Style;
        public string Glyph;
        public Font GlyphFont = Theme.IconSm;
        public bool LeftAlign;

        // Short text shown instead of an icon-only button's glyph when the Windows icon fonts aren't installed.
        static readonly Dictionary<string, string> Fallback = new Dictionary<string, string>
        {
            { "\uE7A7", "\u21BA" }, { "\uE74E", "Update" }, { "\uE71B", "Shortcut" }, { "\uE74D", "Delete" },
            { "\uE8B7", "Folder" }, { "\uE9F9", "Logs" }, { "\uE70F", "Edit" }, { "\uE777", "Restore" }
        };

        public static string FallbackText(string glyph)
        {
            string t;
            return glyph != null && Fallback.TryGetValue(glyph, out t) ? t : "";
        }

        // What actually gets drawn as the label.
        string Label { get { return string.IsNullOrEmpty(Text) && !Theme.HasIcons ? FallbackText(Glyph) : Text; } }

        public PillButton(string text, string glyph, ButtonStyle style)
        {
            Text = text;
            Glyph = glyph;
            Style = style;
            Font = style == ButtonStyle.Ghost ? Theme.Base : Theme.Button;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { down = true; Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter) OnClick(EventArgs.Empty);
            base.OnKeyUp(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Gfx.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            float rad = Style == ButtonStyle.Primary ? Dpi.Sf(10) : Dpi.Sf(8);
            Color fg;

            if (Style == ButtonStyle.Primary)
            {
                if (!Enabled)
                {
                    Gfx.FillRound(g, Theme.AccentDim, r, rad);
                    fg = Color.FromArgb(255, 170, 150);
                }
                else
                {
                    Color c1 = down ? Theme.AccentPress : hover ? Theme.AccentHover : Theme.Accent;
                    Color c2 = down ? Gfx.Mix(Theme.AccentDeep, Color.Black, 0.15f) : hover ? Gfx.Mix(Theme.AccentDeep, Color.White, 0.12f) : Theme.AccentDeep;
                    using (var b = new LinearGradientBrush(new RectangleF(0, 0, Width, Height), c1, c2, 20f)) Gfx.FillRound(g, b, r, rad);
                    using (var p = new Pen(Color.FromArgb(down ? 0 : 60, 255, 255, 255), 1f))
                        g.DrawLine(p, rad, 1.5f, Width - rad, 1.5f);
                    fg = Color.White;
                }
            }
            else if (Style == ButtonStyle.Secondary)
            {
                Gfx.FillRound(g, down ? Theme.Border : hover ? Theme.InputHover : Theme.Input, r, rad);
                Gfx.DrawRound(g, hover ? Theme.Raised : Theme.Border, r, rad, 1f);
                fg = Enabled ? Theme.Text : Theme.Subtle;
            }
            else
            {
                if (Enabled && (hover || down)) Gfx.FillRound(g, down ? Theme.NavSel : Theme.NavHover, r, rad);
                fg = !Enabled ? Gfx.Mix(Theme.Subtle, Theme.Bg, 0.45f) : hover ? Theme.Text : Theme.Muted;
            }

            if (Focused && ShowFocusCues)
                Gfx.DrawRound(g, Theme.Text, new RectangleF(1.5f, 1.5f, Width - 3f, Height - 3f), rad - 1, 1f);

            string label = Label;
            Font font = string.IsNullOrEmpty(Text) && label.Length > 1 ? Theme.Small : Font;
            Size ts = string.IsNullOrEmpty(label) ? Size.Empty : TextRenderer.MeasureText(g, label, font, Size.Empty, Gfx.Flat);
            Size gs = string.IsNullOrEmpty(Glyph) || !Theme.HasIcons ? Size.Empty : TextRenderer.MeasureText(g, Glyph, GlyphFont, Size.Empty, Gfx.Flat);
            int gap = gs.Width > 0 && ts.Width > 0 ? Dpi.S(Style == ButtonStyle.Primary ? 10 : 8) : 0;
            int x = LeftAlign ? Dpi.S(12) : (Width - (gs.Width + gap + ts.Width)) / 2;
            if (gs.Width > 0)
                TextRenderer.DrawText(g, Glyph, GlyphFont, new Point(x, (Height - gs.Height) / 2), Style == ButtonStyle.Ghost && !hover && Enabled ? Theme.Subtle : fg, Gfx.Flat);
            if (ts.Width > 0)
                TextRenderer.DrawText(g, label, font, new Point(x + gs.Width + gap, (Height - ts.Height) / 2), fg, Gfx.Flat);
        }
    }

    // Sidebar navigation entry.
    class NavItem : PaintedControl
    {
        bool hover, selected;
        public string Glyph;

        public NavItem(string text, string glyph)
        {
            Text = text;
            Glyph = glyph;
            Font = Theme.Nav;
            Cursor = Cursors.Hand;
            Height = Dpi.S(42);
        }

        public bool Selected { get { return selected; } set { if (selected != value) { selected = value; Invalidate(); } } }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Gfx.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            if (selected) Gfx.FillRound(g, Theme.NavSel, r, Dpi.Sf(8));
            else if (hover) Gfx.FillRound(g, Theme.NavHover, r, Dpi.Sf(8));
            if (selected)
                Gfx.FillRound(g, Theme.Accent, new RectangleF(0, Height / 2f - Dpi.Sf(9), Dpi.Sf(3), Dpi.Sf(18)), Dpi.Sf(1.5f));

            Color fg = selected || hover ? Theme.Text : Theme.Muted;
            int textX = Dpi.S(46);
            if (Theme.HasIcons)
            {
                Size gs = TextRenderer.MeasureText(g, Glyph, Theme.IconMd, Size.Empty, Gfx.Flat);
                TextRenderer.DrawText(g, Glyph, Theme.IconMd, new Point(Dpi.S(16), (Height - gs.Height) / 2), selected ? Theme.Accent : fg, Gfx.Flat);
            }
            else textX = Dpi.S(18);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(textX, 0, Width - textX - Dpi.S(4), Height), fg,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // Pill-shaped segmented picker (used for quality presets).
    class Segmented : PaintedControl
    {
        readonly string[] items;
        int selected, hoverIdx = -1;
        public event EventHandler SelectedIndexChanged;

        public Segmented(string[] items)
        {
            this.items = items;
            Font = Theme.Button;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        public int SelectedIndex
        {
            get { return selected; }
            set
            {
                int v = Math.Max(0, Math.Min(items.Length - 1, value));
                if (v == selected) return;
                selected = v;
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }

        RectangleF Segment(int i)
        {
            float pad = Dpi.Sf(3), w = (Width - pad * 2) / items.Length;
            return new RectangleF(pad + w * i, pad, w, Height - pad * 2);
        }

        int HitTest(int x)
        {
            for (int i = 0; i < items.Length; i++) if (x < Segment(i).Right) return i;
            return items.Length - 1;
        }

        protected override void OnMouseMove(MouseEventArgs e) { int h = HitTest(e.X); if (h != hoverIdx) { hoverIdx = h; Invalidate(); } base.OnMouseMove(e); }
        protected override void OnMouseLeave(EventArgs e) { hoverIdx = -1; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { Focus(); SelectedIndex = HitTest(e.X); base.OnMouseDown(e); }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Left || keyData == Keys.Right || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Left) SelectedIndex--;
            if (e.KeyCode == Keys.Right) SelectedIndex++;
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Gfx.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Gfx.FillRound(g, Theme.Input, r, Dpi.Sf(8));
            Gfx.DrawRound(g, Focused && ShowFocusCues ? Theme.Text : Theme.Border, r, Dpi.Sf(8), 1f);
            for (int i = 0; i < items.Length; i++)
            {
                var s = Segment(i);
                if (i == selected) Gfx.FillRound(g, Theme.Raised, s, Dpi.Sf(6));
                else if (i == hoverIdx) Gfx.FillRound(g, Theme.InputHover, s, Dpi.Sf(6));
                Color fg = i == selected ? Color.White : i == hoverIdx ? Theme.Text : Theme.Muted;
                TextRenderer.DrawText(g, items[i], Font, Rectangle.Round(s), fg,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPrefix);
            }
        }
    }

    // Status line with a colored dot.
    class StatusView : PaintedControl
    {
        Color dot = Theme.Muted;
        public Color DotColor { get { return dot; } set { dot = value; Invalidate(); } }

        public StatusView() { Font = Theme.Small; ForeColor = Theme.Muted; }

        protected override void OnForeColorChanged(EventArgs e) { Invalidate(); base.OnForeColorChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(BackColor);
            g.SmoothingMode = SmoothingMode.AntiAlias;
            float d = Dpi.Sf(8);
            using (var b = new SolidBrush(Color.FromArgb(50, dot))) g.FillEllipse(b, 0, Height / 2f - d, d * 2, d * 2);
            using (var b = new SolidBrush(dot)) g.FillEllipse(b, d / 2, Height / 2f - d / 2, d, d);
            TextRenderer.DrawText(g, Text, Font, new Rectangle(Dpi.S(26), 0, Width - Dpi.S(26), Height), ForeColor,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.WordBreak | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
    }

    // Single-line label that shortens long paths in the middle.
    class PathLabel : PaintedControl
    {
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, ForeColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.PathEllipsis | TextFormatFlags.SingleLine | TextFormatFlags.NoPrefix);
        }
    }

    // Small letter-spaced caps caption.
    class CapsLabel : PaintedControl
    {
        readonly int spacing;

        public CapsLabel(string text, Font font, Color color, int spacing)
        {
            Text = text;
            Font = font;
            ForeColor = color;
            this.spacing = spacing;
            Size = new Size(Gfx.SpacedWidth(text, font, spacing) + 2, TextRenderer.MeasureText("A", font, Size.Empty, Gfx.Flat).Height + 2);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            Gfx.DrawSpaced(e.Graphics, Text, Font, ForeColor, 0, 0, spacing);
        }
    }

    // ====================================================================== main window

    class PageInfo
    {
        public string Name, Blurb, Glyph, Section;
        public bool IsSettings;
        public StackPage Page;
        public Action OnShow, OnHide;
        public NavItem Nav;
    }

    partial class MainForm : Form
    {
        public const string AppName = "Trooper's Skate 3 Recomp Launcher";
        public const string LauncherVersion = "3.1.1";
        readonly string userDir;
        readonly string configPath;
        string gameExe;
        readonly ConfigFile config;
        readonly List<Category> categories;
        readonly Category advancedCat = new Category { Name = "Advanced", Glyph = "", Blurb = "Every key in settings.toml, including ones the other pages don't cover." };
        readonly Dictionary<string, string> extraChanges = new Dictionary<string, string>();   // keys written on save that have no editor
        readonly List<PageInfo> pageInfos = new List<PageInfo>();
        PageInfo currentPage;
        readonly ToolTip tips = new ToolTip();
        Panel header;
        Label pageTitle, pageBlurb, vramLabel;
        Control[] presetControls;
        Segmented presetBox;
        StatusView statusView;
        PathLabel pathLabel;
        Label pathNote;
        ToggleSwitch keepOpenBox, trayBox;
        PillButton playButton, saveButton, updateBadge;
        Action layoutSidebar, layoutHeader;
        Timer runningTimer;
        bool loading;

        // game session
        Process gameProc;
        DateTime gameStartUtc;
        bool closeWhenGameExits;
        DiscordRpc discord;
        NotifyIcon tray;
        readonly string startupProfile;

        public MainForm() : this(null) { }

        public MainForm(string profileToApply)
        {
            startupProfile = profileToApply;
            gameExe = FindGameExe();
            Paths.Resolve(gameExe);   // portable.txt / saves folder next to skate3.exe move the user data
            userDir = Paths.UserDir;
            configPath = Paths.Config;
            config = new ConfigFile(configPath);
            categories = BuildSettings(Paths.LogsDir(gameExe));

            Text = AppName;
            var wa = Screen.PrimaryScreen.WorkingArea;
            ClientSize = new Size(Math.Min(Dpi.S(1180), wa.Width - 40), Math.Min(Dpi.S(840), wa.Height - 40));
            MinimumSize = new Size(Math.Min(Dpi.S(1000), wa.Width), Math.Min(Dpi.S(660), wa.Height));
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(gameExe ?? Application.ExecutablePath); } catch { }

            BuildUi();
            ReloadFromFile();
            if (!string.IsNullOrEmpty(startupProfile)) LoadProfileByName(startupProfile, false);
            SetupTray();
            SetupV31();

            runningTimer = new Timer();
            runningTimer.Interval = 1500;
            runningTimer.Tick += delegate { UpdateRunningState(); };
            runningTimer.Start();
            UpdateRunningState();
            Shown += delegate { MaybeCheckForUpdates(false); };
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Native.StyleTitleBar(this);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (HandleBindCapture(e)) { base.OnKeyDown(e); return; }
            if (e.Control && e.KeyCode == Keys.S) { SaveSettings(true); e.Handled = e.SuppressKeyPress = true; }
            else if (e.Control && e.KeyCode == Keys.F) { ShowPage("Advanced"); FocusAdvancedSearch(); e.Handled = e.SuppressKeyPress = true; }
            base.OnKeyDown(e);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (WindowState == FormWindowState.Minimized && TrayEnabled && tray != null) { Hide(); tray.Visible = true; }
        }

        // ------------------------------------------------------------------ settings catalog

        static Setting B(string key, string label, string help, bool def)
        {
            return new Setting { Key = key, Label = label, Help = help, Kind = Kind.Bool, Default = Toml.Bool(def) };
        }

        static Setting C(string key, string label, string help, string def, string[] values, string[] labels)
        {
            return new Setting { Key = key, Label = label, Help = help, Kind = Kind.Choice, Default = def, ChoiceValues = values, ChoiceLabels = labels };
        }

        static Setting S(string key, string label, string help, double def, double min, double max, double step, bool isFloat, string suffix)
        {
            return new Setting
            {
                Key = key, Label = label, Help = help, Kind = Kind.Slider, Min = min, Max = max, Step = step, IsFloat = isFloat,
                Suffix = suffix, Default = isFloat ? Toml.Float(def) : Toml.Int((long)def)
            };
        }

        static List<Category> BuildSettings(string logsDir)
        {
            var list = new List<Category>();

            var display = new Category { Name = "Display", Glyph = "", Blurb = "Window, monitor, frame rate and sync." };
            display.Settings.Add(B("fullscreen", "Fullscreen", "Borderless fullscreen instead of a window.", true));
            display.Settings.Add(C("monitor", "Monitor", "Which display the game opens on.", "0",
                new[] { "0", "1", "2", "3", "4" }, new[] { "Default", "Monitor 1 (primary)", "Monitor 2", "Monitor 3", "Monitor 4" }));
            display.Settings.Add(B("vsync", "V-Sync", "Waits for the display before presenting frames. Adds latency and can cause judder with this port's frame pacing — usually best left off, especially with G-Sync/FreeSync.", false));
            display.Settings.Add(B("d3d12_allow_variable_refresh_rate_and_tearing", "Variable refresh rate (G-Sync / FreeSync)", "Allows uncapped presentation on VRR displays. Recommended on.", true));
            display.Settings.Add(B("d3d12_present_frame_limiter", "Frame limiter", "Cap how often frames are presented to the screen.", false));
            var lim = S("d3d12_present_frame_limiter_fps", "Frame limiter target", "Target FPS for the frame limiter above (e.g. your monitor's refresh rate).", 120, 30, 360, 1, true, " FPS");
            lim.Format = "0";
            display.Settings.Add(lim);
            display.Settings.Add(B("skate3_guest_fps_cap_auto", "Automatic game FPS cap", "Let the game pick its own simulation cap. Turn off to set a manual cap below.", true));
            var cap = S("skate3_guest_fps_cap", "Manual game FPS cap", "Caps the game's own frame rate when the automatic cap is off.", 0, 0, 360, 1, true, " FPS");
            cap.Format = "0"; cap.ZeroLabel = "Off";
            display.Settings.Add(cap);
            display.Settings.Add(B("show_fps_counter", "FPS counter", "Show the framerate overlay in the corner.", false));
            list.Add(display);

            var gfx = new Category { Name = "Graphics", Glyph = "", Blurb = "Resolution, anti-aliasing and view distance." };
            gfx.Settings.Add(C("skate3_native_render_scene", "Renderer", "Native renders the scene with the port's own enhanced renderer (needed for the shadow, AO, bloom and lighting options). Emulated shows the original console output. You can also flip this in-game with F5.", "true",
                new[] { "true", "false" }, new[] { "Native (enhanced)", "Emulated (original)" }));
            var rs = C("resolution_scale", "Render scale", "Internal 3D resolution as a multiple of the game's native 720p. The image is always shown at your monitor's resolution — higher values supersample down to it. Biggest performance cost in the launcher.", "1",
                new[] { "1", "2", "3" }, new[] { "1x  (720p)", "2x  (1440p)", "3x  (2160p / 4K)" });
            rs.Mirrors = new[] { "draw_resolution_scale_x", "draw_resolution_scale_y" };
            gfx.Settings.Add(rs);
            gfx.Settings.Add(C("skate3_native_render_scene_msaa", "Anti-aliasing (MSAA)", "Multisample anti-aliasing for the native renderer.", "4",
                new[] { "1", "2", "4", "8" }, new[] { "Off", "2x", "4x", "8x" }));
            var fov = S("skate3_field_of_view", "Field of view", "Gameplay camera field of view in degrees.", 60, 40, 120, 1, true, "°");
            fov.Format = "0";
            gfx.Settings.Add(fov);
            var dd = S("skate3_draw_distance_scale", "Draw distance", "How far away small world meshes (foliage, props, street furniture) keep being drawn. 1x = original console behavior.", 1, 1, 5, 0.25, true, "x");
            gfx.Settings.Add(dd);
            var lod = S("skate3_lod_distance_scale", "Level of detail distance", "How far skaters, pedestrians and vehicles keep their high-detail models. 1x = original console behavior.", 1, 1, 5, 0.25, true, "x");
            gfx.Settings.Add(lod);
            gfx.Settings.Add(B("skate3_ultrawide", "Ultrawide (21:9 / 32:9)", "Widens world rendering for ultrawide displays. Requires the Native renderer.", false));
            gfx.Settings.Add(C("skate3_ultrawide_target_aspect", "Ultrawide aspect", "Aspect ratio used when Ultrawide is on.", "0.0",
                new[] { "0.0", "2.3333", "2.3889", "3.5556" }, new[] { "Auto (match display)", "21:9  (2560x1080)", "21:9  (3440x1440)", "32:9" }));
            gfx.Settings.Add(B("skate3_native_render_mode_indicator", "Renderer indicator", "Small corner readout showing whether the frame came from the NATIVE or EMULATED renderer.", false));
            list.Add(gfx);

            var light = new Category { Name = "Lighting & Shadows", Glyph = "", Blurb = "Enhancements from the native renderer. Need Renderer = Native." };
            light.Settings.Add(B("skate3_native_render_scene_shadows", "Dynamic shadows", "Skater, NPC and prop shadows onto the world.", true));
            light.Settings.Add(C("skate3_native_render_scene_shadow_tile", "Enhanced shadow resolution", "Resolution per cascade of the enhanced shadow map. Higher is sharper at distance but uses more video memory (~50 MB at 2048, ~200 MB at 4096, ~800 MB at 8192).", "0",
                new[] { "0", "2048", "4096", "8192" }, new[] { "Game default", "2048", "4096", "8192" }));
            light.Settings.Add(B("skate3_native_render_scene_shadow_pcss", "Soft shadows (PCSS)", "Shadows are crisp where they touch their caster and soften with distance. The most expensive shadow option at high render scales.", true));
            light.Settings.Add(B("skate3_native_render_scene_shadow_static_casters", "Static sun shadows", "Buildings, trees, rails and props cast live shadows from the sun onto characters and props.", true));
            light.Settings.Add(C("skate3_native_render_scene_shadow_static_size", "Static sun shadow resolution", "Resolution of each cascade tile of the static sun-shadow map.", "4096",
                new[] { "1024", "2048", "4096", "8192" }, new[] { "1024", "2048", "4096", "8192" }));
            light.Settings.Add(B("skate3_native_render_scene_ssao", "Ambient occlusion (SSAO)", "Soft contact shading under ledges, rails, vehicles and feet.", true));
            light.Settings.Add(B("skate3_native_render_scene_ssao_full_res", "Full-resolution SSAO", "Evaluate AO at full resolution instead of half. ~4x the AO cost for slightly sharper contact shadows.", false));
            light.Settings.Add(B("skate3_native_render_scene_bloom", "Bloom", "Real HDR glow from night lamps, neon, the sun and the sky.", true));
            light.Settings.Add(B("skate3_native_render_scene_shafts", "Volumetric sun shafts", "Light shafts through buildings and trees following the time of day.", true));
            light.Settings.Add(B("skate3_native_render_scene_haze", "Directional haze", "Atmospheric haze toward the sun.", true));
            light.Settings.Add(B("skate3_native_render_scene_ssr", "Screen-space reflections (experimental)", "Real-time reflections on glass and water. Off by default upstream — may show some smearing or noise.", false));
            light.Settings.Add(B("skate3_native_render_scene_hdr_packed", "Packed HDR targets (faster)", "Uses a smaller HDR format: saves bandwidth at high resolution + MSAA, with slightly lower precision in very dark gradients.", false));
            list.Add(light);

            var input = new Category { Name = "Controls", Glyph = "", Blurb = "Controller, mouse and keyboard." };
            input.Settings.Add(C("input_backend", "Controller backend", "XInput works for Xbox controllers. SDL adds PlayStation, Switch and other controllers.", "'xinput'",
                new[] { "'xinput'", "'sdl'" }, new[] { "XInput (Xbox controllers)", "SDL (PlayStation / Switch / other)" }));
            input.Settings.Add(B("hid_rumble_enabled", "Controller rumble", "Controller vibration.", true));
            input.Settings.Add(B("mnk_mode", "Mouse & keyboard mode", "Emulates a controller from mouse and keyboard input.", false));
            input.Settings.Add(B("mnk_capture_mouse", "Capture mouse", "Locks and hides the cursor inside the game window while mouse & keyboard mode is active.", false));
            var sens = S("mnk_sensitivity", "Mouse sensitivity", "Mouse-look speed in mouse & keyboard mode.", 1, 0.1, 5, 0.05, true, "");
            sens.Format = "0.00";
            input.Settings.Add(sens);
            input.Settings.Add(new Setting
            {
                Key = "menu_chord", Label = "Settings menu button combo", Kind = Kind.Text, Default = "'rb+start'",
                Help = "Controller buttons that open the in-game settings menu, joined by '+'. Buttons: a, b, x, y, lb, rb, l3, r3, back, start, dpad_up, dpad_down, dpad_left, dpad_right. Leave empty to disable."
            });
            input.Settings.Add(B("skate3_intro_movie_skip", "Skippable intro movies", "Lets you skip the intro movies with A / Start (Space / Enter on keyboard).", true));
            list.Add(input);

            list.Add(BuildPhotoCategory());

            var other = new Category { Name = "Audio & System", Glyph = "", Blurb = "Sound, language and graphics API." };
            other.Settings.Add(B("audio_mute", "Mute audio", "Silence all game audio.", false));
            other.Settings.Add(C("audio_device_sample_frames", "Audio buffer size", "Larger buffers add a little latency but fix crackling on machines with scheduling hiccups.", "0",
                new[] { "0", "256", "512", "1024", "2048" }, new[] { "Default", "256", "512", "1024 (fixes most crackling)", "2048" }));
            other.Settings.Add(C("user_language", "Game language", "Language the game boots in.", "1",
                new[] { "1", "3", "4", "5", "6", "9", "11", "12", "2", "7", "8", "10" },
                new[] { "English", "German", "French", "Spanish", "Italian", "Portuguese", "Polish", "Russian", "Japanese", "Korean", "Chinese (Traditional)", "Chinese (Simplified)" }));
            other.Settings.Add(C("gpu_backend", "Graphics API", "Auto picks Direct3D 12 on Windows. Try Vulkan if you hit driver problems.", "'auto'",
                new[] { "'auto'", "'d3d12'", "'vulkan'" }, new[] { "Auto", "Direct3D 12", "Vulkan" }));

            // GPU picker: Vulkan physical devices in the order the game's 'vulkan_device' index uses.
            var gpus = GpuInfo.Vulkan(logsDir);
            var gpuValues = new List<string> { "-1" };
            var gpuLabels = new List<string> { "Automatic (game picks)" };
            foreach (var g in gpus) { gpuValues.Add(g.Index.ToString(CultureInfo.InvariantCulture)); gpuLabels.Add(g.Label); }
            if (gpus.Count == 0)
                for (int i = 0; i < 3; i++) { gpuValues.Add(i.ToString(CultureInfo.InvariantCulture)); gpuLabels.Add("GPU " + i); }
            other.Settings.Add(C("vulkan_device", "Graphics card (Vulkan)",
                "Which GPU the game renders on when Graphics API is Vulkan. Pick your dedicated card if the game ever starts on the CPU's integrated graphics." +
                (gpus.Count > 0 ? " Detected via " + GpuInfo.Source + "." : " No GPUs detected yet — run the game once so its log lists them."),
                "-1", gpuValues.ToArray(), gpuLabels.ToArray()));
            list.Add(other);

            return list;
        }

        // Graphics presets: key -> TOML literal
        static readonly string[] PresetNames = { "Low", "Medium", "High", "Ultra" };

        static Dictionary<string, string> Preset(string name)
        {
            var p = new Dictionary<string, string>();
            Action<string, string> set = (k, v) => p[k] = v;
            switch (name)
            {
                case "Low":
                    set("resolution_scale", "1"); set("skate3_native_render_scene_msaa", "1");
                    set("skate3_draw_distance_scale", "1.0"); set("skate3_lod_distance_scale", "1.0");
                    set("skate3_native_render_scene_shadow_tile", "0"); set("skate3_native_render_scene_shadow_pcss", "false");
                    set("skate3_native_render_scene_shadow_static_casters", "false"); set("skate3_native_render_scene_shadow_static_size", "2048");
                    set("skate3_native_render_scene_ssao", "false"); set("skate3_native_render_scene_ssao_full_res", "false");
                    set("skate3_native_render_scene_shafts", "false"); set("skate3_native_render_scene_haze", "false");
                    set("skate3_native_render_scene_hdr_packed", "true");
                    break;
                case "Medium":
                    set("resolution_scale", "1"); set("skate3_native_render_scene_msaa", "2");
                    set("skate3_draw_distance_scale", "1.5"); set("skate3_lod_distance_scale", "1.5");
                    set("skate3_native_render_scene_shadow_tile", "2048"); set("skate3_native_render_scene_shadow_pcss", "false");
                    set("skate3_native_render_scene_shadow_static_casters", "true"); set("skate3_native_render_scene_shadow_static_size", "2048");
                    set("skate3_native_render_scene_ssao", "true"); set("skate3_native_render_scene_ssao_full_res", "false");
                    set("skate3_native_render_scene_shafts", "true"); set("skate3_native_render_scene_haze", "true");
                    set("skate3_native_render_scene_hdr_packed", "true");
                    break;
                case "High":
                    set("resolution_scale", "2"); set("skate3_native_render_scene_msaa", "4");
                    set("skate3_draw_distance_scale", "2.0"); set("skate3_lod_distance_scale", "2.0");
                    set("skate3_native_render_scene_shadow_tile", "2048"); set("skate3_native_render_scene_shadow_pcss", "true");
                    set("skate3_native_render_scene_shadow_static_casters", "true"); set("skate3_native_render_scene_shadow_static_size", "4096");
                    set("skate3_native_render_scene_ssao", "true"); set("skate3_native_render_scene_ssao_full_res", "false");
                    set("skate3_native_render_scene_shafts", "true"); set("skate3_native_render_scene_haze", "true");
                    set("skate3_native_render_scene_hdr_packed", "false");
                    break;
                case "Ultra":
                    set("resolution_scale", "3"); set("skate3_native_render_scene_msaa", "8");
                    set("skate3_draw_distance_scale", "3.0"); set("skate3_lod_distance_scale", "3.0");
                    set("skate3_native_render_scene_shadow_tile", "4096"); set("skate3_native_render_scene_shadow_pcss", "true");
                    set("skate3_native_render_scene_shadow_static_casters", "true"); set("skate3_native_render_scene_shadow_static_size", "8192");
                    set("skate3_native_render_scene_ssao", "true"); set("skate3_native_render_scene_ssao_full_res", "true");
                    set("skate3_native_render_scene_shafts", "true"); set("skate3_native_render_scene_haze", "true");
                    set("skate3_native_render_scene_hdr_packed", "false");
                    break;
            }
            set("skate3_native_render_scene", "true");
            set("skate3_native_render_scene_shadows", "true");
            set("skate3_native_render_scene_bloom", "true");
            return p;
        }

        // ------------------------------------------------------------------ UI

        static int D(int v) { return Dpi.S(v); }

        void BuildUi()
        {
            // ---- Sidebar: brand, navigation, quick tools
            var side = new Panel { Dock = DockStyle.Left, Width = D(240), BackColor = Theme.Bg };
            side.Paint += (o, e) => { using (var p = new Pen(Theme.CardBorder)) e.Graphics.DrawLine(p, side.Width - 1, 0, side.Width - 1, side.Height); };

            var brandTop = new CapsLabel("TROOPER'S", Theme.Caption, Theme.Accent, D(3)) { Location = new Point(D(26), D(26)) };
            var brandMain = new TextLabel { Text = "SKATE 3", Font = Theme.Brand, ForeColor = Theme.Text, AutoSize = true, Location = new Point(D(21), D(40)), UseMnemonic = false };
            var brandSub = new CapsLabel("RECOMP LAUNCHER", Theme.Caption, Theme.Muted, D(2)) { Location = new Point(D(26), D(86)) };
            var brandBar = new Panel { Location = new Point(D(26), D(110)), Size = new Size(D(34), D(3)) };
            brandBar.Paint += (o, e) =>
            {
                using (var b = new LinearGradientBrush(brandBar.ClientRectangle, Theme.Accent, Theme.AccentDeep, 0f)) e.Graphics.FillRectangle(b, brandBar.ClientRectangle);
            };
            updateBadge = new PillButton("Update available", "", ButtonStyle.Primary) { Visible = false, Font = Theme.Small, GlyphFont = Theme.IconSm };
            updateBadge.SetBounds(D(22), D(124), D(196), D(30));
            updateBadge.Click += delegate { ShowPage("Game files"); };
            side.Controls.AddRange(new Control[] { brandTop, brandMain, brandSub, brandBar, updateBadge });

            // pages first, so the navigation can be built from them
            foreach (var cat in categories) AddPage(cat.Name, cat.Blurb, cat.Glyph, "SETTINGS", true, BuildSettingsPage(cat));
            AddPage(advancedCat.Name, advancedCat.Blurb, advancedCat.Glyph, "SETTINGS", true, BuildAdvancedPage());
            BuildToolPages();

            var navHost = new ScrollPanel { BackColor = Theme.Bg };
            var navCaptions = new List<CapsLabel>();
            string lastSection = null;
            foreach (var info in pageInfos)
            {
                var pi = info;
                if (info.Section != lastSection)
                {
                    var cap = new CapsLabel(info.Section, Theme.Caption, Theme.Subtle, D(1)) { Tag = info.Section };
                    navCaptions.Add(cap);
                    navHost.Controls.Add(cap);
                    lastSection = info.Section;
                }
                var item = new NavItem(info.Name, info.Glyph) { Tag = info.Name };
                item.Height = D(36);
                item.Click += delegate { ShowPage(pi.Name); };
                info.Nav = item;
                navHost.Controls.Add(item);
            }
            Action layoutNav = delegate
            {
                // shrink the entries (36 px down to 28 px) so every page fits without scrolling when possible
                int captionsH = navCaptions.Sum(c => c.Height + D(8)) + D(10) * Math.Max(0, navCaptions.Count - 1) + D(8);
                int fit = (navHost.ClientSize.Height - captionsH) / Math.Max(1, pageInfos.Count) - D(2);
                int itemH = Math.Max(D(28), Math.Min(D(36), fit));
                foreach (var info in pageInfos) if (info.Nav.Height != itemH) info.Nav.Height = itemH;
                int y = D(4) + navHost.AutoScrollPosition.Y;
                string sec = null;
                foreach (var info in pageInfos)
                {
                    if (info.Section != sec)
                    {
                        var cap = navCaptions.First(c => (string)c.Tag == info.Section);
                        if (sec != null) y += D(10);
                        cap.Location = new Point(D(26), y);
                        y += cap.Height + D(8);
                        sec = info.Section;
                    }
                    info.Nav.SetBounds(D(12), y, Math.Max(D(60), navHost.ClientSize.Width - D(25)), info.Nav.Height);   // ClientSize leaves room for a scrollbar
                    y += info.Nav.Height + D(2);
                }
            };

            var quickCaption = new CapsLabel("QUICK ACCESS", Theme.Caption, Theme.Subtle, D(1));
            var openCfg = new PillButton("", "", ButtonStyle.Ghost);
            var openLogs = new PillButton("", "", ButtonStyle.Ghost);
            var editToml = new PillButton("", "", ButtonStyle.Ghost);
            var restore = new PillButton("", "", ButtonStyle.Ghost);
            openCfg.Click += delegate { OpenFolder(userDir); };
            openLogs.Click += delegate { OpenFolder(gameExe != null ? Path.Combine(Path.GetDirectoryName(gameExe), "logs") : userDir); };
            editToml.Click += delegate { OpenInNotepad(configPath); };
            restore.Click += delegate { RestoreBackup(); };
            tips.SetToolTip(openCfg, "Open the settings folder: " + userDir);
            tips.SetToolTip(openLogs, "Open the game's logs folder");
            tips.SetToolTip(editToml, "Open settings.toml in Notepad");
            tips.SetToolTip(restore, "Restore backup: put back settings.toml as it was before the launcher first changed it.");
            var quickButtons = new[] { openCfg, openLogs, editToml, restore };

            keepOpenBox = new ToggleSwitch { BackColor = Theme.Bg };
            keepOpenBox.Checked = ReadPref("keep_open") == "1";
            keepOpenBox.CheckedChanged += delegate { WritePref("keep_open", keepOpenBox.Checked ? "1" : "0"); };
            var keepOpenLabel = new TextLabel { Text = "Keep launcher open", ForeColor = Theme.Muted, Font = Theme.Base, AutoSize = true, Cursor = Cursors.Hand };
            keepOpenLabel.Click += delegate { keepOpenBox.Checked = !keepOpenBox.Checked; };
            tips.SetToolTip(keepOpenLabel, "Keep this window open after starting the game.");

            trayBox = new ToggleSwitch { BackColor = Theme.Bg };
            trayBox.Checked = LauncherPrefs.Bool("use_tray", false);
            trayBox.CheckedChanged += delegate
            {
                LauncherPrefs.SetBool("use_tray", trayBox.Checked);
                if (tray != null) tray.Visible = trayBox.Checked || !Visible;
            };
            var trayLabel = new TextLabel { Text = "Use system tray", ForeColor = Theme.Muted, Font = Theme.Base, AutoSize = true, Cursor = Cursors.Hand };
            trayLabel.Click += delegate { trayBox.Checked = !trayBox.Checked; };
            tips.SetToolTip(trayLabel, "Minimizing hides the launcher to the tray, and PLAY tucks it into the tray while you play instead of closing it.");

            var fileCaption = new CapsLabel("CONFIG FILE", Theme.Caption, Theme.Subtle, D(1));
            pathLabel = new PathLabel { Font = Theme.Small, ForeColor = Theme.Muted, Height = D(20) };
            pathNote = new TextLabel { Font = Theme.Small, ForeColor = Theme.Warn, AutoSize = true, Text = "Will be created on save" };
            side.Controls.Add(navHost);
            side.Controls.AddRange(quickButtons);
            side.Controls.AddRange(new Control[] { quickCaption, keepOpenBox, keepOpenLabel, trayBox, trayLabel, fileCaption, pathLabel, pathNote });

            layoutSidebar = delegate
            {
                int y = side.Height - D(18);
                pathNote.Location = new Point(D(26), y - pathNote.Height);
                y = pathNote.Visible ? pathNote.Top - D(2) : y;
                pathLabel.SetBounds(D(26), y - pathLabel.Height, side.Width - D(48), pathLabel.Height);
                fileCaption.Location = new Point(D(26), pathLabel.Top - fileCaption.Height - D(4));
                y = fileCaption.Top - D(14);
                trayBox.Location = new Point(D(26), y - trayBox.Height);
                trayLabel.Location = new Point(trayBox.Right + D(10), trayBox.Top + (trayBox.Height - trayLabel.Height) / 2);
                y = trayBox.Top - D(8);
                keepOpenBox.Location = new Point(D(26), y - keepOpenBox.Height);
                keepOpenLabel.Location = new Point(keepOpenBox.Right + D(10), keepOpenBox.Top + (keepOpenBox.Height - keepOpenLabel.Height) / 2);
                y = keepOpenBox.Top - D(12);
                int bw = (side.Width - D(24)) / quickButtons.Length;
                for (int i = 0; i < quickButtons.Length; i++) quickButtons[i].SetBounds(D(12) + i * bw, y - D(36), bw - D(2), D(36));
                quickCaption.Location = new Point(D(26), y - D(36) - quickCaption.Height - D(6));
                int top = updateBadge.Visible ? updateBadge.Bottom + D(8) : D(124);
                navHost.SetBounds(0, top, side.Width - 1, Math.Max(D(60), quickCaption.Top - D(10) - top));
                layoutNav();
            };
            side.Resize += delegate { layoutSidebar(); };
            navHost.Resize += delegate { layoutNav(); };

            // ---- Main column
            var main = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };

            header = new Panel { Dock = DockStyle.Top, Height = D(112), BackColor = Theme.Bg };
            pageTitle = new TextLabel { Font = Theme.PageTitle, ForeColor = Theme.Text, AutoSize = true, Location = new Point(D(30), D(28)), UseMnemonic = false };
            pageBlurb = new TextLabel { Font = Theme.Base, ForeColor = Theme.Muted, AutoSize = true, Location = new Point(D(33), D(70)), UseMnemonic = false };
            var presetCaption = new CapsLabel("QUALITY PRESET", Theme.Caption, Theme.Subtle, D(1));
            presetBox = new Segmented(PresetNames) { Size = new Size(D(290), D(36)) };
            presetBox.SelectedIndex = 2;
            var applyPreset = new PillButton("Apply", "", ButtonStyle.Secondary) { Size = new Size(D(92), D(36)) };
            applyPreset.Click += delegate { ApplyPreset(PresetNames[presetBox.SelectedIndex]); };
            tips.SetToolTip(applyPreset, "Set the graphics and lighting options to this preset. Nothing is saved until you press Save or PLAY.");
            vramLabel = new TextLabel { Font = Theme.Small, ForeColor = Theme.Muted, AutoSize = true, UseMnemonic = false };
            tips.SetToolTip(vramLabel, "Rough estimate of video memory used by the current graphics settings (render targets, shadow maps and game assets). Real use varies by scene.");
            presetControls = new Control[] { presetCaption, presetBox, applyPreset, vramLabel };
            header.Controls.AddRange(new Control[] { pageTitle, pageBlurb, presetCaption, presetBox, applyPreset, vramLabel });
            layoutHeader = delegate
            {
                applyPreset.Location = new Point(header.Width - applyPreset.Width - D(32), D(40));
                presetBox.Location = new Point(applyPreset.Left - presetBox.Width - D(8), D(40));
                presetCaption.Location = new Point(presetBox.Left + D(2), D(20));
                vramLabel.Location = new Point(applyPreset.Right - vramLabel.Width, D(84));
                int right = currentPage == null || currentPage.IsSettings ? presetBox.Left : header.Width - D(20);
                pageBlurb.MaximumSize = new Size(Math.Max(D(120), right - pageBlurb.Left - D(28)), 0);
            };
            header.Resize += delegate { layoutHeader(); };
            vramLabel.SizeChanged += delegate { vramLabel.Left = header.Width - D(32) - vramLabel.Width; };

            var footer = new Panel { Dock = DockStyle.Bottom, Height = D(88), BackColor = Theme.Bg };
            footer.Paint += (o, e) => { using (var p = new Pen(Theme.CardBorder)) e.Graphics.DrawLine(p, 0, 0, footer.Width, 0); };
            statusView = new StatusView { BackColor = Theme.Bg, Location = new Point(D(30), D(18)), Height = D(52) };
            var revert = new PillButton("Revert", "", ButtonStyle.Secondary) { Size = new Size(D(108), D(42)) };
            revert.Click += delegate { ReloadFromFile(); };
            tips.SetToolTip(revert, "Discard unsaved changes and reload settings.toml.");
            saveButton = new PillButton("Save", "", ButtonStyle.Secondary) { Size = new Size(D(100), D(42)) };
            saveButton.Click += delegate { SaveSettings(true); };
            tips.SetToolTip(saveButton, "Save to settings.toml (Ctrl+S).");
            playButton = new PillButton("PLAY", "", ButtonStyle.Primary) { Size = new Size(D(176), D(52)), Font = Theme.Play, GlyphFont = Theme.IconMd };
            playButton.Click += delegate { Play(null); };
            footer.Controls.AddRange(new Control[] { statusView, revert, saveButton, playButton });
            footer.Resize += delegate
            {
                playButton.Location = new Point(footer.Width - playButton.Width - D(32), (footer.Height - playButton.Height) / 2);
                saveButton.Location = new Point(playButton.Left - saveButton.Width - D(12), (footer.Height - saveButton.Height) / 2);
                revert.Location = new Point(saveButton.Left - revert.Width - D(8), saveButton.Top);
                statusView.Top = (footer.Height - statusView.Height) / 2;
                statusView.Width = Math.Max(D(100), revert.Left - statusView.Left - D(20));
            };

            var pagesHost = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
            foreach (var info in pageInfos) pagesHost.Controls.Add(info.Page);

            main.Controls.Add(pagesHost);
            main.Controls.Add(footer);
            main.Controls.Add(header);
            Controls.Add(main);
            Controls.Add(side);
            ShowPage(categories[0].Name);
        }

        PageInfo AddPage(string name, string blurb, string glyph, string section, bool isSettings, StackPage page)
        {
            var info = new PageInfo { Name = name, Blurb = blurb, Glyph = glyph, Section = section, IsSettings = isSettings, Page = page };
            pageInfos.Add(info);
            return info;
        }

        StackPage BuildSettingsPage(Category cat)
        {
            var page = new StackPage { SettingLayout = LayoutSettingCard };
            foreach (var s in cat.Settings) page.Add(BuildSettingCard(s));
            return page;
        }

        Card BuildSettingCard(Setting s)
        {
            var card = new Card { Setting = s };
            s.NameLabel = new TextLabel { Text = s.Label, Font = Theme.Bold, ForeColor = Theme.Text, AutoSize = true, Location = new Point(D(20), D(14)), UseMnemonic = false };
            card.Help = new TextLabel { Text = s.Help, Font = Theme.Small, ForeColor = Theme.Muted, AutoSize = true, Location = new Point(D(20), D(39)), UseMnemonic = false };
            s.WarnLabel = new TextLabel { Font = Theme.Small, ForeColor = Theme.Warn, AutoSize = true, Visible = false, UseMnemonic = false };
            s.ResetButton = new PillButton("", "", ButtonStyle.Ghost) { Size = new Size(D(26), D(24)), Visible = false, GlyphFont = Theme.IconXs };
            var setting = s;
            s.ResetButton.Click += delegate { SetEditor(setting, setting.Default); OnEdited(); };
            card.Controls.Add(s.NameLabel);
            card.Controls.Add(s.ResetButton);
            card.Controls.Add(card.Help);
            card.Controls.Add(s.WarnLabel);
            card.Controls.Add(CreateEditor(s));
            if (s.ValueLabel != null) card.Controls.Add(s.ValueLabel);
            s.Card = card;
            return card;
        }

        void LayoutSettingCard(Card card, int w)
        {
            var s = card.Setting;
            var editor = s.Host;
            int right = D(20);
            int editorW = s.Kind == Kind.Bool ? editor.Width : D(270);
            int textMax = w - editorW - D(20) - right - D(36);
            card.Help.MaximumSize = new Size(textMax, 0);
            s.NameLabel.MaximumSize = new Size(Math.Max(D(60), textMax - D(34)), 0);
            s.ResetButton.Location = new Point(s.NameLabel.Left + s.NameLabel.PreferredWidth + D(6), D(11));
            int bottom = card.Help.Bottom;
            if (!string.IsNullOrEmpty(s.Warning))
            {
                s.WarnLabel.MaximumSize = new Size(textMax, 0);
                s.WarnLabel.Location = new Point(D(20), card.Help.Bottom + D(6));
                bottom = s.WarnLabel.Bottom;
            }
            card.Width = w;
            card.Height = Math.Max(D(70), bottom + D(16));
            if (s.Kind == Kind.Slider)
            {
                s.ValueLabel.Location = new Point(w - right - s.ValueLabel.Width, D(13));
                editor.Width = editorW + D(10);
                editor.Location = new Point(w - right - editorW - D(5), D(38));
                card.Height = Math.Max(card.Height, editor.Bottom + D(12));
            }
            else
            {
                if (s.Kind != Kind.Bool) editor.Width = editorW;
                editor.Location = new Point(w - right - editorW, (card.Height - editor.Height) / 2);
            }
        }

        Control CreateEditor(Setting s)
        {
            switch (s.Kind)
            {
                case Kind.Bool:
                {
                    var t = new ToggleSwitch { BackColor = Theme.Card };
                    t.CheckedChanged += delegate { OnEdited(); };
                    s.Editor = s.Host = t;
                    return t;
                }
                case Kind.Choice:
                {
                    var cb = new DarkCombo();
                    cb.Items.AddRange(s.ChoiceLabels);
                    cb.SelectedIndexChanged += delegate { OnEdited(); };
                    s.Editor = s.Host = cb;
                    return cb;
                }
                case Kind.Slider:
                {
                    var tb = new Slider { BackColor = Theme.Card };
                    tb.Maximum = (int)Math.Round((s.Max - s.Min) / s.Step);
                    tb.SmallChange = 1;
                    tb.LargeChange = Math.Max(1, tb.Maximum / 10);
                    s.ValueLabel = new TextLabel { AutoSize = true, Font = Theme.Bold, ForeColor = Theme.Accent };
                    tb.ValueChanged += delegate { UpdateSliderLabel(s); OnEdited(); };
                    s.Editor = s.Host = tb;
                    return tb;
                }
                default:
                {
                    var tx = new RoundTextBox();
                    if (s.Kind == Kind.Raw) tx.Box.Font = Theme.Mono;
                    tx.Box.TextChanged += delegate { OnEdited(); };
                    s.Editor = tx.Box;
                    s.Host = tx;
                    return tx;
                }
            }
        }

        void UpdateSliderLabel(Setting s)
        {
            var tb = (Slider)s.Editor;
            double v = s.Min + tb.Value * s.Step;
            s.ValueLabel.Text = (s.ZeroLabel != null && Math.Abs(v) < 1e-9) ? s.ZeroLabel : v.ToString(s.Format, CultureInfo.InvariantCulture) + s.Suffix;
            var card = s.ValueLabel.Parent;
            if (card != null) s.ValueLabel.Left = card.Width - D(20) - s.ValueLabel.Width;
        }

        void ShowPage(string name)
        {
            var info = pageInfos.FirstOrDefault(p => p.Name == name);
            if (info == null) return;
            if (currentPage != null && currentPage != info && currentPage.OnHide != null) currentPage.OnHide();
            currentPage = info;
            pageTitle.Text = info.Name;
            pageBlurb.Text = info.Blurb;
            foreach (var c in presetControls) c.Visible = info.IsSettings && info.Name != "Photo Mode";
            foreach (var p in pageInfos) p.Page.Visible = p == info;
            foreach (var p in pageInfos) p.Nav.Selected = p == info;
            if (info.OnShow != null) info.OnShow();
            layoutHeader();
            info.Page.Focus();
        }

        // Scrolls a setting's card into view on its page and briefly highlights it.
        void RevealSetting(Setting s)
        {
            var cat = categories.FirstOrDefault(c => c.Settings.Contains(s));
            ShowPage(cat != null ? cat.Name : "Advanced");
            if (s.Card == null) return;
            var page = s.Card.Parent as ScrollPanel;
            if (page != null) page.AutoScrollPosition = new Point(0, Math.Max(0, s.Card.Top - page.AutoScrollPosition.Y - D(10)));
            s.Card.Changed = true;
            var t = new Timer { Interval = 900 };
            t.Tick += delegate { t.Stop(); t.Dispose(); UpdateStatus(); };
            t.Start();
            s.Host.Focus();
        }

        // ------------------------------------------------------------------ values

        IEnumerable<Setting> AllSettings()
        {
            return categories.SelectMany(c => c.Settings).Concat(advancedCat.Settings);
        }

        Setting Find(string key)
        {
            return AllSettings().FirstOrDefault(s => s.Key == key);
        }

        // Current on-screen value of a key (falls back to the file / pending extra value).
        string Val(string key)
        {
            var s = Find(key);
            if (s != null) return GetEditor(s);
            string v;
            if (extraChanges.TryGetValue(key, out v)) return v;
            return config.Values.TryGetValue(key, out v) ? v : null;
        }

        bool On(string key) { return Toml.Norm(Val(key)) == "true"; }
        double Num(string key, double def) { var v = Val(key); return v == null ? def : Toml.Num(v, def); }

        string FileValue(Setting s)
        {
            string v;
            if (config.Values.TryGetValue(s.Key, out v)) return v;
            return s.Default;
        }

        void SetEditor(Setting s, string lit)
        {
            switch (s.Kind)
            {
                case Kind.Bool:
                    ((ToggleSwitch)s.Editor).Checked = lit.Trim().ToLowerInvariant() == "true";
                    break;
                case Kind.Choice:
                {
                    var cb = (ComboBox)s.Editor;
                    // drop a previous "custom" entry
                    while (cb.Items.Count > s.ChoiceLabels.Length) cb.Items.RemoveAt(cb.Items.Count - 1);
                    int idx = Array.FindIndex(s.ChoiceValues, x => Toml.Norm(x) == Toml.Norm(lit));
                    if (idx < 0)
                    {
                        cb.Items.Add("Custom (" + Toml.Unquote(lit) + ")");
                        cb.Tag = lit;
                        idx = cb.Items.Count - 1;
                    }
                    cb.SelectedIndex = idx;
                    break;
                }
                case Kind.Slider:
                {
                    var tb = (Slider)s.Editor;
                    double v = Toml.Num(lit, s.Min);
                    int pos = (int)Math.Round((v - s.Min) / s.Step);
                    tb.Value = Math.Max(tb.Minimum, Math.Min(tb.Maximum, pos));
                    UpdateSliderLabel(s);
                    break;
                }
                case Kind.Raw:
                    ((TextBox)s.Editor).Text = lit.Trim();
                    break;
                default:
                    ((TextBox)s.Editor).Text = Toml.Unquote(lit);
                    break;
            }
        }

        string GetEditor(Setting s)
        {
            switch (s.Kind)
            {
                case Kind.Bool:
                    return Toml.Bool(((ToggleSwitch)s.Editor).Checked);
                case Kind.Choice:
                {
                    var cb = (ComboBox)s.Editor;
                    if (cb.SelectedIndex >= 0 && cb.SelectedIndex < s.ChoiceValues.Length) return s.ChoiceValues[cb.SelectedIndex];
                    return cb.Tag as string ?? s.Default;
                }
                case Kind.Slider:
                {
                    var tb = (Slider)s.Editor;
                    double v = s.Min + tb.Value * s.Step;
                    return s.IsFloat ? Toml.Float(v) : Toml.Int((long)Math.Round(v));
                }
                case Kind.Raw:
                {
                    string t = ((TextBox)s.Editor).Text.Trim();
                    if (t.Length == 0) return s.Default;
                    return Toml.IsLiteral(t) ? t : Toml.Str(t);
                }
                default:
                    return Toml.Str(((TextBox)s.Editor).Text.Trim());
            }
        }

        // Human-readable default value, for the reset button's tooltip.
        string DefaultText(Setting s)
        {
            switch (s.Kind)
            {
                case Kind.Bool: return Toml.Norm(s.Default) == "true" ? "On" : "Off";
                case Kind.Choice:
                {
                    int i = Array.FindIndex(s.ChoiceValues, x => Toml.Norm(x) == Toml.Norm(s.Default));
                    return i >= 0 ? s.ChoiceLabels[i].Trim() : Toml.Unquote(s.Default);
                }
                case Kind.Slider:
                {
                    double v = Toml.Num(s.Default, s.Min);
                    return (s.ZeroLabel != null && Math.Abs(v) < 1e-9) ? s.ZeroLabel : v.ToString(s.Format, CultureInfo.InvariantCulture) + s.Suffix;
                }
                default:
                    string u = Toml.Unquote(s.Default);
                    return u.Length == 0 ? "(empty)" : u;
            }
        }

        bool ModifiedFromDefault(Setting s)
        {
            return !s.IsRaw && Toml.Norm(GetEditor(s)) != Toml.Norm(s.Default);
        }

        // Keys whose on-screen value differs from what's in the file (or that are missing from it and were changed).
        Dictionary<string, string> PendingChanges()
        {
            var changes = new Dictionary<string, string>();
            foreach (var s in AllSettings())
            {
                string now = GetEditor(s);
                string inFile;
                bool present = config.Values.TryGetValue(s.Key, out inFile);
                bool differs = present ? Toml.Norm(inFile) != Toml.Norm(now) : Toml.Norm(s.Default) != Toml.Norm(now);
                if (differs) changes[s.Key] = now;
                if (s.Mirrors != null)
                    foreach (var m in s.Mirrors)
                    {
                        string mv;
                        if (!config.Values.TryGetValue(m, out mv) || Toml.Norm(mv) != Toml.Norm(now)) changes[m] = now;
                    }
            }
            foreach (var kv in extraChanges)
            {
                string inFile;
                if (!config.Values.TryGetValue(kv.Key, out inFile) || Toml.Norm(inFile) != Toml.Norm(kv.Value)) changes[kv.Key] = kv.Value;
            }
            return changes;
        }

        // Every value the launcher knows about, as it is on screen right now (used for profiles).
        Dictionary<string, string> CurrentValues()
        {
            var d = new Dictionary<string, string>();
            foreach (var kv in config.Values) d[kv.Key] = kv.Value;
            foreach (var s in AllSettings())
            {
                string v = GetEditor(s);
                d[s.Key] = v;
                if (s.Mirrors != null) foreach (var m in s.Mirrors) d[m] = v;
            }
            foreach (var kv in extraChanges) d[kv.Key] = kv.Value;
            return d;
        }

        // Puts a set of key = literal values into the editors (unsaved). Keys without an editor are queued for the next save.
        int ApplyValues(Dictionary<string, string> values)
        {
            var mirrorKeys = new HashSet<string>(AllSettings().Where(s => s.Mirrors != null).SelectMany(s => s.Mirrors));
            int n = 0;
            loading = true;
            try
            {
                foreach (var kv in values)
                {
                    var s = Find(kv.Key);
                    if (s != null) { SetEditor(s, kv.Value); n++; }
                    else if (!mirrorKeys.Contains(kv.Key))
                    {
                        string cur;
                        if (!config.Values.TryGetValue(kv.Key, out cur) || Toml.Norm(cur) != Toml.Norm(kv.Value)) { extraChanges[kv.Key] = kv.Value; n++; }
                    }
                }
            }
            finally { loading = false; }
            OnExtraChangesChanged();
            UpdateStatus();
            return n;
        }

        void ReloadFromFile()
        {
            loading = true;
            try
            {
                config.Load();
                extraChanges.Clear();
                RebuildAdvanced();
                foreach (var s in AllSettings()) SetEditor(s, FileValue(s));
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't read " + configPath + ":\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { loading = false; }
            pathLabel.Text = configPath;
            tips.SetToolTip(pathLabel, configPath);
            bool missing = !File.Exists(configPath);
            pathNote.Visible = missing;
            layoutSidebar();
            OnExtraChangesChanged();
            OnEdited();
        }

        void OnEdited()
        {
            if (loading) return;
            flashUntil = DateTime.MinValue;   // a new edit replaces any message that was showing
            UpdateStatus();
        }

        void UpdateStatus()
        {
            var pending = PendingChanges();
            var warnings = ComputeWarnings();
            int n = 0;
            bool relayout = false;
            foreach (var s in AllSettings())
            {
                bool changed = pending.ContainsKey(s.Key);
                if (changed) n++;
                if (s.Card != null) s.Card.Changed = changed;

                if (s.ResetButton != null)
                {
                    bool mod = ModifiedFromDefault(s);
                    if (s.ResetButton.Visible != mod)
                    {
                        s.ResetButton.Visible = mod;
                        if (mod) tips.SetToolTip(s.ResetButton, "Reset to default: " + DefaultText(s));
                    }
                }
                if (s.WarnLabel != null)
                {
                    string w;
                    warnings.TryGetValue(s.Key, out w);
                    if (w != s.Warning)
                    {
                        s.Warning = w;
                        s.WarnLabel.Text = w == null ? "" : "⚠  " + w;
                        s.WarnLabel.Visible = w != null;
                        relayout = true;
                    }
                }
            }
            n += extraChanges.Keys.Count(k => pending.ContainsKey(k) && Find(k) == null);
            if (relayout && currentPage != null) currentPage.Page.Relayout();
            UpdateVramLabel();

            bool running = IsGameRunning();
            string text;
            Color dot, color = Theme.Muted;
            int warnCount = warnings.Count;
            if (running)
            {
                text = "Skate 3 is running. Close it before saving — the game rewrites settings.toml when it exits.";
                dot = color = Theme.Warn;
            }
            else if (n > 0)
            {
                text = n + " unsaved change" + (n == 1 ? "" : "s") + ". Press Save, or PLAY to save and launch.";
                dot = Theme.Accent;
                color = Theme.Text;
            }
            else if (gameExe != null)
            {
                text = "Settings saved. Ready to play.";
                dot = Theme.Good;
            }
            else
            {
                text = "skate3.exe not found next to the launcher — PLAY will ask for it.";
                dot = Theme.Warn;
            }
            if (!running && DateTime.Now < flashUntil) { Text = AppName + (n > 0 ? " *" : ""); return; }   // keep a recent message on screen
            if (!running && warnCount > 0) text += "  " + warnCount + " setting" + (warnCount == 1 ? " has a" : "s have") + " warning" + (warnCount == 1 ? "" : "s") + " (marked ⚠).";
            statusView.Text = text;
            statusView.ForeColor = color;
            statusView.DotColor = dot;
            Text = AppName + (n > 0 ? " *" : "");
        }

        // ------------------------------------------------------------------ conflict warnings & VRAM estimate

        const string KNative = "skate3_native_render_scene";

        Dictionary<string, string> ComputeWarnings()
        {
            var w = new Dictionary<string, string>();
            bool native = On(KNative);
            string[] nativeOnly =
            {
                "skate3_native_render_scene_msaa", "skate3_native_render_scene_shadows", "skate3_native_render_scene_shadow_tile",
                "skate3_native_render_scene_shadow_pcss", "skate3_native_render_scene_shadow_static_casters", "skate3_native_render_scene_shadow_static_size",
                "skate3_native_render_scene_ssao", "skate3_native_render_scene_ssao_full_res", "skate3_native_render_scene_bloom",
                "skate3_native_render_scene_shafts", "skate3_native_render_scene_haze", "skate3_native_render_scene_ssr", "skate3_native_render_scene_hdr_packed"
            };
            if (!native)
            {
                foreach (var k in nativeOnly) w[k] = "No effect while Renderer is set to Emulated (original).";
                if (On("skate3_ultrawide")) w["skate3_ultrawide"] = "Ultrawide needs Renderer = Native (enhanced) on the Graphics page.";
            }
            else
            {
                if (!On("skate3_native_render_scene_shadows"))
                {
                    w["skate3_native_render_scene_shadow_tile"] = "Only used when Dynamic shadows is on.";
                    w["skate3_native_render_scene_shadow_pcss"] = "Only used when Dynamic shadows is on.";
                }
                if (!On("skate3_native_render_scene_shadow_static_casters")) w["skate3_native_render_scene_shadow_static_size"] = "Only used when Static sun shadows is on.";
                if (!On("skate3_native_render_scene_ssao")) w["skate3_native_render_scene_ssao_full_res"] = "Only used when Ambient occlusion is on.";
            }
            if (!On("skate3_ultrawide") && Toml.Norm(Val("skate3_ultrawide_target_aspect")) != Toml.Norm("0.0"))
                w["skate3_ultrawide_target_aspect"] = "Only used when Ultrawide is on.";

            if (On("vsync") && On("d3d12_allow_variable_refresh_rate_and_tearing"))
                w["vsync"] = "V-Sync is on together with variable refresh rate. That usually adds latency and judder — turn V-Sync off to let G-Sync/FreeSync do the work.";
            var limFps = Find("d3d12_present_frame_limiter_fps");
            if (!On("d3d12_present_frame_limiter") && limFps != null && Toml.Norm(GetEditor(limFps)) != Toml.Norm(limFps.Default))
                w["d3d12_present_frame_limiter_fps"] = "Only used when Frame limiter is on.";
            if (On("skate3_guest_fps_cap_auto") && Num("skate3_guest_fps_cap", 0) > 0)
                w["skate3_guest_fps_cap"] = "Ignored while Automatic game FPS cap is on.";

            if (!On("mnk_mode"))
            {
                if (On("mnk_capture_mouse")) w["mnk_capture_mouse"] = "Only used in mouse & keyboard mode.";
                var sens = Find("mnk_sensitivity");
                if (sens != null && Toml.Norm(GetEditor(sens)) != Toml.Norm(sens.Default)) w["mnk_sensitivity"] = "Only used in mouse & keyboard mode.";
            }

            string chord = Toml.Unquote(Val("menu_chord") ?? "").Trim().ToLowerInvariant();
            if (chord.Length > 0)
            {
                var parts = chord.Split('+').Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
                var bad = parts.Where(p => !XInput.ChordNames.Contains(p)).ToList();
                if (bad.Count > 0) w["menu_chord"] = "Unknown button name" + (bad.Count == 1 ? "" : "s") + ": " + string.Join(", ", bad) + ". Use the Controller page to pick buttons.";
                else if (parts.Count == 1) w["menu_chord"] = "A single button opens the settings menu every time it's pressed in-game. A combo like rb+start is safer.";
            }

            int mon = (int)Num("monitor", 0);
            int screens = Screen.AllScreens.Length;
            if (mon > screens) w["monitor"] = "Only " + screens + " monitor" + (screens == 1 ? " is" : "s are") + " connected right now.";

            int dev = (int)Num("vulkan_device", -1);
            if (dev >= 0)
            {
                var gpus = GpuInfo.Vulkan(Paths.LogsDir(gameExe));
                var gpu = gpus.FirstOrDefault(g => g.Index == dev);
                if (Toml.Norm(Val("gpu_backend")) != Toml.Norm("'vulkan'")) w["vulkan_device"] = "Only applies when Graphics API is set to Vulkan.";
                else if (gpus.Count > 0 && gpu == null) w["vulkan_device"] = "No GPU with index " + dev + " was found.";
                else if (gpu != null && gpu.Type == 1 && gpus.Any(g => g.Type == 2)) w["vulkan_device"] = "This is the integrated GPU. Your discrete card will be much faster.";
            }

            double est = EstimateVramMB();
            var target = TargetGpu();
            if (target != null && target.VramBytes > 0 && target.Type != 1 && est > target.VramBytes / 1048576.0 * 0.9)
                w["resolution_scale"] = "Estimated VRAM use (≈" + (est / 1024).ToString("0.0", CultureInfo.InvariantCulture) + " GB) is close to or above this GPU's " +
                    Math.Round(target.VramBytes / 1073741824.0) + " GB. Expect stutter — lower render scale, MSAA or shadow resolution.";
            ComputeV31Warnings(w);
            return w;
        }

        GpuDevice TargetGpu()
        {
            var gpus = GpuInfo.Vulkan(Paths.LogsDir(gameExe));
            if (gpus.Count == 0) return null;
            int dev = (int)Num("vulkan_device", -1);
            var g = gpus.FirstOrDefault(x => x.Index == dev);
            if (g != null) return g;
            return gpus.FirstOrDefault(x => x.Type == 2) ?? gpus.OrderByDescending(x => x.VramBytes).First();
        }

        // Ballpark VRAM model: game assets + scaled render targets + shadow maps. Calibrated against the game's own memory log lines.
        double EstimateVramMB()
        {
            double scale = Math.Max(1, Num("resolution_scale", 1));
            double px = 1280.0 * 720.0 * scale * scale;
            bool native = On(KNative);
            double perPx;
            double shadows = 0;
            if (native)
            {
                double msaa = Math.Max(1, Num("skate3_native_render_scene_msaa", 4));
                double hdr = On("skate3_native_render_scene_hdr_packed") ? 4 : 8;
                perPx = msaa * (hdr + 4) + 16;
                if (On("skate3_native_render_scene_ssao")) perPx += On("skate3_native_render_scene_ssao_full_res") ? 4 : 1;
                if (On("skate3_native_render_scene_ssr")) perPx += 8;
                if (On("skate3_native_render_scene_bloom")) perPx += 2;
                if (On("skate3_native_render_scene_shadows"))
                {
                    double tile = Num("skate3_native_render_scene_shadow_tile", 0);
                    if (tile <= 0) tile = 1024;
                    shadows += tile * tile * 4 * 3;
                }
                if (On("skate3_native_render_scene_shadow_static_casters"))
                {
                    double st = Num("skate3_native_render_scene_shadow_static_size", 4096);
                    shadows += st * st * 4 * 3;
                }
            }
            else perPx = 24;
            double assets = 900 + (Num("skate3_draw_distance_scale", 1) - 1) * 80 + (Num("skate3_lod_distance_scale", 1) - 1) * 60;
            return assets + (px * perPx + shadows) / 1048576.0;
        }

        void UpdateVramLabel()
        {
            if (vramLabel == null) return;
            double est = EstimateVramMB();
            var g = TargetGpu();
            string text = "Est. VRAM ≈ " + (est / 1024).ToString("0.0", CultureInfo.InvariantCulture) + " GB";
            Color c = Theme.Muted;
            if (g != null && g.VramBytes > 0 && g.Type != 1)
            {
                double total = g.VramBytes / 1048576.0;
                text += " of " + Math.Round(total / 1024) + " GB (" + ShortGpuName(g.Name) + ")";
                if (est > total * 0.9) c = Theme.Warn;
            }
            if (vramLabel.Text != text) vramLabel.Text = text;
            vramLabel.ForeColor = c;
        }

        static string ShortGpuName(string n)
        {
            return n.Replace("NVIDIA GeForce ", "").Replace("AMD Radeon ", "").Replace("Intel(R) ", "").Replace("(TM)", "").Trim();
        }

        // ------------------------------------------------------------------ presets, save, restore

        void ApplyPreset(string name)
        {
            var p = Preset(name);
            foreach (var s in AllSettings())
            {
                string v;
                if (p.TryGetValue(s.Key, out v)) SetEditor(s, v);
            }
            UpdateStatus();
        }

        bool SaveSettings(bool showConfirmation)
        {
            if (IsGameRunning())
            {
                var r = MessageBox.Show(this, "Skate 3 is still running and will overwrite settings.toml when it closes, undoing these changes.\n\nSave anyway?",
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (r != DialogResult.Yes) return false;
            }
            var changes = PendingChanges();
            if (changes.Count == 0) { UpdateStatus(); return true; }
            try
            {
                string backup = configPath + ".launcher-backup";
                if (File.Exists(configPath) && !File.Exists(backup)) File.Copy(configPath, backup);
                Profiles.SnapshotHistory();
                config.Save(changes);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't save " + configPath + ":\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
            ReloadFromFile();
            if (showConfirmation) Flash("Saved " + changes.Count + " setting" + (changes.Count == 1 ? "" : "s") + " to settings.toml.", Theme.Good);
            RefreshToolPages();
            return true;
        }

        void RestoreBackup()
        {
            string backup = configPath + ".launcher-backup";
            if (!File.Exists(backup))
            {
                MessageBox.Show(this, "No backup yet. The launcher makes one (settings.toml.launcher-backup) the first time it saves.", AppName);
                return;
            }
            if (MessageBox.Show(this, "Replace settings.toml with the copy saved before the launcher first changed it?", AppName,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            Profiles.SnapshotHistory();
            File.Copy(backup, configPath, true);
            ReloadFromFile();
        }

        void ResetAllToDefaults()
        {
            if (MessageBox.Show(this, "Set every launcher setting back to its default?\n\nNothing is written until you press Save or PLAY, and Revert undoes it.",
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            loading = true;
            try { foreach (var s in AllSettings().Where(x => !x.IsRaw)) SetEditor(s, s.Default); }
            finally { loading = false; }
            UpdateStatus();
        }

        // ------------------------------------------------------------------ game

        string FindGameExe() { return Paths.FindGameExe(); }

        internal static bool IsGameRunning()
        {
            try
            {
                var ps = Process.GetProcessesByName("skate3");
                bool any = ps.Length > 0;
                foreach (var p in ps) p.Dispose();
                return any;
            }
            catch { return false; }
        }

        void UpdateRunningState()
        {
            bool running = IsGameRunning();
            playButton.Enabled = !running;
            playButton.Text = running ? "RUNNING" : "PLAY";
            playButton.Glyph = running ? "" : "";
            UpdateStatus();
        }

        string lastLaunchProfile;

        void Play(string profileName)
        {
            if (IsGameRunning()) return;
            if (gameExe == null)
            {
                using (var dlg = new OpenFileDialog { Title = "Locate skate3.exe", Filter = "skate3.exe|skate3.exe|Programs|*.exe" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    if (SetGameLocation(dlg.FileName)) return;
                }
            }
            if (profileName != null && !LoadProfileByName(profileName, false)) return;
            lastLaunchProfile = profileName;
            if (!SaveSettings(false)) return;

            if (LauncherPrefs.Bool("auto_backup_saves", true))
            {
                try
                {
                    SaveData.Backup("auto");
                    SaveData.PruneAuto(LauncherPrefs.Int("backup_keep", 10));
                }
                catch { }   // a failed backup must never block playing
            }

            Process p;
            try
            {
                var plan = GameLaunch.Prepare(gameExe);
                if (plan.Error == null && !ConfirmModWarnings(plan)) return;
                p = GameLaunch.Start(gameExe, plan);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't start the game:\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            gameProc = p;
            gameStartUtc = DateTime.UtcNow;
            if (p != null)
            {
                try
                {
                    p.EnableRaisingEvents = true;
                    p.Exited += delegate { try { BeginInvoke(new Action(OnGameExited)); } catch { } };
                }
                catch { }
            }
            StartDiscord();

            if (keepOpenBox.Checked)
            {
                if (TrayEnabled) HideToTray();
                UpdateRunningState();
            }
            else if (p != null && (TrayEnabled || DiscordEnabled))
            {
                // stay alive in the background so Discord presence and crash detection keep working, then close
                closeWhenGameExits = true;
                HideToTray();
            }
            else Close();
        }

        void OnGameExited()
        {
            StopDiscord();
            int code = 0;
            TimeSpan ran = DateTime.UtcNow - gameStartUtc;
            try { if (gameProc != null) code = gameProc.ExitCode; } catch { }
            if (gameProc != null) { try { gameProc.Dispose(); } catch { } }
            gameProc = null;

            if (Mods.ScrubConfig(configPath)) ReloadFromFile();
            if (code != 0) ShowCrashReport(code, ran);
            if (closeWhenGameExits) { Close(); return; }
            if (!Visible && TrayEnabled) RestoreFromTray();
            UpdateRunningState();
            RefreshToolPages();
        }

        void ShowCrashReport(int code, TimeSpan ran)
        {
            string logsDir = Paths.LogsDir(gameExe);
            var last = LogTools.Files(logsDir).FirstOrDefault();
            var sb = new StringBuilder();
            sb.Append("Skate 3 closed unexpectedly (exit code 0x").Append(code.ToString("X8")).Append(" after ").Append(FormatSpan(ran)).Append(").\n\n");
            if (last != null)
            {
                try
                {
                    var lines = LogTools.ReadAll(last.FullName, 400).Where(l => LogTools.Level(l) > 0).Reverse().Take(6).Reverse().ToList();
                    if (lines.Count > 0)
                    {
                        sb.Append("Last warnings/errors in ").Append(last.Name).Append(":\n");
                        foreach (var l in lines) sb.Append("• ").Append(l.Length > 160 ? l.Substring(0, 160) + "…" : l).Append('\n');
                        sb.Append('\n');
                    }
                }
                catch { }
            }
            sb.Append("Open the Logs page to look closer or copy a bug report?");
            bool wasHidden = !Visible;
            if (wasHidden) RestoreFromTray();
            if (MessageBox.Show(this, sb.ToString(), AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
            {
                closeWhenGameExits = false;
                ShowPage("Logs");
            }
        }

        static string FormatSpan(TimeSpan t)
        {
            if (t.TotalMinutes < 1) return (int)t.TotalSeconds + "s";
            if (t.TotalHours < 1) return (int)t.TotalMinutes + "m " + t.Seconds + "s";
            return (int)t.TotalHours + "h " + t.Minutes + "m";
        }

        // ------------------------------------------------------------------ Discord presence

        bool DiscordEnabled { get { return LauncherPrefs.Bool("discord_enabled", false) && !string.IsNullOrEmpty(LauncherPrefs.Read("discord_app_id")); } }

        void StartDiscord()
        {
            if (!DiscordEnabled) return;
            string id = LauncherPrefs.Read("discord_app_id");
            string image = LauncherPrefs.Read("discord_image") ?? "skate3";
            string state = lastLaunchProfile != null ? "Profile: " + lastLaunchProfile : DescribeSettings();
            DateTime start = gameStartUtc;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var d = new DiscordRpc();
                try
                {
                    if (d.Connect(id)) { d.SetActivity("Skating in Skate 3", state, start, image); discord = d; }
                    else d.Dispose();
                }
                catch { d.Dispose(); }
            });
        }

        void StopDiscord()
        {
            var d = discord;
            discord = null;
            if (d == null) return;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate { try { d.Clear(); } catch { } d.Dispose(); });
        }

        string DescribeSettings()
        {
            int scale = (int)Num("resolution_scale", 1);
            string res = scale >= 3 ? "4K" : scale == 2 ? "1440p" : "720p";
            return "Recomp · " + res + " · " + (On(KNative) ? "enhanced renderer" : "original renderer");
        }

        // ------------------------------------------------------------------ tray

        bool TrayEnabled { get { return trayBox != null && trayBox.Checked; } }

        void SetupTray()
        {
            tray = new NotifyIcon { Text = "Skate 3 Recomp Launcher", Icon = Icon ?? SystemIcons.Application, Visible = TrayEnabled };
            var menu = new ContextMenuStrip();
            var open = new ToolStripMenuItem("Open launcher", null, delegate { RestoreFromTray(); });
            open.Font = new Font(open.Font, FontStyle.Bold);
            var play = new ToolStripMenuItem("Play", null, delegate { Play(null); });
            var withProfile = new ToolStripMenuItem("Play with profile");
            var exit = new ToolStripMenuItem("Exit launcher", null, delegate { closeWhenGameExits = false; Close(); });
            menu.Items.AddRange(new ToolStripItem[] { open, play, withProfile, new ToolStripSeparator(), exit });
            menu.Opening += delegate
            {
                bool running = IsGameRunning();
                play.Enabled = !running;
                withProfile.DropDownItems.Clear();
                foreach (var f in Profiles.List())
                {
                    string name = Path.GetFileNameWithoutExtension(f.Name);
                    withProfile.DropDownItems.Add(new ToolStripMenuItem(name, null, delegate { Play(name); }) { Enabled = !running });
                }
                withProfile.Enabled = withProfile.DropDownItems.Count > 0 && !running;
            };
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate { RestoreFromTray(); };
        }

        void HideToTray()
        {
            if (tray != null) tray.Visible = TrayEnabled;
            Hide();
        }

        void RestoreFromTray()
        {
            closeWhenGameExits = false;
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            if (tray != null) tray.Visible = TrayEnabled;
        }

        // ------------------------------------------------------------------ helpers

        static void OpenFolder(string dir)
        {
            try { Directory.CreateDirectory(dir); Process.Start("explorer.exe", "\"" + dir + "\""); } catch { }
        }

        static void OpenInNotepad(string file)
        {
            try
            {
                if (!File.Exists(file)) { Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file, "# Auto-generated cvar configuration\n"); }
                Process.Start("notepad.exe", "\"" + file + "\"");
            }
            catch { }
        }

        static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
        }

        // tiny key=value prefs file for launcher-only options
        string ReadPref(string key) { return LauncherPrefs.Read(key); }
        void WritePref(string key, string value) { LauncherPrefs.Write(key, value); }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing && !IsGameRunning() && PendingChanges().Count > 0)
            {
                var r = MessageBox.Show(this, "Save your changes before closing?", AppName, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (r == DialogResult.Cancel) { e.Cancel = true; return; }
                if (r == DialogResult.Yes && !SaveSettings(false)) { e.Cancel = true; return; }
            }
            StopControllerPolling();
            if (tray != null) { tray.Visible = false; tray.Dispose(); tray = null; }
            var d = discord;
            discord = null;
            if (d != null) { try { d.Clear(); } catch { } d.Dispose(); }
            base.OnFormClosing(e);
        }
    }

    // ====================================================================== entry point & command line

    static class Program
    {
        const string Usage =
            "Trooper's Skate 3 Recomp Launcher\n\n" +
            "  (no arguments)        Open the launcher.\n" +
            "  --profile \"Name\"      Load a saved profile into the launcher (unsaved, review then Save/PLAY).\n" +
            "  --play                Skip the window and start the game with the current settings.toml.\n" +
            "  --play --profile \"Name\"   Apply the profile to settings.toml, then start the game.\n" +
            "  --help                Show this message.";

        [STAThread]
        static int Main(string[] args)
        {
            try { Native.SetProcessDPIAware(); } catch { }
            using (var g = Graphics.FromHwnd(IntPtr.Zero)) Dpi.F = Math.Max(1f, g.DpiX / 96f);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool play = false, help = false;
            string profile = null;
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--play" || a == "-play" || a == "/play") play = true;
                else if ((a == "--profile" || a == "-profile" || a == "/profile") && i + 1 < args.Length) profile = args[++i];
                else if (a == "--help" || a == "-h" || a == "/?" || a == "-?") help = true;
            }
            if (help) { MessageBox.Show(Usage, MainForm.AppName); return 0; }
            Paths.Resolve(Paths.FindGameExe());
            if (play) return QuickPlay(profile);
            Application.Run(new MainForm(profile));
            return 0;
        }

        // Headless launch used by "--play" shortcuts.
        static int QuickPlay(string profile)
        {
            if (MainForm.IsGameRunning()) { MessageBox.Show("Skate 3 is already running.", MainForm.AppName); return 1; }
            if (profile != null)
            {
                string path = Profiles.Find(profile);
                if (path == null) { MessageBox.Show("Profile \"" + profile + "\" wasn't found in " + Paths.Profiles + ".", MainForm.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning); return 1; }
                try
                {
                    var cfg = new ConfigFile(Paths.Config);
                    cfg.Load();
                    var diff = new Dictionary<string, string>();
                    foreach (var kv in TomlSnapshot.Read(path))
                    {
                        string cur;
                        if (!cfg.Values.TryGetValue(kv.Key, out cur) || Toml.Norm(cur) != Toml.Norm(kv.Value)) diff[kv.Key] = kv.Value;
                    }
                    if (diff.Count > 0)
                    {
                        string backup = Paths.Config + ".launcher-backup";
                        if (File.Exists(Paths.Config) && !File.Exists(backup)) File.Copy(Paths.Config, backup);
                        Profiles.SnapshotHistory();
                        cfg.Save(diff);
                    }
                }
                catch (Exception ex) { MessageBox.Show("Couldn't apply the profile:\n\n" + ex.Message, MainForm.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
            }
            string exe = Paths.FindGameExe();
            if (exe == null) { Application.Run(new MainForm(null)); return 0; }   // let the user locate the game in the normal window

            if (LauncherPrefs.Bool("auto_backup_saves", true))
                try { SaveData.Backup("auto"); SaveData.PruneAuto(LauncherPrefs.Int("backup_keep", 10)); } catch { }

            Benchmarks.RecoverConfig();
            Process p;
            try
            {
                var plan = GameLaunch.Prepare(exe);
                if (plan.Error == null && plan.Warnings.Count > 0 &&
                    MessageBox.Show(string.Join("\n\n", plan.Warnings) + "\n\nPlay anyway?", MainForm.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return 1;
                p = GameLaunch.Start(exe, plan);
            }
            catch (Exception ex) { MessageBox.Show("Couldn't start the game:\n\n" + ex.Message, MainForm.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }

            string appId = LauncherPrefs.Read("discord_app_id");
            if (p != null && LauncherPrefs.Bool("discord_enabled", false) && !string.IsNullOrEmpty(appId))
            {
                using (var d = new DiscordRpc())
                {
                    try
                    {
                        if (d.Connect(appId))
                        {
                            d.SetActivity("Skating in Skate 3", profile != null ? "Profile: " + profile : "Skate 3 Recomp", DateTime.UtcNow, LauncherPrefs.Read("discord_image") ?? "skate3");
                            p.WaitForExit();
                            d.Clear();
                        }
                    }
                    catch { }
                }
            }
            return 0;
        }
    }
}
