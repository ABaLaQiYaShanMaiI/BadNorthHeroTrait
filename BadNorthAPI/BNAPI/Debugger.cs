// Author: ABaLaQiYaShanMaiI
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
            Plugin.Logger.LogInfo("[BadNorthAPI] 调试日志开关 = " + Enabled);
        }

        /// <summary>受全局 EnableDebugLog 控制的日志。</summary>
        public static void Log(string message)
        {
            if (Enabled)
                Plugin.Logger.LogInfo(message);
        }

        /// <summary>受全局 EnableDebugLog 控制的警告。</summary>
        public static void LogWarning(string message)
        {
            if (Enabled)
                Plugin.Logger.LogWarning(message);
        }

        /// <summary>受调用方传入的条件控制的日志（每个 Mod 通过自己的 EnableGameplayLog 开关控制）。</summary>
        public static void Log(bool condition, string message)
        {
            if (condition)
                Plugin.Logger.LogInfo(message);
        }

        /// <summary>受调用方传入的条件控制的警告（每个 Mod 通过自己的 EnableGameplayLog 开关控制）。</summary>
        public static void LogWarning(bool condition, string message)
        {
            if (condition)
                Plugin.Logger.LogWarning(message);
        }
    }
}
