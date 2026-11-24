using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.AutoCAD.ApplicationServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CadprofiScanner
{
    /// <summary>Engine 4 (persistence leg) — master JSON, full encyclopedia, and desktop checkpoints.</summary>
    internal static class DesktopJsonCheckpoints
    {
        public static IReadOnlyList<string> ResolveExportRoots(Document doc)
        {
            var roots = new List<string>();
            var drawingDir = Path.GetDirectoryName(doc.Database.Filename);
            if (!string.IsNullOrWhiteSpace(drawingDir))
            {
                roots.Add(drawingDir);
            }

            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (!string.IsNullOrWhiteSpace(desktop))
            {
                roots.Add(desktop);
            }

            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            if (!string.IsNullOrWhiteSpace(documents))
            {
                roots.Add(documents);
            }

            return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static void WriteCheckpointFiles(Document doc, string generator, JObject master, JObject full)
        {
            foreach (var root in ResolveExportRoots(doc))
            {
                Directory.CreateDirectory(root);
                var masterPath = Path.Combine(root, ScanConfig.MasterJsonFileName);
                var fullPath = Path.Combine(root, ScanConfig.FullJsonFileName);
                File.WriteAllText(masterPath, master.ToString(Formatting.Indented), Encoding.UTF8);
                File.WriteAllText(fullPath, full.ToString(Formatting.Indented), Encoding.UTF8);
            }
        }

        public static void WriteProgressCheckpoint(Document doc, string generator, int completed, int total)
        {
            var payload = new JObject
            {
                ["schema"] = "cadprofi-scan-checkpoint/v1",
                ["generatedAt"] = DateTime.Now.ToString("o"),
                ["generator"] = generator,
                ["completed"] = completed,
                ["total"] = total
            };

            foreach (var root in ResolveExportRoots(doc))
            {
                var path = Path.Combine(root, "cadprofi_scan_checkpoint.json");
                File.WriteAllText(path, payload.ToString(Formatting.Indented), Encoding.UTF8);
            }
        }
    }

    internal static class KnowledgeExporter
    {
        public static string Export(
            Document doc,
            CadprofiDiscoveryResult discovery,
            MasterKnowledgeStaticTree staticTree,
            DynamicScanResult dynamic,
            string generator,
            IReadOnlyList<CadprofiLeafRecord> leaves)
        {
            var payload = BuildMasterPayload(discovery, staticTree, dynamic, generator, leaves);
            var full = LeafEncyclopediaExporter.BuildFull(discovery, leaves, dynamic, generator);

            DesktopJsonCheckpoints.WriteCheckpointFiles(doc, generator, payload, full);

            var primaryRoot = DesktopJsonCheckpoints.ResolveExportRoots(doc).First();
            return Path.Combine(primaryRoot, ScanConfig.MasterJsonFileName);
        }

        public static JObject BuildMasterPayload(
            CadprofiDiscoveryResult discovery,
            MasterKnowledgeStaticTree staticTree,
            DynamicScanResult dynamic,
            string generator,
            IReadOnlyList<CadprofiLeafRecord> leaves)
        {
            return new JObject
            {
                ["schema"] = "cadprofi-master-knowledge/v1.1",
                ["generatedAt"] = DateTime.Now.ToString("o"),
                ["generator"] = generator,
                ["discovery"] = BuildDiscoveryJson(discovery),
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
                    ["commandReports"] = JArray.FromObject(dynamic.CommandReports),
                    ["failedCommands"] = JArray.FromObject(dynamic.FailedCommands),
                    ["hungCommands"] = JArray.FromObject(dynamic.HungCommands),
                    ["skippedCommands"] = JArray.FromObject(dynamic.SkippedCommands),
                    ["totalLeaves"] = leaves.Count,
                    ["scannedLeaves"] = dynamic.ScannedLeaves,
                    ["skippedArchitecture"] = dynamic.SkippedArchitecture,
                    ["sampleCount"] = dynamic.Samples.Count
                }
            };
        }

        private static JObject BuildDiscoveryJson(CadprofiDiscoveryResult discovery)
        {
            return new JObject
            {
                ["resolvedRoots"] = JArray.FromObject(discovery.ResolvedRoots),
                ["searchPathHits"] = JArray.FromObject(discovery.SearchPathHits),
                ["primaryMenuTsv"] = discovery.PrimaryMenuTsv,
                ["primaryCpConf"] = discovery.PrimaryCpConf,
                ["assumptions"] = JArray.FromObject(discovery.Assumptions),
                ["tsvFileCount"] = discovery.AllTsvFiles.Count,
                ["iniFileCount"] = discovery.AllIniFiles.Count,
                ["cuixZipCount"] = discovery.CuixZipCount,
                ["cuixDirectoryCount"] = discovery.CuixDirectoryCount,
                ["cuiFileCount"] = discovery.CuiFileCount,
                ["mnlFileCount"] = discovery.MnlFileCount,
                ["scanConfig"] = new JObject
                {
                    ["commandSettleMs"] = ScanConfig.CommandSettleMs,
                    ["commandTimeoutMs"] = ScanConfig.CommandTimeoutMs,
                    ["commandRetry"] = ScanConfig.CommandRetry,
                    ["checkpointEvery"] = ScanConfig.CheckpointEvery
                }
            };
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
                                ["macroUid"] = c.MacroUid,
                                ["caption"] = c.Caption,
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
                ["menuSources"] = JArray.FromObject(tree.MenuSources),
                ["modules"] = modules
            };
        }
    }

    internal static class LeafEncyclopediaExporter
    {
        public static JObject BuildFull(
            CadprofiDiscoveryResult discovery,
            IReadOnlyList<CadprofiLeafRecord> leaves,
            DynamicScanResult dynamic,
            string generator)
        {
            var reportsByKey = dynamic.CommandReports
                .Where(r => !string.IsNullOrWhiteSpace(r.LeafKey))
                .GroupBy(r => r.LeafKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

            var leafArray = new JArray();
            var fullDynamic = 0;
            var staticOnly = 0;
            var failures = 0;
            var successfulDraws = 0;

            foreach (var leaf in leaves)
            {
                reportsByKey.TryGetValue(leaf.LeafKey, out var report);
                var state = leaf.RunnableState;
                if (report != null)
                {
                    if (report.Skipped)
                    {
                        state = "non_runnable";
                    }
                    else if (report.Success && report.FunctionalVerification?["pass"]?.Value<bool>() == true)
                    {
                        state = "runnable_verified";
                        fullDynamic++;
                        successfulDraws++;
                    }
                    else if (report.Success)
                    {
                        state = "runnable_attempted";
                        successfulDraws++;
                    }
                    else
                    {
                        state = "runnable_attempted";
                        failures++;
                    }
                }
                else if (state == "static_only")
                {
                    staticOnly++;
                }

                var dynamicBlock = report == null
                    ? null
                    : new JObject
                    {
                        ["success"] = report.Success,
                        ["reason"] = report.Reason,
                        ["functionalVerification"] = report.FunctionalVerification,
                        ["geometry"] = SerializeGeometry(report.Geometry),
                        ["lispCommandEcho"] = report.Macro
                    };

                leafArray.Add(new JObject
                {
                    ["path"] = new JObject
                    {
                        ["module"] = leaf.Row.Module,
                        ["category"] = leaf.Row.Category,
                        ["type"] = leaf.Row.Type,
                        ["commandLine"] = leaf.Row.CommandLine,
                        ["macroUid"] = leaf.Row.MacroUid,
                        ["macro"] = leaf.Row.CommandLine,
                        ["caption"] = leaf.Row.Caption,
                        ["helpString"] = leaf.Row.HelpString,
                        ["description"] = leaf.Row.Description,
                        ["toolTip"] = leaf.Row.ToolTip,
                        ["sourceFile"] = leaf.Row.SourceFile,
                        ["relatedIniKeys"] = new JArray()
                    },
                    ["runnableState"] = state,
                    ["nonRunnableReason"] = leaf.NonRunnableReason,
                    ["whatItDoes"] = "inferred:" + (leaf.Row.Caption ?? leaf.Row.Type ?? leaf.Row.CommandLine),
                    ["howItWorks"] = "inferred:AutoCAD macro from CADprofi menu registry",
                    ["inferred"] = new JObject { ["whatItDoes"] = true, ["howItWorks"] = true },
                    ["dynamic"] = dynamicBlock
                });
            }

            var mergedLeaves = MergePriorExport(leafArray);

            return new JObject
            {
                ["schema"] = "cadprofi-master-knowledge-full/v2",
                ["generatedAt"] = DateTime.Now.ToString("o"),
                ["generator"] = generator,
                ["discovery"] = BuildDiscoveryJson(discovery),
                ["leaves"] = mergedLeaves,
                ["summary"] = new JObject
                {
                    ["totalLeaves"] = leaves.Count,
                    ["leavesWithFullDynamic"] = fullDynamic,
                    ["leavesStaticOnly"] = staticOnly,
                    ["failures"] = failures,
                    ["successfulDraws"] = successfulDraws,
                    ["executedThisSession"] = dynamic.CommandReports.Count
                }
            };
        }

        private static JArray MergePriorExport(JArray currentLeaves)
        {
            var merged = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                         Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                     })
            {
                var path = Path.Combine(root, ScanConfig.FullJsonFileName);
                if (!File.Exists(path))
                {
                    continue;
                }

                try
                {
                    var prior = JObject.Parse(File.ReadAllText(path, Encoding.UTF8));
                    var arr = prior["leaves"] as JArray;
                    if (arr == null)
                    {
                        continue;
                    }

                    foreach (var token in arr)
                    {
                        var obj = (JObject)token;
                        var key = BuildLeafKeyFromJson(obj);
                        if (!string.IsNullOrWhiteSpace(key))
                        {
                            merged[key] = obj;
                        }
                    }
                }
                catch
                {
                    // ignore corrupt checkpoint
                }
            }

            foreach (var token in currentLeaves)
            {
                var obj = (JObject)token;
                var key = BuildLeafKeyFromJson(obj);
                if (!string.IsNullOrWhiteSpace(key))
                {
                    merged[key] = obj;
                }
            }

            return new JArray(merged.Values);
        }

        private static JObject BuildDiscoveryJson(CadprofiDiscoveryResult discovery)
        {
            return new JObject
            {
                ["cuixZipCount"] = discovery.CuixZipCount,
                ["cuiFileCount"] = discovery.CuiFileCount,
                ["resolvedRoots"] = JArray.FromObject(discovery.ResolvedRoots)
            };
        }

        private static JObject SerializeGeometry(GeometrySample sample)
        {
            if (sample == null)
            {
                return null;
            }

            return new JObject
            {
                ["command"] = sample.Command,
                ["centerVector"] = sample.CenterVector == null ? null : JArray.FromObject(sample.CenterVector),
                ["wallOffset"] = sample.WallOffset,
                ["entityTypes"] = JArray.FromObject(sample.EntityTypes ?? new List<string>()),
                ["elbow"] = sample.Elbow == null
                    ? null
                    : new JObject
                    {
                        ["rIn"] = sample.Elbow.RIn,
                        ["rOut"] = sample.Elbow.ROut,
                        ["w"] = sample.Elbow.W
                    }
            };
        }

        private static string BuildLeafKeyFromJson(JObject leaf)
        {
            var path = leaf["path"] as JObject;
            if (path == null)
            {
                return null;
            }

            return string.Join("|", new[]
            {
                path["module"]?.ToString() ?? string.Empty,
                path["category"]?.ToString() ?? string.Empty,
                path["type"]?.ToString() ?? string.Empty,
                path["commandLine"]?.ToString() ?? string.Empty
            });
        }
    }
}