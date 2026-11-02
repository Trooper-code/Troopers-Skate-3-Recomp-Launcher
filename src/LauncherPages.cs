// Trooper's Skate 3 Recomp Launcher — tool pages (Advanced, Profiles, Saves & DLC, Controller, Logs, Launcher)
// and the extra controls they use. Part of MainForm (partial class).

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Skate3Launcher
{
    // ====================================================================== page building blocks

    // Vertical stack of cards that re-flows to the page width.
    class StackPage : ScrollPanel
    {
        public readonly List<Card> Cards = new List<Card>();
        public Action<Card, int> SettingLayout;

        public StackPage()
        {
            Dock = DockStyle.Fill;
            BackColor = Theme.Bg;
            Visible = false;
            AutoScrollMargin = new Size(0, Dpi.S(24));
            Resize += delegate { Relayout(); };
            VisibleChanged += delegate { Relayout(); };
        }

        public void Add(Card c) { Cards.Add(c); Controls.Add(c); }

        public void Remove(Card c) { Cards.Remove(c); Controls.Remove(c); }

        public void Relayout()
        {
            if (!Visible) return;
            SuspendLayout();
            int pad = Dpi.S(30);
            int w = Math.Max(Dpi.S(460), Math.Min(Dpi.S(1040), Width - pad - Dpi.S(32)));
            int y = Dpi.S(2) + AutoScrollPosition.Y;
            foreach (var card in Cards)
            {
                if (!card.Visible) continue;
                var tc = card as ToolCard;
                if (tc != null) tc.DoLayout(w);
                else if (SettingLayout != null) SettingLayout(card, w);
                card.Location = new Point(pad, y);
                y += card.Height + Dpi.S(8);
            }
            ResumeLayout();
        }
    }

    // Card with a title, optional help text and free-form content laid out by Body(width, top) -> bottom.
    class ToolCard : Card
    {
        public readonly Label Title, HelpText;
        public Func<int, int, int> Body;

        public ToolCard(string title, string help)
        {
            Title = new TextLabel { Text = title, Font = Theme.Bold, ForeColor = Theme.Text, AutoSize = true, Location = new Point(Dpi.S(20), Dpi.S(14)), UseMnemonic = false };
            HelpText = new TextLabel { Text = help ?? "", Font = Theme.Small, ForeColor = Theme.Muted, AutoSize = true, Location = new Point(Dpi.S(20), Dpi.S(39)), UseMnemonic = false };
            Controls.Add(Title);
            Controls.Add(HelpText);
        }

        public void DoLayout(int w)
        {
            Width = w;
            HelpText.MaximumSize = new Size(w - Dpi.S(40), 0);
            int top = HelpText.Text.Length > 0 ? HelpText.Bottom + Dpi.S(14) : Title.Bottom + Dpi.S(12);
            int bottom = Body != null ? Body(w, top) : top - Dpi.S(6);
            Height = Math.Max(Dpi.S(64), bottom + Dpi.S(16));
        }
    }

    // A row inside a card: title + subtitle on the left, buttons on the right, thin divider on top.
    class ItemRow : Panel
    {
        public readonly Label Main, Sub;
        public readonly List<Control> Buttons = new List<Control>();

        public ItemRow(string main, string sub)
        {
            BackColor = Theme.Card;
            Height = Dpi.S(52);
            Main = new TextLabel { Text = main, Font = Theme.Bold, ForeColor = Theme.Text, AutoSize = true, UseMnemonic = false, Location = new Point(0, Dpi.S(8)) };
            Sub = new TextLabel { Text = sub ?? "", Font = Theme.Small, ForeColor = Theme.Muted, AutoSize = true, UseMnemonic = false, Location = new Point(0, Dpi.S(29)) };
            Controls.Add(Main);
            Controls.Add(Sub);
            Paint += (o, e) => { using (var p = new Pen(Theme.CardBorder)) e.Graphics.DrawLine(p, 0, 0, Width, 0); };
            Resize += delegate { Arrange(); };
        }

        public void AddButton(Control b) { Buttons.Add(b); Controls.Add(b); Arrange(); }

        void Arrange()
        {
            int x = Width;
            for (int i = Buttons.Count - 1; i >= 0; i--)
            {
                var b = Buttons[i];
                x -= b.Width;
                b.Location = new Point(x, (Height - b.Height) / 2);
                x -= Dpi.S(6);
            }
            Main.MaximumSize = new Size(Math.Max(Dpi.S(60), x - Dpi.S(10)), Dpi.S(20));
            Sub.MaximumSize = new Size(Math.Max(Dpi.S(60), x - Dpi.S(10)), Dpi.S(18));
        }
    }

    // Toggleable chip used for picking controller buttons. Lit = currently held on a controller.
    class Chip : PaintedControl
    {
        bool isChecked, lit, hover;
        public event EventHandler CheckedChanged;

        public Chip(string text)
        {
            Text = text;
            Font = Theme.Small;
            Cursor = Cursors.Hand;
            Size = new Size(TextRenderer.MeasureText(text, Theme.Small).Width + Dpi.S(22), Dpi.S(30));
        }

        public bool Checked
        {
            get { return isChecked; }
            set { if (isChecked != value) { isChecked = value; Invalidate(); if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty); } }
        }

        public bool Lit { get { return lit; } set { if (lit != value) { lit = value; Invalidate(); } } }

        protected override void OnClick(EventArgs e) { Checked = !Checked; base.OnClick(e); }
        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Gfx.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var r = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            Color fill = isChecked ? Theme.Accent : hover ? Theme.InputHover : Theme.Input;
            Gfx.FillRound(g, fill, r, Dpi.Sf(15));
            Gfx.DrawRound(g, lit ? Theme.Good : isChecked ? Theme.Accent : Theme.Border, r, Dpi.Sf(15), lit ? 2f : 1f);
            TextRenderer.DrawText(g, Text, Font, ClientRectangle, isChecked ? Color.White : Theme.Text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    // Live drawing of an XInput pad: sticks, triggers and every button.
    class ControllerView : PaintedControl
    {
        public XInput.State State;
        public bool Connected;
        static readonly string[] Labels = { "A", "B", "X", "Y", "LB", "RB", "L3", "R3", "Back", "Start", "Up", "Down", "Left", "Right" };

        public ControllerView() { Height = Dpi.S(150); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Gfx.ParentBack(this));
            g.SmoothingMode = SmoothingMode.AntiAlias;
            var pad = State.Pad;
            float r = Dpi.Sf(48);
            DrawStick(g, Dpi.Sf(10), Dpi.Sf(10), r, pad.LX, pad.LY, "Left stick");
            DrawStick(g, Dpi.Sf(10) + r * 2 + Dpi.Sf(24), Dpi.Sf(10), r, pad.RX, pad.RY, "Right stick");

            float tx = Dpi.Sf(10) + r * 4 + Dpi.Sf(56);
            DrawTrigger(g, tx, Dpi.Sf(12), "LT", pad.LeftTrigger);
            DrawTrigger(g, tx + Dpi.Sf(40), Dpi.Sf(12), "RT", pad.RightTrigger);

            float bx = tx + Dpi.Sf(96), by = Dpi.Sf(10);
            float cw = Dpi.Sf(62), ch = Dpi.Sf(28);
            int perRow = Math.Max(1, (int)((Width - bx) / (cw + Dpi.Sf(6))));
            for (int i = 0; i < Labels.Length; i++)
            {
                float x = bx + (i % perRow) * (cw + Dpi.Sf(6));
                float y = by + (i / perRow) * (ch + Dpi.Sf(6));
                bool down = Connected && (pad.Buttons & XInput.ChordMasks[i]) != 0;
                var rr = new RectangleF(x, y, cw, ch);
                Gfx.FillRound(g, down ? Theme.Accent : Theme.Input, rr, Dpi.Sf(6));
                Gfx.DrawRound(g, down ? Theme.Accent : Theme.Border, rr, Dpi.Sf(6), 1f);
                TextRenderer.DrawText(g, Labels[i], Theme.Small, Rectangle.Round(rr), down ? Color.White : Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
            }
            if (!Connected)
                TextRenderer.DrawText(g, "No controller in this slot", Theme.Small, new Point((int)bx, (int)(Height - Dpi.Sf(24))), Theme.Warn, Gfx.Flat);
        }

        void DrawStick(Graphics g, float x, float y, float r, short sx, short sy, string label)
        {
            var c = new RectangleF(x, y, r * 2, r * 2);
            using (var b = new SolidBrush(Theme.Input)) g.FillEllipse(b, c);
            using (var p = new Pen(Theme.Border)) g.DrawEllipse(p, c);
            float dx = sx / 32768f * (r - Dpi.Sf(8)), dy = -sy / 32768f * (r - Dpi.Sf(8));
            float k = Dpi.Sf(8);
            using (var b = new SolidBrush(Connected ? Theme.Accent : Theme.Subtle)) g.FillEllipse(b, x + r + dx - k, y + r + dy - k, k * 2, k * 2);
            TextRenderer.DrawText(g, label, Theme.Small, new Point((int)x, (int)(y + r * 2 + Dpi.Sf(6))), Theme.Muted, Gfx.Flat);
        }

        void DrawTrigger(Graphics g, float x, float y, string label, byte v)
        {
            float h = Dpi.Sf(96), w = Dpi.Sf(22);
            var outer = new RectangleF(x, y, w, h);
            Gfx.FillRound(g, Theme.Input, outer, Dpi.Sf(5));
            float fh = h * v / 255f;
            if (fh > 1) Gfx.FillRound(g, Theme.Accent, new RectangleF(x, y + h - fh, w, fh), Dpi.Sf(5));
            Gfx.DrawRound(g, Theme.Border, outer, Dpi.Sf(5), 1f);
            TextRenderer.DrawText(g, label, Theme.Small, new Point((int)x, (int)(y + h + Dpi.Sf(6))), Theme.Muted, Gfx.Flat);
        }
    }

    // Owner-drawn, colored log viewer.
    class LogView : ListBox
    {
        public LogView()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            BackColor = Color.FromArgb(17, 18, 23);
            ForeColor = Theme.Text;
            Font = Theme.Mono;
            ItemHeight = Dpi.S(19);
            HorizontalScrollbar = true;
            IntegralHeight = false;
            SelectionMode = SelectionMode.MultiExtended;
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.DarkTheme(this, "DarkMode_Explorer"); }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Items.Count) return;
            string line = (string)Items[e.Index];
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(sel ? Theme.NavSel : BackColor)) e.Graphics.FillRectangle(b, e.Bounds);
            int lv = LogTools.Level(line);
            Color c = lv == 2 ? Color.FromArgb(255, 120, 120) : lv == 1 ? Theme.Warn : Color.FromArgb(196, 200, 212);
            TextRenderer.DrawText(e.Graphics, line, Font, new Point(e.Bounds.X + Dpi.S(6), e.Bounds.Y + Dpi.S(1)), c, Gfx.Flat);
        }
    }

    // ====================================================================== MainForm: tool pages

    partial class MainForm
    {
        // ------------------------------------------------------------------ small factories

        static Label MkLabel(string text, Font font, Color color)
        {
            return new TextLabel { Text = text, Font = font, ForeColor = color, AutoSize = true, UseMnemonic = false };
        }

        PillButton MkButton(string text, string glyph, ButtonStyle style, string tip)
        {
            var b = new PillButton(text, glyph, style) { Height = D(36) };
            int tw = string.IsNullOrEmpty(text) ? 0 : TextRenderer.MeasureText(text, b.Font).Width;
            b.Width = string.IsNullOrEmpty(text) ? D(38) : tw + D(string.IsNullOrEmpty(glyph) ? 32 : 56);
            if (string.IsNullOrEmpty(text) && !Theme.HasIcons)
                b.Width = Math.Max(D(38), TextRenderer.MeasureText(PillButton.FallbackText(glyph), Theme.Small).Width + D(20));
            if (tip != null) tips.SetToolTip(b, tip);
            return b;
        }

        ToggleSwitch MkToggle(string prefKey, bool def, Action<bool> changed)
        {
            var t = new ToggleSwitch { BackColor = Theme.Card };
            t.Checked = LauncherPrefs.Bool(prefKey, def);
            t.CheckedChanged += delegate { LauncherPrefs.SetBool(prefKey, t.Checked); if (changed != null) changed(t.Checked); };
            return t;
        }

        // Lays a row of controls left to right; returns the bottom.
        static int Row(int x, int y, params Control[] items)
        {
            int h = 0;
            foreach (var c in items) h = Math.Max(h, c.Height);
            foreach (var c in items)
            {
                c.Location = new Point(x, y + (h - c.Height) / 2);
                x += c.Width + Dpi.S(8);
            }
            return y + h;
        }

        // Toggle + label on one line; returns bottom.
        static int ToggleLine(int x, int y, ToggleSwitch t, Label l)
        {
            t.Location = new Point(x, y);
            l.Location = new Point(t.Right + Dpi.S(12), y + (t.Height - l.Height) / 2);
            return y + t.Height;
        }

        static int StackRows(IList<ItemRow> rows, int w, int top)
        {
            int y = top;
            foreach (var r in rows)
            {
                r.SetBounds(Dpi.S(20), y, w - Dpi.S(40), r.Height);
                y += r.Height;
            }
            return y;
        }

        void ReplaceRows(ToolCard card, List<ItemRow> rows, IEnumerable<ItemRow> fresh)
        {
            foreach (var r in rows) { card.Controls.Remove(r); r.Dispose(); }
            rows.Clear();
            rows.AddRange(fresh);
            foreach (var r in rows) card.Controls.Add(r);
        }

        bool GameRunningGuard(string action)
        {
            if (!IsGameRunning()) return false;
            MessageBox.Show(this, "Close Skate 3 first — " + action + " while the game is running isn't safe.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return true;
        }

        static string When(DateTime t)
        {
            var span = DateTime.Now - t;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalHours < 1) return (int)span.TotalMinutes + " min ago";
            if (span.TotalDays < 1 && t.Date == DateTime.Today) return "today " + t.ToString("HH:mm");
            if (t.Date == DateTime.Today.AddDays(-1)) return "yesterday " + t.ToString("HH:mm");
            return t.ToString("d MMM yyyy HH:mm");
        }

        DateTime flashUntil;

        // Shows a message in the status line and keeps it there for a few seconds (the 1.5 s status refresh would otherwise replace it).
        void Flash(string text, Color dot)
        {
            flashUntil = DateTime.Now.AddSeconds(6);
            statusView.Text = text;
            statusView.ForeColor = Theme.Text;
            statusView.DotColor = dot;
        }

        void RefreshToolPages()
        {
            if (currentPage != null && !currentPage.IsSettings && currentPage.OnShow != null) currentPage.OnShow();
        }

        void BuildToolPages()
        {
            BuildGameFilesPage();
            BuildModsPage();
            BuildProfilesPage();
            BuildSavesPage();
            BuildBenchmarkPage();
            BuildControllerPage();
            BuildLogsPage();
            BuildLauncherPage();
            AddPhotoModeCards();
            AddLauncherV31Cards();
            AddLogsV31Cards();
        }

        // ================================================================== Advanced

        StackPage advPage;
        RoundTextBox advSearch, advKey, advValue;
        ToolCard advRawHeader, advAddCard;
        readonly List<PillButton> advMatchButtons = new List<PillButton>();
        Label advPendingLabel;

        StackPage BuildAdvancedPage()
        {
            advPage = new StackPage { SettingLayout = LayoutSettingCard };

            var search = new ToolCard("Search every setting", "Type part of a name, cvar key or description (Ctrl+F jumps here). Matches on other pages show as links; raw keys below are filtered.");
            advSearch = new RoundTextBox();
            advSearch.Box.TextChanged += delegate { ApplyAdvancedFilter(); };
            search.Controls.Add(advSearch);
            search.Body = (w, top) =>
            {
                advSearch.SetBounds(D(20), top, Math.Min(D(520), w - D(40)), D(34));
                int x = D(20), y = advSearch.Bottom + D(10), lineH = 0;
                foreach (var b in advMatchButtons)
                {
                    if (x + b.Width > w - D(20)) { x = D(20); y += lineH + D(6); lineH = 0; }
                    b.Location = new Point(x, y);
                    x += b.Width + D(6);
                    lineH = Math.Max(lineH, b.Height);
                }
                return advMatchButtons.Count > 0 ? y + lineH : advSearch.Bottom;
            };
            advPage.Add(search);

            advAddCard = new ToolCard("Add or override a key",
                "Writes any cvar into settings.toml on the next save — for options the game supports that the launcher doesn't list. " +
                "Values: true / false, numbers, or text (text is quoted for you).");
            advKey = new RoundTextBox();
            advValue = new RoundTextBox();
            advKey.Box.Font = advValue.Box.Font = Theme.Mono;
            var keyCap = MkLabel("Key", Theme.Small, Theme.Muted);
            var valCap = MkLabel("Value", Theme.Small, Theme.Muted);
            var addBtn = MkButton("Add", "", ButtonStyle.Secondary, "Queue this key for the next save.");
            var clearBtn = MkButton("Clear queued", "", ButtonStyle.Ghost, "Forget keys queued here or by a profile.");
            var notepad = MkButton("Open settings.toml in Notepad", "", ButtonStyle.Ghost, null);
            advPendingLabel = MkLabel("", Theme.Small, Theme.Accent);
            addBtn.Click += delegate
            {
                string k = advKey.Box.Text.Trim();
                string v = advValue.Box.Text.Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(k, "^[A-Za-z0-9_]+$"))
                {
                    MessageBox.Show(this, "Keys use only letters, numbers and underscores (for example vulkan_device).", AppName);
                    return;
                }
                if (v.Length == 0) { MessageBox.Show(this, "Enter a value for " + k + ".", AppName); return; }
                string lit = Toml.IsLiteral(v) ? v : Toml.Str(v);
                var s = Find(k);
                if (s != null) { SetEditor(s, lit); RevealSetting(s); }
                else extraChanges[k] = lit;
                advKey.Box.Text = advValue.Box.Text = "";
                OnExtraChangesChanged();
                UpdateStatus();
            };
            clearBtn.Click += delegate { extraChanges.Clear(); OnExtraChangesChanged(); UpdateStatus(); };
            notepad.Click += delegate { OpenInNotepad(configPath); };
            advAddCard.Controls.AddRange(new Control[] { advKey, advValue, keyCap, valCap, addBtn, clearBtn, notepad, advPendingLabel });
            advAddCard.Body = (w, top) =>
            {
                keyCap.Location = new Point(D(20), top);
                advKey.SetBounds(D(20), keyCap.Bottom + D(4), D(300), D(34));
                valCap.Location = new Point(advKey.Right + D(10), top);
                advValue.SetBounds(advKey.Right + D(10), advKey.Top, Math.Max(D(120), Math.Min(D(260), w - advKey.Right - D(10) - addBtn.Width - D(40))), D(34));
                addBtn.Location = new Point(advValue.Right + D(8), advKey.Top - 1);
                int y = advKey.Bottom + D(10);
                if (advPendingLabel.Text.Length > 0)
                {
                    advPendingLabel.MaximumSize = new Size(w - D(40), 0);
                    advPendingLabel.Location = new Point(D(20), y);
                    y = advPendingLabel.Bottom + D(8);
                }
                clearBtn.Visible = extraChanges.Count > 0;
                return clearBtn.Visible ? Row(D(14), y, notepad, clearBtn) : Row(D(14), y, notepad);
            };
            advPage.Add(advAddCard);

            advRawHeader = new ToolCard("Other keys in settings.toml", "");
            advPage.Add(advRawHeader);
            return advPage;
        }

        void FocusAdvancedSearch() { if (advSearch != null) { advSearch.Box.Focus(); advSearch.Box.SelectAll(); } }

        // Builds one card per key in settings.toml that no other page manages.
        void RebuildAdvanced()
        {
            if (advPage == null) return;
            var managed = new HashSet<string>(categories.SelectMany(c => c.Settings).SelectMany(s => new[] { s.Key }.Concat(s.Mirrors ?? new string[0])));
            var keys = config.Values.Keys.Where(k => !managed.Contains(k)).OrderBy(k => k, StringComparer.Ordinal).ToList();
            if (keys.SequenceEqual(advancedCat.Settings.Select(s => s.Key)))
            {
                foreach (var s in advancedCat.Settings) s.Default = config.Values[s.Key];
            }
            else
            {
                foreach (var s in advancedCat.Settings) { advPage.Remove(s.Card); s.Card.Dispose(); }
                advancedCat.Settings.Clear();
                foreach (var k in keys)
                {
                    string lit = config.Values[k];
                    string norm = Toml.Norm(lit);
                    var s = new Setting
                    {
                        Key = k, Label = k, Help = RawHelp(k), IsRaw = true, Default = lit,
                        Kind = norm == "true" || norm == "false" ? Kind.Bool : Kind.Raw
                    };
                    advancedCat.Settings.Add(s);
                    advPage.Add(BuildSettingCard(s));
                }
            }
            advRawHeader.HelpText.Text = keys.Count == 0
                ? "Every key in your settings.toml already has a proper editor on the other pages."
                : keys.Count + " key" + (keys.Count == 1 ? "" : "s") + " in your settings.toml that the other pages don't cover. They're edited as raw TOML values — the game rewrites this file on exit, so unknown keys may be dropped.";
            ApplyAdvancedFilter();
        }

        static string RawHelp(string key)
        {
            if (key.StartsWith("vulkan_allow_present_mode_")) return "Allows the Vulkan '" + key.Substring("vulkan_allow_present_mode_".Length).Replace('_', ' ') + "' present mode. Leave on unless you're chasing a presentation bug.";
            if (key == "skate3_draw_distance_stream_probe") return "Internal draw-distance streaming probe used by the recomp's developers. 0 = off.";
            if (key == "vulkan_device") return "Index of the Vulkan GPU to use (-1 = automatic).";
            return "Not covered by the other pages. Raw TOML value — numbers, true/false, or text.";
        }

        void ApplyAdvancedFilter()
        {
            if (advPage == null) return;
            string q = advSearch.Box.Text.Trim();
            foreach (var s in advancedCat.Settings)
                s.Card.Visible = q.Length == 0 || s.Key.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0 || s.Help.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0;

            var search = (ToolCard)advPage.Cards[0];
            foreach (var b in advMatchButtons) { search.Controls.Remove(b); b.Dispose(); }
            advMatchButtons.Clear();
            if (q.Length >= 2)
            {
                foreach (var cat in categories)
                    foreach (var s in cat.Settings)
                    {
                        if (s.Label.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 && s.Key.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                            s.Help.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                        if (advMatchButtons.Count >= 16) break;
                        var setting = s;
                        var b = MkButton(s.Label + "  ·  " + cat.Name, "", ButtonStyle.Secondary, s.Key);
                        b.Height = D(32);
                        b.Click += delegate { RevealSetting(setting); };
                        advMatchButtons.Add(b);
                        search.Controls.Add(b);
                    }
            }
            advPage.Relayout();
        }

        void OnExtraChangesChanged()
        {
            if (advPendingLabel == null) return;
            var pendingOnly = extraChanges.Where(kv => Find(kv.Key) == null).ToList();
            advPendingLabel.Text = pendingOnly.Count == 0 ? "" :
                "Queued for next save: " + string.Join(",  ", pendingOnly.Select(kv => kv.Key + " = " + kv.Value));
            if (advPage != null) advPage.Relayout();
        }

        // ================================================================== Profiles

        ToolCard profListCard, historyCard;
        readonly List<ItemRow> profRows = new List<ItemRow>(), historyRows = new List<ItemRow>();

        void BuildProfilesPage()
        {
            var page = new StackPage();

            var saveCard = new ToolCard("Save current settings as a profile",
                "Captures everything on screen — including unsaved changes — so you can swap setups in one click (e.g. \"Ultra 4K\", \"Ultrawide\", \"Potato laptop\"). " +
                "Profiles are stored in " + Paths.Profiles + ".");
            var nameBox = new RoundTextBox();
            var saveBtn = MkButton("Save profile", "", ButtonStyle.Secondary, null);
            nameBox.Box.KeyDown += (o, e) => { if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; saveBtn.Focus(); SaveProfile(nameBox.Box.Text, true); nameBox.Box.Text = ""; } };
            saveBtn.Click += delegate { SaveProfile(nameBox.Box.Text, true); nameBox.Box.Text = ""; };
            saveCard.Controls.AddRange(new Control[] { nameBox, saveBtn });
            saveCard.Body = (w, top) => { nameBox.Width = D(320); return Row(D(20), top, nameBox, saveBtn); };
            page.Add(saveCard);

            profListCard = new ToolCard("Your profiles", "");
            profListCard.Body = (w, top) => StackRows(profRows, w, top);
            page.Add(profListCard);

            historyCard = new ToolCard("Settings history",
                "A copy of settings.toml is kept every time the launcher saves over it (newest 15). Load one to put those values back on screen, then Save.");
            var openHist = MkButton("Open history folder", "", ButtonStyle.Ghost, Paths.History);
            openHist.Click += delegate { OpenFolder(Paths.History); };
            historyCard.Controls.Add(openHist);
            historyCard.Body = (w, top) => { int y = StackRows(historyRows, w, top); return Row(D(14), y + D(6), openHist); };
            page.Add(historyCard);

            var original = new ToolCard("Original backup",
                "settings.toml exactly as it was before the launcher first changed it (settings.toml.launcher-backup). Restoring replaces the file immediately.");
            var restoreBtn = MkButton("Restore original backup", "", ButtonStyle.Secondary, null);
            restoreBtn.Click += delegate { RestoreBackup(); };
            original.Controls.Add(restoreBtn);
            original.Body = (w, top) => Row(D(20), top, restoreBtn);
            page.Add(original);

            var info = AddPage("Profiles", "Named setups you can switch between, plus a history of every save.", "", "TOOLS", false, page);
            info.OnShow = RefreshProfiles;
        }

        void RefreshProfiles()
        {
            var list = Profiles.List();
            var rows = new List<ItemRow>();
            foreach (var f in list)
            {
                string name = Path.GetFileNameWithoutExtension(f.Name);
                int count = 0;
                try { count = TomlSnapshot.Read(f.FullName).Count; } catch { }
                var row = new ItemRow(name, "Saved " + When(f.LastWriteTime) + "  ·  " + count + " settings");
                var load = MkButton("Load", "", ButtonStyle.Secondary, "Put this profile's values on screen (unsaved) so you can review them.");
                var play = MkButton("Play", "", ButtonStyle.Secondary, "Load, save and launch the game with this profile.");
                var update = MkButton("", "", ButtonStyle.Ghost, "Overwrite this profile with what's on screen now.");
                var shortcut = MkButton("", "", ButtonStyle.Ghost, "Create a desktop shortcut that launches the game with this profile.");
                var del = MkButton("", "", ButtonStyle.Ghost, "Delete this profile.");
                load.Click += delegate { LoadProfileByName(name, true); };
                play.Click += delegate { Play(name); };
                update.Click += delegate { SaveProfile(name, false); };
                shortcut.Click += delegate { CreateShortcut("Skate 3 - " + name, "--profile \"" + name + "\" --play", "Play Skate 3 with the " + name + " profile"); };
                del.Click += delegate
                {
                    if (MessageBox.Show(this, "Delete the profile \"" + name + "\"?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    try { File.Delete(f.FullName); } catch (Exception ex) { MessageBox.Show(this, ex.Message, AppName); }
                    RefreshProfiles();
                };
                foreach (var b in new Control[] { load, play, update, shortcut, del }) row.AddButton(b);
                rows.Add(row);
            }
            ReplaceRows(profListCard, profRows, rows);
            profListCard.HelpText.Text = list.Count == 0 ? "No profiles yet. Save one above." : "Load puts a profile's values on screen for review; Play loads, saves and launches in one go.";

            var hist = new List<ItemRow>();
            foreach (var f in Profiles.History().Take(15))
            {
                var row = new ItemRow("settings.toml from " + When(f.LastWriteTime), f.Name);
                var load = MkButton("Load", "", ButtonStyle.Secondary, "Put these values back on screen (unsaved).");
                var path = f.FullName;
                load.Click += delegate
                {
                    try { int n = ApplyValues(TomlSnapshot.Read(path)); Flash("Loaded " + n + " values from history. Review them, then Save or PLAY.", Theme.Accent); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, AppName); }
                };
                row.AddButton(load);
                hist.Add(row);
            }
            ReplaceRows(historyCard, historyRows, hist);
            if (currentPage != null) currentPage.Page.Relayout();
        }

        void SaveProfile(string name, bool confirmOverwrite)
        {
            name = (name ?? "").Trim();
            if (name.Length == 0) { MessageBox.Show(this, "Give the profile a name first.", AppName); return; }
            string path = Profiles.PathFor(name);
            if (confirmOverwrite && File.Exists(path) &&
                MessageBox.Show(this, "A profile called \"" + name + "\" already exists. Overwrite it?", AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            try
            {
                TomlSnapshot.Write(path, CurrentValues(), "Trooper's Skate 3 Recomp Launcher profile \"" + name + "\" — saved " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
                Flash("Saved profile \"" + name + "\".", Theme.Good);
            }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't save the profile:\n\n" + ex.Message, AppName); }
            RefreshProfiles();
        }

        bool LoadProfileByName(string name, bool announce)
        {
            string path = Profiles.Find(name);
            if (path == null) { MessageBox.Show(this, "Profile \"" + name + "\" wasn't found in " + Paths.Profiles + ".", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning); return false; }
            try
            {
                ApplyValues(TomlSnapshot.Read(path));
                if (announce || startupProfile != null) Flash("Loaded profile \"" + Path.GetFileNameWithoutExtension(path) + "\". Review it, then Save or PLAY.", Theme.Accent);
                return true;
            }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't read the profile:\n\n" + ex.Message, AppName); return false; }
        }

        void CreateShortcut(string name, string args, string description)
        {
            try
            {
                string lnk = Shortcuts.Desktop(name);
                Shortcuts.Create(lnk, Paths.LauncherExe, args, Path.GetDirectoryName(Paths.LauncherExe), gameExe ?? Paths.LauncherExe, description);
                Flash("Created \"" + Path.GetFileName(lnk) + "\" on your desktop.", Theme.Good);
            }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't create the shortcut:\n\n" + ex.Message, AppName); }
        }

        // ================================================================== Saves & DLC

        ToolCard saveInfoCard, backupListCard, dlcCard;
        Label saveInfoText, dlcText;
        readonly List<ItemRow> backupRows = new List<ItemRow>();

        void BuildSavesPage()
        {
            var page = new StackPage();

            saveInfoCard = new ToolCard("Save data", "Your career and created skaters live in one folder per gamer profile inside " + Paths.SavesRoot +
                (Paths.PortableSaves ? " (the portable saves folder next to skate3.exe)." : "."));
            saveInfoText = MkLabel("", Theme.Base, Theme.Text);
            var backupNow = MkButton("Back up now", "", ButtonStyle.Secondary, null);
            var openSaves = MkButton("Open save folder", "", ButtonStyle.Ghost, null);
            backupNow.Click += delegate
            {
                try
                {
                    string z = SaveData.Backup("manual");
                    Flash(z == null ? "There's no save data to back up yet." : "Saves backed up to " + Path.GetFileName(z) + ".", z == null ? Theme.Warn : Theme.Good);
                }
                catch (Exception ex) { MessageBox.Show(this, "Backup failed:\n\n" + ex.Message, AppName); }
                RefreshSaves();
            };
            openSaves.Click += delegate { OpenFolder(Paths.SavesRoot); };
            saveInfoCard.Controls.AddRange(new Control[] { saveInfoText, backupNow, openSaves });
            saveInfoCard.Body = (w, top) =>
            {
                saveInfoText.MaximumSize = new Size(w - D(40), 0);
                saveInfoText.Location = new Point(D(20), top);
                return Row(D(20), saveInfoText.Bottom + D(12), backupNow, openSaves);
            };
            page.Add(saveInfoCard);

            var autoCard = new ToolCard("Automatic backups",
                "Zips your saves into " + Paths.SaveBackups + " every time you press PLAY (and from quick-play shortcuts). " +
                "Only automatic backups are trimmed; manual and pre-restore backups are kept until you delete them.");
            var autoToggle = MkToggle("auto_backup_saves", true, null);
            var autoLabel = MkLabel("Back up saves before every launch", Theme.Base, Theme.Text);
            var keepCap = MkLabel("Keep the newest", Theme.Base, Theme.Muted);
            var keepBox = new DarkCombo { Width = D(150) };
            int[] keeps = { 5, 10, 20, 50 };
            keepBox.Items.AddRange(keeps.Select(k => k + " automatic").Cast<object>().ToArray());
            int cur = LauncherPrefs.Int("backup_keep", 10);
            keepBox.SelectedIndex = Math.Max(0, Array.IndexOf(keeps, cur) < 0 ? 1 : Array.IndexOf(keeps, cur));
            keepBox.SelectedIndexChanged += delegate { LauncherPrefs.Write("backup_keep", keeps[keepBox.SelectedIndex].ToString(CultureInfo.InvariantCulture)); };
            autoCard.Controls.AddRange(new Control[] { autoToggle, autoLabel, keepCap, keepBox });
            autoCard.Body = (w, top) =>
            {
                int y = ToggleLine(D(20), top, autoToggle, autoLabel) + D(12);
                return Row(D(20), y, keepCap, keepBox);
            };
            page.Add(autoCard);

            backupListCard = new ToolCard("Backups", "");
            var openBackups = MkButton("Open backups folder", "", ButtonStyle.Ghost, Paths.SaveBackups);
            openBackups.Click += delegate { OpenFolder(Paths.SaveBackups); };
            backupListCard.Controls.Add(openBackups);
            backupListCard.Body = (w, top) => { int y = StackRows(backupRows, w, top); return Row(D(14), y + D(6), openBackups); };
            page.Add(backupListCard);

            dlcCard = new ToolCard("DLC", "The game loads DLC packages from " + Paths.Dlc + ". Only use DLC you legally own (dumped from your own console).");
            dlcText = MkLabel("", Theme.Base, Theme.Text);
            var addDlc = MkButton("Add DLC files…", "", ButtonStyle.Secondary, "Copy DLC package files into the dlc folder.");
            var openDlc = MkButton("Open DLC folder", "", ButtonStyle.Ghost, null);
            addDlc.Click += delegate { AddDlcFiles(); };
            openDlc.Click += delegate { OpenFolder(Paths.Dlc); };
            dlcCard.Controls.AddRange(new Control[] { dlcText, addDlc, openDlc });
            dlcCard.Body = (w, top) =>
            {
                dlcText.MaximumSize = new Size(w - D(40), 0);
                dlcText.Location = new Point(D(20), top);
                return Row(D(20), dlcText.Bottom + D(12), addDlc, openDlc);
            };
            page.Add(dlcCard);

            var info = AddPage("Saves & DLC", "Back up and restore your career, and manage DLC packages.", "", "TOOLS", false, page);
            info.OnShow = RefreshSaves;
        }

        void RefreshSaves()
        {
            var dirs = SaveData.ProfileDirs();
            if (dirs.Count == 0) saveInfoText.Text = "No save data found yet — it appears after you create a profile in-game.";
            else
            {
                var sb = new StringBuilder();
                foreach (var d in dirs)
                {
                    DateTime last = d.LastWriteTime;
                    try { foreach (var f in d.GetFiles("*", SearchOption.AllDirectories)) if (f.LastWriteTime > last) last = f.LastWriteTime; } catch { }
                    sb.Append("Profile ").Append(d.Name).Append("   ·   ").Append(Paths.Size(Paths.DirSize(d.FullName))).Append("   ·   last saved ").Append(When(last)).Append('\n');
                }
                saveInfoText.Text = sb.ToString().TrimEnd();
            }

            var backups = SaveData.Backups();
            var rows = new List<ItemRow>();
            foreach (var f in backups.Take(20))
            {
                string kind = f.Name.EndsWith("-auto.zip") ? "Automatic" : f.Name.EndsWith("-pre-restore.zip") ? "Before a restore" : "Manual";
                var row = new ItemRow(When(f.LastWriteTime), kind + "  ·  " + Paths.Size(f.Length) + "  ·  " + f.Name);
                var restore = MkButton("Restore", "", ButtonStyle.Secondary, "Replace your current saves with this backup (your current saves are backed up first).");
                var del = MkButton("", "", ButtonStyle.Ghost, "Delete this backup.");
                var path = f.FullName;
                restore.Click += delegate
                {
                    if (GameRunningGuard("restoring saves")) return;
                    if (MessageBox.Show(this, "Replace your current saves with the backup from " + When(File.GetLastWriteTime(path)) + "?\n\nYour current saves are backed up first, so this can be undone.",
                            AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                    try { SaveData.Restore(path); Flash("Saves restored.", Theme.Good); }
                    catch (Exception ex) { MessageBox.Show(this, "Restore failed:\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
                    RefreshSaves();
                };
                del.Click += delegate
                {
                    if (MessageBox.Show(this, "Delete this backup?\n\n" + Path.GetFileName(path), AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                    try { File.Delete(path); } catch (Exception ex) { MessageBox.Show(this, ex.Message, AppName); }
                    RefreshSaves();
                };
                row.AddButton(restore);
                row.AddButton(del);
                rows.Add(row);
            }
            ReplaceRows(backupListCard, backupRows, rows);
            long total = backups.Sum(b => b.Length);
            backupListCard.HelpText.Text = backups.Count == 0 ? "No backups yet." :
                backups.Count + " backup" + (backups.Count == 1 ? "" : "s") + " (" + Paths.Size(total) + ")" + (backups.Count > 20 ? " — showing the newest 20." : ".");

            var dlc = SaveData.DlcFiles();
            dlcText.Text = dlc.Count == 0 ? "No DLC packages installed." :
                dlc.Count + " file" + (dlc.Count == 1 ? "" : "s") + " (" + Paths.Size(dlc.Sum(f => f.Length)) + "):  " +
                string.Join(",  ", dlc.Take(8).Select(f => f.Name)) + (dlc.Count > 8 ? ", …" : "");
            if (currentPage != null) currentPage.Page.Relayout();
        }

        void AddDlcFiles()
        {
            using (var dlg = new OpenFileDialog { Title = "Choose DLC package files", Multiselect = true, Filter = "All files|*.*" })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    Directory.CreateDirectory(Paths.Dlc);
                    foreach (var f in dlg.FileNames) File.Copy(f, Path.Combine(Paths.Dlc, Path.GetFileName(f)), true);
                    Flash("Copied " + dlg.FileNames.Length + " file" + (dlg.FileNames.Length == 1 ? "" : "s") + " into the DLC folder. They load next time you start the game.", Theme.Good);
                }
                catch (Exception ex) { MessageBox.Show(this, "Couldn't copy the files:\n\n" + ex.Message, AppName); }
            }
            RefreshSaves();
        }

        // ================================================================== Controller

        Timer padTimer;
        Segmented padSlot;
        ControllerView padView;
        Label padSlotsText, joyText, chordValue, recordText;
        readonly List<Chip> chordChips = new List<Chip>();
        bool syncingChips;
        DateTime recordUntil, recordStable;
        ushort recordMask;
        bool recording;

        void BuildControllerPage()
        {
            var page = new StackPage();

            var xi = new ToolCard("Xbox controllers (XInput)", "Press buttons and move the sticks to check everything registers. Rumble test buzzes the selected controller.");
            padSlot = new Segmented(new[] { "Slot 1", "Slot 2", "Slot 3", "Slot 4" }) { Size = new Size(D(320), D(34)) };
            padSlotsText = MkLabel("", Theme.Small, Theme.Muted);
            padView = new ControllerView { BackColor = Theme.Card };
            var rumble = MkButton("Test rumble", "", ButtonStyle.Secondary, null);
            rumble.Click += delegate
            {
                int slot = padSlot.SelectedIndex;
                XInput.Rumble(slot, 40000, 40000);
                var t = new Timer { Interval = 600 };
                t.Tick += delegate { t.Stop(); t.Dispose(); XInput.Rumble(slot, 0, 0); };
                t.Start();
            };
            xi.Controls.AddRange(new Control[] { padSlot, padSlotsText, padView, rumble });
            xi.Body = (w, top) =>
            {
                int y = Row(D(20), top, padSlot, rumble);
                padSlotsText.Location = new Point(D(20), y + D(8));
                padView.SetBounds(D(20), padSlotsText.Bottom + D(10), w - D(40), D(150));
                return padView.Bottom;
            };
            page.Add(xi);

            var joy = new ToolCard("Other controllers (DirectInput)",
                "PlayStation, Switch and other non-Xbox pads show up here. To use one in-game, set Controller backend to SDL on the Controls page.");
            joyText = MkLabel("", Theme.Mono, Theme.Text);
            joy.Controls.Add(joyText);
            joy.Body = (w, top) => { joyText.MaximumSize = new Size(w - D(40), 0); joyText.Location = new Point(D(20), top); return joyText.Bottom; };
            page.Add(joy);

            var chord = new ToolCard("Settings menu button combo",
                "Pick the buttons that open the recomp's in-game settings menu, or press Record and hold the combo on your controller. " +
                "This edits the same setting as the Controls page (saved with Save / PLAY).");
            for (int i = 0; i < XInput.ChordNames.Length; i++)
            {
                var c = new Chip(XInput.ChordNames[i]) { BackColor = Theme.Card };
                c.CheckedChanged += delegate { if (!syncingChips) ChipsToChord(); };
                chordChips.Add(c);
                chord.Controls.Add(c);
            }
            chordValue = MkLabel("", Theme.Bold, Theme.Accent);
            recordText = MkLabel("", Theme.Small, Theme.Warn);
            var record = MkButton("Record from controller", "", ButtonStyle.Secondary, "Hold the buttons you want for about a second.");
            var clear = MkButton("Clear", "", ButtonStyle.Ghost, "Disable the settings menu combo.");
            record.Click += delegate
            {
                recording = true;
                recordUntil = DateTime.Now.AddSeconds(8);
                recordMask = 0;
                recordText.Text = "Hold the combo on your controller now…";
                chord.DoLayout(chord.Width);
                page.Relayout();
            };
            clear.Click += delegate { SetChord(""); };
            chord.Controls.AddRange(new Control[] { chordValue, recordText, record, clear });
            chord.Body = (w, top) =>
            {
                int x = D(20), y = top;
                foreach (var c in chordChips)
                {
                    if (x + c.Width > w - D(20)) { x = D(20); y += c.Height + D(6); }
                    c.Location = new Point(x, y);
                    x += c.Width + D(6);
                }
                y += D(30) + D(12);
                chordValue.Location = new Point(D(20), y);
                y = Row(D(20), chordValue.Bottom + D(10), record, clear);
                if (recordText.Text.Length > 0) { recordText.Location = new Point(D(20), y + D(8)); y = recordText.Bottom; }
                return y;
            };
            page.Add(chord);

            var chordSetting = Find("menu_chord");
            if (chordSetting != null) ((TextBox)chordSetting.Editor).TextChanged += delegate { SyncChips(); };

            padTimer = new Timer { Interval = 33 };
            padTimer.Tick += delegate { PollControllers(); };
            var info = AddPage("Controller", "Test controllers and set the in-game settings menu combo.", "", "TOOLS", false, page);
            info.OnShow = delegate { SyncChips(); padTimer.Start(); PollControllers(); };
            info.OnHide = delegate { StopControllerPolling(); };
        }

        void StopControllerPolling()
        {
            if (padTimer != null) padTimer.Stop();
            recording = false;
            if (recordText != null) recordText.Text = "";
        }

        void PollControllers()
        {
            var slots = new List<string>();
            ushort anyButtons = 0;
            for (int i = 0; i < 4; i++)
            {
                XInput.State st;
                bool ok = XInput.Get(i, out st);
                slots.Add("Slot " + (i + 1) + ": " + (ok ? "connected" : "—"));
                if (ok) anyButtons |= st.Pad.Buttons;
                if (i == padSlot.SelectedIndex)
                {
                    var shown = padView.State;
                    if (ok != padView.Connected || st.Packet != shown.Packet) { padView.Connected = ok; padView.State = st; padView.Invalidate(); }
                }
            }
            string slotsLine = string.Join("     ", slots);
            if (padSlotsText.Text != slotsLine) padSlotsText.Text = slotsLine;

            var sb = new StringBuilder();
            int n = WinMM.Count();
            int found = 0;
            for (int j = 0; j < n; j++)
            {
                WinMM.JoyInfoEx ji;
                if (!WinMM.Get(j, out ji)) continue;
                found++;
                var pressed = new List<string>();
                for (int b = 0; b < 32; b++) if ((ji.Buttons & (1u << b)) != 0) pressed.Add((b + 1).ToString(CultureInfo.InvariantCulture));
                sb.Append("Controller ").Append(j + 1).Append(":  X ").Append(Pct(ji.X)).Append("  Y ").Append(Pct(ji.Y))
                  .Append("  Z ").Append(Pct(ji.Z)).Append("  R ").Append(Pct(ji.R)).Append("   buttons: ").Append(pressed.Count == 0 ? "none" : string.Join(" ", pressed)).Append('\n');
            }
            string joyLine = found == 0 ? "No DirectInput controllers detected." : sb.ToString().TrimEnd() + "\n(Xbox controllers also appear here.)";
            if (joyText.Text != joyLine)
            {
                bool relayout = joyText.Text.Split('\n').Length != joyLine.Split('\n').Length;
                joyText.Text = joyLine;
                if (relayout && currentPage != null) currentPage.Page.Relayout();
            }

            for (int i = 0; i < chordChips.Count; i++) chordChips[i].Lit = (anyButtons & XInput.ChordMasks[i]) != 0;

            if (recording)
            {
                ushort mask = 0;
                foreach (var m in XInput.ChordMasks) mask |= (ushort)(anyButtons & m);
                if (DateTime.Now > recordUntil) { recording = false; recordText.Text = "Nothing recorded — make sure an Xbox controller is connected."; }
                else if (mask != 0)
                {
                    if (mask != recordMask) { recordMask = mask; recordStable = DateTime.Now; }
                    else if ((DateTime.Now - recordStable).TotalMilliseconds > 900)
                    {
                        recording = false;
                        var names = new List<string>();
                        for (int i = 0; i < XInput.ChordNames.Length; i++) if ((mask & XInput.ChordMasks[i]) != 0) names.Add(XInput.ChordNames[i]);
                        SetChord(string.Join("+", names));
                        recordText.Text = "Recorded " + string.Join("+", names) + ".";
                    }
                }
            }
        }

        static string Pct(uint v) { return ((int)Math.Round(v / 655.35)).ToString(CultureInfo.InvariantCulture).PadLeft(3) + "%"; }

        void SetChord(string chord)
        {
            var s = Find("menu_chord");
            if (s == null) return;
            SetEditor(s, Toml.Str(chord));
            OnEdited();
            SyncChips();
        }

        void ChipsToChord()
        {
            var names = new List<string>();
            for (int i = 0; i < chordChips.Count; i++) if (chordChips[i].Checked) names.Add(XInput.ChordNames[i]);
            SetChord(string.Join("+", names));
        }

        void SyncChips()
        {
            var s = Find("menu_chord");
            if (s == null || chordValue == null) return;
            string chord = ((TextBox)s.Editor).Text.Trim().ToLowerInvariant();
            var parts = new HashSet<string>(chord.Split('+').Select(p => p.Trim()));
            syncingChips = true;
            for (int i = 0; i < chordChips.Count; i++) chordChips[i].Checked = parts.Contains(XInput.ChordNames[i]);
            syncingChips = false;
            chordValue.Text = chord.Length == 0 ? "Current combo: none (menu disabled)" : "Current combo: " + chord;
        }

        // ================================================================== Logs

        ToolCard sessionCard, viewerCard;
        Label sessionText, logsSizeText;
        DarkCombo logFiles;
        Segmented logFilter;
        RoundTextBox logSearch;
        LogView logView;
        List<string> logLines = new List<string>();
        List<FileInfo> logFileList = new List<FileInfo>();
        bool refreshingLogs;

        void BuildLogsPage()
        {
            var page = new StackPage();

            sessionCard = new ToolCard("Last session", "");
            sessionText = MkLabel("", Theme.Base, Theme.Text);
            sessionCard.Controls.Add(sessionText);
            sessionCard.Body = (w, top) => { sessionText.MaximumSize = new Size(w - D(40), 0); sessionText.Location = new Point(D(20), top); return sessionText.Bottom; };
            page.Add(sessionCard);

            viewerCard = new ToolCard("Log viewer", "Errors are red, warnings are yellow. Shows the last 20,000 lines of the chosen log; Ctrl+C copies selected lines.");
            logFiles = new DarkCombo { Width = D(360) };
            logFilter = new Segmented(new[] { "All", "Warnings", "Errors" }) { Size = new Size(D(250), D(34)) };
            logSearch = new RoundTextBox { Width = D(200) };
            logView = new LogView();
            var refresh = MkButton("Refresh", "", ButtonStyle.Ghost, null);
            var notepad = MkButton("Open in Notepad", "", ButtonStyle.Ghost, null);
            var folder = MkButton("Open logs folder", "", ButtonStyle.Ghost, null);
            logFiles.SelectedIndexChanged += delegate { if (!refreshingLogs) LoadLogFile(); };
            logFilter.SelectedIndexChanged += delegate { FilterLog(); };
            logSearch.Box.TextChanged += delegate { FilterLog(); };
            refresh.Click += delegate { RefreshLogs(); };
            notepad.Click += delegate { var f = SelectedLog(); if (f != null) OpenInNotepad(f.FullName); };
            folder.Click += delegate { string d = Paths.LogsDir(gameExe); if (d != null) OpenFolder(d); };
            logView.KeyDown += (o, e) =>
            {
                if (e.Control && e.KeyCode == Keys.C && logView.SelectedItems.Count > 0)
                {
                    Clipboard.SetText(string.Join(Environment.NewLine, logView.SelectedItems.Cast<string>()));
                    e.Handled = true;
                }
            };
            viewerCard.Controls.AddRange(new Control[] { logFiles, logFilter, logSearch, logView, refresh, notepad, folder });
            viewerCard.Body = (w, top) =>
            {
                int y = Row(D(20), top, logFiles, logFilter);
                logSearch.SetBounds(logFilter.Right + D(8), logFilter.Top, Math.Max(D(120), w - logFilter.Right - D(28)), D(34));
                if (logSearch.Right > w - D(20) || logFilter.Right > w - D(140))
                {
                    logSearch.SetBounds(D(20), y + D(8), w - D(40), D(34));
                    y = logSearch.Bottom;
                }
                logView.SetBounds(D(20), y + D(10), w - D(40), D(380));
                return Row(D(14), logView.Bottom + D(8), refresh, notepad, folder);
            };
            page.Add(viewerCard);

            var tools = new ToolCard("Bug reports & cleanup",
                "A bug report bundles your launcher/game versions, PC specs, GPU list, settings.toml and the latest log's warnings and errors — ready to paste into a GitHub issue or Discord.");
            var copy = MkButton("Copy bug report", "", ButtonStyle.Secondary, "Copy the report text to the clipboard.");
            var zip = MkButton("Save bug report (.zip)", "", ButtonStyle.Secondary, "Saves report.txt, settings.toml and the latest logs as a zip on your desktop.");
            var clean = MkButton("Delete old logs", "", ButtonStyle.Ghost, "Keep the newest 3 logs and delete the rest.");
            logsSizeText = MkLabel("", Theme.Small, Theme.Muted);
            copy.Click += delegate
            {
                try { Clipboard.SetText(BuildBugReport()); Flash("Bug report copied to the clipboard.", Theme.Good); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, AppName); }
            };
            zip.Click += delegate { SaveBugReportZip(); };
            clean.Click += delegate { DeleteOldLogs(); };
            tools.Controls.AddRange(new Control[] { copy, zip, clean, logsSizeText });
            tools.Body = (w, top) =>
            {
                int y = Row(D(20), top, copy, zip, clean);
                logsSizeText.Location = new Point(D(20), y + D(10));
                return logsSizeText.Bottom;
            };
            page.Add(tools);

            var info = AddPage("Logs", "Read the game's logs, spot crashes and build bug reports.", "", "TOOLS", false, page);
            info.OnShow = RefreshLogs;
        }

        FileInfo SelectedLog()
        {
            int i = logFiles.SelectedIndex;
            return i >= 0 && i < logFileList.Count ? logFileList[i] : null;
        }

        void RefreshLogs()
        {
            string dir = Paths.LogsDir(gameExe);
            var prevSel = SelectedLog();
            logFileList = LogTools.Files(dir);
            refreshingLogs = true;
            logFiles.Items.Clear();
            foreach (var f in logFileList) logFiles.Items.Add(f.Name + "   ·   " + When(f.LastWriteTime) + "   ·   " + Paths.Size(f.Length));
            int sel = prevSel != null ? logFileList.FindIndex(f => f.FullName == prevSel.FullName) : -1;
            if (logFileList.Count > 0) logFiles.SelectedIndex = sel >= 0 ? sel : 0;
            refreshingLogs = false;
            LoadLogFile();

            var s = LogTools.LastSession(dir);
            if (dir == null) { sessionText.Text = "skate3.exe hasn't been found yet, so there's no logs folder to read."; sessionText.ForeColor = Theme.Warn; }
            else if (s == null) { sessionText.Text = "No logs yet — they appear in " + dir + " after the game runs."; sessionText.ForeColor = Theme.Muted; }
            else
            {
                bool running = IsGameRunning();
                string state = running ? "Skate 3 is running right now." :
                    s.Clean ? "Last session ended cleanly." :
                    Benchmarks.IsBenchmarkLog(s.File) ? "Last session was a benchmark run: the launcher closed the game itself when it finished." : "Last session didn't shut down cleanly — the log stops without the usual shutdown lines, which usually means a crash or a forced close.";
                sessionText.Text = state + "\n" + s.File.Name + "  ·  " + When(s.File.LastWriteTime) +
                    (s.Version != null ? "  ·  game v" + s.Version : "") + "  ·  " + s.Errors + " error" + (s.Errors == 1 ? "" : "s") + ", " + s.Warnings + " warning" + (s.Warnings == 1 ? "" : "s");
                sessionText.ForeColor = running || s.Clean || Benchmarks.IsBenchmarkLog(s.File) ? Theme.Text : Theme.Warn;
            }
            long total = logFileList.Sum(f => f.Length);
            logsSizeText.Text = logFileList.Count + " log file" + (logFileList.Count == 1 ? "" : "s") + " using " + Paths.Size(total) + ".";
            if (currentPage != null) currentPage.Page.Relayout();
        }

        void LoadLogFile()
        {
            var f = SelectedLog();
            logLines = new List<string>();
            if (f != null)
            {
                try { logLines = LogTools.ReadAll(f.FullName, 20000); }
                catch (Exception ex) { logLines.Add("Couldn't read " + f.Name + ": " + ex.Message); }
            }
            FilterLog();
        }

        void FilterLog()
        {
            int min = logFilter.SelectedIndex;   // 0 all, 1 warnings+errors, 2 errors
            string q = logSearch.Box.Text.Trim();
            logView.BeginUpdate();
            logView.Items.Clear();
            int maxLen = 0;
            foreach (var l in logLines)
            {
                if (min > 0 && LogTools.Level(l) < min) continue;
                if (q.Length > 0 && l.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) continue;
                logView.Items.Add(l);
                if (l.Length > maxLen) maxLen = l.Length;
            }
            logView.HorizontalExtent = (int)(maxLen * TextRenderer.MeasureText("0000000000", Theme.Mono).Width / 10.0) + D(20);
            if (logView.Items.Count > 0) logView.TopIndex = logView.Items.Count - 1;
            logView.EndUpdate();
        }

        string BuildBugReport()
        {
            string dir = Paths.LogsDir(gameExe);
            var sb = new StringBuilder();
            sb.AppendLine("Skate 3 Recomp bug report — " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine("Launcher: Trooper's Skate 3 Recomp Launcher v" + LauncherVersion);
            sb.AppendLine("Game: " + (LogTools.InstalledVersion(dir, gameExe) ?? "unknown version") + (gameExe != null ? "  (" + gameExe + ")" : ""));
            sb.AppendLine("OS: " + SysInfo.Os());
            sb.AppendLine("CPU: " + SysInfo.Cpu());
            sb.AppendLine("RAM: " + SysInfo.Ram());
            var gpus = GpuInfo.Vulkan(dir);
            sb.AppendLine("GPUs: " + (gpus.Count == 0 ? "unknown" : string.Join("; ", gpus.Select(g => g.Label))));
            sb.AppendLine("Monitors: " + string.Join("; ", Screen.AllScreens.Select(s => s.Bounds.Width + "x" + s.Bounds.Height + (s.Primary ? " (primary)" : ""))));
            AppendV31Report(sb);
            var s0 = LogTools.LastSession(dir);
            if (s0 != null)
                sb.AppendLine("Last session: " + s0.File.Name + " — " + (s0.Clean ? "ended cleanly" : Benchmarks.IsBenchmarkLog(s0.File) ? "benchmark run (closed by the launcher)" : "did NOT shut down cleanly") + ", " + s0.Errors + " errors, " + s0.Warnings + " warnings");
            sb.AppendLine();
            sb.AppendLine("---- settings.toml ----");
            try { sb.AppendLine(File.Exists(configPath) ? File.ReadAllText(configPath).Trim() : "(missing)"); } catch (Exception ex) { sb.AppendLine("(unreadable: " + ex.Message + ")"); }
            sb.AppendLine();
            if (s0 != null)
            {
                sb.AppendLine("---- warnings & errors from " + s0.File.Name + " (last 60) ----");
                try
                {
                    var lines = LogTools.ReadAll(s0.File.FullName, 0).Where(l => LogTools.Level(l) > 0).ToList();
                    foreach (var l in lines.Skip(Math.Max(0, lines.Count - 60))) sb.AppendLine(l);
                    sb.AppendLine();
                    sb.AppendLine("---- last 25 lines ----");
                    foreach (var l in LogTools.ReadAll(s0.File.FullName, 25)) sb.AppendLine(l);
                }
                catch { }
            }
            return sb.ToString();
        }

        void SaveBugReportZip()
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "Skate3-bug-report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".zip");
                using (var fs = new FileStream(path, FileMode.Create))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    var e = zip.CreateEntry("report.txt");
                    using (var w = new StreamWriter(e.Open(), new UTF8Encoding(false))) w.Write(BuildBugReport());
                    if (File.Exists(configPath)) AddFileToZip(zip, configPath, "settings.toml");
                    foreach (var f in LogTools.Files(Paths.LogsDir(gameExe)).Take(2)) AddFileToZip(zip, f.FullName, "logs/" + f.Name);
                    AddCrashDumpToZip(zip);
                }
                Flash("Saved " + Path.GetFileName(path) + " to your desktop.", Theme.Good);
                Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't save the bug report:\n\n" + ex.Message, AppName); }
        }

        static void AddFileToZip(ZipArchive zip, string file, string name)
        {
            var e = zip.CreateEntry(name);
            using (var src = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var dst = e.Open()) src.CopyTo(dst);
        }

        void DeleteOldLogs()
        {
            var files = LogTools.Files(Paths.LogsDir(gameExe));
            var old = files.Skip(3).ToList();
            if (old.Count == 0) { MessageBox.Show(this, "Nothing to clean up — there are 3 or fewer logs.", AppName); return; }
            long size = old.Sum(f => f.Length);
            if (MessageBox.Show(this, "Delete " + old.Count + " older log file" + (old.Count == 1 ? "" : "s") + " (" + Paths.Size(size) + ")? The newest 3 are kept.",
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            int deleted = 0;
            foreach (var f in old) { try { f.Delete(); deleted++; } catch { } }
            Flash("Deleted " + deleted + " old log" + (deleted == 1 ? "" : "s") + ".", Theme.Good);
            RefreshLogs();
        }

        // ================================================================== Launcher

        Label updateText, cacheText, gamePathText, discordStatus;
        DarkCombo winGpuBox;
        RoundTextBox repoBox, discordId, discordImage;
        string installedVersion;

        void BuildLauncherPage()
        {
            var page = new StackPage();
            installedVersion = RecompVersion.Current(Paths.LogsDir(gameExe), gameExe);

            // ---- updates
            var upd = new ToolCard("Updates", "Checks GitHub for a newer Skate 3 Recomp release. Install updates (and roll back) on the Game files page.");
            updateText = MkLabel("", Theme.Base, Theme.Text);
            var check = MkButton("Check now", "", ButtonStyle.Secondary, null);
            var releases = MkButton("Open releases page", "", ButtonStyle.Ghost, null);
            var autoCheck = MkToggle("check_updates", true, null);
            var autoLabel = MkLabel("Check when the launcher starts (at most twice a day)", Theme.Base, Theme.Text);
            var repoCap = MkLabel("Release source (GitHub owner/repo)", Theme.Small, Theme.Muted);
            repoBox = new RoundTextBox { Width = D(300) };
            repoBox.Box.Text = LauncherPrefs.Read("update_repo") ?? Updates.DefaultRepo;
            repoBox.Box.Leave += delegate
            {
                string r = repoBox.Box.Text.Trim();
                if (r.Length == 0) { r = Updates.DefaultRepo; repoBox.Box.Text = r; }
                LauncherPrefs.Write("update_repo", r);
            };
            check.Click += delegate { repoBox.Box.Text = repoBox.Box.Text.Trim(); LauncherPrefs.Write("update_repo", repoBox.Box.Text); MaybeCheckForUpdates(true); };
            releases.Click += delegate { OpenUrl("https://github.com/" + UpdateRepo + "/releases"); };
            upd.Controls.AddRange(new Control[] { updateText, check, releases, autoCheck, autoLabel, repoCap, repoBox });
            upd.Body = (w, top) =>
            {
                updateText.MaximumSize = new Size(w - D(40), 0);
                updateText.Location = new Point(D(20), top);
                int y = Row(D(20), updateText.Bottom + D(12), check, releases) + D(14);
                y = ToggleLine(D(20), y, autoCheck, autoLabel) + D(14);
                repoCap.Location = new Point(D(20), y);
                repoBox.Location = new Point(D(20), repoCap.Bottom + D(4));
                return repoBox.Bottom;
            };
            page.Add(upd);

            // ---- Windows GPU preference
            var wgpu = new ToolCard("Windows graphics preference for skate3.exe",
                "The per-app GPU choice from Windows Settings → System → Display → Graphics. This is what picks the GPU when the game uses Direct3D 12 " +
                "(for Vulkan, use \"Graphics card (Vulkan)\" on the Audio & System page). Applies immediately.");
            winGpuBox = new DarkCombo { Width = D(300) };
            winGpuBox.Items.AddRange(new object[] { "Let Windows decide", "Power saving (integrated GPU)", "High performance (dedicated GPU)" });
            bool loadingPref = true;
            winGpuBox.SelectedIndex = gameExe != null ? Math.Max(0, Math.Min(2, GpuInfo.WindowsPreference(gameExe))) : 0;
            loadingPref = false;
            winGpuBox.SelectedIndexChanged += delegate
            {
                if (loadingPref) return;
                if (gameExe == null) { MessageBox.Show(this, "skate3.exe hasn't been found yet — set the game location below first.", AppName); return; }
                try { GpuInfo.SetWindowsPreference(gameExe, winGpuBox.SelectedIndex); Flash("Windows graphics preference for skate3.exe set to \"" + winGpuBox.Text + "\".", Theme.Good); }
                catch (Exception ex) { MessageBox.Show(this, "Couldn't change the preference:\n\n" + ex.Message, AppName); }
            };
            wgpu.Controls.Add(winGpuBox);
            wgpu.Body = (w, top) => Row(D(20), top, winGpuBox);
            page.Add(wgpu);

            // ---- cache
            var cache = new ToolCard("Shader & pipeline cache",
                "The game caches compiled shaders in " + Paths.Cache + ". Clearing it is the standard fix for graphics glitches or crashes after a GPU driver or game update — " +
                "the first run afterwards stutters a little while shaders rebuild.");
            cacheText = MkLabel("", Theme.Base, Theme.Text);
            var clearShaders = MkButton("Clear shader cache", "", ButtonStyle.Secondary, "Deletes cache\\nrhi_shaders and cache\\shaders.");
            var clearAll = MkButton("Clear all cache", "", ButtonStyle.Ghost, "Deletes everything inside the cache folder (it's all rebuilt automatically).");
            var openCache = MkButton("Open cache folder", "", ButtonStyle.Ghost, null);
            clearShaders.Click += delegate { ClearCache(false); };
            clearAll.Click += delegate { ClearCache(true); };
            openCache.Click += delegate { OpenFolder(Paths.Cache); };
            cache.Controls.AddRange(new Control[] { cacheText, clearShaders, clearAll, openCache });
            cache.Body = (w, top) =>
            {
                cacheText.MaximumSize = new Size(w - D(40), 0);
                cacheText.Location = new Point(D(20), top);
                return Row(D(20), cacheText.Bottom + D(12), clearShaders, clearAll, openCache);
            };
            page.Add(cache);

            // ---- shortcuts & command line
            var sc = new ToolCard("Desktop shortcuts & command line",
                "Quick play skips the launcher window and starts the game with your saved settings (saves are still backed up). Per-profile shortcuts are on the Profiles page.\n" +
                "Command line:  --play   ·   --profile \"Name\"   ·   --play --profile \"Name\"   ·   --help");
            var scLauncher = MkButton("Launcher shortcut", "", ButtonStyle.Secondary, null);
            var scPlay = MkButton("Quick-play shortcut", "", ButtonStyle.Secondary, null);
            scLauncher.Click += delegate { CreateShortcut("Skate 3 Recomp Launcher", "", "Open Trooper's Skate 3 Recomp Launcher"); };
            scPlay.Click += delegate { CreateShortcut("Skate 3", "--play", "Play Skate 3 (Recomp) straight away"); };
            sc.Controls.AddRange(new Control[] { scLauncher, scPlay });
            sc.Body = (w, top) => Row(D(20), top, scLauncher, scPlay);
            page.Add(sc);

            // ---- Discord
            var dc = new ToolCard("Discord Rich Presence",
                "Shows \"Skating in Skate 3\" with a play timer on your Discord profile while the game runs. Discord needs an Application ID: create a free app at " +
                "discord.com/developers/applications (name it \"Skate 3\" — that name is what friends see), copy its Application ID here, and optionally upload an image " +
                "under Rich Presence → Art Assets with the key below. While presence is on, the launcher stays running in the background until you quit the game.");
            var dcToggle = MkToggle("discord_enabled", false, null);
            var dcLabel = MkLabel("Show Skate 3 on my Discord profile", Theme.Base, Theme.Text);
            var idCap = MkLabel("Application ID", Theme.Small, Theme.Muted);
            var imgCap = MkLabel("Image asset key", Theme.Small, Theme.Muted);
            discordId = new RoundTextBox { Width = D(260) };
            discordImage = new RoundTextBox { Width = D(160) };
            discordId.Box.Text = LauncherPrefs.Read("discord_app_id") ?? "";
            discordImage.Box.Text = LauncherPrefs.Read("discord_image") ?? "skate3";
            discordId.Box.TextChanged += delegate { LauncherPrefs.Write("discord_app_id", discordId.Box.Text.Trim()); };
            discordImage.Box.TextChanged += delegate { LauncherPrefs.Write("discord_image", discordImage.Box.Text.Trim()); };
            var test = MkButton("Test connection", "", ButtonStyle.Secondary, "Connects to the Discord app and shows a test presence for 15 seconds.");
            discordStatus = MkLabel("", Theme.Small, Theme.Muted);
            test.Click += delegate { TestDiscord(); };
            dc.Controls.AddRange(new Control[] { dcToggle, dcLabel, idCap, imgCap, discordId, discordImage, test, discordStatus });
            dc.Body = (w, top) =>
            {
                int y = ToggleLine(D(20), top, dcToggle, dcLabel) + D(14);
                idCap.Location = new Point(D(20), y);
                discordId.Location = new Point(D(20), idCap.Bottom + D(4));
                imgCap.Location = new Point(discordId.Right + D(10), y);
                discordImage.Location = new Point(discordId.Right + D(10), discordId.Top);
                test.Location = new Point(discordImage.Right + D(10), discordId.Top - 1);
                y = discordId.Bottom;
                if (discordStatus.Text.Length > 0) { discordStatus.MaximumSize = new Size(w - D(40), 0); discordStatus.Location = new Point(D(20), y + D(8)); y = discordStatus.Bottom; }
                return y;
            };
            page.Add(dc);

            // ---- reset & about
            var about = new ToolCard("Game location & reset", "");
            gamePathText = MkLabel("", Theme.Base, Theme.Text);
            var change = MkButton("Change game location…", "", ButtonStyle.Secondary, "Point the launcher at a different skate3.exe.");
            var reset = MkButton("Reset all settings to defaults", "", ButtonStyle.Ghost, "Puts every launcher setting back to its default (unsaved until Save / PLAY).");
            change.Click += delegate
            {
                using (var dlg = new OpenFileDialog { Title = "Locate skate3.exe", Filter = "skate3.exe|skate3.exe|Programs|*.exe" })
                {
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    if (SetGameLocation(dlg.FileName)) return;
                    installedVersion = RecompVersion.Current(Paths.LogsDir(gameExe), gameExe);
                    RefreshLauncherPage();
                    UpdateStatus();
                }
            };
            reset.Click += delegate { ResetAllToDefaults(); };
            about.Controls.AddRange(new Control[] { gamePathText, change, reset });
            about.Body = (w, top) =>
            {
                gamePathText.MaximumSize = new Size(w - D(40), 0);
                gamePathText.Location = new Point(D(20), top);
                return Row(D(20), gamePathText.Bottom + D(12), change, reset);
            };
            page.Add(about);

            var info = AddPage("Launcher", "Updates, GPU preference, cache, shortcuts, Discord and more.", "", "TOOLS", false, page);
            info.OnShow = RefreshLauncherPage;
            ApplyUpdateResult(null);
        }

        string UpdateRepo
        {
            get { string r = LauncherPrefs.Read("update_repo"); return string.IsNullOrEmpty(r) ? Updates.DefaultRepo : r; }
        }

        void RefreshLauncherPage()
        {
            long shaders = Paths.DirSize(Path.Combine(Paths.Cache, "nrhi_shaders")) + Paths.DirSize(Path.Combine(Paths.Cache, "shaders"));
            long all = Paths.DirSize(Paths.Cache);
            cacheText.Text = "Shader cache: " + Paths.Size(shaders) + "   ·   whole cache folder: " + Paths.Size(all);
            gamePathText.Text = "Game: " + (gameExe ?? "skate3.exe not found") + "\nSettings, profiles and backups: " + Paths.UserDir + (Paths.Portable ? "  (portable mode: portable.txt is next to skate3.exe)" : "") +
                "\nSaves: " + Paths.SavesRoot + (Paths.PortableSaves ? "  (portable saves folder)" : "") +
                "\nLauncher v" + LauncherVersion + (installedVersion != null ? "   ·   Skate 3 Recomp v" + installedVersion.TrimStart('v') : "");
            if (gameExe != null)
            {
                int pref = Math.Max(0, Math.Min(2, GpuInfo.WindowsPreference(gameExe)));
                if (winGpuBox.SelectedIndex != pref) winGpuBox.SelectedIndex = pref;
            }
            ApplyUpdateResult(null);
            if (currentPage != null) currentPage.Page.Relayout();
        }

        void ClearCache(bool everything)
        {
            if (GameRunningGuard("clearing the cache")) return;
            if (MessageBox.Show(this, everything ? "Delete everything inside the cache folder? It's rebuilt automatically next launch." :
                    "Delete the shader cache? Shaders are rebuilt automatically (expect a little stutter the first time).",
                    AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            long before = Paths.DirSize(Paths.Cache);
            try
            {
                if (everything)
                {
                    if (Directory.Exists(Paths.Cache))
                    {
                        foreach (var d in Directory.GetDirectories(Paths.Cache)) Directory.Delete(d, true);
                        foreach (var f in Directory.GetFiles(Paths.Cache)) File.Delete(f);
                    }
                }
                else
                {
                    foreach (var sub in new[] { "nrhi_shaders", "shaders" })
                    {
                        string d = Path.Combine(Paths.Cache, sub);
                        if (Directory.Exists(d)) Directory.Delete(d, true);
                    }
                }
                Flash("Cleared " + Paths.Size(before - Paths.DirSize(Paths.Cache)) + " of cache.", Theme.Good);
            }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't clear everything:\n\n" + ex.Message, AppName); }
            RefreshLauncherPage();
        }

        void TestDiscord()
        {
            string id = discordId.Box.Text.Trim();
            if (id.Length == 0) { SetDiscordStatus("Enter your Discord Application ID first.", Theme.Warn); return; }
            SetDiscordStatus("Connecting to Discord…", Theme.Muted);
            string image = discordImage.Box.Text.Trim();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var d = new DiscordRpc();
                try
                {
                    if (!d.Connect(id))
                    {
                        PostDiscordStatus("Couldn't connect. Make sure the Discord desktop app is running and the Application ID is right.", Theme.Warn);
                        return;
                    }
                    d.SetActivity("Testing the launcher", "Skate 3 Recomp", DateTime.UtcNow, image);
                    PostDiscordStatus("Connected" + (d.UserName != null ? " as " + d.UserName : "") + " — check your Discord profile. The test presence clears in 15 seconds.", Theme.Good);
                    System.Threading.Thread.Sleep(15000);
                    d.Clear();
                    PostDiscordStatus("Test finished — presence cleared. It's working.", Theme.Good);
                }
                catch (Exception ex) { PostDiscordStatus("Discord error: " + ex.Message, Theme.Warn); }
                finally { d.Dispose(); }
            });
        }

        void PostDiscordStatus(string text, Color color)
        {
            try { BeginInvoke(new Action(delegate { SetDiscordStatus(text, color); })); } catch { }
        }

        void SetDiscordStatus(string text, Color color)
        {
            discordStatus.Text = text;
            discordStatus.ForeColor = color;
            if (currentPage != null) currentPage.Page.Relayout();
        }

        // ------------------------------------------------------------------ update check

        void MaybeCheckForUpdates(bool force)
        {
            if (!force)
            {
                if (!LauncherPrefs.Bool("check_updates", true)) { ApplyUpdateResult(null); return; }
                long last;
                long.TryParse(LauncherPrefs.Read("update_last_check"), out last);
                if (last > 0 && DateTime.UtcNow - new DateTime(last, DateTimeKind.Utc) < TimeSpan.FromHours(12)) { ApplyUpdateResult(null); return; }
            }
            if (updateText != null) { updateText.Text = "Checking " + UpdateRepo + "…"; updateText.ForeColor = Theme.Muted; }
            string repo = UpdateRepo;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string tag, url, error;
                bool ok = Updates.Latest(repo, out tag, out url, out error);
                if (ok)
                {
                    LauncherPrefs.Write("update_latest_tag", tag);
                    LauncherPrefs.Write("update_latest_url", url);
                    LauncherPrefs.Write("update_last_check", DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
                }
                try { BeginInvoke(new Action(delegate { ApplyUpdateResult(ok ? null : error); })); } catch { }
            });
        }

        void ApplyUpdateResult(string error)
        {
            string tag = LauncherPrefs.Read("update_latest_tag");
            long last;
            long.TryParse(LauncherPrefs.Read("update_last_check"), out last);
            bool newer = tag != null && installedVersion != null && Updates.Compare(tag, installedVersion) > 0;

            if (updateBadge != null)
            {
                bool show = newer && LauncherPrefs.Bool("check_updates", true);
                updateBadge.Text = tag != null ? tag + " available" : "Update available";
                if (updateBadge.Visible != show) { updateBadge.Visible = show; if (layoutSidebar != null) layoutSidebar(); }
                tips.SetToolTip(updateBadge, "A newer Skate 3 Recomp release is out. Click to install it from the Game files page.");
            }
            if (updateText == null) return;
            string installed = installedVersion != null ? "v" + installedVersion.TrimStart('v') : "unknown (run the game once so its log records the version)";
            string text = "Installed: " + installed + "     Latest: " + (tag ?? "not checked yet") +
                (last > 0 ? "  (checked " + When(new DateTime(last, DateTimeKind.Utc).ToLocalTime()) + ")" : "");
            if (error != null) { text += "\nCouldn't check for updates: " + error; updateText.ForeColor = Theme.Warn; }
            else if (newer) { text += "\nA newer release is available. Install it from the Game files page."; updateText.ForeColor = Theme.Accent; }
            else updateText.ForeColor = Theme.Text;
            updateText.Text = text;
            if (currentPage != null && !currentPage.IsSettings) currentPage.Page.Relayout();
        }
    }
}
