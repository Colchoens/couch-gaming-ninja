using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace CouchGamingNinja
{
    public class SteamShortcut
    {
        public string Name;
        public string Exe;            // unquoted path
        public string StartDir;       // unquoted path
        public string LaunchOptions;
        public string Icon;

        public string QuotedExe { get { return "\"" + Exe + "\""; } }

        /// <summary>Steam's id for a non-Steam shortcut: CRC32 of the quoted exe + name, with the top bit set.</summary>
        public uint AppId { get { return Steam.ShortcutAppId(QuotedExe, Name); } }

        /// <summary>The id used in steam://rungameid/ links.</summary>
        public ulong GameId { get { return ((ulong)AppId << 32) | 0x02000000UL; } }
    }

    public static class Steam
    {
        public static string InstallPath
        {
            get
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    var p = k == null ? null : k.GetValue("SteamPath") as string;
                    if (!string.IsNullOrEmpty(p)) return p.Replace('/', '\\');
                }
                string def = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
                return Directory.Exists(def) ? def : null;
            }
        }

        public static string SteamExe { get { var p = InstallPath; return p == null ? null : Path.Combine(p, "steam.exe"); } }

        /// <summary>The Steam user folder to write to: the logged-in user, else the most recently used one.</summary>
        public static string UserConfigDir
        {
            get
            {
                string root = InstallPath;
                if (root == null) return null;
                string userdata = Path.Combine(root, "userdata");
                if (!Directory.Exists(userdata)) return null;

                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess"))
                {
                    object v = k == null ? null : k.GetValue("ActiveUser");
                    if (v is int && (int)v != 0)
                    {
                        string d = Path.Combine(userdata, ((uint)(int)v).ToString(), "config");
                        if (Directory.Exists(d)) return d;
                    }
                }

                var best = new DirectoryInfo(userdata).GetDirectories()
                    .Where(d => d.Name.All(char.IsDigit) && d.Name != "0")
                    .Select(d => new DirectoryInfo(Path.Combine(d.FullName, "config")))
                    .Where(d => d.Exists)
                    .OrderByDescending(d => File.Exists(Path.Combine(d.FullName, "localconfig.vdf")) ? File.GetLastWriteTimeUtc(Path.Combine(d.FullName, "localconfig.vdf")) : d.LastWriteTimeUtc)
                    .FirstOrDefault();
                return best == null ? null : best.FullName;
            }
        }

        public static bool IsRunning { get { return Process.GetProcessesByName("steam").Length > 0; } }

        /// <summary>Asks Steam to quit and waits for it. Steam rewrites its files on exit, so it must be closed while we edit them.</summary>
        public static bool Shutdown(int timeoutSeconds = 45)
        {
            if (!IsRunning) return true;
            var exe = SteamExe;
            if (exe != null && File.Exists(exe)) Process.Start(exe, "-shutdown");
            var until = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (DateTime.UtcNow < until)
            {
                if (!IsRunning) { Thread.Sleep(1500); return true; }
                Thread.Sleep(500);
            }
            return !IsRunning;
        }

        public static void Start()
        {
            var exe = SteamExe;
            if (exe != null && File.Exists(exe)) Process.Start(exe);
        }

        public static uint ShortcutAppId(string quotedExe, string name)
        {
            return Crc32.Compute(Encoding.UTF8.GetBytes(quotedExe + name)) | 0x80000000u;
        }

        static void Backup(string file)
        {
            if (!File.Exists(file)) return;
            AppPaths.Ensure();
            string name = Path.GetFileNameWithoutExtension(file) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + Path.GetExtension(file);
            File.Copy(file, Path.Combine(AppPaths.Backups, name), true);
        }

        // ---- shortcuts.vdf ----

        public static VdfNode LoadShortcuts(string configDir)
        {
            string file = Path.Combine(configDir, "shortcuts.vdf");
            if (!File.Exists(file))
            {
                var root = VdfNode.NewMap();
                root.Set("shortcuts", VdfNode.NewMap());
                return root;
            }
            return BinaryVdf.Read(File.ReadAllBytes(file));
        }

        public static void SaveShortcuts(string configDir, VdfNode root)
        {
            string file = Path.Combine(configDir, "shortcuts.vdf");
            Backup(file);
            File.WriteAllBytes(file, BinaryVdf.Write(root));
        }

        /// <summary>Adds or replaces our shortcut (matched by name + our tag). Returns the appid.</summary>
        public static uint UpsertShortcut(VdfNode root, SteamShortcut s)
        {
            var list = root.GetOrAddMap("shortcuts");
            uint appid = s.AppId;

            VdfNode entry = FindShortcut(list, s.Name);
            if (entry == null)
            {
                entry = VdfNode.NewMap();
                list.Children.Add(new KeyValuePair<string, VdfNode>(list.Children.Count.ToString(), entry));
            }

            entry.Set("appid", VdfNode.NewInt(unchecked((int)appid)));
            entry.Set("AppName", VdfNode.NewString(s.Name));
            entry.Set("Exe", VdfNode.NewString(s.QuotedExe));
            entry.Set("StartDir", VdfNode.NewString("\"" + s.StartDir + "\""));
            entry.Set("icon", VdfNode.NewString(s.Icon ?? ""));
            if (entry.Get("ShortcutPath") == null) entry.Set("ShortcutPath", VdfNode.NewString(""));
            entry.Set("LaunchOptions", VdfNode.NewString(s.LaunchOptions ?? ""));
            if (entry.Get("IsHidden") == null) entry.Set("IsHidden", VdfNode.NewInt(0));
            entry.Set("AllowDesktopConfig", VdfNode.NewInt(1));
            entry.Set("AllowOverlay", VdfNode.NewInt(1));
            if (entry.Get("OpenVR") == null) entry.Set("OpenVR", VdfNode.NewInt(0));
            if (entry.Get("Devkit") == null) entry.Set("Devkit", VdfNode.NewInt(0));
            if (entry.Get("DevkitGameID") == null) entry.Set("DevkitGameID", VdfNode.NewString(""));
            if (entry.Get("DevkitOverrideAppID") == null) entry.Set("DevkitOverrideAppID", VdfNode.NewInt(0));
            if (entry.Get("LastPlayTime") == null) entry.Set("LastPlayTime", VdfNode.NewInt(0));
            if (entry.Get("FlatpakAppID") == null) entry.Set("FlatpakAppID", VdfNode.NewString(""));
            var tags = entry.GetOrAddMap("tags");
            if (!tags.Children.Any(t => t.Value.Str == AppPaths.ShortcutTag))
                tags.Children.Add(new KeyValuePair<string, VdfNode>(tags.Children.Count.ToString(), VdfNode.NewString(AppPaths.ShortcutTag)));
            return appid;
        }

        public static bool RemoveShortcut(VdfNode root, string name)
        {
            var list = root.GetOrAddMap("shortcuts");
            var e = FindShortcut(list, name);
            if (e == null) return false;
            list.Children.RemoveAll(kv => kv.Value == e);
            Renumber(list);
            return true;
        }

        /// <summary>Names of shortcuts this app created.</summary>
        public static HashSet<string> OurShortcutNames(VdfNode root)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = root.Get("shortcuts");
            if (list == null) return names;
            foreach (var kv in list.Children)
                if (IsOurs(kv.Value)) names.Add(kv.Value.GetString("AppName") ?? kv.Value.GetString("appname") ?? "");
            return names;
        }

        /// <summary>Our shortcuts whose launch options don't point at the app's current folder (folder moved or recreated).</summary>
        public static HashSet<string> OurShortcutsNeedingRepair(VdfNode root, string expectedText)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var list = root.Get("shortcuts");
            if (list == null) return names;
            foreach (var kv in list.Children)
            {
                if (!IsOurs(kv.Value)) continue;
                string lo = kv.Value.GetString("LaunchOptions") ?? "";
                if (lo.IndexOf(expectedText, StringComparison.OrdinalIgnoreCase) < 0)
                    names.Add(kv.Value.GetString("AppName") ?? kv.Value.GetString("appname") ?? "");
            }
            return names;
        }

        /// <summary>Rewrites a path inside our shortcuts' launch options (used when the app's folder moves). The appid doesn't change.</summary>
        public static int ReplaceInOurLaunchOptions(VdfNode root, string oldText, string newText)
        {
            int n = 0;
            var list = root.Get("shortcuts");
            if (list == null) return 0;
            foreach (var kv in list.Children)
            {
                if (!IsOurs(kv.Value)) continue;
                string lo = kv.Value.GetString("LaunchOptions") ?? "";
                int i = lo.IndexOf(oldText, StringComparison.OrdinalIgnoreCase);
                if (i < 0) continue;
                var sb = new StringBuilder();
                int start = 0;
                while (i >= 0)
                {
                    sb.Append(lo, start, i - start).Append(newText);
                    start = i + oldText.Length;
                    i = lo.IndexOf(oldText, start, StringComparison.OrdinalIgnoreCase);
                }
                sb.Append(lo.Substring(start));
                kv.Value.Set("LaunchOptions", VdfNode.NewString(sb.ToString()));
                n++;
            }
            return n;
        }

        static bool IsOurs(VdfNode e)
        {
            var tags = e.Get("tags");
            return tags != null && tags.Children.Any(t => t.Value.Str == AppPaths.ShortcutTag);
        }

        static VdfNode FindShortcut(VdfNode list, string name)
        {
            foreach (var kv in list.Children)
            {
                var n = kv.Value.GetString("AppName") ?? kv.Value.GetString("appname");
                if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase) && IsOurs(kv.Value)) return kv.Value;
            }
            return null;
        }

        static void Renumber(VdfNode list)
        {
            for (int i = 0; i < list.Children.Count; i++)
                list.Children[i] = new KeyValuePair<string, VdfNode>(i.ToString(), list.Children[i].Value);
        }

        // ---- localconfig.vdf: per-game Steam Input ----

        /// <summary>Sets "Steam Input: Enabled" for a non-Steam shortcut. Non-Steam games use the unsigned 32-bit appid here.</summary>
        public static void SetSteamInput(string configDir, IEnumerable<uint> appIds, bool enabled)
        {
            string file = Path.Combine(configDir, "localconfig.vdf");
            if (!File.Exists(file)) return;
            Backup(file);
            var root = TextVdf.Parse(File.ReadAllText(file, Encoding.UTF8));
            var store = root.GetOrAddMap("UserLocalConfigStore");
            var apps = store.Get("Apps") ?? store.Get("apps");
            if (apps == null) { apps = VdfNode.NewMap(); store.Set("Apps", apps); }
            foreach (var id in appIds)
            {
                var app = apps.GetOrAddMap(id.ToString());
                app.Set("UseSteamControllerConfig", VdfNode.NewString(enabled ? "2" : "0"));
            }
            File.WriteAllText(file, TextVdf.Write(root), new UTF8Encoding(false));
        }

        public static void RemoveSteamInput(string configDir, IEnumerable<uint> appIds)
        {
            string file = Path.Combine(configDir, "localconfig.vdf");
            if (!File.Exists(file)) return;
            var root = TextVdf.Parse(File.ReadAllText(file, Encoding.UTF8));
            var store = root.Get("UserLocalConfigStore");
            var apps = store == null ? null : (store.Get("Apps") ?? store.Get("apps"));
            if (apps == null) return;
            Backup(file);
            var ids = new HashSet<string>(appIds.Select(i => i.ToString()));
            apps.Children.RemoveAll(kv => ids.Contains(kv.Key));
            File.WriteAllText(file, TextVdf.Write(root), new UTF8Encoding(false));
        }
    }

    public static class Crc32
    {
        static readonly uint[] Table = MakeTable();

        static uint[] MakeTable()
        {
            var t = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[i] = c;
            }
            return t;
        }

        public static uint Compute(byte[] data)
        {
            uint c = 0xFFFFFFFFu;
            foreach (var b in data) c = Table[(c ^ b) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }
    }
}
