using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthUltimateSquad
{
    [BepInDependency("nacu.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("nacu.badnorthultimatesquad", "Bad North - Ultimate Squad Trait", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Logger;

        public void OnEnable()
        {
            Logger = base.Logger;
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            CustomSprites.AddCustomSprite(modPath, "trait_ultimatesquad");

            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<UltimateSquad>(),
                UltimateSquad.ULTIMATE_ID,
                true
            );

            CustomText.CustomTermsAdded += AddCustomTerms;

            Logger.LogInfo("Fancy Traits loaded");
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("YYYYY/TRAIT/ULTIMATE/NAME", "终极部队");
            CustomText.AddCustomTerm("YYYYY/TRAIT/ULTIMATE/DESCSHORT", "部队力量提升，但升级很贵");
            CustomText.AddCustomTerm("YYYYY/TRAIT/ULTIMATE/DESC", "部队大幅提高实力\n升级价格非常高");
        }
    }
}