using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CouchGamingNinja
{
    /// <summary>
    /// Optional: puts a .url per game in RetroBat's roms\windows so the game shows up in RetroBat too.
    /// Files this app writes carry a [CouchGamingNinja] section. A file the app didn't write is never deleted:
    /// it is only replaced when the user says so, after a backup, and the backup is restored on removal.
    /// </summary>
    public static class RetroBat
    {
        public const string Marker = "[CouchGamingNinja]";
        const string FileKey = "retrobat:";       // + title -> file name we wrote in RomsDir
        const string BackupKey = "retrobatBackup:"; // + title -> file name of the user's original in RetroBatBackups

        public static string RomsDir
        {
            get { return Settings.Get("retrobatRomsWindows"); }
            set { Settings.Set("retrobatRomsWindows", value); }
        }

        public static string Detect()
        {
            var roots = new List<string>();
            foreach (var d in DriveInfo.GetDrives())
            {
                try { if (d.DriveType != DriveType.Fixed || !d.IsReady) continue; } catch { continue; }
                roots.Add(Path.Combine(d.RootDirectory.FullName, "RetroBat"));
                roots.Add(Path.Combine(d.RootDirectory.FullName, "Games", "RetroBat"));
            }
            foreach (var r in roots)
            {
                string w = Path.Combine(r, "roms", "windows");
                if (Directory.Exists(w)) return w;
            }
            return null;
        }

        /// <summary>Accepts the RetroBat folder itself, its roms folder or roms\windows.</summary>
        public static string Normalize(string picked)
        {
            if (string.IsNullOrEmpty(picked)) return null;
            if (Path.GetFileName(picked).Equals("windows", StringComparison.OrdinalIgnoreCase)) return picked;
            if (Path.GetFileName(picked).Equals("roms", StringComparison.OrdinalIgnoreCase)) return Path.Combine(picked, "windows");
            string w = Path.Combine(picked, "roms", "windows");
            return Directory.Exists(Path.Combine(picked, "roms")) ? w : picked;
        }

        public static string SafeName(string title)
        {
            return new string((title ?? "").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray()).Trim();
        }

        /// <summary>The usual file name for a game: "Title.url".</summary>
        public static string UrlPath(string title)
        {
            if (string.IsNullOrEmpty(RomsDir)) return null;
            return Path.Combine(RomsDir, SafeName(title) + ".url");
        }

        /// <summary>Name used when the user keeps their own file: "Title (Couch Gaming Ninja).url".</summary>
        public static string AltUrlPath(string title)
        {
            if (string.IsNullOrEmpty(RomsDir)) return null;
            return Path.Combine(RomsDir, SafeName(title) + " (Couch Gaming Ninja).url");
        }

        /// <summary>The file this app wrote for the game, if any.</summary>
        public static string OurPath(string title)
        {
            var name = Settings.Get(FileKey + title);
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(RomsDir)) return null;
            return Path.Combine(RomsDir, name);
        }

        public static bool IsOurFile(string path)
        {
            try { return path != null && File.Exists(path) && File.ReadAllText(path).Contains(Marker); } catch { return false; }
        }

        public static bool HasUrl(string title)
        {
            return IsOurFile(OurPath(title));
        }

        /// <summary>True when "Title.url" exists and wasn't written by this app (the user's own, or another tool's).</summary>
        public static bool HasForeignFile(string title)
        {
            var p = UrlPath(title);
            if (p == null || !File.Exists(p)) return false;
            if (IsOurFile(p)) return false;
            return true;
        }

        public enum Conflict { None, Replace, KeepBoth }

        /// <summary>Writes our .url. With Replace, the user's file is backed up first; with KeepBoth, ours gets another name.</summary>
        public static void WriteUrl(string title, SteamShortcut s, Conflict choice)
        {
            Directory.CreateDirectory(RomsDir);
            string target = OurPath(title);
            if (target == null || !IsOurFile(target))
            {
                target = UrlPath(title);
                if (File.Exists(target) && !IsOurFile(target))
                {
                    if (choice == Conflict.KeepBoth) target = AltUrlPath(title);
                    else if (choice == Conflict.Replace)
                    {
                        Directory.CreateDirectory(AppPaths.RetroBatBackups);
                        string backupName = Path.GetFileName(target);
                        File.Copy(target, Path.Combine(AppPaths.RetroBatBackups, backupName), true);
                        Settings.Set(BackupKey + title, backupName);
                    }
                    else return; // never overwrite without an answer
                }
            }
            File.WriteAllText(target, Launcher.UrlFile(s));
            Settings.Set(FileKey + title, Path.GetFileName(target));
        }

        /// <summary>Deletes our .url (only if it is ours) and puts back the user's original if we had replaced it.</summary>
        public static void DeleteUrl(string title)
        {
            var ours = OurPath(title);
            if (IsOurFile(ours)) File.Delete(ours);
            Settings.Remove(FileKey + title);

            var backupName = Settings.Get(BackupKey + title);
            if (!string.IsNullOrEmpty(backupName) && !string.IsNullOrEmpty(RomsDir))
            {
                string backup = Path.Combine(AppPaths.RetroBatBackups, backupName);
                string original = Path.Combine(RomsDir, backupName);
                if (File.Exists(backup) && !File.Exists(original)) File.Copy(backup, original);
                Settings.Remove(BackupKey + title);
            }
        }

        /// <summary>Titles that have a RetroBat file or backup recorded (also those whose Steam shortcut is already gone).</summary>
        public static IEnumerable<string> TrackedTitles()
        {
            return Settings.Keys(FileKey).Select(k => k.Substring(FileKey.Length))
                .Concat(Settings.Keys(BackupKey).Select(k => k.Substring(BackupKey.Length)))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    /// <summary>Small key/value settings stored in the app's folder.</summary>
    public static class Settings
    {
        static Dictionary<string, object> data;

        public static void Reset() { data = null; }

        static Dictionary<string, object> Data
        {
            get
            {
                if (data != null) return data;
                data = new Dictionary<string, object>();
                try
                {
                    if (File.Exists(AppPaths.SettingsFile))
                    {
                        var o = Json.Obj(Json.Parse(File.ReadAllText(AppPaths.SettingsFile)));
                        if (o != null) foreach (var kv in o) data[kv.Key] = kv.Value;
                    }
                }
                catch { }
                return data;
            }
        }

        public static string Get(string key)
        {
            object v;
            return Data.TryGetValue(key, out v) && v != null ? v.ToString() : null;
        }

        public static IEnumerable<string> Keys(string prefix)
        {
            return Data.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList();
        }

        public static void Set(string key, string value)
        {
            Data[key] = value;
            Save();
        }

        public static void Remove(string key)
        {
            if (Data.Remove(key)) Save();
        }

        public static void Save()
        {
            AppPaths.Ensure();
            File.WriteAllText(AppPaths.SettingsFile, Json.Serialize(Data));
        }
    }
}
