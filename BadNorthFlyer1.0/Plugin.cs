// Author: ABaLaQiYaShanMaiI
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthFlyer
{
    [BepInDependency("ABaLaQiYaShanMaiI.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("ABaLaQiYaShanMaiI.badnorthflyer1.0", "Bad North - Flyer Trait 1.0", "1.0")]
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
                "是否启用 Flyer 1.0 的游戏运行时日志。不影响加载/卸载日志。"
            ).Value;

            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            // 1. Load custom sprite
            CustomSprites.AddCustomSprite(modPath, "trait_flyer");
            Logger.LogInfo("[Flyer] Sprite loaded");

            // 2. Register trait
            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<Flyer>(),
                Flyer.FLYER_ID,
                true  // alwaysUnlocked
            );
            Logger.LogInfo("[Flyer] Trait registered: " + Flyer.FLYER_ID);

            // 3. Add localization (防重复订阅)
            CustomText.CustomTermsAdded -= AddCustomTerms;
            CustomText.CustomTermsAdded += AddCustomTerms;
            Logger.LogInfo("[Flyer] Localization callback added");

            Logger.LogInfo("======== [Flyer] Ready (1.0) ========");
        }

        public void OnDisable()
        {
            CustomText.CustomTermsAdded -= AddCustomTerms;
            Logger.LogInfo("[Flyer] Disabled");
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/FLYER/NAME", "神鹰 (BadNorthFlyer1.0)");
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/FLYER/DESCSHORT", "让敌人飞起来 (BadNorthFlyer1.0)");
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/FLYER/DESC", "产生让敌人升天的力量。\n英雄获得飞斧能力，所有单位攻击附带击飞效果。 (BadNorthFlyer1.0)");
        }
    }
}