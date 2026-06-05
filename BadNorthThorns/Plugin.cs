using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthThorns
{
    [BepInDependency("nacu.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("nacu.badnorththorns", "Bad North - Thorns Trait", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Logger;

        public void OnEnable()
        {
            Logger = base.Logger;
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            // 1. Load custom sprite
            CustomSprites.AddCustomSprite(modPath, "trait_thorns");

            // 2. Register trait
            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<Thorns>(),
                Thorns.THORNS_ID,
                false  // not alwaysUnlocked
            );

            // 3. Add localization
            CustomText.CustomTermsAdded += AddCustomTerms;

            Logger.LogInfo($"======== BadNorthThorns 已就绪，特性ID: {Thorns.THORNS_ID} ========");
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("NACU/TRAIT/THORNS/NAME", "荆棘");
            CustomText.AddCustomTerm("NACU/TRAIT/THORNS/DESCSHORT", "近战攻击者会受到反伤");
            CustomText.AddCustomTerm("NACU/TRAIT/THORNS/DESC", "所有近战攻击者都会受到少量反伤。");
        }
    }
}
