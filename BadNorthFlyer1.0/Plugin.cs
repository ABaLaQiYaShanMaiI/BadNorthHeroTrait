using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthFlyer
{
    [BepInDependency("nacu.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("nacu.badnorthflyer1.0", "Bad North - Flyer Trait 1.0", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Logger;

        public void OnEnable()
        {
            Logger = base.Logger;
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            // 1. Load custom sprite
            CustomSprites.AddCustomSprite(modPath, "trait_flyer");

            // 2. Register trait
            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<Flyer>(),
                Flyer.FLYER_ID,
                true  // alwaysUnlocked
            );

            // 3. Add localization
            CustomText.CustomTermsAdded += AddCustomTerms;

            Logger.LogInfo(string.Format("======== BadNorthFlyer 1.0 已就绪，特性ID: {0} ========", Flyer.FLYER_ID));
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("YYYYY/TRAIT/FLYER/NAME", "神鹰");
            CustomText.AddCustomTerm("YYYYY/TRAIT/FLYER/DESCSHORT", "让敌人飞起来");
            CustomText.AddCustomTerm("YYYYY/TRAIT/FLYER/DESC", "产生让敌人升天的力量。\n英雄获得飞斧能力，所有单位攻击附带击飞效果。");
        }
    }
}