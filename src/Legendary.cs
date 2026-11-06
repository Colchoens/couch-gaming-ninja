using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace CouchGamingNinja
{
    public class EpicGame
    {
        public string AppName;
        public string Title;
        public bool Installed;          // installed and known to our Legendary
        public string InstallPath;      // from Legendary, the Epic Launcher or Heroic
        public string Executable;       // relative path, e.g. Binaries/Win64/RocketLeague.exe
        public string Source;           // "legendary", "epic", "heroic" or null

        public string ProcessName
        {
            get { return string.IsNullOrEmpty(Executable) ? null : Path.GetFileNameWithoutExtension(Executable.Replace('/', '\\')); }
        }

        public bool HasInstall { get { return !string.IsNullOrEmpty(InstallPath) && Directory.Exists(InstallPath); } }
    }

    public class CommandResult
    {
        public int ExitCode;
        public string Output;
        public string Error;
        public bool Ok { get { return ExitCode == 0; } }
    }

    /// <summary>Runs Legendary (https://github.com/derrod/legendary), the open-source Epic client.</summary>
    public static class Legendary
    {
        // 0.20.34: its embedded Epic login window works on Windows; 0.21.1's fails (pythonnet / Python 3.14).
        public const string PinnedVersion = "0.20.34";
        public const string DownloadUrl = "https://github.com/derrod/legendary/releases/download/0.20.34/legendary.exe";

        public static bool IsDownloaded { get { return File.Exists(AppPaths.LegendaryExe) && new FileInfo(AppPaths.LegendaryExe).Length > 1000000; } }

        public static void Download()
        {
            AppPaths.Ensure();
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string tmp = AppPaths.LegendaryExe + ".part";
            using (var wc = new WebClient())
            {
                wc.Headers.Add("User-Agent", "CouchGamingNinja/" + AppPaths.Version);
                wc.DownloadFile(DownloadUrl, tmp);
            }
            if (File.Exists(AppPaths.LegendaryExe)) File.Delete(AppPaths.LegendaryExe);
            File.Move(tmp, AppPaths.LegendaryExe);
        }

        public static CommandResult Run(params string[] args)
        {
            return RunWithTimeout(TimeSpan.FromMinutes(5), args);
        }

        /// <summary>
        /// Runs Legendary hidden. Its stdin is closed, so a question it asks (like "import DLCs? [Y/n]") ends instead of
        /// waiting forever for an answer nobody can type; and it is stopped after the timeout.
        /// </summary>
        public static CommandResult RunWithTimeout(TimeSpan timeout, params string[] args)
        {
            var psi = new ProcessStartInfo(AppPaths.LegendaryExe, string.Join(" ", args.Select(Quote)))
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = AppPaths.Root
            };
            psi.EnvironmentVariables["LEGENDARY_CONFIG_PATH"] = AppPaths.LegendaryConfig;
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8";
            using (var p = Process.Start(psi))
            {
                try { p.StandardInput.Close(); } catch { }
                var err = p.StandardError.ReadToEndAsync();
                var outp = p.StandardOutput.ReadToEndAsync();
                if (!p.WaitForExit((int)timeout.TotalMilliseconds))
                {
                    try { p.Kill(); } catch { }
                    return new CommandResult { ExitCode = -1, Output = "", Error = "Legendary didn't answer in " + (int)timeout.TotalMinutes + " minutes and was stopped (legendary " + string.Join(" ", args) + ")." };
                }
                p.WaitForExit();
                return new CommandResult { ExitCode = p.ExitCode, Output = outp.Result, Error = err.Result };
            }
        }

        static string Quote(string a)
        {
            if (a.Length > 0 && a.IndexOfAny(new[] { ' ', '\t', '"' }) < 0) return a;
            return "\"" + a.Replace("\"", "\\\"") + "\"";
        }

        /// <summary>Opens Epic's own sign-in window (Legendary's embedded browser). Blocks until it closes.</summary>
        public static CommandResult Login()
        {
            return RunWithTimeout(TimeSpan.FromMinutes(15), "auth");
        }

        public static CommandResult Logout()
        {
            return Run("auth", "--delete");
        }

        /// <summary>The signed-in Epic account name, or null when not signed in.</summary>
        public static string Account()
        {
            if (!IsDownloaded) return null;
            var r = Run("status", "--json");
            var o = Json.Obj(Json.Parse(r.Output));
            string acc = Json.Str(o, "account");
            if (string.IsNullOrEmpty(acc) || acc.IndexOf("not logged in", StringComparison.OrdinalIgnoreCase) >= 0) return null;
            return acc;
        }

        public static List<EpicGame> Games()
        {
            var games = new Dictionary<string, EpicGame>(StringComparer.OrdinalIgnoreCase);

            var owned = Run("list", "--json");
            foreach (var item in Json.Arr(Json.Parse(owned.Output)))
            {
                var o = Json.Obj(item);
                string app = Json.Str(o, "app_name");
                if (app == null) continue;
                games[app] = new EpicGame { AppName = app, Title = Json.Str(o, "app_title") ?? app };
            }

            var installed = Run("list-installed", "--json");
            foreach (var item in Json.Arr(Json.Parse(installed.Output)))
            {
                var o = Json.Obj(item);
                string app = Json.Str(o, "app_name");
                if (app == null) continue;
                EpicGame g;
                if (!games.TryGetValue(app, out g)) games[app] = g = new EpicGame { AppName = app };
                g.Title = Json.Str(o, "title") ?? g.Title ?? app;
                g.Installed = true;
                g.InstallPath = Json.Str(o, "install_path");
                g.Executable = Json.Str(o, "executable");
                g.Source = "legendary";
            }

            // Games installed by the Epic Launcher or Heroic that our Legendary doesn't know yet.
            foreach (var found in InstallFinder.Find())
            {
                EpicGame g;
                if (!games.TryGetValue(found.AppName, out g)) continue; // not in this account
                if (g.Installed) continue;
                g.InstallPath = found.InstallPath;
                g.Executable = g.Executable ?? found.Executable;
                g.Source = found.Source;
            }

            return games.Values.OrderBy(g => g.Installed || g.HasInstall ? 0 : 1).ThenBy(g => g.Title, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Registers an existing install with Legendary. Reads the folder only; game files are not changed.</summary>
        public static CommandResult Import(string appName, string installPath)
        {
            // --skip-dlcs: otherwise Legendary asks "import DLCs? [Y/n]" for games with DLC (Rocket League) and waits.
            return RunWithTimeout(TimeSpan.FromMinutes(3), "import", "--skip-dlcs", appName, installPath);
        }
    }
}
