// Trooper's Skate 3 Recomp Launcher — modding services (no UI).
// mod.json format 1 metadata, Recompiled+ flavor detection, compatibility/dependency warnings,
// and new-mod folder scaffolding. Written for the C# 5 compiler in build.cmd.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Skate3Launcher
{
    // Detects whether the game install is official Skate 3 Recomp ("official") or Skate 3 Recompiled+ ("plus").
    static class RecompFlavor
    {
        public const string Official = "official";
        public const string Plus = "plus";
        public const string Any = "any";

        public static string Detect(string gameExe)
        {
            return Detect(gameExe, Paths.Config);
        }

        public static string Detect(string gameExe, string configPath)
        {
            try
            {
                if (!string.IsNullOrEmpty(gameExe))
                {
                    string dir = Path.GetDirectoryName(gameExe);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(Path.Combine(dir, "mods", "skate2")))
                        return Plus;
                }
            }
            catch { }

            try
            {
                if (!string.IsNullOrEmpty(configPath) && File.Exists(configPath))
                {
                    if (TomlSnapshot.Read(configPath).ContainsKey("skate3_skate2_enabled"))
                        return Plus;
                }
            }
            catch { }

            return Official;
        }
    }

    // Reads mod.json format 1 fields and checks mod compatibility, dependencies and conflicts.
    static class ModManifest
    {
        public const int CurrentFormat = 1;

        public static void ReadFormat1(ModInfo m, object jsonRoot, string jsonDir)
        {
            if (m == null || jsonRoot == null) return;

            string fmtStr = MiniJson.S(jsonRoot, "format");
            int fmtInt;
            double fmtDbl;
            if (!string.IsNullOrEmpty(fmtStr))
            {
                if (int.TryParse(fmtStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out fmtInt)) m.Format = fmtInt;
                else if (double.TryParse(fmtStr, NumberStyles.Float, CultureInfo.InvariantCulture, out fmtDbl)) m.Format = (int)fmtDbl;
            }

            string preview = (MiniJson.S(jsonRoot, "preview") ?? "").Trim();
            if (preview.Length > 0 && !string.IsNullOrEmpty(jsonDir) && !string.IsNullOrEmpty(m.Dir))
            {
                try
                {
                    string candidate = Path.Combine(jsonDir, preview.Replace('/', '\\'));
                    if (FileUtil.Inside(m.Dir, candidate) && File.Exists(candidate))
                        m.PreviewPath = Path.GetFullPath(candidate);
                }
                catch { }
            }

            object targets = MiniJson.O(jsonRoot, "targets");
            if (targets != null)
            {
                string recomp = (MiniJson.S(targets, "recomp") ?? "").Trim().ToLowerInvariant();
                if (recomp.Length > 0) m.TargetRecomp = recomp;
                string minVer = (MiniJson.S(targets, "min_version") ?? "").Trim();
                if (minVer.Length > 0) m.MinVersion = minVer;
            }

            ReadStringArray(jsonRoot, "requires", m.Requires);
            ReadStringArray(jsonRoot, "conflicts", m.ConflictsWith);
        }

        static void ReadStringArray(object obj, string key, List<string> target)
        {
            var arr = MiniJson.O(obj, key) as List<object>;
            if (arr == null) return;
            foreach (var item in arr)
            {
                string s = (item != null ? Convert.ToString(item, CultureInfo.InvariantCulture) : "").Trim();
                if (s.Length > 0 && !target.Contains(s, StringComparer.OrdinalIgnoreCase))
                    target.Add(s);
            }
        }

        public static bool MatchesMod(ModInfo m, string query)
        {
            if (m == null || string.IsNullOrEmpty(query)) return false;
            string q = query.Trim();
            if (q.Length == 0) return false;
            if (string.Equals(m.Name, q, StringComparison.OrdinalIgnoreCase)) return true;
            if (!string.IsNullOrEmpty(m.Title) && string.Equals(m.Title, q, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static ModInfo FindMod(IEnumerable<ModInfo> mods, string query)
        {
            if (mods == null || string.IsNullOrEmpty(query)) return null;
            return mods.FirstOrDefault(m => MatchesMod(m, query));
        }

        // Computes per-mod warnings for the Mods page (never blocks PLAY).
        public static List<string> Warnings(ModInfo m, IList<ModInfo> allMods, string recompFlavor, string installedRecompVersion)
        {
            var list = new List<string>();
            if (m == null) return list;

            if (m.Format > CurrentFormat)
                list.Add("Made for a newer launcher (mod.json format " + m.Format + ")");

            string target = (m.TargetRecomp ?? RecompFlavor.Any).Trim().ToLowerInvariant();
            string flavor = (recompFlavor ?? RecompFlavor.Official).Trim().ToLowerInvariant();
            if (target == RecompFlavor.Plus && flavor != RecompFlavor.Plus)
                list.Add("Made for Skate 3 Recompiled+ (this install is official Skate 3 Recomp)");
            else if (target == RecompFlavor.Official && flavor == RecompFlavor.Plus)
                list.Add("Made for official Skate 3 Recomp (this install is Skate 3 Recompiled+)");
            else if (target.Length > 0 && target != RecompFlavor.Any && target != RecompFlavor.Official && target != RecompFlavor.Plus)
                list.Add("Unknown target recomp \"" + m.TargetRecomp + "\"");

            if (!string.IsNullOrEmpty(m.MinVersion) && !string.IsNullOrEmpty(installedRecompVersion))
            {
                if (Updates.Compare(installedRecompVersion, m.MinVersion) < 0)
                    list.Add("Needs Skate 3 Recomp v" + m.MinVersion.TrimStart('v', 'V') + " or newer (installed: v" + installedRecompVersion.TrimStart('v', 'V') + ")");
            }

            if (m.Requires != null && m.Requires.Count > 0)
            {
                foreach (var req in m.Requires)
                {
                    var dep = FindMod(allMods, req);
                    if (dep == null)
                        list.Add("Missing required mod \"" + req + "\"");
                    else if (!dep.Enabled)
                        list.Add("Requires \"" + dep.Display + "\" to be enabled");
                }
            }

            var conflicting = new List<string>();
            if (m.ConflictsWith != null)
            {
                foreach (var c in m.ConflictsWith)
                {
                    var other = FindMod(allMods, c);
                    if (other != null && other != m && other.Enabled && !conflicting.Contains(other.Display, StringComparer.OrdinalIgnoreCase))
                        conflicting.Add(other.Display);
                }
            }
            if (allMods != null)
            {
                foreach (var other in allMods)
                {
                    if (other == null || other == m || !other.Enabled || other.ConflictsWith == null) continue;
                    if (other.ConflictsWith.Any(c => MatchesMod(m, c)) && !conflicting.Contains(other.Display, StringComparer.OrdinalIgnoreCase))
                        conflicting.Add(other.Display);
                }
            }
            foreach (var c in conflicting)
                list.Add("Conflicts with \"" + c + "\"");

            return list;
        }
    }

    // Scaffolds a new mod folder under mods\<name>\ with mod.json (format 1), data\, and README.txt.
    static class ModCreator
    {
        static readonly HashSet<string> ReservedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public static string ValidateName(string gameExe, string rawName, out string cleanName)
        {
            cleanName = (rawName ?? "").Trim().TrimEnd('.');
            if (string.IsNullOrEmpty(gameExe)) return "Set the game location on the Launcher page first.";
            if (cleanName.Length == 0) return "Enter a name for the mod.";
            if (cleanName.StartsWith(".") || cleanName.StartsWith("_"))
                return "Mod folder names can't start with '.' or '_' (the mod loader ignores those folders).";
            if (cleanName.Equals("skate2", StringComparison.OrdinalIgnoreCase))
                return "\"skate2\" is reserved for Skate 3 Recompiled+'s New San Vanelona files.";
            if (cleanName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || cleanName.Contains("/") || cleanName.Contains("\\"))
                return "That name contains characters Windows can't use in a folder name.";
            if (ReservedNames.Contains(cleanName))
                return "\"" + cleanName + "\" is a reserved Windows folder name.";

            string modsDir = Mods.Dir(gameExe);
            string dest = Path.Combine(modsDir, cleanName);
            if (!FileUtil.Inside(modsDir, dest))
                return "Invalid mod folder name.";
            if (Directory.Exists(dest) || File.Exists(dest))
                return "A mod folder named \"" + cleanName + "\" already exists.";
            return null;
        }

        public static string Create(string gameExe, string rawName)
        {
            string name;
            string err = ValidateName(gameExe, rawName, out name);
            if (err != null) throw new InvalidOperationException(err);

            string modsDir = Mods.Dir(gameExe);
            string modDir = Path.Combine(modsDir, name);
            Directory.CreateDirectory(modDir);
            Directory.CreateDirectory(Path.Combine(modDir, "data"));

            var json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"format\": 1,");
            json.AppendLine("  \"name\": \"" + Json.Esc(name) + "\",");
            json.AppendLine("  \"version\": \"0.1.0\",");
            json.AppendLine("  \"author\": \"\",");
            json.AppendLine("  \"description\": \"\",");
            json.AppendLine("  \"targets\": {");
            json.AppendLine("    \"recomp\": \"any\"");
            json.AppendLine("  },");
            json.AppendLine("  \"requires\": [],");
            json.AppendLine("  \"conflicts\": []");
            json.AppendLine("}");
            File.WriteAllText(Path.Combine(modDir, "mod.json"), json.ToString(), new UTF8Encoding(false));

            var readme = new StringBuilder();
            readme.AppendLine(name);
            readme.AppendLine(new string('=', Math.Max(4, name.Length)));
            readme.AppendLine();
            readme.AppendLine("Put the game files your mod replaces inside the \"data\" folder, using the same");
            readme.AppendLine("folder layout as the game's \"game\\data\" folder. For example:");
            readme.AppendLine();
            readme.AppendLine("  data\\content\\createacharacter.big");
            readme.AppendLine();
            readme.AppendLine("Edit mod.json to fill in your author name, description, version, preview image,");
            readme.AppendLine("or target recomp (\"any\", \"official\", or \"plus\").");
            readme.AppendLine();
            readme.AppendLine("When you enable this mod on the Mods page and press PLAY, the launcher links");
            readme.AppendLine("your files into a temporary copy of the game folder without changing your");
            readme.AppendLine("original game files.");
            File.WriteAllText(Path.Combine(modDir, "README.txt"), readme.ToString(), new UTF8Encoding(false));

            return modDir;
        }
    }
}
