// Trooper's Skate 3 Recomp Launcher — Modding tools page and Mods-page format-1 row rendering.
// Part of MainForm (partial class). Written for the C# 5 compiler in build.cmd.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Skate3Launcher
{
    // Mods-page row that adds an optional preview thumbnail and an optional ⚠ warning line to ItemRow.
    class ModItemRow : ItemRow
    {
        public readonly Label Warn;
        Image preview;

        public Size PreviewMemorySize { get { return preview != null ? preview.Size : Size.Empty; } }

        public ModItemRow(string main, string sub, string previewPath, IList<string> warnings, ToolTip tips)
            : base(main, sub)
        {
            if (!string.IsNullOrEmpty(previewPath) && File.Exists(previewPath))
            {
                try
                {
                    using (var fs = new FileStream(previewPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                    using (var img = Image.FromStream(fs))
                    {
                        int sz = Math.Max(88, Dpi.S(44) * 2);
                        var thumb = new Bitmap(sz, sz);
                        using (var g = Graphics.FromImage(thumb))
                        {
                            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                            g.SmoothingMode = SmoothingMode.HighQuality;
                            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                            g.DrawImage(img, new Rectangle(0, 0, sz, sz));
                        }
                        preview = thumb;
                    }
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

    // Owner-drawn list for ModValidator results.
    class ModCheckView : ListBox
    {
        public readonly List<ModCheckResult> Results = new List<ModCheckResult>();

        public ModCheckView()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Small;
            ItemHeight = Dpi.S(22);
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
            ModCheckSeverity sev = e.Index < Results.Count ? Results[e.Index].Severity : ModCheckSeverity.Info;
            Color c = sev == ModCheckSeverity.Error ? Theme.Error :
                      sev == ModCheckSeverity.Warning ? Theme.Warn :
                      Theme.Muted;
            TextRenderer.DrawText(e.Graphics, line, Font, new Point(e.Bounds.X + Dpi.S(8), e.Bounds.Y + Dpi.S(2)), c, Gfx.Flat);
        }
    }

    // Owner-drawn checklist for ModFromChanges.Compare results (Changed and New files can be ticked; Skipped items are greyed out).
    class ModChangesView : ListBox
    {
        public class RowItem
        {
            public ModChangeEntry Entry;
            public bool Checked;
        }

        public readonly List<RowItem> Rows = new List<RowItem>();
        public event EventHandler CheckedChanged;

        public ModChangesView()
        {
            DrawMode = DrawMode.OwnerDrawFixed;
            BorderStyle = BorderStyle.None;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Small;
            ItemHeight = Dpi.S(24);
            HorizontalScrollbar = true;
            IntegralHeight = false;
            SelectionMode = SelectionMode.One;
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.DarkTheme(this, "DarkMode_Explorer"); }

        public void SetEntries(IEnumerable<ModChangeEntry> entries)
        {
            BeginUpdate();
            Items.Clear();
            Rows.Clear();
            if (entries != null)
            {
                foreach (var entry in entries)
                {
                    if (entry == null || entry.Status == ModChangeStatus.Unchanged) continue;
                    bool selectable = entry.Status == ModChangeStatus.Changed || entry.Status == ModChangeStatus.New;
                    var row = new RowItem { Entry = entry, Checked = selectable };
                    Rows.Add(row);
                    Items.Add(FormatRowText(entry));
                }
            }
            EndUpdate();
        }

        static string FormatRowText(ModChangeEntry e)
        {
            if (e.Status == ModChangeStatus.Skipped)
                return "[Skipped]  " + e.RelativePath + "  —  " + (e.SkipReason ?? "skipped");
            string tag = e.Status == ModChangeStatus.New ? "[New]      " : "[Changed]  ";
            return tag + e.RelativePath + "  (" + Paths.Size(e.Size) + ")";
        }

        void ToggleRow(int index)
        {
            if (index < 0 || index >= Rows.Count) return;
            var r = Rows[index];
            if (r.Entry.Status == ModChangeStatus.Skipped) return;
            r.Checked = !r.Checked;
            Invalidate(GetItemRectangle(index));
            if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            int idx = IndexFromPoint(e.Location);
            if (idx >= 0 && idx < Rows.Count) ToggleRow(idx);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space && SelectedIndex >= 0)
            {
                ToggleRow(SelectedIndex);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            base.OnKeyDown(e);
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= Rows.Count) return;
            var row = Rows[e.Index];
            var entry = row.Entry;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            using (var b = new SolidBrush(sel ? Theme.NavSel : BackColor))
                e.Graphics.FillRectangle(b, e.Bounds);

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int boxSz = Dpi.S(14);
            int boxX = e.Bounds.X + Dpi.S(8);
            int boxY = e.Bounds.Y + (e.Bounds.Height - boxSz) / 2;
            var boxRect = new RectangleF(boxX, boxY, boxSz, boxSz);

            bool selectable = entry.Status == ModChangeStatus.Changed || entry.Status == ModChangeStatus.New;
            if (selectable)
            {
                Color fill = row.Checked ? Theme.Accent : Theme.Input;
                Color border = row.Checked ? Theme.Accent : Theme.Border;
                Gfx.FillRound(e.Graphics, fill, boxRect, Dpi.Sf(3));
                Gfx.DrawRound(e.Graphics, border, boxRect, Dpi.Sf(3), 1f);
                if (row.Checked)
                {
                    using (var pen = new Pen(Theme.Text, Math.Max(1.5f, Dpi.Sf(1.8f))))
                    {
                        e.Graphics.DrawLines(pen, new[]
                        {
                            new PointF(boxX + boxSz * 0.22f, boxY + boxSz * 0.52f),
                            new PointF(boxX + boxSz * 0.44f, boxY + boxSz * 0.74f),
                            new PointF(boxX + boxSz * 0.78f, boxY + boxSz * 0.28f)
                        });
                    }
                }
            }
            else
            {
                Gfx.FillRound(e.Graphics, Theme.Card, boxRect, Dpi.Sf(3));
                Gfx.DrawRound(e.Graphics, Theme.CardBorder, boxRect, Dpi.Sf(3), 1f);
            }

            Color textColor = entry.Status == ModChangeStatus.Skipped ? Theme.Muted :
                              !row.Checked ? Theme.Muted :
                              entry.Status == ModChangeStatus.New ? Theme.Good :
                              Theme.Text;
            string line = (string)Items[e.Index];
            TextRenderer.DrawText(e.Graphics, line, Font, new Point(boxX + boxSz + Dpi.S(8), e.Bounds.Y + Dpi.S(3)), textColor, Gfx.Flat);
        }
    }

    partial class MainForm
    {
        RoundTextBox newModNameBox;
        Label newModStatus;

        DarkCombo modCheckCombo;
        PillButton checkModBtn, removeIdenticalBtn, addModJsonBtn, packageZipBtn;
        Label modCheckStatus;
        ModCheckView modCheckView;
        List<ModInfo> moddingModList = new List<ModInfo>();
        List<ModCheckResult> lastCheckResults;
        string lastCheckedModDir;
        bool modCheckRunning, modCheckCancel, updatingModCheckCombo;

        RoundTextBox workDirBox, fromChangesNameBox;
        PillButton browseWorkBtn, compareWorkBtn, createFromChangesBtn;
        Label fromChangesStatus;
        ModChangesView modChangesView;
        List<ModChangeEntry> lastCompareResults;
        string lastComparedWorkDir;
        bool compareRunning, compareCancel;

        ModItemRow CreateModRow(ModInfo m, string subText, IList<ModInfo> allMods)
        {
            string flavor = RecompFlavor.Detect(gameExe, configPath);
            string recompVer = RecompVersion.Current(Paths.LogsDir(gameExe), gameExe);
            return CreateModRow(m, subText, allMods, flavor, recompVer);
        }

        ModItemRow CreateModRow(ModInfo m, string subText, IList<ModInfo> allMods, string flavor, string recompVer)
        {
            var warnings = ModManifest.Warnings(m, allMods, flavor, recompVer);
            string title = (m.Enabled ? "\u25CF  " : "\u25CB  ") + m.Display + (string.IsNullOrEmpty(m.Version) ? "" : "  v" + m.Version.TrimStart('v'));
            var row = new ModItemRow(title, subText, m.PreviewPath, warnings, tips);
            EnableModsDragDrop(row);
            return row;
        }

        // Enables drag-and-drop installation of .zip files and mod folders onto the Mods page.
        void EnableModsDragDrop(params Control[] controls)
        {
            foreach (var c in controls)
            {
                if (c == null) continue;
                c.AllowDrop = true;
                c.DragEnter += OnModsDragEnter;
                c.DragDrop += OnModsDragDrop;
            }
        }

        static string[] ExtractDroppedModSources(IDataObject data)
        {
            if (data == null || !data.GetDataPresent(DataFormats.FileDrop)) return new string[0];
            var paths = data.GetData(DataFormats.FileDrop) as string[];
            if (paths == null) return new string[0];
            return paths.Where(p => !string.IsNullOrEmpty(p) &&
                (Directory.Exists(p) || (File.Exists(p) && p.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))).ToArray();
        }

        void OnModsDragEnter(object sender, DragEventArgs e)
        {
            var valid = ExtractDroppedModSources(e.Data);
            e.Effect = valid.Length > 0 ? DragDropEffects.Copy : DragDropEffects.None;
        }

        void OnModsDragDrop(object sender, DragEventArgs e)
        {
            var valid = ExtractDroppedModSources(e.Data);
            if (valid.Length > 0) InstallMods(valid);
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

            // ---- M2: Check and package a mod
            var checkCard = new ToolCard("Check and package a mod",
                "Checks a mod for errors (no usable files, protected executables, escaping paths), warnings (whole 20 MB+ game archives, unchanged copies of game files, PS3 files, unknown paths) and missing metadata, then packages it into <Name>-<version>.zip on your Desktop.");
            var modCap = MkLabel("Mod", Theme.Small, Theme.Muted);
            modCheckCombo = new DarkCombo { Width = D(320) };
            checkModBtn = MkButton("Check mod", "\uE73E", ButtonStyle.Secondary, "Run validator checks on the selected mod.");
            modCheckStatus = MkLabel("", Theme.Base, Theme.Text);
            modCheckView = new ModCheckView { Visible = false };
            modCheckView.KeyDown += (o, e) =>
            {
                if (e.Control && e.KeyCode == Keys.C && modCheckView.SelectedItems.Count > 0)
                {
                    try { Clipboard.SetText(string.Join(Environment.NewLine, modCheckView.SelectedItems.Cast<string>())); } catch { }
                    e.SuppressKeyPress = true;
                }
            };
            packageZipBtn = MkButton("Package as .zip", "\uE7B8", ButtonStyle.Primary, "Write <Name>-<version>.zip to your Desktop.");
            addModJsonBtn = MkButton("Add mod.json", "\uE710", ButtonStyle.Secondary, "Create or complete mod.json with format 1 defaults.");
            removeIdenticalBtn = MkButton("Remove identical files", "\uE74D", ButtonStyle.Ghost, "Delete files in this mod that are byte-for-byte identical to the original files in game\\.");

            modCheckCombo.SelectedIndexChanged += delegate
            {
                if (updatingModCheckCombo) return;
                ClearModCheckResults();
                UpdateModCheckButtons();
                RelayoutCurrent();
            };
            checkModBtn.Click += delegate { StartOrCancelModCheck(); };
            removeIdenticalBtn.Click += delegate { RemoveIdenticalModFiles(); };
            addModJsonBtn.Click += delegate { AddModJsonToSelectedMod(); };
            packageZipBtn.Click += delegate { PackageSelectedMod(); };

            checkCard.Controls.AddRange(new Control[] { modCap, modCheckCombo, checkModBtn, modCheckStatus, modCheckView, packageZipBtn, addModJsonBtn, removeIdenticalBtn });
            checkCard.Body = (w, top) =>
            {
                modCap.Location = new Point(D(20), top);
                modCheckCombo.SetBounds(D(20), modCap.Bottom + D(4), Math.Min(D(340), Math.Max(D(180), w - checkModBtn.Width - D(52))), D(32));
                checkModBtn.Location = new Point(modCheckCombo.Right + D(8), modCheckCombo.Top - 2);
                int y = Math.Max(modCheckCombo.Bottom, checkModBtn.Bottom);
                if (modCheckStatus.Text.Length > 0)
                {
                    modCheckStatus.MaximumSize = new Size(w - D(40), 0);
                    modCheckStatus.Location = new Point(D(20), y + D(12));
                    y = modCheckStatus.Bottom;
                }
                if (modCheckView.Visible)
                {
                    int listH = Math.Min(D(220), Math.Max(D(66), modCheckView.Items.Count * modCheckView.ItemHeight + D(12)));
                    modCheckView.SetBounds(D(20), y + D(10), w - D(40), listH);
                    y = modCheckView.Bottom;
                }
                return Row(D(20), y + D(12), packageZipBtn, addModJsonBtn, removeIdenticalBtn);
            };
            page.Add(checkCard);

            // ---- M3: Create a mod from changed files
            var fromChangesCard = new ToolCard("Create a mod from changed files",
                "Compare an edited copy of the game\\ folder against your clean game\\ folder, tick the changed or new files you want to ship, and build a new mod in mods\\.");
            var workCap = MkLabel("Work folder (your edited copy of game\\)", Theme.Small, Theme.Muted);
            workDirBox = new RoundTextBox { Width = D(360) };
            browseWorkBtn = MkButton("Browse…", "\uE8B7", ButtonStyle.Secondary, "Pick your edited copy of the game\\ folder.");
            var fromNameCap = MkLabel("Mod name", Theme.Small, Theme.Muted);
            fromChangesNameBox = new RoundTextBox { Width = D(280) };
            compareWorkBtn = MkButton("Compare", "\uE721", ButtonStyle.Secondary, "Compare the work folder against game\\.");
            fromChangesStatus = MkLabel("Pick a work folder and press Compare.", Theme.Base, Theme.Muted);
            modChangesView = new ModChangesView { Visible = false };
            createFromChangesBtn = MkButton("Create mod", "\uE710", ButtonStyle.Primary, "Copy the ticked files into mods\\<name>\\ with a format-1 mod.json and run Check mod.");

            browseWorkBtn.Click += delegate { BrowseWorkFolder(); };
            workDirBox.Box.TextChanged += delegate
            {
                if (lastComparedWorkDir != null && !string.Equals(workDirBox.Box.Text.Trim(), lastComparedWorkDir, StringComparison.OrdinalIgnoreCase))
                    ClearFromChangesResults();
                UpdateFromChangesButtons();
                RelayoutCurrent();
            };
            fromChangesNameBox.Box.TextChanged += delegate { UpdateFromChangesButtons(); };
            compareWorkBtn.Click += delegate { StartOrCancelCompare(); };
            modChangesView.CheckedChanged += delegate { UpdateFromChangesSummaryAndButtons(); };
            createFromChangesBtn.Click += delegate { CreateModFromChanges(); };

            fromChangesCard.Controls.AddRange(new Control[]
            {
                workCap, workDirBox, browseWorkBtn,
                fromNameCap, fromChangesNameBox, compareWorkBtn,
                fromChangesStatus, modChangesView, createFromChangesBtn
            });
            fromChangesCard.Body = (w, top) =>
            {
                workCap.Location = new Point(D(20), top);
                int workBoxW = Math.Max(D(180), w - browseWorkBtn.Width - D(48));
                workDirBox.SetBounds(D(20), workCap.Bottom + D(4), workBoxW, D(34));
                browseWorkBtn.Location = new Point(workDirBox.Right + D(8), workDirBox.Top - 1);
                int y = Math.Max(workDirBox.Bottom, browseWorkBtn.Bottom) + D(10);

                fromNameCap.Location = new Point(D(20), y);
                int nameBoxW = Math.Min(D(320), Math.Max(D(160), w - compareWorkBtn.Width - D(48)));
                fromChangesNameBox.SetBounds(D(20), fromNameCap.Bottom + D(4), nameBoxW, D(34));
                compareWorkBtn.Location = new Point(fromChangesNameBox.Right + D(8), fromChangesNameBox.Top - 1);
                y = Math.Max(fromChangesNameBox.Bottom, compareWorkBtn.Bottom);

                if (fromChangesStatus.Text.Length > 0)
                {
                    fromChangesStatus.MaximumSize = new Size(w - D(40), 0);
                    fromChangesStatus.Location = new Point(D(20), y + D(12));
                    y = fromChangesStatus.Bottom;
                }
                if (modChangesView.Visible)
                {
                    int listH = Math.Min(D(220), Math.Max(D(66), modChangesView.Items.Count * modChangesView.ItemHeight + D(12)));
                    modChangesView.SetBounds(D(20), y + D(10), w - D(40), listH);
                    y = modChangesView.Bottom;
                }
                return Row(D(20), y + D(12), createFromChangesBtn);
            };
            page.Add(fromChangesCard);

            var info = AddPage("Modding tools", "Make, test and package mods.", "\uE90F", "TOOLS", false, page);
            info.OnShow = RefreshModdingToolsPage;
        }

        ModInfo SelectedModForCheck()
        {
            if (modCheckCombo == null || modCheckCombo.SelectedIndex < 0 || modCheckCombo.SelectedIndex >= moddingModList.Count)
                return null;
            return moddingModList[modCheckCombo.SelectedIndex];
        }

        void RefreshModdingToolsPage()
        {
            if (modCheckCombo == null) return;
            var prev = SelectedModForCheck();
            string prevDir = prev != null ? prev.Dir : lastCheckedModDir;
            moddingModList = gameExe != null ? Mods.List(gameExe) : new List<ModInfo>();

            updatingModCheckCombo = true;
            modCheckCombo.Items.Clear();
            int sel = -1;
            for (int i = 0; i < moddingModList.Count; i++)
            {
                var m = moddingModList[i];
                string label = m.Display + (string.IsNullOrEmpty(m.Version) ? "" : "  v" + m.Version.TrimStart('v', 'V')) +
                    "  (" + m.Files.Count + " file" + (m.Files.Count == 1 ? "" : "s") + ")";
                modCheckCombo.Items.Add(label);
                if (prevDir != null && string.Equals(m.Dir, prevDir, StringComparison.OrdinalIgnoreCase))
                    sel = i;
            }
            if (moddingModList.Count == 0)
            {
                modCheckCombo.Items.Add("No mods installed in mods\\");
                modCheckCombo.SelectedIndex = 0;
                modCheckCombo.Enabled = false;
            }
            else
            {
                modCheckCombo.Enabled = !modCheckRunning;
                modCheckCombo.SelectedIndex = sel >= 0 ? sel : 0;
            }
            updatingModCheckCombo = false;

            var now = SelectedModForCheck();
            if (now == null || lastCheckedModDir == null || !string.Equals(now.Dir, lastCheckedModDir, StringComparison.OrdinalIgnoreCase))
                ClearModCheckResults();

            UpdateModCheckButtons();
            UpdateFromChangesButtons();
            RelayoutCurrent();
        }

        void ClearModCheckResults()
        {
            lastCheckResults = null;
            lastCheckedModDir = null;
            if (modCheckView != null)
            {
                modCheckView.Items.Clear();
                modCheckView.Results.Clear();
                modCheckView.Visible = false;
            }
            if (modCheckStatus != null && !modCheckRunning)
            {
                modCheckStatus.Text = moddingModList.Count == 0
                    ? (gameExe == null ? "Set the game location on the Launcher page first." : "Create or install a mod first, then pick it above and press Check mod.")
                    : "Pick a mod above and press Check mod.";
                modCheckStatus.ForeColor = Theme.Muted;
            }
        }

        void UpdateModCheckButtons()
        {
            if (checkModBtn == null) return;
            var sel = SelectedModForCheck();
            bool hasMod = sel != null;
            bool checkedThisMod = hasMod && lastCheckResults != null && string.Equals(sel.Dir, lastCheckedModDir, StringComparison.OrdinalIgnoreCase);
            bool hasErrors = checkedThisMod && lastCheckResults.Any(r => r.Severity == ModCheckSeverity.Error);
            bool hasIdentical = checkedThisMod && lastCheckResults.Any(r => r.Kind == ModCheckKind.IdenticalToOriginal);
            bool needsModJson = hasMod && (checkedThisMod
                ? lastCheckResults.Any(r => r.Kind == ModCheckKind.MissingOrIncompleteModJson)
                : (!File.Exists(Path.Combine(sel.Dir, "mod.json")) || string.IsNullOrEmpty(sel.Title) || string.IsNullOrEmpty(sel.Version)));

            checkModBtn.Text = modCheckRunning ? "Cancel" : "Check mod";
            checkModBtn.Enabled = hasMod || modCheckRunning;
            modCheckCombo.Enabled = moddingModList.Count > 0 && !modCheckRunning;
            packageZipBtn.Enabled = !modCheckRunning && checkedThisMod && !hasErrors;
            addModJsonBtn.Enabled = !modCheckRunning && needsModJson;
            removeIdenticalBtn.Enabled = !modCheckRunning && hasIdentical && !hasErrors;
        }

        void StartOrCancelModCheck()
        {
            if (modCheckRunning)
            {
                modCheckCancel = true;
                return;
            }
            var m = SelectedModForCheck();
            if (m == null) return;

            modCheckRunning = true;
            modCheckCancel = false;
            if (modCheckView != null)
            {
                modCheckView.Items.Clear();
                modCheckView.Results.Clear();
                modCheckView.Visible = false;
            }
            modCheckStatus.Text = "Checking " + m.Display + "…";
            modCheckStatus.ForeColor = Theme.Muted;
            UpdateModCheckButtons();
            RelayoutCurrent();

            string modDir = m.Dir;
            string exe = gameExe;
            DateTime lastUi = DateTime.MinValue;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<ModCheckResult> results = null;
                bool cancelled = false;
                string fatalError = null;
                try
                {
                    results = ModValidator.Validate(modDir, exe, (done, total, file) =>
                    {
                        if ((DateTime.UtcNow - lastUi).TotalMilliseconds < 80) return;
                        lastUi = DateTime.UtcNow;
                        int pct = total > 0 ? (int)Math.Min(99L, done * 100L / total) : 0;
                        Ui(delegate
                        {
                            if (!modCheckRunning) return;
                            modCheckStatus.Text = "Hashing " + file + "… " + pct.ToString(CultureInfo.InvariantCulture) + "%";
                        });
                    }, () => modCheckCancel);
                }
                catch (OperationCanceledException) { cancelled = true; }
                catch (Exception ex) { fatalError = ex.Message; }

                Ui(delegate
                {
                    modCheckRunning = false;
                    if (cancelled)
                    {
                        modCheckStatus.Text = "Check cancelled.";
                        modCheckStatus.ForeColor = Theme.Muted;
                        UpdateModCheckButtons();
                        RelayoutCurrent();
                        return;
                    }
                    if (fatalError != null)
                    {
                        modCheckStatus.Text = "Couldn't check the mod: " + fatalError;
                        modCheckStatus.ForeColor = Theme.Warn;
                        UpdateModCheckButtons();
                        RelayoutCurrent();
                        return;
                    }
                    ApplyModCheckResults(modDir, results ?? new List<ModCheckResult>());
                });
            });
        }

        void ApplyModCheckResults(string modDir, List<ModCheckResult> results)
        {
            lastCheckedModDir = modDir;
            lastCheckResults = results;

            int errors = results.Count(r => r.Severity == ModCheckSeverity.Error);
            int warnings = results.Count(r => r.Severity == ModCheckSeverity.Warning);
            int infos = results.Count(r => r.Severity == ModCheckSeverity.Info);

            modCheckView.Items.Clear();
            modCheckView.Results.Clear();
            foreach (var r in results)
            {
                string prefix = r.Severity == ModCheckSeverity.Error ? "\u2716  [Error]   " :
                                r.Severity == ModCheckSeverity.Warning ? "\u26A0  [Warning] " :
                                "\u2139  [Info]    ";
                modCheckView.Results.Add(r);
                modCheckView.Items.Add(prefix + r.Message);
            }
            modCheckView.Visible = results.Count > 0;

            string summary = errors + " error" + (errors == 1 ? "" : "s") + ", " +
                             warnings + " warning" + (warnings == 1 ? "" : "s") + ", " +
                             infos + " info note" + (infos == 1 ? "" : "s") + ". ";
            if (errors > 0)
            {
                modCheckStatus.Text = summary + "Fix the errors below before packaging.";
                modCheckStatus.ForeColor = Theme.Error;
            }
            else if (warnings > 0)
            {
                modCheckStatus.Text = summary + "Ready to package (review the warnings below first).";
                modCheckStatus.ForeColor = Theme.Warn;
            }
            else
            {
                modCheckStatus.Text = summary + "Everything looks good — ready to package.";
                modCheckStatus.ForeColor = Theme.Good;
            }

            UpdateModCheckButtons();
            RelayoutCurrent();
        }

        void RemoveIdenticalModFiles()
        {
            var m = SelectedModForCheck();
            if (m == null || lastCheckResults == null) return;
            if (lastCheckResults.Any(r => r.Severity == ModCheckSeverity.Error)) return;
            if (GameRunningGuard("modifying a mod")) return;

            var identical = lastCheckResults.Where(r => r.Kind == ModCheckKind.IdenticalToOriginal).ToList();
            if (identical.Count == 0) return;

            string previewList = string.Join("\n", identical.Take(8).Select(r => "  \u2022 " + (r.FilePath ?? Path.GetFileName(r.FullPath))));
            if (identical.Count > 8) previewList += "\n  \u2022 …and " + (identical.Count - 8) + " more";

            if (DarkDialog.Ask(this,
                    "Remove " + identical.Count + " identical file" + (identical.Count == 1 ? "" : "s") + " from " + m.Display + "?",
                    "These files in " + m.Dir + " have the exact same SHA-256 hash as the originals in game\\, so they do nothing:\n\n" +
                    previewList + "\n\nOnly the copies inside this mod's folder will be deleted. Your game files are never touched.",
                    "Remove files", "Cancel") != 0)
                return;

            try
            {
                int removed = ModValidator.RemoveIdenticalFiles(m, gameExe, identical);
                Flash("Removed " + removed + " identical file" + (removed == 1 ? "" : "s") + " from " + m.Display + ".", Theme.Good);
                RefreshMods();
                RefreshModdingToolsPage();
                StartOrCancelModCheck();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't remove the files:\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void AddModJsonToSelectedMod()
        {
            var m = SelectedModForCheck();
            if (m == null) return;
            try
            {
                string path = ModValidator.EnsureModJson(m);
                Flash("Wrote " + path + ".", Theme.Good);
                RefreshMods();
                RefreshModdingToolsPage();
                StartOrCancelModCheck();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't write mod.json:\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void PackageSelectedMod()
        {
            var m = SelectedModForCheck();
            if (m == null) return;
            if (lastCheckResults == null || !string.Equals(m.Dir, lastCheckedModDir, StringComparison.OrdinalIgnoreCase))
            {
                Flash("Run Check mod first before packaging.", Theme.Warn);
                return;
            }
            if (lastCheckResults.Any(r => r.Severity == ModCheckSeverity.Error))
            {
                MessageBox.Show(this, "Fix the errors found by Check mod before packaging this mod.", AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            ModInfo fresh = Mods.Read(m.Dir);
            string zipPath = ModPackager.DesktopZipPath(fresh);
            if (File.Exists(zipPath))
            {
                if (DarkDialog.Ask(this,
                        "Overwrite " + Path.GetFileName(zipPath) + "?",
                        Path.GetFileName(zipPath) + " already exists on your Desktop. Replace it?",
                        "Overwrite", "Cancel") != 0)
                    return;
            }

            try
            {
                ModPackager.Package(fresh, zipPath, lastCheckResults);
                Flash("Saved " + Path.GetFileName(zipPath) + " to your Desktop.", Theme.Good);
                try { Process.Start("explorer.exe", "/select,\"" + zipPath + "\""); } catch { }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Couldn't package the mod:\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
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
                lastCheckedModDir = dir;
                RefreshMods();
                RefreshModdingToolsPage();
                OpenFolder(dir);
            }
            catch (Exception ex)
            {
                if (newModStatus != null) { newModStatus.Text = ex.Message; newModStatus.ForeColor = Theme.Warn; }
                MessageBox.Show(this, "Couldn't create the mod folder:\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RelayoutCurrent();
        }

        // ---- M3 handlers: Create a mod from changed files

        void BrowseWorkFolder()
        {
            if (compareRunning) return;
            using (var dlg = new FolderBrowserDialog { Description = "Pick your edited copy of the game\\ folder (not the game's own game\\ folder)." })
            {
                string cur = workDirBox != null ? workDirBox.Box.Text.Trim() : "";
                if (Directory.Exists(cur)) dlg.SelectedPath = cur;
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string chosen = dlg.SelectedPath;
                string resolved;
                if (TryResolveAndValidateWorkFolder(chosen, out resolved))
                    workDirBox.Box.Text = resolved;
            }
        }

        bool TryResolveAndValidateWorkFolder(string rawWorkDir, out string resolvedWorkDir)
        {
            resolvedWorkDir = (rawWorkDir ?? "").Trim();
            string suggested;
            string err = ModFromChanges.ValidateWorkDir(resolvedWorkDir, gameExe, out suggested);
            if (err != null)
            {
                if (fromChangesStatus != null) { fromChangesStatus.Text = err; fromChangesStatus.ForeColor = Theme.Warn; }
                MessageBox.Show(this, err, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                RelayoutCurrent();
                return false;
            }
            if (!string.IsNullOrEmpty(suggested))
            {
                if (DarkDialog.Ask(this,
                        "Use the game\\ subfolder?",
                        "That folder isn't a game\\ copy itself, but it has a game\\ subfolder holding default.xex:\n\n" +
                        suggested + "\n\nCompare that game\\ subfolder instead?",
                        "Use game\\ subfolder", "Use chosen folder") == 0)
                {
                    resolvedWorkDir = suggested;
                }
            }
            return true;
        }

        void ClearFromChangesResults()
        {
            lastCompareResults = null;
            lastComparedWorkDir = null;
            if (modChangesView != null)
            {
                modChangesView.SetEntries(null);
                modChangesView.Visible = false;
            }
            if (fromChangesStatus != null && !compareRunning)
            {
                fromChangesStatus.Text = "Pick a work folder and press Compare.";
                fromChangesStatus.ForeColor = Theme.Muted;
            }
        }

        void UpdateFromChangesButtons()
        {
            if (compareWorkBtn == null) return;
            string workText = workDirBox != null ? workDirBox.Box.Text.Trim() : "";
            string nameText = fromChangesNameBox != null ? fromChangesNameBox.Box.Text.Trim() : "";
            bool hasCompared = lastCompareResults != null && !string.IsNullOrEmpty(lastComparedWorkDir) &&
                               string.Equals(workText, lastComparedWorkDir, StringComparison.OrdinalIgnoreCase);
            bool anyTicked = hasCompared && modChangesView != null && modChangesView.Rows.Any(r => r.Checked &&
                             (r.Entry.Status == ModChangeStatus.Changed || r.Entry.Status == ModChangeStatus.New));

            compareWorkBtn.Text = compareRunning ? "Cancel" : "Compare";
            compareWorkBtn.Enabled = compareRunning || workText.Length > 0;
            if (browseWorkBtn != null) browseWorkBtn.Enabled = !compareRunning;
            if (workDirBox != null) workDirBox.Enabled = !compareRunning;
            if (createFromChangesBtn != null) createFromChangesBtn.Enabled = !compareRunning && anyTicked && nameText.Length > 0;
        }

        void UpdateFromChangesSummaryAndButtons()
        {
            if (lastCompareResults != null && fromChangesStatus != null)
            {
                int changed = lastCompareResults.Count(e => e.Status == ModChangeStatus.Changed);
                int added = lastCompareResults.Count(e => e.Status == ModChangeStatus.New);
                int unchanged = lastCompareResults.Count(e => e.Status == ModChangeStatus.Unchanged);
                int skipped = lastCompareResults.Count(e => e.Status == ModChangeStatus.Skipped);
                var ticked = modChangesView != null
                    ? modChangesView.Rows.Where(r => r.Checked && (r.Entry.Status == ModChangeStatus.Changed || r.Entry.Status == ModChangeStatus.New)).ToList()
                    : new List<ModChangesView.RowItem>();
                long tickedBytes = ticked.Sum(r => r.Entry.Size);

                string summary = changed + " changed, " + added + " new, " + unchanged + " unchanged, " + skipped + " skipped.";
                if (changed + added == 0)
                {
                    fromChangesStatus.Text = summary + " No changed or new files were found.";
                    fromChangesStatus.ForeColor = Theme.Muted;
                }
                else
                {
                    fromChangesStatus.Text = summary + "  (" + ticked.Count + " ticked, " + Paths.Size(tickedBytes) + ")";
                    fromChangesStatus.ForeColor = ticked.Count > 0 ? Theme.Good : Theme.Muted;
                }
            }
            UpdateFromChangesButtons();
            RelayoutCurrent();
        }

        void StartOrCancelCompare()
        {
            if (compareRunning)
            {
                compareCancel = true;
                return;
            }
            string rawWork = workDirBox != null ? workDirBox.Box.Text.Trim() : "";
            string workDir;
            if (!TryResolveAndValidateWorkFolder(rawWork, out workDir)) return;
            if (workDirBox != null && !string.Equals(workDirBox.Box.Text.Trim(), workDir, StringComparison.Ordinal))
                workDirBox.Box.Text = workDir;

            compareRunning = true;
            compareCancel = false;
            if (modChangesView != null)
            {
                modChangesView.SetEntries(null);
                modChangesView.Visible = false;
            }
            fromChangesStatus.Text = "Comparing " + workDir + " against game\\…";
            fromChangesStatus.ForeColor = Theme.Muted;
            UpdateFromChangesButtons();
            RelayoutCurrent();

            string exe = gameExe;
            DateTime lastUi = DateTime.MinValue;
            ThreadPool.QueueUserWorkItem(delegate
            {
                List<ModChangeEntry> entries = null;
                bool cancelled = false;
                string fatalError = null;
                try
                {
                    entries = ModFromChanges.Compare(workDir, exe, (done, total, file) =>
                    {
                        if ((DateTime.UtcNow - lastUi).TotalMilliseconds < 80) return;
                        lastUi = DateTime.UtcNow;
                        int pct = total > 0 ? (int)Math.Min(99L, done * 100L / total) : 0;
                        Ui(delegate
                        {
                            if (!compareRunning) return;
                            fromChangesStatus.Text = "Hashing " + file + "… " + pct.ToString(CultureInfo.InvariantCulture) + "%";
                        });
                    }, () => compareCancel);
                }
                catch (OperationCanceledException) { cancelled = true; }
                catch (Exception ex) { fatalError = ex.Message; }

                Ui(delegate
                {
                    compareRunning = false;
                    if (cancelled)
                    {
                        fromChangesStatus.Text = "Comparison cancelled.";
                        fromChangesStatus.ForeColor = Theme.Muted;
                        UpdateFromChangesButtons();
                        RelayoutCurrent();
                        return;
                    }
                    if (fatalError != null)
                    {
                        fromChangesStatus.Text = "Couldn't compare the work folder: " + fatalError;
                        fromChangesStatus.ForeColor = Theme.Warn;
                        UpdateFromChangesButtons();
                        RelayoutCurrent();
                        return;
                    }
                    ApplyCompareResults(workDir, entries ?? new List<ModChangeEntry>());
                });
            });
        }

        void ApplyCompareResults(string workDir, List<ModChangeEntry> entries)
        {
            lastComparedWorkDir = workDir;
            lastCompareResults = entries;
            if (modChangesView != null)
            {
                modChangesView.SetEntries(entries);
                modChangesView.Visible = modChangesView.Items.Count > 0;
            }
            UpdateFromChangesSummaryAndButtons();
        }

        void CreateModFromChanges()
        {
            if (compareRunning || lastCompareResults == null || modChangesView == null) return;
            if (GameRunningGuard("creating a mod")) return;

            string workDir = workDirBox != null ? workDirBox.Box.Text.Trim() : "";
            string cleanName;
            string nameErr = ModCreator.ValidateName(gameExe, fromChangesNameBox != null ? fromChangesNameBox.Box.Text : "", out cleanName);
            if (nameErr != null)
            {
                fromChangesStatus.Text = nameErr;
                fromChangesStatus.ForeColor = Theme.Warn;
                Flash(nameErr, Theme.Warn);
                RelayoutCurrent();
                return;
            }

            var ticked = modChangesView.Rows
                .Where(r => r.Checked && (r.Entry.Status == ModChangeStatus.Changed || r.Entry.Status == ModChangeStatus.New))
                .Select(r => r.Entry)
                .ToList();
            if (ticked.Count == 0)
            {
                Flash("Tick at least one changed or new file first.", Theme.Warn);
                return;
            }

            long totalBytes = ticked.Sum(e => e.Size);
            if (ticked.Count > ModFromChanges.LargeSelectionFileWarningCount || totalBytes > ModFromChanges.LargeSelectionBytesWarning)
            {
                if (DarkDialog.Ask(this,
                        "Create mod with " + ticked.Count + " files (" + Paths.Size(totalBytes) + ")?",
                        "That's a lot of the game. Ship only the files you changed.\n\nCreate \"" + cleanName + "\" with these " + ticked.Count + " files anyway?",
                        "Create mod", "Cancel") != 0)
                    return;
            }

            try
            {
                string createdDir = ModFromChanges.Create(gameExe, workDir, cleanName, ticked.Select(e => e.RelativePath));
                if (fromChangesNameBox != null) fromChangesNameBox.Box.Text = "";
                fromChangesStatus.Text = "Created " + createdDir + " (" + ticked.Count + " file" + (ticked.Count == 1 ? "" : "s") + ").";
                fromChangesStatus.ForeColor = Theme.Good;
                Flash("Created mod \"" + cleanName + "\" from " + ticked.Count + " file" + (ticked.Count == 1 ? "" : "s") + ".", Theme.Good);

                // Select the newly created mod in "Check and package a mod" and run Check mod on it automatically.
                lastCheckedModDir = createdDir;
                RefreshMods();
                RefreshModdingToolsPage();
                if (!modCheckRunning)
                    StartOrCancelModCheck();
            }
            catch (Exception ex)
            {
                fromChangesStatus.Text = ex.Message;
                fromChangesStatus.ForeColor = Theme.Warn;
                MessageBox.Show(this, "Couldn't create the mod:\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            UpdateFromChangesButtons();
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
