using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Couch Gaming Ninja")]
[assembly: System.Reflection.AssemblyProduct("Couch Gaming Ninja")]
[assembly: System.Reflection.AssemblyDescription("Play your Epic games from Steam with any controller.")]
[assembly: System.Reflection.AssemblyVersion("0.1.1.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.1.1.0")]

namespace CouchGamingNinja
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            MainForm.Demo = Array.IndexOf(args, "--demo") >= 0;
            int ci = Array.IndexOf(args, "--cleanup");
            string cleanup = ci >= 0 && ci + 1 < args.Length ? args[ci + 1] : null;

            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (s, e) => MessageBox.Show(e.Exception.Message, AppPaths.AppName, MessageBoxButtons.OK, MessageBoxIcon.Error);

            if (!MainForm.Demo)
            {
                // First run: ask where the app's folder goes.
                if (!AppPaths.IsConfigured)
                {
                    using (var d = new InstallDialog(false))
                    {
                        if (d.ShowDialog() != DialogResult.OK) return;
                        // No message here: straight to the sign-in. The one summary comes after the shortcuts are created.
                        Installer.Install(d.ChosenPath, d.StartMenu);
                    }
                }

                // Started from somewhere else (Downloads, a .zip): update the installed copy and run that one instead.
                if (!SameDir(AppPaths.RunningDir, AppPaths.Root))
                {
                    try
                    {
                        if (!File.Exists(AppPaths.InstalledExe) || IsNewerBuild(AppPaths.RunningExe, AppPaths.InstalledExe))
                            File.Copy(AppPaths.RunningExe, AppPaths.InstalledExe, true);
                        Installer.Relaunch(AppPaths.InstalledExe, "");
                        return;
                    }
                    catch { /* the installed copy is open or locked: just run from here */ }
                }

                if (cleanup != null) new Thread(() => Installer.CleanupOldFolder(cleanup)) { IsBackground = true }.Start();
            }

            Application.Run(new MainForm());
        }

        static bool SameDir(string a, string b)
        {
            try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
            catch { return false; }
        }

        /// <summary>
        /// True when 'a' should replace the installed 'b': a higher version, or the same version but a different build
        /// (test builds keep the version number). An older version never replaces a newer one.
        /// </summary>
        static bool IsNewerBuild(string a, string b)
        {
            try
            {
                var va = new Version(FileVersionInfo.GetVersionInfo(a).FileVersion);
                var vb = new Version(FileVersionInfo.GetVersionInfo(b).FileVersion);
                if (va != vb) return va > vb;
                return !SameBytes(a, b);
            }
            catch { return false; }
        }

        static bool SameBytes(string a, string b)
        {
            var x = File.ReadAllBytes(a);
            var y = File.ReadAllBytes(b);
            if (x.Length != y.Length) return false;
            for (int i = 0; i < x.Length; i++) if (x[i] != y[i]) return false;
            return true;
        }
    }
}
