using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace BadNorthArcheryFix
{
	[BepInPlugin("ABaLaQiYaShanMaiI.badnortharcheryfix1.0", "Bad North - Archery Crash Fix 1.0", "1.0")]
	public class Plugin : BaseUnityPlugin
	{
		public new static ManualLogSource Logger;
		public static bool EnableGameplayLog;

		public void OnEnable()
		{
			Logger = base.Logger;

			EnableGameplayLog = Config.Bind(
				"Log",
				"EnableGameplayLog",
				false,
				"是否启用 ArcheryFix 游戏运行时日志（缺省关闭，调试时打开）。"
			).Value;

			Harmony harmony = new Harmony("ABaLaQiYaShanMaiI.badnortharcheryfix1.0.patches");
			ArcheryCrashFix.ApplyPatches(harmony);

			Logger.LogInfo("======== BadNorthArcheryFix 1.0 已就绪（仅专注技能防崩溃） ========");
		}
	}
}