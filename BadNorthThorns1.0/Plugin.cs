// Author: ABaLaQiYaShanMaiI
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthThorns
{
    [BepInDependency("ABaLaQiYaShanMaiI.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("ABaLaQiYaShanMaiI.badnorththorns1.0", "Bad North - Thorns Trait 1.0", "1.0")]
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
                "是否启用 Thorns 1.0 的游戏运行时日志。不影响加载/卸载日志。"
            ).Value;

            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            // 1. Load custom sprite
            CustomSprites.AddCustomSprite(modPath, "trait_thorns");
            Logger.LogInfo("[Thorns] Sprite loaded");

            // 2. Register trait
            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<Thorns>(),
                Thorns.THORNS_ID,
                false  // not alwaysUnlocked
            );
            Logger.LogInfo("[Thorns] Trait registered: " + Thorns.THORNS_ID);

            // 3. Add localization (防重复订阅)
            CustomText.CustomTermsAdded -= AddCustomTerms;
            CustomText.CustomTermsAdded += AddCustomTerms;
            Logger.LogInfo("[Thorns] Localization callback added");

            Logger.LogInfo("======== [Thorns] Ready (1.0) ========");
        }

        public void OnDisable()
        {
            CustomText.CustomTermsAdded -= AddCustomTerms;
            Logger.LogInfo("[Thorns] Disabled");
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/THORNS/NAME", "荆棘 (BadNorthThorns1.0)");
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/THORNS/DESCSHORT", "近战攻击者会受到反伤 (BadNorthThorns1.0)");
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/THORNS/DESC", "所有近战攻击者都会受到少量反伤。 (BadNorthThorns1.0)");
        }
    }
}