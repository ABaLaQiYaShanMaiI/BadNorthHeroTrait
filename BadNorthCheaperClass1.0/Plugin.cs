using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthCheaperClass
{
    [BepInDependency("nacu.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("nacu.badnorthcheaperclass1.0", "Bad North - Cheaper Class Trait 1.0", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Logger;

        public void OnEnable()
        {
            Logger = base.Logger;
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            // 1. Load custom sprite
            CustomSprites.AddCustomSprite(modPath, "trait_cheaperclass");

            // 2. Register trait
            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<CheaperClass>(),
                CheaperClass.CHEAPERCLASS_ID,
                true  // alwaysUnlocked
            );

            // 3. Add localization
            CustomText.CustomTermsAdded += AddCustomTerms;

            Logger.LogInfo($"======== BadNorthCheaperClass 1.0 已就绪，特性ID: {CheaperClass.CHEAPERCLASS_ID} ========");
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("NACU/TRAIT/CCLASS/NAME", "迅捷精通");
            CustomText.AddCustomTerm("NACU/TRAIT/CCLASS/DESCSHORT", "职业升级费用更低");
            CustomText.AddCustomTerm("NACU/TRAIT/CCLASS/DESC", "该指挥官的职业升级费用降低 40%。");
        }
    }
}
