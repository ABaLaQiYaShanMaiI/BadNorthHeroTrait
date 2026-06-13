// Author: ABaLaQiYaShanMaiI
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthCheaperClass
{
    [BepInDependency("ABaLaQiYaShanMaiI.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("ABaLaQiYaShanMaiI.badnorthcheaperclass1.0", "Bad North - Cheaper Class Trait 1.0", "1.0")]
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
                "是否启用 CheaperClass 1.0 的游戏运行时日志（折扣应用详情等）。不影响加载/卸载日志。"
            ).Value;

            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            // 1. Load custom sprite
            CustomSprites.AddCustomSprite(modPath, "trait_cheaperclass");
            Logger.LogInfo("[CheaperClass] Sprite loaded");

            // 2. Register trait
            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<CheaperClass>(),
                CheaperClass.CHEAPERCLASS_ID,
                true  // alwaysUnlocked
            );
            Logger.LogInfo("[CheaperClass] Trait registered: " + CheaperClass.CHEAPERCLASS_ID);

            // 3. Add localization (防重复订阅)
            CustomText.CustomTermsAdded -= AddCustomTerms;
            CustomText.CustomTermsAdded += AddCustomTerms;
            Logger.LogInfo("[CheaperClass] Localization callback added");

            Logger.LogInfo("======== [CheaperClass] Ready (1.0) ========");
        }

        public void OnDisable()
        {
            CustomText.CustomTermsAdded -= AddCustomTerms;
            Logger.LogInfo("[CheaperClass] Disabled");
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/CCLASS/NAME", "迅捷精通 (BadNorthCheaperClass1.0)");
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/CCLASS/DESCSHORT", "职业升级费用更低 (BadNorthCheaperClass1.0)");
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/CCLASS/DESC", "该指挥官的职业升级费用降低 40%。 (BadNorthCheaperClass1.0)");
        }
    }
}