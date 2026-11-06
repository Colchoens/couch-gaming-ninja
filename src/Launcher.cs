using System;
using System.IO;

namespace CouchGamingNinja
{
    /// <summary>
    /// Builds what Steam runs: a hidden PowerShell that launches the game through Legendary and then waits
    /// for the game's own process to exit. Steam tracks the PowerShell process, so it sees the whole session
    /// and applies Steam Input to the game.
    /// </summary>
    public static class Launcher
    {
        public static string PowerShellExe
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), @"WindowsPowerShell\v1.0\powershell.exe"); }
        }

        public static SteamShortcut ShortcutFor(EpicGame g)
        {
            return new SteamShortcut
            {
                Name = g.Title,
                Exe = PowerShellExe,
                StartDir = g.InstallPath,
                LaunchOptions = LaunchOptions(g.AppName, g.ProcessName),
                Icon = string.IsNullOrEmpty(g.Executable) ? "" : Path.Combine(g.InstallPath, g.Executable.Replace('/', '\\'))
            };
        }

        public static string LaunchOptions(string appName, string processName)
        {
            string leg = PsQuote(AppPaths.LegendaryExe);
            string cfg = PsQuote(AppPaths.LegendaryConfig);
            string app = PsQuote(appName);
            // If the app's folder was deleted or moved by hand, say so instead of silently doing nothing.
            string script = "if(-not(Test-Path " + leg + ")){Add-Type -A System.Windows.Forms;"
                          + "[void][Windows.Forms.MessageBox]::Show('Couch Gaming Ninja files are missing. Open Couch Gaming Ninja to repair this shortcut.','Couch Gaming Ninja');exit 1};";
            // --skip-version-check: games are usually updated by the Epic Launcher or Heroic, which our Legendary
            // doesn't see, so its own version record would wrongly block the launch.
            script += " $env:LEGENDARY_CONFIG_PATH=" + cfg + "; & " + leg + " launch " + app + " --skip-version-check;";
            if (!string.IsNullOrEmpty(processName))
            {
                string proc = PsQuote(processName);
                script += " $i=0; while(-not (Get-Process " + proc + " -EA 0) -and $i -lt 120){Start-Sleep 1; $i++};"
                        + " while(Get-Process " + proc + " -EA 0){Start-Sleep 2}";
            }
            return "-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -Command \"" + script + "\"";
        }

        // PowerShell single-quoted string; double quotes are avoided because the whole script sits inside "...".
        public static string PsQuote(string s)
        {
            return "'" + (s ?? "").Replace("'", "''") + "'";
        }

        /// <summary>Contents of a .url file a frontend like RetroBat can launch. The marker section tells the app the file is its own.</summary>
        public static string UrlFile(SteamShortcut s)
        {
            return "[InternetShortcut]\r\nURL=steam://rungameid/" + s.GameId + "\r\nIconIndex=0\r\n"
                 + (string.IsNullOrEmpty(s.Icon) ? "" : "IconFile=" + s.Icon + "\r\n")
                 + "\r\n" + RetroBat.Marker + "\r\nCreatedBy=" + AppPaths.AppName + " " + AppPaths.Version + "\r\n";
        }
    }
}
