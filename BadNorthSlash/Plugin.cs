using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthSlash
{
    [BepInDependency("nacu.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("nacu.badnorthsweepingblade", "Bad North - Sweeping Blade Trait", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Logger;

        public void OnEnable()
        {
            Logger = base.Logger;
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            CustomSprites.AddCustomSprite(modPath, "trait_slash");

            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<SweepingBlade>(),
                SweepingBlade.Slash_ID,
                true
            );

            CustomText.CustomTermsAdded += AddCustomTerms;

            Logger.LogInfo("Fancy Traits loaded");
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("YYYYY/TRAIT/SLASH/NAME", "横扫之刃");
            CustomText.AddCustomTerm("YYYYY/TRAIT/SLASH/DESCSHORT", "命中额外目标");
            CustomText.AddCustomTerm("YYYYY/TRAIT/SLASH/DESC", "单位的攻击能击中多个敌人");
        }
    }
}