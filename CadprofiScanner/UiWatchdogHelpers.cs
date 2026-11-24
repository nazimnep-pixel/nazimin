using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Automation;
namespace CadprofiScanner
{
    /// <summary>Engine 4 (UI leg) — desktop modal detection and autonomous cancel helpers.</summary>
    internal sealed class UiWatchdogHelpers
    {
        private readonly List<string> _observedDialogs = new List<string>();
        private int _cycleStartTick;
        private int _modalHits;

        public IReadOnlyList<string> ObservedDialogs => _observedDialogs;

        public void BeginCycle()
        {
            _cycleStartTick = Environment.TickCount;
            _modalHits = 0;
        }

        public void Poll()
        {
            foreach (var title in ProbeModalWindowTitles())
            {
                if (!_observedDialogs.Contains(title, StringComparer.OrdinalIgnoreCase))
                {
                    _observedDialogs.Add(title);
                }

                _modalHits++;
            }
        }

        public bool ShouldCancelHang(bool commandActive, bool geometrySeen)
        {
            if (!commandActive)
            {
                return false;
            }

            var elapsed = Environment.TickCount - _cycleStartTick;
            return elapsed >= ScanConfig.CommandTimeoutMs;
        }

        public void SendEscapeTriplet(Document doc)
        {
            for (var i = 0; i < 3; i++)
            {
                doc.SendStringToExecute("\x1B", true, false, false);
            }

            TryDismissVisibleDialogs();
        }

        private static IEnumerable<string> ProbeModalWindowTitles()
        {
            var titles = new List<string>();
            try
            {
                var autoCad = FindAutoCadProcessWindow();
                if (autoCad == null)
                {
                    return titles;
                }

                var root = AutomationElement.FromHandle(autoCad.Value);
                if (root == null)
                {
                    return titles;
                }

                var condition = new PropertyCondition(AutomationElement.IsWindowPatternAvailableProperty, true);
                foreach (AutomationElement child in root.FindAll(TreeScope.Children, condition))
                {
                    var name = child.Current.Name;
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        continue;
                    }

                    if (child.Current.IsEnabled && !child.Current.IsOffscreen)
                    {
                        titles.Add(name);
                    }
                }
            }
            catch
            {
                // UIAutomation unavailable — caller falls back to CMDNAMES polling only.
            }

            return titles;
        }

        private void TryDismissVisibleDialogs()
        {
            try
            {
                foreach (var element in ProbeModalElements())
                {
                    var ok = element.FindFirst(
                        TreeScope.Descendants,
                        new OrCondition(
                            new PropertyCondition(AutomationElement.NameProperty, "OK"),
                            new PropertyCondition(AutomationElement.NameProperty, "Cancel"),
                            new PropertyCondition(AutomationElement.NameProperty, "Close")));
                    if (ok != null && ok.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern))
                    {
                        ((InvokePattern)pattern).Invoke();
                    }
                }
            }
            catch
            {
                // Best-effort only.
            }
        }

        private static IEnumerable<AutomationElement> ProbeModalElements()
        {
            var list = new List<AutomationElement>();
            var hwnd = FindAutoCadProcessWindow();
            if (hwnd == null)
            {
                return list;
            }

            var root = AutomationElement.FromHandle(hwnd.Value);
            if (root == null)
            {
                return list;
            }

            var condition = new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Window),
                new PropertyCondition(AutomationElement.IsEnabledProperty, true));
            foreach (AutomationElement window in root.FindAll(TreeScope.Children, condition))
            {
                if (!string.IsNullOrWhiteSpace(window.Current.Name))
                {
                    list.Add(window);
                }
            }

            return list;
        }

        private static IntPtr? FindAutoCadProcessWindow()
        {
            foreach (var name in new[] { "acad", "AcQMod", "acadlt" })
            {
                foreach (var proc in Process.GetProcessesByName(name))
                {
                    if (proc.MainWindowHandle != IntPtr.Zero)
                    {
                        return proc.MainWindowHandle;
                    }
                }
            }

            return null;
        }
    }
}