using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Upgrades;

namespace BadNorthRegenerative
{
    public class Regenerative : HeroUpgradeDefinition
    {
        public static readonly string REGENERATIVE_ID = "Hero_Trait_Regenerative";

        private int currentSquadLevel;

        [SerializeField]
        private float[] speedLevels = new float[]
        {
            2.4f,
            2.6f,
            2.8f,
            3f
        };

        [Header("Sound")]
        [SerializeField]
        private string swordSoundPrefix = "Sfx/Viking/Twohanded";
        [SerializeField]
        private FabricEventReference moveSound = "Sfx/Viking/Twohanded/Move";
        [SerializeField]
        private FabricEventReference deathSound = "Sfx/Viking/Twohanded/Die";
        [SerializeField]
        private FabricEventReference swingSound = "Sfx/Viking/Twohanded/Swing";
        [SerializeField]
        private FabricEventReference swordSound = "Sfx/Viking/Twohanded";

        public Regenerative()
        {
            Plugin.Logger.LogInfo("REGENERATIVE CREATED");
            this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
            this.upgradeType.typeEnum = (HeroUpgradeTypeEnum)4;
            this.upgradeType.canBeStartItem = true;
            this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
            this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
            this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
            this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
            this.affectsPortrait = false;
            base.name = REGENERATIVE_ID;
            this.nameTerm = "NACU/TRAIT/REGENERATIVE/NAME";
            this.shortDescription = "NACU/TRAIT/REGENERATIVE/DESCSHORT";
            this.infoSprite = CustomSprites.Sprites["trait_regenerative"];
            HeroUpgradeDefinition.Level[] array = new HeroUpgradeDefinition.Level[1];
            int num = 0;
            HeroUpgradeDefinition.Level[] array2 = array;
            int num2 = num;
            HeroUpgradeDefinition.Level level = default(HeroUpgradeDefinition.Level);
            level.cost = 0;
            level.description = "NACU/TRAIT/REGENERATIVE/DESC";
            array2[num2] = level;
            this.levels = array;
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);
            ManualLogSource logger = Plugin.Logger;
            string format = "[Regenerative] OnAppliedToSquad: squad={0}, agents.Count={1}, livingAgents.Count={2}";
            object arg = (squad != null) ? squad.name : null;
            int? num;
            if (squad == null)
            {
                num = null;
            }
            else
            {
                List<Agent> agents = squad.agents;
                num = ((agents != null) ? new int?(agents.Count) : null);
            }
            object arg2 = num;
            int? num2;
            if (squad == null)
            {
                num2 = null;
            }
            else
            {
                List<Agent> livingAgents = squad.livingAgents;
                num2 = ((livingAgents != null) ? new int?(livingAgents.Count) : null);
            }
            logger.LogInfo(string.Format(format, arg, arg2, num2));
            if (!this.IsShieldInfantry(squad) && !this.IsSpearInfantry(squad))
            {
                Plugin.Logger.LogInfo("[Regenerative] 非盾兵/非矛兵，跳过改造");
                return;
            }
            bool flag = this.IsShieldInfantry(squad);
            Plugin.Logger.LogInfo(flag ? "[Regenerative] 盾兵小队，执行双刀改造" : "[Regenerative] 矛兵小队，执行不抬枪改造");
            int num3 = 0;
            foreach (Agent agent in squad.agents)
            {
                this.ModifyAgentProperties(agent, upgradeLevel);
                this.ApplyDualWieldAnimations(agent);
                num3++;
            }
            foreach (Agent agent2 in squad.livingAgents)
            {
                if (!squad.agents.Contains(agent2))
                {
                    this.ModifyAgentProperties(agent2, upgradeLevel);
                    this.ApplyDualWieldAnimations(agent2);
                    num3++;
                }
            }
            this.currentSquadLevel = upgradeLevel;
            squad.onAgentCreated -= this.OnAgentCreated;
            squad.onAgentCreated += this.OnAgentCreated;
        }

        private bool IsShieldInfantry(EnglishSquad squad)
        {
            if (squad == null)
            {
                Plugin.Logger.LogWarning("[Regenerative] IsShieldInfantry: squad为null");
                return false;
            }
            if (squad.minionPrefab == null)
            {
                Plugin.Logger.LogWarning("[Regenerative] IsShieldInfantry: minionPrefab为null");
                return false;
            }
            bool flag = squad.minionPrefab.GetComponent<Swordsman>() != null;
            Plugin.Logger.LogInfo(string.Format("[Regenerative] IsShieldInfantry: minionPrefab={0}, hasSwordsman={1}", squad.minionPrefab.name, flag));
            return flag;
        }

        private void SetupAgentSounds(Agent agent, Swordsman swordsman)
        {
            if (agent.body != null && !string.IsNullOrEmpty(this.moveSound?.name))
            {
                agent.body.baseMoveSoundRef = this.moveSound;
            }
            if (!string.IsNullOrEmpty(this.swingSound?.name))
            {
                swordsman.swingSound = this.swingSound.name;
            }
            if (!string.IsNullOrEmpty(this.swordSoundPrefix))
            {
                swordsman.chargeSound = this.swordSoundPrefix;
            }
            if (!string.IsNullOrEmpty(this.swordSound?.name))
            {
                swordsman.swordSound = this.swordSound.name;
            }
            Death component = agent.GetComponent<Death>();
            if (component != null && !string.IsNullOrEmpty(this.deathSound?.name))
            {
                component.deathSound = this.deathSound.name;
            }
        }

        private void ApplyDualWieldAnimations(Agent agent)
        {
            if (agent.GetComponent<Swordsman>() == null)
            {
                return;
            }
            Animator component = agent.GetComponent<Animator>();
            if (component == null || component.runtimeAnimatorController == null)
            {
                return;
            }
            AnimatorOverrideController animatorOverrideController = new AnimatorOverrideController(component.runtimeAnimatorController);
            Dictionary<string, AnimationClip> dictionary = new Dictionary<string, AnimationClip>
            {
                { "Swordsman_Idle", null },
                { "Swordsman_Walk", null },
                { "Swordsman_Run", null },
                { "Swordsman_Attack", null },
                { "Swordsman_Ragdoll", null },
                { "Swordsman_Death", null }
            };
            foreach (AnimationClip animationClip in Resources.FindObjectsOfTypeAll<AnimationClip>())
            {
                string name = animationClip.name;
                if (!(name == "Twohanded_Idle"))
                {
                    if (!(name == "Twohanded_Walk"))
                    {
                        if (!(name == "Twohanded_Run"))
                        {
                            if (!(name == "Twohanded_Attack"))
                            {
                                if (!(name == "Twohanded_Ragdoll"))
                                {
                                    if (name == "Twohanded_Death")
                                    {
                                        dictionary["Swordsman_Death"] = animationClip;
                                    }
                                }
                                else
                                {
                                    dictionary["Swordsman_Ragdoll"] = animationClip;
                                }
                            }
                            else
                            {
                                dictionary["Swordsman_Attack"] = animationClip;
                            }
                        }
                        else
                        {
                            dictionary["Swordsman_Run"] = animationClip;
                        }
                    }
                    else
                    {
                        dictionary["Swordsman_Walk"] = animationClip;
                    }
                }
                else
                {
                    dictionary["Swordsman_Idle"] = animationClip;
                }
            }
            foreach (KeyValuePair<string, AnimationClip> keyValuePair in dictionary)
            {
                if (keyValuePair.Value != null)
                {
                    animatorOverrideController[keyValuePair.Key] = keyValuePair.Value;
                }
            }
            component.runtimeAnimatorController = animatorOverrideController;
        }

        private void OnAgentCreated(Agent agent)
        {
            if (agent == null)
            {
                return;
            }
            if (agent.body == null)
            {
                return;
            }
            this.ModifyAgentProperties(agent, this.currentSquadLevel);
            this.ApplyDualWieldAnimations(agent);
        }

        // 使用字符串名称引用可能不存在的类型，避免 JIT 编译时 TypeLoadException
        private static readonly string SpearShieldTypeName = "Voxels.TowerDefense.SpearShield";

        private void RemoveSpearShieldIfPresent(Agent agent)
        {
            try
            {
                // 使用 GameObject.GetComponent(string) 通过字符串查找组件
                Component shield = agent.GetComponent(SpearShieldTypeName);
                if (shield != null)
                {
                    // 禁用组件并关闭其 GameObject，防止渲染引用崩溃
                    ((MonoBehaviour)shield).enabled = false;
                    shield.gameObject.SetActive(false);
                    // 不再需要 RefreshRenderers
                }
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning(string.Format("[Regenerative] 移除 SpearShield 失败：{0}", ex.Message));
            }
        }

        // 控制 isGiant 警告仅输出一次
        private static bool _isGiantWarningLogged = false;

        private void ModifyAgentProperties(Agent agent, int squadLevel)
        {
            if (agent == null)
            {
                Plugin.Logger.LogWarning("[Regenerative] ModifyAgentProperties: agent为null");
                return;
            }
            Swordsman component = agent.GetComponent<Swordsman>();
            if (component != null)
            {
                // 使用反射安全设置 isGiant，避免因版本差异导致 MissingFieldException
                var isGiantField = typeof(Swordsman).GetField("isGiant", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!ReferenceEquals(isGiantField, null))
                {
                    isGiantField.SetValue(component, true);
                }
                else if (!_isGiantWarningLogged)
                {
                    _isGiantWarningLogged = true;
                    Plugin.Logger.LogWarning("[Regenerative] Swordsman.isGiant field not found, skipping. (此警告仅显示一次)");
                }
                for (int i = 0; i < component.stunLevels.Length; i++)
                {
                    component.stunLevels[i] *= 0.1f;
                }
                if (agent.GetComponent<RegenerativeBuff>() == null)
                {
                    agent.gameObject.AddComponent<RegenerativeBuff>();
                }
                if (agent.GetComponent<RegenerativeJumpResponder>() == null)
                {
                    agent.gameObject.AddComponent<RegenerativeJumpResponder>();
                }
                this.RemoveShield(component, agent);
                int num = Mathf.Clamp(squadLevel, 0, this.speedLevels.Length - 1);
                agent.maxSpeed = this.speedLevels[num];
                this.SetupAgentSounds(agent, component);
                return;
            }
            if (agent.GetComponent<Spear>() != null)
            {
                if (agent.GetComponent<RegenerativeSpearBuff>() == null)
                {
                    agent.gameObject.AddComponent<RegenerativeSpearBuff>();
                }
                // 使用反射方式移除 SpearShield，避免硬编码类型引用导致 JIT 崩溃
                this.RemoveSpearShieldIfPresent(agent);
                return;
            }
            Plugin.Logger.LogWarning("[Regenerative] " + agent.name + " 无Swordsman/无Spear组件，跳过改造");
        }

        private void RemoveShield(Swordsman swordsman, Agent agent)
        {
            Shield shield = swordsman.shield;
            if (shield)
            {
                if (shield.shield != null)
                {
                    // 不再销毁，改为禁用，避免 AgentSelected 访问已销毁的渲染器
                    shield.shield.gameObject.SetActive(false);
                }
                shield.enabled = false;          // 禁止盾牌逻辑
                swordsman.shield = null;
                // 不再调用 RefreshRenderers（该版本无此方法）
            }
        }

        private bool IsSpearInfantry(EnglishSquad squad)
        {
            if (squad == null)
            {
                Plugin.Logger.LogWarning("[Regenerative] IsSpearInfantry: squad为null");
                return false;
            }
            if (squad.minionPrefab == null)
            {
                Plugin.Logger.LogWarning("[Regenerative] IsSpearInfantry: minionPrefab为null");
                return false;
            }
            bool flag = squad.minionPrefab.GetComponent<Spear>() != null;
            Plugin.Logger.LogInfo(string.Format("[Regenerative] IsSpearInfantry: minionPrefab={0}, hasSpear={1}", squad.minionPrefab.name, flag));
            return flag;
        }
    }
}
