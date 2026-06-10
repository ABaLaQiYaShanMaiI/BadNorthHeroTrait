using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BadNorthAPI;
using HarmonyLib;
using UnityEngine;

namespace BadNorthTitan
{
	[BepInDependency("nacu.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
	[BepInPlugin("nacu.badnorthtitan1.1", "Bad North - Titan Trait 1.1", "1.1")]
	public class Plugin : BaseUnityPlugin
	{
		public static ManualLogSource Logger;

		public void OnEnable()
		{
			Logger = base.Logger;
			string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

			// 1. Load custom sprite
			CustomSprites.AddCustomSprite(modPath, "trait_titan");

			// 2. Register trait
			CustomTraits.RegisterTrait(
				ScriptableObject.CreateInstance<Titan>(),
				Titan.Titan_ID,
				true
			);

			// 3. Add localization
			CustomText.CustomTermsAdded += AddCustomTerms;

			// 4. Apply Titan archery Harmony patches (8 patches: sight/aim/ballistics + focus fix)
			Harmony harmony = new Harmony("nacu.badnorthtitan1.1.archeryfix");
			TitanArcheryFixes.ApplyPatches(harmony);

			Logger.LogInfo("======== BadNorthTitan 1.1 已就绪 ========");
		}

		private void AddCustomTerms()
		{
			CustomText.AddCustomTerm("YYYYY/TRAIT/TITAN/NAME", "泰坦");
			CustomText.AddCustomTerm("YYYYY/TRAIT/TITAN/DESCSHORT", "真正的巨人之力，盾弓皆可，升级后起效");
			CustomText.AddCustomTerm("YYYYY/TRAIT/TITAN/DESC", "步兵与弓箭手皆可获得泰坦之力。\n大幅提升伤害、护甲与抗性，但小队人数减半。\n需要小队达到1级后解锁。");
		}
	}
}