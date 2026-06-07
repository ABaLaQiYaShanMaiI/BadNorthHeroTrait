using System;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using BadNorthAPI;

namespace BadNorthAPI
{
	[BepInPlugin("nacu.bnapi.modular", "BN Mod API (Modular)", "1.0")]
	public class Plugin : BaseUnityPlugin
	{
		public void OnEnable()
		{
			Plugin.logger = base.Logger;
			Plugin.ConfigRef = Config;

			// 初始化全局调试日志开关（从 BepInEx 配置文件读取）
			Debugger.Initialize(Config);

			CustomText.ApplyHooks();
			CustomTraits.ApplyHooks();

			// 使用 StringBuilder 替代 string.Join 避免 Mono CLR 2.0 兼容性问题
			StringBuilder sb = new StringBuilder("======== [BadNorthAPI] 已就绪，特性ID: ");
			for (int i = 0; i < CustomTraits.startingTraits.Count; i++)
			{
				if (i > 0) sb.Append(", ");
				sb.Append(CustomTraits.startingTraits[i]);
			}
			sb.Append(" (API 1.0) ========");
			Plugin.logger.LogInfo(sb.ToString());
		}

		public static ManualLogSource logger;

		public static ConfigFile ConfigRef;

		public const string VERSION = "1.0";
	}
}
