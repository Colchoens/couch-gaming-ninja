using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace CouchGamingNinja
{
    /// <summary>Puts the app and all its files in one folder the user picks, and moves that folder later.</summary>
    public static class Installer
    {
        public static string StartMenuShortcut
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppPaths.AppName + ".lnk"); }
        }

        /// <summary>Running from a temporary folder (for example straight from inside a .zip)?</summary>
        public static bool RunningFromTemp
        {
            get { return IsUnder(AppPaths.RunningDir, Path.GetTempPath()); }
        }

        public static string PortableSuggestion
        {
            get { return Path.Combine(AppPaths.RunningDir, "CouchGamingNinja"); }
        }

        static bool IsUnder(string path, string parent)
        {
            if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(parent)) return false;
            string p = Path.GetFullPath(path).TrimEnd('\\') + "\\";
            string q = Path.GetFullPath(parent).TrimEnd('\\') + "\\";
            return p.StartsWith(q, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Null when the folder can be used, otherwise the reason it can't.</summary>
        public static string Validate(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "Pick a folder.";
            try { path = Path.GetFullPath(path); } catch { return "That isn't a valid folder path."; }
            foreach (var sf in new[] { Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.ProgramFilesX86 })
            {
                string pf = Environment.GetFolderPath(sf);
                if (IsUnder(path, pf))
                    return "Program Files can't be used: Windows only lets administrators write there, and Steam starts games without administrator rights, so your Epic sign-in couldn't be saved. Pick another folder, for example C:\\CouchGamingNinja.";
            }
            if (IsUnder(path, Environment.GetFolderPath(Environment.SpecialFolder.Windows))) return "The Windows folder can't be used. Pick another folder.";
            if (IsUnder(path, Path.GetTempPath())) return "That is a temporary folder and Windows may clean it. Pick another folder.";
            try
            {
                Directory.CreateDirectory(path);
                string test = Path.Combine(path, ".write-test");
                File.WriteAllText(test, "ok");
                File.Delete(test);
            }
            catch (Exception ex) { return "Can't write to that folder: " + ex.Message; }
            return null;
        }

        /// <summary>Copies this .exe into the folder, makes it the app's folder and optionally adds a Start menu shortcut.
        /// Returns a short summary of what was done, to show the user.</summary>
        public static string Install(string path, bool startMenu)
        {
            path = Path.GetFullPath(path).TrimEnd('\\');
            Directory.CreateDirectory(path);
            string exe = Path.Combine(path, AppPaths.ExeName);
            bool copied = false;
            if (!string.Equals(Path.GetFullPath(AppPaths.RunningExe), exe, StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(AppPaths.RunningExe, exe, true);
                copied = true;
            }
            AppPaths.SetRoot(path);
            AppPaths.Ensure();
            Settings.Set("installedVersion", AppPaths.Version);
            Settings.Set("startMenu", startMenu ? "1" : "0");
            bool menuOk = false;
            if (startMenu) { CreateStartMenuShortcut(exe); menuOk = File.Exists(StartMenuShortcut); } else DeleteStartMenuShortcut();

            var lines = new System.Collections.Generic.List<string>();
            lines.Add("• Created the app's folder: " + path);
            lines.Add(copied ? "• Copied the app there: " + exe : "• The app already runs from there.");
            lines.Add("• Saved where the folder is, so the app finds it next time.");
            if (startMenu) lines.Add(menuOk ? "• Added \"" + AppPaths.AppName + "\" to the Start menu." : "• Couldn't add the Start menu entry. Open the app from " + exe + ".");
            else lines.Add("• No Start menu entry (you can open the app from " + exe + ").");
            lines.Add("");
            lines.Add("Nothing in Steam or RetroBat was changed yet. Next: sign in with Epic and pick your games.");
            if (copied) lines.Add("The .exe you downloaded isn't needed anymore; you can delete it.");
            return string.Join("\n", lines);
        }

        public static void CreateStartMenuShortcut(string exe)
        {
            string ps = "$s=(New-Object -ComObject WScript.Shell).CreateShortcut(" + Launcher.PsQuote(StartMenuShortcut) + ");"
                      + "$s.TargetPath=" + Launcher.PsQuote(exe) + ";$s.WorkingDirectory=" + Launcher.PsQuote(Path.GetDirectoryName(exe)) + ";"
                      + "$s.Description=" + Launcher.PsQuote("Play your Epic games from Steam with any controller") + ";$s.Save()";
            RunHidden(Launcher.PowerShellExe, "-NoProfile -ExecutionPolicy Bypass -Command \"" + ps + "\"", true);
        }

        public static void DeleteStartMenuShortcut()
        {
            try { if (File.Exists(StartMenuShortcut)) File.Delete(StartMenuShortcut); } catch { }
        }

        /// <summary>Copies everything from the old folder to the new one. The old folder is deleted by the new copy of the app after this one exits.</summary>
        public static void CopyFolder(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var dir in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
                Directory.CreateDirectory(Path.Combine(to, dir.Substring(from.Length).TrimStart('\\')));
            foreach (var file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
                File.Copy(file, Path.Combine(to, file.Substring(from.Length).TrimStart('\\')), true);
        }

        /// <summary>Deletes a folder once the process that ran from it has exited.</summary>
        public static void DeleteFolderLater(string folder)
        {
            if (string.IsNullOrEmpty(folder)) return;
            RunHidden(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                "/c ping -n 4 127.0.0.1 >nul & rmdir /s /q \"" + folder + "\"", false);
        }

        /// <summary>Waits for the previous copy of the app to exit, then deletes its old folder.</summary>
        public static void CleanupOldFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder)) return;
            if (string.Equals(Path.GetFullPath(folder).TrimEnd('\\'), Path.GetFullPath(AppPaths.Root).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)) return;
            for (int i = 0; i < 20; i++)
            {
                try { Directory.Delete(folder, true); return; } catch { Thread.Sleep(500); }
            }
        }

        public static void Relaunch(string exe, string args)
        {
            Process.Start(new ProcessStartInfo(exe, args ?? "") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe) });
        }

        static void RunHidden(string file, string args, bool wait)
        {
            try
            {
                var p = Process.Start(new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
                if (wait && p != null) p.WaitForExit(15000);
            }
            catch { }
        }
    }

    /// <summary>"Where should Couch Gaming Ninja live?" Used on first run and for "Move folder…".</summary>
    public class InstallDialog : Form
    {
        public string ChosenPath { get; private set; }
        public bool StartMenu { get { return startMenu.Checked; } }

        readonly RadioButton optDefault, optPortable, optCustom;
        readonly Label customPath, error;
        readonly CheckBox startMenu;

        public InstallDialog(bool moving)
        {
            Text = AppPaths.AppName;
            BackColor = Theme.Ground;
            ForeColor = Theme.Text;
            Font = Theme.Ui(10);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            var stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Padding = new Padding(28, 24, 28, 20), BackColor = Theme.Ground };
            Controls.Add(stack);

            stack.Controls.Add(Theme.MakeLabel(moving ? "Move Couch Gaming Ninja's folder" : "Where should Couch Gaming Ninja live?", 16, Theme.Text, true));
            var sub = Theme.MakeLabel("Everything goes in one folder: the app, Legendary, your Epic sign-in, settings and backups. Your games stay where they are.", 10, Theme.Muted);
            sub.MaximumSize = new Size(600, 0);
            stack.Controls.Add(sub);
            stack.Controls.Add(Gap(10));

            optDefault = Option("Recommended (your user's app data):\n" + AppPaths.DefaultRoot);
            stack.Controls.Add(optDefault);

            bool portableOk = !Installer.RunningFromTemp;
            optPortable = Option("Portable, next to this app:\n" + Installer.PortableSuggestion);
            if (portableOk) stack.Controls.Add(optPortable);
            else
            {
                // A disabled radio button draws its text in a dark grey Windows picks, unreadable on this background,
                // so show the reason as a normal label instead.
                var why = Theme.MakeLabel("Portable (next to this app): not available, because the app is running from inside a .zip. Extract CouchGamingNinja.exe first if you want this option.", 9.5f, Theme.Muted);
                why.MaximumSize = new Size(600, 0);
                why.Margin = new Padding(18, 8, 0, 4);
                stack.Controls.Add(why);
            }

            optCustom = Option("Another folder…");
            stack.Controls.Add(optCustom);
            var customRow = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Ground, Margin = new Padding(26, 0, 0, 0) };
            var browse = Theme.MakeButton("Choose…", false);
            browse.Click += (s, e) => Browse();
            customPath = Theme.MakeLabel("", 10, Theme.Muted);
            customPath.Anchor = AnchorStyles.Left;
            customPath.Margin = new Padding(10, 10, 0, 0);
            customRow.Controls.Add(browse);
            customRow.Controls.Add(customPath);
            stack.Controls.Add(customRow);

            stack.Controls.Add(Gap(6));
            var note = Theme.MakeLabel("Program Files isn't offered: Windows only lets administrators write there, and Steam starts games without administrator rights.", 9, Theme.Muted);
            note.MaximumSize = new Size(600, 0);
            stack.Controls.Add(note);

            startMenu = new CheckBox { Text = "Add Couch Gaming Ninja to the Start menu", Checked = Settings.Get("startMenu") != "0", AutoSize = true, ForeColor = Theme.Text, Margin = new Padding(0, 12, 0, 0) };
            stack.Controls.Add(startMenu);

            error = Theme.MakeLabel("", 10, Theme.Bad);
            error.MaximumSize = new Size(600, 0);
            stack.Controls.Add(error);

            var buttons = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Ground, Margin = new Padding(0, 14, 0, 0) };
            var ok = Theme.MakeButton(moving ? "Move here" : "Install here", true);
            ok.Click += (s, e) => Confirm();
            var cancel = Theme.MakeButton("Cancel", false);
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            buttons.Controls.Add(ok);
            buttons.Controls.Add(cancel);
            stack.Controls.Add(buttons);
            AcceptButton = ok;
            CancelButton = cancel;

            string current = AppPaths.IsConfigured ? AppPaths.Root : null;
            if (moving && current != null)
            {
                // Not disabled (Windows would draw the text unreadable on this background); Confirm refuses it instead.
                if (Same(current, AppPaths.DefaultRoot)) optDefault.Text += "  (current)";
                if (Same(current, Installer.PortableSuggestion)) optPortable.Text += "  (current)";
                optCustom.Checked = true;
            }
            else optDefault.Checked = true;
        }

        static bool Same(string a, string b)
        {
            try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); } catch { return false; }
        }

        static Control Gap(int h) { return new Panel { Height = h, Width = 10, BackColor = Color.Transparent }; }

        RadioButton Option(string text)
        {
            return new RadioButton { Text = text, AutoSize = true, ForeColor = Theme.Text, Font = Theme.Ui(10.5f), Margin = new Padding(0, 6, 0, 2) };
        }

        void Browse()
        {
            using (var dlg = new FolderBrowserDialog { Description = "Pick where to put the CouchGamingNinja folder." })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                string p = dlg.SelectedPath;
                if (!Path.GetFileName(p.TrimEnd('\\')).Equals("CouchGamingNinja", StringComparison.OrdinalIgnoreCase))
                    p = Path.Combine(p, "CouchGamingNinja");
                customPath.Text = p;
                optCustom.Checked = true;
            }
        }

        void Confirm()
        {
            string p = optDefault.Checked ? AppPaths.DefaultRoot
                     : optPortable.Checked ? Installer.PortableSuggestion
                     : customPath.Text;
            if (optCustom.Checked && string.IsNullOrEmpty(p)) { error.Text = "Click \"Choose…\" to pick a folder."; return; }
            if (AppPaths.IsConfigured && Same(p, AppPaths.Root)) { error.Text = "That is already the app's folder. Pick another one."; return; }
            string problem = Installer.Validate(p);
            if (problem != null) { error.Text = problem; return; }
            ChosenPath = Path.GetFullPath(p).TrimEnd('\\');
            DialogResult = DialogResult.OK;
            Close();
        }
    }

    /// <summary>A message with custom buttons. Returns the index of the button clicked, or -1 if closed.</summary>
    public static class Ask
    {
        public static int Show(IWin32Window owner, string title, string message, params string[] buttons)
        {
            using (var f = new Form
            {
                Text = AppPaths.AppName,
                BackColor = Theme.Ground,
                ForeColor = Theme.Text,
                Font = Theme.Ui(10),
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                StartPosition = owner == null ? FormStartPosition.CenterScreen : FormStartPosition.CenterParent,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ShowInTaskbar = false
            })
            {
                int result = -1;
                var stack = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true, Padding = new Padding(24, 20, 24, 18), BackColor = Theme.Ground };
                f.Controls.Add(stack);
                stack.Controls.Add(Theme.MakeLabel(title, 13, Theme.Text, true));
                var msg = Theme.MakeLabel(message, 10, Theme.Muted);
                msg.MaximumSize = new Size(560, 0);
                msg.Margin = new Padding(0, 6, 0, 14);
                stack.Controls.Add(msg);
                var row = new FlowLayoutPanel { AutoSize = true, BackColor = Theme.Ground };
                for (int i = 0; i < buttons.Length; i++)
                {
                    int idx = i;
                    var b = Theme.MakeButton(buttons[i], i == 0);
                    b.Click += (s, e) => { result = idx; f.Close(); };
                    row.Controls.Add(b);
                }
                stack.Controls.Add(row);
                f.ShowDialog(owner);
                return result;
            }
        }
    }
}
