// Author: ABaLaQiYaShanMaiI
using System;
using System.Text;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;

using BadNorthAPI;

namespace BadNorthAPI
{
	[BepInPlugin("ABaLaQiYaShanMaiI.bnapi.modular", "BN Mod API (Modular)", "1.0")]
	public class Plugin : BaseUnityPlugin
	{
		public static ManualLogSource Logger;
		public static ConfigFile ConfigRef;

		public void OnEnable()
		{
			Logger = base.Logger;
			Plugin.ConfigRef = Config;

			// 初始化全局调试日志开关（从 BepInEx 配置文件读取）
			Debugger.Initialize(Config);

			CustomText.ApplyHooks();
			CustomTraits.ApplyHooks();

			// 应用 SerializableHeroUpgrade.PreSave 空引用保护补丁
			SerializableHeroUpgradePatch.Apply();

			// 使用 StringBuilder 替代 string.Join 避免 Mono CLR 2.0 兼容性问题
			StringBuilder sb = new StringBuilder("======== [BadNorthAPI] 已就绪，特性ID: ");
            for (int i = 0; i < CustomTraits.StartingTraits.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(CustomTraits.StartingTraits[i]);
			}
			sb.Append(" (API 1.0) ========");
			Logger.LogInfo(sb.ToString());
		}

		public const string VERSION = "1.0";
	}
}