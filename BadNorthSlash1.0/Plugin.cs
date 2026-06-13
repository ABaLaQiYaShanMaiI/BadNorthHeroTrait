// Author: ABaLaQiYaShanMaiI
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthSlash
{
    [BepInDependency("ABaLaQiYaShanMaiI.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("ABaLaQiYaShanMaiI.badnorthsweepingblade1.0", "Bad North - Sweeping Blade Trait 1.0", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Logger;

        public void OnEnable()
        {
            Logger = base.Logger;
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            CustomSprites.AddCustomSprite(modPath, "trait_slash");
            Logger.LogInfo("[Slash] Sprite loaded");

            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<SweepingBlade>(),
                SweepingBlade.Slash_ID,
                true
            );
            Logger.LogInfo("[Slash] Trait registered: " + SweepingBlade.Slash_ID);

            CustomText.CustomTermsAdded -= AddCustomTerms;
            CustomText.CustomTermsAdded += AddCustomTerms;
            Logger.LogInfo("[Slash] Localization callback added");

            Logger.LogInfo("======== [Slash] Ready (1.0) ========");
        }

        public void OnDisable()
        {
            CustomText.CustomTermsAdded -= AddCustomTerms;
            Logger.LogInfo("[Slash] Disabled");
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/SLASH/NAME", "横扫之刃 (BadNorthSlash1.0)");
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/SLASH/DESCSHORT", "命中额外目标 (BadNorthSlash1.0)");
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/SLASH/DESC", "单位的攻击能击中多个敌人 (BadNorthSlash1.0)");
        }
    }
}