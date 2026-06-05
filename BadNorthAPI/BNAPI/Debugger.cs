using BepInEx.Configuration;

namespace BadNorthAPI
{
    public static class Debugger
    {
        public static bool Enabled { get; private set; } = false;

        internal static void Initialize(ConfigFile config)
        {
            var entry = config.Bind(
                "Debug",
                "EnableDebugLog",
                false,
                "是否启用所有特质 Mod 的调试日志输出。关闭可显著提升游戏性能。"
            );

            Enabled = entry.Value;
            Plugin.logger.LogInfo("[BadNorthAPI] 调试日志开关 = " + Enabled);
        }

        public static void Log(string message)
        {
            if (Enabled)
                Plugin.logger.LogInfo(message);
        }

        public static void LogWarning(string message)
        {
            if (Enabled)
                Plugin.logger.LogWarning(message);
        }
    }
}
