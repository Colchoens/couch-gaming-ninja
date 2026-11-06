using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace CouchGamingNinja
{
    /// <summary>
    /// Where the app keeps its own files. Everything lives in one folder (the app itself, Legendary, the Epic session,
    /// settings and backups), chosen by the user on first run, so the folder can be moved or removed as a whole.
    /// </summary>
    public static class AppPaths
    {
        public const string AppName = "Couch Gaming Ninja";
        public const string Version = "0.1.1";
        public const string ShortcutTag = "Couch Gaming Ninja";
        public const string ExeName = "CouchGamingNinja.exe";

        const string RegKey = @"Software\CouchGamingNinja";
        const string RegValue = "DataFolder";

        static string root;

        /// <summary>The suggested folder: %LOCALAPPDATA%\CouchGamingNinja.</summary>
        public static string DefaultRoot
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CouchGamingNinja"); }
        }

        public static string RunningExe { get { return Application.ExecutablePath; } }
        public static string RunningDir { get { return Path.GetDirectoryName(RunningExe); } }

        /// <summary>The app's folder. Falls back to the default before the user has chosen one.</summary>
        public static string Root
        {
            get { return root ?? (root = FindConfigured() ?? DefaultRoot); }
        }

        public static bool IsConfigured { get { return FindConfigured() != null; } }

        /// <summary>
        /// 1. The .exe runs from inside its own data folder (portable: copy the folder anywhere and it keeps working).
        /// 2. The folder saved in the registry.
        /// 3. A folder left by v0.1.0 at the default location.
        /// </summary>
        static string FindConfigured()
        {
            try { if (LooksLikeDataFolder(RunningDir)) return RunningDir; } catch { }
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(RegKey))
                {
                    var p = k == null ? null : k.GetValue(RegValue) as string;
                    if (!string.IsNullOrEmpty(p) && Directory.Exists(p)) return p;
                }
            }
            catch { }
            if (LooksLikeDataFolder(DefaultRoot)) return DefaultRoot;
            return null;
        }

        static bool LooksLikeDataFolder(string dir)
        {
            return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "settings.json"));
        }

        public static void SetRoot(string path)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(RegKey)) k.SetValue(RegValue, path);
            root = path;
            Settings.Reset();
        }

        public static void ForgetRoot()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(RegKey, false); } catch { }
        }

        public static string InstalledExe { get { return Path.Combine(Root, ExeName); } }
        public static string LegendaryExe { get { return Path.Combine(Root, "legendary.exe"); } }
        public static string LegendaryConfig { get { return Path.Combine(Root, "legendary-config"); } }
        public static string Backups { get { return Path.Combine(Root, "backups"); } }
        public static string RetroBatBackups { get { return Path.Combine(Backups, "retrobat"); } }
        public static string SettingsFile { get { return Path.Combine(Root, "settings.json"); } }

        public static void Ensure()
        {
            Directory.CreateDirectory(Root);
            Directory.CreateDirectory(LegendaryConfig);
            Directory.CreateDirectory(Backups);
        }
    }
}
