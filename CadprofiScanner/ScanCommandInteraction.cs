using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace CadprofiScanner
{
    internal sealed class ScanSkipDecision
    {
        public bool Skip { get; set; }
        public string Reason { get; set; }
    }

    internal sealed class CommandRunOutcome
    {
        public bool Success { get; set; }
        public string Reason { get; set; }
        public bool Hung { get; set; }
        public bool Skipped { get; set; }
        public int CreatedCount { get; set; }
        public List<ObjectId> CreatedEntityIds { get; } = new List<ObjectId>();
        public string PreparedMacro { get; set; }
        public string LispEcho { get; set; }
    }

    internal static class ScanCommandInteraction
    {
        private static readonly string[] InteractiveTokens =
        {
            "DIALOG", "PICK", "SELECT", "GETFILE", "BROWSE", "FILEDIA", "FILEOPEN", "^P", "^D"
        };

        private static readonly string[] UtilityMacroFragments =
        {
            "cp_about", "cp_helpx", "cp_setapp", "cp_specification", "cp_describex",
            "CPTOOLS_ALGORITHM"
        };

        public static ScanSkipDecision ShouldSkipMacro(string macro)
        {
            if (string.IsNullOrWhiteSpace(macro))
            {
                return new ScanSkipDecision { Skip = true, Reason = "non_runnable_macro" };
            }

            var trimmed = macro.Trim();
            if (Regex.IsMatch(trimmed, @"^\^C+\s*$", RegexOptions.IgnoreCase))
            {
                return new ScanSkipDecision { Skip = true, Reason = "cancel_only_macro" };
            }

            var upper = trimmed.ToUpperInvariant();
            if (InteractiveTokens.Any(t => upper.Contains(t, StringComparison.OrdinalIgnoreCase)))
            {
                return new ScanSkipDecision { Skip = true, Reason = "requires_user_input" };
            }

            if (Regex.IsMatch(upper, @"\bINSERT\b") && !upper.Contains("-INSERT"))
            {
                return new ScanSkipDecision { Skip = true, Reason = "requires_user_input" };
            }

            if (UtilityMacroFragments.Any(u => upper.IndexOf(u, StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return new ScanSkipDecision { Skip = true, Reason = "interactive_only" };
            }

            return new ScanSkipDecision { Skip = false };
        }

        public static string PrepareSilentMacro(string macro)
        {
            if (string.IsNullOrWhiteSpace(macro))
            {
                return macro;
            }

            var result = macro;
            result = Regex.Replace(result, @"(?<!\-)\bINSERT\b", "-INSERT", RegexOptions.IgnoreCase);
            return result;
        }

        public static CommandRunOutcome ExecuteWithWatchdog(
            Document doc,
            string macro,
            HashSet<string> beforeHandles,
            UiWatchdogHelpers watchdog)
        {
            var outcome = new CommandRunOutcome { PreparedMacro = PrepareSilentMacro(macro) };
            var skip = ShouldSkipMacro(outcome.PreparedMacro);
            if (skip.Skip)
            {
                outcome.Skipped = true;
                outcome.Reason = skip.Reason;
                return outcome;
            }

            watchdog.BeginCycle();
            doc.SendStringToExecute(outcome.PreparedMacro + "\n", true, false, false);

            var settleMs = Math.Min(ScanConfig.CommandSettleMs, ScanConfig.CommandTimeoutMs);
            var started = Environment.TickCount;
            var deadline = started + ScanConfig.CommandTimeoutMs;
            var geometrySeen = false;

            while (Environment.TickCount < deadline)
            {
                var created = CollectNewEntityIds(doc.Database, beforeHandles);
                if (created.Count > 0)
                {
                    geometrySeen = true;
                    outcome.CreatedEntityIds.Clear();
                    outcome.CreatedEntityIds.AddRange(created);
                    outcome.CreatedCount = created.Count;
                }

                var commandActive = IsCommandInProgress();
                watchdog.Poll();

                if (geometrySeen && !commandActive)
                {
                    Thread.Sleep(settleMs);
                    outcome.Success = true;
                    outcome.Reason = "ok";
                    return outcome;
                }

                if (!geometrySeen && !commandActive && Environment.TickCount - started >= settleMs)
                {
                    outcome.Success = false;
                    outcome.Reason = "no_geometry";
                    return outcome;
                }

                if (watchdog.ShouldCancelHang(commandActive, geometrySeen))
                {
                    watchdog.SendEscapeTriplet(doc);
                    outcome.Hung = true;
                    outcome.Reason = "interactive_prompt_timeout";
                    return outcome;
                }

                Thread.Sleep(ScanConfig.PollIntervalMs);
            }

            if (IsCommandInProgress())
            {
                watchdog.SendEscapeTriplet(doc);
                outcome.Hung = true;
                outcome.Reason = "interactive_prompt_timeout";
                return outcome;
            }

            outcome.Success = geometrySeen;
            outcome.Reason = geometrySeen ? "ok" : "no_geometry";
            outcome.CreatedEntityIds.Clear();
            outcome.CreatedEntityIds.AddRange(CollectNewEntityIds(doc.Database, beforeHandles));
            outcome.CreatedCount = outcome.CreatedEntityIds.Count;
            return outcome;
        }

        private static bool IsCommandInProgress()
        {
            try
            {
                var name = Convert.ToString(AcApp.GetSystemVariable("CMDNAMES"));
                return !string.IsNullOrWhiteSpace(name);
            }
            catch
            {
                return false;
            }
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

                    if (!beforeHandles.Contains(id.Handle.ToString()))
                    {
                        ids.Add(id);
                    }
                }

                tr.Commit();
            }

            return ids;
        }
    }
}