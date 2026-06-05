using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BadNorthAPI;
using UnityEngine;

namespace BadNorthSweepingBlade
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

            CustomSprites.AddCustomSprite(modPath, "trait_sweepingblade");

            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<SweepingBlade>(),
                SweepingBlade.SWEEPINGBLADE_ID,
                true
            );

            CustomText.CustomTermsAdded += AddCustomTerms;

            Logger.LogInfo(string.Format("======== BadNorthSweepingBlade 已就绪，特性ID: {0} ========", SweepingBlade.SWEEPINGBLADE_ID));
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("NACU/TRAIT/SWEEP/NAME", "横扫之刃");
            CustomText.AddCustomTerm("NACU/TRAIT/SWEEP/DESCSHORT", "近战攻击变为范围伤害。");
            CustomText.AddCustomTerm("NACU/TRAIT/SWEEP/DESC", "所有近战单位的攻击会对其攻击方向120度扇形范围内2.5米内的额外敌人造成伤害。\n英雄拥有更大的横扫范围（3.5米）和更高的溅射伤害比例。");
        }
    }
}