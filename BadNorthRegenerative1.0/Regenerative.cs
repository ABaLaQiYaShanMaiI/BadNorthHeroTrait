// Author: ABaLaQiYaShanMaiI
using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using BadNorthAPI;
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
            this.upgradeType = TraitHelper.CreateTraitUpgradeType();
            TraitHelper.SetupBaseDefinition(this, REGENERATIVE_ID,
                "ABaLaQiYaShanMaiI/TRAIT/REGENERATIVE/NAME",
                "ABaLaQiYaShanMaiI/TRAIT/REGENERATIVE/DESCSHORT",
                CustomSprites.Sprites["trait_regenerative"],
                TraitHelper.CreateSingleLevel("ABaLaQiYaShanMaiI/TRAIT/REGENERATIVE/DESC"));
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);
            if (BadNorthAPI.Debugger.Enabled)
            {
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
            }
            if (!this.IsShieldInfantry(squad) && !this.IsSpearInfantry(squad))
            {
                Debugger.Log("[Regenerative] 非盾兵/非矛兵，跳过改造");
                return;
            }
            bool flag = this.IsShieldInfantry(squad);
            Debugger.Log(flag ? "[Regenerative] 盾兵小队，执行双刀改造" : "[Regenerative] 矛兵小队，执行不抬枪改造");
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
                Debugger.Log("[Regenerative] IsShieldInfantry: squad为null");
                return false;
            }
            if (squad.minionPrefab == null)
            {
                Debugger.Log("[Regenerative] IsShieldInfantry: minionPrefab为null");
                return false;
            }
            bool flag = squad.minionPrefab.GetComponent<Swordsman>() != null;
            Debugger.Log(string.Format("[Regenerative] IsShieldInfantry: minionPrefab={0}, hasSwordsman={1}", squad.minionPrefab.name, flag));
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

            var clipMap = new Dictionary<string, string>
            {
                { "Twohanded_Idle", "Swordsman_Idle" },
                { "Twohanded_Walk", "Swordsman_Walk" },
                { "Twohanded_Run", "Swordsman_Run" },
                { "Twohanded_Attack", "Swordsman_Attack" },
                { "Twohanded_Ragdoll", "Swordsman_Ragdoll" },
                { "Twohanded_Death", "Swordsman_Death" }
            };

            foreach (AnimationClip animationClip in Resources.FindObjectsOfTypeAll<AnimationClip>())
            {
                string clipKey;
                if (clipMap.TryGetValue(animationClip.name, out clipKey))
                {
                    animatorOverrideController[clipKey] = animationClip;
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

        private static readonly string SpearShieldTypeName = "Voxels.TowerDefense.SpearShield";

        private void RemoveSpearShieldIfPresent(Agent agent)
        {
            try
            {
                Component shield = agent.GetComponent(SpearShieldTypeName);
                if (shield != null)
                {
                    ((MonoBehaviour)shield).enabled = false;
                    shield.gameObject.SetActive(false);
                }
            }
            catch (System.Exception ex)
            {
                Debugger.Log(string.Format("[Regenerative] 移除 SpearShield 失败：{0}", ex.Message));
            }
        }

        private static bool _isGiantWarningLogged = false;

        private void ModifyAgentProperties(Agent agent, int squadLevel)
        {
            if (agent == null)
            {
                Debugger.Log("[Regenerative] ModifyAgentProperties: agent为null");
                return;
            }
            Swordsman component = agent.GetComponent<Swordsman>();
            if (component != null)
            {
                var isGiantField = typeof(Swordsman).GetField("isGiant", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!ReferenceEquals(isGiantField, null))
                {
                    isGiantField.SetValue(component, true);
                }
                else if (!_isGiantWarningLogged)
                {
                    _isGiantWarningLogged = true;
                    Debugger.Log("[Regenerative] Swordsman.isGiant field not found, skipping. (此警告仅显示一次)");
                }
                for (int i = 0; i < component.stunLevels.Length; i++)
                {
                    component.stunLevels[i] *= 0.1f;
                }
                ComponentHelper.GetOrAddComponent<RegenerativeBuff>(agent.gameObject);
                ComponentHelper.GetOrAddComponent<RegenerativeJumpResponder>(agent.gameObject);
                this.RemoveShield(component, agent);
                int num = Mathf.Clamp(squadLevel, 0, this.speedLevels.Length - 1);
                agent.maxSpeed = this.speedLevels[num];
                this.SetupAgentSounds(agent, component);
                return;
            }
            if (agent.GetComponent<Spear>() != null)
            {
                ComponentHelper.GetOrAddComponent<RegenerativeSpearBuff>(agent.gameObject);
                this.RemoveSpearShieldIfPresent(agent);
                return;
            }
            Debugger.Log("[Regenerative] " + agent.name + " 无Swordsman/无Spear组件，跳过改造");
        }

        private void RemoveShield(Swordsman swordsman, Agent agent)
        {
            Shield shield = swordsman.shield;
            if (shield)
            {
                if (shield.shield != null)
                {
                    shield.shield.gameObject.SetActive(false);
                }
                shield.enabled = false;
                swordsman.shield = null;
            }
        }

        private bool IsSpearInfantry(EnglishSquad squad)
        {
            if (squad == null)
            {
                Debugger.Log("[Regenerative] IsSpearInfantry: squad为null");
                return false;
            }
            if (squad.minionPrefab == null)
            {
                Debugger.Log("[Regenerative] IsSpearInfantry: minionPrefab为null");
                return false;
            }
            bool flag = squad.minionPrefab.GetComponent<Spear>() != null;
            Debugger.Log(string.Format("[Regenerative] IsSpearInfantry: minionPrefab={0}, hasSpear={1}", squad.minionPrefab.name, flag));
            return flag;
        }
    }
}