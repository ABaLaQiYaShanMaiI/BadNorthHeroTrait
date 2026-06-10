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
    [BepInPlugin("nacu.badnorthyuri1.0", "Bad North - Yuri Trait 1.0", "1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Logger;

        public void OnEnable()
        {
            Logger = base.Logger;
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            CustomSprites.AddCustomSprite(modPath, "trait_yuri");
            Logger.LogInfo("[Yuri] Sprite loaded");

            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<MindElite>(),
                MindElite.YURI_ID,
                true
            );
            Logger.LogInfo("[Yuri] Trait registered: " + MindElite.YURI_ID);

            CustomText.CustomTermsAdded -= AddCustomTerms;
            CustomText.CustomTermsAdded += AddCustomTerms;
            Logger.LogInfo("[Yuri] Localization callback added");

            Logger.LogInfo("======== [Yuri] Ready (1.0) ========");
        }

        public void OnDisable()
        {
            CustomText.CustomTermsAdded -= AddCustomTerms;
            Logger.LogInfo("[Yuri] Disabled");
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("YYYYY/TRAIT/YURI/NAME", "心灵精英");
            CustomText.AddCustomTerm("YYYYY/TRAIT/YURI/DESCSHORT", "不能心控，但能念力晕人");
            CustomText.AddCustomTerm("YYYYY/TRAIT/YURI/DESC", "指挥官每隔一段时间发动超能力\n伤害并眩晕附近的敌人\n效果随队伍等级提高\n人数低时攻速加快");
        }
    }
}