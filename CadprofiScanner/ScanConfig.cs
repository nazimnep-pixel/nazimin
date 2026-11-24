using System;

namespace CadprofiScanner
{
    internal static class ScanConfig
    {
        public const int ReferenceSpan = 2000;
        public const int MaxDrawCommandsPerRun = 500;
        public const string MasterJsonFileName = "cadprofi_master_knowledge.json";
        public const string FullJsonFileName = "cadprofi_master_knowledge_full.json";

        public static int CommandSettleMs => ReadInt("AZM_SCAN_COMMAND_SETTLE_MS", 350);

        public static int CommandTimeoutMs => ReadInt("AZM_SCAN_COMMAND_TIMEOUT_MS", 6000);

        public static bool CommandRetry => ReadBool("AZM_SCAN_COMMAND_RETRY", true);

        public static int FastCount => ReadInt("AZM_FAST_COUNT", 2);

        public static int BatchOffset => ReadInt("AZM_BATCH_OFFSET", 0);

        public static int BatchSize => ReadInt("AZM_BATCH_SIZE", 25);

        public static int CheckpointEvery => ReadInt("AZM_SCAN_CHECKPOINT_EVERY", 10);

        public static int PollIntervalMs => 50;

        private static int ReadInt(string name, int defaultValue)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            return int.TryParse(raw, out var value) ? value : defaultValue;
        }

        private static bool ReadBool(string name, bool defaultValue)
        {
            var raw = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return defaultValue;
            }

            if (bool.TryParse(raw, out var b))
            {
                return b;
            }

            return raw == "1" || raw.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}