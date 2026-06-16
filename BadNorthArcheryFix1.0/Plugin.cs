// Author: ABaLaQiYaShanMaiI
// BadNorthArcheryFix 1.0 — 通用巨人弓箭手崩溃修复
// 零外部依赖（不依赖 BadNorthAPI），纯 Harmony 补丁。
// 自动检测所有 scale > 1.0f 的英军弓箭手，透明修复专注技能崩溃。
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

			// 注册 3 个 Harmony 补丁（仅专注技能防崩溃）
			Harmony harmony = new Harmony("ABaLaQiYaShanMaiI.badnortharcheryfix1.0.patches");
			ArcheryCrashFix.ApplyPatches(harmony);

			Logger.LogInfo("======== BadNorthArcheryFix 1.0 已就绪（仅专注技能防崩溃） ========");
		}
	}
}