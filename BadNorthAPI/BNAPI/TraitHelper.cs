// Author: ABaLaQiYaShanMaiI
using System;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthAPI
{
    /// <summary>
    /// Trait 公共初始化工具。
    /// 提供创建标准 HeroUpgradeType、单级 levels 等模板方法，
    /// 减少各 Trait 中的重复代码。
    /// </summary>
    public static class TraitHelper
    {
        /// <summary>
        /// 创建标准的 Trait 类型 HeroUpgradeType。
        /// 设置 typeEnum = HeroUpgradeTypeEnum.Trait (4)、canBeStartItem = true 及默认未知/锁定文本。
        /// </summary>
        public static HeroUpgradeType CreateTraitUpgradeType(
            string unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME",
            string unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC",
            string startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED",
            string startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED")
        {
            HeroUpgradeType type = ScriptableObject.CreateInstance<HeroUpgradeType>();
            type.typeEnum = HeroUpgradeTypeEnum.Trait;
            type.canBeStartItem = true;
            type.unknownNameTerm = unknownNameTerm;
            type.unknownDescriptionTerm = unknownDescriptionTerm;
            type.startItemLockedTerm = startItemLockedTerm;
            type.startItemUnlockedTerm = startItemUnlockedTerm;
            return type;
        }

        /// <summary>
        /// 创建标准单级 levels 数组（cost=0）。
        /// </summary>
        public static HeroUpgradeDefinition.Level[] CreateSingleLevel(string description, int cost = 0)
        {
            return new HeroUpgradeDefinition.Level[]
            {
                new HeroUpgradeDefinition.Level
                {
                    cost = cost,
                    description = description
                }
            };
        }

        /// <summary>
        /// 设置 HeroUpgradeDefinition 的基本属性（name, nameTerm, shortDescription, infoSprite, affectsPortrait, levels）。
        /// 返回自身以支持链式调用。
        /// </summary>
        public static T SetupBaseDefinition<T>(T def, string traitID, string nameTerm, string shortDescription,
            Sprite infoSprite, HeroUpgradeDefinition.Level[] levels) where T : HeroUpgradeDefinition
        {
            def.name = traitID;
            def.nameTerm = nameTerm;
            def.shortDescription = shortDescription;
            def.infoSprite = infoSprite;
            def.affectsPortrait = false;
            def.levels = levels;
            return def;
        }
    }
}