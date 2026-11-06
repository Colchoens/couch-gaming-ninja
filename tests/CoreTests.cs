using System;
using System.IO;
using System.Text;
using CouchGamingNinja;

static class CoreTests
{
    static int failed = 0;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + what);
        if (!ok) failed++;
    }

    static int Main()
    {
        // appid must match what Steam generated on the real PC for the Heroic shortcut
        uint id = Steam.ShortcutAppId("\"D:\\Heroic\\Heroic.exe\"", "Rocket League®");
        ulong gid = ((ulong)id << 32) | 0x02000000UL;
        Check(gid == 15582105212066725888UL, "rungameid matches Steam (Heroic shortcut): " + gid);

        uint rb = Steam.ShortcutAppId("\"C:\\Games\\Retrobat\\retrobat.exe\"", "Retrobat");
        Check((rb & 0x80000000u) != 0, "top bit set");

        // binary VDF round trip with a Steam-like file
        var root = VdfNode.NewMap();
        root.Set("shortcuts", VdfNode.NewMap());
        var s = new SteamShortcut { Name = "Rocket League®", Exe = @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe", StartDir = @"D:\rocketleague", LaunchOptions = Launcher.LaunchOptions("Sugar", "RocketLeague") };
        uint a = Steam.UpsertShortcut(root, s);
        Steam.UpsertShortcut(root, s); // idempotent
        var bytes = BinaryVdf.Write(root);
        var back = BinaryVdf.Read(bytes);
        Check(back.Get("shortcuts").Children.Count == 1, "upsert is idempotent");
        var e = back.Get("shortcuts").Get("0");
        Check(e.GetString("AppName") == "Rocket League®", "name survives (UTF-8)");
        Check(unchecked((uint)e.Get("appid").Int) == a, "appid survives");
        Check(Convert.ToBase64String(BinaryVdf.Write(back)) == Convert.ToBase64String(bytes), "binary round trip is byte-identical");
        Check(Steam.OurShortcutNames(back).Contains("Rocket League®"), "tagged as ours");
        Check(bytes[bytes.Length - 1] == 0x08 && bytes[bytes.Length - 2] == 0x08, "ends with 08 08");
        Check(Steam.RemoveShortcut(back, "Rocket League®") && back.Get("shortcuts").Children.Count == 0, "remove");

        // foreign shortcut is preserved and never matched
        var r2 = VdfNode.NewMap(); var l2 = r2.GetOrAddMap("shortcuts");
        var other = VdfNode.NewMap(); other.Set("appid", VdfNode.NewInt(123)); other.Set("AppName", VdfNode.NewString("Rocket League®")); other.Set("tags", VdfNode.NewMap());
        l2.Children.Add(new System.Collections.Generic.KeyValuePair<string, VdfNode>("0", other));
        Steam.UpsertShortcut(r2, s);
        Check(l2.Children.Count == 2 && l2.Get("0").Get("appid").Int == 123, "user's own shortcut with same name untouched");

        // text VDF
        string txt = "\"UserLocalConfigStore\"\n{\n\t\"Apps\"\n\t{\n\t\t\"123\"\n\t\t{\n\t\t\t\"OverlayAppEnable\"\t\t\"1\"\n\t\t}\n\t}\n\t\"x\"\t\t\"a \\\"q\\\" b\"\n}\n";
        var t = TextVdf.Parse(txt);
        Check(TextVdf.Write(t) == txt, "text VDF round trip");
        var apps = t.Get("UserLocalConfigStore").Get("Apps");
        apps.GetOrAddMap("2851537152").Set("UseSteamControllerConfig", VdfNode.NewString("2"));
        var t2 = TextVdf.Parse(TextVdf.Write(t));
        Check(t2.Get("UserLocalConfigStore").Get("Apps").Get("2851537152").GetString("UseSteamControllerConfig") == "2", "steam input key written");

        // launch options: no double quotes inside the -Command string
        string lo = Launcher.LaunchOptions("Sugar", "RocketLeague");
        string inner = lo.Substring(lo.IndexOf("-Command \"") + 10);
        inner = inner.Substring(0, inner.Length - 1);
        Check(inner.IndexOf('"') < 0, "launch script has no inner double quotes");
        Console.WriteLine(lo);

        Check(lo.Contains("Test-Path") && lo.Contains("Open Couch Gaming Ninja to repair"), "shortcut warns when the app folder is missing");

        // moving the app folder: launch options are rewritten, appid stays the same
        var r3 = VdfNode.NewMap();
        Steam.UpsertShortcut(r3, s);
        uint before = unchecked((uint)r3.Get("shortcuts").Get("0").Get("appid").Int);
        string oldRoot = AppPaths.Root;
        Check(Steam.OurShortcutsNeedingRepair(r3, AppPaths.LegendaryExe).Count == 0, "fresh shortcut needs no repair");
        Check(Steam.OurShortcutsNeedingRepair(r3, @"E:\Elsewhere\legendary.exe").Count == 1, "shortcut pointing elsewhere needs repair");
        Check(Steam.ReplaceInOurLaunchOptions(r3, oldRoot, @"E:\Elsewhere") == 1, "move rewrites our shortcut");
        string moved = r3.Get("shortcuts").Get("0").GetString("LaunchOptions");
        Check(moved.Contains(@"E:\Elsewhere") && moved.Contains("legendary.exe") && !moved.Contains(oldRoot), "launch options point to the new folder");
        Check(unchecked((uint)r3.Get("shortcuts").Get("0").Get("appid").Int) == before, "appid unchanged by a move (RetroBat links keep working)");

        // RetroBat: a .url the app didn't write is never overwritten without an answer, and is restored on removal
        string tmp = Path.Combine(Path.GetTempPath(), "cgn-test-" + Guid.NewGuid().ToString("N"));
        string roms = Path.Combine(tmp, "roms", "windows");
        Directory.CreateDirectory(roms);
        AppPaths.SetRoot(Path.Combine(tmp, "app"));
        RetroBat.RomsDir = roms;
        string mine = Path.Combine(roms, "Rocket League®.url");
        string userText = "[InternetShortcut]\r\nURL=steam://rungameid/15582105212066725888\r\n";
        File.WriteAllText(mine, userText);
        Check(RetroBat.HasForeignFile("Rocket League®"), "user's own .url detected");
        RetroBat.WriteUrl("Rocket League®", s, RetroBat.Conflict.None);
        Check(File.ReadAllText(mine) == userText && !RetroBat.HasUrl("Rocket League®"), "no answer: user's file untouched");
        RetroBat.WriteUrl("Rocket League®", s, RetroBat.Conflict.KeepBoth);
        Check(File.ReadAllText(mine) == userText && RetroBat.HasUrl("Rocket League®") && File.Exists(Path.Combine(roms, "Rocket League® (Couch Gaming Ninja).url")), "keep both: ours gets its own name");
        RetroBat.WriteUrl("Rocket League®", s, RetroBat.Conflict.KeepBoth);
        Check(Directory.GetFiles(roms).Length == 2, "applying again doesn't make a third file");
        RetroBat.DeleteUrl("Rocket League®");
        Check(Directory.GetFiles(roms).Length == 1 && File.ReadAllText(mine) == userText, "remove (keep both): only ours deleted");
        RetroBat.WriteUrl("Rocket League®", s, RetroBat.Conflict.Replace);
        Check(RetroBat.IsOurFile(mine) && File.Exists(Path.Combine(AppPaths.RetroBatBackups, "Rocket League®.url")), "replace: backup kept, ours in place");
        Check(new System.Collections.Generic.List<string>(RetroBat.TrackedTitles()).Contains("Rocket League®"), "replaced file is tracked");
        RetroBat.DeleteUrl("Rocket League®");
        Check(File.Exists(mine) && File.ReadAllText(mine) == userText, "remove (replace): user's original restored");
        Check(new System.Collections.Generic.List<string>(RetroBat.TrackedTitles()).Count == 0, "nothing left tracked");
        AppPaths.ForgetRoot();
        try { Directory.Delete(tmp, true); } catch { }

        Console.WriteLine(Launcher.UrlFile(s));
        Console.WriteLine(failed == 0 ? "ALL PASSED" : failed + " FAILED");
        return failed;
    }
}
