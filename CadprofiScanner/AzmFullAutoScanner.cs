using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

[assembly: ExtensionApplication(typeof(CadprofiScanner.AzmFullAutoScannerApp))]

namespace CadprofiScanner
{
    /// <summary>
    /// Autonomous CADprofi knowledge extractor — no user interaction during <see cref="ScanAll"/>.
    /// </summary>
    public sealed class AzmFullAutoScannerApp : IExtensionApplication
    {
        public void Initialize() { }

        public void Terminate() { }
    }

    public static class AzmFullAutoScanner
    {
        private const string MasterJsonFileName = "cadprofi_master_knowledge.json";
        private const int ReferenceSpan = 2000;
        private const int CommandSettleMs = 350;
        private const int MaxDrawCommandsPerRun = 500;

        [CommandMethod("AZM_SCAN_ALL", CommandFlags.Session)]
        public static void ScanAll()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            try
            {
                ed.WriteMessage("\nAZMEXANIKA: CADprofi tam analiz başladı...\n");

                var discovery = CadprofiPathDiscovery.Discover(doc);
                ed.WriteMessage($"\nAZMEXANIKA: {discovery.ResolvedRoots.Count} CADprofi qovluğu tapıldı.\n");

                var staticTree = StaticTreeBuilder.Build(discovery);
                ed.WriteMessage(
                    $"\nAZMEXANIKA: Statik iyerarxiya — {staticTree.Modules.Count} modul, {staticTree.TotalCommandRows} əmr sətiri.\n");

                var drawCommands = StaticTreeBuilder.ExtractDrawableCommands(staticTree, discovery);
                if (drawCommands.Count > MaxDrawCommandsPerRun)
                {
                    drawCommands = drawCommands.Take(MaxDrawCommandsPerRun).ToList();
                }

                var dynamic = DynamicGeometryScanner.Run(doc, drawCommands);
                ed.WriteMessage(
                    $"\nAZMEXANIKA: Dinamik skan — {dynamic.Samples.Count} nümunə, {dynamic.FailedCommands.Count} uğursuz əmr.\n");

                var exportPath = KnowledgeExporter.Export(doc, discovery, staticTree, dynamic);
                ed.WriteMessage(
                    $"\nAZMEXANIKA: CADprofi tam analiz olundu və master JSON faylı yaradıldı!\n");
                ed.WriteMessage($"\nAZMEXANIKA: {exportPath}\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nAZMEXANIKA: Xəta — {ex.Message}\n");
            }
        }
    }

    #region Path discovery

    internal sealed class CadprofiDiscoveryResult
    {
        public List<string> ResolvedRoots { get; } = new List<string>();
        public List<string> SearchPathHits { get; } = new List<string>();
        public string PrimaryMenuTsv { get; set; }
        public string PrimaryCpConf { get; set; }
        public List<string> AllTsvFiles { get; } = new List<string>();
        public List<string> AllIniFiles { get; } = new List<string>();
        public List<string> Assumptions { get; } = new List<string>();
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
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Skip protected subtrees.
            }
        }

        private static string FindNamedFile(IEnumerable<string> files, string exactName)
        {
            return files.FirstOrDefault(f =>
                string.Equals(Path.GetFileName(f), exactName, StringComparison.OrdinalIgnoreCase));
        }
    }

    #endregion

    #region Static hierarchy

    internal sealed class MasterKnowledgeStaticTree
    {
        public List<CpModuleNode> Modules { get; } = new List<CpModuleNode>();
        public Dictionary<string, JObject> IniSnapshots { get; } = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
        public List<TsvFileSummary> TsvSources { get; } = new List<TsvFileSummary>();
        public int TotalCommandRows { get; set; }
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

            return tree;
        }

        public static List<MenuCommandRow> ExtractDrawableCommands(
            MasterKnowledgeStaticTree tree,
            CadprofiDiscoveryResult discovery)
        {
            var rows = tree.Modules
                .SelectMany(m => m.Categories.Values)
                .SelectMany(c => c.Types.Values)
                .SelectMany(t => t.Commands)
                .Where(r => !string.IsNullOrWhiteSpace(r.CommandLine))
                .Where(LooksLikeDrawCommand)
                .GroupBy(r => r.CommandLine.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            if (rows.Count == 0 && !string.IsNullOrWhiteSpace(discovery.PrimaryMenuTsv))
            {
                rows = ParseTsvForCommandsOnly(discovery.PrimaryMenuTsv);
            }

            return rows;
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

    #endregion

    #region Dynamic geometry scan

    internal sealed class DynamicScanResult
    {
        public List<GeometrySample> Samples { get; } = new List<GeometrySample>();
        public List<string> FailedCommands { get; } = new List<string>();
        public ReferenceLinesCalibration Calibration { get; set; }
    }

    internal sealed class ReferenceLinesCalibration
    {
        public double[] CenterVector { get; set; }
        public double WallOffset { get; set; }
    }

    internal sealed class GeometrySample
    {
        public string Command { get; set; }
        public double[] CenterVector { get; set; }
        public double? WallOffset { get; set; }
        public ElbowMetrics Elbow { get; set; }
        public Dictionary<string, object> ExtensionDictionary { get; set; }
        public Dictionary<string, List<string>> XData { get; set; }
        public List<string> EntityTypes { get; set; }
    }

    internal sealed class ElbowMetrics
    {
        public double? RIn { get; set; }
        public double? ROut { get; set; }
        public double? W { get; set; }
    }

    internal static class DynamicGeometryScanner
    {
        public static DynamicScanResult Run(Document doc, List<MenuCommandRow> commands)
        {
            var result = new DynamicScanResult();
            var db = doc.Database;

            using (doc.LockDocument())
            {
                result.Calibration = DrawReferenceLines(db);

                foreach (var row in commands)
                {
                    var cmd = NormalizeCommand(row.CommandLine);
                    if (string.IsNullOrWhiteSpace(cmd))
                    {
                        continue;
                    }

                    try
                    {
                        var before = CollectEntityHandles(db);
                        ExecuteCommand(doc, cmd);
                        var afterIds = CollectNewEntityIds(db, before);
                        var sample = ReadGeometry(db, cmd, afterIds);
                        if (sample != null)
                        {
                            result.Samples.Add(sample);
                        }

                        EraseEntities(db, afterIds);
                    }
                    catch (Exception)
                    {
                        result.FailedCommands.Add(cmd);
                    }
                }
            }

            return result;
        }

        private static string NormalizeCommand(string commandLine)
        {
            var cmd = commandLine.Trim();
            if (cmd.StartsWith("_", StringComparison.Ordinal))
            {
                return cmd;
            }

            if (cmd.IndexOf(' ') > 0)
            {
                return cmd;
            }

            return "_" + cmd;
        }

        private static void ExecuteCommand(Document doc, string command)
        {
            doc.SendStringToExecute(command + "\n", true, false, false);
            Thread.Sleep(CommandSettleMs);
        }

        private static ReferenceLinesCalibration DrawReferenceLines(Database db)
        {
            var calibration = new ReferenceLinesCalibration();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                var lineA = new Line(new Point3d(0, 0, 0), new Point3d(ReferenceSpan, 0, 0));
                var lineB = new Line(new Point3d(ReferenceSpan, 0, 0), new Point3d(ReferenceSpan, ReferenceSpan, 0));
                ms.AppendEntity(lineA);
                ms.AppendEntity(lineB);
                tr.AddNewlyCreatedDBObject(lineA, true);
                tr.AddNewlyCreatedDBObject(lineB, true);

                var mid = new Point3d(ReferenceSpan * 0.5, ReferenceSpan * 0.25, 0);
                calibration.CenterVector = new[] { mid.X, mid.Y, mid.Z };
                calibration.WallOffset = Math.Abs(lineB.StartPoint.Y - lineA.StartPoint.Y);

                lineA.Erase();
                lineB.Erase();
                tr.Commit();
            }

            return calibration;
        }

        private static HashSet<string> CollectEntityHandles(Database db)
        {
            var handles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    if (!id.IsErased)
                    {
                        handles.Add(id.Handle.ToString());
                    }
                }

                tr.Commit();
            }

            return handles;
        }

        private static List<ObjectId> CollectNewEntityIds(Database db, HashSet<string> beforeHandles)
        {
            var ids = new List<ObjectId>();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
                foreach (ObjectId id in ms)
                {
                    if (id.IsErased)
                    {
                        continue;
                    }

                    var handle = id.Handle.ToString();
                    if (!beforeHandles.Contains(handle))
                    {
                        ids.Add(id);
                    }
                }

                tr.Commit();
            }

            return ids;
        }

        private static GeometrySample ReadGeometry(Database db, string command, List<ObjectId> entityIds)
        {
            if (entityIds.Count == 0)
            {
                return new GeometrySample
                {
                    Command = command,
                    CenterVector = new[] { 0.0, 0.0, 0.0 },
                    EntityTypes = new List<string>()
                };
            }

            var sample = new GeometrySample
            {
                Command = command,
                ExtensionDictionary = new Dictionary<string, object>(),
                XData = new Dictionary<string, List<string>>(),
                EntityTypes = new List<string>()
            };

            var points = new List<Point3d>();
            ElbowMetrics elbow = null;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in entityIds)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null)
                    {
                        continue;
                    }

                    sample.EntityTypes.Add(ent.GetType().Name);
                    CollectEntityPoints(ent, points);
                    elbow = elbow ?? TryReadElbowMetrics(ent);

                    foreach (var pair in ExtensionDictionaryReader.Read(ent, tr))
                    {
                        sample.ExtensionDictionary[pair.Key] = pair.Value;
                    }

                    foreach (var xdata in XDataReader.ReadAll(ent))
                    {
                        if (!sample.XData.ContainsKey(xdata.Key))
                        {
                            sample.XData[xdata.Key] = new List<string>();
                        }

                        sample.XData[xdata.Key].AddRange(xdata.Value);
                    }
                }

                tr.Commit();
            }

            sample.CenterVector = ComputeCenterVector(points);
            sample.WallOffset = ComputeWallOffset(points);
            sample.Elbow = elbow;
            return sample;
        }

        private static void EraseEntities(Database db, List<ObjectId> entityIds)
        {
            if (entityIds.Count == 0)
            {
                return;
            }

            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in entityIds)
                {
                    var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                    ent?.Erase();
                }

                tr.Commit();
            }
        }

        private static void CollectEntityPoints(Entity ent, List<Point3d> points)
        {
            switch (ent)
            {
                case Line line:
                    points.Add(line.StartPoint);
                    points.Add(line.EndPoint);
                    break;
                case Arc arc:
                    points.Add(arc.Center);
                    points.Add(arc.StartPoint);
                    points.Add(arc.EndPoint);
                    break;
                case Circle circle:
                    points.Add(circle.Center);
                    break;
                case BlockReference br:
                    points.Add(br.Position);
                    break;
                case Polyline pl:
                    for (var i = 0; i < pl.NumberOfVertices; i++)
                    {
                        points.Add(pl.GetPoint3dAt(i));
                    }

                    break;
            }
        }

        private static double[] ComputeCenterVector(List<Point3d> points)
        {
            if (points.Count == 0)
            {
                return new[] { 0.0, 0.0, 0.0 };
            }

            var x = points.Average(p => p.X);
            var y = points.Average(p => p.Y);
            var z = points.Average(p => p.Z);
            return new[] { x, y, z };
        }

        private static double? ComputeWallOffset(List<Point3d> points)
        {
            if (points.Count < 2)
            {
                return null;
            }

            var minX = points.Min(p => p.X);
            var maxY = points.Max(p => p.Y);
            return Math.Abs(maxY - minX);
        }

        private static ElbowMetrics TryReadElbowMetrics(Entity ent)
        {
            var metrics = new ElbowMetrics();
            var textBlobs = new List<string>();

            void AddBlob(string s)
            {
                if (!string.IsNullOrWhiteSpace(s))
                {
                    textBlobs.Add(s);
                }
            }

            if (ent is Arc arc)
            {
                metrics.RIn = arc.Radius;
                metrics.ROut = arc.Radius;
                AddBlob($"R={arc.Radius.ToString(CultureInfo.InvariantCulture)}");
            }

            foreach (var xdata in XDataReader.ReadAll(ent))
            {
                foreach (var val in xdata.Value)
                {
                    AddBlob(val);
                }
            }

            foreach (var blob in textBlobs)
            {
                var rin = MatchDouble(blob, @"R_in\s*[=:]\s*([\d.,]+)", @"Rin\s*[=:]\s*([\d.,]+)");
                var rout = MatchDouble(blob, @"R_out\s*[=:]\s*([\d.,]+)", @"Rout\s*[=:]\s*([\d.,]+)");
                var w = MatchDouble(blob, @"\bW\s*[=:]\s*([\d.,]+)", @"\bWidth\s*[=:]\s*([\d.,]+)");
                if (rin.HasValue)
                {
                    metrics.RIn = rin;
                }

                if (rout.HasValue)
                {
                    metrics.ROut = rout;
                }

                if (w.HasValue)
                {
                    metrics.W = w;
                }
            }

            if (!metrics.RIn.HasValue && !metrics.ROut.HasValue && !metrics.W.HasValue)
            {
                return null;
            }

            return metrics;
        }

        private static double? MatchDouble(string text, params string[] patterns)
        {
            foreach (var pattern in patterns)
            {
                var m = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
                if (m.Success && double.TryParse(m.Groups[1].Value.Replace(',', '.'),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                {
                    return d;
                }
            }

            return null;
        }
    }

    internal static class ExtensionDictionaryReader
    {
        public static IEnumerable<KeyValuePair<string, object>> Read(Entity ent, Transaction tr)
        {
            if (ent.ExtensionDictionary == ObjectId.Null)
            {
                yield break;
            }

            var dict = tr.GetObject(ent.ExtensionDictionary, OpenMode.ForRead) as DBDictionary;
            if (dict == null)
            {
                yield break;
            }

            foreach (var entry in dict)
            {
                var val = tr.GetObject(entry.Value, OpenMode.ForRead);
                yield return new KeyValuePair<string, object>(entry.Key, FormatObject(val));
            }
        }

        private static object FormatObject(DBObject obj)
        {
            switch (obj)
            {
                case Xrecord xrec:
                    var parts = new List<string>();
                    foreach (var tv in xrec.Data)
                    {
                        parts.Add(FormatTypedValue(tv));
                    }

                    return parts;
                case DBDictionary nested:
                    return nested.Count;
                default:
                    return obj.GetType().Name;
            }
        }

        private static string FormatTypedValue(TypedValue tv)
        {
            return tv.Value?.ToString() ?? string.Empty;
        }
    }

    internal static class XDataReader
    {
        public static Dictionary<string, List<string>> ReadAll(Entity ent)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var rb = ent.XData;
            if (rb == null)
            {
                return result;
            }

            string currentApp = null;
            foreach (TypedValue tv in rb)
            {
                if (tv.TypeCode == (int)DxfCode.ExtendedDataRegAppName)
                {
                    currentApp = tv.Value?.ToString() ?? "unknown";
                    if (!result.ContainsKey(currentApp))
                    {
                        result[currentApp] = new List<string>();
                    }
                }
                else if (currentApp != null)
                {
                    result[currentApp].Add(FormatTypedValue(tv));
                }
            }

            return result;
        }

        private static string FormatTypedValue(TypedValue tv)
        {
            return $"{tv.TypeCode}:{tv.Value}";
        }
    }

    #endregion

    #region Export

    internal static class KnowledgeExporter
    {
        public static string Export(
            Document doc,
            CadprofiDiscoveryResult discovery,
            MasterKnowledgeStaticTree staticTree,
            DynamicScanResult dynamic)
        {
            var drawingDir = Path.GetDirectoryName(doc.Database.Filename);
            if (string.IsNullOrWhiteSpace(drawingDir))
            {
                drawingDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            var exportPath = Path.Combine(drawingDir, MasterJsonFileName);

            var payload = new JObject
            {
                ["schema"] = "cadprofi-master-knowledge/v1",
                ["generatedAt"] = DateTime.Now.ToString("o"),
                ["generator"] = "AzmFullAutoScanner.AZM_SCAN_ALL",
                ["discovery"] = new JObject
                {
                    ["resolvedRoots"] = JArray.FromObject(discovery.ResolvedRoots),
                    ["searchPathHits"] = JArray.FromObject(discovery.SearchPathHits),
                    ["primaryMenuTsv"] = discovery.PrimaryMenuTsv,
                    ["primaryCpConf"] = discovery.PrimaryCpConf,
                    ["assumptions"] = JArray.FromObject(discovery.Assumptions),
                    ["tsvFileCount"] = discovery.AllTsvFiles.Count,
                    ["iniFileCount"] = discovery.AllIniFiles.Count
                },
                ["staticHierarchy"] = BuildStaticJson(staticTree),
                ["configuration"] = BuildConfigurationJson(staticTree.IniSnapshots),
                ["dynamicAnalysis"] = new JObject
                {
                    ["calibration"] = dynamic.Calibration == null
                        ? null
                        : new JObject
                        {
                            ["centerVector"] = JArray.FromObject(dynamic.Calibration.CenterVector ?? new double[0]),
                            ["wallOffset"] = dynamic.Calibration.WallOffset
                        },
                    ["samples"] = JArray.FromObject(dynamic.Samples),
                    ["failedCommands"] = JArray.FromObject(dynamic.FailedCommands),
                    ["sampleCount"] = dynamic.Samples.Count
                }
            };

            File.WriteAllText(exportPath, payload.ToString(Formatting.Indented), Encoding.UTF8);
            return exportPath;
        }

        private static JObject BuildConfigurationJson(Dictionary<string, JObject> iniSnapshots)
        {
            var config = new JObject();
            foreach (var kv in iniSnapshots.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
            {
                config[kv.Key] = kv.Value;
            }

            return config;
        }

        private static JObject BuildStaticJson(MasterKnowledgeStaticTree tree)
        {
            var modules = new JArray();
            foreach (var mod in tree.Modules.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase))
            {
                var categories = new JArray();
                foreach (var cat in mod.Categories.Values.OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var types = new JArray();
                    foreach (var typ in cat.Types.Values.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
                    {
                        types.Add(new JObject
                        {
                            ["name"] = typ.Name,
                            ["sizeStandards"] = JArray.FromObject(typ.SizeStandards.OrderBy(s => s).ToList()),
                            ["commands"] = JArray.FromObject(typ.Commands.Select(c => new JObject
                            {
                                ["commandLine"] = c.CommandLine,
                                ["sizeStandard"] = c.SizeStandard,
                                ["sourceFile"] = c.SourceFile,
                                ["lineNumber"] = c.LineNumber
                            }))
                        });
                    }

                    categories.Add(new JObject
                    {
                        ["name"] = cat.Name,
                        ["types"] = types
                    });
                }

                modules.Add(new JObject
                {
                    ["name"] = mod.Name,
                    ["categories"] = categories
                });
            }

            return new JObject
            {
                ["moduleCount"] = tree.Modules.Count,
                ["totalCommandRows"] = tree.TotalCommandRows,
                ["tsvSources"] = JArray.FromObject(tree.TsvSources),
                ["modules"] = modules
            };
        }
    }

    #endregion
}