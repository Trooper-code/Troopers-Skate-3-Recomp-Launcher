// Trooper's Skate 3 Recomp Launcher — background services (no UI).
// Paths, launcher prefs, GPU enumeration, save backups, logs, Discord presence, update checks,
// shortcuts, controller input and system info. Everything here uses only assemblies that ship with
// the .NET Framework 4.5+ that is part of Windows, so the launcher stays a single portable .exe.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Skate3Launcher
{
    // ====================================================================== paths & prefs

    static class Paths
    {
        public static readonly string UserDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "skate3");
        public static readonly string Config = Path.Combine(UserDir, "settings.toml");
        public static readonly string Prefs = Path.Combine(UserDir, "launcher.ini");
        public static readonly string Profiles = Path.Combine(UserDir, "launcher-profiles");
        public static readonly string History = Path.Combine(UserDir, "launcher-history");
        public static readonly string SaveBackups = Path.Combine(UserDir, "launcher-backups");
        public static readonly string Cache = Path.Combine(UserDir, "cache");
        public static readonly string Dlc = Path.Combine(UserDir, "dlc");

        public static string LogsDir(string gameExe)
        {
            return gameExe != null ? Path.Combine(Path.GetDirectoryName(gameExe), "logs") : null;
        }

        public static string LauncherExe { get { return Assembly.GetExecutingAssembly().Location; } }

        public static string FindGameExe()
        {
            string pref = LauncherPrefs.Read("game_exe");
            if (!string.IsNullOrEmpty(pref) && File.Exists(pref)) return pref;
            string dir = AppDomain.CurrentDomain.BaseDirectory;
            foreach (var cand in new[] { Path.Combine(dir, "skate3.exe"), Path.Combine(Path.GetDirectoryName(dir.TrimEnd('\\')) ?? dir, "skate3.exe") })
                if (File.Exists(cand)) return cand;
            return null;
        }

        public static long DirSize(string dir)
        {
            long total = 0;
            try
            {
                if (!Directory.Exists(dir)) return 0;
                foreach (var f in new DirectoryInfo(dir).EnumerateFiles("*", SearchOption.AllDirectories))
                    try { total += f.Length; } catch { }
            }
            catch { }
            return total;
        }

        public static string Size(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0", CultureInfo.InvariantCulture) + " KB";
            if (bytes < 1024L * 1024 * 1024) return (bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
            return (bytes / 1073741824.0).ToString("0.00", CultureInfo.InvariantCulture) + " GB";
        }

        public static string SafeName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim().TrimEnd('.');
        }
    }

    // Tiny key=value file (%APPDATA%\skate3\launcher.ini) for launcher-only options.
    static class LauncherPrefs
    {
        public static string Read(string key)
        {
            try
            {
                if (!File.Exists(Paths.Prefs)) return null;
                foreach (var line in File.ReadAllLines(Paths.Prefs))
                {
                    int i = line.IndexOf('=');
                    if (i > 0 && line.Substring(0, i).Trim() == key) return line.Substring(i + 1).Trim();
                }
            }
            catch { }
            return null;
        }

        public static void Write(string key, string value)
        {
            try
            {
                var lines = File.Exists(Paths.Prefs) ? File.ReadAllLines(Paths.Prefs).ToList() : new List<string>();
                lines.RemoveAll(l => l.StartsWith(key + "="));
                lines.Add(key + "=" + value);
                Directory.CreateDirectory(Paths.UserDir);
                File.WriteAllLines(Paths.Prefs, lines);
            }
            catch { }
        }

        public static bool Bool(string key, bool def)
        {
            string v = Read(key);
            if (v == null) return def;
            return v == "1" || v.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        public static int Int(string key, int def)
        {
            int i;
            return int.TryParse(Read(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out i) ? i : def;
        }

        public static void SetBool(string key, bool v) { Write(key, v ? "1" : "0"); }
    }

    // Plain key = literal TOML snapshot files, used for profiles and settings history.
    static class TomlSnapshot
    {
        static readonly Regex LineRx = new Regex(@"^\s*([A-Za-z0-9_]+)\s*=\s*(.*?)\s*$");

        public static Dictionary<string, string> Read(string path)
        {
            var d = new Dictionary<string, string>();
            foreach (var line in File.ReadAllLines(path))
            {
                if (line.TrimStart().StartsWith("#")) continue;
                var m = LineRx.Match(line);
                if (m.Success) d[m.Groups[1].Value] = m.Groups[2].Value;
            }
            return d;
        }

        public static void Write(string path, Dictionary<string, string> values, string header)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var sb = new StringBuilder();
            sb.Append("# ").Append(header).Append('\n');
            foreach (var kv in values.OrderBy(k => k.Key)) sb.Append(kv.Key).Append(" = ").Append(kv.Value).Append('\n');
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
        }
    }

    static class Profiles
    {
        public static List<FileInfo> List()
        {
            try
            {
                if (!Directory.Exists(Paths.Profiles)) return new List<FileInfo>();
                return new DirectoryInfo(Paths.Profiles).GetFiles("*.toml").OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToList();
            }
            catch { return new List<FileInfo>(); }
        }

        public static string PathFor(string name) { return Path.Combine(Paths.Profiles, Paths.SafeName(name) + ".toml"); }

        public static string Find(string name)
        {
            string p = PathFor(name);
            if (File.Exists(p)) return p;
            var match = List().FirstOrDefault(f => Path.GetFileNameWithoutExtension(f.Name).Equals(name, StringComparison.OrdinalIgnoreCase));
            return match != null ? match.FullName : null;
        }

        // Copies settings.toml into the history folder before it gets overwritten; keeps the newest 15.
        public static void SnapshotHistory()
        {
            try
            {
                if (!File.Exists(Paths.Config)) return;
                Directory.CreateDirectory(Paths.History);
                File.Copy(Paths.Config, Path.Combine(Paths.History, "settings-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".toml"), true);
                foreach (var old in History().Skip(15)) try { old.Delete(); } catch { }
            }
            catch { }
        }

        public static List<FileInfo> History()
        {
            try
            {
                if (!Directory.Exists(Paths.History)) return new List<FileInfo>();
                return new DirectoryInfo(Paths.History).GetFiles("settings-*.toml").OrderByDescending(f => f.LastWriteTimeUtc).ToList();
            }
            catch { return new List<FileInfo>(); }
        }
    }

    // ====================================================================== GPUs

    class GpuDevice
    {
        public int Index;
        public string Name;
        public int Type;            // Vulkan VkPhysicalDeviceType: 1 integrated, 2 discrete, 3 virtual, 4 cpu, 0 unknown
        public long VramBytes;

        public string TypeName
        {
            get { return Type == 1 ? "integrated" : Type == 2 ? "discrete" : Type == 3 ? "virtual" : Type == 4 ? "software" : "GPU"; }
        }

        public string Label
        {
            get
            {
                string s = Index + ": " + Name;
                var bits = new List<string>();
                if (VramBytes > 0 && Type != 1) bits.Add(Math.Round(VramBytes / 1073741824.0).ToString(CultureInfo.InvariantCulture) + " GB");
                if (Type != 0) bits.Add(TypeName);
                return bits.Count > 0 ? s + "  (" + string.Join(", ", bits) + ")" : s;
            }
        }
    }

    static class GpuInfo
    {
        static List<GpuDevice> cached;
        public static string Source = "";

        [StructLayout(LayoutKind.Sequential)]
        struct VkInstanceCreateInfo
        {
            public int sType;
            public IntPtr pNext;
            public uint flags;
            public IntPtr pApplicationInfo;
            public uint enabledLayerCount;
            public IntPtr ppEnabledLayerNames;
            public uint enabledExtensionCount;
            public IntPtr ppEnabledExtensionNames;
        }

        [DllImport("vulkan-1.dll")] static extern int vkCreateInstance(ref VkInstanceCreateInfo info, IntPtr alloc, out IntPtr instance);
        [DllImport("vulkan-1.dll")] static extern void vkDestroyInstance(IntPtr instance, IntPtr alloc);
        [DllImport("vulkan-1.dll")] static extern int vkEnumeratePhysicalDevices(IntPtr instance, ref uint count, IntPtr[] devices);
        [DllImport("vulkan-1.dll")] static extern void vkGetPhysicalDeviceProperties(IntPtr device, IntPtr props);
        [DllImport("vulkan-1.dll")] static extern void vkGetPhysicalDeviceMemoryProperties(IntPtr device, IntPtr props);

        // Vulkan physical devices in the same order the game's 'vulkan_device' index uses.
        public static List<GpuDevice> Vulkan(string logsDir)
        {
            if (cached != null) return cached;
            var list = new List<GpuDevice>();
            try { list = EnumerateVulkan(); Source = "Vulkan driver"; }
            catch { list = new List<GpuDevice>(); }
            if (list.Count == 0)
            {
                list = FromLog(logsDir);
                Source = list.Count > 0 ? "game log" : "";
            }
            cached = list;
            return list;
        }

        static List<GpuDevice> EnumerateVulkan()
        {
            var list = new List<GpuDevice>();
            var info = new VkInstanceCreateInfo { sType = 1 };
            IntPtr inst;
            if (vkCreateInstance(ref info, IntPtr.Zero, out inst) != 0 || inst == IntPtr.Zero) return list;
            IntPtr props = Marshal.AllocHGlobal(4096), mem = Marshal.AllocHGlobal(4096);
            try
            {
                uint count = 0;
                vkEnumeratePhysicalDevices(inst, ref count, null);
                if (count == 0) return list;
                var devs = new IntPtr[count];
                vkEnumeratePhysicalDevices(inst, ref count, devs);
                for (int i = 0; i < count; i++)
                {
                    vkGetPhysicalDeviceProperties(devs[i], props);
                    var d = new GpuDevice { Index = i, Type = Marshal.ReadInt32(props, 16) };
                    var nameBytes = new byte[256];
                    Marshal.Copy(props + 20, nameBytes, 0, 256);
                    int len = Array.IndexOf(nameBytes, (byte)0);
                    d.Name = Encoding.UTF8.GetString(nameBytes, 0, len < 0 ? 256 : len).Trim();

                    vkGetPhysicalDeviceMemoryProperties(devs[i], mem);
                    int heaps = Marshal.ReadInt32(mem, 260);
                    for (int h = 0; h < heaps && h < 16; h++)
                    {
                        long size = Marshal.ReadInt64(mem, 264 + h * 16);
                        int flags = Marshal.ReadInt32(mem, 264 + h * 16 + 8);
                        if ((flags & 1) != 0 && size > d.VramBytes) d.VramBytes = size;
                    }
                    list.Add(d);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(props);
                Marshal.FreeHGlobal(mem);
                vkDestroyInstance(inst, IntPtr.Zero);
            }
            return list;
        }

        // Fallback: the game prints "* 0: AMD Radeon RX 6800 XT" under "Available Vulkan physical devices".
        static List<GpuDevice> FromLog(string logsDir)
        {
            var list = new List<GpuDevice>();
            try
            {
                foreach (var f in LogTools.Files(logsDir))
                {
                    var lines = LogTools.ReadAll(f.FullName, 400);
                    bool inList = false;
                    foreach (var l in lines)
                    {
                        if (l.Contains("Available Vulkan physical devices")) { inList = true; list.Clear(); continue; }
                        if (!inList) continue;
                        var m = Regex.Match(l, @"\*\s*(\d+):\s*(.+)$");
                        if (m.Success)
                        {
                            string name = m.Groups[2].Value.Trim();
                            list.Add(new GpuDevice { Index = int.Parse(m.Groups[1].Value), Name = name, Type = name.Contains("(TM) Graphics") || name.Contains("UHD") || name.Contains("Iris") ? 1 : 0 });
                        }
                        else if (list.Count > 0) break;
                    }
                    if (list.Count > 0) break;
                }
            }
            catch { }
            return list;
        }

        // Windows "Graphics settings" per-app GPU preference (affects Direct3D 12 adapter choice).
        const string PrefKey = @"Software\Microsoft\DirectX\UserGpuPreferences";

        public static int WindowsPreference(string exe)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(PrefKey))
                {
                    var v = k != null ? k.GetValue(exe) as string : null;
                    if (v == null) return 0;
                    var m = Regex.Match(v, @"GpuPreference=(\d)");
                    return m.Success ? int.Parse(m.Groups[1].Value) : 0;
                }
            }
            catch { return 0; }
        }

        public static void SetWindowsPreference(string exe, int pref)
        {
            using (var k = Registry.CurrentUser.CreateSubKey(PrefKey))
            {
                if (pref <= 0) k.DeleteValue(exe, false);
                else k.SetValue(exe, "GpuPreference=" + pref + ";");
            }
        }
    }

    // ====================================================================== save data

    static class SaveData
    {
        static readonly Regex ProfileRx = new Regex("^[0-9A-Fa-f]{16}$");

        // Game profile folders (one per gamer profile, e.g. B13E07DFF9AB6772) inside %APPDATA%\skate3.
        public static List<DirectoryInfo> ProfileDirs()
        {
            try
            {
                if (!Directory.Exists(Paths.UserDir)) return new List<DirectoryInfo>();
                return new DirectoryInfo(Paths.UserDir).GetDirectories().Where(d => ProfileRx.IsMatch(d.Name)).ToList();
            }
            catch { return new List<DirectoryInfo>(); }
        }

        public static List<FileInfo> Backups()
        {
            try
            {
                if (!Directory.Exists(Paths.SaveBackups)) return new List<FileInfo>();
                return new DirectoryInfo(Paths.SaveBackups).GetFiles("saves-*.zip").OrderByDescending(f => f.LastWriteTimeUtc).ToList();
            }
            catch { return new List<FileInfo>(); }
        }

        // Zips every profile folder. kind: "auto", "manual" or "pre-restore". Returns null when there is nothing to back up.
        public static string Backup(string kind)
        {
            var dirs = ProfileDirs();
            if (dirs.Count == 0) return null;
            Directory.CreateDirectory(Paths.SaveBackups);
            string path = Path.Combine(Paths.SaveBackups, "saves-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + kind + ".zip");
            string tmp = path + ".tmp";
            using (var fs = new FileStream(tmp, FileMode.Create))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var d in dirs)
                    foreach (var f in d.GetFiles("*", SearchOption.AllDirectories))
                    {
                        string rel = f.FullName.Substring(Paths.UserDir.Length).TrimStart('\\', '/').Replace('\\', '/');
                        var entry = zip.CreateEntry(rel, CompressionLevel.Optimal);
                        entry.LastWriteTime = f.LastWriteTime;
                        using (var src = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (var dst = entry.Open()) src.CopyTo(dst);
                    }
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
            return path;
        }

        public static void PruneAuto(int keep)
        {
            foreach (var f in Backups().Where(b => b.Name.EndsWith("-auto.zip")).Skip(Math.Max(1, keep)))
                try { f.Delete(); } catch { }
        }

        // Restores a backup zip. Always makes a "pre-restore" backup of the current saves first.
        public static void Restore(string zipPath)
        {
            Backup("pre-restore");
            char sep = Path.DirectorySeparatorChar;
            string root = Path.GetFullPath(Paths.UserDir).TrimEnd(sep) + sep;
            using (var zip = ZipFile_Open(zipPath))
            {
                var tops = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var e in zip.Entries)
                {
                    int slash = e.FullName.IndexOf('/');
                    string top = slash > 0 ? e.FullName.Substring(0, slash) : e.FullName;
                    if (ProfileRx.IsMatch(top)) tops.Add(top);
                }
                foreach (var t in tops)
                {
                    string d = Path.Combine(Paths.UserDir, t);
                    if (Directory.Exists(d)) Directory.Delete(d, true);
                }
                foreach (var e in zip.Entries)
                {
                    if (string.IsNullOrEmpty(e.Name)) continue;   // directory entry
                    string dest = Path.GetFullPath(Path.Combine(Paths.UserDir, e.FullName.Replace('/', sep)));
                    if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;   // never write outside the save folder
                    int slash = e.FullName.IndexOf('/');
                    if (slash <= 0 || !ProfileRx.IsMatch(e.FullName.Substring(0, slash))) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    using (var src = e.Open())
                    using (var dst = new FileStream(dest, FileMode.Create)) src.CopyTo(dst);
                    try { File.SetLastWriteTime(dest, e.LastWriteTime.DateTime); } catch { }
                }
            }
        }

        static ZipArchive ZipFile_Open(string path)
        {
            return new ZipArchive(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read), ZipArchiveMode.Read);
        }

        public static List<FileInfo> DlcFiles()
        {
            try
            {
                if (!Directory.Exists(Paths.Dlc)) return new List<FileInfo>();
                return new DirectoryInfo(Paths.Dlc).GetFiles("*", SearchOption.AllDirectories).OrderBy(f => f.Name).ToList();
            }
            catch { return new List<FileInfo>(); }
        }
    }

    // ====================================================================== logs

    class SessionInfo
    {
        public FileInfo File;
        public bool Clean;
        public int Errors, Warnings;
        public string Version;
    }

    static class LogTools
    {
        public static List<FileInfo> Files(string logsDir)
        {
            try
            {
                if (logsDir == null || !Directory.Exists(logsDir)) return new List<FileInfo>();
                return new DirectoryInfo(logsDir).GetFiles("*.log").OrderByDescending(f => f.LastWriteTimeUtc).ToList();
            }
            catch { return new List<FileInfo>(); }
        }

        // Reads a log even while the game has it open. maxLines keeps only the last N lines (0 = all).
        public static List<string> ReadAll(string path, int maxLines)
        {
            var lines = new List<string>();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var sr = new StreamReader(fs, Encoding.UTF8, true))
            {
                string l;
                var q = new Queue<string>();
                while ((l = sr.ReadLine()) != null)
                {
                    if (maxLines > 0)
                    {
                        q.Enqueue(l);
                        if (q.Count > maxLines) q.Dequeue();
                    }
                    else lines.Add(l);
                }
                if (maxLines > 0) lines.AddRange(q);
            }
            return lines;
        }

        public static int Level(string line)
        {
            if (line.IndexOf("[error]", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("[critical]", StringComparison.OrdinalIgnoreCase) >= 0 ||
                line.IndexOf("fatal", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("exception", StringComparison.OrdinalIgnoreCase) >= 0) return 2;
            if (line.IndexOf("[warning]", StringComparison.OrdinalIgnoreCase) >= 0) return 1;
            return 0;
        }

        public static SessionInfo LastSession(string logsDir)
        {
            var f = Files(logsDir).FirstOrDefault();
            if (f == null) return null;
            var s = new SessionInfo { File = f };
            try
            {
                foreach (var l in ReadAll(f.FullName, 0))
                {
                    int lv = Level(l);
                    if (lv == 2) s.Errors++;
                    else if (lv == 1) s.Warnings++;
                    if (l.Contains("Execution complete") || l.Contains("shutting down")) s.Clean = true;
                    var m = Regex.Match(l, @"starting \[v?([0-9][0-9A-Za-z\.]*)");
                    if (m.Success) s.Version = m.Groups[1].Value;
                }
            }
            catch { }
            return s;
        }

        public static string InstalledVersion(string logsDir, string gameExe)
        {
            foreach (var f in Files(logsDir).Take(5))
            {
                try
                {
                    foreach (var l in ReadAll(f.FullName, 0).Take(40))
                    {
                        var m = Regex.Match(l, @"starting \[v?([0-9][0-9A-Za-z\.]*)");
                        if (m.Success) return m.Groups[1].Value;
                    }
                }
                catch { }
            }
            try
            {
                if (gameExe != null)
                {
                    var vi = FileVersionInfo.GetVersionInfo(gameExe);
                    if (!string.IsNullOrEmpty(vi.ProductVersion)) return vi.ProductVersion;
                }
            }
            catch { }
            return null;
        }
    }

    // ====================================================================== update check

    static class Updates
    {
        public const string DefaultRepo = "mchughalex/skate3recomp";

        public static bool Latest(string repo, out string tag, out string url, out string error)
        {
            tag = url = error = null;
            try
            {
                ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072;   // TLS 1.2
                var req = (HttpWebRequest)WebRequest.Create("https://api.github.com/repos/" + repo + "/releases/latest");
                req.UserAgent = "TroopersSkate3Launcher";
                req.Accept = "application/vnd.github+json";
                req.Timeout = 10000;
                string json;
                using (var resp = (HttpWebResponse)req.GetResponse())
                using (var sr = new StreamReader(resp.GetResponseStream())) json = sr.ReadToEnd();
                var t = Regex.Match(json, "\"tag_name\"\\s*:\\s*\"([^\"]+)\"");
                var u = Regex.Match(json, "\"html_url\"\\s*:\\s*\"([^\"]+)\"");
                if (!t.Success) { error = "No release found."; return false; }
                tag = t.Groups[1].Value;
                url = u.Success ? u.Groups[1].Value : "https://github.com/" + repo + "/releases";
                return true;
            }
            catch (Exception ex) { error = ex.Message; return false; }
        }

        static int[] Parts(string v)
        {
            if (v == null) return new int[0];
            return Regex.Matches(v, @"\d+").Cast<Match>().Take(4).Select(m => { int i; int.TryParse(m.Value, out i); return i; }).ToArray();
        }

        // > 0 when 'latest' is newer than 'installed'.
        public static int Compare(string latest, string installed)
        {
            var a = Parts(latest);
            var b = Parts(installed);
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                int x = i < a.Length ? a[i] : 0, y = i < b.Length ? b[i] : 0;
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }
    }

    // ====================================================================== Discord Rich Presence (local IPC pipe, no library)

    class DiscordRpc : IDisposable
    {
        NamedPipeClientStream pipe;
        public string UserName;

        public bool Connect(string appId)
        {
            for (int i = 0; i < 10 && pipe == null; i++)
            {
                try
                {
                    var p = new NamedPipeClientStream(".", "discord-ipc-" + i, PipeDirection.InOut, PipeOptions.None);
                    p.Connect(400);
                    pipe = p;
                }
                catch { }
            }
            if (pipe == null) return false;
            Send(0, "{\"v\":1,\"client_id\":\"" + Json.Esc(appId) + "\"}");
            string reply = Read();
            if (reply == null || reply.Contains("\"evt\":\"ERROR\"") || !reply.Contains("READY")) { Dispose(); return false; }
            var m = Regex.Match(reply, "\"(?:global_name|username)\"\\s*:\\s*\"([^\"]*)\"");
            UserName = m.Success ? m.Groups[1].Value : null;
            return true;
        }

        public void SetActivity(string details, string state, DateTime startUtc, string largeImage)
        {
            if (pipe == null) return;
            long start = (long)(startUtc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            var sb = new StringBuilder();
            sb.Append("{\"cmd\":\"SET_ACTIVITY\",\"nonce\":\"").Append(Guid.NewGuid().ToString()).Append("\",\"args\":{\"pid\":")
              .Append(Process.GetCurrentProcess().Id).Append(",\"activity\":{\"details\":\"").Append(Json.Esc(details))
              .Append("\",\"state\":\"").Append(Json.Esc(state)).Append("\",\"timestamps\":{\"start\":").Append(start).Append("}");
            if (!string.IsNullOrEmpty(largeImage))
                sb.Append(",\"assets\":{\"large_image\":\"").Append(Json.Esc(largeImage)).Append("\",\"large_text\":\"Skate 3 Recomp\"}");
            sb.Append("}}}");
            Send(1, sb.ToString());
            Read();
        }

        public void Clear()
        {
            if (pipe == null) return;
            try
            {
                Send(1, "{\"cmd\":\"SET_ACTIVITY\",\"nonce\":\"" + Guid.NewGuid() + "\",\"args\":{\"pid\":" + Process.GetCurrentProcess().Id + "}}");
                Read();
            }
            catch { }
        }

        void Send(int op, string json)
        {
            var payload = Encoding.UTF8.GetBytes(json);
            var frame = new byte[8 + payload.Length];
            BitConverter.GetBytes(op).CopyTo(frame, 0);
            BitConverter.GetBytes(payload.Length).CopyTo(frame, 4);
            payload.CopyTo(frame, 8);
            pipe.Write(frame, 0, frame.Length);
            pipe.Flush();
        }

        string Read()
        {
            try
            {
                var head = ReadExact(8);
                if (head == null) return null;
                int len = BitConverter.ToInt32(head, 4);
                if (len < 0 || len > 1 << 20) return null;
                var body = ReadExact(len);
                return body == null ? null : Encoding.UTF8.GetString(body);
            }
            catch { return null; }
        }

        byte[] ReadExact(int n)
        {
            var buf = new byte[n];
            int got = 0;
            while (got < n)
            {
                int r = pipe.Read(buf, got, n - got);
                if (r <= 0) return null;
                got += r;
            }
            return buf;
        }

        public void Dispose()
        {
            if (pipe != null) { try { pipe.Dispose(); } catch { } pipe = null; }
        }
    }

    static class Json
    {
        public static string Esc(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder();
            foreach (char c in s)
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }

    // ====================================================================== shortcuts, system info

    static class Shortcuts
    {
        public static void Create(string lnkPath, string target, string args, string workDir, string icon, string description)
        {
            Type t = Type.GetTypeFromProgID("WScript.Shell");
            object shell = Activator.CreateInstance(t);
            try
            {
                object lnk = t.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                Type lt = lnk.GetType();
                lt.InvokeMember("TargetPath", BindingFlags.SetProperty, null, lnk, new object[] { target });
                lt.InvokeMember("Arguments", BindingFlags.SetProperty, null, lnk, new object[] { args ?? "" });
                lt.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, lnk, new object[] { workDir ?? "" });
                if (!string.IsNullOrEmpty(icon)) lt.InvokeMember("IconLocation", BindingFlags.SetProperty, null, lnk, new object[] { icon + ",0" });
                lt.InvokeMember("Description", BindingFlags.SetProperty, null, lnk, new object[] { description ?? "" });
                lt.InvokeMember("Save", BindingFlags.InvokeMethod, null, lnk, null);
                Marshal.FinalReleaseComObject(lnk);
            }
            finally { Marshal.FinalReleaseComObject(shell); }
        }

        public static string Desktop(string name)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Paths.SafeName(name) + ".lnk");
        }
    }

    static class SysInfo
    {
        [StructLayout(LayoutKind.Sequential)]
        class MemStatus
        {
            public uint dwLength = (uint)Marshal.SizeOf(typeof(MemStatus));
            public uint dwMemoryLoad;
            public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx([In, Out] MemStatus m);

        static string Reg(string key, string name)
        {
            try { using (var k = Registry.LocalMachine.OpenSubKey(key)) return k != null ? Convert.ToString(k.GetValue(name)) : null; }
            catch { return null; }
        }

        public static string Os()
        {
            string name = Reg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ProductName") ?? "Windows";
            string build = Reg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "CurrentBuild");
            string disp = Reg(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion");
            int b;
            if (build != null && int.TryParse(build, out b) && b >= 22000) name = name.Replace("Windows 10", "Windows 11");
            return name + (disp != null ? " " + disp : "") + (build != null ? " (build " + build + ")" : "");
        }

        public static string Cpu() { return (Reg(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") ?? "Unknown CPU").Trim(); }

        public static string Ram()
        {
            try
            {
                var m = new MemStatus();
                if (GlobalMemoryStatusEx(m)) return Math.Round(m.ullTotalPhys / 1073741824.0).ToString(CultureInfo.InvariantCulture) + " GB";
            }
            catch { }
            return "unknown";
        }
    }

    // ====================================================================== controllers

    static class XInput
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Gamepad
        {
            public ushort Buttons;
            public byte LeftTrigger, RightTrigger;
            public short LX, LY, RX, RY;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct State { public uint Packet; public Gamepad Pad; }

        [StructLayout(LayoutKind.Sequential)]
        struct Vibration { public ushort Left, Right; }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")] static extern int GetState14(int idx, out State s);
        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")] static extern int GetState910(int idx, out State s);
        [DllImport("xinput1_4.dll", EntryPoint = "XInputSetState")] static extern int SetState14(int idx, ref Vibration v);
        [DllImport("xinput9_1_0.dll", EntryPoint = "XInputSetState")] static extern int SetState910(int idx, ref Vibration v);

        static int api = 14;

        public static bool Get(int idx, out State s)
        {
            s = new State();
            try { return (api == 14 ? GetState14(idx, out s) : GetState910(idx, out s)) == 0; }
            catch (DllNotFoundException) { if (api == 14) { api = 910; return Get(idx, out s); } return false; }
            catch { return false; }
        }

        public static void Rumble(int idx, ushort left, ushort right)
        {
            var v = new Vibration { Left = left, Right = right };
            try { if (api == 14) SetState14(idx, ref v); else SetState910(idx, ref v); } catch { }
        }

        // Button names exactly as the game's menu_chord cvar spells them.
        public static readonly string[] ChordNames = { "a", "b", "x", "y", "lb", "rb", "l3", "r3", "back", "start", "dpad_up", "dpad_down", "dpad_left", "dpad_right" };
        public static readonly ushort[] ChordMasks = { 0x1000, 0x2000, 0x4000, 0x8000, 0x0100, 0x0200, 0x0040, 0x0080, 0x0020, 0x0010, 0x0001, 0x0002, 0x0004, 0x0008 };
    }

    // Generic controllers (DirectInput / HID — PlayStation, Switch, etc.) through the classic joystick API.
    static class WinMM
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct JoyInfoEx
        {
            public uint Size, Flags, X, Y, Z, R, U, V, Buttons, ButtonNumber, Pov, Reserved1, Reserved2;
        }

        [DllImport("winmm.dll")] static extern int joyGetPosEx(int id, ref JoyInfoEx info);
        [DllImport("winmm.dll")] static extern int joyGetNumDevs();

        public static int Count() { try { return Math.Min(16, joyGetNumDevs()); } catch { return 0; } }

        public static bool Get(int id, out JoyInfoEx info)
        {
            info = new JoyInfoEx { Size = (uint)Marshal.SizeOf(typeof(JoyInfoEx)), Flags = 0xFF };
            try { return joyGetPosEx(id, ref info) == 0; } catch { return false; }
        }
    }
}
