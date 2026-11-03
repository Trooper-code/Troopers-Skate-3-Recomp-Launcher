// Trooper's Skate 3 Recomp Launcher — Modding tools page and Mods-page format-1 row rendering.
// Part of MainForm (partial class). Written for the C# 5 compiler in build.cmd.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Skate3Launcher
{
    // Mods-page row that adds an optional preview thumbnail and an optional ⚠ warning line to ItemRow.
    class ModItemRow : ItemRow
    {
        public readonly Label Warn;
        Image preview;

        public ModItemRow(string main, string sub, string previewPath, IList<string> warnings, ToolTip tips)
            : base(main, sub)
        {
            if (!string.IsNullOrEmpty(previewPath) && File.Exists(previewPath))
            {
                try
                {
                    using (var fs = new FileStream(previewPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var img = Image.FromStream(fs))
                        preview = new Bitmap(img);
                }
                catch { preview = null; }
            }

            if (warnings != null && warnings.Count > 0)
            {
                Height = Dpi.S(72);
                Warn = new TextLabel
                {
                    Text = "\u26A0  " + string.Join("  \u00B7  ", warnings),
                    Font = Theme.Small,
                    ForeColor = Theme.Warn,
                    AutoSize = true,
                    UseMnemonic = false
                };
                Controls.Add(Warn);
                if (tips != null) tips.SetToolTip(Warn, string.Join("\n", warnings));
            }
            else if (preview != null)
            {
                Height = Dpi.S(58);
            }

            Resize += delegate { ArrangeModRow(); };
            ArrangeModRow();
        }

        public new void AddButton(Control b)
        {
            base.AddButton(b);
            ArrangeModRow();
        }

        void ArrangeModRow()
        {
            int left = preview != null ? Dpi.S(56) : 0;
            int rightEdge = Width;
            for (int i = 0; i < Buttons.Count; i++)
                if (Buttons[i].Left < rightEdge) rightEdge = Buttons[i].Left;
            int maxW = Math.Max(Dpi.S(60), rightEdge - left - Dpi.S(10));

            Main.Location = new Point(left, Dpi.S(8));
            Sub.Location = new Point(left, Dpi.S(29));
            Main.MaximumSize = new Size(maxW, Dpi.S(20));
            Sub.MaximumSize = new Size(maxW, Dpi.S(18));
            if (Warn != null)
            {
                Warn.Location = new Point(left, Dpi.S(48));
                Warn.MaximumSize = new Size(maxW, Dpi.S(18));
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (preview == null) return;
            int w = Dpi.S(44), h = Dpi.S(44);
            int x = Dpi.S(2), y = (Height - h) / 2;
            var g = e.Graphics;
            var prevMode = g.InterpolationMode;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(preview, new Rectangle(x, y, w, h));
            g.InterpolationMode = prevMode;
            using (var p = new Pen(Theme.Border))
                g.DrawRectangle(p, x, y, w - 1, h - 1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && preview != null)
            {
                preview.Dispose();
                preview = null;
            }
            base.Dispose(disposing);
        }
    }

    partial class MainForm
    {
        RoundTextBox newModNameBox;
        Label newModStatus;

        ModItemRow CreateModRow(ModInfo m, string subText, IList<ModInfo> allMods)
        {
            string flavor = RecompFlavor.Detect(gameExe, configPath);
            string recompVer = RecompVersion.Current(Paths.LogsDir(gameExe), gameExe);
            var warnings = ModManifest.Warnings(m, allMods, flavor, recompVer);
            string title = (m.Enabled ? "\u25CF  " : "\u25CB  ") + m.Display + (string.IsNullOrEmpty(m.Version) ? "" : "  v" + m.Version.TrimStart('v'));
            return new ModItemRow(title, subText, m.PreviewPath, warnings, tips);
        }

        void BuildModdingToolsPage()
        {
            var page = new StackPage();

            var start = new ToolCard("Start a new mod",
                "Creates a folder in mods\\ with a format-1 mod.json, an empty data\\ folder for your replacement game files, and a short README.txt, then opens the folder in Explorer.");
            var nameCap = MkLabel("Mod name", Theme.Small, Theme.Muted);
            newModNameBox = new RoundTextBox { Width = D(300) };
            var createBtn = MkButton("Create mod folder", "\uE710", ButtonStyle.Secondary, null);
            newModStatus = MkLabel("", Theme.Small, Theme.Muted);
            createBtn.Click += delegate { CreateNewModFolder(); };
            newModNameBox.Box.KeyDown += (o, e) =>
            {
                if (e.KeyCode == Keys.Enter)
                {
                    e.SuppressKeyPress = true;
                    CreateNewModFolder();
                }
            };
            start.Controls.AddRange(new Control[] { nameCap, newModNameBox, createBtn, newModStatus });
            start.Body = (w, top) =>
            {
                nameCap.Location = new Point(D(20), top);
                newModNameBox.SetBounds(D(20), nameCap.Bottom + D(4), Math.Min(D(320), Math.Max(D(160), w - createBtn.Width - D(52))), D(34));
                createBtn.Location = new Point(newModNameBox.Right + D(8), newModNameBox.Top - 1);
                int bottom = Math.Max(newModNameBox.Bottom, createBtn.Bottom);
                if (newModStatus.Text.Length > 0)
                {
                    newModStatus.MaximumSize = new Size(w - D(40), 0);
                    newModStatus.Location = new Point(D(20), bottom + D(10));
                    bottom = newModStatus.Bottom;
                }
                return bottom;
            };
            page.Add(start);

            AddPage("Modding tools", "Make, test and package mods.", "\uE90F", "TOOLS", false, page);
        }

        void CreateNewModFolder()
        {
            string cleanName;
            string err = ModCreator.ValidateName(gameExe, newModNameBox != null ? newModNameBox.Box.Text : "", out cleanName);
            if (err != null)
            {
                if (newModStatus != null) { newModStatus.Text = err; newModStatus.ForeColor = Theme.Warn; }
                Flash(err, Theme.Warn);
                RelayoutCurrent();
                return;
            }
            try
            {
                string dir = ModCreator.Create(gameExe, cleanName);
                if (newModNameBox != null) newModNameBox.Box.Text = "";
                if (newModStatus != null) { newModStatus.Text = "Created " + dir + "."; newModStatus.ForeColor = Theme.Good; }
                Flash("Created mod folder \"" + cleanName + "\".", Theme.Good);
                RefreshMods();
                OpenFolder(dir);
            }
            catch (Exception ex)
            {
                if (newModStatus != null) { newModStatus.Text = ex.Message; newModStatus.ForeColor = Theme.Warn; }
                MessageBox.Show(this, "Couldn't create the mod folder:\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RelayoutCurrent();
        }

        void AddLauncherModdingCard()
        {
            var info = pageInfos.FirstOrDefault(p => p.Name == "Launcher");
            if (info == null) return;
            var page = info.Page;

            var card = new ToolCard("Modding tools", "Adds a Modding tools page for making and testing mods.");
            Label restartNote = MkLabel("", Theme.Small, Theme.Accent);
            var toggle = MkToggle("modding_tools", false, delegate
            {
                restartNote.Text = "Restart the launcher to show or hide the page.";
                Flash("Restart the launcher to show or hide the page.", Theme.Accent);
                RelayoutCurrent();
            });
            var label = MkLabel("Show modding tools", Theme.Base, Theme.Text);
            label.Cursor = Cursors.Hand;
            label.Click += delegate { toggle.Checked = !toggle.Checked; };
            card.Controls.AddRange(new Control[] { toggle, label, restartNote });
            card.Body = (w, top) =>
            {
                int y = ToggleLine(D(20), top, toggle, label);
                if (restartNote.Text.Length > 0)
                {
                    restartNote.MaximumSize = new Size(w - D(40), 0);
                    restartNote.Location = new Point(D(20), y + D(8));
                    y = restartNote.Bottom;
                }
                return y;
            };

            int at = Math.Max(0, page.Cards.Count - 1);
            page.Cards.Insert(at, card);
            page.Controls.Add(card);
        }
    }
}
