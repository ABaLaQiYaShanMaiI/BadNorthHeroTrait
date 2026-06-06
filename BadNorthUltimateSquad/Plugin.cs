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
                UltimateSquad.ULTIMATESQUAD_ID,
                true
            );

            CustomText.CustomTermsAdded += AddCustomTerms;

            Logger.LogInfo(string.Format("======== BadNorthUltimateSquad 已就绪，特性ID: {0} ========", UltimateSquad.ULTIMATESQUAD_ID));
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("NACU/TRAIT/ULTIMATE/NAME", "终极部队");
            CustomText.AddCustomTerm("NACU/TRAIT/ULTIMATE/DESCSHORT", "少数精锐，极限战力。");
            CustomText.AddCustomTerm("NACU/TRAIT/ULTIMATE/DESC", "小队人数大幅削减为原来的1/3，但每个单位都拥有巨大的体型、极高的伤害、护甲和抗性。\n步兵获得跳劈能力，弓箭手大幅强化射速与精准度。");
        }
    }
}