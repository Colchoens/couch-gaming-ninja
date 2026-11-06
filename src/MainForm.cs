using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace CouchGamingNinja
{
    public class MainForm : Form
    {
        public const string RepoUrl = "https://github.com/Colchoens/couch-gaming-ninja";
        public const string SponsorUrl = "https://github.com/sponsors/Colchoens";

        // sidebar
        Label epicStatus, steamStatus, folderLabel;
        LinkLabel signOut;

        // content
        Panel content;
        Panel loginView, libraryView;
        FlowLayoutPanel list;
        Label summary, busyLabel, emptyLabel, steamWarning;
        Button applyButton, rescanButton, loginButton;
        Label loginError;
        readonly Dictionary<string, Button> filterButtons = new Dictionary<string, Button>();
        string filter = "installed";

        // data
        List<EpicGame> games = new List<EpicGame>();
        HashSet<string> inSteam = new HashSet<string>(StringComparer.OrdinalIgnoreCase);   // titles with our shortcut
        HashSet<string> wantSteam = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // by AppName
        HashSet<string> wantRetro = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // by AppName
        HashSet<string> needRepair = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // our shortcuts pointing at an old folder
        string account;
        bool busy;

        public MainForm()
        {
            Text = AppPaths.AppName;
            BackColor = Theme.Ground;
            ForeColor = Theme.Text;
            Font = Theme.Ui(10);
            MinimumSize = new Size(900, 620);
            Size = new Size(1180, 780);
            StartPosition = FormStartPosition.CenterScreen;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            content = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Ground, Padding = new Padding(36, 28, 36, 24) };
            Controls.Add(content);
            Controls.Add(BuildSidebar());

            loginView = BuildLoginView();
            libraryView = BuildLibraryView();
            content.Controls.Add(libraryView);
            content.Controls.Add(loginView);

            Shown += (s, e) => Startup();
        }

        // ---------------- layout ----------------

        Control BuildSidebar()
        {
            var side = new Panel { Dock = DockStyle.Left, Width = 250, BackColor = Theme.Side, Padding = new Padding(20, 26, 20, 20) };

            var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Side };
            stack.Controls.Add(Theme.MakeLabel(AppPaths.AppName, 14, Theme.Text, true));
            stack.Controls.Add(Theme.MakeLabel("v" + AppPaths.Version + " · free & open source", 9, Theme.Muted));
            stack.Controls.Add(Spacer(22));

            stack.Controls.Add(NavItem("Epic → Steam", true));
            stack.Controls.Add(NavItem("RetroBat (soon)", false));
            stack.Controls.Add(NavItem("Wiimotes (soon)", false));
            stack.Controls.Add(NavItem("Save backup (soon)", false));
            side.Controls.Add(stack);

            var bottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Theme.Side };
            epicStatus = Theme.MakeLabel("Epic: not signed in", 10, Theme.Muted);
            steamStatus = Theme.MakeLabel("Steam: checking…", 10, Theme.Muted);
            signOut = MakeLink("Sign out of Epic", (s, e) => SignOut());
            signOut.Visible = false;
            bottom.Controls.Add(epicStatus);
            bottom.Controls.Add(steamStatus);
            bottom.Controls.Add(signOut);
            bottom.Controls.Add(Spacer(8));
            folderLabel = Theme.MakeLabel("", 8.5f, Theme.Muted);
            folderLabel.MaximumSize = new Size(206, 0);
            bottom.Controls.Add(folderLabel);
            bottom.Controls.Add(MakeLink("Move app folder…", (s, e) => MoveFolder()));
            bottom.Controls.Add(Spacer(8));
            bottom.Controls.Add(MakeLink("♥ Sponsor on GitHub", (s, e) => Open(SponsorUrl)));
            bottom.Controls.Add(MakeLink("Project page", (s, e) => Open(RepoUrl)));
            bottom.Controls.Add(MakeLink("Remove everything…", (s, e) => RemoveEverything()));
            side.Controls.Add(bottom);
            return side;
        }

        static Control Spacer(int h) { return new Panel { Height = h, Width = 10, BackColor = Color.Transparent }; }


        static Label NavItem(string text, bool active)
        {
            var l = Theme.MakeLabel(text, 11, active ? Theme.Text : Theme.Muted, active);
            l.AutoSize = false;
            l.Size = new Size(206, 40);
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Padding = new Padding(10, 0, 0, 0);
            l.BackColor = active ? Color.FromArgb(0x1F, 0x25, 0x33) : Theme.Side;
            l.Margin = new Padding(0, 0, 0, 4);
            return l;
        }

        static LinkLabel MakeLink(string text, LinkLabelLinkClickedEventHandler onClick)
        {
            var l = new LinkLabel
            {
                Text = text,
                AutoSize = true,
                LinkColor = Theme.Accent,
                ActiveLinkColor = Theme.Text,
                VisitedLinkColor = Theme.Accent,
                Font = Theme.Ui(10),
                Margin = new Padding(0, 4, 0, 4),
                LinkBehavior = LinkBehavior.HoverUnderline
            };
            l.LinkClicked += onClick;
            return l;
        }

        Panel BuildLoginView()
        {
            var v = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Ground };
            var card = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Theme.Card, Padding = new Padding(36), Anchor = AnchorStyles.None };
            var title = Theme.MakeLabel("Play your Epic games from Steam,\nwith any controller.", 20, Theme.Text, true);
            card.Controls.Add(title);
            card.Controls.Add(Spacer(8));
            card.Controls.Add(Theme.MakeLabel("Sign in once. No Epic Launcher or Heroic needs to stay open,\nand nothing runs in the background.", 11, Theme.Muted));
            card.Controls.Add(Spacer(16));
            card.Controls.Add(Theme.MakeLabel("1   Downloads Legendary, the open-source Epic client (about 8 MB)", 11, Theme.Text));
            card.Controls.Add(Theme.MakeLabel("2   Opens the official Epic sign-in window", 11, Theme.Text));
            card.Controls.Add(Theme.MakeLabel("3   Finds the games you already installed", 11, Theme.Text));
            card.Controls.Add(Spacer(20));
            loginButton = Theme.MakeButton("Sign in with Epic Games", true);
            loginButton.Click += (s, e) => SignIn();
            card.Controls.Add(loginButton);
            card.Controls.Add(Spacer(8));
            card.Controls.Add(Theme.MakeLabel("Your password goes only to Epic. This app never sees it.", 9.5f, Theme.Muted));
            loginError = Theme.MakeLabel("", 10, Theme.Bad);
            loginError.MaximumSize = new Size(520, 0);
            card.Controls.Add(loginError);
            v.Controls.Add(card);
            v.Resize += (s, e) => card.Location = new Point(Math.Max(0, (v.Width - card.Width) / 2), Math.Max(0, (v.Height - card.Height) / 2));
            return v;
        }

        Panel BuildLibraryView()
        {
            var v = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Ground };

            var header = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Theme.Ground };
            header.Controls.Add(Theme.MakeLabel("Your Epic games", 22, Theme.Text, true));
            var sub = Theme.MakeLabel("Pick the games you want in Steam. Each one gets its own shortcut with Steam Input on, so any controller works, and Steam tracks it until you quit.", 10.5f, Theme.Muted);
            sub.MaximumSize = new Size(760, 0);
            header.Controls.Add(sub);
            steamWarning = Theme.MakeLabel("", 10.5f, Theme.Warn);
            steamWarning.MaximumSize = new Size(760, 0);
            steamWarning.Visible = false;
            header.Controls.Add(steamWarning);

            var filters = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Ground, Margin = new Padding(0, 14, 0, 10), WrapContents = true };
            foreach (var f in new[] { new[] { "installed", "Installed" }, new[] { "steam", "In Steam" }, new[] { "all", "All" } })
            {
                string id = f[0];
                var b = Theme.MakeButton(f[1], false);
                b.Click += (s, e) => { filter = id; RenderList(); };
                filterButtons[id] = b;
                filters.Controls.Add(b);
            }
            rescanButton = Theme.MakeButton("Rescan", false);
            rescanButton.Click += (s, e) => Refresh(true);
            filters.Controls.Add(rescanButton);
            header.Controls.Add(filters);

            list = new NoJumpFlowPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BackColor = Theme.Ground };
            list.Resize += (s, e) => { foreach (Control c in list.Controls) c.Width = RowWidth(); };
            emptyLabel = Theme.MakeLabel("", 11, Theme.Muted);
            emptyLabel.MaximumSize = new Size(700, 0);

            var footer = new Panel { Dock = DockStyle.Bottom, Height = 76, BackColor = Theme.Panel, Padding = new Padding(18, 12, 18, 12) };
            var ftext = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = Theme.Panel };
            summary = Theme.MakeLabel("", 11, Theme.Text, true);
            busyLabel = Theme.MakeLabel("Steam closes for a few seconds while shortcuts are written, then reopens.", 9.5f, Theme.Muted);
            ftext.Controls.Add(summary);
            ftext.Controls.Add(busyLabel);
            applyButton = Theme.MakeButton("Apply to Steam", true);
            applyButton.Dock = DockStyle.Right;
            applyButton.Click += (s, e) => Apply();
            footer.Controls.Add(ftext);
            footer.Controls.Add(applyButton);

            v.Controls.Add(list);
            v.Controls.Add(footer);
            v.Controls.Add(header);
            return v;
        }

        int RowWidth() { return Math.Max(400, list.ClientSize.Width - 24); }

        Control BuildRow(EpicGame g)
        {
            var row = new TableLayoutPanel
            {
                Width = RowWidth(),
                Height = 84,
                ColumnCount = 4,
                RowCount = 1,
                BackColor = Theme.Card,
                Padding = new Padding(12),
                Margin = new Padding(0, 0, 0, 10)
            };
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            row.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var cover = new Label
            {
                Text = Initials(g.Title),
                Size = new Size(58, 58),
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Theme.CoverColor(g.AppName),
                ForeColor = Theme.Text,
                Font = Theme.Head(14),
                Margin = new Padding(0)
            };
            row.Controls.Add(cover, 0, 0);

            var text = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, BackColor = Theme.Card, Anchor = AnchorStyles.Left };
            text.Controls.Add(Theme.MakeLabel(g.Title, 12, Theme.Text, true));
            string detail = g.Installed ? g.InstallPath
                : g.HasInstall ? g.InstallPath + "  ·  found (" + (g.Source == "heroic" ? "Heroic" : "Epic Launcher") + "), imported on Apply"
                : "Not installed";
            if (inSteam.Contains(g.Title)) detail += "  ·  in Steam";
            var d = Theme.MakeLabel(detail, 9.5f, Theme.Muted);
            d.AutoEllipsis = true;
            d.AutoSize = false;
            d.Size = new Size(Math.Max(200, RowWidth() - 520), 22);
            text.Controls.Add(d);
            row.Controls.Add(text, 1, 0);

            bool usable = g.Installed || g.HasInstall;
            if (usable)
            {
                var retro = new CheckBox
                {
                    Text = "RetroBat shortcut",
                    AutoSize = true,
                    Checked = wantRetro.Contains(g.AppName),
                    ForeColor = Theme.Text,
                    Font = Theme.Ui(10),
                    Anchor = AnchorStyles.None,
                    Margin = new Padding(8, 0, 12, 0)
                };
                retro.CheckedChanged += (s, e) =>
                {
                    if (retro.Checked && !EnsureRetroBatFolder()) { retro.Checked = false; return; }
                    if (retro.Checked) { wantRetro.Add(g.AppName); wantSteam.Add(g.AppName); } else wantRetro.Remove(g.AppName);
                    RenderList();
                };
                row.Controls.Add(retro, 2, 0);

                bool on = wantSteam.Contains(g.AppName);
                var btn = Theme.MakeButton(on ? "In Steam ✓" : "Add to Steam", false);
                if (on) { btn.ForeColor = Theme.Accent; btn.FlatAppearance.BorderColor = Theme.Accent; btn.BackColor = Theme.Card; }
                btn.Anchor = AnchorStyles.None;
                btn.Click += (s, e) =>
                {
                    if (wantSteam.Contains(g.AppName)) { wantSteam.Remove(g.AppName); wantRetro.Remove(g.AppName); }
                    else wantSteam.Add(g.AppName);
                    RenderList();
                };
                row.Controls.Add(btn, 3, 0);
            }
            else
            {
                var locate = Theme.MakeButton("Locate folder…", false);
                locate.Anchor = AnchorStyles.None;
                locate.Click += (s, e) => Locate(g);
                row.SetColumnSpan(locate, 2);
                row.Controls.Add(locate, 2, 0);
            }
            return row;
        }

        static string Initials(string title)
        {
            var words = (title ?? "?").Split(new[] { ' ', ':', '-' }, StringSplitOptions.RemoveEmptyEntries).Where(w => char.IsLetterOrDigit(w[0])).ToArray();
            if (words.Length == 0) return "?";
            if (words.Length == 1) return words[0].Substring(0, Math.Min(2, words[0].Length)).ToUpperInvariant();
            return (words[0][0].ToString() + words[1][0]).ToUpperInvariant();
        }

        // ---------------- behaviour ----------------

        /// <summary>--demo: shows sample data without touching Epic or Steam (for screenshots).</summary>
        public static bool Demo;

        void Startup()
        {
            folderLabel.Text = "App folder: " + AppPaths.Root;
            UpdateSteamStatus();
            if (Demo)
            {
                account = "demo-player";
                games = new List<EpicGame>
                {
                    new EpicGame { AppName = "Sugar", Title = "Rocket League®", Installed = true, InstallPath = @"C:\Games\rocketleague", Executable = "Binaries/Win64/RocketLeague.exe" },
                    new EpicGame { AppName = "Fall", Title = "Fall Guys", Installed = true, InstallPath = @"C:\Games\FallGuys" },
                    new EpicGame { AppName = "AW2", Title = "Alan Wake 2", InstallPath = Environment.GetFolderPath(Environment.SpecialFolder.Windows), Source = "epic" },
                    new EpicGame { AppName = "DS", Title = "Death Stranding" }
                };
                inSteam = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Rocket League®" };
                wantSteam = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Sugar" };
                ShowLibrary();
                RenderList();
                steamWarning.Visible = false;
                steamStatus.Text = "● Steam is running";
                steamStatus.ForeColor = Theme.Text;
                SetApplyEnabled(true);
                return;
            }
            if (!Legendary.IsDownloaded) { ShowLogin(); return; }
            RunBusy("Checking your Epic sign-in…", () => { account = Legendary.Account(); }, () =>
            {
                if (account == null) ShowLogin(); else Refresh(false);
            });
        }

        void ShowLogin()
        {
            if (!busy && loginError.ForeColor != Theme.Bad) loginError.Text = "";
            loginView.Visible = true;
            libraryView.Visible = false;
            loginView.BringToFront();
            UpdateEpicStatus();
        }

        void ShowLibrary()
        {
            loginView.Visible = false;
            libraryView.Visible = true;
            libraryView.BringToFront();
            UpdateEpicStatus();
        }

        void SignIn()
        {
            loginError.Text = "";
            RunBusy("Downloading Legendary…", () =>
            {
                if (!Legendary.IsDownloaded) Legendary.Download();
                SetBusyText("Waiting for the Epic sign-in window…");
                var r = Legendary.Login();
                account = Legendary.Account();
                if (account == null)
                    throw new Exception("Sign-in did not finish. " + LastLine(r.Error));
            }, () => Refresh(false), ex =>
            {
                loginError.ForeColor = Theme.Bad;
                loginError.Text = ex is System.Net.WebException
                    ? "Couldn't download Legendary from GitHub. Check your internet connection and try again."
                    : ex.Message;
                ShowLogin();
            });
        }

        void SignOut()
        {
            if (MessageBox.Show(this, "Sign out of Epic? Games already in Steam will stop launching until you sign in again.", AppPaths.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            RunBusy("Signing out…", () => Legendary.Logout(), () => { account = null; ShowLogin(); });
        }

        void Refresh(bool keepChoices)
        {
            var oldWantSteam = new HashSet<string>(wantSteam, StringComparer.OrdinalIgnoreCase);
            var oldWantRetro = new HashSet<string>(wantRetro, StringComparer.OrdinalIgnoreCase);
            RunBusy("Loading your games…", () =>
            {
                games = Legendary.Games();
                inSteam = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                needRepair = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var cfg = Steam.UserConfigDir;
                if (cfg != null)
                {
                    var sc = Steam.LoadShortcuts(cfg);
                    inSteam = Steam.OurShortcutNames(sc);
                    needRepair = Steam.OurShortcutsNeedingRepair(sc, AppPaths.LegendaryExe.Replace("'", "''"));
                }
            }, () =>
            {
                wantSteam = new HashSet<string>(games.Where(g => inSteam.Contains(g.Title)).Select(g => g.AppName), StringComparer.OrdinalIgnoreCase);
                wantRetro = new HashSet<string>(games.Where(g => inSteam.Contains(g.Title) && RetroBat.HasUrl(g.Title)).Select(g => g.AppName), StringComparer.OrdinalIgnoreCase);
                if (keepChoices) { wantSteam = oldWantSteam; wantRetro = oldWantRetro; }
                UpdateSteamStatus();
                if (needRepair.Count > 0 && !steamWarning.Visible)
                {
                    steamWarning.Text = needRepair.Count + (needRepair.Count == 1 ? " Steam shortcut points" : " Steam shortcuts point") + " to an old app folder. Press Apply to Steam to repair.";
                    steamWarning.Visible = true;
                }
                ShowLibrary();
                RenderList();
            });
        }

        void RenderList()
        {
            foreach (var kv in filterButtons)
            {
                bool sel = kv.Key == filter;
                kv.Value.BackColor = sel ? Theme.Text : Theme.Button;
                kv.Value.ForeColor = sel ? Theme.Ground : Theme.Text;
            }

            IEnumerable<EpicGame> shown = games;
            if (filter == "installed") shown = games.Where(g => g.Installed || g.HasInstall);
            else if (filter == "steam") shown = games.Where(g => wantSteam.Contains(g.AppName) || inSteam.Contains(g.Title));

            list.SuspendLayout();
            list.Controls.Clear();
            var arr = shown.ToList();
            foreach (var g in arr) list.Controls.Add(BuildRow(g));
            if (arr.Count == 0)
            {
                emptyLabel.Text = games.Count == 0
                    ? "No games found in this Epic account. Free games you claim on the Epic Store show up here too."
                    : filter == "installed"
                        ? "None of your Epic games are installed on this PC. Install one with the Epic Games Launcher or Heroic, then press Rescan. Already have it in another folder? Open \"All\" and use \"Locate folder…\"."
                        : "Nothing here yet.";
                list.Controls.Add(emptyLabel);
            }
            list.ResumeLayout();

            int n = wantSteam.Count;
            bool changes = HasChanges();
            summary.Text = n + (n == 1 ? " game" : " games") + " set up for Steam";
            if (!busy)
                busyLabel.Text = Steam.UserConfigDir == null ? "Steam isn't ready (see the warning above)."
                    : changes ? "Steam closes for a few seconds while shortcuts are written, then reopens."
                    : n > 0 ? "Everything is already set up in Steam. Nothing to apply."
                    : "Click \"Add to Steam\" on a game, then Apply.";
            SetApplyEnabled(!busy && Steam.UserConfigDir != null && changes);
        }

        // A flat WinForms button keeps its colours when disabled, so grey it out by hand.
        void SetApplyEnabled(bool on)
        {
            applyButton.Enabled = on;
            applyButton.BackColor = on ? Theme.Accent : Theme.Button;
            applyButton.ForeColor = on ? Theme.Ground : Theme.Muted;
        }

        bool HasChanges()
        {
            foreach (var g in games)
            {
                bool want = wantSteam.Contains(g.AppName), has = inSteam.Contains(g.Title);
                if (want != has) return true;
                if (want && needRepair.Contains(g.Title)) return true;
                if (want && !g.Installed && g.HasInstall) return true; // in Steam, but our Legendary doesn't know the install yet: import it
                if (want && wantRetro.Contains(g.AppName) != RetroBat.HasUrl(g.Title)) return true;
            }
            return false;
        }

        void UpdateEpicStatus()
        {
            epicStatus.Text = account == null ? "● Epic: not signed in" : "● Epic: " + account;
            epicStatus.ForeColor = account == null ? Theme.Muted : Theme.Accent;
            signOut.Visible = account != null;
        }

        void UpdateSteamStatus()
        {
            string warn = null;
            if (Steam.InstallPath == null) { steamStatus.Text = "● Steam: not found"; steamStatus.ForeColor = Theme.Bad; warn = "Steam isn't installed. Install Steam from store.steampowered.com, sign in once, then press Rescan."; }
            else if (Steam.UserConfigDir == null) { steamStatus.Text = "● Steam: never signed in"; steamStatus.ForeColor = Theme.Warn; warn = "Open Steam and sign in once, so it creates your user folder. Then press Rescan."; }
            else { steamStatus.Text = Steam.IsRunning ? "● Steam is running" : "● Steam is closed"; steamStatus.ForeColor = Theme.Text; }
            steamWarning.Text = warn ?? "";
            steamWarning.Visible = warn != null;
        }

        bool EnsureRetroBatFolder()
        {
            if (!string.IsNullOrEmpty(RetroBat.RomsDir) && Directory.Exists(RetroBat.RomsDir)) return true;
            var found = RetroBat.Detect();
            if (found != null &&
                MessageBox.Show(this, "Use this RetroBat folder?\n\n" + found, AppPaths.AppName, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                RetroBat.RomsDir = found;
                return true;
            }
            using (var dlg = new FolderBrowserDialog { Description = "Pick your RetroBat folder (or its roms\\windows folder)." })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return false;
                RetroBat.RomsDir = RetroBat.Normalize(dlg.SelectedPath);
                return true;
            }
        }

        void Locate(EpicGame g)
        {
            using (var dlg = new FolderBrowserDialog { Description = "Pick the folder where " + g.Title + " is installed." })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string path = dlg.SelectedPath;
                CommandResult r = null;
                RunBusy("Checking " + g.Title + "…", () => { r = Legendary.Import(g.AppName, path); }, () =>
                {
                    if (!r.Ok) MessageBox.Show(this, "That folder doesn't look like " + g.Title + ".\n\n" + LastLine(r.Error), AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    Refresh(true);
                });
            }
        }

        void Apply()
        {
            if (Demo) { MessageBox.Show(this, "Demo mode: nothing is changed.", AppPaths.AppName); return; }
            var cfg = Steam.UserConfigDir;
            if (cfg == null) { UpdateSteamStatus(); return; }

            bool wasRunning = Steam.IsRunning;
            if (wasRunning && MessageBox.Show(this, "Steam needs to close for a few seconds to save the shortcuts. It reopens by itself.\n\nClose any Steam game first. Continue?", AppPaths.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;

            var toAdd = games.Where(g => wantSteam.Contains(g.AppName)).ToList();
            var toRemove = games.Where(g => !wantSteam.Contains(g.AppName) && inSteam.Contains(g.Title)).ToList();
            var problems = new List<string>();

            // RetroBat: never overwrite a .url this app didn't write without asking.
            var retroChoice = new Dictionary<string, RetroBat.Conflict>(StringComparer.OrdinalIgnoreCase);
            foreach (var g in toAdd.Where(x => wantRetro.Contains(x.AppName)))
            {
                if (RetroBat.HasUrl(g.Title) || !RetroBat.HasForeignFile(g.Title)) continue;
                int a = Ask.Show(this, "RetroBat already has \"" + g.Title + "\"",
                    "There is already a shortcut called \"" + Path.GetFileName(RetroBat.UrlPath(g.Title)) + "\" in\n" + RetroBat.RomsDir +
                    "\n\nIt wasn't made by this app (you or another tool made it). What should happen to it?",
                    "Replace it (keep a backup)", "Keep both", "Leave RetroBat as it is");
                if (a == 0) retroChoice[g.AppName] = RetroBat.Conflict.Replace;
                else if (a == 1) retroChoice[g.AppName] = RetroBat.Conflict.KeepBoth;
                else wantRetro.Remove(g.AppName);
            }

            var report = new List<string>();
            RunBusy("Importing games…", () =>
            {
                foreach (var g in toAdd.Where(x => !x.Installed && x.HasInstall))
                {
                    SetBusyText("Importing " + g.Title + "…");
                    var r = Legendary.Import(g.AppName, g.InstallPath);
                    if (!r.Ok) problems.Add(g.Title + ": " + LastLine(r.Error));
                }
                var fresh = Legendary.Games().ToDictionary(x => x.AppName, StringComparer.OrdinalIgnoreCase);
                toAdd = toAdd.Select(x => fresh.ContainsKey(x.AppName) ? fresh[x.AppName] : x).Where(x => x.Installed).ToList();

                SetBusyText("Closing Steam…");
                if (!Steam.Shutdown()) throw new Exception("Steam didn't close. Close it from the tray icon and press Apply again.");

                SetBusyText("Writing Steam shortcuts…");
                var root = Steam.LoadShortcuts(cfg);
                var added = new List<uint>();
                var removed = new List<uint>();
                foreach (var g in toAdd)
                {
                    var sc = Launcher.ShortcutFor(g);
                    added.Add(Steam.UpsertShortcut(root, sc));
                    RetroBat.Conflict choice;
                    if (!retroChoice.TryGetValue(g.AppName, out choice)) choice = RetroBat.Conflict.None;
                    bool isNew = !inSteam.Contains(g.Title);
                    string line = "• " + g.Title + ": " + (isNew ? "added to Steam with Steam Input on" : "Steam shortcut updated");
                    if (wantRetro.Contains(g.AppName) && !string.IsNullOrEmpty(RetroBat.RomsDir))
                    {
                        bool had = RetroBat.HasUrl(g.Title);
                        RetroBat.WriteUrl(g.Title, sc, choice);
                        var where = RetroBat.OurPath(g.Title);
                        if (RetroBat.IsOurFile(where))
                            line += had ? "; RetroBat shortcut updated" : "; RetroBat shortcut " + (choice == RetroBat.Conflict.Replace ? "replaced yours (backup kept)" : "added") + ": " + Path.GetFileName(where);
                    }
                    else if (RetroBat.HasUrl(g.Title)) { RetroBat.DeleteUrl(g.Title); line += "; RetroBat shortcut removed"; }
                    report.Add(line);
                }
                foreach (var g in toRemove)
                {
                    if (Steam.RemoveShortcut(root, g.Title)) removed.Add(Steam.ShortcutAppId("\"" + Launcher.PowerShellExe + "\"", g.Title));
                    RetroBat.DeleteUrl(g.Title);
                    report.Add("• " + g.Title + ": removed from Steam and RetroBat");
                }
                Steam.SaveShortcuts(cfg, root);
                if (added.Count > 0) Steam.SetSteamInput(cfg, added, true);
                if (removed.Count > 0) Steam.RemoveSteamInput(cfg, removed);

                SetBusyText("Starting Steam…");
                Steam.Start();
            }, () =>
            {
                if (problems.Count > 0) { report.Add(""); report.Add("Couldn't add:"); report.AddRange(problems.Select(x => "• " + x)); }
                report.Add("");
                report.Add("Steam was closed and reopened. The app's files are in " + AppPaths.Root
                    + (Settings.Get("startMenu") == "1" ? " and it's in the Start menu." : ".")
                    + " Backups of the Steam files are in its backups folder.");
                if (problems.Count > 0)
                {
                    // Something failed: keep the window open so it can be fixed and applied again.
                    Ask.Show(this, "Done, with problems", string.Join("\n", report), "OK");
                    Refresh(false);
                    return;
                }
                report.Add("");
                report.Add("You can play from Steam now. This window closes; open Couch Gaming Ninja again only to add or remove games.");
                Ask.Show(this, "All set", string.Join("\n", report), "OK, close");
                Close();
            });
        }

        void RemoveEverything()
        {
            if (Demo) { MessageBox.Show(this, "Demo mode: nothing is changed.", AppPaths.AppName); return; }
            if (MessageBox.Show(this,
                "This removes every Steam shortcut and RetroBat shortcut this app made, signs out of Epic and deletes the app's data folder.\n\nYour games are not touched. Continue?",
                AppPaths.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) != DialogResult.OK) return;

            // Show what is happening instead of the stale game list.
            ShowLibrary();
            list.Controls.Clear();
            emptyLabel.Text = "Removing this app's shortcuts from Steam and RetroBat, putting back any RetroBat files it replaced, signing out of Epic and deleting " + AppPaths.Root + "…";
            list.Controls.Add(emptyLabel);
            summary.Text = "Removing everything…";
            bool runningInside = AppPaths.RunningDir.StartsWith(AppPaths.Root, StringComparison.OrdinalIgnoreCase);

            RunBusy("Removing…", () =>
            {
                var cfg = Steam.UserConfigDir;
                if (cfg != null)
                {
                    var root = Steam.LoadShortcuts(cfg);
                    var names = Steam.OurShortcutNames(root).ToList();
                    if (names.Count > 0)
                    {
                        if (!Steam.Shutdown()) throw new Exception("Steam didn't close. Close it and try again.");
                        foreach (var n in names) { Steam.RemoveShortcut(root, n); RetroBat.DeleteUrl(n); }
                        Steam.SaveShortcuts(cfg, root);
                        Steam.RemoveSteamInput(cfg, names.Select(n => Steam.ShortcutAppId("\"" + Launcher.PowerShellExe + "\"", n)));
                        Steam.Start();
                    }
                }
                foreach (var t in RetroBat.TrackedTitles()) RetroBat.DeleteUrl(t);
                if (Legendary.IsDownloaded) Legendary.Logout();
                Installer.DeleteStartMenuShortcut();
                AppPaths.ForgetRoot();
                if (!runningInside) { try { Directory.Delete(AppPaths.Root, true); } catch { } }
            }, () =>
            {
                Ask.Show(this, "Everything removed", string.Join("\n", new[]
                {
                    "• Took this app's shortcuts out of Steam and RetroBat (Steam was closed and reopened if needed).",
                    "• Put back any RetroBat shortcut it had replaced.",
                    "• Signed out of Epic and removed the Start menu entry.",
                    runningInside ? "• The app's folder (" + AppPaths.Root + ") is deleted as soon as this window closes." : "• Deleted the app's folder (" + AppPaths.Root + ").",
                    "",
                    "Your games and your own Steam/RetroBat shortcuts were not touched." + (runningInside ? "" : " You can delete this .exe now.")
                }), "OK, close");
                if (runningInside) Installer.DeleteFolderLater(AppPaths.Root);
                Close();
            });
        }

        void MoveFolder()
        {
            if (Demo) { MessageBox.Show(this, "Demo mode: nothing is changed.", AppPaths.AppName); return; }
            string oldRoot = AppPaths.Root;
            string newRoot;
            bool startMenu;
            using (var d = new InstallDialog(true))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                newRoot = d.ChosenPath;
                startMenu = d.StartMenu;
            }
            if (string.Equals(oldRoot.TrimEnd('\\'), newRoot.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
            if (newRoot.StartsWith(oldRoot.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(this, "The new folder can't be inside the current one.", AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var cfg = Steam.UserConfigDir;
            bool hasShortcuts = cfg != null && Steam.OurShortcutNames(Steam.LoadShortcuts(cfg)).Count > 0;
            if (hasShortcuts && Steam.IsRunning && MessageBox.Show(this, "Steam needs to close for a few seconds so the shortcuts can point to the new folder. It reopens by itself.\n\nClose any Steam game first. Continue?", AppPaths.AppName, MessageBoxButtons.OKCancel, MessageBoxIcon.Information) != DialogResult.OK) return;

            int rewritten = 0;
            bool menuOk = false;
            RunBusy("Copying the app's folder…", () =>
            {
                Installer.CopyFolder(oldRoot, newRoot);
                if (hasShortcuts)
                {
                    SetBusyText("Closing Steam…");
                    if (!Steam.Shutdown()) throw new Exception("Steam didn't close. Close it from the tray icon and try again. Nothing was moved.");
                    SetBusyText("Pointing Steam shortcuts to the new folder…");
                    var root = Steam.LoadShortcuts(cfg);
                    rewritten = Steam.ReplaceInOurLaunchOptions(root, oldRoot.Replace("'", "''"), newRoot.Replace("'", "''"));
                    Steam.SaveShortcuts(cfg, root);
                    Steam.Start();
                }
                AppPaths.SetRoot(newRoot);
                Settings.Set("startMenu", startMenu ? "1" : "0");
                if (startMenu) { Installer.CreateStartMenuShortcut(AppPaths.InstalledExe); menuOk = File.Exists(Installer.StartMenuShortcut); }
                else Installer.DeleteStartMenuShortcut();
            }, () =>
            {
                var lines = new List<string>
                {
                    "• Copied everything to " + newRoot,
                    hasShortcuts ? "• Pointed " + rewritten + (rewritten == 1 ? " Steam shortcut" : " Steam shortcuts") + " to the new folder (Steam was closed and reopened)." : "• No Steam shortcuts to update.",
                    "• RetroBat shortcuts keep working: their Steam ids didn't change.",
                    startMenu ? (menuOk ? "• Start menu entry now opens the new folder." : "• Couldn't update the Start menu entry.") : "• No Start menu entry.",
                    "• The old folder (" + oldRoot + ") is deleted when the app reopens from the new one."
                };
                Ask.Show(this, "Moved", string.Join("\n", lines), "OK, reopen");
                Installer.Relaunch(AppPaths.InstalledExe, "--cleanup \"" + oldRoot + "\"");
                Close();
            }, ex =>
            {
                MessageBox.Show(this, ex.Message, AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                try { if (!string.Equals(AppPaths.Root, newRoot, StringComparison.OrdinalIgnoreCase)) Directory.Delete(newRoot, true); } catch { }
            });
        }

        // ---------------- helpers ----------------

        void RunBusy(string text, Action work, Action done, Action<Exception> failed = null)
        {
            busy = true;
            SetBusyText(text);
            UseWaitCursor = true;
            SetApplyEnabled(false);
            foreach (var b in new[] { rescanButton, loginButton }) if (b != null) b.Enabled = false;
            var t = new Thread(() =>
            {
                Exception error = null;
                try { work(); } catch (Exception ex) { error = ex; }
                BeginInvoke((Action)(() =>
                {
                    busy = false;
                    UseWaitCursor = false;
                    rescanButton.Enabled = true;
                    loginButton.Enabled = true;
                    busyLabel.Text = error != null ? "Something went wrong (see the message)." : "Done.";
                    if (error != null)
                    {
                        if (failed != null) failed(error);
                        else MessageBox.Show(this, error.Message, AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);
                        RenderList();
                    }
                    else done();
                }));
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        void SetBusyText(string text)
        {
            if (InvokeRequired) { BeginInvoke((Action)(() => SetBusyText(text))); return; }
            busyLabel.Text = text;
            if (loginView.Visible && loginError != null) loginError.ForeColor = Theme.Muted;
            if (loginView.Visible) loginError.Text = text;
        }

        static string LastLine(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            var lines = s.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            return lines.Length == 0 ? "" : lines[lines.Length - 1].Trim();
        }

        static void Open(string url)
        {
            try { Process.Start(url); } catch { }
        }
    }

    /// <summary>
    /// A scrolling list that doesn't jump to the clicked control. WinForms scrolls a focused control into view on mouse down;
    /// the few pixels it moves can make the mouse-up miss the button, so the click is lost.
    /// </summary>
    public class NoJumpFlowPanel : FlowLayoutPanel
    {
        protected override Point ScrollToControl(Control activeControl) { return DisplayRectangle.Location; }
    }
}
