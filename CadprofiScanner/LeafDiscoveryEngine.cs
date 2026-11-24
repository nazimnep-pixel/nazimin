using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml.Linq;
using System.IO.Compression;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

namespace CadprofiScanner
{
    internal sealed class CadprofiDiscoveryResult
    {
        public List<string> ResolvedRoots { get; } = new List<string>();
        public List<string> SearchPathHits { get; } = new List<string>();
        public string PrimaryMenuTsv { get; set; }
        public string PrimaryCpConf { get; set; }
        public List<string> AllTsvFiles { get; } = new List<string>();
        public List<string> AllIniFiles { get; } = new List<string>();
        public List<string> AllCuixZipFiles { get; } = new List<string>();
        public List<string> AllCuixDirectories { get; } = new List<string>();
        public List<string> AllCuiFiles { get; } = new List<string>();
        public List<string> AllMnlFiles { get; } = new List<string>();
        public List<string> Assumptions { get; } = new List<string>();
        public int CuixZipCount => AllCuixZipFiles.Count;
        public int CuixDirectoryCount => AllCuixDirectories.Count;
        public int CuiFileCount => AllCuiFiles.Count;
        public int MnlFileCount => AllMnlFiles.Count;
    }

    internal static class CadprofiPathDiscovery
    {
        private static readonly string[] DefaultRoots =
        {
            @"C:\Program Files\CADprofi",
            @"C:\Program Files (x86)\CADprofi",
            @"C:\ProgramData\CADprofi",
            @"C:\CADprofi"
        };

        public static CadprofiDiscoveryResult Discover(Document doc)
        {
            var result = new CadprofiDiscoveryResult();
            var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddIfExists(string path)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    return;
                }

                var full = Path.GetFullPath(path.Trim().TrimEnd('\\', '/'));
                if (Directory.Exists(full))
                {
                    candidates.Add(full);
                }
            }

            var envHome = Environment.GetEnvironmentVariable("CADPROFI_HOME");
            if (!string.IsNullOrWhiteSpace(envHome))
            {
                AddIfExists(envHome);
                result.Assumptions.Add("CADPROFI_HOME environment variable honored when set.");
            }

            foreach (var root in DefaultRoots)
            {
                AddIfExists(root);
                if (Directory.Exists(root))
                {
                    foreach (var sub in Directory.EnumerateDirectories(root))
                    {
                        AddIfExists(sub);
                    }
                }
            }

            CollectAutoCadSupportPaths(doc, candidates, result);

            foreach (var path in candidates.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                result.ResolvedRoots.Add(path);
                ScanTree(path, result);
            }

            result.PrimaryMenuTsv = FindNamedFile(result.AllTsvFiles, "cp_menu_commands.tsv");
            result.PrimaryCpConf = FindNamedFile(result.AllIniFiles, "cpconf.ini");

            if (result.PrimaryMenuTsv == null)
            {
                result.Assumptions.Add(
                    "cp_menu_commands.tsv not found by exact name; hierarchy also built from other *.tsv menu files.");
            }

            if (result.PrimaryCpConf == null)
            {
                result.Assumptions.Add(
                    "cpconf.ini not found by exact name; size standards may come from other *.ini sections only.");
            }

            result.Assumptions.Add(
                "Version subfolders (e.g. 2024.0) are discovered under Program Files, ProgramData, and legacy C:\\CADprofi.");
            result.Assumptions.Add(
                "AutoCAD Support File Search Path entries containing 'CADprofi' or 'cadprofi' are merged.");

            return result;
        }

        private static void CollectAutoCadSupportPaths(
            Document doc,
            HashSet<string> candidates,
            CadprofiDiscoveryResult result)
        {
            try
            {
                var found = (string)AcApp.GetSystemVariable("ACADPREFIX");
                if (!string.IsNullOrWhiteSpace(found))
                {
                    foreach (var segment in found.Split(';'))
                    {
                        if (segment.IndexOf("cadprofi", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            if (Directory.Exists(segment))
                            {
                                candidates.Add(Path.GetFullPath(segment));
                                result.SearchPathHits.Add(segment);
                            }
                        }
                    }
                }
            }
            catch
            {
                // ACADPREFIX unavailable outside host.
            }

            try
            {
                var cfg = AcApp.GetProfileString("Options", "ACAD", null);
                if (!string.IsNullOrWhiteSpace(cfg))
                {
                    foreach (var segment in cfg.Split(';'))
                    {
                        if (segment.IndexOf("cadprofi", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            Directory.Exists(segment))
                        {
                            candidates.Add(Path.GetFullPath(segment));
                            result.SearchPathHits.Add(segment);
                        }
                    }
                }
            }
            catch
            {
                // Profile string may be unavailable.
            }
        }

        private static void ScanTree(string root, CadprofiDiscoveryResult result)
        {
            try
            {
                foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
                {
                    if (dir.EndsWith(".cuix", StringComparison.OrdinalIgnoreCase) &&
                        !result.AllCuixDirectories.Contains(dir))
                    {
                        result.AllCuixDirectories.Add(dir);
                    }
                }

                foreach (var file in Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories))
                {
                    var ext = Path.GetExtension(file);
                    if (ext.Equals(".tsv", StringComparison.OrdinalIgnoreCase))
                    {
                        result.AllTsvFiles.Add(file);
                    }
                    else if (ext.Equals(".ini", StringComparison.OrdinalIgnoreCase))
                    {
                        result.AllIniFiles.Add(file);
                    }
                    else if (ext.Equals(".cuix", StringComparison.OrdinalIgnoreCase))
                    {
                        if (IsZipCuix(file))
                        {
                            result.AllCuixZipFiles.Add(file);
                        }
                    }
                    else if (ext.Equals(".cui", StringComparison.OrdinalIgnoreCase))
                    {
                        result.AllCuiFiles.Add(file);
                    }
                    else if (ext.Equals(".mnl", StringComparison.OrdinalIgnoreCase))
                    {
                        result.AllMnlFiles.Add(file);
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip protected subtrees.
            }
        }

        private static bool IsZipCuix(string path)
        {
            try
            {
                using (var fs = File.OpenRead(path))
                {
                    if (fs.Length < 4)
                    {
                        return false;
                    }

                    var header = new byte[4];
                    return fs.Read(header, 0, 4) == 4 &&
                           header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string FindNamedFile(IEnumerable<string> files, string exactName)
        {
            return files.FirstOrDefault(f =>
                string.Equals(Path.GetFileName(f), exactName, StringComparison.OrdinalIgnoreCase));
        }
    }

    #endregion

    internal sealed class MasterKnowledgeStaticTree
    {
        public List<CpModuleNode> Modules { get; } = new List<CpModuleNode>();
        public Dictionary<string, JObject> IniSnapshots { get; } = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
        public List<TsvFileSummary> TsvSources { get; } = new List<TsvFileSummary>();
        public List<MenuSourceSummary> MenuSources { get; } = new List<MenuSourceSummary>();
        public int TotalCommandRows { get; set; }
    }

    internal sealed class MenuSourceSummary
    {
        public string Path { get; set; }
        public string Kind { get; set; }
        public int MacroCount { get; set; }
    }

    internal sealed class CpModuleNode
    {
        public string Name { get; set; }
        public Dictionary<string, CpCategoryNode> Categories { get; } =
            new Dictionary<string, CpCategoryNode>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class CpCategoryNode
    {
        public string Name { get; set; }
        public Dictionary<string, CpTypeNode> Types { get; } =
            new Dictionary<string, CpTypeNode>(StringComparer.OrdinalIgnoreCase);
    }

    internal sealed class CpTypeNode
    {
        public string Name { get; set; }
        public HashSet<string> SizeStandards { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public List<MenuCommandRow> Commands { get; } = new List<MenuCommandRow>();
    }

    internal sealed class MenuCommandRow
    {
        public string Module { get; set; }
        public string Category { get; set; }
        public string Type { get; set; }
        public string CommandLine { get; set; }
        public string SizeStandard { get; set; }
        public string SourceFile { get; set; }
        public int LineNumber { get; set; }
        public string MacroUid { get; set; }
        public string Caption { get; set; }
        public string HelpString { get; set; }
        public string Description { get; set; }
        public string ToolTip { get; set; }
    }

    internal sealed class CadprofiLeafRecord
    {
        public MenuCommandRow Row { get; set; }
        public string LeafKey { get; set; }
        public string RunnableState { get; set; }
        public string NonRunnableReason { get; set; }
        public bool IsArchitecture { get; set; }
        public bool IsRunnable { get; set; }
    }

    internal sealed class TsvFileSummary
    {
        public string Path { get; set; }
        public string[] Headers { get; set; }
        public int RowCount { get; set; }
    }

    internal static class StaticTreeBuilder
    {
        private static readonly string[] DrawCommandHints =
        {
            "DRAW", "DÇİZ", "INSERT", "BLOCK", "CP_", "SYMBOL", "PARAM"
        };

        private static readonly string[] ModuleHeaderNames = { "module", "modul", "modül", "program" };
        private static readonly string[] CategoryHeaderNames = { "category", "kateqoriya", "kat", "group", "qrup" };
        private static readonly string[] TypeHeaderNames = { "type", "tip", "symbol", "simvol", "element" };
        private static readonly string[] CommandHeaderNames = { "command", "komanda", "cmd", "macro", "Əmr" };
        private static readonly string[] SizeHeaderNames = { "size", "standard", "dn", "nominal", "ölçü" };

        public static MasterKnowledgeStaticTree Build(CadprofiDiscoveryResult discovery)
        {
            var tree = new MasterKnowledgeStaticTree();
            var sizeHints = LoadSizeStandardsFromIni(discovery, tree);

            var tsvFiles = discovery.AllTsvFiles
                .OrderBy(f => Path.GetFileName(f).Equals("cp_menu_commands.tsv", StringComparison.OrdinalIgnoreCase)
                    ? 0
                    : 1)
                .ThenBy(f => f, StringComparer.OrdinalIgnoreCase);

            foreach (var tsvPath in tsvFiles)
            {
                ParseTsvFile(tsvPath, tree, sizeHints);
            }

            CadprofiCuixMenuParser.MergeIntoStaticTree(discovery, tree);

            return tree;
        }

        public static List<MenuCommandRow> ExtractDrawableCommands(
            MasterKnowledgeStaticTree tree,
            CadprofiDiscoveryResult discovery)
        {
            return DiscoverLeaves(tree, discovery)
                .Where(l => l.IsRunnable)
                .Select(l => l.Row)
                .GroupBy(r => LeafDiscoveryEngine.BuildLeafKey(r), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();
        }

        public static List<CadprofiLeafRecord> DiscoverLeaves(
            MasterKnowledgeStaticTree tree,
            CadprofiDiscoveryResult discovery)
        {
            var leaves = tree.Modules
                .SelectMany(m => m.Categories.Values)
                .SelectMany(c => c.Types.Values)
                .SelectMany(t => t.Commands)
                .Where(r => !string.IsNullOrWhiteSpace(r.CommandLine))
                .Select(r => LeafDiscoveryEngine.ToLeafRecord(r))
                .GroupBy(l => l.LeafKey, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (leaves.Count == 0 && !string.IsNullOrWhiteSpace(discovery.PrimaryMenuTsv))
            {
                foreach (var row in ParseTsvForCommandsOnly(discovery.PrimaryMenuTsv))
                {
                    leaves.Add(LeafDiscoveryEngine.ToLeafRecord(row));
                }
            }

            return leaves;
        }

        private static bool LooksLikeDrawCommand(MenuCommandRow row)
        {
            var cmd = row.CommandLine.Trim();
            if (cmd.Length < 2)
            {
                return false;
            }

            if (cmd.StartsWith(";", StringComparison.Ordinal) || cmd.StartsWith("#", StringComparison.Ordinal))
            {
                return false;
            }

            var upper = cmd.ToUpperInvariant();
            if (DrawCommandHints.Any(h => upper.Contains(h)))
            {
                return true;
            }

            return upper.StartsWith("CP", StringComparison.Ordinal) ||
                   upper.Contains("_") ||
                   !upper.All(ch => char.IsLetter(ch));
        }

        private static List<MenuCommandRow> ParseTsvForCommandsOnly(string tsvPath)
        {
            var list = new List<MenuCommandRow>();
            var lines = File.ReadAllLines(tsvPath, Encoding.UTF8);
            if (lines.Length == 0)
            {
                return list;
            }

            var headers = SplitTsvLine(lines[0]);
            var cmdIndex = FindColumnIndex(headers, CommandHeaderNames);
            if (cmdIndex < 0 && headers.Length > 0)
            {
                cmdIndex = headers.Length - 1;
            }

            for (var i = 1; i < lines.Length; i++)
            {
                var cells = SplitTsvLine(lines[i]);
                if (cells.Length <= cmdIndex)
                {
                    continue;
                }

                var cmd = cells[cmdIndex].Trim();
                if (string.IsNullOrWhiteSpace(cmd) || !LooksLikeDrawCommand(new MenuCommandRow { CommandLine = cmd }))
                {
                    continue;
                }

                list.Add(new MenuCommandRow
                {
                    CommandLine = cmd,
                    SourceFile = tsvPath,
                    LineNumber = i + 1,
                    Module = "Unknown",
                    Category = "Menu",
                    Type = Path.GetFileName(tsvPath)
                });
            }

            return list;
        }

        private static HashSet<string> LoadSizeStandardsFromIni(
            CadprofiDiscoveryResult discovery,
            MasterKnowledgeStaticTree tree)
        {
            var sizes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var iniPriority = discovery.AllIniFiles
                .OrderBy(f => Path.GetFileName(f).Equals("cpconf.ini", StringComparison.OrdinalIgnoreCase) ? 0 : 1);

            foreach (var iniPath in iniPriority)
            {
                var parsed = IniParser.ParseFile(iniPath);
                tree.IniSnapshots[iniPath] = parsed;

                foreach (var prop in parsed.Properties())
                {
                    var section = prop as JProperty;
                    if (section?.Value is JObject sectionObj)
                    {
                        foreach (var kv in sectionObj.Properties())
                        {
                            var key = kv.Name;
                            var val = kv.Value?.ToString() ?? string.Empty;
                            if (SizeHeaderNames.Any(s => key.IndexOf(s, StringComparison.OrdinalIgnoreCase) >= 0))
                            {
                                sizes.Add(val);
                            }

                            if (Regex.IsMatch(key, @"\bDN\d+\b", RegexOptions.IgnoreCase))
                            {
                                sizes.Add(key);
                            }
                        }
                    }
                }
            }

            return sizes;
        }

        private static void ParseTsvFile(string tsvPath, MasterKnowledgeStaticTree tree, HashSet<string> sizeHints)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(tsvPath, Encoding.UTF8);
            }
            catch
            {
                try
                {
                    lines = File.ReadAllLines(tsvPath, Encoding.GetEncoding(1252));
                }
                catch
                {
                    return;
                }
            }

            if (lines.Length == 0)
            {
                return;
            }

            var headers = SplitTsvLine(lines[0]);
            var hasHeader = HeadersLookLikeNames(headers);
            var startRow = hasHeader ? 1 : 0;

            var modIdx = FindColumnIndex(headers, ModuleHeaderNames);
            var catIdx = FindColumnIndex(headers, CategoryHeaderNames);
            var typeIdx = FindColumnIndex(headers, TypeHeaderNames);
            var cmdIdx = FindColumnIndex(headers, CommandHeaderNames);
            var sizeIdx = FindColumnIndex(headers, SizeHeaderNames);

            FallbackColumnLayout(headers.Length, ref modIdx, ref catIdx, ref typeIdx, ref cmdIdx, ref sizeIdx);

            tree.TsvSources.Add(new TsvFileSummary
            {
                Path = tsvPath,
                Headers = headers,
                RowCount = Math.Max(0, lines.Length - startRow)
            });

            for (var i = startRow; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var cells = SplitTsvLine(line);
                if (cells.Length == 0)
                {
                    continue;
                }

                var module = GetCell(cells, modIdx, "General");
                var category = GetCell(cells, catIdx, "Default");
                var type = GetCell(cells, typeIdx, Path.GetFileNameWithoutExtension(tsvPath));
                var command = GetCell(cells, cmdIdx, string.Empty);
                var size = GetCell(cells, sizeIdx, string.Empty);

                if (string.IsNullOrWhiteSpace(command))
                {
                    continue;
                }

                tree.TotalCommandRows++;
                var modNode = GetOrCreateModule(tree, module);
                var catNode = GetOrCreateCategory(modNode, category);
                var typeNode = GetOrCreateType(catNode, type);

                if (!string.IsNullOrWhiteSpace(size))
                {
                    typeNode.SizeStandards.Add(size);
                }

                foreach (var hint in sizeHints)
                {
                    if (line.IndexOf(hint, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        typeNode.SizeStandards.Add(hint);
                    }
                }

                typeNode.Commands.Add(new MenuCommandRow
                {
                    Module = module,
                    Category = category,
                    Type = type,
                    CommandLine = command,
                    SizeStandard = size,
                    SourceFile = tsvPath,
                    LineNumber = i + 1
                });
            }
        }

        private static void FallbackColumnLayout(
            int colCount,
            ref int modIdx,
            ref int catIdx,
            ref int typeIdx,
            ref int cmdIdx,
            ref int sizeIdx)
        {
            if (colCount <= 0)
            {
                return;
            }

            if (cmdIdx < 0)
            {
                cmdIdx = colCount - 1;
            }

            if (modIdx < 0 && colCount > 3)
            {
                modIdx = 0;
            }

            if (catIdx < 0 && colCount > 2)
            {
                catIdx = Math.Min(1, colCount - 1);
            }

            if (typeIdx < 0 && colCount > 1)
            {
                typeIdx = Math.Min(2, colCount - 1);
            }

            if (sizeIdx < 0 && colCount > 4)
            {
                sizeIdx = 3;
            }
        }

        private static bool HeadersLookLikeNames(string[] headers)
        {
            if (headers == null || headers.Length == 0)
            {
                return false;
            }

            var joined = string.Join(" ", headers).ToLowerInvariant();
            return ModuleHeaderNames.Any(h => joined.Contains(h)) ||
                   CommandHeaderNames.Any(h => joined.Contains(h)) ||
                   joined.Contains("tab") ||
                   joined.Contains("menu");
        }

        private static int FindColumnIndex(string[] headers, string[] aliases)
        {
            for (var i = 0; i < headers.Length; i++)
            {
                var h = headers[i].Trim().ToLowerInvariant();
                if (aliases.Any(a => h.Contains(a)))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string GetCell(string[] cells, int index, string fallback)
        {
            if (index < 0 || index >= cells.Length)
            {
                return fallback;
            }

            var v = cells[index].Trim();
            return string.IsNullOrWhiteSpace(v) ? fallback : v;
        }

        private static string[] SplitTsvLine(string line)
        {
            return line.Split('\t');
        }

        private static CpModuleNode GetOrCreateModule(MasterKnowledgeStaticTree tree, string name)
        {
            var existing = tree.Modules.FirstOrDefault(m =>
                string.Equals(m.Name, name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            var node = new CpModuleNode { Name = name };
            tree.Modules.Add(node);
            return node;
        }

        private static CpCategoryNode GetOrCreateCategory(CpModuleNode module, string name)
        {
            if (!module.Categories.TryGetValue(name, out var cat))
            {
                cat = new CpCategoryNode { Name = name };
                module.Categories[name] = cat;
            }

            return cat;
        }

        private static CpTypeNode GetOrCreateType(CpCategoryNode category, string name)
        {
            if (!category.Types.TryGetValue(name, out var type))
            {
                type = new CpTypeNode { Name = name };
                category.Types[name] = type;
            }

            return type;
        }
    }

    internal static class IniParser
    {
        public static JObject ParseFile(string path)
        {
            var root = new JObject();
            JObject section = root;
            const string defaultSection = "_default";

            foreach (var rawLine in File.ReadAllLines(path, Encoding.UTF8))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) ||
                    line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.StartsWith("[") && line.EndsWith("]"))
                {
                    var sectionName = line.Substring(1, line.Length - 2).Trim();
                    section = new JObject();
                    root[sectionName] = section;
                    continue;
                }

                var eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                if (section == root && root[defaultSection] == null)
                {
                    section = new JObject();
                    root[defaultSection] = section;
                }

                var key = line.Substring(0, eq).Trim();
                var value = line.Substring(eq + 1).Trim();
                section[key] = value;
            }

            return root;
        }
    }

    /// <summary>Engine 1 — filesystem discovery, static hierarchy, and menu leaf enumeration.</summary>
    internal static class LeafDiscoveryEngine
    {
        public static CadprofiDiscoveryResult Discover(Document doc)
        {
            return CadprofiPathDiscovery.Discover(doc);
        }

        public static MasterKnowledgeStaticTree BuildStatic(CadprofiDiscoveryResult discovery)
        {
            return StaticTreeBuilder.Build(discovery);
        }

        public static List<CadprofiLeafRecord> EnumerateLeaves(
            MasterKnowledgeStaticTree tree,
            CadprofiDiscoveryResult discovery)
        {
            return StaticTreeBuilder.DiscoverLeaves(tree, discovery);
        }

        public static string BuildLeafKey(MenuCommandRow row)
        {
            return string.Join("|", new[]
            {
                row.Module ?? string.Empty,
                row.Category ?? string.Empty,
                row.Type ?? string.Empty,
                row.CommandLine?.Trim() ?? string.Empty
            });
        }

        public static CadprofiLeafRecord ToLeafRecord(MenuCommandRow row)
        {
            var macro = row.CommandLine?.Trim() ?? string.Empty;
            var isArch = IsArchitectureUid(row.MacroUid) ||
                         string.Equals(row.Module, "Architecture", StringComparison.OrdinalIgnoreCase);
            var runnable = CadprofiCuixMenuParser.IsRunnableMacro(macro);
            var skip = ScanCommandInteraction.ShouldSkipMacro(macro);

            string state;
            string reason = null;
            if (isArch)
            {
                state = "non_runnable";
                reason = "architecture_module_skipped";
            }
            else if (skip.Skip)
            {
                state = "non_runnable";
                reason = skip.Reason ?? "requires_user_input";
            }
            else if (!runnable)
            {
                state = "static_only";
                reason = "non_runnable_macro";
            }
            else
            {
                state = "runnable_not_scanned";
            }

            return new CadprofiLeafRecord
            {
                Row = row,
                LeafKey = BuildLeafKey(row),
                RunnableState = state,
                NonRunnableReason = reason,
                IsArchitecture = isArch,
                IsRunnable = state.StartsWith("runnable", StringComparison.OrdinalIgnoreCase)
            };
        }

        private static bool IsArchitectureUid(string uid)
        {
            return !string.IsNullOrWhiteSpace(uid) &&
                   uid.StartsWith("ID_ARCH_", StringComparison.OrdinalIgnoreCase);
        }
    }
}
