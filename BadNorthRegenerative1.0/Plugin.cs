using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthRegenerative
{
    [BepInDependency("nacu.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("nacu.badnorthregenerative1.0", "Bad North - Regenerative Trait 1.0", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Logger;

        public void OnEnable()
        {
            Logger = base.Logger;
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            // 1. Load custom sprite
            CustomSprites.AddCustomSprite(modPath, "trait_regenerative");
            Logger.LogInfo("[Regenerative] Sprite loaded");

            // 2. Register trait
            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<Regenerative>(),
                Regenerative.REGENERATIVE_ID,
                false  // not alwaysUnlocked
            );
            Logger.LogInfo("[Regenerative] Trait registered: " + Regenerative.REGENERATIVE_ID);

            // 3. Add localization (防重复订阅)
            CustomText.CustomTermsAdded -= AddCustomTerms;
            CustomText.CustomTermsAdded += AddCustomTerms;
            Logger.LogInfo("[Regenerative] Localization callback added");

            Logger.LogInfo("======== [Regenerative] Ready (1.0) ========");
        }

        public void OnDisable()
        {
            CustomText.CustomTermsAdded -= AddCustomTerms;
            Logger.LogInfo("[Regenerative] Disabled");
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("NACU/TRAIT/REGENERATIVE/NAME", "追猎");
            CustomText.AddCustomTerm("NACU/TRAIT/REGENERATIVE/DESCSHORT", "特化部队，依据当前兵种获得不同的针对性机制。");
            CustomText.AddCustomTerm("NACU/TRAIT/REGENERATIVE/DESC", "基础移速很快，依据当前兵种获得对应的针对性特化效果。");
            CustomText.AddCustomTerm("NACU/HERO_TRAITS/REGENERATIVE/ABILITY_TOOLTIP", "精锐部队无法在房屋补员。");
        }
    }
}