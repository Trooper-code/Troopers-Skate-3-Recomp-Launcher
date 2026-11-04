// Trooper's Skate 3 Recomp Launcher — v3.1 pages: Game files (setup, recomp updates, file check), Mods, Benchmark,
// Photo Mode extras, Steam / driver / crash-report cards, controller navigation and the "What's new" dialog.
// Part of MainForm (partial class). C# 5 only: build.cmd uses the compiler that ships with Windows.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace Skate3Launcher
{
    // Small dark dialog with a message and a row of buttons. ShowDialog returns DialogResult.OK; Choice is the button index (-1 = closed).
    class DarkDialog : Form
    {
        public int Choice = -1;

        public DarkDialog(string title, string heading, string message, string[] buttons, int width)
        {
            Text = title;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Base;
            int w = Dpi.S(width);
            var head = new TextLabel { Text = heading, Font = Theme.PageTitle, ForeColor = Theme.Text, AutoSize = true, UseMnemonic = false, Location = new Point(Dpi.S(28), Dpi.S(22)), MaximumSize = new Size(w - Dpi.S(56), 0) };
            var body = new TextLabel { Text = message, Font = Theme.Base, ForeColor = Theme.Muted, AutoSize = true, UseMnemonic = false, MaximumSize = new Size(w - Dpi.S(56), 0) };
            Controls.Add(head);
            Controls.Add(body);
            body.Location = new Point(Dpi.S(30), head.Bottom + Dpi.S(14));
            int y = body.Bottom + Dpi.S(24);
            int x = w - Dpi.S(28);
            var made = new List<PillButton>();
            for (int i = buttons.Length - 1; i >= 0; i--)
            {
                int idx = i;
                var b = new PillButton(buttons[i], null, i == 0 ? ButtonStyle.Primary : ButtonStyle.Secondary);
                b.Size = new Size(TextRenderer.MeasureText(buttons[i], b.Font).Width + Dpi.S(36), Dpi.S(38));
                x -= b.Width;
                b.Location = new Point(x, y);
                x -= Dpi.S(8);
                b.Click += delegate { Choice = idx; DialogResult = DialogResult.OK; Close(); };
                Controls.Add(b);
                made.Add(b);
            }
            ClientSize = new Size(w, y + Dpi.S(38) + Dpi.S(24));
            KeyPreview = true;
            KeyDown += (o, e) => { if (e.KeyCode == Keys.Escape) Close(); };
            Shown += delegate { if (made.Count > 0) made[made.Count - 1].Focus(); };
        }

        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); Native.StyleTitleBar(this); }

        public static int Ask(IWin32Window owner, string heading, string message, params string[] buttons)
        {
            using (var d = new DarkDialog(MainForm.AppName, heading, message, buttons, 560))
            {
                d.ShowDialog(owner);
                return d.Choice;
            }
        }
    }

    partial class MainForm
    {
        // ------------------------------------------------------------------ shared helpers

        void Ui(Action a)
        {
            try { if (IsHandleCreated && !IsDisposed) BeginInvoke(a); } catch { }
        }

        void RelayoutCurrent() { if (currentPage != null) currentPage.Page.Relayout(); }

        static string Check(bool ok) { return ok ? "✓  " : "⚠  "; }

        // ------------------------------------------------------------------ startup hook (called from the constructor)

        void SetupV31()
        {
            if (Benchmarks.RecoverConfig()) ReloadFromFile();   // the launcher closed in the middle of a benchmark last time
            navEnabled = LauncherPrefs.Bool("controller_nav", true);
            navTimer = new Timer { Interval = 40 };
            navTimer.Tick += delegate { NavTick(); };
            navTimer.Start();
            Shown += delegate
            {
                MaybeShowWhatsNew();
                RunFirstRunCheck();
                System.Threading.ThreadPool.QueueUserWorkItem(delegate { if (KnownIssues.Refresh(false)) Ui(delegate { UpdateStatus(); }); });
            };
        }

        // Points the launcher at a different skate3.exe. Returns true when the launcher restarts because the game's data folder
        // changed (portable.txt or a saves folder next to the new exe), since settings.toml is opened once at startup.
        bool SetGameLocation(string exe)
        {
            string before = Paths.UserDir + "|" + Paths.SavesRoot;
            gameExe = exe;
            WritePref("game_exe", exe);
            Paths.Resolve(exe);
            if (Paths.UserDir + "|" + Paths.SavesRoot == before) return false;
            DarkDialog.Ask(this, "Restarting the launcher",
                "The game in that folder keeps its " + (Paths.Portable ? "settings in the game folder (portable mode)" : "settings in " + Paths.UserDir) +
                " and its saves in " + Paths.SavesRoot + ". The launcher restarts to load them.", "OK");
            Application.Restart();
            return true;
        }

        void RunFirstRunCheck()
        {
            string exe = gameExe;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                GameCheck r;
                try { r = GameFiles.Check(exe, false, null, delegate { return false; }); }
                catch { return; }
                Ui(delegate
                {
                    ApplyGameCheck(r);
                    if (!r.SetupOk)
                    {
                        ShowPage("Game files");
                        Flash(!r.ExeFound ? "skate3.exe wasn't found. The Game files page walks you through setting things up." :
                              !r.GameInstalled ? "The Skate 3 game files aren't installed yet. See the Game files page." :
                              "Something's missing from your Skate 3 install. See the Game files page.", Theme.Warn);
                    }
                });
            });
        }

        // ================================================================== What's new

        static readonly string[] WhatsNew311 =
        {
            "Portable installs: if portable.txt or a saves folder sits next to skate3.exe, the launcher now edits the same settings.toml and backs up the same saves the game uses.",
            "Known-issue warnings: settings that trigger known recomp bugs get a ⚠ on their card. First up: on Vulkan, full-resolution SSAO or a static sun shadow resolution other than 4096 breaks shadows (use Direct3D 12). The list updates from GitHub without a new launcher release.",
            "Status messages (saved, installed, copied…) now stay on screen for a few seconds instead of vanishing.",
            "Buttons that can't be used right now, like ▲ on the first mod, now look disabled.",
            "A finished benchmark no longer shows up on the Logs page as a crash."
        };

        static readonly string[] WhatsNew310 =
        {
            "Mod loader: install, enable, order and remove mods on the new Mods page. Your game files are never changed.",
            "Install and update Skate 3 Recomp from the Game files page, with one-click rollback to the previous build.",
            "First-run setup and a game file check, so a missing title update or a damaged dump is spotted before you launch.",
            "Benchmark: the game drives itself to the same spot every time and the launcher records frame times, so you can compare presets and profiles with real numbers.",
            "Photo Mode page: free camera speed, screenshot and free-camera keys, and the screenshots folder.",
            "Add to Steam, with your own artwork, for Big Picture and Steam Deck.",
            "Graphics driver check and better crash reports, including a pre-filled GitHub issue.",
            "Controller navigation for the launcher itself: D-pad to move, A to select, LB/RB to switch pages, Start to play."
        };

        void MaybeShowWhatsNew()
        {
            string seen = LauncherPrefs.Read("last_seen_version");
            bool existingUser = File.Exists(Paths.Prefs) && seen != LauncherVersion;
            LauncherPrefs.Write("last_seen_version", LauncherVersion);
            if (!existingUser || (seen != null && Updates.Compare(LauncherVersion, seen) <= 0)) return;
            ShowWhatsNew(seen == null || Updates.Compare("3.1.0", seen) > 0);
        }

        void ShowWhatsNew() { ShowWhatsNew(false); }

        // includeV310: also list v3.1.0's changes, for people updating straight from v3.0.
        void ShowWhatsNew(bool includeV310)
        {
            string text = "•  " + string.Join("\n\n•  ", WhatsNew311);
            if (includeV310) text += "\n\nAlso new since v3.0 (from v3.1.0):\n\n•  " + string.Join("\n\n•  ", WhatsNew310);
            DarkDialog.Ask(this, "What's new in v" + LauncherVersion, text, "Got it");
        }

        // ================================================================== Game files

        ToolCard gfSetupCard, gfVersionCard, gfVerifyCard;
        readonly List<ItemRow> gfSetupRows = new List<ItemRow>(), gfBackupRows = new List<ItemRow>();
        Label gfVersionText, gfProgressText, gfVerifyText;
        PillButton gfInstall, gfCancel, gfVerifyCancel, gfStartInstaller;
        RecompRelease gfLatest;
        bool gfBusy, gfVerifyBusy, gfCancelRequested, gfVerifyCancelRequested;

        void BuildGameFilesPage()
        {
            var page = new StackPage();

            gfSetupCard = new ToolCard("Setup checklist", "What the recomp needs before it can boot. Re-check after you change anything.");
            var recheck = MkButton("Re-check", "", ButtonStyle.Secondary, null);
            var openGame = MkButton("Open game folder", "", ButtonStyle.Ghost, null);
            gfStartInstaller = MkButton("Start the game's installer", "", ButtonStyle.Secondary,
                "Starts skate3.exe. With no game files installed it shows its own setup: click \"Select ISO\" and pick your legally obtained Skate 3 ISO.");
            recheck.Click += delegate { RunQuickCheck(); };
            openGame.Click += delegate { if (gameExe != null) OpenFolder(Path.GetDirectoryName(gameExe)); };
            gfStartInstaller.Click += delegate
            {
                if (gameExe == null) { MessageBox.Show(this, "Set the game location on the Launcher page first.", AppName); return; }
                try { Process.Start(new ProcessStartInfo(gameExe) { WorkingDirectory = Path.GetDirectoryName(gameExe), UseShellExecute = true }); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, AppName); }
            };
            gfSetupCard.Controls.AddRange(new Control[] { recheck, openGame, gfStartInstaller });
            gfSetupCard.Body = (w, top) =>
            {
                int y = StackRows(gfSetupRows, w, top);
                return gfStartInstaller.Visible ? Row(D(14), y + D(8), recheck, gfStartInstaller, openGame) : Row(D(14), y + D(8), recheck, openGame);
            };
            page.Add(gfSetupCard);

            gfVersionCard = new ToolCard("Skate 3 Recomp version",
                "Installs releases of the recomp from GitHub (" + Updates.DefaultRepo + " unless you changed the release source on the Launcher page). " +
                "Every file an update replaces is backed up first, so you can roll back if a new build breaks something. Your game files, saves and settings are never touched.");
            gfVersionText = MkLabel("", Theme.Base, Theme.Text);
            gfProgressText = MkLabel("", Theme.Small, Theme.Accent);
            var checkBtn = MkButton("Check for updates", "", ButtonStyle.Secondary, null);
            gfInstall = MkButton("Install", "", ButtonStyle.Primary, null);
            gfInstall.Height = D(36);
            var fromZip = MkButton("Install from a zip…", "", ButtonStyle.Ghost, "Install a Skate3Recomp-Windows.zip you already downloaded.");
            var notes = MkButton("Release notes", null, ButtonStyle.Ghost, null);
            gfCancel = MkButton("Cancel", null, ButtonStyle.Ghost, null);
            gfCancel.Visible = false;
            checkBtn.Click += delegate { CheckRecompRelease(true); };
            gfInstall.Click += delegate { InstallRecompRelease(); };
            fromZip.Click += delegate
            {
                using (var dlg = new OpenFileDialog { Title = "Choose a Skate 3 Recomp release zip", Filter = "Zip files|*.zip" })
                    if (dlg.ShowDialog(this) == DialogResult.OK) InstallRecompZip(dlg.FileName, Path.GetFileNameWithoutExtension(dlg.FileName));
            };
            notes.Click += delegate { OpenUrl(gfLatest != null && gfLatest.PageUrl != null ? gfLatest.PageUrl : "https://github.com/" + UpdateRepo + "/releases"); };
            gfCancel.Click += delegate { gfCancelRequested = true; };
            gfVersionCard.Controls.AddRange(new Control[] { gfVersionText, gfProgressText, checkBtn, gfInstall, fromZip, notes, gfCancel });
            gfVersionCard.Body = (w, top) =>
            {
                gfVersionText.MaximumSize = new Size(w - D(40), 0);
                gfVersionText.Location = new Point(D(20), top);
                var buttons = new List<Control>();
                if (gfInstall.Visible) buttons.Add(gfInstall);
                buttons.Add(checkBtn);
                buttons.Add(fromZip);
                buttons.Add(notes);
                if (gfCancel.Visible) buttons.Add(gfCancel);
                int y = Row(D(20), gfVersionText.Bottom + D(12), buttons.ToArray());
                if (gfProgressText.Text.Length > 0)
                {
                    gfProgressText.MaximumSize = new Size(w - D(40), 0);
                    gfProgressText.Location = new Point(D(20), y + D(10));
                    y = gfProgressText.Bottom;
                }
                return StackRows(gfBackupRows, w, y + (gfBackupRows.Count > 0 ? D(12) : 0));
            };
            page.Add(gfVersionCard);

            gfVerifyCard = new ToolCard("Check game files",
                "Quick check compares sizes against a known-good dump. Full check reads every file (about " + Paths.Size(GameFiles.ReferenceBytes) + ", a minute or two on an SSD) and compares SHA-256 hashes. " +
                "If your copy is from another region, save it as your baseline once and later checks compare against that instead.");
            gfVerifyText = MkLabel("", Theme.Base, Theme.Text);
            var quick = MkButton("Quick check", "", ButtonStyle.Secondary, null);
            var fullBtn = MkButton("Full check", "", ButtonStyle.Secondary, null);
            var baseline = MkButton("Save as my baseline", null, ButtonStyle.Ghost, "Hash your game files as they are now and treat them as known-good from now on.");
            gfVerifyCancel = MkButton("Cancel", null, ButtonStyle.Ghost, null);
            gfVerifyCancel.Visible = false;
            quick.Click += delegate { RunVerify(false); };
            fullBtn.Click += delegate { RunVerify(true); };
            baseline.Click += delegate { SaveGameBaseline(); };
            gfVerifyCancel.Click += delegate { gfVerifyCancelRequested = true; };
            gfVerifyCard.Controls.AddRange(new Control[] { gfVerifyText, quick, fullBtn, baseline, gfVerifyCancel });
            gfVerifyCard.Body = (w, top) =>
            {
                gfVerifyText.MaximumSize = new Size(w - D(40), 0);
                gfVerifyText.Location = new Point(D(20), top);
                int y = gfVerifyText.Text.Length > 0 ? gfVerifyText.Bottom + D(12) : top;
                return gfVerifyCancel.Visible ? Row(D(20), y, quick, fullBtn, baseline, gfVerifyCancel) : Row(D(20), y, quick, fullBtn, baseline);
            };
            page.Add(gfVerifyCard);

            var info = AddPage("Game files", "Setup, Skate 3 Recomp updates and a check that your game files are intact.", "", "TOOLS", false, page);
            info.OnShow = delegate
            {
                RefreshRecompVersionCard();
                if (GameFiles.Last != null) ApplyGameCheck(GameFiles.Last); else RunQuickCheck();
                if (gfLatest == null && !gfBusy) CheckRecompRelease(false);
            };
        }

        void RunQuickCheck()
        {
            string exe = gameExe;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var r = GameFiles.Check(exe, false, null, delegate { return false; });
                Ui(delegate { ApplyGameCheck(r); });
            });
        }

        void ApplyGameCheck(GameCheck r)
        {
            if (gfSetupCard == null) return;
            var rows = new List<ItemRow>();
            Func<bool, string, string, ItemRow> add = (ok, main, sub) =>
            {
                var row = new ItemRow(Check(ok) + main, sub);
                row.Main.ForeColor = ok ? Theme.Text : Theme.Warn;
                rows.Add(row);
                return row;
            };
            add(r.ExeFound, "skate3.exe found", r.ExeFound ? gameExe : "Put this launcher next to skate3.exe, or set the game location on the Launcher page.");
            if (r.ExeFound)
            {
                add(r.GameInstalled, "Game files installed", r.GameInstalled ? r.GameRoot : "Start the game once and click \"Select ISO\" to install them from your Xbox 360 ISO.");
                if (r.GameInstalled)
                {
                    add(r.XexKnown, "default.xex is the expected version", r.XexKnown ? "Matches the NTSC-U build the recomp is made for." : "Unknown version. Other regions or revisions may not boot.");
                    add(r.TitleUpdate, "Title Update 3 staged", r.TitleUpdate ? "default.xexp and EAWebkit.xexp match the recomp's own checks." : "The game downloads it the first time it starts (it needs the internet once).");
                    add(r.WebkitPresent, "EAWebkit.xex present", r.WebkitPresent ? "data\\webkit\\EAWebkit.xex" : "This file comes from the ISO. Re-install the game files.");
                    bool ntfs = r.FileSystem == null || r.FileSystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase);
                    add(ntfs, "Drive supports the mod loader", (r.FileSystem ?? "unknown") + " file system" + (ntfs ? "" : ". Mods need NTFS."));
                }
            }
            ReplaceRows(gfSetupCard, gfSetupRows, rows);
            gfStartInstaller.Visible = r.ExeFound && !r.GameInstalled;
            if (r.Error != null) gfSetupCard.HelpText.Text = "Couldn't finish the check: " + r.Error;
            else gfSetupCard.HelpText.Text = r.SetupOk ? "Everything the recomp needs is in place." : "Fix the items marked ⚠, then Re-check.";
            if (gfVerifyText != null && r.ExeFound && r.GameInstalled && !gfVerifyBusy) ShowVerifyResult(r);
            RelayoutCurrent();
        }

        void ShowVerifyResult(GameCheck r)
        {
            var sb = new StringBuilder();
            if (r.Cancelled) sb.Append("Check cancelled.");
            else
            {
                sb.Append(r.Full ? "Full check" : "Quick check").Append(" (").Append(When(r.When)).Append("): ");
                if (r.Missing.Count == 0 && r.Changed.Count == 0)
                    sb.Append(r.Checked).Append(" files OK").Append(r.MatchedBaseline > 0 ? " (" + r.MatchedBaseline + " matched your baseline)" : "").Append(".");
                else
                {
                    if (r.Missing.Count > 0) sb.Append(r.Missing.Count).Append(" missing");
                    if (r.Missing.Count > 0 && r.Changed.Count > 0) sb.Append(", ");
                    if (r.Changed.Count > 0) sb.Append(r.Changed.Count).Append(" different from the known-good copy");
                    sb.Append(".");
                    foreach (var m in r.Missing.Take(6)) sb.Append("\n   missing: ").Append(m);
                    foreach (var c in r.Changed.Take(8)) sb.Append("\n   different: ").Append(c);
                    if (r.Missing.Count + r.Changed.Count > 14) sb.Append("\n   …");
                    sb.Append("\nA different region's dump shows up here too. If the game runs fine, save it as your baseline. Otherwise re-install the game files from your ISO.");
                }
                foreach (var n in r.Notes) sb.Append("\n").Append(n);
            }
            gfVerifyText.Text = sb.ToString();
            gfVerifyText.ForeColor = r.AllGood ? Theme.Text : Theme.Warn;
            RelayoutCurrent();
        }

        void RunVerify(bool full)
        {
            if (gfVerifyBusy) return;
            gfVerifyBusy = true;
            gfVerifyCancelRequested = false;
            gfVerifyCancel.Visible = true;
            gfVerifyText.ForeColor = Theme.Muted;
            gfVerifyText.Text = full ? "Hashing game files…" : "Checking…";
            RelayoutCurrent();
            string exe = gameExe;
            DateTime lastUi = DateTime.MinValue;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var r = GameFiles.Check(exe, full, (done, total, file) =>
                {
                    if ((DateTime.Now - lastUi).TotalMilliseconds < 200) return;
                    lastUi = DateTime.Now;
                    string t = (full ? "Hashing " : "Checking ") + (total > 0 ? (done * 100 / total) + "%" : "") + "  ·  " + file;
                    Ui(delegate { gfVerifyText.Text = t; });
                }, delegate { return gfVerifyCancelRequested; });
                Ui(delegate
                {
                    gfVerifyBusy = false;
                    gfVerifyCancel.Visible = false;
                    ApplyGameCheck(r);
                    ShowVerifyResult(r);
                });
            });
        }

        void SaveGameBaseline()
        {
            if (gfVerifyBusy || gameExe == null) return;
            if (DarkDialog.Ask(this, "Save these files as your baseline?",
                    "The launcher hashes every game file as it is now (a minute or two) and treats that as known-good from now on. Only do this if the game runs correctly.",
                    "Save baseline", "Cancel") != 0) return;
            gfVerifyBusy = true;
            gfVerifyCancelRequested = false;
            gfVerifyCancel.Visible = true;
            RelayoutCurrent();
            string exe = gameExe;
            DateTime lastUi = DateTime.MinValue;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string result;
                try
                {
                    int n = GameFiles.SaveBaseline(exe, (done, total, file) =>
                    {
                        if ((DateTime.Now - lastUi).TotalMilliseconds < 200) return;
                        lastUi = DateTime.Now;
                        string t = "Hashing " + (total > 0 ? (done * 100 / total) + "%" : "") + "  ·  " + file;
                        Ui(delegate { gfVerifyText.Text = t; });
                    }, delegate { return gfVerifyCancelRequested; });
                    result = "Saved a baseline of " + n + " files.";
                }
                catch (OperationCanceledException) { result = "Baseline cancelled."; }
                catch (Exception ex) { result = "Couldn't save the baseline: " + ex.Message; }
                Ui(delegate
                {
                    gfVerifyBusy = false;
                    gfVerifyCancel.Visible = false;
                    gfVerifyText.Text = result;
                    gfVerifyText.ForeColor = Theme.Text;
                    RelayoutCurrent();
                });
            });
        }

        // ---- recomp releases

        void RefreshRecompVersionCard()
        {
            if (gfVersionCard == null) return;
            installedVersion = RecompVersion.Current(Paths.LogsDir(gameExe), gameExe);
            string inst = installedVersion != null ? "v" + installedVersion.TrimStart('v') : "unknown";
            var sb = new StringBuilder("Installed: " + inst);
            bool newer = gfLatest != null && (installedVersion == null || Updates.Compare(gfLatest.Tag, installedVersion) > 0);
            if (gfLatest != null)
            {
                sb.Append("     Latest: ").Append(gfLatest.Tag);
                if (gfLatest.Published != DateTime.MinValue) sb.Append(" (").Append(gfLatest.Published.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture)).Append(")");
                if (gfLatest.AssetSize > 0) sb.Append("  ·  ").Append(Paths.Size(gfLatest.AssetSize));
                if (newer) sb.Append("\nA newer build is out. Install it below. Your current build is backed up first.");
                else sb.Append("\nYou're on the latest build.");
            }
            gfVersionText.Text = sb.ToString();
            gfVersionText.ForeColor = newer ? Theme.Accent : Theme.Text;
            gfInstall.Visible = gfLatest != null && gfLatest.AssetUrl != null && !gfBusy && (newer || gameExe == null);
            if (gfLatest != null)
            {
                gfInstall.Text = "Install " + gfLatest.Tag;
                gfInstall.Width = TextRenderer.MeasureText(gfInstall.Text, gfInstall.Font).Width + D(56);
            }

            var rows = new List<ItemRow>();
            if (gameExe != null)
                foreach (var b in RecompInstaller.Backups(Path.GetDirectoryName(gameExe)))
                {
                    var backup = b;
                    var row = new ItemRow("Backup of v" + (b.FromVersion ?? "?").TrimStart('v') + "  (before " + (b.ToVersion ?? "an update") + ")",
                        When(b.When) + "  ·  " + b.Files + " files");
                    var roll = MkButton("Roll back", "", ButtonStyle.Secondary, "Put this build back and undo the update.");
                    roll.Click += delegate { RollBackRecomp(backup); };
                    row.AddButton(roll);
                    rows.Add(row);
                }
            ReplaceRows(gfVersionCard, gfBackupRows, rows);
            RelayoutCurrent();
        }

        void CheckRecompRelease(bool announce)
        {
            if (gfBusy) return;
            string repo = UpdateRepo;
            if (announce) { gfProgressText.Text = "Checking " + repo + "…"; RelayoutCurrent(); }
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                RecompRelease r = null;
                string error = null;
                try { r = RecompInstaller.Latest(repo); }
                catch (Exception ex) { error = ex.Message; }
                Ui(delegate
                {
                    if (r != null)
                    {
                        gfLatest = r;
                        LauncherPrefs.Write("update_latest_tag", r.Tag);
                        if (r.PageUrl != null) LauncherPrefs.Write("update_latest_url", r.PageUrl);
                        LauncherPrefs.Write("update_last_check", DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
                        ApplyUpdateResult(null);
                        gfProgressText.Text = r.AssetUrl == null ? "The latest release (" + r.Tag + ") has no Windows zip to install." : "";
                    }
                    else gfProgressText.Text = announce ? "Couldn't check GitHub: " + error : "";
                    RefreshRecompVersionCard();
                });
            });
        }

        void InstallRecompRelease()
        {
            if (gfBusy || gfLatest == null) return;
            if (GameRunningGuard("updating the recomp")) return;
            if (gameExe == null) { MessageBox.Show(this, "Set the game location on the Launcher page first.", AppName); return; }
            var rel = gfLatest;
            if (DarkDialog.Ask(this, "Install Skate 3 Recomp " + rel.Tag + "?",
                    "The launcher downloads " + (rel.AssetName ?? "the release") + (rel.AssetSize > 0 ? " (" + Paths.Size(rel.AssetSize) + ")" : "") +
                    " from GitHub and installs it into " + Path.GetDirectoryName(gameExe) + ".\n\nEvery file it replaces is backed up so you can roll back. Game files, saves and settings are left alone.",
                    "Install", "Cancel") != 0) return;
            gfBusy = true;
            gfCancelRequested = false;
            gfCancel.Visible = true;
            gfInstall.Visible = false;
            RelayoutCurrent();
            string dir = Path.GetDirectoryName(gameExe);
            DateTime lastUi = DateTime.MinValue;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string zip = null, error = null;
                try
                {
                    zip = RecompInstaller.Download(rel, Path.Combine(dir, RecompInstaller.BackupFolder, "download"), (got, total) =>
                    {
                        if ((DateTime.Now - lastUi).TotalMilliseconds < 150) return;
                        lastUi = DateTime.Now;
                        string t = "Downloading " + Paths.Size(got) + (total > 0 ? " of " + Paths.Size(total) + " (" + (got * 100 / total) + "%)" : "") + "…";
                        Ui(delegate { gfProgressText.Text = t; gfProgressText.ForeColor = Theme.Accent; RelayoutCurrent(); });
                    }, delegate { return gfCancelRequested; });
                }
                catch (OperationCanceledException) { error = "Download cancelled."; }
                catch (Exception ex) { error = "Download failed: " + ex.Message; }
                Ui(delegate
                {
                    gfCancel.Visible = false;
                    gfBusy = false;
                    if (error != null) { gfProgressText.Text = error; gfProgressText.ForeColor = Theme.Warn; RefreshRecompVersionCard(); return; }
                    InstallRecompZip(zip, rel.Tag);
                    try { File.Delete(zip); } catch { }
                });
            });
        }

        void InstallRecompZip(string zip, string tag)
        {
            if (GameRunningGuard("updating the recomp")) return;
            if (gameExe == null) { MessageBox.Show(this, "Set the game location on the Launcher page first.", AppName); return; }
            string dir = Path.GetDirectoryName(gameExe);
            string from = installedVersion;
            gfBusy = true;
            RelayoutCurrent();
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                string error = null;
                try
                {
                    RecompInstaller.Install(zip, dir, from, tag, s => Ui(delegate { gfProgressText.Text = s; RelayoutCurrent(); }));
                    if (tag != null && Regex.IsMatch(tag, @"\d")) RecompVersion.Remember(gameExe, tag);
                }
                catch (Exception ex) { error = ex.Message; }
                Ui(delegate
                {
                    gfBusy = false;
                    if (error != null)
                    {
                        gfProgressText.Text = "Install failed and was undone: " + error;
                        gfProgressText.ForeColor = Theme.Warn;
                    }
                    else
                    {
                        gfProgressText.Text = "Installed " + tag + ". Clear the shader cache on the Launcher page if you see graphics glitches on the first run.";
                        gfProgressText.ForeColor = Theme.Good;
                        Flash("Skate 3 Recomp " + tag + " installed.", Theme.Good);
                        ApplyUpdateResult(null);
                    }
                    RefreshRecompVersionCard();
                });
            });
        }

        void RollBackRecomp(RecompBackup b)
        {
            if (GameRunningGuard("rolling back the recomp")) return;
            if (DarkDialog.Ask(this, "Roll back to v" + (b.FromVersion ?? "?").TrimStart('v') + "?",
                    "Puts back the " + b.Files + " files this backup saved and removes files the update added. The backup is used up by the rollback.",
                    "Roll back", "Cancel") != 0) return;
            try
            {
                RecompInstaller.Rollback(b, Path.GetDirectoryName(gameExe));
                if (!string.IsNullOrEmpty(b.FromVersion)) RecompVersion.Remember(gameExe, b.FromVersion); else LauncherPrefs.Write("recomp_installed_tag", "");
                Flash("Rolled back to v" + (b.FromVersion ?? "?").TrimStart('v') + ".", Theme.Good);
            }
            catch (Exception ex) { MessageBox.Show(this, "Rollback failed:\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Error); }
            ApplyUpdateResult(null);
            RefreshRecompVersionCard();
        }

        // ================================================================== Mods

        ToolCard modsHeaderCard, modsListCard;
        Label modsStatus;
        readonly List<ItemRow> modRows = new List<ItemRow>();
        List<ModInfo> modList = new List<ModInfo>();

        void BuildModsPage()
        {
            var page = new StackPage();

            modsHeaderCard = new ToolCard("Mod loader",
                "Mods go in the mods folder next to skate3.exe, one folder per mod, laid out like the game folder (for example mods\\My Deck\\data\\content\\createacharacter.big). " +
                "When you press PLAY the launcher builds a modded copy of the game folder out of hard links, so it takes no extra disk space and your real game files are never changed. " +
                "Mods lower in the list win when two replace the same file.");
            var enable = MkToggle("mods_enabled", true, delegate { RefreshMods(); });
            var enableLabel = MkLabel("Load enabled mods when I press PLAY", Theme.Base, Theme.Text);
            modsStatus = MkLabel("", Theme.Small, Theme.Muted);
            var zipBtn = MkButton("Install from .zip…", "", ButtonStyle.Secondary, null);
            var folderBtn = MkButton("Install from folder…", "", ButtonStyle.Ghost, null);
            var openBtn = MkButton("Open mods folder", "", ButtonStyle.Ghost, null);
            zipBtn.Click += delegate
            {
                using (var dlg = new OpenFileDialog { Title = "Choose mod zips", Filter = "Zip files|*.zip", Multiselect = true })
                    if (dlg.ShowDialog(this) == DialogResult.OK) InstallMods(dlg.FileNames);
            };
            folderBtn.Click += delegate
            {
                using (var dlg = new FolderBrowserDialog { Description = "Choose a mod folder" })
                    if (dlg.ShowDialog(this) == DialogResult.OK) InstallMods(new[] { dlg.SelectedPath });
            };
            openBtn.Click += delegate { if (gameExe != null) OpenFolder(Mods.Dir(gameExe)); };
            modsHeaderCard.Controls.AddRange(new Control[] { enable, enableLabel, modsStatus, zipBtn, folderBtn, openBtn });
            modsHeaderCard.Body = (w, top) =>
            {
                int y = ToggleLine(D(20), top, enable, enableLabel) + D(10);
                modsStatus.MaximumSize = new Size(w - D(40), 0);
                modsStatus.Location = new Point(D(20), y);
                return Row(D(20), modsStatus.Bottom + D(12), zipBtn, folderBtn, openBtn);
            };
            page.Add(modsHeaderCard);

            modsListCard = new ToolCard("Installed mods", "");
            modsListCard.Body = (w, top) => StackRows(modRows, w, top);
            page.Add(modsListCard);
            EnableModsDragDrop(page, modsHeaderCard, modsListCard);

            var info = AddPage("Mods", "Install, enable and order mods. Your game files are never changed.", "", "TOOLS", false, page);
            info.OnShow = RefreshMods;
        }

        void InstallMods(IEnumerable<string> sources)
        {
            if (gameExe == null) { MessageBox.Show(this, "Set the game location on the Launcher page first.", AppName); return; }
            if (GameRunningGuard("changing mods")) return;
            var done = new List<string>();
            foreach (var s in sources)
            {
                try { done.Add(Mods.Install(gameExe, s)); }
                catch (Exception ex) { MessageBox.Show(this, "Couldn't install " + Path.GetFileName(s) + ":\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            }
            if (done.Count > 0) Flash("Installed and enabled " + string.Join(", ", done) + ".", Theme.Good);
            RefreshMods();
        }

        void RefreshMods()
        {
            if (modsListCard == null) return;
            modList = gameExe != null ? Mods.List(gameExe) : new List<ModInfo>();
            var enabled = modList.Where(m => m.Enabled && m.Files.Count > 0).ToList();
            var conflicts = Mods.Conflicts(enabled);
            string flavor = RecompFlavor.Detect(gameExe, configPath);
            string recompVer = RecompVersion.Current(Paths.LogsDir(gameExe), gameExe);
            var rows = new List<ItemRow>();
            for (int i = 0; i < modList.Count; i++)
            {
                var m = modList[i];
                int index = i;
                var sub = new List<string>();
                if (!string.IsNullOrEmpty(m.Author)) sub.Add("by " + m.Author);
                sub.Add(m.Files.Count + " file" + (m.Files.Count == 1 ? "" : "s") + ", " + Paths.Size(m.Bytes));
                int lose = conflicts.Count(kv => kv.Value.Contains(m) && kv.Value.Last() != m);
                int win = conflicts.Count(kv => kv.Value.Contains(m) && kv.Value.Last() == m);
                if (m.Enabled && lose > 0) sub.Add(lose + " file" + (lose == 1 ? " is" : "s are") + " overridden by a mod lower down");
                if (m.Enabled && win > 0) sub.Add("overrides " + win + " file" + (win == 1 ? "" : "s") + " from a mod above");
                if (m.Skipped.Count > 0) sub.Add("skips " + string.Join(", ", m.Skipped) + " (the recomp can't use replaced code)");
                if (m.Files.Count == 0) sub.Add("no usable game files");
                var row = CreateModRow(m, string.Join("  ·  ", sub), modList, flavor, recompVer);
                row.Main.ForeColor = m.Enabled ? Theme.Text : Theme.Muted;
                if (!string.IsNullOrEmpty(m.Description)) tips.SetToolTip(row.Main, m.Description);
                var toggle = MkButton(m.Enabled ? "Disable" : "Enable", null, m.Enabled ? ButtonStyle.Ghost : ButtonStyle.Secondary, null);
                var up = MkButton("▲", null, ButtonStyle.Ghost, "Load earlier (lower priority).");
                var down = MkButton("▼", null, ButtonStyle.Ghost, "Load later (wins conflicts).");
                var folder = MkButton("", "", ButtonStyle.Ghost, "Open this mod's folder.");
                var del = MkButton("", "", ButtonStyle.Ghost, "Remove this mod (deletes its folder).");
                up.Width = down.Width = D(38);
                up.Enabled = index > 0;
                down.Enabled = index < modList.Count - 1;
                toggle.Click += delegate
                {
                    if (GameRunningGuard("changing mods")) return;
                    m.Enabled = !m.Enabled;
                    if (m.Enabled) Mods.RecordBase(gameExe, m);
                    Mods.SaveList(gameExe, modList);
                    RefreshMods();
                };
                up.Click += delegate { MoveMod(index, -1); };
                down.Click += delegate { MoveMod(index, 1); };
                folder.Click += delegate { OpenFolder(m.Dir); };
                del.Click += delegate
                {
                    if (GameRunningGuard("changing mods")) return;
                    if (DarkDialog.Ask(this, "Remove " + m.Display + "?", "This deletes " + m.Dir + ". Your game files aren't affected.", "Remove", "Cancel") != 0) return;
                    try { Mods.Remove(gameExe, m); } catch (Exception ex) { MessageBox.Show(this, ex.Message, AppName); }
                    RefreshMods();
                };
                foreach (var b in new Control[] { toggle, up, down, folder, del }) row.AddButton(b);
                rows.Add(row);
            }
            ReplaceRows(modsListCard, modRows, rows);
            modsListCard.HelpText.Text = gameExe == null ? "skate3.exe hasn't been found yet." :
                modList.Count == 0 ? "No mods installed. Use Install above, or drop mod folders into " + Mods.Dir(gameExe) + "." :
                "● = enabled. Order matters: a mod lower in the list wins when two mods replace the same file.";

            bool loader = Mods.LoaderEnabled;
            string fs = gameExe != null ? FileUtil.DriveFormat(gameExe) : null;
            modsStatus.Text = gameExe == null ? "" :
                (!loader ? "The mod loader is off: PLAY starts the unmodded game." :
                 enabled.Count == 0 ? "No mods enabled: PLAY starts the unmodded game." :
                 enabled.Count + " mod" + (enabled.Count == 1 ? "" : "s") + " will load" + (conflicts.Count > 0 ? ", " + conflicts.Count + " file conflict" + (conflicts.Count == 1 ? "" : "s") + " (resolved by load order)" : "") + ".") +
                (fs != null && !fs.Equals("NTFS", StringComparison.OrdinalIgnoreCase) ? "  ⚠ This drive is " + fs + ": mods need NTFS." : "");
            modsStatus.ForeColor = fs != null && !fs.Equals("NTFS", StringComparison.OrdinalIgnoreCase) ? Theme.Warn : Theme.Muted;
            RelayoutCurrent();
        }

        void MoveMod(int index, int dir)
        {
            int to = index + dir;
            if (to < 0 || to >= modList.Count) return;
            var m = modList[index];
            modList.RemoveAt(index);
            modList.Insert(to, m);
            Mods.SaveList(gameExe, modList);
            RefreshMods();
        }

        // Shown before launching when the mod loader had something to say.
        bool ConfirmModWarnings(ModPlan plan)
        {
            if (plan.Warnings.Count == 0) return true;
            return DarkDialog.Ask(this, "Before you play", string.Join("\n\n", plan.Warnings), "Play anyway", "Cancel") == 0;
        }

        // ================================================================== Benchmark

        DarkCombo benchRoute, benchProfile, benchLength;
        RoundTextBox benchInputs;
        Label benchStatus;
        PillButton benchRun, benchCancel;
        ToolCard benchResultsCard;
        readonly List<ItemRow> benchRows = new List<ItemRow>();
        bool benchBusy, benchCancelRequested;

        static readonly string[] BenchRouteNames = { "Spawn point (stand still)", "PCU Library (the recomp's own map-switch route)", "Custom inputs…" };
        static readonly string[] BenchRouteInputs = { "", "start,a,rt:1500,rt:1500,down,down,a,down,down,down,down,down,a,a", null };
        static readonly int[] BenchWindows = { 4, 10, 20 };

        void BuildBenchmarkPage()
        {
            var page = new StackPage();

            var run = new ToolCard("Run a benchmark",
                "The recomp's demo-path automation boots the game straight to gameplay (signed out, so your career save is never touched), optionally plays a fixed list of controller inputs, " +
                "and logs how evenly frames are presented every 600 frames. The launcher reads those numbers, closes the game and puts your settings.toml back exactly as it was. " +
                "Don't touch the controller or keyboard while it runs.");
            var routeCap = MkLabel("Route", Theme.Small, Theme.Muted);
            var profCap = MkLabel("Settings", Theme.Small, Theme.Muted);
            var lenCap = MkLabel("Length", Theme.Small, Theme.Muted);
            benchRoute = new DarkCombo { Width = D(330) };
            benchRoute.Items.AddRange(BenchRouteNames);
            benchRoute.SelectedIndex = Math.Max(0, Math.Min(BenchRouteNames.Length - 1, LauncherPrefs.Int("bench_route", 0)));
            benchProfile = new DarkCombo { Width = D(220) };
            benchLength = new DarkCombo { Width = D(200) };
            benchLength.Items.AddRange(new object[] { "Quick (2,400 frames)", "Standard (6,000 frames)", "Long (12,000 frames)" });
            benchLength.SelectedIndex = 0;
            benchInputs = new RoundTextBox();
            benchInputs.Box.Font = Theme.Mono;
            benchInputs.Box.Text = LauncherPrefs.Read("bench_custom") ?? "";
            var inputsCap = MkLabel("Inputs: a b x y start back lb rb lt rt up down left right l3 r3, comma separated. Add :ms to wait after one (rt:1500).", Theme.Small, Theme.Muted);
            benchRun = MkButton("Run benchmark", "", ButtonStyle.Primary, null);
            benchRun.Height = D(38);
            benchCancel = MkButton("Stop", null, ButtonStyle.Ghost, null);
            benchCancel.Visible = false;
            benchStatus = MkLabel("", Theme.Small, Theme.Accent);
            benchRoute.SelectedIndexChanged += delegate { LauncherPrefs.Write("bench_route", benchRoute.SelectedIndex.ToString(CultureInfo.InvariantCulture)); RelayoutCurrent(); };
            benchRun.Click += delegate { RunBenchmark(); };
            benchCancel.Click += delegate { benchCancelRequested = true; };
            run.Controls.AddRange(new Control[] { routeCap, profCap, lenCap, benchRoute, benchProfile, benchLength, benchInputs, inputsCap, benchRun, benchCancel, benchStatus });
            run.Body = (w, top) =>
            {
                routeCap.Location = new Point(D(20), top);
                benchRoute.Location = new Point(D(20), routeCap.Bottom + D(4));
                profCap.Location = new Point(benchRoute.Right + D(10), top);
                benchProfile.Location = new Point(benchRoute.Right + D(10), benchRoute.Top);
                lenCap.Location = new Point(benchProfile.Right + D(10), top);
                benchLength.Location = new Point(benchProfile.Right + D(10), benchRoute.Top);
                int y = benchRoute.Bottom + D(10);
                bool custom = benchRoute.SelectedIndex == BenchRouteNames.Length - 1;
                benchInputs.Visible = inputsCap.Visible = custom;
                if (custom)
                {
                    benchInputs.SetBounds(D(20), y, w - D(40), D(34));
                    inputsCap.MaximumSize = new Size(w - D(40), 0);
                    inputsCap.Location = new Point(D(20), benchInputs.Bottom + D(4));
                    y = inputsCap.Bottom + D(10);
                }
                y = benchCancel.Visible ? Row(D(20), y + D(4), benchRun, benchCancel) : Row(D(20), y + D(4), benchRun);
                if (benchStatus.Text.Length > 0)
                {
                    benchStatus.MaximumSize = new Size(w - D(40), 0);
                    benchStatus.Location = new Point(D(20), y + D(10));
                    y = benchStatus.Bottom;
                }
                return y;
            };
            page.Add(run);

            benchResultsCard = new ToolCard("Results", "");
            var copy = MkButton("Copy results", null, ButtonStyle.Ghost, "Copy every result as text.");
            var openCsv = MkButton("Open results file", "", ButtonStyle.Ghost, Benchmarks.ResultsPath);
            copy.Click += delegate
            {
                var all = Benchmarks.Load();
                if (all.Count == 0) return;
                try { Clipboard.SetText(string.Join(Environment.NewLine, all.Select(r => BenchLine(r)))); Flash("Benchmark results copied.", Theme.Good); } catch { }
            };
            openCsv.Click += delegate { if (File.Exists(Benchmarks.ResultsPath)) OpenInNotepad(Benchmarks.ResultsPath); };
            benchResultsCard.Controls.AddRange(new Control[] { copy, openCsv });
            benchResultsCard.Body = (w, top) => { int y = StackRows(benchRows, w, top); return Row(D(14), y + D(6), copy, openCsv); };
            page.Add(benchResultsCard);

            var info = AddPage("Benchmark", "Measure frame rate and pacing on a fixed route, then compare presets and profiles.", "", "TOOLS", false, page);
            info.OnShow = RefreshBenchmarks;
        }

        static string BenchLine(BenchResult r)
        {
            return r.When.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + "  " + r.Label + "  ·  " + r.Route + "  ·  " +
                r.Fps.ToString("0", CultureInfo.InvariantCulture) + " FPS avg (" + r.AvgMs.ToString("0.00", CultureInfo.InvariantCulture) + " ms), worst frame " +
                r.MaxMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms, jitter " + r.Jitter.ToString("0.00", CultureInfo.InvariantCulture) + " ms  ·  " + r.Settings + "  ·  " + r.Gpu;
        }

        void RefreshBenchmarks()
        {
            if (benchProfile == null) return;
            string prevProfile = benchProfile.SelectedIndex >= 0 ? benchProfile.Items[benchProfile.SelectedIndex].ToString() : null;
            benchProfile.Items.Clear();
            benchProfile.Items.Add("Current settings");
            foreach (var f in Profiles.List()) benchProfile.Items.Add(Path.GetFileNameWithoutExtension(f.Name));
            int sel = prevProfile != null ? benchProfile.Items.IndexOf(prevProfile) : 0;
            benchProfile.SelectedIndex = Math.Max(0, sel);

            var all = Benchmarks.Load();
            var rows = new List<ItemRow>();
            double best = all.Count > 0 ? all.Max(r => r.Fps) : 0;
            foreach (var r in all.Take(30))
            {
                var res = r;
                var prev = all.FirstOrDefault(o => o.When < r.When && o.Route == r.Route);
                string delta = "";
                if (prev != null && prev.Fps > 0)
                {
                    double pct = (r.Fps - prev.Fps) / prev.Fps * 100;
                    delta = "  ·  " + (pct >= 0 ? "+" : "") + pct.ToString("0", CultureInfo.InvariantCulture) + "% vs " + prev.Label;
                }
                var row = new ItemRow(r.Fps.ToString("0", CultureInfo.InvariantCulture) + " FPS avg   ·   worst frame " + r.MaxMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms   ·   jitter " +
                    r.Jitter.ToString("0.00", CultureInfo.InvariantCulture) + " ms" + (all.Count > 1 && Math.Abs(r.Fps - best) < 0.01 ? "   ★ best" : ""),
                    r.Label + "  ·  " + r.Route + "  ·  " + When(r.When) + "  ·  " + r.Settings + delta);
                tips.SetToolTip(row.Sub, r.Gpu + (r.Source != null ? "  ·  source: " + r.Source : ""));
                var del = MkButton("", "", ButtonStyle.Ghost, "Delete this result.");
                del.Click += delegate
                {
                    var list = Benchmarks.Load();
                    list.RemoveAll(x => x.When == res.When && x.Label == res.Label);
                    try { Benchmarks.SaveAll(list); } catch { }
                    RefreshBenchmarks();
                };
                row.AddButton(del);
                rows.Add(row);
            }
            ReplaceRows(benchResultsCard, benchRows, rows);
            benchResultsCard.HelpText.Text = all.Count == 0 ? "No results yet. Run a benchmark above." :
                "Average FPS is the main number. Worst frame is the longest single frame (a stutter); jitter is how unevenly frames arrive (lower is smoother).";
            RelayoutCurrent();
        }

        static readonly Regex BenchTokenRx = new Regex(@"^(a|b|x|y|start|back|lb|rb|lt|rt|up|down|left|right|l3|r3)(:\d{2,5})?$");

        void RunBenchmark()
        {
            if (benchBusy) return;
            if (IsGameRunning()) { MessageBox.Show(this, "Close Skate 3 first. The benchmark starts its own session.", AppName); return; }
            if (gameExe == null) { MessageBox.Show(this, "Set the game location on the Launcher page first.", AppName); return; }
            int routeIdx = Math.Max(0, benchRoute.SelectedIndex);
            string inputs = BenchRouteInputs[routeIdx] ?? benchInputs.Box.Text.Trim();
            if (BenchRouteInputs[routeIdx] == null)
            {
                LauncherPrefs.Write("bench_custom", inputs);
                var bad = inputs.Split(',').Select(t => t.Trim().ToLowerInvariant()).Where(t => t.Length > 0 && !BenchTokenRx.IsMatch(t)).ToList();
                if (bad.Count > 0) { MessageBox.Show(this, "Unknown input" + (bad.Count == 1 ? "" : "s") + ": " + string.Join(", ", bad), AppName); return; }
            }
            if (!SaveSettings(false)) return;   // the benchmark runs on what's saved

            var overrides = new Dictionary<string, string>();
            string label = "Current settings";
            if (benchProfile.SelectedIndex > 0)
            {
                label = benchProfile.Items[benchProfile.SelectedIndex].ToString();
                string p = Profiles.Find(label);
                if (p == null) { MessageBox.Show(this, "That profile no longer exists.", AppName); return; }
                try { overrides = TomlSnapshot.Read(p); } catch (Exception ex) { MessageBox.Show(this, ex.Message, AppName); return; }
            }
            else
            {
                // describe the current setup by the preset it matches, if any
                foreach (var name in PresetNames)
                    if (Preset(name).All(kv => Toml.Norm(Val(kv.Key)) == Toml.Norm(kv.Value))) { label = name + " preset"; break; }
            }
            string summary = SettingsSummary(overrides);
            var gpu = TargetGpu();
            string gpuName = gpu != null ? gpu.Name : "unknown GPU";
            string route = routeIdx == BenchRouteNames.Length - 1 ? "custom route" : BenchRouteNames[routeIdx].Split('(')[0].Trim();
            int windows = BenchWindows[Math.Max(0, benchLength.SelectedIndex)];

            benchBusy = true;
            benchCancelRequested = false;
            benchRun.Enabled = false;
            benchCancel.Visible = true;
            benchStatus.ForeColor = Theme.Accent;
            benchStatus.Text = "Preparing…";
            RelayoutCurrent();
            string exe = gameExe;
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                BenchResult result = null;
                string error = null;
                try { result = Benchmarks.Run(exe, overrides, label, route, inputs, windows, summary, gpuName, s => Ui(delegate { benchStatus.Text = s; RelayoutCurrent(); }), delegate { return benchCancelRequested; }); }
                catch (OperationCanceledException) { error = "Benchmark stopped."; }
                catch (Exception ex) { error = ex.Message; }
                Ui(delegate
                {
                    benchBusy = false;
                    benchRun.Enabled = true;
                    benchCancel.Visible = false;
                    ReloadFromFile();
                    if (result != null)
                    {
                        benchStatus.ForeColor = Theme.Good;
                        benchStatus.Text = "Done: " + result.Fps.ToString("0", CultureInfo.InvariantCulture) + " FPS average, worst frame " + result.MaxMs.ToString("0.0", CultureInfo.InvariantCulture) + " ms. Your settings were restored.";
                    }
                    else
                    {
                        benchStatus.ForeColor = Theme.Warn;
                        benchStatus.Text = error + " Your settings were restored.";
                    }
                    Activate();
                    RefreshBenchmarks();
                });
            });
        }

        string SettingsSummary(Dictionary<string, string> overrides)
        {
            Func<string, string> v = k => { string x; return overrides.TryGetValue(k, out x) ? x : Val(k); };
            Func<string, bool> on = k => Toml.Norm(v(k)) == "true";
            var bits = new List<string>();
            bits.Add(Toml.Num(v("resolution_scale") ?? "1", 1).ToString("0", CultureInfo.InvariantCulture) + "x render");
            if (on("skate3_native_render_scene"))
            {
                double msaa = Toml.Num(v("skate3_native_render_scene_msaa") ?? "4", 4);
                bits.Add(msaa <= 1 ? "no MSAA" : "MSAA " + msaa.ToString("0", CultureInfo.InvariantCulture) + "x");
                if (on("skate3_native_render_scene_ssao")) bits.Add("SSAO");
                if (on("skate3_native_render_scene_shadow_pcss")) bits.Add("PCSS");
            }
            else bits.Add("emulated renderer");
            string api = Toml.Unquote(v("gpu_backend") ?? "'auto'").ToLowerInvariant();
            bits.Add(api == "d3d12" ? "D3D12" : api == "vulkan" ? "Vulkan" : "auto API");
            return string.Join(" · ", bits);
        }

        // ================================================================== Photo Mode (settings page extras)

        static readonly string[] BindKeys = { "bind_skate3_freecam", "bind_skate3_screenshot", "bind_skate3_native_render_toggle", "bind_skate3_menu_alt" };
        // Key names exactly as the recomp's keybind parser spells them (rexglue src/ui/keybinds.cpp); case matters.
        static readonly Regex BindRx = new Regex(@"^((Ctrl|Control|Shift|Alt|Super|Win)\+)*(F([1-9]|1[0-9]|2[0-4])|[A-Z0-9]|Backtick|Minus|Plus|Comma|Period|Semicolon|Slash|Backslash|LBracket|RBracket|Quote|Escape|Return|Space|Tab|Backspace|Delete|Insert|Home|End|PageUp|PageDown|Left|Right|Up|Down|Numpad[0-9]|NumpadEnter|NumpadPlus|NumpadMinus|NumpadStar|NumpadSlash|PrintScreen|Pause)$");

        // Keys the recomp already uses for its own tools (defaults from skate3_app_common.cpp and rexglue's rex_app.cpp).
        static readonly string[][] ReservedKeys =
        {
            new[] { "Escape", "the settings menu" }, new[] { "F2", "the FPS counter" }, new[] { "F3", "the debug overlay" }, new[] { "F4", "the settings overlay" },
            new[] { "F8", "a developer tool" }, new[] { "F9", "the log debug marker" }, new[] { "F10", "the log user marker" }, new[] { "F12", "the native renderer debug window" },
            new[] { "Backtick", "the console" }, new[] { "Ctrl+Shift+B", "the graphics showcase" }
        };
        Setting bindCapture;
        PillButton bindCaptureButton;
        Label shotsText;

        static Setting KeyBind(string key, string label, string def, string help)
        {
            return new Setting { Key = key, Label = label, Help = help + " Default: " + def + ".", Kind = Kind.Text, Default = Toml.Str(def) };
        }

        static Category BuildPhotoCategory()
        {
            var photo = new Category { Name = "Photo Mode", Glyph = "", Blurb = "Free camera, screenshots and the keys that drive them." };
            var speed = S("skate3_native_render_scene_freecam_speed", "Free camera speed", "How fast the free camera flies, in meters per second. Hold Shift for 4x, Ctrl for 0.2x.", 8, 0.5, 100, 0.5, true, " m/s");
            speed.Format = "0.0";
            photo.Settings.Add(speed);
            var look = S("skate3_native_render_scene_freecam_look_speed", "Free camera look speed", "How fast the arrow keys turn the free camera.", 90, 10, 360, 5, true, "°/s");
            look.Format = "0";
            photo.Settings.Add(look);
            photo.Settings.Add(B("skate3_native_render_scene_freecam_capture_input", "Freeze the skater while flying", "While the free camera is on, keyboard and controller input goes to the camera only, so flying doesn't also steer your skater.", true));
            photo.Settings.Add(KeyBind("bind_skate3_freecam", "Free camera key", "End", "Turns the free camera on and off. Needs Renderer = Native."));
            photo.Settings.Add(KeyBind("bind_skate3_screenshot", "Screenshot key", "F6", "Saves a PNG of the current frame into the screenshots folder next to skate3.exe."));
            photo.Settings.Add(KeyBind("bind_skate3_native_render_toggle", "Renderer toggle key", "F5", "Flips between the native (enhanced) and emulated (original) renderer while playing."));
            photo.Settings.Add(KeyBind("bind_skate3_menu_alt", "Settings menu key (alternate)", "F1", "Opens the recomp's settings menu. Escape always works too."));
            return photo;
        }

        void AddPhotoModeCards()
        {
            var info = pageInfos.FirstOrDefault(p => p.Name == "Photo Mode");
            if (info == null) return;
            var page = info.Page;

            var how = new ToolCard("Free camera controls",
                "Press the free camera key (End by default) in-game. WASD flies, E / Space goes up, Q / C goes down, the arrow keys or right-mouse drag look around, Z / X zoom, Shift is fast and Ctrl is slow. " +
                "The game keeps running underneath, so you can line up a shot of your skater mid-trick. The free camera needs Renderer = Native (enhanced). Field of view is on the Graphics page.");
            var fov = MkButton("Field of view setting", null, ButtonStyle.Ghost, null);
            fov.Click += delegate { var s = Find("skate3_field_of_view"); if (s != null) RevealSetting(s); };
            how.Controls.Add(fov);
            how.Body = (w, top) => Row(D(14), top, fov);

            var shots = new ToolCard("Screenshots", "");
            shotsText = MkLabel("", Theme.Base, Theme.Text);
            var openShots = MkButton("Open screenshots folder", "", ButtonStyle.Secondary, null);
            var latest = MkButton("Open latest", null, ButtonStyle.Ghost, null);
            openShots.Click += delegate { if (gameExe != null) OpenFolder(ShotsDir()); };
            latest.Click += delegate
            {
                var f = Shots().FirstOrDefault();
                if (f != null) try { Process.Start(new ProcessStartInfo(f.FullName) { UseShellExecute = true }); } catch { }
            };
            shots.Controls.AddRange(new Control[] { shotsText, openShots, latest });
            shots.Body = (w, top) =>
            {
                shotsText.MaximumSize = new Size(w - D(40), 0);
                shotsText.Location = new Point(D(20), top);
                return Row(D(20), shotsText.Bottom + D(12), openShots, latest);
            };

            var rebind = new ToolCard("Rebind a key", "Click a button, then press the key (or combo, like Ctrl+Shift+P) you want. Escape cancels. Saved with Save / PLAY.");
            var buttons = new List<PillButton>();
            foreach (var k in BindKeys)
            {
                var s = Find(k);
                if (s == null) continue;
                var setting = s;
                var b = MkButton(s.Label.Replace(" (alternate)", ""), "", ButtonStyle.Secondary, null);
                b.Click += delegate { StartBindCapture(setting, b); };
                buttons.Add(b);
                rebind.Controls.Add(b);
            }
            rebind.Body = (w, top) =>
            {
                int x = D(20), y = top, lineH = 0;
                foreach (var b in buttons)
                {
                    if (x + b.Width > w - D(20)) { x = D(20); y += lineH + D(6); lineH = 0; }
                    b.Location = new Point(x, y);
                    x += b.Width + D(8);
                    lineH = Math.Max(lineH, b.Height);
                }
                return y + lineH;
            };

            foreach (var c in new Card[] { rebind, shots, how }) { page.Cards.Insert(0, c); page.Controls.Add(c); }
            var prevShow = info.OnShow;
            info.OnShow = delegate { RefreshShots(); if (prevShow != null) prevShow(); };
        }

        string ShotsDir() { return Path.Combine(Path.GetDirectoryName(gameExe), "screenshots"); }

        List<FileInfo> Shots()
        {
            try
            {
                if (gameExe == null || !Directory.Exists(ShotsDir())) return new List<FileInfo>();
                return new DirectoryInfo(ShotsDir()).GetFiles("*.png").OrderByDescending(f => f.LastWriteTimeUtc).ToList();
            }
            catch { return new List<FileInfo>(); }
        }

        void RefreshShots()
        {
            if (shotsText == null) return;
            var list = Shots();
            shotsText.Text = gameExe == null ? "skate3.exe hasn't been found yet." :
                list.Count == 0 ? "No screenshots yet. Press " + Toml.Unquote(Val("bind_skate3_screenshot") ?? "'F6'") + " in-game to take one. They're saved to " + ShotsDir() + "." :
                list.Count + " screenshot" + (list.Count == 1 ? "" : "s") + " (" + Paths.Size(list.Sum(f => f.Length)) + ")  ·  latest " + When(list[0].LastWriteTime) + "  ·  " + ShotsDir();
            RelayoutCurrent();
        }

        void StartBindCapture(Setting s, PillButton b)
        {
            if (bindCaptureButton != null) bindCaptureButton.Text = bindCapture.Label.Replace(" (alternate)", "");
            bindCapture = s;
            bindCaptureButton = b;
            b.Text = "Press a key…";
            b.Focus();
            Flash("Press the new key for \"" + s.Label + "\" (Escape cancels).", Theme.Accent);
        }

        // Called from OnKeyDown. Returns true when the key was used for rebinding.
        bool HandleBindCapture(KeyEventArgs e)
        {
            if (bindCapture == null) return false;
            Keys k = e.KeyCode;
            if (k == Keys.ControlKey || k == Keys.ShiftKey || k == Keys.Menu) return true;   // wait for the real key
            var s = bindCapture;
            var b = bindCaptureButton;
            bindCapture = null;
            bindCaptureButton = null;
            b.Text = s.Label.Replace(" (alternate)", "");
            e.Handled = e.SuppressKeyPress = true;
            if (k == Keys.Escape) { UpdateStatus(); return true; }
            string name = null;
            if (k >= Keys.F1 && k <= Keys.F24) name = k.ToString();
            else if (k >= Keys.A && k <= Keys.Z) name = k.ToString();
            else if (k >= Keys.D0 && k <= Keys.D9) name = ((int)(k - Keys.D0)).ToString(CultureInfo.InvariantCulture);
            else if (k >= Keys.NumPad0 && k <= Keys.NumPad9) name = "Numpad" + (int)(k - Keys.NumPad0);
            else if (k == Keys.Home || k == Keys.End || k == Keys.Insert || k == Keys.Delete || k == Keys.Space || k == Keys.Pause || k == Keys.Tab) name = k.ToString();
            else if (k == Keys.PageUp) name = "PageUp";
            else if (k == Keys.PageDown) name = "PageDown";
            else if (k == Keys.Back) name = "Backspace";
            else if (k == Keys.Oemtilde) name = "Backtick";
            else if (k == Keys.OemMinus) name = "Minus";
            else if (k == Keys.Oemplus) name = "Plus";
            else if (k == Keys.Oemcomma) name = "Comma";
            else if (k == Keys.OemPeriod) name = "Period";
            else if (k == Keys.Multiply) name = "NumpadStar";
            else if (k == Keys.Add) name = "NumpadPlus";
            else if (k == Keys.Subtract) name = "NumpadMinus";
            else if (k == Keys.Divide) name = "NumpadSlash";
            if (name == null) { Flash("That key can't be used for a bind. Try an F key, a letter, a number or Home / End / Insert / Delete.", Theme.Warn); return true; }
            string combo = (e.Control ? "Ctrl+" : "") + (e.Shift ? "Shift+" : "") + (e.Alt ? "Alt+" : "") + name;
            SetEditor(s, Toml.Str(combo));
            OnEdited();
            Flash("\"" + s.Label + "\" set to " + combo + ". Save or PLAY to keep it.", Theme.Accent);
            return true;
        }

        // Extra conflict warnings for v3.1 settings (called at the end of ComputeWarnings).
        void ComputeV31Warnings(Dictionary<string, string> w)
        {
            // recomp bugs from known-issues.txt (shown first: they explain broken visuals, the others only explain no-ops)
            foreach (var r in KnownIssues.Rules)
            {
                if (w.ContainsKey(r.Key) || !KnownIssues.Matches(r, k => Val(k), installedVersion)) continue;
                w[r.Key] = r.Message;
            }

            var used = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var r in ReservedKeys) used[r[0]] = r[1];
            foreach (var k in BindKeys)
            {
                var s = Find(k);
                if (s == null) continue;
                string v = Toml.Unquote(Val(k) ?? "").Trim();
                if (v.Length == 0) { w[k] = "No key: this action can't be triggered from the keyboard."; continue; }
                if (!BindRx.IsMatch(v)) { w[k] = "\"" + v + "\" isn't a key name the launcher recognises. Use the Rebind buttons on this page."; continue; }
                string other;
                if (used.TryGetValue(v, out other)) w[k] = v + " is also used for " + other + ".";
                else used[v] = s.Label.ToLowerInvariant();
            }
            if (!On(KNative))
            {
                w["bind_skate3_freecam"] = w.ContainsKey("bind_skate3_freecam") ? w["bind_skate3_freecam"] : "The free camera only works with Renderer = Native (enhanced) on the Graphics page.";
                w["skate3_native_render_scene_freecam_speed"] = "Only used with Renderer = Native (enhanced).";
            }
        }

        // ================================================================== Launcher page additions: Steam, driver, controller

        Label steamText, driverText;
        ToggleSwitch navToggle;

        void AddLauncherV31Cards()
        {
            var info = pageInfos.FirstOrDefault(p => p.Name == "Launcher");
            if (info == null) return;
            var page = info.Page;

            var steam = new ToolCard("Add to Steam",
                "Adds Skate 3 to your Steam library as a non-Steam game, so it shows up in Big Picture and on a Steam Deck with the Steam overlay and controller config. " +
                "Steam has to be closed while the launcher edits your library. Artwork is optional: pick your own images and the launcher files them by shape (tall = library cover, very wide = hero banner, wide = grid, square = logo).");
            steamText = MkLabel("", Theme.Base, Theme.Text);
            var mode = new DarkCombo { Width = D(300) };
            mode.Items.AddRange(new object[] { "Open the launcher (controller friendly)", "Play straight away (quick play)" });
            mode.SelectedIndex = LauncherPrefs.Bool("steam_quickplay", false) ? 1 : 0;
            mode.SelectedIndexChanged += delegate { LauncherPrefs.SetBool("steam_quickplay", mode.SelectedIndex == 1); };
            var add = MkButton("Add to Steam", "", ButtonStyle.Secondary, null);
            var art = MkButton("Set artwork…", null, ButtonStyle.Ghost, "Choose up to four images (PNG or JPG).");
            add.Click += delegate { AddToSteam(mode.SelectedIndex == 1); };
            art.Click += delegate { SetSteamArtwork(); };
            steam.Controls.AddRange(new Control[] { steamText, mode, add, art });
            steam.Body = (w, top) =>
            {
                steamText.MaximumSize = new Size(w - D(40), 0);
                steamText.Location = new Point(D(20), top);
                return Row(D(20), steamText.Bottom + D(12), mode, add, art);
            };

            var driver = new ToolCard("Graphics driver",
                "Old or buggy GPU drivers are the most common cause of glitches and crashes in the recomp. The launcher reads your driver version, " +
                "flags drivers over a year old, and checks a list of known-bad versions kept in this launcher's GitHub repo.");
            driverText = MkLabel("", Theme.Base, Theme.Text);
            var recheck = MkButton("Check driver", "", ButtonStyle.Secondary, null);
            var get = MkButton("Get the latest driver", null, ButtonStyle.Ghost, null);
            recheck.Click += delegate { CheckDriver(true); };
            get.Click += delegate
            {
                var d = DriverCheck.Inspect(TargetGpu() != null ? TargetGpu().Name : null, Paths.LogsDir(gameExe));
                OpenUrl(d.DownloadUrl ?? "https://www.google.com/search?q=" + Uri.EscapeDataString((d.Gpu ?? "GPU") + " driver download"));
            };
            driver.Controls.AddRange(new Control[] { driverText, recheck, get });
            driver.Body = (w, top) =>
            {
                driverText.MaximumSize = new Size(w - D(40), 0);
                driverText.Location = new Point(D(20), top);
                return Row(D(20), driverText.Bottom + D(12), recheck, get);
            };

            var pad = new ToolCard("Controller navigation",
                "Drive the launcher with an Xbox controller: D-pad or left stick moves between controls, A selects, B closes a drop-down, LB / RB switch pages, Start plays and Back saves. " +
                "It's paused on the Controller page so the tester gets every button.");
            navToggle = MkToggle("controller_nav", true, on => { navEnabled = on; });
            var navLabel = MkLabel("Navigate the launcher with a controller", Theme.Base, Theme.Text);
            var whatsNew = MkButton("What's new in v" + LauncherVersion, null, ButtonStyle.Ghost, null);
            whatsNew.Click += delegate { ShowWhatsNew(); };
            pad.Controls.AddRange(new Control[] { navToggle, navLabel, whatsNew });
            pad.Body = (w, top) => { int y = ToggleLine(D(20), top, navToggle, navLabel); return Row(D(14), y + D(12), whatsNew); };

            // after Updates, before the cache card
            int at = Math.Min(page.Cards.Count, 2);
            foreach (var c in new Card[] { pad, driver, steam }) { page.Cards.Insert(at, c); page.Controls.Add(c); }
            var prevShow = info.OnShow;
            info.OnShow = delegate { if (prevShow != null) prevShow(); RefreshSteamText(); CheckDriver(false); };
        }

        void RefreshSteamText()
        {
            if (steamText == null) return;
            string steamDir = SteamShortcut.SteamDir();
            if (steamDir == null) { steamText.Text = "Steam isn't installed (or hasn't been run on this PC yet)."; steamText.ForeColor = Theme.Muted; return; }
            bool added = SteamShortcut.Exists(Paths.LauncherExe);
            steamText.Text = (added ? "Skate 3 is in your Steam library." : "Not in your Steam library yet.") + (SteamShortcut.SteamRunning() ? "  Steam is running: close it before adding." : "");
            steamText.ForeColor = added ? Theme.Good : Theme.Text;
        }

        bool SteamClosedGuard()
        {
            if (!SteamShortcut.SteamRunning()) return true;
            return DarkDialog.Ask(this, "Close Steam first",
                "Steam rewrites its library file when it exits, which would undo the change. Quit Steam (Steam menu → Exit, or right-click the tray icon → Exit), then continue.",
                "I've closed Steam", "Cancel") == 0 && !SteamShortcut.SteamRunning();
        }

        void AddToSteam(bool quickPlay)
        {
            if (!SteamClosedGuard()) { if (SteamShortcut.SteamRunning()) Flash("Steam is still running, so nothing was changed.", Theme.Warn); return; }
            try
            {
                SteamShortcut.Add(Paths.LauncherExe, quickPlay ? "--play" : "", gameExe ?? Paths.LauncherExe);
                Flash("Added \"" + SteamShortcut.AppName + "\" to Steam. Start Steam to see it in your library.", Theme.Good);
            }
            catch (Exception ex) { MessageBox.Show(this, "Couldn't add the game to Steam:\n\n" + ex.Message, AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            RefreshSteamText();
        }

        void SetSteamArtwork()
        {
            if (!SteamShortcut.Exists(Paths.LauncherExe)) { MessageBox.Show(this, "Add the game to Steam first.", AppName); return; }
            using (var dlg = new OpenFileDialog { Title = "Choose artwork (up to 4 images)", Filter = "Images|*.png;*.jpg;*.jpeg", Multiselect = true })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    uint id = SteamShortcut.AppId("\"" + Paths.LauncherExe + "\"", SteamShortcut.AppName);
                    var slots = SteamShortcut.SetArtwork(id, dlg.FileNames.Take(4));
                    Flash("Artwork set: " + string.Join(", ", slots) + ". Restart Steam to see it.", Theme.Good);
                }
                catch (Exception ex) { MessageBox.Show(this, "Couldn't set the artwork:\n\n" + ex.Message, AppName); }
            }
        }

        bool driverChecking;

        void CheckDriver(bool online)
        {
            if (driverText == null || driverChecking) return;
            driverChecking = true;
            string gpu = TargetGpu() != null ? TargetGpu().Name : null;
            string logs = Paths.LogsDir(gameExe);
            System.Threading.ThreadPool.QueueUserWorkItem(delegate
            {
                var d = DriverCheck.Inspect(gpu, logs);
                string netError = null;
                List<string> known = online ? DriverCheck.KnownIssues(d, out netError) : new List<string>();
                Ui(delegate
                {
                    driverChecking = false;
                    var sb = new StringBuilder();
                    sb.Append(d.Gpu ?? "Unknown GPU");
                    if (d.Version != null) sb.Append("  ·  driver ").Append(d.Version);
                    if (d.Date != DateTime.MinValue) sb.Append(" (").Append(d.Date.ToString("d MMM yyyy", CultureInfo.InvariantCulture)).Append(")");
                    if (d.VulkanDriver != null) sb.Append("\nVulkan driver reported by the game: ").Append(d.VulkanDriver);
                    foreach (var x in d.Warnings) sb.Append("\n⚠  ").Append(x);
                    foreach (var x in known) sb.Append("\n⚠  Known issue with this driver: ").Append(x);
                    if (online && netError == null && known.Count == 0) sb.Append("\nNo known problems with this driver.");
                    if (online && netError != null) sb.Append("\nCouldn't download the known-issues list: ").Append(netError);
                    driverText.Text = sb.ToString();
                    driverText.ForeColor = d.Warnings.Count + known.Count > 0 ? Theme.Warn : Theme.Text;
                    driverInfoCache = d;
                    RelayoutCurrent();
                });
            });
        }

        DriverInfo driverInfoCache;

        // ================================================================== Logs page additions: crash reports

        Label crashText;

        void AddLogsV31Cards()
        {
            var info = pageInfos.FirstOrDefault(p => p.Name == "Logs");
            if (info == null) return;
            var card = new ToolCard("Report a crash",
                "Opens a pre-filled GitHub issue for the right project, with your versions, PC, driver, mods and the last session's errors. The full report is copied to your clipboard so you can paste it in. " +
                "Windows can also keep a small crash dump each time skate3.exe crashes, which developers can open in a debugger: turning that on needs one admin prompt.");
            crashText = MkLabel("", Theme.Base, Theme.Text);
            var report = MkButton("Report on GitHub…", null, ButtonStyle.Secondary, null);
            var dumps = MkButton("Keep crash dumps", null, ButtonStyle.Ghost, "Asks Windows to save a small dump file when skate3.exe crashes (needs admin once).");
            var openDumps = MkButton("Open crash dumps folder", "", ButtonStyle.Ghost, CrashDumps.DumpDir);
            report.Click += delegate { ReportOnGitHub(); };
            dumps.Click += delegate
            {
                if (CrashDumps.Enabled()) { Flash("Crash dumps are already on.", Theme.Good); return; }
                Flash(CrashDumps.Enable() ? "Crash dumps are on. The next crash leaves a .dmp in " + CrashDumps.DumpDir + "." : "Crash dumps weren't turned on (the admin prompt was declined).",
                      CrashDumps.Enabled() ? Theme.Good : Theme.Warn);
                RefreshCrashText();
            };
            openDumps.Click += delegate { OpenFolder(CrashDumps.DumpDir); };
            card.Controls.AddRange(new Control[] { crashText, report, dumps, openDumps });
            card.Body = (w, top) =>
            {
                crashText.MaximumSize = new Size(w - D(40), 0);
                crashText.Location = new Point(D(20), top);
                return Row(D(20), crashText.Bottom + D(12), report, dumps, openDumps);
            };
            info.Page.Cards.Insert(1, card);
            info.Page.Controls.Add(card);
            var prevShow = info.OnShow;
            info.OnShow = delegate { RefreshCrashText(); if (prevShow != null) prevShow(); };
        }

        void RefreshCrashText()
        {
            if (crashText == null) return;
            var dumps = CrashDumps.Find();
            crashText.Text = (CrashDumps.Enabled() ? "Crash dumps: on." : "Crash dumps: off.") +
                (dumps.Count > 0 ? "  " + dumps.Count + " dump" + (dumps.Count == 1 ? "" : "s") + ", newest " + When(dumps[0].LastWriteTime) + " (included in the bug report zip)." : "");
        }

        // Extra lines for the bug report (called from BuildBugReport).
        void AppendV31Report(StringBuilder sb)
        {
            var d = driverInfoCache ?? DriverCheck.Inspect(TargetGpu() != null ? TargetGpu().Name : null, Paths.LogsDir(gameExe));
            sb.AppendLine("Driver: " + (d.Gpu ?? "?") + " " + (d.Version ?? "") + (d.Date != DateTime.MinValue ? " (" + d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + ")" : "") +
                (d.VulkanDriver != null ? ", Vulkan " + d.VulkanDriver : ""));
            try
            {
                var mods = gameExe != null && Mods.LoaderEnabled ? Mods.List(gameExe).Where(m => m.Enabled).ToList() : new List<ModInfo>();
                sb.AppendLine("Mods: " + (mods.Count == 0 ? "none" : string.Join("; ", mods.Select(m => m.Display + (m.Version != null ? " v" + m.Version : "")))));
            }
            catch { }
            var g = GameFiles.Last;
            if (g != null)
                sb.AppendLine("Game files: " + (g.SetupOk ? "setup OK" : "setup incomplete") + ", xex " + (g.XexKnown ? "known" : "unknown") + ", TU3 " + (g.TitleUpdate ? "staged" : "missing") +
                    ", " + g.Missing.Count + " missing, " + g.Changed.Count + " different (" + (g.Full ? "full" : "quick") + " check)");
            var dumps = CrashDumps.Find();
            sb.AppendLine("Crash dumps: " + (CrashDumps.Enabled() ? "on" : "off") + (dumps.Count > 0 ? ", newest " + dumps[0].Name + " (" + When(dumps[0].LastWriteTime) + ")" : ""));
        }

        // Adds the newest crash dump (if small enough) to the bug report zip.
        void AddCrashDumpToZip(ZipArchive zip)
        {
            var d = CrashDumps.Find().FirstOrDefault();
            if (d != null && d.Length < 150L * 1024 * 1024 && (DateTime.Now - d.LastWriteTime).TotalDays < 14)
                try { AddFileToZip(zip, d.FullName, "dumps/" + d.Name); } catch { }
        }

        void ReportOnGitHub()
        {
            int c = DarkDialog.Ask(this, "Where should this go?",
                "Problems with the game itself (crashes, graphics, sound, gameplay) go to the Skate 3 Recomp project. Problems with this launcher's pages, settings or buttons go to the launcher's project.",
                "The game (Skate 3 Recomp)", "This launcher", "Cancel");
            if (c < 0 || c > 1) return;
            string repo = c == 0 ? UpdateRepo : "Trooper-code/Troopers-Skate-3-Recomp-Launcher";
            string full = BuildBugReport();
            try { Clipboard.SetText(full); } catch { }
            var s = LogTools.LastSession(Paths.LogsDir(gameExe));
            string title = c == 0 ? "Crash/bug: " + (s != null && !s.Clean ? "game didn't shut down cleanly" : "describe the problem") : "Launcher: describe the problem";
            var head = string.Join("\n", full.Split('\n').TakeWhile(l => !l.StartsWith("---- settings.toml")).Select(l => l.TrimEnd('\r')));
            string body = "**What happened:**\n\n\n**Steps to reproduce:**\n\n\n**System (from Trooper's Skate 3 Recomp Launcher v" + LauncherVersion + "):**\n```\n" + head.Trim() + "\n```\n\n" +
                          "<details><summary>Full report</summary>\n\n(The full report is on your clipboard: paste it here.)\n\n</details>\n";
            if (body.Length > 6000) body = body.Substring(0, 6000);
            OpenUrl("https://github.com/" + repo + "/issues/new?title=" + Uri.EscapeDataString(title) + "&body=" + Uri.EscapeDataString(body));
            Flash("Opened a new GitHub issue. The full report is on your clipboard: paste it into the issue.", Theme.Good);
        }

        // ================================================================== controller navigation

        Timer navTimer;
        bool navEnabled, navCuesShown;
        ushort navPrev;
        int navSlot = -1;
        DateTime navSlotScan, navRepeatAt;

        const ushort PadUp = 0x0001, PadDown = 0x0002, PadLeft = 0x0004, PadRight = 0x0008, PadStart = 0x0010, PadBack = 0x0020,
                     PadLB = 0x0100, PadRB = 0x0200, PadA = 0x1000, PadB = 0x2000;

        void NavTick()
        {
            if (!navEnabled || !Visible || WindowState == FormWindowState.Minimized || Form.ActiveForm != this) { navPrev = 0; return; }
            if (currentPage != null && currentPage.Name == "Controller") { navPrev = 0; return; }

            // only poll the slot that has a controller; rescan the others every couple of seconds (empty slots are slow to query)
            XInput.State st;
            bool ok = navSlot >= 0 && XInput.Get(navSlot, out st);
            if (!ok)
            {
                if (DateTime.Now < navSlotScan) { navPrev = 0; return; }
                navSlotScan = DateTime.Now.AddSeconds(2);
                navSlot = -1;
                for (int i = 0; i < 4; i++) if (XInput.Get(i, out st)) { navSlot = i; ok = true; break; }
                if (!ok) { navPrev = 0; return; }
            }
            XInput.Get(navSlot, out st);
            ushort b = st.Pad.Buttons;
            if (st.Pad.LY > 20000) b |= PadUp;
            if (st.Pad.LY < -20000) b |= PadDown;
            if (st.Pad.LX < -20000) b |= PadLeft;
            if (st.Pad.LX > 20000) b |= PadRight;
            ushort pressed = (ushort)(b & ~navPrev);
            ushort dirs = (ushort)(b & (PadUp | PadDown | PadLeft | PadRight));
            if ((pressed & dirs) != 0) navRepeatAt = DateTime.Now.AddMilliseconds(380);
            else if (dirs != 0 && DateTime.Now >= navRepeatAt) { pressed |= dirs; navRepeatAt = DateTime.Now.AddMilliseconds(110); }
            navPrev = b;
            if (pressed == 0) return;

            if (!navCuesShown) { navCuesShown = true; Native.SendMessage(Handle, 0x0127, (IntPtr)0x00010002, IntPtr.Zero); }   // show focus rectangles
            if ((pressed & PadUp) != 0) NavMove(false);
            if ((pressed & PadDown) != 0) NavMove(true);
            if ((pressed & PadLeft) != 0) NavAdjust(-1);
            if ((pressed & PadRight) != 0) NavAdjust(1);
            if ((pressed & PadA) != 0) NavActivate();
            if ((pressed & PadB) != 0) { var cb = ActiveControl as DarkCombo; if (cb != null && cb.DroppedDown) cb.DroppedDown = false; }
            if ((pressed & PadLB) != 0) NavPage(-1);
            if ((pressed & PadRB) != 0) NavPage(1);
            if ((pressed & PadStart) != 0 && !IsGameRunning()) Play(null);
            if ((pressed & PadBack) != 0) SaveSettings(true);
        }

        void NavMove(bool forward)
        {
            var cb = ActiveControl as DarkCombo;
            if (cb != null && cb.DroppedDown)
            {
                cb.SelectedIndex = Math.Max(0, Math.Min(cb.Items.Count - 1, cb.SelectedIndex + (forward ? 1 : -1)));
                return;
            }
            Control start = ActiveControl ?? currentPage.Page;
            if (start is ScrollPanel || start == null)
            {
                // land on the first control of the page instead of the sidebar
                var first = FirstFocusable(currentPage.Page);
                if (first != null) { first.Focus(); NavReveal(first); return; }
            }
            SelectNextControl(start, forward, true, true, true);
            if (ActiveControl != null) NavReveal(ActiveControl);
        }

        static Control FirstFocusable(Control root)
        {
            foreach (Control c in root.Controls.Cast<Control>().OrderBy(c => c.Top).ThenBy(c => c.Left))
            {
                if (!c.Visible || !c.Enabled) continue;
                if (c.CanSelect && c.TabStop && !(c is Card)) return c;
                var inner = FirstFocusable(c);
                if (inner != null) return inner;
            }
            return null;
        }

        void NavAdjust(int dir)
        {
            var c = ActiveControl;
            var sl = c as Slider;
            if (sl != null) { sl.Value += dir * sl.SmallChange; return; }
            var seg = c as Segmented;
            if (seg != null) { seg.SelectedIndex += dir; return; }
            var cb = c as DarkCombo;
            if (cb != null) { if (cb.Items.Count > 0) cb.SelectedIndex = Math.Max(0, Math.Min(cb.Items.Count - 1, cb.SelectedIndex + dir)); return; }
            var t = c as ToggleSwitch;
            if (t != null) { t.Checked = dir > 0; return; }
            NavMove(dir > 0);   // anywhere else, left/right moves like up/down
        }

        void NavActivate()
        {
            var c = ActiveControl;
            if (c == null) return;
            var cb = c as DarkCombo;
            if (cb != null) { cb.DroppedDown = !cb.DroppedDown; return; }
            if (c is TextBox || c is ScrollPanel || c is ListBox) return;
            InvokeOnClick(c, EventArgs.Empty);
        }

        void NavPage(int dir)
        {
            if (currentPage == null) return;
            int i = pageInfos.IndexOf(currentPage);
            int n = pageInfos.Count;
            var next = pageInfos[((i + dir) % n + n) % n];
            ShowPage(next.Name);
            var first = FirstFocusable(next.Page);
            if (first != null) { first.Focus(); NavReveal(first); }
        }

        // Scrolls the page so a control reached with the controller is on screen (ScrollPanel deliberately doesn't do this for mouse focus).
        static void NavReveal(Control c)
        {
            Control p = c.Parent;
            while (p != null && !(p is ScrollPanel)) p = p.Parent;
            var page = p as ScrollPanel;
            if (page == null) return;
            var r = page.RectangleToClient(c.RectangleToScreen(c.ClientRectangle));
            int margin = Dpi.S(40);
            if (r.Top >= margin && r.Bottom <= page.ClientSize.Height - margin) return;
            int y = -page.AutoScrollPosition.Y + (r.Top < margin ? r.Top - margin : r.Bottom - page.ClientSize.Height + margin);
            page.AutoScrollPosition = new Point(0, Math.Max(0, y));
        }
    }
}
