using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using Exception = System.Exception;

[assembly: ExtensionApplication(typeof(CadprofiScanner.AzmFullAutoScannerApp))]

namespace CadprofiScanner
{
    public sealed class AzmFullAutoScannerApp : IExtensionApplication
    {
        public void Initialize() { }

        public void Terminate() { }
    }

    public static class AzmFullAutoScanner
    {
        [CommandMethod("AZM_SCAN_ALL", CommandFlags.Session)]
        public static void ScanAll()
        {
            RunSession("AzmFullAutoScanner.AZM_SCAN_ALL", int.MaxValue, ScanConfig.BatchOffset);
        }

        [CommandMethod("AZM_SCAN_FAST", CommandFlags.Session)]
        public static void ScanFast()
        {
            RunSession("AzmFullAutoScanner.AZM_SCAN_FAST", ScanConfig.FastCount, ScanConfig.BatchOffset);
        }

        [CommandMethod("AZM_SCAN_ONE", CommandFlags.Session)]
        public static void ScanOne()
        {
            RunSession("AzmFullAutoScanner.AZM_SCAN_ONE", 1, ScanConfig.BatchOffset);
        }

        [CommandMethod("AZM_SCAN_BATCH", CommandFlags.Session)]
        public static void ScanBatch()
        {
            RunSession("AzmFullAutoScanner.AZM_SCAN_BATCH", ScanConfig.BatchSize, ScanConfig.BatchOffset);
        }

        private static void RunSession(string generator, int take, int offset)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null)
            {
                return;
            }

            var ed = doc.Editor;
            try
            {
                ed.WriteMessage("\nAZMEXANIKA: Dörd mühərrikli CADprofi skanı başladı...\n");

                var discovery = LeafDiscoveryEngine.Discover(doc);
                ed.WriteMessage($"\nAZMEXANIKA: [Engine 1] {discovery.ResolvedRoots.Count} qovluq, {discovery.CuiFileCount} CUI.\n");

                var staticTree = LeafDiscoveryEngine.BuildStatic(discovery);
                var leaves = LeafDiscoveryEngine.EnumerateLeaves(staticTree, discovery);
                var queue = leaves.Where(l => l.IsRunnable).Skip(offset).Take(Math.Min(take, ScanConfig.MaxDrawCommandsPerRun)).ToList();

                ed.WriteMessage(
                    $"\nAZMEXANIKA: [Engine 1] {leaves.Count} yarpaq, icra növbəsi {queue.Count} (offset={offset}).\n");

                var watchdog = new UiWatchdogHelpers();
                var dynamic = GeometryXRayEngine.Run(doc, queue, watchdog);

                var exportPath = KnowledgeExporter.Export(doc, discovery, staticTree, dynamic, generator, leaves);
                ed.WriteMessage("\nAZMEXANIKA: CADprofi tam analiz olundu və master JSON faylı yaradıldı!\n");
                ed.WriteMessage($"\nAZMEXANIKA: {exportPath}\n");
                ed.WriteMessage(
                    $"\nAZMEXANIKA: [Engine 3] {dynamic.ScannedLeaves} yarpaq, {dynamic.HungCommands.Count} donma, {dynamic.FailedCommands.Count} uğursuz.\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\nAZMEXANIKA: Xəta — {ex.Message}\n");
            }
        }
    }
}