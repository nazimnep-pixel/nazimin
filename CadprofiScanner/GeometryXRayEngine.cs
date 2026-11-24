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
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

namespace CadprofiScanner
{
    internal sealed class DynamicScanResult
    {
        public List<GeometrySample> Samples { get; } = new List<GeometrySample>();
        public List<CommandReport> CommandReports { get; } = new List<CommandReport>();
        public List<string> FailedCommands { get; } = new List<string>();
        public List<string> HungCommands { get; } = new List<string>();
        public List<string> SkippedCommands { get; } = new List<string>();
        public int SkippedArchitecture { get; set; }
        public int ScannedLeaves { get; set; }
        public ReferenceLinesCalibration Calibration { get; set; }
    }

    internal sealed class CommandReport
    {
        public string Command { get; set; }
        public string Macro { get; set; }
        public string LeafKey { get; set; }
        public bool Success { get; set; }
        public string Reason { get; set; }
        public bool Hung { get; set; }
        public bool Skipped { get; set; }
        public GeometrySample Geometry { get; set; }
        public JObject FunctionalVerification { get; set; }
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

    /// <summary>Engine 2+3 — macro execution, geometry diff, and functional verification (x-ray).</summary>
    internal static class GeometryXRayEngine
    {
        public static DynamicScanResult Run(
            Document doc,
            IReadOnlyList<CadprofiLeafRecord> leaves,
            UiWatchdogHelpers watchdog)
        {
            var result = new DynamicScanResult();
            var db = doc.Database;
            var runnable = leaves.Where(l => l.IsRunnable).ToList();
            result.SkippedArchitecture = leaves.Count(l => l.IsArchitecture);

            using (doc.LockDocument())
            {
                result.Calibration = DynamicGeometryScannerHelpers.DrawReferenceLines(db);

                var index = 0;
                foreach (var leaf in runnable)
                {
                    index++;
                    var macro = leaf.Row.CommandLine?.Trim();
                    if (string.IsNullOrWhiteSpace(macro))
                    {
                        continue;
                    }

                    doc.Editor.WriteMessage($"\nAZMEXANIKA: [{index}/{runnable.Count}] icra — `{macro}`\n");

                    var report = new CommandReport
                    {
                        Command = macro,
                        Macro = macro,
                        LeafKey = leaf.LeafKey
                    };

                    try
                    {
                        var before = DynamicGeometryScannerHelpers.CollectEntityHandles(db);
                        var beforeTypes = CountEntitiesByType(db);
                        var beforeCount = before.Count;

                        var outcome = ScanCommandInteraction.ExecuteWithWatchdog(doc, macro, before, watchdog);
                        report.Skipped = outcome.Skipped;
                        report.Hung = outcome.Hung;
                        report.Success = outcome.Success;
                        report.Reason = outcome.Reason;

                        if (outcome.Skipped)
                        {
                            result.SkippedCommands.Add(macro);
                            result.CommandReports.Add(report);
                            continue;
                        }

                        if (outcome.Hung)
                        {
                            result.HungCommands.Add(macro);
                            result.CommandReports.Add(report);
                            continue;
                        }

                        var sample = DynamicGeometryScannerHelpers.ReadGeometry(db, macro, outcome.CreatedEntityIds);
                        report.Geometry = sample;
                        if (sample != null)
                        {
                            result.Samples.Add(sample);
                        }

                        report.FunctionalVerification = BuildFunctionalVerification(
                            macro,
                            beforeCount,
                            beforeTypes,
                            db,
                            outcome.CreatedEntityIds,
                            sample,
                            outcome);

                        if (!outcome.Success)
                        {
                            result.FailedCommands.Add(macro);
                        }
                        else
                        {
                            result.ScannedLeaves++;
                        }

                        DynamicGeometryScannerHelpers.EraseEntities(db, outcome.CreatedEntityIds);
                        result.CommandReports.Add(report);

                        if (index % ScanConfig.CheckpointEvery == 0)
                        {
                            DesktopJsonCheckpoints.WriteProgressCheckpoint(doc, "GeometryXRayEngine", index, runnable.Count);
                        }

                        if (outcome.Success && ScanConfig.CommandRetry &&
                            outcome.Reason == "no_geometry" && !outcome.Hung)
                        {
                            Thread.Sleep(ScanConfig.CommandSettleMs * 2);
                        }
                    }
                    catch (Exception ex)
                    {
                        report.Success = false;
                        report.Reason = "exception:" + ex.GetType().Name;
                        report.FunctionalVerification = new JObject
                        {
                            ["pass"] = false,
                            ["reasons"] = new JArray("exception:" + ex.GetType().Name)
                        };
                        result.FailedCommands.Add(macro);
                        result.CommandReports.Add(report);
                    }
                }
            }

            return result;
        }

        private static JObject BuildFunctionalVerification(
            string macro,
            int beforeCount,
            Dictionary<string, int> beforeTypes,
            Database db,
            List<ObjectId> createdIds,
            GeometrySample sample,
            CommandRunOutcome outcome)
        {
            var afterTypes = CountEntitiesByType(db);
            var afterCount = beforeCount + createdIds.Count;
            var createdTypes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var id in createdIds)
            {
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null)
                    {
                        continue;
                    }

                    var name = ent.GetType().Name;
                    createdTypes[name] = createdTypes.TryGetValue(name, out var c) ? c + 1 : 1;
                    tr.Commit();
                }
            }

            var reasons = new List<string>();
            if (createdIds.Count == 0)
            {
                reasons.Add("no_entities_created_in_modelspace");
            }

            if (sample?.EntityTypes == null || sample.EntityTypes.Count == 0)
            {
                if (createdIds.Count > 0)
                {
                    reasons.Add("no_drawable_entity_types");
                }
            }

            var summaries = BuildEntitySummaries(db, createdIds);
            var meta = MeasureMetadataCompleteness(macro, summaries);
            if (meta["missingCadprofiMetadata"].Value<bool>())
            {
                reasons.Add("missing_extensiondict_and_xdata");
            }

            var measured = BuildMeasuredProperties(sample, summaries, macro, reasons);

            var fv = new JObject
            {
                ["pass"] = reasons.Count == 0,
                ["reasons"] = JArray.FromObject(reasons),
                ["modelSpaceDiff"] = new JObject
                {
                    ["beforeEntityCount"] = beforeCount,
                    ["afterEntityCount"] = afterCount,
                    ["createdCount"] = createdIds.Count,
                    ["beforeEntitiesByType"] = JObject.FromObject(beforeTypes),
                    ["afterEntitiesByType"] = JObject.FromObject(afterTypes),
                    ["createdEntitiesByType"] = JObject.FromObject(createdTypes),
                    ["newEntityHandles"] = JArray.FromObject(createdIds.Select(id => id.Handle.ToString()).ToList())
                },
                ["entitySummaries"] = summaries,
                ["measuredProperties"] = measured,
                ["metadataCompleteness"] = meta,
                ["dialogOutcome"] = new JObject
                {
                    ["detected"] = outcome.Hung,
                    ["outcome"] = outcome.Hung ? "command_in_progress" : "not_observed",
                    ["note"] = "UIAutomation watchdog supplements CMDNAMES polling for unattended scans."
                }
            };

            return fv;
        }

        private static JArray BuildEntitySummaries(Database db, List<ObjectId> createdIds)
        {
            var array = new JArray();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (var id in createdIds)
                {
                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null)
                    {
                        continue;
                    }

                    var center = EntityCenter(ent);
                    var extKeys = new List<string>();
                    var extMap = new JObject();
                    foreach (var pair in ExtensionDictionaryReader.Read(ent, tr))
                    {
                        extKeys.Add(pair.Key);
                        extMap[pair.Key] = JToken.FromObject(pair.Value);
                    }

                    var xdataApps = new List<string>();
                    var xdataMap = new JObject();
                    foreach (var x in XDataReader.ReadAll(ent))
                    {
                        xdataApps.Add(x.Key);
                        xdataMap[x.Key] = JArray.FromObject(x.Value);
                    }

                    array.Add(new JObject
                    {
                        ["handle"] = id.Handle.ToString(),
                        ["entityType"] = ent.GetType().Name,
                        ["layer"] = ent.Layer,
                        ["hasExtensionDictionary"] = ent.ExtensionDictionary != ObjectId.Null,
                        ["extensionDictionaryKeys"] = JArray.FromObject(extKeys),
                        ["extensionDictionary"] = extMap,
                        ["xdataApplications"] = JArray.FromObject(xdataApps),
                        ["xdata"] = xdataMap,
                        ["center"] = JArray.FromObject(center),
                        ["elbow"] = ElbowToJson(DynamicGeometryScannerHelpers.TryReadElbowMetrics(ent))
                    });
                }

                tr.Commit();
            }

            return array;
        }

        private static JObject MeasureMetadataCompleteness(string macro, JArray summaries)
        {
            var withExt = 0;
            var withX = 0;
            var apps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var cadTagged = 0;

            foreach (var token in summaries)
            {
                var obj = (JObject)token;
                if (obj["hasExtensionDictionary"]?.Value<bool>() == true)
                {
                    withExt++;
                }

                var xapps = obj["xdataApplications"] as JArray;
                if (xapps != null && xapps.Count > 0)
                {
                    withX++;
                    foreach (var a in xapps)
                    {
                        apps.Add(a.ToString());
                    }
                }

                if (LooksCadprofiTagged(obj, macro))
                {
                    cadTagged++;
                }
            }

            var needsMeta = macro.IndexOf("CP", StringComparison.OrdinalIgnoreCase) >= 0;
            var missing = needsMeta && summaries.Count > 0 && withExt == 0 && withX == 0;

            return new JObject
            {
                ["entitiesWithExtensionDictionary"] = withExt,
                ["entitiesWithXData"] = withX,
                ["xdataApplicationCount"] = apps.Count,
                ["cadprofiTaggedEntities"] = cadTagged,
                ["missingCadprofiMetadata"] = missing
            };
        }

        private static bool LooksCadprofiTagged(JObject summary, string macro)
        {
            var blob = summary.ToString();
            return blob.IndexOf("cadprofi", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   blob.IndexOf("CPROF", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   macro.IndexOf("CP_", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static JObject BuildMeasuredProperties(
            GeometrySample sample,
            JArray summaries,
            string macro,
            List<string> reasons)
        {
            var invariants = new JArray();
            var elbow = sample?.Elbow;
            if (elbow != null)
            {
                if (elbow.RIn.HasValue && elbow.ROut.HasValue && elbow.ROut < elbow.RIn)
                {
                    invariants.Add("elbow_r_out_lt_r_in");
                    reasons.Add("geometry_invariant:elbow_r_out_lt_r_in");
                }

                if (!elbow.W.HasValue && macro.IndexOf("ELB", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    invariants.Add("elbow_w_missing");
                    reasons.Add("geometry_invariant:elbow_w_missing");
                }
            }
            else if (macro.IndexOf("ELB", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     macro.IndexOf("ELBOW", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     macro.IndexOf("KOLENO", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                reasons.Add("elbow_metrics_not_measured");
            }

            return new JObject
            {
                ["aggregateCenter"] = sample?.CenterVector != null ? JArray.FromObject(sample.CenterVector) : new JArray(),
                ["wallOffset"] = sample?.WallOffset,
                ["elbow"] = ElbowToJson(elbow),
                ["geometryInvariantChecks"] = invariants
            };
        }

        private static JObject ElbowToJson(ElbowMetrics elbow)
        {
            if (elbow == null)
            {
                return null;
            }

            return new JObject
            {
                ["rIn"] = elbow.RIn,
                ["rOut"] = elbow.ROut,
                ["w"] = elbow.W
            };
        }

        private static double[] EntityCenter(Entity ent)
        {
            switch (ent)
            {
                case Line line:
                    var mid = line.StartPoint + (line.EndPoint - line.StartPoint) * 0.5;
                    return new[] { mid.X, mid.Y, mid.Z };
                case Arc arc:
                    return new[] { arc.Center.X, arc.Center.Y, arc.Center.Z };
                case Circle circle:
                    return new[] { circle.Center.X, circle.Center.Y, circle.Center.Z };
                case BlockReference br:
                    return new[] { br.Position.X, br.Position.Y, br.Position.Z };
                default:
                    return new[] { 0.0, 0.0, 0.0 };
            }
        }

        private static Dictionary<string, int> CountEntitiesByType(Database db)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
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

                    var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                    if (ent == null)
                    {
                        continue;
                    }

                    var name = ent.GetType().Name;
                    map[name] = map.TryGetValue(name, out var c) ? c + 1 : 1;
                }

                tr.Commit();
            }

            return map;
        }

    }

    internal static class DynamicGeometryScannerHelpers
    {
        public static DynamicScanResult RunLegacy(Document doc, List<MenuCommandRow> commands)
        {
            var leaves = commands.Select(LeafDiscoveryEngine.ToLeafRecord).ToList();
            return GeometryXRayEngine.Run(doc, leaves, new UiWatchdogHelpers());
        }

        public static string NormalizeCommand(string commandLine)
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

        public static ReferenceLinesCalibration DrawReferenceLines(Database db)
        {
            var calibration = new ReferenceLinesCalibration();
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                var lineA = new Line(new Point3d(0, 0, 0), new Point3d(ScanConfig.ReferenceSpan, 0, 0));
                var lineB = new Line(new Point3d(ScanConfig.ReferenceSpan, 0, 0), new Point3d(ScanConfig.ReferenceSpan, ScanConfig.ReferenceSpan, 0));
                ms.AppendEntity(lineA);
                ms.AppendEntity(lineB);
                tr.AddNewlyCreatedDBObject(lineA, true);
                tr.AddNewlyCreatedDBObject(lineB, true);

                var mid = new Point3d(ScanConfig.ReferenceSpan * 0.5, ScanConfig.ReferenceSpan * 0.25, 0);
                calibration.CenterVector = new[] { mid.X, mid.Y, mid.Z };
                calibration.WallOffset = Math.Abs(lineB.StartPoint.Y - lineA.StartPoint.Y);

                lineA.Erase();
                lineB.Erase();
                tr.Commit();
            }

            return calibration;
        }

        public static HashSet<string> CollectEntityHandles(Database db)
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

        public static List<ObjectId> CollectNewEntityIds(Database db, HashSet<string> beforeHandles)
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

        public static GeometrySample ReadGeometry(Database db, string command, List<ObjectId> entityIds)
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

        public static void EraseEntities(Database db, List<ObjectId> entityIds)
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

        public static ElbowMetrics TryReadElbowMetrics(Entity ent)
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

}
