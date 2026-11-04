// Trooper's Skate 3 Recomp Launcher — modding services (no UI).
// mod.json format 1 metadata, Recompiled+ flavor detection, compatibility/dependency warnings,
// new-mod folder scaffolding, mod validator and zip packager. Written for the C# 5 compiler in build.cmd.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
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

        public static bool IsReservedDeviceName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string trimmed = name.Trim().TrimEnd('.');
            if (ReservedNames.Contains(trimmed)) return true;
            int dot = trimmed.IndexOf('.');
            if (dot >= 0)
            {
                string stem = trimmed.Substring(0, dot).Trim();
                if (ReservedNames.Contains(stem)) return true;
            }
            return false;
        }

        public static string ValidateName(string gameExe, string rawName, out string cleanName)
        {
            cleanName = (rawName ?? "").Trim().TrimEnd('.', ' ');
            if (string.IsNullOrEmpty(gameExe)) return "Set the game location on the Launcher page first.";
            if (cleanName.Length == 0) return "Enter a name for the mod.";
            if (cleanName.StartsWith(".") || cleanName.StartsWith("_"))
                return "Mod folder names can't start with '.' or '_' (the mod loader ignores those folders).";
            if (cleanName.Equals("skate2", StringComparison.OrdinalIgnoreCase))
                return "\"skate2\" is reserved for Skate 3 Recompiled+'s New San Vanelona files.";
            if (cleanName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || cleanName.Contains("/") || cleanName.Contains("\\"))
                return "That name contains characters Windows can't use in a folder name.";
            if (IsReservedDeviceName(cleanName))
                return "\"" + cleanName + "\" is a reserved Windows folder name.";

            string modsDir = Mods.Dir(gameExe);
            string dest = Path.Combine(modsDir, cleanName);
            if (!FileUtil.Inside(modsDir, dest))
                return "Invalid mod folder name.";
            if (Directory.Exists(dest) || File.Exists(dest))
                return "A mod folder named \"" + cleanName + "\" already exists.";
            return null;
        }

        public static string Format1ModJson(string name)
        {
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
            return json.ToString();
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

            File.WriteAllText(Path.Combine(modDir, "mod.json"), Format1ModJson(name), new UTF8Encoding(false));

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

    // ====================================================================== M2: mod validator and zip packager

    enum ModCheckSeverity { Error, Warning, Info }

    enum ModCheckKind
    {
        General,
        NoUsableFiles,
        ProtectedFile,
        UnsafePath,
        WholeGameArchive,
        IdenticalToOriginal,
        Ps3File,
        NewFileNotInGame,
        MissingOrIncompleteModJson,
        MissingPreview
    }

    class ModCheckResult
    {
        public ModCheckSeverity Severity;
        public ModCheckKind Kind;
        public string Message;
        public string FilePath;   // relative path inside mod (or null for mod-level checks)
        public string FullPath;   // full path on disk inside the mod folder (when applicable)

        public string RelativePath { get { return FilePath; } set { FilePath = value; } }
    }

    static class ModValidator
    {
        public const long WholeArchiveBytes = 20L * 1024 * 1024;

        static readonly string[] Ps3Extensions = { ".psg", ".fpo", ".vpo", ".spu", ".self", ".sprx" };
        static readonly string[] Ps3FileNames = { "eboot.bin", "param.sfo" };

        public static bool IsPs3File(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return false;
            string name = Path.GetFileName(relPath).ToLowerInvariant();
            if (Ps3FileNames.Contains(name)) return true;
            foreach (var ext in Ps3Extensions)
                if (name.EndsWith(ext, StringComparison.Ordinal)) return true;
            return false;
        }

        // Checks whether a relative path stays inside modRoot and uses only characters/names Windows can store.
        public static bool IsSafeRelativePath(string modRoot, string relPath, out string reason)
        {
            reason = null;
            if (string.IsNullOrEmpty(relPath)) { reason = "Empty file path."; return false; }
            if (relPath.StartsWith("/") || relPath.StartsWith("\\") || relPath.IndexOf(':') >= 0)
            {
                reason = "Path \"" + relPath + "\" escapes the mod folder.";
                return false;
            }
            string[] parts = relPath.Split('/', '\\');
            foreach (var part in parts)
            {
                if (part.Length == 0 || part == "." || part == "..")
                {
                    reason = "Path \"" + relPath + "\" escapes the mod folder.";
                    return false;
                }
                if (part.EndsWith(" ") || part.EndsWith("."))
                {
                    reason = "Path \"" + relPath + "\" has a folder or file name ending with a space or period, which Windows can't store.";
                    return false;
                }
                if (part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.Any(c => c < 32))
                {
                    reason = "Path \"" + relPath + "\" contains characters Windows can't store.";
                    return false;
                }
                if (ModCreator.IsReservedDeviceName(part))
                {
                    reason = "Path \"" + relPath + "\" uses a reserved Windows device name (\"" + part + "\").";
                    return false;
                }
            }
            if (!string.IsNullOrEmpty(modRoot))
            {
                try
                {
                    string combined = Path.Combine(modRoot, relPath.Replace('/', '\\'));
                    if (!FileUtil.Inside(modRoot, combined))
                    {
                        reason = "Path \"" + relPath + "\" escapes the mod folder.";
                        return false;
                    }
                }
                catch
                {
                    reason = "Path \"" + relPath + "\" is not a valid Windows path.";
                    return false;
                }
            }
            return true;
        }

        public static List<ModCheckResult> Validate(string modDir, string gameExe)
        {
            return Validate(modDir, gameExe, null, null);
        }

        public static List<ModCheckResult> Validate(string modDir, string gameExe, Action<long, long, string> progress, Func<bool> cancelled)
        {
            if (string.IsNullOrEmpty(modDir) || !Directory.Exists(modDir))
            {
                return new List<ModCheckResult>
                {
                    new ModCheckResult { Severity = ModCheckSeverity.Error, Kind = ModCheckKind.NoUsableFiles, Message = "Mod folder does not exist." }
                };
            }
            return Validate(Mods.Read(modDir), gameExe, progress, cancelled);
        }

        public static List<ModCheckResult> Validate(ModInfo mod, string gameExe)
        {
            return Validate(mod, gameExe, null, null);
        }

        public static List<ModCheckResult> Validate(ModInfo mod, string gameExe, Action<long, long, string> progress, Func<bool> cancelled)
        {
            var results = new List<ModCheckResult>();
            if (mod == null)
            {
                results.Add(new ModCheckResult { Severity = ModCheckSeverity.Error, Kind = ModCheckKind.NoUsableFiles, Message = "No mod selected." });
                return results;
            }

            // Keep any synthetic/in-memory paths passed on 'mod' before re-reading from disk.
            var extraFiles = mod.Files != null ? new List<string>(mod.Files) : new List<string>();
            var extraSkipped = mod.Skipped != null ? new List<string>(mod.Skipped) : new List<string>();

            ModInfo m = !string.IsNullOrEmpty(mod.Dir) && Directory.Exists(mod.Dir) ? Mods.Read(mod.Dir) : mod;
            string modRoot = m.Root ?? m.Dir;

            // 1. Check any synthetic paths in extraFiles/extraSkipped that aren't on disk for path escapes or protected files.
            var unsafeSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in extraFiles.Concat(extraSkipped))
            {
                string why;
                if (!IsSafeRelativePath(modRoot, rel, out why))
                {
                    if (unsafeSeen.Add(rel))
                        results.Add(new ModCheckResult { Severity = ModCheckSeverity.Error, Kind = ModCheckKind.UnsafePath, FilePath = rel, Message = why });
                    continue;
                }
                string normRel = rel.Replace('\\', '/');
                if (GameFiles.Protected.Contains(normRel.ToLowerInvariant()) && !m.Skipped.Contains(normRel, StringComparer.OrdinalIgnoreCase))
                    m.Skipped.Add(normRel);
                else if (!m.Files.Contains(normRel, StringComparer.OrdinalIgnoreCase) && !m.Skipped.Contains(normRel, StringComparer.OrdinalIgnoreCase))
                    m.Files.Add(normRel);
            }

            // 2. Check disk entries and mod.json preview for escaping or invalid paths.
            if (!string.IsNullOrEmpty(m.Dir) && Directory.Exists(m.Dir))
            {
                CheckDiskPaths(m, results, unsafeSeen);
            }

            // 3. Protected files (default.xex, default.xexp, EAWebkit.xex, EAWebkit.xexp).
            foreach (var p in m.Skipped)
            {
                string full = !string.IsNullOrEmpty(modRoot) ? Path.Combine(modRoot, p.Replace('/', '\\')) : null;
                results.Add(new ModCheckResult
                {
                    Severity = ModCheckSeverity.Error,
                    Kind = ModCheckKind.ProtectedFile,
                    FilePath = p,
                    FullPath = full,
                    Message = p + ": Protected executable file. The recomp's code is built against the original default.xex and EAWebkit files, so mods cannot replace them."
                });
            }

            // 4. Usable files check.
            if (m.Files.Count == 0)
            {
                results.Add(new ModCheckResult
                {
                    Severity = ModCheckSeverity.Error,
                    Kind = ModCheckKind.NoUsableFiles,
                    Message = "This mod has no usable game files."
                });
            }

            // 5. Per-file checks (whole 20 MB+ archives, identical SHA-256 to game\, PS3 formats, new files not in game\).
            string gameRoot = !string.IsNullOrEmpty(gameExe) ? FileUtil.GameRoot(gameExe) : null;
            bool hasGameRoot = !string.IsNullOrEmpty(gameRoot) && Directory.Exists(gameRoot);

            long totalHashBytes = 0, doneHashBytes = 0;
            if (hasGameRoot && !string.IsNullOrEmpty(modRoot))
            {
                foreach (var rel in m.Files)
                {
                    if (unsafeSeen.Contains(rel)) continue;
                    string modFile = Path.Combine(modRoot, rel.Replace('/', '\\'));
                    string gameFile = Path.Combine(gameRoot, rel.Replace('/', '\\'));
                    if (File.Exists(modFile) && File.Exists(gameFile))
                    {
                        long len = new FileInfo(modFile).Length;
                        if (len == new FileInfo(gameFile).Length)
                            totalHashBytes += len * 2;
                    }
                }
            }

            foreach (var rel in m.Files)
            {
                if (cancelled != null && cancelled()) throw new OperationCanceledException();
                if (unsafeSeen.Contains(rel)) continue;

                string modFile = !string.IsNullOrEmpty(modRoot) ? Path.Combine(modRoot, rel.Replace('/', '\\')) : null;
                bool ps3 = IsPs3File(rel);
                if (ps3)
                {
                    results.Add(new ModCheckResult
                    {
                        Severity = ModCheckSeverity.Warning,
                        Kind = ModCheckKind.Ps3File,
                        FilePath = rel,
                        FullPath = modFile,
                        Message = rel + ": This looks like a PS3 mod. The recomp needs Xbox 360 files."
                    });
                }

                if (!hasGameRoot || string.IsNullOrEmpty(modFile) || !File.Exists(modFile)) continue;

                string gameFile = Path.Combine(gameRoot, rel.Replace('/', '\\'));
                var modFi = new FileInfo(modFile);
                if (File.Exists(gameFile))
                {
                    if (modFi.Length > WholeArchiveBytes)
                    {
                        long mb = (long)Math.Round(modFi.Length / 1048576.0);
                        results.Add(new ModCheckResult
                        {
                            Severity = ModCheckSeverity.Warning,
                            Kind = ModCheckKind.WholeGameArchive,
                            FilePath = rel,
                            FullPath = modFile,
                            Message = rel + ": This is a whole game archive (" + mb.ToString(CultureInfo.InvariantCulture) + " MB). Sharing it means sharing EA's files, and it makes the mod huge. Once texture packs are supported, ship only the files you changed."
                        });
                    }

                    var gameFi = new FileInfo(gameFile);
                    if (modFi.Length == gameFi.Length)
                    {
                        string currentRel = rel;
                        long beforeMod = doneHashBytes;
                        string modHash = FileUtil.Sha256(modFile, cancelled, n =>
                        {
                            doneHashBytes += n;
                            if (progress != null) progress(doneHashBytes, totalHashBytes, currentRel);
                        });
                        doneHashBytes = beforeMod + modFi.Length;

                        long beforeGame = doneHashBytes;
                        string gameHash = FileUtil.Sha256(gameFile, cancelled, n =>
                        {
                            doneHashBytes += n;
                            if (progress != null) progress(doneHashBytes, totalHashBytes, currentRel);
                        });
                        doneHashBytes = beforeGame + gameFi.Length;

                        if (string.Equals(modHash, gameHash, StringComparison.OrdinalIgnoreCase))
                        {
                            results.Add(new ModCheckResult
                            {
                                Severity = ModCheckSeverity.Warning,
                                Kind = ModCheckKind.IdenticalToOriginal,
                                FilePath = rel,
                                FullPath = modFile,
                                Message = rel + ": Identical to the original game file in game\\ (same SHA-256), so it does nothing."
                            });
                        }
                    }
                }
                else if (!ps3)
                {
                    results.Add(new ModCheckResult
                    {
                        Severity = ModCheckSeverity.Warning,
                        Kind = ModCheckKind.NewFileNotInGame,
                        FilePath = rel,
                        FullPath = modFile,
                        Message = rel + ": Doesn't exist in game\\ (allowed for new files, but probably a wrong path)."
                    });
                }
            }

            // 6. Metadata checks: mod.json and preview image.
            CheckMetadata(m, results, unsafeSeen);

            return results;
        }

        static void CheckDiskPaths(ModInfo m, List<ModCheckResult> results, HashSet<string> unsafeSeen)
        {
            try
            {
                foreach (var f in new DirectoryInfo(m.Dir).GetFileSystemInfos("*", SearchOption.AllDirectories))
                {
                    string rel = FileUtil.Rel(m.Dir, f.FullName);
                    if ((f.Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        if (unsafeSeen.Add(rel))
                            results.Add(new ModCheckResult
                            {
                                Severity = ModCheckSeverity.Error,
                                Kind = ModCheckKind.UnsafePath,
                                FilePath = rel,
                                FullPath = f.FullName,
                                Message = rel + ": Links or junctions inside a mod folder are not allowed."
                            });
                        continue;
                    }
                    string why;
                    if (!IsSafeRelativePath(m.Dir, rel, out why) && unsafeSeen.Add(rel))
                    {
                        results.Add(new ModCheckResult
                        {
                            Severity = ModCheckSeverity.Error,
                            Kind = ModCheckKind.UnsafePath,
                            FilePath = rel,
                            FullPath = f.FullName,
                            Message = why
                        });
                    }
                }
            }
            catch { }
        }

        static void CheckMetadata(ModInfo m, List<ModCheckResult> results, HashSet<string> unsafeSeen)
        {
            string jsonPath = null;
            if (!string.IsNullOrEmpty(m.Dir))
            {
                foreach (var cand in new[] { Path.Combine(m.Dir, "mod.json"), Path.Combine(m.Root ?? m.Dir, "mod.json") })
                    if (File.Exists(cand)) { jsonPath = cand; break; }
            }

            bool hasName = !string.IsNullOrEmpty(m.Title);
            bool hasVersion = !string.IsNullOrEmpty(m.Version);
            string rawPreview = null;

            if (jsonPath == null)
            {
                results.Add(new ModCheckResult
                {
                    Severity = ModCheckSeverity.Info,
                    Kind = ModCheckKind.MissingOrIncompleteModJson,
                    FilePath = "mod.json",
                    Message = "No mod.json file. Use Add mod.json to add the mod's name and version."
                });
            }
            else
            {
                try
                {
                    var o = MiniJson.Parse(File.ReadAllText(jsonPath));
                    string n = (MiniJson.S(o, "name") ?? "").Trim();
                    string v = (MiniJson.S(o, "version") ?? "").Trim();
                    rawPreview = (MiniJson.S(o, "preview") ?? "").Trim();
                    hasName = n.Length > 0;
                    hasVersion = v.Length > 0;
                    if (!hasName || !hasVersion)
                    {
                        string missing = !hasName && !hasVersion ? "name and version" : !hasName ? "name" : "version";
                        results.Add(new ModCheckResult
                        {
                            Severity = ModCheckSeverity.Info,
                            Kind = ModCheckKind.MissingOrIncompleteModJson,
                            FilePath = "mod.json",
                            FullPath = jsonPath,
                            Message = "mod.json is missing " + missing + ". Use Add mod.json to fill in defaults."
                        });
                    }
                }
                catch
                {
                    results.Add(new ModCheckResult
                    {
                        Severity = ModCheckSeverity.Info,
                        Kind = ModCheckKind.MissingOrIncompleteModJson,
                        FilePath = "mod.json",
                        FullPath = jsonPath,
                        Message = "mod.json couldn't be read. Use Add mod.json to write a clean one."
                    });
                }
            }

            if (!string.IsNullOrEmpty(rawPreview))
            {
                string why;
                string baseDir = jsonPath != null ? Path.GetDirectoryName(jsonPath) : m.Dir;
                if (!IsSafeRelativePath(m.Dir, rawPreview, out why) ||
                    (!string.IsNullOrEmpty(baseDir) && !string.IsNullOrEmpty(m.Dir) && !FileUtil.Inside(m.Dir, Path.Combine(baseDir, rawPreview.Replace('/', '\\')))))
                {
                    if (unsafeSeen.Add(rawPreview))
                        results.Add(new ModCheckResult
                        {
                            Severity = ModCheckSeverity.Error,
                            Kind = ModCheckKind.UnsafePath,
                            FilePath = rawPreview,
                            Message = "Preview path \"" + rawPreview + "\" in mod.json escapes the mod folder or contains invalid characters."
                        });
                }
            }

            bool hasPreview = !string.IsNullOrEmpty(m.PreviewPath) && File.Exists(m.PreviewPath);
            if (!hasPreview && !string.IsNullOrEmpty(m.Dir) && Directory.Exists(m.Dir))
            {
                foreach (var p in new[] { "preview.png", "preview.jpg", "preview.jpeg", "thumbnail.png", "thumbnail.jpg" })
                    if (File.Exists(Path.Combine(m.Dir, p)) || (!string.IsNullOrEmpty(m.Root) && File.Exists(Path.Combine(m.Root, p))))
                    { hasPreview = true; break; }
            }
            if (!hasPreview)
            {
                results.Add(new ModCheckResult
                {
                    Severity = ModCheckSeverity.Info,
                    Kind = ModCheckKind.MissingPreview,
                    Message = "No preview image (optional: add a preview.png next to mod.json)."
                });
            }
        }

        // Creates or updates mod.json in the mod folder so it has format 1, name and version.
        public static string EnsureModJson(ModInfo mod)
        {
            if (mod == null || string.IsNullOrEmpty(mod.Dir) || !Directory.Exists(mod.Dir))
                throw new InvalidOperationException("Mod folder does not exist.");

            string root = !string.IsNullOrEmpty(mod.Root) && Directory.Exists(mod.Root) ? mod.Root : mod.Dir;
            string jsonPath = File.Exists(Path.Combine(root, "mod.json")) && !File.Exists(Path.Combine(mod.Dir, "mod.json"))
                ? Path.Combine(root, "mod.json")
                : Path.Combine(mod.Dir, "mod.json");

            string name = !string.IsNullOrEmpty(mod.Title) ? mod.Title : mod.Name;
            string version = !string.IsNullOrEmpty(mod.Version) ? mod.Version : "0.1.0";
            string author = mod.Author ?? "";
            string description = mod.Description ?? "";
            string preview = "";
            string targetRecomp = !string.IsNullOrEmpty(mod.TargetRecomp) ? mod.TargetRecomp : RecompFlavor.Any;
            string minVersion = mod.MinVersion ?? "";
            var requires = mod.Requires != null ? new List<string>(mod.Requires) : new List<string>();
            var conflicts = mod.ConflictsWith != null ? new List<string>(mod.ConflictsWith) : new List<string>();

            if (File.Exists(jsonPath))
            {
                try
                {
                    var o = MiniJson.Parse(File.ReadAllText(jsonPath));
                    string n = (MiniJson.S(o, "name") ?? "").Trim();
                    string v = (MiniJson.S(o, "version") ?? "").Trim();
                    string a = MiniJson.S(o, "author");
                    string d = MiniJson.S(o, "description");
                    string p = MiniJson.S(o, "preview");
                    if (n.Length > 0) name = n;
                    if (v.Length > 0) version = v;
                    if (a != null) author = a;
                    if (d != null) description = d;
                    if (p != null) preview = p;
                }
                catch { }
            }
            if (string.IsNullOrEmpty(preview))
            {
                foreach (var cand in new[] { "preview.png", "preview.jpg", "preview.jpeg" })
                    if (File.Exists(Path.Combine(Path.GetDirectoryName(jsonPath), cand))) { preview = cand; break; }
            }

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"format\": 1,");
            sb.AppendLine("  \"name\": \"" + Json.Esc(name) + "\",");
            sb.AppendLine("  \"version\": \"" + Json.Esc(version) + "\",");
            sb.AppendLine("  \"author\": \"" + Json.Esc(author) + "\",");
            sb.AppendLine("  \"description\": \"" + Json.Esc(description) + "\",");
            if (!string.IsNullOrEmpty(preview))
                sb.AppendLine("  \"preview\": \"" + Json.Esc(preview) + "\",");
            sb.AppendLine("  \"targets\": {");
            if (!string.IsNullOrEmpty(minVersion))
            {
                sb.AppendLine("    \"recomp\": \"" + Json.Esc(targetRecomp) + "\",");
                sb.AppendLine("    \"min_version\": \"" + Json.Esc(minVersion) + "\"");
            }
            else
            {
                sb.AppendLine("    \"recomp\": \"" + Json.Esc(targetRecomp) + "\"");
            }
            sb.AppendLine("  },");
            sb.AppendLine("  \"requires\": [" + string.Join(", ", requires.Select(r => "\"" + Json.Esc(r) + "\"")) + "],");
            sb.AppendLine("  \"conflicts\": [" + string.Join(", ", conflicts.Select(c => "\"" + Json.Esc(c) + "\"")) + "]");
            sb.AppendLine("}");
            File.WriteAllText(jsonPath, sb.ToString(), new UTF8Encoding(false));
            return jsonPath;
        }

        public static bool HasReparsePoint(string path)
        {
            try
            {
                if (!File.Exists(path) && !Directory.Exists(path)) return false;
                return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
            }
            catch { return true; }
        }

        public static void EnsureNoReparsePointInChain(string filePath, string modDir)
        {
            string stop = Path.GetFullPath(modDir).TrimEnd('\\', '/');
            string cur = Path.GetFullPath(filePath).TrimEnd('\\', '/');
            while (!string.IsNullOrEmpty(cur))
            {
                if (HasReparsePoint(cur))
                    throw new InvalidOperationException("Refusing to use or delete files when the folder contains a link or junction: " + cur);
                if (string.Equals(cur, stop, StringComparison.OrdinalIgnoreCase))
                    return;
                string parent = Path.GetDirectoryName(cur);
                if (string.IsNullOrEmpty(parent) || string.Equals(parent, cur, StringComparison.OrdinalIgnoreCase))
                    break;
                cur = parent.TrimEnd('\\', '/');
            }
            throw new InvalidOperationException("Path is outside the expected folder: " + filePath);
        }

        // Deletes identical files inside the mod's own folder only. Never touches game\, mods\skate2 or mods\.active-game.
        public static int RemoveIdenticalFiles(ModInfo mod, string gameExe, IEnumerable<ModCheckResult> results)
        {
            if (mod == null || string.IsNullOrEmpty(mod.Dir) || string.IsNullOrEmpty(gameExe) || results == null)
                throw new InvalidOperationException("Select a valid mod first.");

            string modsDir = Path.GetFullPath(Mods.Dir(gameExe));
            string modDir = Path.GetFullPath(mod.Dir);
            string gameRoot = Path.GetFullPath(FileUtil.GameRoot(gameExe));
            string skate2Dir = Path.GetFullPath(Path.Combine(modsDir, "skate2"));
            string activeDir = Path.GetFullPath(Mods.ActiveDir(gameExe));

            if (!FileUtil.Inside(modsDir, modDir) ||
                string.Equals(modDir, gameRoot, StringComparison.OrdinalIgnoreCase) || FileUtil.Inside(gameRoot, modDir) ||
                string.Equals(modDir, skate2Dir, StringComparison.OrdinalIgnoreCase) || FileUtil.Inside(skate2Dir, modDir) ||
                string.Equals(modDir, activeDir, StringComparison.OrdinalIgnoreCase) || FileUtil.Inside(activeDir, modDir))
                throw new InvalidOperationException("Refusing to delete files outside a normal mod folder.");

            if (HasReparsePoint(modDir))
                throw new InvalidOperationException("Refusing to delete files because the mod folder is a link or junction.");

            // Check every candidate file and its ancestor chain up to modDir (inclusive) before deleting anything.
            var toDelete = new List<string>();
            foreach (var r in results)
            {
                if (r == null || r.Kind != ModCheckKind.IdenticalToOriginal) continue;
                string target = !string.IsNullOrEmpty(r.FullPath) ? r.FullPath :
                    (!string.IsNullOrEmpty(r.FilePath) ? Path.Combine(mod.Root ?? mod.Dir, r.FilePath.Replace('/', '\\')) : null);
                if (string.IsNullOrEmpty(target)) continue;
                string full = Path.GetFullPath(target);
                if (!FileUtil.Inside(modDir, full) ||
                    FileUtil.Inside(gameRoot, full) ||
                    FileUtil.Inside(skate2Dir, full) ||
                    FileUtil.Inside(activeDir, full))
                    throw new InvalidOperationException("Refusing to delete a file outside the mod folder: " + full);

                EnsureNoReparsePointInChain(full, modDir);
                if (File.Exists(full) && !toDelete.Contains(full, StringComparer.OrdinalIgnoreCase))
                    toDelete.Add(full);
            }

            int removed = 0;
            foreach (var full in toDelete)
            {
                if (File.Exists(full))
                {
                    File.Delete(full);
                    removed++;
                }
            }
            return removed;
        }
    }

    // Packages a validated mod folder into <Name>-<version>.zip with a single top-level folder.
    static class ModPackager
    {
        public static string DefaultZipFileName(ModInfo mod)
        {
            string name = Paths.SafeName(mod != null && !string.IsNullOrEmpty(mod.Display) ? mod.Display : (mod != null ? mod.Name : "Mod"));
            if (string.IsNullOrEmpty(name)) name = "Mod";
            string ver = Paths.SafeName(mod != null && !string.IsNullOrEmpty(mod.Version) ? mod.Version.TrimStart('v', 'V') : "0.1.0");
            if (string.IsNullOrEmpty(ver)) ver = "0.1.0";
            return name + "-" + ver + ".zip";
        }

        public static string DesktopZipPath(ModInfo mod)
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), DefaultZipFileName(mod));
        }

        public static string Package(ModInfo mod, string zipPath, IList<ModCheckResult> checkResults)
        {
            if (mod == null || string.IsNullOrEmpty(mod.Dir) || !Directory.Exists(mod.Dir))
                throw new InvalidOperationException("Mod folder does not exist.");
            if (string.IsNullOrEmpty(zipPath))
                throw new ArgumentException("Specify a destination .zip path.");

            var checks = checkResults ?? ModValidator.Validate(mod, null);
            if (checks.Any(r => r.Severity == ModCheckSeverity.Error))
                throw new InvalidOperationException("Cannot package this mod while validation errors exist.");

            ModInfo fresh = Mods.Read(mod.Dir);
            string topFolder = Paths.SafeName(!string.IsNullOrEmpty(fresh.Name) ? fresh.Name : fresh.Display);
            if (string.IsNullOrEmpty(topFolder) || topFolder.StartsWith(".") || topFolder.StartsWith("_") || topFolder.Equals("skate2", StringComparison.OrdinalIgnoreCase))
                topFolder = "Mod-" + topFolder;

            string root = !string.IsNullOrEmpty(fresh.Root) && Directory.Exists(fresh.Root) ? fresh.Root : fresh.Dir;
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(zipPath)));

            var entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Include top-level metadata/doc files (mod.json, preview, README, LICENSE, CHANGELOG) from Dir and Root.
            foreach (var dir in new[] { fresh.Dir, root }.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                foreach (var f in new DirectoryInfo(dir).GetFiles("*", SearchOption.TopDirectoryOnly))
                {
                    if (IsIgnoredPackageFile(f.Name)) continue;
                    string entryName = topFolder + "/" + f.Name;
                    entries[entryName] = f.FullName;
                }
            }
            if (!string.IsNullOrEmpty(fresh.PreviewPath) && File.Exists(fresh.PreviewPath) && FileUtil.Inside(fresh.Dir, fresh.PreviewPath))
            {
                string relPrev = FileUtil.Inside(root, fresh.PreviewPath) ? FileUtil.Rel(root, fresh.PreviewPath) : Path.GetFileName(fresh.PreviewPath);
                if (!IsInDotFolder(relPrev) && !IsIgnoredPackageFile(Path.GetFileName(fresh.PreviewPath)))
                    entries[topFolder + "/" + relPrev] = fresh.PreviewPath;
            }

            // Include all mod files under Root.
            foreach (var f in new DirectoryInfo(root).GetFiles("*", SearchOption.AllDirectories))
            {
                if (IsIgnoredPackageFile(f.Name)) continue;
                string rel = FileUtil.Rel(root, f.FullName);
                if (IsInDotFolder(rel)) continue;
                if (GameFiles.Protected.Contains(rel.ToLowerInvariant())) continue;
                entries[topFolder + "/" + rel] = f.FullName;
            }

            string tmpZip = zipPath + ".tmp-launcher";
            try
            {
                if (File.Exists(tmpZip)) File.Delete(tmpZip);
                using (var fs = new FileStream(tmpZip, FileMode.Create, FileAccess.Write))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    foreach (var kv in entries.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                    {
                        var e = zip.CreateEntry(kv.Key, CompressionLevel.Optimal);
                        using (var src = new FileStream(kv.Value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                        using (var dst = e.Open())
                            src.CopyTo(dst);
                    }
                }
                if (File.Exists(zipPath)) File.Delete(zipPath);
                File.Move(tmpZip, zipPath);
            }
            catch
            {
                try { if (File.Exists(tmpZip)) File.Delete(tmpZip); } catch { }
                throw;
            }
            return zipPath;
        }

        static bool IsInDotFolder(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return false;
            var parts = relPath.Split('/', '\\');
            for (int i = 0; i < parts.Length - 1; i++)
                if (parts[i].StartsWith(".", StringComparison.Ordinal)) return true;
            return false;
        }

        static bool IsIgnoredPackageFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return true;
            string n = fileName.ToLowerInvariant();
            return n == ".base" || n == "thumbs.db" || n == "desktop.ini" || n == ".ds_store" || n.EndsWith(".tmp-launcher");
        }
    }

    // ====================================================================== M3: create a mod from changed files

    enum ModChangeStatus
    {
        Changed,
        New,
        Unchanged,
        Skipped
    }

    class ModChangeEntry
    {
        public string RelativePath; // '/' separators
        public string FullPath;     // full path inside workDir
        public ModChangeStatus Status;
        public long Size;
        public string SkipReason;   // non-null when Status == Skipped
    }

    static class ModFromChanges
    {
        public const int LargeSelectionFileWarningCount = 50;
        public const long LargeSelectionBytesWarning = 500L * 1024 * 1024;

        // Validates a work folder against the active Skate 3 install.
        // Returns a plain error message when invalid, or null when valid.
        // If the folder isn't itself a game\ copy but has a game\ subfolder holding default.xex, sets suggestedSubfolder.
        public static string ValidateWorkDir(string workDir, string gameExe, out string suggestedSubfolder)
        {
            suggestedSubfolder = null;
            if (string.IsNullOrEmpty(gameExe))
                return "Set the game location on the Launcher page first.";
            if (string.IsNullOrEmpty(workDir) || !Directory.Exists(workDir))
                return "Choose an existing work folder first.";

            string workFull;
            try { workFull = Path.GetFullPath(workDir); }
            catch { return "That work folder path is not valid."; }

            string exeDir = Path.GetFullPath(Path.GetDirectoryName(Path.GetFullPath(gameExe)));
            string gameDir = Path.GetFullPath(FileUtil.GameRoot(gameExe));
            string modsDir = Path.GetFullPath(Mods.Dir(gameExe));

            if (Mods.SameOrInside(gameDir, workFull))
                return "That's the game's own game\\ folder, not a separate work copy. Copy game\\ to another folder first and edit the copy.";
            if (Mods.SameOrInside(modsDir, workFull))
                return "That folder is inside mods\\. Pick a separate copy of the game\\ folder.";
            if (Mods.SameOrInside(workFull, gameDir) || Mods.SameOrInside(workFull, modsDir) || Mods.SameOrInside(workFull, exeDir))
                return "That's the game's own folder, not a separate work copy. Copy game\\ to another folder first and edit the copy.";
            if (ModValidator.HasReparsePoint(workFull))
                return "That work folder is a link or junction. Pick a real folder copy instead.";

            bool looksLikeGameCopy = File.Exists(Path.Combine(workFull, "default.xex")) || Directory.Exists(Path.Combine(workFull, "data"));
            string subGame = Path.Combine(workFull, "game");
            if (!looksLikeGameCopy && Directory.Exists(subGame) && !ModValidator.HasReparsePoint(subGame) && File.Exists(Path.Combine(subGame, "default.xex")))
                suggestedSubfolder = subGame;

            return null;
        }

        public static List<ModChangeEntry> Compare(string workDir, string gameExe)
        {
            return Compare(workDir, gameExe, null, null);
        }

        public static List<ModChangeEntry> Compare(string workDir, string gameExe, Action<long, long, string> progress, Func<bool> cancelled)
        {
            string suggested;
            string err = ValidateWorkDir(workDir, gameExe, out suggested);
            if (err != null) throw new InvalidOperationException(err);

            string workFull = Path.GetFullPath(workDir);
            string gameRoot = Path.GetFullPath(FileUtil.GameRoot(gameExe));
            if (!Directory.Exists(gameRoot))
                throw new InvalidOperationException("The game\\ folder was not found next to skate3.exe.");

            var entries = new List<ModChangeEntry>();
            var toHash = new List<ModChangeEntry>();
            long totalHashBytes = 0, doneHashBytes = 0;

            WalkWorkDirectory(new DirectoryInfo(workFull), workFull, gameRoot, entries, toHash, ref totalHashBytes, cancelled);

            foreach (var item in toHash)
            {
                if (cancelled != null && cancelled()) throw new OperationCanceledException();

                string currentRel = item.RelativePath;
                string workFile = item.FullPath;
                string gameFile = Path.Combine(gameRoot, currentRel.Replace('/', '\\'));

                long beforeWork = doneHashBytes;
                string workHash = FileUtil.Sha256(workFile, cancelled, n =>
                {
                    doneHashBytes += n;
                    if (progress != null) progress(doneHashBytes, totalHashBytes, currentRel);
                });
                doneHashBytes = beforeWork + item.Size;

                long beforeGame = doneHashBytes;
                string gameHash = FileUtil.Sha256(gameFile, cancelled, n =>
                {
                    doneHashBytes += n;
                    if (progress != null) progress(doneHashBytes, totalHashBytes, currentRel);
                });
                doneHashBytes = beforeGame + item.Size;

                item.Status = string.Equals(workHash, gameHash, StringComparison.OrdinalIgnoreCase)
                    ? ModChangeStatus.Unchanged
                    : ModChangeStatus.Changed;
            }

            return entries;
        }

        static void WalkWorkDirectory(DirectoryInfo dir, string workRoot, string gameRoot,
            List<ModChangeEntry> entries, List<ModChangeEntry> toHash, ref long totalHashBytes, Func<bool> cancelled)
        {
            if (cancelled != null && cancelled()) throw new OperationCanceledException();

            DirectoryInfo[] subDirs;
            try
            {
                subDirs = dir.GetDirectories("*", SearchOption.TopDirectoryOnly)
                    .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception ex)
            {
                throw new IOException("Couldn't read directory \"" + dir.FullName + "\": " + ex.Message, ex);
            }

            FileInfo[] files;
            try
            {
                files = dir.GetFiles("*", SearchOption.TopDirectoryOnly)
                    .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ToArray();
            }
            catch (Exception ex)
            {
                throw new IOException("Couldn't read files in \"" + dir.FullName + "\": " + ex.Message, ex);
            }

            foreach (var f in files)
            {
                if (cancelled != null && cancelled()) throw new OperationCanceledException();
                string rel = FileUtil.Rel(workRoot, f.FullName);
                if ((f.Attributes & FileAttributes.ReparsePoint) != 0 || ModValidator.HasReparsePoint(f.FullName))
                {
                    entries.Add(new ModChangeEntry
                    {
                        RelativePath = rel,
                        FullPath = f.FullName,
                        Status = ModChangeStatus.Skipped,
                        Size = 0,
                        SkipReason = "link / junction skipped"
                    });
                    continue;
                }
                if (GameFiles.Protected.Contains(rel.ToLowerInvariant()))
                {
                    entries.Add(new ModChangeEntry
                    {
                        RelativePath = rel,
                        FullPath = f.FullName,
                        Status = ModChangeStatus.Skipped,
                        Size = f.Length,
                        SkipReason = "can't be modded"
                    });
                    continue;
                }

                string gameFile = Path.Combine(gameRoot, rel.Replace('/', '\\'));
                if (!File.Exists(gameFile))
                {
                    entries.Add(new ModChangeEntry
                    {
                        RelativePath = rel,
                        FullPath = f.FullName,
                        Status = ModChangeStatus.New,
                        Size = f.Length
                    });
                }
                else
                {
                    long gameLen = new FileInfo(gameFile).Length;
                    if (f.Length != gameLen)
                    {
                        entries.Add(new ModChangeEntry
                        {
                            RelativePath = rel,
                            FullPath = f.FullName,
                            Status = ModChangeStatus.Changed,
                            Size = f.Length
                        });
                    }
                    else
                    {
                        var entry = new ModChangeEntry
                        {
                            RelativePath = rel,
                            FullPath = f.FullName,
                            Status = ModChangeStatus.Unchanged,
                            Size = f.Length
                        };
                        entries.Add(entry);
                        toHash.Add(entry);
                        totalHashBytes += f.Length * 2;
                    }
                }
            }

            foreach (var sub in subDirs)
            {
                if (cancelled != null && cancelled()) throw new OperationCanceledException();
                string relDir = FileUtil.Rel(workRoot, sub.FullName);
                if ((sub.Attributes & FileAttributes.ReparsePoint) != 0 || ModValidator.HasReparsePoint(sub.FullName))
                {
                    entries.Add(new ModChangeEntry
                    {
                        RelativePath = relDir,
                        FullPath = sub.FullName,
                        Status = ModChangeStatus.Skipped,
                        Size = 0,
                        SkipReason = "link / junction skipped"
                    });
                    continue;
                }
                WalkWorkDirectory(sub, workRoot, gameRoot, entries, toHash, ref totalHashBytes, cancelled);
            }
        }

        // Copies only the selected relative paths from workDir into mods\<modName>\ with a format-1 mod.json.
        // Uses a temporary staging folder under mods\ so a failed copy leaves nothing half-made.
        public static string Create(string gameExe, string workDir, string modName, IEnumerable<string> selectedRelPaths)
        {
            string suggested;
            string workErr = ValidateWorkDir(workDir, gameExe, out suggested);
            if (workErr != null) throw new InvalidOperationException(workErr);

            string cleanName;
            string nameErr = ModCreator.ValidateName(gameExe, modName, out cleanName);
            if (nameErr != null) throw new InvalidOperationException(nameErr);

            if (selectedRelPaths == null)
                throw new InvalidOperationException("Tick at least one changed or new file to include in the mod.");

            string workFull = Path.GetFullPath(workDir);
            var cleanRels = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var rawRel in selectedRelPaths)
            {
                if (string.IsNullOrEmpty(rawRel)) continue;
                string rel = rawRel.Trim().Replace('\\', '/').TrimStart('/');
                if (rel.Length == 0 || !seen.Add(rel)) continue;

                string why;
                if (!ModValidator.IsSafeRelativePath(workFull, rel, out why))
                    throw new InvalidOperationException(why);
                if (GameFiles.Protected.Contains(rel.ToLowerInvariant()))
                    throw new InvalidOperationException(rel + " is a protected executable and can't be modded.");

                string srcFile = Path.GetFullPath(Path.Combine(workFull, rel.Replace('/', '\\')));
                if (!FileUtil.Inside(workFull, srcFile) || !File.Exists(srcFile))
                    throw new FileNotFoundException("Selected file does not exist in the work folder: " + rel);

                ModValidator.EnsureNoReparsePointInChain(srcFile, workFull);
                cleanRels.Add(rel);
            }

            if (cleanRels.Count == 0)
                throw new InvalidOperationException("Tick at least one changed or new file to include in the mod.");

            string modsDir = Mods.Dir(gameExe);
            Directory.CreateDirectory(modsDir);
            string dest = Path.Combine(modsDir, cleanName);
            if (Directory.Exists(dest) || File.Exists(dest))
                throw new InvalidOperationException("A mod folder named \"" + cleanName + "\" already exists.");

            string tmp = Path.Combine(modsDir, "_creating-" + Guid.NewGuid().ToString("N"));
            try
            {
                Directory.CreateDirectory(tmp);
                foreach (var rel in cleanRels)
                {
                    string srcFile = Path.Combine(workFull, rel.Replace('/', '\\'));
                    string dstFile = Path.Combine(tmp, rel.Replace('/', '\\'));
                    if (!FileUtil.Inside(tmp, dstFile))
                        throw new InvalidOperationException("Invalid destination path: " + rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dstFile));
                    File.Copy(srcFile, dstFile, true);
                }

                File.WriteAllText(Path.Combine(tmp, "mod.json"), ModCreator.Format1ModJson(cleanName), new UTF8Encoding(false));
                Directory.Move(tmp, dest);
            }
            catch
            {
                FileUtil.TryDeleteDir(tmp);
                throw;
            }

            return dest;
        }
    }
}
