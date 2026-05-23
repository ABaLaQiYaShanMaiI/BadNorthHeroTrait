using Voxels.TowerDefense.Ballistics;
using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorthAxeThrower
{
    public class AxeThrower : HeroUpgradeDefinition
    {
        public static readonly string AXETHROWER_ID = "Hero_Trait_AxeThrower";

        // 默认数值（当模板获取失败时使用）
        private static readonly Voxels.TowerDefense.Ballistics.AttackSettings DefaultAttackSettings = new Voxels.TowerDefense.Ballistics.AttackSettings
        {
            damage = 1f,
            knockback = 1f,
            stun = 1f,
            launchImpulse = 1f
        };

        public AxeThrower()
        {
            Plugin.Logger.LogInfo("AXETHROWER CREATED");
            this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
            this.upgradeType.typeEnum = 4; // AxeThrower = 4
            this.upgradeType.canBeStartItem = true;
            this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
            this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
            this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
            this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
            this.affectsPortrait = false;
            base.name = AXETHROWER_ID;
            this.nameTerm = "NACU/TRAIT/AXE/NAME";
            this.shortDescription = "NACU/TRAIT/AXE/DESCSHORT";
            this.infoSprite = CustomSprites.Sprites["trait_axe"];
            HeroUpgradeDefinition.Level[] array = new HeroUpgradeDefinition.Level[1];
            int num = 0;
            HeroUpgradeDefinition.Level level = default(HeroUpgradeDefinition.Level);
            level.cost = 0;
            level.description = "NACU/TRAIT/AXE/DESC";
            array[num] = level;
            this.levels = array;
        }

        /// <summary>
        /// 安全获取 AxeThrowing 模板，支持多级容错：
        /// 1. 尝试从 LevelStateObjectReferences.dict 获取
        /// 2. 失败时遍历 Resources 查找
        /// 3. 均失败则使用内置默认值
        /// </summary>
        private static AxeThrowing GetAxeThrowingTemplate()
        {
            // 方法1: 从 LevelStateObjectReferences.dict 获取
            try
            {
                if (LevelStateObjectReferences.dict != null &&
                    LevelStateObjectReferences.dict.TryGetValue("Viking_AxeThrower", out var reference) &&
                    reference is VikingReference vikingRef &&
                    vikingRef.viking != null &&
                    vikingRef.viking.agent != null)
                {
                    var template = vikingRef.viking.agent.GetComponent<Voxels.TowerDefense.Ballistics.AxeThrowing>();
                    if (template != null)
                    {
                        Plugin.Logger.LogInfo("[AxeThrower] 从 LevelStateObjectReferences 成功获取模板");
                        return template;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning("[AxeThrower] LevelStateObjectReferences 获取失败: " + ex.Message);
            }

            // 方法2: 遍历 Resources 查找 AxeThrowing 组件
            try
            {
                var allAxeThrowings = Resources.FindObjectsOfTypeAll<Voxels.TowerDefense.Ballistics.AxeThrowing>();
                if (allAxeThrowings != null && allAxeThrowings.Length > 0)
                {
                    Plugin.Logger.LogInfo("[AxeThrower] 从 Resources.FindObjectsOfTypeAll 获取模板 (共 " + allAxeThrowings.Length + " 个)");
                    return allAxeThrowings[0];
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning("[AxeThrower] Resources.FindObjectsOfTypeAll 获取失败: " + ex.Message);
            }

            // 方法3: 均失败，返回 null（调用方使用默认值）
            Plugin.Logger.LogWarning("[AxeThrower] 无法获取 AxeThrowing 模板，将使用内置默认值");
            return null;
        }

        /// <summary>
        /// 根据小队等级应用攻击属性倍率
        /// </summary>
        private static void ApplyLevelScaling(AxeThrowing comp, int squadLevel)
        {
            switch (squadLevel)
            {
                case 0:
                    comp.ammo = 5;
                    comp.attackSettings.launchImpulse *= 0.9f;
                    break;
                case 1:
                    comp.ammo = 8;
                    comp.attackSettings.damage *= 1.33f;
                    comp.attackSettings.knockback *= 1.33f;
                    comp.attackSettings.stun *= 1.5f;
                    break;
                case 2:
                    comp.ammo = 11;
                    comp.attackSettings.damage *= 1.66f;
                    comp.attackSettings.launchImpulse *= 1.1f;
                    comp.attackSettings.knockback *= 1.66f;
                    comp.attackSettings.stun *= 2f;
                    break;
                default:
                    comp.ammo = 14;
                    comp.attackSettings.damage *= 2f;
                    comp.attackSettings.launchImpulse *= 1.2f;
                    comp.attackSettings.knockback *= 2f;
                    comp.attackSettings.stun *= 2.5f;
                    break;
            }
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            // 确保 LineOfSight 组件存在
            squad.heroAgent.GetOrAddComponent<LineOfSight>();

            // 获取模板（带容错）
            Voxels.TowerDefense.Ballistics.AxeThrowing template = GetAxeThrowingTemplate();

            // 添加 AxeThrowing 组件
            squad.heroAgent.gameObject.AddComponent<Voxels.TowerDefense.Ballistics.AxeThrowing>();
            Voxels.TowerDefense.Ballistics.AxeThrowing comp = squad.heroAgent.GetComponent<Voxels.TowerDefense.Ballistics.AxeThrowing>();

            if (template != null)
            {
                // 从模板复制属性
                comp.prepareSound = template.prepareSound;
                comp.throwingAxePrefab = template.throwingAxePrefab;
                comp.trajectoryUtility = template.trajectoryUtility;
                comp.attackSettings = template.attackSettings;
            }
            else
            {
                // 使用内置默认值
                Plugin.Logger.LogWarning("[AxeThrower] 使用内置默认 AttackSettings");
                comp.attackSettings = new Voxels.TowerDefense.Ballistics.AttackSettings
                {
                    damage = 1f,
                    knockback = 1f,
                    stun = 1f,
                    launchImpulse = 1f
                };
            }

            // 根据小队等级调整属性
            ApplyLevelScaling(comp, squad.hero.squadLevel);

            // 初始化组件
            comp.Setup();
        }
    }
}
