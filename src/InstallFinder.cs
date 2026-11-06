using System;
using System.Collections.Generic;
using System.IO;

namespace CouchGamingNinja
{
    public class FoundInstall
    {
        public string AppName;
        public string Title;
        public string InstallPath;
        public string Executable;
        public string Source;
    }

    /// <summary>Finds games already installed by the Epic Games Launcher or Heroic, so nothing needs to be downloaded again.</summary>
    public static class InstallFinder
    {
        public static List<FoundInstall> Find()
        {
            var all = new List<FoundInstall>();
            try { all.AddRange(FromEpicLauncher()); } catch { }
            try { all.AddRange(FromHeroic()); } catch { }
            return all;
        }

        // C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests\*.item (JSON)
        static IEnumerable<FoundInstall> FromEpicLauncher()
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Epic\EpicGamesLauncher\Data\Manifests");
            if (!Directory.Exists(dir)) yield break;
            foreach (var file in Directory.GetFiles(dir, "*.item"))
            {
                IDictionary<string, object> o = null;
                try { o = Json.Obj(Json.Parse(File.ReadAllText(file))); } catch { }
                if (o == null) continue;
                string app = Json.Str(o, "AppName");
                string path = Json.Str(o, "InstallLocation");
                if (string.IsNullOrEmpty(app) || string.IsNullOrEmpty(path) || !Directory.Exists(path)) continue;
                yield return new FoundInstall
                {
                    AppName = app,
                    Title = Json.Str(o, "DisplayName"),
                    InstallPath = path,
                    Executable = Json.Str(o, "LaunchExecutable"),
                    Source = "epic"
                };
            }
        }

        // %APPDATA%\heroic\legendaryConfig\legendary\installed.json  { "<app>": { "install_path": ..., "executable": ... } }
        static IEnumerable<FoundInstall> FromHeroic()
        {
            string file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"heroic\legendaryConfig\legendary\installed.json");
            if (!File.Exists(file)) yield break;
            var o = Json.Obj(Json.Parse(File.ReadAllText(file)));
            if (o == null) yield break;
            foreach (var kv in o)
            {
                var g = Json.Obj(kv.Value);
                string path = Json.Str(g, "install_path");
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) continue;
                yield return new FoundInstall
                {
                    AppName = kv.Key,
                    Title = Json.Str(g, "title"),
                    InstallPath = path,
                    Executable = Json.Str(g, "executable"),
                    Source = "heroic"
                };
            }
        }
    }
}
