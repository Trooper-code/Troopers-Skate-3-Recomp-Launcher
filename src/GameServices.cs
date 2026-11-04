// Trooper's Skate 3 Recomp Launcher — v3.1 background services (no UI).
// Mod loader, recomp installer/updater, game file checks, benchmark runner, Steam shortcuts,
// graphics driver check and crash dumps. Like LauncherServices.cs this only uses assemblies that
// ship with the .NET Framework in Windows, and it is written for the C# 5 compiler in build.cmd.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Skate3Launcher
{
    // ====================================================================== small helpers

    static class FileUtil
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern bool CreateHardLink(string newFile, string existingFile, IntPtr security);

        // Creates a hard link; returns 0 on success or the Win32 error code.
        public static int HardLink(string newFile, string existingFile)
        {
            if (CreateHardLink(newFile, existingFile, IntPtr.Zero)) return 0;
            int err = Marshal.GetLastWin32Error();
            return err == 0 ? -1 : err;
        }

        public static string Sha256(string path, Func<bool> cancelled, Action<long> progressed)
        {
            using (var sha = SHA256.Create())
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 1 << 20))
            {
                var buf = new byte[1 << 20];
                int n;
                while ((n = fs.Read(buf, 0, buf.Length)) > 0)
                {
                    if (cancelled != null && cancelled()) throw new OperationCanceledException();
                    sha.TransformBlock(buf, 0, n, null, 0);
                    if (progressed != null) progressed(n);
                }
                sha.TransformFinalBlock(buf, 0, 0);
                var sb = new StringBuilder(64);
                foreach (byte b in sha.Hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        public static string Rel(string root, string full)
        {
            return full.Substring(root.TrimEnd('\\', '/').Length).TrimStart('\\', '/').Replace('\\', '/');
        }

        // True when 'full' is inside 'root' (used to refuse zip entries like ..\..\evil.dll).
        public static bool Inside(string root, string full)
        {
            string r = Path.GetFullPath(root).TrimEnd('\\') + "\\";
            return Path.GetFullPath(full).StartsWith(r, StringComparison.OrdinalIgnoreCase);
        }

        public static void CopyStreamToFile(Stream src, string dest)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            string tmp = dest + ".tmp-launcher";
            using (var dst = new FileStream(tmp, FileMode.Create)) src.CopyTo(dst);
            if (File.Exists(dest)) File.Delete(dest);
            File.Move(tmp, dest);
        }

        public static string DriveFormat(string path)
        {
            try { return new DriveInfo(Path.GetPathRoot(Path.GetFullPath(path))).DriveFormat; }
            catch { return null; }
        }

        public static string GameRoot(string gameExe) { return gameExe == null ? null : Path.Combine(Path.GetDirectoryName(gameExe), "game"); }

        public static void TryDeleteDir(string dir)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); } catch { }
        }
    }

    // Tiny JSON reader for GitHub API replies: objects become Dictionary<string, object>, arrays List<object>,
    // numbers double, plus string / bool / null.
    static class MiniJson
    {
        public static object Parse(string s)
        {
            int i = 0;
            var v = Value(s, ref i);
            return v;
        }

        static void Ws(string s, ref int i) { while (i < s.Length && char.IsWhiteSpace(s[i])) i++; }

        static object Value(string s, ref int i)
        {
            Ws(s, ref i);
            if (i >= s.Length) throw new FormatException("Unexpected end of JSON");
            char c = s[i];
            if (c == '{')
            {
                var d = new Dictionary<string, object>();
                i++;
                Ws(s, ref i);
                if (i < s.Length && s[i] == '}') { i++; return d; }
                while (true)
                {
                    Ws(s, ref i);
                    string k = Str(s, ref i);
                    Ws(s, ref i);
                    if (s[i] != ':') throw new FormatException("Expected ':'");
                    i++;
                    d[k] = Value(s, ref i);
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw new FormatException("Expected ',' or '}'");
                }
            }
            if (c == '[')
            {
                var l = new List<object>();
                i++;
                Ws(s, ref i);
                if (i < s.Length && s[i] == ']') { i++; return l; }
                while (true)
                {
                    l.Add(Value(s, ref i));
                    Ws(s, ref i);
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return l; }
                    throw new FormatException("Expected ',' or ']'");
                }
            }
            if (c == '"') return Str(s, ref i);
            if (s.Substring(i).StartsWith("true")) { i += 4; return true; }
            if (s.Substring(i).StartsWith("false")) { i += 5; return false; }
            if (s.Substring(i).StartsWith("null")) { i += 4; return null; }
            int start = i;
            while (i < s.Length && "+-0123456789.eE".IndexOf(s[i]) >= 0) i++;
            return double.Parse(s.Substring(start, i - start), CultureInfo.InvariantCulture);
        }

        static string Str(string s, ref int i)
        {
            if (s[i] != '"') throw new FormatException("Expected string");
            i++;
            var sb = new StringBuilder();
            while (s[i] != '"')
            {
                char c = s[i++];
                if (c != '\\') { sb.Append(c); continue; }
                char e = s[i++];
                switch (e)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u': sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16)); i += 4; break;
                    default: sb.Append(e); break;
                }
            }
            i++;
            return sb.ToString();
        }

        public static string S(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(key, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;
        }

        public static object O(object o, string key)
        {
            var d = o as Dictionary<string, object>;
            object v;
            return d != null && d.TryGetValue(key, out v) ? v : null;
        }
    }

    static class Http
    {
        public static HttpWebRequest Request(string url)
        {
            ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2
            var req = (HttpWebRequest)WebRequest.Create(url);
            req.UserAgent = "TroopersSkate3Launcher";
            req.Timeout = 15000;
            req.ReadWriteTimeout = 30000;
            return req;
        }

        public static string GetString(string url, string accept)
        {
            var req = Request(url);
            if (accept != null) req.Accept = accept;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8)) return sr.ReadToEnd();
        }
    }

    // ====================================================================== recomp version tracking

    // The game's log only records its version after it runs, so right after the launcher installs an update
    // it remembers the tag it installed (keyed to skate3.exe's size and timestamp) until the logs catch up.
    static class RecompVersion
    {
        static string Stamp(string exe)
        {
            try { var f = new FileInfo(exe); return f.Length + ":" + f.LastWriteTimeUtc.Ticks; } catch { return null; }
        }

        public static void Remember(string exe, string tag)
        {
            LauncherPrefs.Write("recomp_installed_tag", tag);
            LauncherPrefs.Write("recomp_installed_stamp", Stamp(exe) ?? "");
        }

        public static string Current(string logsDir, string exe)
        {
            if (exe != null)
            {
                string tag = LauncherPrefs.Read("recomp_installed_tag");
                string stamp = LauncherPrefs.Read("recomp_installed_stamp");
                if (!string.IsNullOrEmpty(tag) && stamp == Stamp(exe))
                {
                    // the logs win again once the new build has actually run
                    var log = LogTools.Files(logsDir).FirstOrDefault();
                    if (log == null || log.LastWriteTimeUtc < File.GetLastWriteTimeUtc(exe)) return tag.TrimStart('v', 'V');
                }
            }
            return LogTools.InstalledVersion(logsDir, exe);
        }
    }

    // ====================================================================== recomp releases: install, update, roll back

    class RecompRelease
    {
        public string Tag, PageUrl, Notes, AssetName, AssetUrl;
        public long AssetSize;
        public DateTime Published;
    }

    class RecompBackup
    {
        public string Dir, FromVersion, ToVersion;
        public DateTime When;
        public int Files;
    }

    static class RecompInstaller
    {
        public const string BackupFolder = "recomp-backups";

        public static RecompRelease Latest(string repo)
        {
            string json = Http.GetString("https://api.github.com/repos/" + repo + "/releases/latest", "application/vnd.github+json");
            var root = MiniJson.Parse(json);
            var r = new RecompRelease
            {
                Tag = MiniJson.S(root, "tag_name"),
                PageUrl = MiniJson.S(root, "html_url"),
                Notes = MiniJson.S(root, "body") ?? ""
            };
            DateTime pub;
            if (DateTime.TryParse(MiniJson.S(root, "published_at"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out pub)) r.Published = pub;
            var assets = MiniJson.O(root, "assets") as List<object>;
            if (assets != null)
            {
                object pick = assets.FirstOrDefault(a => string.Equals(MiniJson.S(a, "name"), "Skate3Recomp-Windows.zip", StringComparison.OrdinalIgnoreCase)) ??
                              assets.FirstOrDefault(a => (MiniJson.S(a, "name") ?? "").EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                                                         (MiniJson.S(a, "name") ?? "").IndexOf("windows", StringComparison.OrdinalIgnoreCase) >= 0);
                if (pick != null)
                {
                    r.AssetName = MiniJson.S(pick, "name");
                    r.AssetUrl = MiniJson.S(pick, "browser_download_url");
                    long size;
                    long.TryParse(MiniJson.S(pick, "size"), NumberStyles.Float, CultureInfo.InvariantCulture, out size);
                    r.AssetSize = size;
                }
            }
            if (r.Tag == null) throw new InvalidDataException("GitHub didn't return a release.");
            return r;
        }

        public static string Download(RecompRelease r, string destDir, Action<long, long> progress, Func<bool> cancelled)
        {
            if (r.AssetUrl == null) throw new InvalidOperationException("This release has no Windows download (" + (r.Tag ?? "?") + ").");
            Directory.CreateDirectory(destDir);
            string path = Path.Combine(destDir, r.AssetName ?? "Skate3Recomp-Windows.zip");
            string tmp = path + ".download";
            var req = Http.Request(r.AssetUrl);
            req.Timeout = 30000;
            using (var resp = (HttpWebResponse)req.GetResponse())
            using (var src = resp.GetResponseStream())
            using (var dst = new FileStream(tmp, FileMode.Create))
            {
                long total = resp.ContentLength > 0 ? resp.ContentLength : r.AssetSize, got = 0;
                var buf = new byte[1 << 16];
                int n;
                while ((n = src.Read(buf, 0, buf.Length)) > 0)
                {
                    if (cancelled()) throw new OperationCanceledException();
                    dst.Write(buf, 0, n);
                    got += n;
                    progress(got, total);
                }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return path;
        }

        // Where skate3.exe sits inside the release zip ("" or "Skate3Recomp-Windows/").
        static string Prefix(ZipArchive zip)
        {
            var exe = zip.Entries.FirstOrDefault(e => e.Name.Equals("skate3.exe", StringComparison.OrdinalIgnoreCase));
            if (exe == null) throw new InvalidDataException("This zip doesn't contain skate3.exe, so it isn't a Skate 3 Recomp release.");
            return exe.FullName.Substring(0, exe.FullName.Length - exe.Name.Length);
        }

        static bool Skip(string rel)
        {
            string r = rel.ToLowerInvariant();
            // never touch the game data, the user's files or this launcher
            return r.StartsWith("game/") || r.StartsWith("mods/") || r.StartsWith("logs/") || r.StartsWith("saves/") ||
                   r.StartsWith(BackupFolder + "/") || r.EndsWith("launcher.exe") || r == "settings.toml" || r == "portable.txt";
        }

        // Installs a release zip over installDir. Every file it replaces is copied into recomp-backups first so it can be rolled back.
        public static RecompBackup Install(string zipPath, string installDir, string fromVersion, string toVersion, Action<string> status)
        {
            string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
            string backupDir = Path.Combine(installDir, BackupFolder, stamp + "-v" + Paths.SafeName((fromVersion ?? "unknown").TrimStart('v')));
            var manifest = new List<string>();
            using (var zip = new ZipArchive(new FileStream(zipPath, FileMode.Open, FileAccess.Read), ZipArchiveMode.Read))
            {
                string prefix = Prefix(zip);
                var entries = zip.Entries.Where(e => e.Name.Length > 0 && e.FullName.StartsWith(prefix)).ToList();
                Directory.CreateDirectory(backupDir);
                foreach (var e in entries)
                {
                    string rel = e.FullName.Substring(prefix.Length);
                    if (Skip(rel)) continue;
                    string dest = Path.Combine(installDir, rel.Replace('/', '\\'));
                    if (!FileUtil.Inside(installDir, dest)) continue;
                    if (File.Exists(dest))
                    {
                        string b = Path.Combine(backupDir, rel.Replace('/', '\\'));
                        Directory.CreateDirectory(Path.GetDirectoryName(b));
                        File.Copy(dest, b, true);
                        manifest.Add("R|" + rel);
                    }
                    else manifest.Add("A|" + rel);
                }
                File.WriteAllLines(Path.Combine(backupDir, "backup.txt"), new[]
                {
                    "from=" + (fromVersion ?? ""), "to=" + (toVersion ?? ""), "when=" + DateTime.Now.ToString("o", CultureInfo.InvariantCulture)
                }.Concat(manifest));

                int done = 0;
                try
                {
                    foreach (var e in entries)
                    {
                        string rel = e.FullName.Substring(prefix.Length);
                        if (Skip(rel)) continue;
                        string dest = Path.Combine(installDir, rel.Replace('/', '\\'));
                        if (!FileUtil.Inside(installDir, dest)) continue;
                        if (status != null) status("Installing " + rel + " (" + (++done) + "/" + manifest.Count + ")…");
                        using (var s = e.Open()) FileUtil.CopyStreamToFile(s, dest);
                    }
                }
                catch
                {
                    // put everything back the way it was before re-throwing
                    try { Rollback(Read(backupDir), installDir); } catch { }
                    throw;
                }
            }
            Prune(installDir, 3);
            return Read(backupDir);
        }

        static RecompBackup Read(string dir)
        {
            var b = new RecompBackup { Dir = dir };
            var lines = File.ReadAllLines(Path.Combine(dir, "backup.txt"));
            foreach (var l in lines)
            {
                if (l.StartsWith("from=")) b.FromVersion = l.Substring(5);
                else if (l.StartsWith("to=")) b.ToVersion = l.Substring(3);
                else if (l.StartsWith("when=")) DateTime.TryParse(l.Substring(5), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out b.When);
                else if (l.StartsWith("R|") || l.StartsWith("A|")) b.Files++;
            }
            if (b.When == DateTime.MinValue) b.When = Directory.GetCreationTime(dir);
            return b;
        }

        public static List<RecompBackup> Backups(string installDir)
        {
            var list = new List<RecompBackup>();
            try
            {
                string root = Path.Combine(installDir, BackupFolder);
                if (!Directory.Exists(root)) return list;
                foreach (var d in Directory.GetDirectories(root))
                    if (File.Exists(Path.Combine(d, "backup.txt"))) try { list.Add(Read(d)); } catch { }
            }
            catch { }
            return list.OrderByDescending(b => b.When).ToList();
        }

        // Puts back the files a backup replaced and removes the ones the update added.
        public static void Rollback(RecompBackup b, string installDir)
        {
            foreach (var l in File.ReadAllLines(Path.Combine(b.Dir, "backup.txt")))
            {
                if (l.Length < 3 || l[1] != '|') continue;
                string rel = l.Substring(2);
                string dest = Path.Combine(installDir, rel.Replace('/', '\\'));
                if (!FileUtil.Inside(installDir, dest)) continue;
                if (l[0] == 'R')
                {
                    string src = Path.Combine(b.Dir, rel.Replace('/', '\\'));
                    if (File.Exists(src)) { Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Copy(src, dest, true); }
                }
                else if (l[0] == 'A' && File.Exists(dest)) File.Delete(dest);
            }
            FileUtil.TryDeleteDir(b.Dir);
        }

        static void Prune(string installDir, int keep)
        {
            foreach (var b in Backups(installDir).Skip(keep)) FileUtil.TryDeleteDir(b.Dir);
        }
    }

    // ====================================================================== game file check

    class GameCheck
    {
        public bool ExeFound, GameInstalled, XexKnown, TitleUpdate, WebkitPresent, Full, Cancelled;
        public string GameRoot, FileSystem, Error;
        public List<string> Missing = new List<string>(), Changed = new List<string>(), Notes = new List<string>();
        public int Checked, MatchedBaseline;
        public DateTime When = DateTime.Now;

        public bool SetupOk { get { return ExeFound && GameInstalled && TitleUpdate && WebkitPresent; } }
        public bool AllGood { get { return SetupOk && XexKnown && Missing.Count == 0 && Changed.Count == 0; } }
    }

    static class GameFiles
    {
        // Known-good Skate 3 dump (NTSC-U, with Title Update 3 staged by the recomp): path | size | SHA-256.
        // Title Update hashes are the same ones the recomp's own installer checks (skate3_title_update_installer.cpp).
        static readonly string[] Reference =
        {
            "data/anim/cac_edit_female.abin|3165904|1a4d5511a6a40be6e0bec386fe853cbf54cd187b02f7b6a74a66d88dfca934d4",
            "data/anim/cac_edit_male.abin|3191520|9a0e7d015ec28ef1b0b29991906806064c066f369ea9b94debe7ce42b4854b78",
            "data/audio/MixMapSK8.mxb|25252|105f46bbc4ae25cf51bafc8524e00b0aef608a3aa91424e3d39bbc57fff305c2",
            "data/audio/ambience.big|143708256|30f089774a16ca375d2a3bbaee2921dbaa3d916bae2003e23f1c9bba862270ec",
            "data/audio/ambienceresident.big|1568|97a7ab5f5e32ea67bdeff74600909853103e91a80f51edce890306a7c0c9a761",
            "data/audio/audiofiles.big|104748800|650a64b31783e710bb073fccf1fa66e16fca4d8c881ea655b1b6ba4d370faa94",
            "data/audio/english/announcerspeech.big|71081488|55b66648aa58b0e508ad7332beaddcd687d761974463912b5ec20eede566ed1c",
            "data/audio/english/cameramanspeech.big|37312384|1308edd641b4e2c4f18078d27fa3e51d25da8f5310498d53b2f18b5ff0c68a8f",
            "data/audio/english/livingworldspeech.big|342752832|2b93f72a5a30823011034f4e63f5e8c507d9d62a3dc0d6d9f5c6ff32a384024a",
            "data/audio/english/maincastspeech.big|162622352|1344f36fd44e24f66ced91a3e1bbd15d7a23e79df30925cdcf83e125e4eb9c5f",
            "data/audio/english/nis.big|77985456|d3f4f2e2b0cf3b9d96c7fdef2875be033e00f95f520c479b928e5ddc3aecfb1a",
            "data/audio/french/announcerspeech.big|30439456|fbdf4277b5964408edf9772b24a6c0b860eb21b59360fbf8e0dc55dd9c56ebf3",
            "data/audio/french/cameramanspeech.big|13197728|a2f9cb7f7919ca890d173563880b3d9a0bde43687f9917e00f978bb06e0d7d36",
            "data/audio/french/livingworldspeech.big|110227280|37075b9c19b5747b560797ee901efeda07019d32da0840e84b5578bb9f772e5d",
            "data/audio/french/maincastspeech.big|59269936|b2f539e9424d9a2039cb41830c7b16152b71e866db46f92a655394953cf80268",
            "data/audio/french/nis.big|73723392|a7b5f5558f73789e4f79ab7d66c20015c2823016e5d3bafb6dc701ec9c84734d",
            "data/audio/german/announcerspeech.big|34875328|51fb36c5827f2fd338ce44131e63b6acf38aca605f34df84ee7b9489ee4d10bf",
            "data/audio/german/cameramanspeech.big|12877600|92a7dbb429df84cc57ca5a8226f13a7f5d4973ede3b4abee1803676074b45b59",
            "data/audio/german/livingworldspeech.big|121050768|daabdb16642446aa3d1c3657857b57b7d2c95ec31533ec942650b99ed7778cf6",
            "data/audio/german/maincastspeech.big|61164144|2c6d76287436147a0d184d068a2d0c59ddc34583e58a3b1f4d3a3c3fbe970683",
            "data/audio/german/nis.big|75830256|756d57076387653bc317ae6ad662c73c7269507ff731dd492bb31ce1f852013f",
            "data/audio/grains.big|3446976|4d517b1624d62cab9256dd647c721292d7c90487c0ac2b7d840abc3b2e9efaad",
            "data/audio/music/Game_Stream.mus|28052480|6ba1b6e407a44dc2a0261411240319b00981a9b17a1abead63c80c8cc8d3b812",
            "data/audio/music/Ipod_Stream.mus|180580480|8b8be6eff062223fbe178944fb0bacd5faa625f2d76b44a42ecd7a0b3e6cd341",
            "data/audio/music/World_Stream.mus|220346880|213fbfa1dac84dba9854c741b33b0075a7903b249c186a009c36829f2ff2a77f",
            "data/audio/music/game.mpf|39160|12fb207bc411421d909f7bff99e5f0aea79c4437d3721891d2d34d4d7b74e08d",
            "data/audio/music/ipod.mpf|57560|8843fdb5c2dd3c436bd81669efb277092dea32d85daad335abea852e1d14a9b8",
            "data/audio/music/overlays.bh|336|334a0ff499e708daaceaf76733d2909eb9100bbca60b259a52080c776ab9294e",
            "data/audio/music/overlays.big|333236|e33ddb51de149dbd87ee2b5cffa6b3cf39e9275f35e1e91de2328a12398a263c",
            "data/audio/music/world.mpf|198860|ae8146c2ff0fe5f4b0f78744bb80426eb873ae2425254c60f3b2ae726882d036",
            "data/audio/post.big|16821296|0836e44f30d75acb49be8e39e652ce554c171d137b22f95fe816a64a859035a7",
            "data/audio/wheels.big|256304|d17db54193e3864ef3568c94fba6e5b64d65ac78ed01572de7abddf43af049f6",
            "data/big/db.big|2615040|b5c5967a2b31b28b4a03b1232061228d3d2c5a84dfc3e27e19e28f8da53ba87a",
            "data/big/fedata.big|1050624|dedc38588165a7deb2733a1b311db6732982795652d9ac57cc1f7f49ee669c7a",
            "data/big/fedynamic.big|28716480|3b8031820836a8147ea46c8e4f021514a56715007e554024c8dd48b52b003408",
            "data/big/fetexture.big|2170368|7fd3c82f1fa950d7c6484c47f5e45e0d5a809102ed74a463b463c12d91b2ed30",
            "data/big/miscboot.big|4347200|f3a212f9218d5bb0b96f67d6b64e1cf5e01833339e74a8a08fa267bbd5c44f45",
            "data/big/miscload.big|44920384|3673fdc0ca3b6daa72260350dd9263f068ec5999d9604b3c11bfe4f4248942ec",
            "data/big/scene.big|276683968|d99288136f19050fae34de1b65f53bff1c18f9f59654a4a3332471bcb414ccb9",
            "data/big/shaders_final.big|533184|aac10b1fd571b0a6a874e7c25dbcdad42ab85a42bb06f6941677172e3fa652ba",
            "data/content/createacharacter.big|472328128|b87e9e01d446df37d707d0f2ac2ab872baf29bba91eeae5fd08997c7d475d4eb",
            "data/content/livingworld.big|27549248|47aac8f5e63219aa5e4c6e28d473e4a68fcb440c8a3fc8cc5d9e35e9c3e6bd57",
            "data/content/marquee.big|142019712|0e12660e22c33239d9f28a708d09b2e3f291276012c0c6296c42ae20b6b41acc",
            "data/content/missions.big|64577152|b1354c3cfd5c653b338ae6016a91f136913d38456f0387cf68bff9f935c828cd",
            "data/content/parkassets.big|35185728|db390a97294da9293bdb80dbd424aa24473e3f964490199b964d29a8c6d1af9c",
            "data/content/proxyDowntown_100_Proxy.big|10271328|abfbc17b4aba2f706f01b3192a99f4dfb0d99f7bd47f36af3d13ac6e7e3d3e2c",
            "data/content/proxyIndustrial_100_Proxy.big|11181664|52112fff3d461fbb706cabd634285443d046afa38812558ee911b82a6f9eb291",
            "data/content/proxyUniversity_100_Proxy.big|12034240|1a9e66c58d0b1befe2271bbd92152fe61b8cd59d59c364bc869a5491559d9fba",
            "data/content/world/AssetList_AdInventory.bin|239|58f5468c8378b9e732eb4bc3a66e91bfb8437308f8597abeaa266e8b8ffe1a09",
            "data/content/worldDIST_BlackBoxPark.big|5339184|735468fbfdbd1e6d070cb71588f9a19eb3204dd4d3bdf5cbca13e0db716d31ff",
            "data/content/worldDIST_DownTown.big|843137744|a8a9a56e34ed82aa64fe592b57e37a61fed7b5b1f829328e540ba28bf7624fba",
            "data/content/worldDIST_DownTownSkatePark.big|11066304|415ce8441cd127ff60f3aeccd8efe6f6d24d4a922b74f23e5f562a1be2a5175d",
            "data/content/worldDIST_Industrial.big|477452272|0263a5631b36410d3d867a707d6d51e00a110a496a06902ceb0910be72fe014c",
            "data/content/worldDIST_IndustrialSkatePark.big|5846640|2f68c94952075c936504da70b572bf9c87969562032eb28456d5a61e0bd602d0",
            "data/content/worldDIST_MaloofMoneyCup.big|14546800|77524428d3c9608d631f7d9c5b3903d3f58a4d3bcc88c9c67f2dcb88a0e44ed2",
            "data/content/worldDIST_MegaPark.big|3593232|421e89ff426b9a549586ffc267b7b74815ff4c453f352ebb8c2cc8b9609c576a",
            "data/content/worldDIST_SkateSchool.big|18202656|19013875b0345e373c41a82a5a15a4c63a7a2e6b9601002cab567c705083c433",
            "data/content/worldDIST_StartPark.big|2581520|3f4682097fae29fa22f5c022ac0cdb6bd42cba48a7a3f323f56070c3bbc29f48",
            "data/content/worldDIST_University.big|798165184|37d6a4517bd0a5e25f493f18409edfbd3b3d74229f12f1ab6c15598aa3240091",
            "data/content/worlddmo.big|9917056|4cee5536c4eec4483697c6c6b587b3e6ddeb7e0cade49aeb99e3c9862ca31089",
            "data/content/worldmisc.big|9917056|4cee5536c4eec4483697c6c6b587b3e6ddeb7e0cade49aeb99e3c9862ca31089",
            "data/fe/subtitles/subtitles.big|22592|050a6a0f1bdfd97fca920f181328fac3b7257238f5849af52f8f5c453eb861cb",
            "data/movies/Attract_english_ntsc.vp6|185003612|c322336d2f527b2f7a0c176d204182861bedb7c9fd618af99686e2655f651ea2",
            "data/movies/Coach_frank_intro_english_ntsc.vp6|42392672|6a67054bfd5caafadaf724b3f95e8a1d1af4388b516d8eafbce464e20cd9f99d",
            "data/movies/EA_Blackbox_english_ntsc.vp6|14001720|dbd9f89b8c44d48d613b290b11da6f8a757bc3d2cfd0d36272bfefdfc11a6d6a",
            "data/movies/cameraHigh_english_ntsc.vp6|4863320|b907902f4dbde9e7553552977d8ea6dcbfc6b871a79b0a1c95879a1e0a012e8a",
            "data/movies/cameraLow_english_ntsc.vp6|4536488|65908a2747fe2f3a373fff838cb35246f17e71531f3269997d8d44a43fdaf82f",
            "data/movies/create_industrial_intro_english_ntsc.vp6|15937176|731bdf159641e4f506f7c4b9927c0e05d4bd54feebb3d8430c6b40d64b085016",
            "data/movies/create_plaza_intro_english_ntsc.vp6|16852372|1190ed2660118aa9756bc6518e3ef487da4d6961b6812b99bd6d0776da913584",
            "data/movies/create_stadium_intro_english_ntsc.vp6|16936008|fb43f87889da340e31a8cdac01336a19706f639623990266067c5a730f4dac6b",
            "data/movies/film_ender_outro_english_ntsc.vp6|78204928|e590a99875d2f941d2ff223f8b6f9fbfa597402e1b60fea9727c31946223adda",
            "data/movies/intro_movie_english_ntsc.vp6|647557372|420d80e845305835f207da54b8afaa52db06409bb913740567cf27174b122c9c",
            "data/movies/skate_park_english_ntsc.vp6|32551148|4ac328df713b51e2a7c71f73019f28f32c040890764ee1e96adcea25fd75e5d5",
            "data/webkit/EAWebkit.xex|4923392|0ee66b9558c888147f4ffdfd748cfc28950e80e46a97321da2de2afdb068523f",
            "data/webkit/EAWebkit.xexp|4096|5d4a308d2a6c768fc27c8b62ccb6661171dc504f8c0011a3e116cf0074e09438",
            "default.xex|6615040|1db39496585c521d17a2137804f42cf73ebed2b32cac166ec42dbf772f4dcf7f",
            "default.xexp|1701888|eb9ef9109dfa6d940df2e156e7eaeda4603d2b2319ca6451f324b1c27f2b1f4c",
            "nxeart|614400|3005bb39ba6c416ce90b17f2b2ae46ba08411e29948268cec9f31cd8f7687d17"
        };

        public const string XexHash = "1db39496585c521d17a2137804f42cf73ebed2b32cac166ec42dbf772f4dcf7f";
        static readonly string[] TitleUpdate = { "default.xexp|1701888|eb9ef9109dfa6d940df2e156e7eaeda4603d2b2319ca6451f324b1c27f2b1f4c",
                                                 "data/webkit/EAWebkit.xexp|4096|5d4a308d2a6c768fc27c8b62ccb6661171dc504f8c0011a3e116cf0074e09438" };

        // Files the launcher never lets a mod replace: the recompiled code is built against exactly these.
        public static readonly string[] Protected = { "default.xex", "default.xexp", "data/webkit/eawebkit.xex", "data/webkit/eawebkit.xexp" };

        public static string BaselinePath { get { return Path.Combine(Paths.UserDir, "launcher-game-baseline.txt"); } }
        public static GameCheck Last;

        class Entry { public string Rel; public long Size; public string Hash; }

        static List<Entry> Parse(IEnumerable<string> lines)
        {
            var list = new List<Entry>();
            foreach (var l in lines)
            {
                var p = l.Split('|');
                long size;
                if (p.Length == 3 && long.TryParse(p[1], out size)) list.Add(new Entry { Rel = p[0], Size = size, Hash = p[2] });
            }
            return list;
        }

        static Dictionary<string, Entry> Baseline()
        {
            var d = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            try { if (File.Exists(BaselinePath)) foreach (var e in Parse(File.ReadAllLines(BaselinePath))) d[e.Rel] = e; } catch { }
            return d;
        }

        public static bool HasBaseline { get { return File.Exists(BaselinePath); } }

        public static long ReferenceBytes { get { return Parse(Reference).Sum(e => e.Size); } }

        // Quick check: setup essentials + sizes. Full check also hashes every file (about 6 GB).
        public static GameCheck Check(string gameExe, bool full, Action<long, long, string> progress, Func<bool> cancelled)
        {
            var r = new GameCheck { Full = full, ExeFound = gameExe != null && File.Exists(gameExe) };
            if (!r.ExeFound) return r;
            string root = FileUtil.GameRoot(gameExe);
            r.GameRoot = root;
            r.FileSystem = FileUtil.DriveFormat(root);
            r.GameInstalled = File.Exists(Path.Combine(root, "default.xex"));
            r.WebkitPresent = File.Exists(Path.Combine(root, "data", "webkit", "EAWebkit.xex"));
            if (!r.GameInstalled) return r;
            try
            {
                r.XexKnown = FileUtil.Sha256(Path.Combine(root, "default.xex"), cancelled, null) == XexHash;
                r.TitleUpdate = Parse(TitleUpdate).All(e =>
                {
                    string f = Path.Combine(root, e.Rel.Replace('/', '\\'));
                    return File.Exists(f) && new FileInfo(f).Length == e.Size && FileUtil.Sha256(f, cancelled, null) == e.Hash;
                });

                var refs = Parse(Reference);
                var baseline = Baseline();
                long total = full ? refs.Sum(e => e.Size) : refs.Count, done = 0;
                foreach (var e in refs)
                {
                    if (cancelled()) { r.Cancelled = true; break; }
                    string f = Path.Combine(root, e.Rel.Replace('/', '\\'));
                    Entry b;
                    bool hasBase = baseline.TryGetValue(e.Rel, out b);
                    if (!File.Exists(f))
                    {
                        // other regions ship differently named movies/audio; only flag them when the user's own baseline had them
                        if (!IsRegional(e.Rel) || hasBase) r.Missing.Add(e.Rel);
                        done += full ? e.Size : 1;
                        continue;
                    }
                    long len = new FileInfo(f).Length;
                    r.Checked++;
                    if (!full)
                    {
                        if (len != e.Size && !(hasBase && len == b.Size)) r.Changed.Add(e.Rel + "  (size " + Paths.Size(len) + ", expected " + Paths.Size(e.Size) + ")");
                        else if (hasBase && len == b.Size && len != e.Size) r.MatchedBaseline++;
                        done++;
                        if (progress != null) progress(done, total, e.Rel);
                        continue;
                    }
                    long before = done;
                    string h = FileUtil.Sha256(f, cancelled, n => { done += n; if (progress != null) progress(done, total, e.Rel); });
                    done = before + e.Size;
                    if (h == e.Hash) continue;
                    if (hasBase && h == b.Hash) { r.MatchedBaseline++; continue; }
                    r.Changed.Add(e.Rel);
                }
                if (!r.XexKnown) r.Notes.Add("default.xex isn't the version this launcher knows (NTSC-U). The recomp is built against one specific version of the game, so other regions or revisions may not boot.");
                if (!r.TitleUpdate) r.Notes.Add("Title Update 3 isn't staged yet. The game downloads and installs it the first time it starts (it needs an internet connection once).");
                if (r.FileSystem != null && !r.FileSystem.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
                    r.Notes.Add("The game folder is on a " + r.FileSystem + " drive. The mod loader needs NTFS (it uses hard links so mods take no extra space).");
            }
            catch (OperationCanceledException) { r.Cancelled = true; }
            catch (Exception ex) { r.Error = ex.Message; }
            if (!r.Cancelled) Last = r;
            return r;
        }

        static bool IsRegional(string rel)
        {
            string r = rel.ToLowerInvariant();
            return r.StartsWith("data/movies/") || r.StartsWith("data/audio/english/") || r.StartsWith("data/audio/french/") || r.StartsWith("data/audio/german/");
        }

        // Records the hashes of the files as they are now, so a later check can tell the user's own (e.g. PAL) dump from damage.
        public static int SaveBaseline(string gameExe, Action<long, long, string> progress, Func<bool> cancelled)
        {
            string root = FileUtil.GameRoot(gameExe);
            var files = new DirectoryInfo(root).GetFiles("*", SearchOption.AllDirectories)
                .Where(f => !FileUtil.Rel(root, f.FullName).StartsWith("$")).OrderBy(f => f.FullName, StringComparer.OrdinalIgnoreCase).ToList();
            long total = files.Sum(f => f.Length), done = 0;
            var lines = new List<string> { "# Game file baseline saved by Trooper's Skate 3 Recomp Launcher " + DateTime.Now.ToString("yyyy-MM-dd HH:mm") };
            foreach (var f in files)
            {
                string rel = FileUtil.Rel(root, f.FullName);
                long before = done;
                string h = FileUtil.Sha256(f.FullName, cancelled, n => { done += n; if (progress != null) progress(done, total, rel); });
                done = before + f.Length;
                lines.Add(rel + "|" + f.Length + "|" + h);
            }
            Directory.CreateDirectory(Paths.UserDir);
            File.WriteAllLines(BaselinePath, lines);
            return files.Count;
        }
    }

    // ====================================================================== mod loader

    class ModInfo
    {
        public string Name;          // folder name under mods\ (the mod's id)
        public string Title, Author, Version, Description;
        public int Format;           // mod.json format (0 = v3.1 / unspecified, 1 = format 1)
        public string PreviewPath;   // full path to preview image next to mod.json, if present
        public string TargetRecomp = RecompFlavor.Any;   // "official", "plus", or "any"
        public string MinVersion;    // targets.min_version
        public List<string> Requires = new List<string>();
        public List<string> ConflictsWith = new List<string>();
        public string Dir, Root;     // Root = the folder whose layout mirrors game\
        public bool Enabled;
        public List<string> Files = new List<string>();   // relative paths, '/' separated
        public List<string> Skipped = new List<string>(); // protected files the mod tried to replace
        public long Bytes;

        public string Display { get { return string.IsNullOrEmpty(Title) ? Name : Title; } }
    }

    class ModPlan
    {
        public string ActiveRoot;    // folder passed to --game_data_root, or null to launch normally
        public List<string> Warnings = new List<string>();
        public string Error;
        public int Replaced, Added;
    }

    static class Mods
    {
        public const string ActiveName = ".active-game";
        static readonly string[] Docs = { "mod.json", "readme", "readme.txt", "readme.md", "license", "license.txt", "changelog.txt", "changelog.md" };

        public static string Dir(string gameExe) { return gameExe == null ? null : Path.Combine(Path.GetDirectoryName(gameExe), "mods"); }
        public static string ActiveDir(string gameExe) { return Path.Combine(Dir(gameExe), ActiveName); }
        static string ListFile(string gameExe) { return Path.Combine(Dir(gameExe), "modlist.txt"); }
        static string ManifestFile(string gameExe) { return Path.Combine(Dir(gameExe), ActiveName + ".manifest"); }

        public static bool LoaderEnabled { get { return LauncherPrefs.Bool("mods_enabled", true); } }

        // Content root of a mod folder: a "game" subfolder if there is one, otherwise the folder itself
        // (following a single wrapper folder, which is how most zips are made).
        static string ResolveRoot(string dir)
        {
            for (int depth = 0; depth < 3; depth++)
            {
                if (Directory.Exists(Path.Combine(dir, "game"))) return Path.Combine(dir, "game");
                if (Directory.Exists(Path.Combine(dir, "data")) || File.Exists(Path.Combine(dir, "mod.json"))) return dir;
                var subs = Directory.GetDirectories(dir);
                var files = Directory.GetFiles(dir).Where(f => !IsDoc(Path.GetFileName(f))).ToArray();
                if (subs.Length == 1 && files.Length == 0) { dir = subs[0]; continue; }
                break;
            }
            return dir;
        }

        static bool IsDoc(string fileName)
        {
            string n = fileName.ToLowerInvariant();
            return Docs.Contains(n) || n.StartsWith("preview.") || n.StartsWith("thumbnail.") || n.EndsWith(".md") || n.EndsWith(".txt") || n == ".base";
        }

        public static ModInfo Read(string dir)
        {
            var m = new ModInfo { Name = Path.GetFileName(dir), Dir = dir };
            m.Root = ResolveRoot(dir);
            foreach (var j in new[] { Path.Combine(dir, "mod.json"), Path.Combine(m.Root, "mod.json") })
            {
                if (!File.Exists(j)) continue;
                try
                {
                    var o = MiniJson.Parse(File.ReadAllText(j));
                    m.Title = MiniJson.S(o, "name");
                    m.Author = MiniJson.S(o, "author");
                    m.Version = MiniJson.S(o, "version");
                    m.Description = MiniJson.S(o, "description");
                    ModManifest.ReadFormat1(m, o, Path.GetDirectoryName(j));
                }
                catch { }
                break;
            }
            foreach (var f in new DirectoryInfo(m.Root).GetFiles("*", SearchOption.AllDirectories))
            {
                string rel = FileUtil.Rel(m.Root, f.FullName);
                if (!rel.Contains('/') && IsDoc(f.Name)) continue;
                if (m.PreviewPath != null && string.Equals(f.FullName, m.PreviewPath, StringComparison.OrdinalIgnoreCase)) continue;
                if (GameFiles.Protected.Contains(rel.ToLowerInvariant())) { m.Skipped.Add(rel); continue; }
                m.Files.Add(rel);
                m.Bytes += f.Length;
            }
            return m;
        }

        // Installed mods in load order (later entries win when two mods replace the same file).
        public static List<ModInfo> List(string gameExe)
        {
            var result = new List<ModInfo>();
            string dir = Dir(gameExe);
            if (dir == null || !Directory.Exists(dir)) return result;
            var folders = Directory.GetDirectories(dir).Where(d => { string n = Path.GetFileName(d); return !n.StartsWith(".") && !n.StartsWith("_") && !n.Equals("skate2", StringComparison.OrdinalIgnoreCase); })
                .ToDictionary(d => Path.GetFileName(d), StringComparer.OrdinalIgnoreCase);
            var order = new List<KeyValuePair<string, bool>>();
            try
            {
                if (File.Exists(ListFile(gameExe)))
                    foreach (var l in File.ReadAllLines(ListFile(gameExe)))
                    {
                        string t = l.Trim();
                        if (t.Length < 2 || (t[0] != '+' && t[0] != '-')) continue;
                        order.Add(new KeyValuePair<string, bool>(t.Substring(1), t[0] == '+'));
                    }
            }
            catch { }
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in order)
            {
                string path;
                if (!folders.TryGetValue(kv.Key, out path) || !seen.Add(kv.Key)) continue;
                try { var m = Read(path); m.Enabled = kv.Value; result.Add(m); } catch { }
            }
            foreach (var kv in folders.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                if (seen.Contains(kv.Key)) continue;
                try { result.Add(Read(kv.Value)); } catch { }   // new folders start disabled
            }
            return result;
        }

        public static void SaveList(string gameExe, List<ModInfo> mods)
        {
            Directory.CreateDirectory(Dir(gameExe));
            var lines = new List<string> { "# Load order for Trooper's Skate 3 Recomp Launcher. '+' = enabled, '-' = disabled. Later mods win." };
            lines.AddRange(mods.Select(m => (m.Enabled ? "+" : "-") + m.Name));
            File.WriteAllLines(ListFile(gameExe), lines);
        }

        static bool SameOrInside(string dirA, string dirB)
        {
            if (string.IsNullOrEmpty(dirA) || string.IsNullOrEmpty(dirB)) return false;
            string a = Path.GetFullPath(dirA).TrimEnd('\\', '/');
            string b = Path.GetFullPath(dirB).TrimEnd('\\', '/');
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase) || FileUtil.Inside(a, b);
        }

        // Copies a mod (a .zip file or a folder) into mods\<name>. Returns the new mod's folder name.
        public static string Install(string gameExe, string source)
        {
            string modsDir = Dir(gameExe);
            if (Directory.Exists(source))
            {
                string srcFull = Path.GetFullPath(source);
                string exeDir = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(gameExe)));
                string gameDir = Path.GetFullPath(FileUtil.GameRoot(gameExe));
                string fullModsDir = Path.GetFullPath(modsDir);
                if (SameOrInside(fullModsDir, srcFull) ||
                    SameOrInside(srcFull, fullModsDir) ||
                    SameOrInside(srcFull, gameDir) ||
                    SameOrInside(srcFull, exeDir))
                    throw new InvalidOperationException("That's the game's own folder, not a mod.");
            }
            Directory.CreateDirectory(modsDir);
            string baseName = Paths.SafeName(Path.GetFileNameWithoutExtension(source.TrimEnd('\\')));
            if (baseName.Length == 0 || baseName.StartsWith(".") || baseName.StartsWith("_") || baseName.Equals("skate2", StringComparison.OrdinalIgnoreCase)) baseName = "mod" + baseName;
            string name = baseName;
            for (int i = 2; Directory.Exists(Path.Combine(modsDir, name)); i++) name = baseName + " (" + i + ")";
            string dest = Path.Combine(modsDir, name);
            string tmp = Path.Combine(modsDir, "_installing-" + Guid.NewGuid().ToString("N"));
            try
            {
                if (Directory.Exists(source)) CopyDir(source, tmp);
                else
                {
                    using (var zip = new ZipArchive(new FileStream(source, FileMode.Open, FileAccess.Read), ZipArchiveMode.Read))
                        foreach (var e in zip.Entries)
                        {
                            if (e.Name.Length == 0) continue;
                            string target = Path.Combine(tmp, e.FullName.Replace('/', '\\'));
                            if (!FileUtil.Inside(tmp, target)) continue;
                            using (var s = e.Open()) FileUtil.CopyStreamToFile(s, target);
                        }
                }
                if (!Directory.Exists(tmp) || Directory.GetFiles(tmp, "*", SearchOption.AllDirectories).Length == 0)
                    throw new InvalidDataException("There are no files in that mod.");
                Directory.Move(tmp, dest);
            }
            catch { FileUtil.TryDeleteDir(tmp); throw; }

            var list = List(gameExe);
            var added = list.FirstOrDefault(m => m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (added != null)
            {
                if (added.Files.Count == 0) { FileUtil.TryDeleteDir(dest); throw new InvalidDataException("That mod doesn't contain any game files the loader can use" + (added.Skipped.Count > 0 ? " (it only replaces default.xex/EAWebkit, which the recomp can't use)." : ".")); }
                list.Remove(added);
                added.Enabled = true;
                list.Add(added);
                SaveList(gameExe, list);
            }
            return name;
        }

        static void CopyDir(string src, string dst)
        {
            Directory.CreateDirectory(dst);
            foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
            {
                string target = Path.Combine(dst, f.Substring(src.TrimEnd('\\').Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(f, target, true);
            }
        }

        public static void Remove(string gameExe, ModInfo m)
        {
            Directory.Delete(m.Dir, true);
            var list = List(gameExe);
            SaveList(gameExe, list);
        }

        // rel path (lower case) -> mods that ship it, in load order
        public static Dictionary<string, List<ModInfo>> Conflicts(IEnumerable<ModInfo> enabled)
        {
            var d = new Dictionary<string, List<ModInfo>>(StringComparer.OrdinalIgnoreCase);
            foreach (var m in enabled)
                foreach (var f in m.Files)
                {
                    List<ModInfo> l;
                    if (!d.TryGetValue(f, out l)) d[f] = l = new List<ModInfo>();
                    l.Add(m);
                }
            return d.Where(kv => kv.Value.Count > 1).ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        }

        // ---- "base file changed" tracking: when a mod is enabled the size/time of each game file it replaces is recorded
        static string BasePath(ModInfo m) { return Path.Combine(m.Dir, ".base"); }

        public static void RecordBase(string gameExe, ModInfo m)
        {
            string root = FileUtil.GameRoot(gameExe);
            var lines = new List<string>();
            foreach (var rel in m.Files)
            {
                string f = Path.Combine(root, rel.Replace('/', '\\'));
                if (File.Exists(f)) { var fi = new FileInfo(f); lines.Add(rel + "|" + fi.Length + "|" + fi.LastWriteTimeUtc.Ticks); }
            }
            try { File.WriteAllLines(BasePath(m), lines); } catch { }
        }

        public static List<string> ChangedBase(string gameExe, ModInfo m)
        {
            var changed = new List<string>();
            string root = FileUtil.GameRoot(gameExe);
            if (!File.Exists(BasePath(m))) { RecordBase(gameExe, m); return changed; }
            try
            {
                foreach (var l in File.ReadAllLines(BasePath(m)))
                {
                    var p = l.Split('|');
                    if (p.Length != 3) continue;
                    string f = Path.Combine(root, p[0].Replace('/', '\\'));
                    if (!File.Exists(f)) { changed.Add(p[0]); continue; }
                    var fi = new FileInfo(f);
                    if (fi.Length.ToString(CultureInfo.InvariantCulture) != p[1] || fi.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture) != p[2]) changed.Add(p[0]);
                }
            }
            catch { }
            return changed;
        }

        // ---- launch: builds mods\.active-game (hard links to the game plus the mods' files) and returns it
        public static ModPlan Prepare(string gameExe)
        {
            var plan = new ModPlan();
            if (gameExe == null) return plan;
            List<ModInfo> enabled;
            try { enabled = LoaderEnabled ? List(gameExe).Where(m => m.Enabled && m.Files.Count > 0).ToList() : new List<ModInfo>(); }
            catch (Exception ex) { plan.Warnings.Add("Couldn't read the mods folder: " + ex.Message); enabled = new List<ModInfo>(); }
            if (enabled.Count == 0) { CleanActive(gameExe); return plan; }

            string root = FileUtil.GameRoot(gameExe);
            if (!File.Exists(Path.Combine(root, "default.xex")))
            {
                plan.Warnings.Add("Mods are off for this launch: the game files aren't installed yet. Start the game once to install them from your ISO.");
                return plan;
            }
            if (!File.Exists(Path.Combine(root, "default.xexp")))
            {
                plan.Warnings.Add("Mods are off for this launch: Title Update 3 isn't installed yet. Start the game once without mods so it can install it.");
                return plan;
            }
            string fs = FileUtil.DriveFormat(root);
            if (fs != null && !fs.Equals("NTFS", StringComparison.OrdinalIgnoreCase))
            {
                plan.Error = "Mods need the game folder to be on an NTFS drive (this one is " + fs + "). Move the Skate 3 Recomp folder to an NTFS drive, or turn mods off on the Mods page.";
                return plan;
            }

            // what goes where: start from the game, then each mod in load order
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in new DirectoryInfo(root).GetFiles("*", SearchOption.AllDirectories)) map[FileUtil.Rel(root, f.FullName)] = f.FullName;
            foreach (var m in enabled)
            {
                foreach (var rel in m.Files)
                {
                    if (map.ContainsKey(rel)) plan.Replaced++; else plan.Added++;
                    map[rel] = Path.Combine(m.Root, rel.Replace('/', '\\'));
                }
                var changedBase = ChangedBase(gameExe, m);
                foreach (var c in changedBase.Take(3))
                    plan.Warnings.Add(m.Display + ": the game file " + c + " changed since this mod was enabled (game or recomp update?). If something looks wrong, check for a newer version of the mod.");
                if (changedBase.Count > 0) RecordBase(gameExe, m);   // warn once, not on every launch
            }

            string active = ActiveDir(gameExe);
            string manifest = ManifestFile(gameExe);
            var wanted = map.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase).Select(k => k.Key + "|" + k.Value + "|" + SafeLen(k.Value)).ToList();
            try
            {
                if (Directory.Exists(active) && File.Exists(manifest) && File.ReadAllLines(manifest).SequenceEqual(wanted) && AllPresent(active, map))
                {
                    plan.ActiveRoot = active;
                    return plan;   // nothing changed since last time
                }
                Salvage(gameExe, root, active, manifest);
                if (Directory.Exists(active)) Directory.Delete(active, true);   // removes links only, never the originals
                foreach (var kv in map)
                {
                    string dest = Path.Combine(active, kv.Key.Replace('/', '\\'));
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    int err = FileUtil.HardLink(dest, kv.Value);
                    if (err != 0)
                    {
                        if (err == 17 || err == 1 || err == 50)
                        {
                            plan.Error = "Windows couldn't create hard links in " + Dir(gameExe) + " (error " + err + "). The mods folder must be on the same NTFS drive as the game.";
                            FileUtil.TryDeleteDir(active);
                            return plan;
                        }
                        throw new IOException("Couldn't link " + kv.Key + " (Windows error " + err + ").");
                    }
                }
                File.WriteAllLines(manifest, wanted);
                plan.ActiveRoot = active;
            }
            catch (Exception ex)
            {
                plan.Error = "Couldn't prepare the modded game folder: " + ex.Message;
                FileUtil.TryDeleteDir(active);
            }
            return plan;
        }

        static string SafeLen(string f) { try { return new FileInfo(f).Length.ToString(CultureInfo.InvariantCulture); } catch { return "0"; } }

        static bool AllPresent(string active, Dictionary<string, string> map)
        {
            foreach (var kv in map) if (!File.Exists(Path.Combine(active, kv.Key.Replace('/', '\\')))) return false;
            return true;
        }

        // Anything the game wrote into the modded folder (it shouldn't, but e.g. a title update install would) goes back into game\.
        static void Salvage(string gameExe, string root, string active, string manifest)
        {
            if (!Directory.Exists(active)) return;
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try { if (File.Exists(manifest)) foreach (var l in File.ReadAllLines(manifest)) { int i = l.IndexOf('|'); if (i > 0) known.Add(l.Substring(0, i)); } } catch { }
            foreach (var f in Directory.GetFiles(active, "*", SearchOption.AllDirectories))
            {
                string rel = FileUtil.Rel(active, f);
                if (known.Contains(rel)) continue;
                string dest = Path.Combine(root, rel.Replace('/', '\\'));
                try { if (!File.Exists(dest)) { Directory.CreateDirectory(Path.GetDirectoryName(dest)); File.Move(f, dest); } } catch { }
            }
        }

        public static void CleanActive(string gameExe)
        {
            try
            {
                string active = ActiveDir(gameExe);
                if (!Directory.Exists(active)) return;
                Salvage(gameExe, FileUtil.GameRoot(gameExe), active, ManifestFile(gameExe));
                Directory.Delete(active, true);
                if (File.Exists(ManifestFile(gameExe))) File.Delete(ManifestFile(gameExe));
            }
            catch { }
        }

        // The recomp could save game_data_root into settings.toml; make sure a normal launch never points at the modded folder.
        public static bool ScrubConfig(string configPath)
        {
            try
            {
                if (!File.Exists(configPath)) return false;
                var lines = File.ReadAllLines(configPath).ToList();
                int n = lines.RemoveAll(l => Regex.IsMatch(l, @"^\s*game_data_root\s*=") && l.IndexOf(ActiveName, StringComparison.OrdinalIgnoreCase) >= 0);
                if (n > 0) File.WriteAllLines(configPath, lines);
                return n > 0;
            }
            catch { return false; }
        }
    }

    // ====================================================================== starting the game (shared by PLAY, quick play and the benchmark)

    static class GameLaunch
    {
        // Step 1: build the modded game folder (if any mods are on). Show plan.Warnings to the user before Start.
        public static ModPlan Prepare(string gameExe) { return Mods.Prepare(gameExe); }

        // Step 2: start skate3.exe, pointed at the modded folder when there is one. Throws if the mod loader failed.
        public static Process Start(string gameExe, ModPlan plan)
        {
            if (plan != null && plan.Error != null) throw new InvalidOperationException(plan.Error);
            if (plan == null || plan.ActiveRoot == null) Mods.ScrubConfig(Paths.Config);
            var psi = new ProcessStartInfo(gameExe) { WorkingDirectory = Path.GetDirectoryName(gameExe), UseShellExecute = true };
            if (plan != null && plan.ActiveRoot != null) psi.Arguments = "--game_data_root \"" + plan.ActiveRoot + "\"";
            return Process.Start(psi);
        }
    }

    // ====================================================================== benchmark

    class BenchResult
    {
        public DateTime When;
        public string Label, Route, Settings, Gpu, Source;
        public double AvgMs, MaxMs, Jitter, SdMs;
        public int Frames;

        public double Fps { get { return AvgMs > 0 ? 1000.0 / AvgMs : 0; } }

        static string Clean(string s) { return (s ?? "").Replace(",", ";").Replace("\n", " "); }

        public string ToCsv()
        {
            var c = CultureInfo.InvariantCulture;
            return string.Join(",", new[]
            {
                When.ToString("o", c), Clean(Label), Clean(Route), AvgMs.ToString("0.###", c), MaxMs.ToString("0.###", c),
                Jitter.ToString("0.####", c), SdMs.ToString("0.###", c), Frames.ToString(c), Clean(Settings), Clean(Gpu), Clean(Source)
            });
        }

        public static BenchResult FromCsv(string line)
        {
            var p = line.Split(',');
            if (p.Length < 11) return null;
            var c = CultureInfo.InvariantCulture;
            var r = new BenchResult { Label = p[1], Route = p[2], Settings = p[8], Gpu = p[9], Source = p[10] };
            if (!DateTime.TryParse(p[0], c, DateTimeStyles.RoundtripKind, out r.When)) return null;
            double.TryParse(p[3], NumberStyles.Float, c, out r.AvgMs);
            double.TryParse(p[4], NumberStyles.Float, c, out r.MaxMs);
            double.TryParse(p[5], NumberStyles.Float, c, out r.Jitter);
            double.TryParse(p[6], NumberStyles.Float, c, out r.SdMs);
            int.TryParse(p[7], NumberStyles.Integer, c, out r.Frames);
            return r;
        }
    }

    static class Benchmarks
    {
        public static string ResultsPath { get { return Path.Combine(Paths.UserDir, "launcher-benchmarks.csv"); } }
        public static string ConfigBackup { get { return Paths.Config + ".benchmark-backup"; } }

        public static List<BenchResult> Load()
        {
            var list = new List<BenchResult>();
            try
            {
                if (File.Exists(ResultsPath))
                    foreach (var l in File.ReadAllLines(ResultsPath).Skip(1)) { var r = BenchResult.FromCsv(l); if (r != null) list.Add(r); }
            }
            catch { }
            return list.OrderByDescending(r => r.When).ToList();
        }

        public static void Append(BenchResult r)
        {
            Directory.CreateDirectory(Paths.UserDir);
            if (!File.Exists(ResultsPath)) File.WriteAllText(ResultsPath, "when,label,route,avg_ms,max_ms,jitter,sd_ms,frames,settings,gpu,source\n");
            File.AppendAllText(ResultsPath, r.ToCsv() + "\n");
        }

        public static void SaveAll(List<BenchResult> list)
        {
            var lines = new List<string> { "when,label,route,avg_ms,max_ms,jitter,sd_ms,frames,settings,gpu,source" };
            lines.AddRange(list.OrderBy(r => r.When).Select(r => r.ToCsv()));
            File.WriteAllLines(ResultsPath, lines);
        }

        // Marks a backup taken when there was no settings.toml at all, so restoring deletes the file again
        // (an empty settings.toml would stop the recomp from applying its first-run defaults).
        const string NoFileMarker = "# launcher: settings.toml did not exist before the benchmark";

        static void BackupConfig()
        {
            if (File.Exists(Paths.Config)) File.Copy(Paths.Config, ConfigBackup, true);
            else File.WriteAllText(ConfigBackup, NoFileMarker + "\n");
        }

        static void RestoreConfig()
        {
            if (!File.Exists(ConfigBackup)) return;
            if (File.ReadAllText(ConfigBackup).StartsWith(NoFileMarker)) { if (File.Exists(Paths.Config)) File.Delete(Paths.Config); }
            else File.Copy(ConfigBackup, Paths.Config, true);
            File.Delete(ConfigBackup);
        }

        // If the launcher died mid-benchmark, put the user's real settings back.
        public static bool RecoverConfig()
        {
            try
            {
                if (!File.Exists(ConfigBackup) || MainForm.IsGameRunning()) return false;
                RestoreConfig();
                return true;
            }
            catch { return false; }
        }

        static readonly Regex TimeRx = new Regex(@"^\[(\d{4}-\d\d-\d\d \d\d:\d\d:\d\d\.\d+)\]");
        static readonly Regex CadenceRx = new Regex(@"present-cadence: avg=([\d.]+)ms sd=([\d.]+) jitter=([\d.]+) max=([\d.]+)");
        static readonly Regex GuestRx = new Regex(@"native-scene perf: guest_fps=([\d.]+) guest_dt_max=([\d.]+)ms");

        static DateTime Stamp(string line)
        {
            var m = TimeRx.Match(line);
            DateTime t;
            return m.Success && DateTime.TryParseExact(m.Groups[1].Value, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture, DateTimeStyles.None, out t) ? t : DateTime.MinValue;
        }

        // Runs one benchmark on a worker thread. 'status' and the return value are delivered from that thread.
        // windows = number of 600-frame windows to average.
        public static BenchResult Run(string gameExe, Dictionary<string, string> overrides, string label, string route, string inputs, int windows,
                                      string settingsSummary, string gpu, Action<string> status, Func<bool> cancelled)
        {
            string logsDir = Paths.LogsDir(gameExe);
            BackupConfig();
            Process p = null;
            DateTime started = DateTime.Now;
            try
            {
                var cfg = new ConfigFile(Paths.Config);
                cfg.Load();
                var bench = new Dictionary<string, string>(overrides);
                bench["skate3_demo_path"] = "true";
                bench["skate3_demo_path_signed_in"] = "false";            // signed-out boot: the benchmark never touches your career save
                bench["skate3_demo_path_gameplay_inputs"] = Toml.Str(inputs ?? "");
                bench["skate3_intro_movie_skip"] = "true";
                bench["presenter_present_cadence_log"] = "true";          // the recomp logs frame pacing every 600 frames
                bench["skate3_native_render_scene_perf_log"] = "true";    // fallback source: guest fps from the native renderer
                cfg.Save(bench);

                started = DateTime.Now;
                status("Starting Skate 3…");
                p = GameLaunch.Start(gameExe, GameLaunch.Prepare(gameExe));
                if (p == null) throw new InvalidOperationException("The game didn't start.");

                bool wantInputs = !string.IsNullOrWhiteSpace(inputs);
                DateTime marker = DateTime.MinValue;
                var cadence = new List<double[]>();
                var guest = new List<double[]>();
                DateTime deadline = DateTime.Now.AddMinutes(4);
                const int settleSeconds = 8;
                while (true)
                {
                    if (cancelled()) throw new OperationCanceledException();
                    p.Refresh();
                    if (p.HasExited) throw new InvalidOperationException("Skate 3 closed during the benchmark (exit code " + SafeExit(p) + "). Check the Logs page.");
                    if (DateTime.Now > deadline) throw new TimeoutException(marker == DateTime.MinValue ? "The game didn't reach gameplay within 4 minutes." : "Not enough frame data was logged in time.");
                    System.Threading.Thread.Sleep(1000);

                    var lines = new List<string>();
                    foreach (var f in LogTools.Files(logsDir).Where(f => f.LastWriteTime >= started.AddSeconds(-2)).OrderBy(f => f.LastWriteTimeUtc))
                        try { lines.AddRange(LogTools.ReadAll(f.FullName, 0)); } catch { }
                    lines = lines.Where(l => Stamp(l) >= started.AddSeconds(-1)).ToList();

                    if (marker == DateTime.MinValue)
                    {
                        var hit = lines.FirstOrDefault(l => wantInputs ? l.Contains("demo path: gameplay input sequence complete") :
                                                              (l.Contains("boot automation complete") || l.Contains("demo path: gameplay reached")));
                        if (hit == null)
                        {
                            var bad = lines.FirstOrDefault(l => l.Contains("sequence disabled"));
                            if (bad != null) throw new InvalidDataException("The game rejected the route's inputs: " + bad.Substring(Math.Max(0, bad.IndexOf("demo path"))));
                            status(lines.Any(l => l.Contains("demo path")) ? "Booting to gameplay (the game is pressing Start for you)…" : "Waiting for the game to boot…");
                            continue;
                        }
                        marker = Stamp(hit);
                        status("In gameplay. Letting the scene settle…");
                        continue;
                    }

                    DateTime from = marker.AddSeconds(settleSeconds);
                    cadence = lines.Where(l => Stamp(l) > from).Select(l => CadenceRx.Match(l)).Where(m => m.Success)
                        .Select(m => new[] { D(m, 1), D(m, 2), D(m, 3), D(m, 4) }).ToList();
                    guest = lines.Where(l => Stamp(l) > from).Select(l => GuestRx.Match(l)).Where(m => m.Success)
                        .Select(m => new[] { D(m, 1), D(m, 2) }).ToList();
                    if (cadence.Count >= windows) break;
                    if (cadence.Count == 0 && guest.Count >= windows * 3 && (DateTime.Now - from).TotalSeconds > 45) break;   // no cadence log on this backend
                    if (DateTime.Now > from.AddSeconds(30 + windows * 20) && (cadence.Count > 0 || guest.Count > 0)) break;
                    status("Measuring… " + Math.Min(cadence.Count, windows) + " of " + windows + " samples (600 frames each)");
                }

                var r = new BenchResult { When = DateTime.Now, Label = label, Route = route, Settings = settingsSummary, Gpu = gpu };
                if (cadence.Count > 0)
                {
                    var use = cadence.Take(windows).ToList();
                    r.AvgMs = use.Average(x => x[0]);
                    r.SdMs = use.Average(x => x[1]);
                    r.Jitter = use.Average(x => x[2]);
                    r.MaxMs = use.Max(x => x[3]);
                    r.Frames = use.Count * 600;
                    r.Source = "present cadence";
                }
                else
                {
                    r.AvgMs = 1000.0 / Math.Max(1, guest.Average(x => x[0]));
                    r.MaxMs = guest.Max(x => x[1]);
                    r.Frames = 0;
                    r.Source = "native renderer guest fps";
                }
                Append(r);
                return r;
            }
            finally
            {
                status("Closing the game and restoring your settings…");
                StopGame(p);
                MarkBenchmarkLog(logsDir, started);
                try { RestoreConfig(); } catch { }
            }
        }

        static double D(Match m, int g) { return double.Parse(m.Groups[g].Value, CultureInfo.InvariantCulture); }

        // Remembers which log the benchmark session wrote, so the Logs page doesn't report the launcher closing the game as a crash.
        static void MarkBenchmarkLog(string logsDir, DateTime started)
        {
            try
            {
                var f = LogTools.Files(logsDir).FirstOrDefault(x => x.LastWriteTime >= started.AddSeconds(-2));
                if (f != null) LauncherPrefs.Write("bench_log", f.Name + "|" + f.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture));
            }
            catch { }
        }

        public static bool IsBenchmarkLog(FileInfo f)
        {
            if (f == null) return false;
            string v = LauncherPrefs.Read("bench_log");
            return v != null && v == f.Name + "|" + f.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture);
        }

        static string SafeExit(Process p) { try { return "0x" + p.ExitCode.ToString("X8"); } catch { return "?"; } }

        static void StopGame(Process p)
        {
            if (p == null) return;
            try
            {
                p.Refresh();
                if (!p.HasExited)
                {
                    p.CloseMainWindow();
                    if (!p.WaitForExit(6000)) { p.Kill(); p.WaitForExit(5000); }
                }
            }
            catch { }
            try { p.Dispose(); } catch { }
        }
    }

    // ====================================================================== Steam (non-Steam game shortcut)

    // Binary KeyValues node as used by Steam's shortcuts.vdf.
    class KvNode
    {
        public string Key;
        public byte Type;            // 0 = map, 1 = string, 2 = int32
        public string Str;
        public int Int;
        public List<KvNode> Children = new List<KvNode>();
        public byte[] Raw;           // other value types, kept verbatim

        public KvNode Child(string k) { return Children.FirstOrDefault(c => c.Key.Equals(k, StringComparison.OrdinalIgnoreCase)); }
        public string S(string k) { var c = Child(k); return c != null ? c.Str : null; }

        public void Set(string k, string v) { var c = Child(k); if (c == null) Children.Add(c = new KvNode { Key = k, Type = 1 }); c.Type = 1; c.Str = v; }
        public void Set(string k, int v) { var c = Child(k); if (c == null) Children.Add(c = new KvNode { Key = k, Type = 2 }); c.Type = 2; c.Int = v; }
    }

    static class BinaryVdf
    {
        public static KvNode Read(byte[] data)
        {
            int i = 0;
            var root = new KvNode { Key = "", Type = 0 };
            ReadMap(data, ref i, root);
            return root;
        }

        static string CStr(byte[] d, ref int i)
        {
            int s = i;
            while (d[i] != 0) i++;
            string r = Encoding.UTF8.GetString(d, s, i - s);
            i++;
            return r;
        }

        static void ReadMap(byte[] d, ref int i, KvNode node)
        {
            while (i < d.Length)
            {
                byte t = d[i++];
                if (t == 8) return;
                var c = new KvNode { Type = t, Key = CStr(d, ref i) };
                switch (t)
                {
                    case 0: ReadMap(d, ref i, c); break;
                    case 1: c.Str = CStr(d, ref i); break;
                    case 2: case 3: case 4: case 6: c.Int = BitConverter.ToInt32(d, i); c.Raw = new byte[4]; Array.Copy(d, i, c.Raw, 0, 4); i += 4; break;
                    case 7: case 10: c.Raw = new byte[8]; Array.Copy(d, i, c.Raw, 0, 8); i += 8; break;
                    default: throw new InvalidDataException("Unknown value type " + t + " in shortcuts.vdf.");
                }
                node.Children.Add(c);
            }
        }

        public static byte[] Write(KvNode root)
        {
            using (var ms = new MemoryStream())
            {
                foreach (var c in root.Children) WriteNode(ms, c);
                ms.WriteByte(8);
                return ms.ToArray();
            }
        }

        static void WriteCStr(Stream s, string v) { var b = Encoding.UTF8.GetBytes(v ?? ""); s.Write(b, 0, b.Length); s.WriteByte(0); }

        static void WriteNode(Stream s, KvNode n)
        {
            s.WriteByte(n.Type);
            WriteCStr(s, n.Key);
            if (n.Type == 0) { foreach (var c in n.Children) WriteNode(s, c); s.WriteByte(8); }
            else if (n.Type == 1) WriteCStr(s, n.Str);
            else if (n.Type == 2) s.Write(BitConverter.GetBytes(n.Int), 0, 4);
            else s.Write(n.Raw, 0, n.Raw.Length);
        }
    }

    static class SteamShortcut
    {
        public const string AppName = "Skate 3 (Recomp)";

        public static string SteamDir()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                {
                    string p = k != null ? k.GetValue("SteamPath") as string : null;
                    if (!string.IsNullOrEmpty(p)) { p = p.Replace('/', '\\'); if (Directory.Exists(p)) return p; }
                }
            }
            catch { }
            foreach (var p in new[] { @"C:\Program Files (x86)\Steam", @"C:\Program Files\Steam" }) if (Directory.Exists(p)) return p;
            return null;
        }

        // The Steam account folder used most recently (userdata\<id>).
        public static string UserDir()
        {
            string steam = SteamDir();
            if (steam == null) return null;
            string ud = Path.Combine(steam, "userdata");
            if (!Directory.Exists(ud)) return null;
            return Directory.GetDirectories(ud).Where(d => Regex.IsMatch(Path.GetFileName(d), @"^\d+$") && Path.GetFileName(d) != "0")
                .OrderByDescending(d =>
                {
                    string lc = Path.Combine(d, "config", "localconfig.vdf");
                    return File.Exists(lc) ? File.GetLastWriteTimeUtc(lc) : Directory.GetLastWriteTimeUtc(d);
                }).FirstOrDefault();
        }

        public static bool SteamRunning()
        {
            try { var ps = Process.GetProcessesByName("steam"); bool any = ps.Length > 0; foreach (var p in ps) p.Dispose(); return any; }
            catch { return false; }
        }

        static uint Crc32(byte[] data)
        {
            uint crc = 0xFFFFFFFF;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
            }
            return ~crc;
        }

        // The id Steam gives a non-Steam shortcut (used for its artwork file names).
        public static uint AppId(string exeQuoted, string name) { return Crc32(Encoding.UTF8.GetBytes(exeQuoted + name)) | 0x80000000; }

        public static bool Exists(string launcherExe)
        {
            try
            {
                string user = UserDir();
                if (user == null) return false;
                string vdf = Path.Combine(user, "config", "shortcuts.vdf");
                if (!File.Exists(vdf)) return false;
                var list = BinaryVdf.Read(File.ReadAllBytes(vdf)).Child("shortcuts");
                return list != null && list.Children.Any(c => (c.S("Exe") ?? c.S("exe") ?? "").Trim('"').Equals(launcherExe, StringComparison.OrdinalIgnoreCase));
            }
            catch { return false; }
        }

        // Adds (or updates) the shortcut. Returns the app id. Steam must be closed or it overwrites the file on exit.
        public static uint Add(string launcherExe, string launchOptions, string iconPath)
        {
            string user = UserDir();
            if (user == null) throw new InvalidOperationException("Couldn't find a Steam account on this PC. Sign in to Steam once, then try again.");
            string cfgDir = Path.Combine(user, "config");
            Directory.CreateDirectory(cfgDir);
            string vdf = Path.Combine(cfgDir, "shortcuts.vdf");
            KvNode root = File.Exists(vdf) ? BinaryVdf.Read(File.ReadAllBytes(vdf)) : new KvNode { Type = 0, Key = "" };
            var list = root.Child("shortcuts");
            if (list == null) root.Children.Add(list = new KvNode { Type = 0, Key = "shortcuts" });

            string exeQ = "\"" + launcherExe + "\"";
            var entry = list.Children.FirstOrDefault(c => (c.S("Exe") ?? c.S("exe") ?? "").Trim('"').Equals(launcherExe, StringComparison.OrdinalIgnoreCase));
            if (entry == null)
            {
                int next = list.Children.Select(c => { int n; return int.TryParse(c.Key, out n) ? n + 1 : 0; }).DefaultIfEmpty(0).Max();
                entry = new KvNode { Type = 0, Key = next.ToString(CultureInfo.InvariantCulture) };
                list.Children.Add(entry);
            }
            uint id = AppId(exeQ, AppName);
            entry.Set("appid", unchecked((int)id));
            entry.Set("AppName", AppName);
            entry.Set("Exe", exeQ);
            entry.Set("StartDir", "\"" + Path.GetDirectoryName(launcherExe) + "\"");
            entry.Set("icon", iconPath ?? "");
            entry.Set("ShortcutPath", "");
            entry.Set("LaunchOptions", launchOptions ?? "");
            if (entry.Child("IsHidden") == null) entry.Set("IsHidden", 0);
            entry.Set("AllowDesktopConfig", 1);
            entry.Set("AllowOverlay", 1);
            if (entry.Child("OpenVR") == null) entry.Set("OpenVR", 0);
            if (entry.Child("LastPlayTime") == null) entry.Set("LastPlayTime", 0);
            if (entry.Child("tags") == null) entry.Children.Add(new KvNode { Type = 0, Key = "tags" });

            if (File.Exists(vdf)) File.Copy(vdf, vdf + ".launcher-backup", true);
            File.WriteAllBytes(vdf, BinaryVdf.Write(root));
            return id;
        }

        // Copies the user's own artwork into Steam's grid folder under the names Steam expects.
        // Picks the slot from the image shape: tall = library capsule, very wide = hero, wide = grid, otherwise logo.
        public static List<string> SetArtwork(uint appId, IEnumerable<string> images)
        {
            string user = UserDir();
            if (user == null) throw new InvalidOperationException("Couldn't find a Steam account on this PC.");
            string grid = Path.Combine(user, "config", "grid");
            Directory.CreateDirectory(grid);
            var done = new List<string>();
            foreach (var img in images)
            {
                double ratio;
                using (var bmp = System.Drawing.Image.FromFile(img)) ratio = bmp.Width / (double)Math.Max(1, bmp.Height);
                string slot, suffix;
                if (ratio < 0.85) { slot = "library capsule"; suffix = "p"; }
                else if (ratio >= 2.6) { slot = "hero"; suffix = "_hero"; }
                else if (ratio >= 1.6) { slot = "grid"; suffix = ""; }
                else { slot = "logo"; suffix = "_logo"; }
                string ext = Path.GetExtension(img).ToLowerInvariant();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg") ext = ".png";
                foreach (var old in Directory.GetFiles(grid, appId + suffix + ".*")) try { File.Delete(old); } catch { }
                File.Copy(img, Path.Combine(grid, appId + suffix + (ext == ".jpeg" ? ".jpg" : ext)), true);
                done.Add(slot);
            }
            return done;
        }
    }

    // ====================================================================== graphics driver check

    class DriverInfo
    {
        public string Gpu, Provider, Version, VulkanDriver, Vendor;
        public DateTime Date;
        public List<string> Warnings = new List<string>();
        public string DownloadUrl;
    }

    static class DriverCheck
    {
        const string DisplayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
        public const string KnownIssuesUrl = "https://raw.githubusercontent.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/main/known-driver-issues.txt";

        class Adapter { public string Desc, Provider, Version; public DateTime Date; }

        static List<Adapter> Adapters()
        {
            var list = new List<Adapter>();
            try
            {
                using (var cls = Registry.LocalMachine.OpenSubKey(DisplayClass))
                {
                    if (cls == null) return list;
                    foreach (var name in cls.GetSubKeyNames())
                    {
                        if (!Regex.IsMatch(name, @"^\d{4}$")) continue;
                        try
                        {
                            using (var k = cls.OpenSubKey(name))
                            {
                                if (k == null) continue;
                                var a = new Adapter { Desc = k.GetValue("DriverDesc") as string, Provider = k.GetValue("ProviderName") as string, Version = k.GetValue("DriverVersion") as string };
                                if (a.Desc == null) continue;
                                DateTime d;
                                string ds = k.GetValue("DriverDate") as string;
                                if (ds != null && DateTime.TryParseExact(ds, new[] { "M-d-yyyy", "MM-dd-yyyy" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out d)) a.Date = d;
                                list.Add(a);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return list;
        }

        // gpuName: the GPU the game uses (from the Vulkan list). logsDir gives the Vulkan driver string the game logged.
        public static DriverInfo Inspect(string gpuName, string logsDir)
        {
            var info = new DriverInfo { Gpu = gpuName };
            var adapters = Adapters();
            Adapter a = null;
            if (gpuName != null) a = adapters.FirstOrDefault(x => Norm(x.Desc) == Norm(gpuName)) ?? adapters.FirstOrDefault(x => Norm(gpuName).Contains(Norm(x.Desc)) || Norm(x.Desc).Contains(Norm(gpuName)));
            if (a == null) a = adapters.FirstOrDefault(x => x.Desc.IndexOf("Microsoft Basic", StringComparison.OrdinalIgnoreCase) < 0);
            if (a != null)
            {
                if (info.Gpu == null) info.Gpu = a.Desc;
                info.Provider = a.Provider;
                info.Version = a.Version;
                info.Date = a.Date;
            }
            try
            {
                foreach (var f in LogTools.Files(logsDir).Take(3))
                {
                    var l = LogTools.ReadAll(f.FullName, 0).Take(120).FirstOrDefault(x => x.Contains("* driverInfo:"));
                    if (l != null) { info.VulkanDriver = l.Substring(l.IndexOf("driverInfo:") + 11).Trim(); break; }
                }
            }
            catch { }

            string g = (info.Gpu ?? "") + " " + (info.Provider ?? "");
            if (g.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) { info.Vendor = "NVIDIA"; info.DownloadUrl = "https://www.nvidia.com/en-us/drivers/"; }
            else if (g.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 || g.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0 || g.IndexOf("Advanced Micro", StringComparison.OrdinalIgnoreCase) >= 0)
            { info.Vendor = "AMD"; info.DownloadUrl = "https://www.amd.com/en/support/download/drivers.html"; }
            else if (g.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0) { info.Vendor = "Intel"; info.DownloadUrl = "https://www.intel.com/content/www/us/en/download-center/home.html"; }

            if (info.Date != DateTime.MinValue && (DateTime.Now - info.Date).TotalDays > 365)
                info.Warnings.Add("This driver is from " + info.Date.ToString("MMMM yyyy", CultureInfo.InvariantCulture) + ", over a year old. The recomp leans on recent Vulkan and Direct3D 12 features, so updating is the first thing to try if you see glitches or crashes.");
            return info;
        }

        static string Norm(string s) { return Regex.Replace((s ?? "").ToLowerInvariant().Replace("(tm)", "").Replace("(r)", ""), @"\s+", " ").Trim(); }

        // Lines in known-driver-issues.txt:  vendor | text that appears in the driver version | what goes wrong
        public static List<string> KnownIssues(DriverInfo info, out string error)
        {
            error = null;
            var hits = new List<string>();
            try
            {
                string text = Http.GetString(KnownIssuesUrl, null);
                foreach (var raw in text.Split('\n'))
                {
                    string l = raw.Trim();
                    if (l.Length == 0 || l.StartsWith("#")) continue;
                    var p = l.Split('|').Select(x => x.Trim()).ToArray();
                    if (p.Length < 3 || info.Vendor == null || !p[0].Equals(info.Vendor, StringComparison.OrdinalIgnoreCase)) continue;
                    if ((info.Version != null && info.Version.Contains(p[1])) || (info.VulkanDriver != null && info.VulkanDriver.Contains(p[1]))) hits.Add(p[2]);
                }
            }
            catch (Exception ex) { error = ex.Message; }
            return hits;
        }
    }

    // ====================================================================== crash dumps

    static class CrashDumps
    {
        const string LocalDumps = @"SOFTWARE\Microsoft\Windows\Windows Error Reporting\LocalDumps\skate3.exe";

        public static string DumpDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CrashDumps"); } }

        public static bool Enabled()
        {
            try { using (var k = Registry.LocalMachine.OpenSubKey(LocalDumps)) return k != null; } catch { return false; }
        }

        // Asks Windows (with a UAC prompt) to keep a small crash dump whenever skate3.exe crashes.
        public static bool Enable()
        {
            var psi = new ProcessStartInfo("reg.exe", "add \"HKLM\\" + LocalDumps + "\" /v DumpType /t REG_DWORD /d 1 /f")
            { Verb = "runas", UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
            try
            {
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit(15000);
                    return p.ExitCode == 0;
                }
            }
            catch { return false; }   // the user said no to the UAC prompt
        }

        public static List<FileInfo> Find()
        {
            try
            {
                if (!Directory.Exists(DumpDir)) return new List<FileInfo>();
                return new DirectoryInfo(DumpDir).GetFiles("skate3*.dmp").OrderByDescending(f => f.LastWriteTimeUtc).ToList();
            }
            catch { return new List<FileInfo>(); }
        }
    }
}

namespace Skate3Launcher
{
    // ====================================================================== known issues (recomp bugs the launcher warns about)

    class KnownIssue
    {
        public string Key, Message;
        public List<string[]> Conditions = new List<string[]>();   // { key, op, value } with op "=", "!=", "<=", ">="
    }

    // Rules live in known-issues.txt in the launcher's repo so new recomp bugs can be flagged without a launcher release.
    // A copy ships inside the exe for offline use; the downloaded list is cached and refreshed at most every 12 hours.
    static class KnownIssues
    {
        public const string Url = "https://raw.githubusercontent.com/Trooper-code/Troopers-Skate-3-Recomp-Launcher/main/known-issues.txt";
        static string CachePath { get { return Path.Combine(Paths.DefaultUserDir, "launcher-known-issues.txt"); } }

        const string BuiltIn =
            "skate3_native_render_scene_ssao_full_res | gpu_backend='vulkan'; skate3_native_render_scene_ssao_full_res=true | Known recomp bug (seen on v2.0.2): on Vulkan, full-resolution SSAO makes shadows swim and cast in the wrong place. Set Graphics API to Direct3D 12 on Audio & System, or turn this off.\n" +
            "skate3_native_render_scene_shadow_static_size | gpu_backend='vulkan'; skate3_native_render_scene_shadow_static_casters=true; skate3_native_render_scene_shadow_static_size!=4096 | Known recomp bug (seen on v2.0.2): on Vulkan, any size other than 4096 makes shadows swim and cast in the wrong place. Set Graphics API to Direct3D 12 on Audio & System, or keep 4096.\n" +
            "gpu_backend | gpu_backend='vulkan'; skate3_native_render_scene_ssao_full_res=true | Known recomp bug: Vulkan with full-resolution SSAO breaks shadows. Direct3D 12 doesn't have this problem.\n" +
            "gpu_backend | gpu_backend='vulkan'; skate3_native_render_scene_shadow_static_casters=true; skate3_native_render_scene_shadow_static_size!=4096 | Known recomp bug: Vulkan with a static sun shadow resolution other than 4096 breaks shadows. Direct3D 12 doesn't have this problem.\n";

        static List<KnownIssue> rules;

        public static List<KnownIssue> Rules
        {
            get
            {
                if (rules != null) return rules;
                string text = null;
                try { if (File.Exists(CachePath)) text = File.ReadAllText(CachePath); } catch { }
                rules = Parse(text ?? BuiltIn);
                if (rules.Count == 0) rules = Parse(BuiltIn);
                return rules;
            }
        }

        public static List<KnownIssue> Parse(string text)
        {
            var list = new List<KnownIssue>();
            foreach (var raw in (text ?? "").Split('\n'))
            {
                string l = raw.Trim();
                if (l.Length == 0 || l.StartsWith("#")) continue;
                var p = l.Split(new[] { '|' }, 3);
                if (p.Length < 3) continue;
                var r = new KnownIssue { Key = p[0].Trim(), Message = p[2].Trim() };
                bool ok = r.Key.Length > 0;
                foreach (var c in p[1].Split(';'))
                {
                    string t = c.Trim();
                    if (t.Length == 0) continue;
                    var m = Regex.Match(t, @"^([A-Za-z0-9_]+)\s*(!=|<=|>=|=)\s*(.+)$");
                    if (!m.Success) { ok = false; break; }
                    r.Conditions.Add(new[] { m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value.Trim() });
                }
                if (ok) list.Add(r);
            }
            return list;
        }

        // value(key) = the current TOML literal for a setting (null if unknown). "recomp" compares against the installed recomp version.
        public static bool Matches(KnownIssue r, Func<string, string> value, string recompVersion)
        {
            foreach (var c in r.Conditions)
            {
                if (c[0] == "recomp")
                {
                    if (recompVersion == null) return false;
                    int cmp = Updates.Compare(recompVersion, c[2]);
                    bool pass = c[1] == "=" ? cmp == 0 : c[1] == "!=" ? cmp != 0 : c[1] == "<=" ? cmp <= 0 : cmp >= 0;
                    if (!pass) return false;
                    continue;
                }
                string v = value(c[0]);
                if (v == null) return false;
                if (c[1] == "=" || c[1] == "!=")
                {
                    bool eq = Toml.Norm(v) == Toml.Norm(c[2]);
                    if (eq != (c[1] == "=")) return false;
                }
                else
                {
                    double a = Toml.Num(v, double.NaN), b = Toml.Num(c[2], double.NaN);
                    if (double.IsNaN(a) || double.IsNaN(b) || (c[1] == "<=" ? a > b : a < b)) return false;
                }
            }
            return true;
        }

        // Downloads the current list (at most every 12 hours unless forced). Returns true when the rules changed.
        public static bool Refresh(bool force)
        {
            try
            {
                if (!force && File.Exists(CachePath) && DateTime.UtcNow - File.GetLastWriteTimeUtc(CachePath) < TimeSpan.FromHours(12)) return false;
                string text = Http.GetString(Url, null);
                var parsed = Parse(text);
                if (parsed.Count == 0 && text.IndexOf('|') >= 0) return false;   // don't replace good rules with a broken download
                Directory.CreateDirectory(Paths.DefaultUserDir);
                string old = File.Exists(CachePath) ? File.ReadAllText(CachePath) : null;
                File.WriteAllText(CachePath, text);
                rules = parsed;
                return old != text;
            }
            catch { return false; }
        }
    }
}
