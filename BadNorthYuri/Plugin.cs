using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthYuri
{
    [BepInDependency("nacu.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("nacu.badnorthyuri", "Bad North - Yuri Trait", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Logger;

        public void OnEnable()
        {
            Logger = base.Logger;
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            CustomSprites.AddCustomSprite(modPath, "trait_yuri");

            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<MindElite>(),
                MindElite.MINDELITE_ID,
                true
            );

            CustomText.CustomTermsAdded += AddCustomTerms;

            Logger.LogInfo(string.Format("======== BadNorthYuri 已就绪，特性ID: {0} ========", MindElite.MINDELITE_ID));
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("NACU/TRAIT/MIND/NAME", "心灵精英");
            CustomText.AddCustomTerm("NACU/TRAIT/MIND/DESCSHORT", "指挥官的精神力辐射全队。");
            CustomText.AddCustomTerm("NACU/TRAIT/MIND/DESC", "指挥官强大的精神力提升全队战力。\n所有单位获得伤害提升、攻击加速、精准度提高、移速提升和恐惧免疫。\n英雄自身获得额外增强。");
        }
    }
}