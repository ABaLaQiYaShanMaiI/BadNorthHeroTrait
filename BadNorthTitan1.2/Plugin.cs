// Author: ABaLaQiYaShanMaiI
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BadNorthAPI;
using HarmonyLib;
using UnityEngine;

namespace BadNorthTitan
{
	[BepInDependency("ABaLaQiYaShanMaiI.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
	[BepInPlugin("ABaLaQiYaShanMaiI.badnorthtitan1.2", "Bad North - Titan Trait 1.2", "1.2")]
	public class Plugin : BaseUnityPlugin
	{
		public static ManualLogSource Logger;
		public static bool EnableGameplayLog;

		public void OnEnable()
		{
			Logger = base.Logger;

			EnableGameplayLog = Config.Bind(
				"Log",
				"EnableGameplayLog",
				true,
				"是否启用 Titan 1.2 的游戏运行时日志（Harmony patch 诊断、TitanFocusHelper 射击日志等）。不影响加载/卸载日志。"
			).Value;

			string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

			// 1. Load custom sprite
			CustomSprites.AddCustomSprite(modPath, "trait_titan");

			// 2. Register trait
			CustomTraits.RegisterTrait(
				ScriptableObject.CreateInstance<Titan>(),
				Titan.Titan_ID,
				true
			);

			// 3. Add localization (防重复订阅)
			CustomText.CustomTermsAdded -= AddCustomTerms;
			CustomText.CustomTermsAdded += AddCustomTerms;

			// 4. Apply Titan archery Harmony patches (8 patches: sight/aim/ballistics + focus fix)
			Harmony harmony = new Harmony("ABaLaQiYaShanMaiI.badnorthtitan1.2.archeryfix");
			TitanArcheryFixes.ApplyPatches(harmony);

			Logger.LogInfo("======== BadNorthTitan 1.2 已就绪 ========");
		}

		public void OnDisable()
		{
			CustomText.CustomTermsAdded -= AddCustomTerms;
			Logger.LogInfo("[Titan] Disabled");
		}

		private void AddCustomTerms()
		{
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/TITAN/NAME", "泰坦 (BadNorthTitan1.2)");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/TITAN/DESCSHORT", "真正的巨人之力，盾弓皆可，升级后起效 (BadNorthTitan1.2)");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/TITAN/DESC", "步兵与弓箭手皆可获得泰坦之力。\n大幅提升伤害、护甲与抗性，但小队人数减半。\n需要小队达到1级后解锁。 (BadNorthTitan1.2)");
		}
	}
}