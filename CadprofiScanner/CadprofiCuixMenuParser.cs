using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
namespace CadprofiScanner
{
    internal static class CadprofiCuixMenuParser
    {
        private static readonly Regex LispDefunRegex = new Regex(
            @"\(defun\s+c:([A-Za-z0-9_\-]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex IniCommandRegex = new Regex(
            @"\b(cp[_\w]+)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static void MergeIntoStaticTree(CadprofiDiscoveryResult discovery, MasterKnowledgeStaticTree tree)
        {
            var registry = new Dictionary<string, MenuCommandRow>(StringComparer.OrdinalIgnoreCase);

            foreach (var cui in discovery.AllCuiFiles)
            {
                MergeCuiFile(cui, registry, tree);
            }

            foreach (var zip in discovery.AllCuixZipFiles)
            {
                MergeCuixZip(zip, registry, tree);
            }

            foreach (var dir in discovery.AllCuixDirectories)
            {
                foreach (var cui in Directory.EnumerateFiles(dir, "*.cui", SearchOption.AllDirectories))
                {
                    MergeCuiFile(cui, registry, tree);
                }
            }

            foreach (var mnl in discovery.AllMnlFiles)
            {
                MergeMnl(mnl, registry, tree);
            }

            foreach (var ini in discovery.AllIniFiles)
            {
                MergeIniTokens(ini, registry, tree);
            }

            foreach (var row in registry.Values)
            {
                AttachRow(tree, row);
            }
        }

        public static bool IsRunnableMacro(string macro)
        {
            if (string.IsNullOrWhiteSpace(macro))
            {
                return false;
            }

            var body = macro.Trim();
            body = Regex.Replace(body, @"^\^C+", string.Empty, RegexOptions.IgnoreCase);
            return Regex.IsMatch(body, @"[A-Za-z0-9_]");
        }

        private static void MergeCuixZip(string zipPath, Dictionary<string, MenuCommandRow> registry, MasterKnowledgeStaticTree tree)
        {
            try
            {
                using (var archive = ZipFile.OpenRead(zipPath))
                {
                    foreach (var entry in archive.Entries.Where(e => e.FullName.EndsWith(".cui", StringComparison.OrdinalIgnoreCase)))
                    {
                        using (var stream = entry.Open())
                        using (var reader = new StreamReader(stream, Encoding.UTF8))
                        {
                            ParseCuiText(reader.ReadToEnd(), entry.FullName, registry, tree);
                        }
                    }
                }

                tree.MenuSources.Add(new MenuSourceSummary
                {
                    Path = zipPath,
                    Kind = "cuix_zip",
                    MacroCount = registry.Count
                });
            }
            catch
            {
                // unreadable archive
            }
        }

        private static void MergeCuiFile(string path, Dictionary<string, MenuCommandRow> registry, MasterKnowledgeStaticTree tree)
        {
            try
            {
                var text = File.ReadAllText(path, Encoding.UTF8);
                ParseCuiText(text, path, registry, tree);
                tree.MenuSources.Add(new MenuSourceSummary { Path = path, Kind = "cui", MacroCount = registry.Count });
            }
            catch
            {
                try
                {
                    var text = File.ReadAllText(path, Encoding.GetEncoding(1252));
                    ParseCuiText(text, path, registry, tree);
                }
                catch
                {
                    // skip
                }
            }
        }

        private static void ParseCuiText(
            string xml,
            string source,
            Dictionary<string, MenuCommandRow> registry,
            MasterKnowledgeStaticTree tree)
        {
            if (string.IsNullOrWhiteSpace(xml))
            {
                return;
            }

            XDocument doc;
            try
            {
                doc = XDocument.Parse(xml);
            }
            catch
            {
                return;
            }

            var module = InferModuleFromFileName(source);
            foreach (var macro in doc.Descendants().Where(e => e.Name.LocalName.Equals("MenuMacro", StringComparison.OrdinalIgnoreCase)))
            {
                var uid = AttributeValue(macro, "UID") ?? AttributeValue(macro, "MenuMacroID");
                var cmd = macro.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Command", StringComparison.OrdinalIgnoreCase))?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(cmd))
                {
                    continue;
                }

                var name = macro.Descendants().FirstOrDefault(e => e.Name.LocalName.Equals("Name", StringComparison.OrdinalIgnoreCase))?.Value?.Trim()
                           ?? uid
                           ?? "Macro";
                var row = new MenuCommandRow
                {
                    Module = MapUidToModule(uid) ?? module,
                    Category = "Menu",
                    Type = name,
                    CommandLine = cmd,
                    MacroUid = uid,
                    Caption = name,
                    SourceFile = source,
                    LineNumber = 0
                };

                registry[cmd] = row;
            }

            EnrichHierarchyFromPopMenus(doc, registry);
        }

        private static void EnrichHierarchyFromPopMenus(XDocument doc, Dictionary<string, MenuCommandRow> registry)
        {
            var currentModule = "CP System";
            foreach (var line in doc.ToString().Split('\n'))
            {
                if (line.IndexOf("Cui_ARCH", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    currentModule = "Architecture";
                }
                else if (line.IndexOf("Cui_SANIT", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    currentModule = "Sanitary";
                }
                else if (line.IndexOf("Cui_ELECTRO", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    currentModule = "Electro";
                }
                else if (line.IndexOf("Cui_MECH", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    currentModule = "Mechanical";
                }
            }

            foreach (var row in registry.Values)
            {
                if (string.IsNullOrWhiteSpace(row.Module) || row.Module == "CP System")
                {
                    row.Module = MapUidToModule(row.MacroUid) ?? currentModule;
                }
            }
        }

        private static void MergeMnl(string path, Dictionary<string, MenuCommandRow> registry, MasterKnowledgeStaticTree tree)
        {
            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch
            {
                return;
            }

            var count = 0;
            foreach (Match m in LispDefunRegex.Matches(text))
            {
                var name = m.Groups[1].Value;
                var cmd = "MNL_" + name;
                registry[cmd] = new MenuCommandRow
                {
                    Module = "CP System",
                    Category = "MNL",
                    Type = name,
                    CommandLine = cmd,
                    SourceFile = path,
                    LineNumber = 0
                };
                count++;
            }

            tree.MenuSources.Add(new MenuSourceSummary { Path = path, Kind = "mnl", MacroCount = count });
        }

        private static void MergeIniTokens(string path, Dictionary<string, MenuCommandRow> registry, MasterKnowledgeStaticTree tree)
        {
            string text;
            try
            {
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch
            {
                return;
            }

            var count = 0;
            foreach (Match m in IniCommandRegex.Matches(text))
            {
                var token = m.Groups[1].Value;
                var cmd = "INI_" + token;
                if (registry.ContainsKey(cmd))
                {
                    continue;
                }

                registry[cmd] = new MenuCommandRow
                {
                    Module = "CP System",
                    Category = "INI",
                    Type = token,
                    CommandLine = token,
                    SourceFile = path,
                    LineNumber = 0
                };
                count++;
            }

            if (count > 0)
            {
                tree.MenuSources.Add(new MenuSourceSummary { Path = path, Kind = "ini_tokens", MacroCount = count });
            }
        }

        private static void AttachRow(MasterKnowledgeStaticTree tree, MenuCommandRow row)
        {
            tree.TotalCommandRows++;
            var modNode = tree.Modules.FirstOrDefault(m => string.Equals(m.Name, row.Module, StringComparison.OrdinalIgnoreCase));
            if (modNode == null)
            {
                modNode = new CpModuleNode { Name = row.Module ?? "CP System" };
                tree.Modules.Add(modNode);
            }

            if (!modNode.Categories.TryGetValue(row.Category ?? "Menu", out var cat))
            {
                cat = new CpCategoryNode { Name = row.Category ?? "Menu" };
                modNode.Categories[cat.Name] = cat;
            }

            if (!cat.Types.TryGetValue(row.Type ?? "Macro", out var type))
            {
                type = new CpTypeNode { Name = row.Type ?? "Macro" };
                cat.Types[type.Name] = type;
            }

            type.Commands.Add(row);
        }

        private static string AttributeValue(XElement element, string name)
        {
            return element.Attributes().FirstOrDefault(a => a.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
        }

        private static string InferModuleFromFileName(string path)
        {
            var file = Path.GetFileName(path).ToUpperInvariant();
            if (file.Contains("ARCH"))
            {
                return "Architecture";
            }

            if (file.Contains("SAN"))
            {
                return "Sanitary";
            }

            if (file.Contains("ELECTRO") || file.Contains("EL_"))
            {
                return "Electro";
            }

            if (file.Contains("MECH"))
            {
                return "Mechanical";
            }

            return "CP System";
        }

        private static string MapUidToModule(string uid)
        {
            if (string.IsNullOrWhiteSpace(uid))
            {
                return null;
            }

            if (uid.StartsWith("ID_ARCH_", StringComparison.OrdinalIgnoreCase))
            {
                return "Architecture";
            }

            if (uid.StartsWith("ID_SAN_", StringComparison.OrdinalIgnoreCase) ||
                uid.StartsWith("ID_AC_", StringComparison.OrdinalIgnoreCase))
            {
                return "Sanitary";
            }

            if (uid.StartsWith("ID_EL_", StringComparison.OrdinalIgnoreCase))
            {
                return "Electro";
            }

            if (uid.StartsWith("ID_MECH_", StringComparison.OrdinalIgnoreCase))
            {
                return "Mechanical";
            }

            return null;
        }
    }
}