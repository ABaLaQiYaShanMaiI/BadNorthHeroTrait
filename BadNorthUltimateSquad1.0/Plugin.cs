// Author: ABaLaQiYaShanMaiI
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthUltimateSquad
{
    [BepInDependency("ABaLaQiYaShanMaiI.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("ABaLaQiYaShanMaiI.badnorthultimatesquad1.0", "Bad North - Ultimate Squad Trait 1.0", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Logger;

        public void OnEnable()
        {
            Logger = base.Logger;
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            CustomSprites.AddCustomSprite(modPath, "trait_ultimatesquad");
            Logger.LogInfo("[UltimateSquad] Sprite loaded");

            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<UltimateSquad>(),
                UltimateSquad.ULTIMATE_ID,
                true
            );
            Logger.LogInfo("[UltimateSquad] Trait registered: " + UltimateSquad.ULTIMATE_ID);

            CustomText.CustomTermsAdded -= AddCustomTerms;
            CustomText.CustomTermsAdded += AddCustomTerms;
            Logger.LogInfo("[UltimateSquad] Localization callback added");

            Logger.LogInfo("======== [UltimateSquad] Ready (1.0) ========");
        }

        public void OnDisable()
        {
            CustomText.CustomTermsAdded -= AddCustomTerms;
            Logger.LogInfo("[UltimateSquad] Disabled");
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/ULTIMATE/NAME", "终极部队 (BadNorthUltimateSquad1.0)");
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/ULTIMATE/DESCSHORT", "部队力量提升，但升级很贵 (BadNorthUltimateSquad1.0)");
            CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/ULTIMATE/DESC", "部队大幅提高实力\n升级价格非常高 (BadNorthUltimateSquad1.0)");
        }
    }
}