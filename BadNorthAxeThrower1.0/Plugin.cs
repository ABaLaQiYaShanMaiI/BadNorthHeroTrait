// Author: ABaLaQiYaShanMaiI
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthAxeThrower
{
    [BepInDependency("ABaLaQiYaShanMaiI.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("ABaLaQiYaShanMaiI.badnorthaxethrower1.0", "Bad North - Axe Thrower Trait 1.0", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Logger;

        public void OnEnable()
        {
            Logger = base.Logger;
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            // 1. Load custom sprite
            CustomSprites.AddCustomSprite(modPath, "trait_axethrower");
            Logger.LogInfo("[AxeThrower] Sprite loaded");

            // 2. Register trait
            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<AxeThrower>(),
                AxeThrower.AXETHROWER_ID,
                true  // alwaysUnlocked
            );
            Logger.LogInfo("[AxeThrower] Trait registered: " + AxeThrower.AXETHROWER_ID);

            // 3. Add localization (防重复订阅)
            CustomText.CustomTermsAdded -= AddCustomTerms;
            CustomText.CustomTermsAdded += AddCustomTerms;
            Logger.LogInfo("[AxeThrower] Localization callback added");

            Logger.LogInfo("======== [AxeThrower] Ready (1.0) ========");
        }

        public void OnDisable()
        {
            CustomText.CustomTermsAdded -= AddCustomTerms;
            Logger.LogInfo("[AxeThrower] Disabled");
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/AXE/NAME", "掷斧手 (BadNorthAxeThrower1.0)");
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/AXE/DESCSHORT", "指挥官可以投掷战斧 (BadNorthAxeThrower1.0)");
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/AXE/DESC", "指挥官会投掷战斧。\n战斧的数量与威力会随小队等级变化。 (BadNorthAxeThrower1.0)");
        }
    }
}