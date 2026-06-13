// Author: ABaLaQiYaShanMaiI
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorthRegenerative
{
    public class RegenerativeJumpResponder : MonoBehaviour, IAttackResponder
    {
        private Agent agent;
        private JumpAttack jumpAttack;
        private Agent pendingJumpTarget;
        private float cooldownTimer = 0f;
        private float cooldownDuration = 1.5f;

        private static bool _reflectionReady = false;
        private static bool _reflectionAttempted = false;
        private static bool _jumpAttackTypeExists = true;

        private static FieldInfo jumpAttack_attackSettings;
        private static FieldInfo jumpAttack_target;
        private static FieldInfo jumpAttack_jumpComponent;
        private static FieldInfo jumpAttack_plungeState;
        private static FieldInfo jumpAttack_plungePrepareState;
        private static FieldInfo jumpAttack_plungeAttackState;
        private static FieldInfo jumpAttack_landPos;
        private static FieldInfo jumpAttack_faceDirection;
        private static FieldInfo jumpAttack_plungeJumpId;
        private static FieldInfo jumpAttack_attackAnimId;

        private static MethodInfo jumpAttack_PlungeJump;

        private static FieldInfo jumpComponent_jumpingState;
        private static FieldInfo jumpComponent_targetPos;
        private static FieldInfo jumpComponent_agent;
        private static FieldInfo jumpComponent_navSpot;
        private static FieldInfo jumpComponent_jumpType;

        private static FieldInfo agent_groundedState;
        private static FieldInfo agent_deadState;
        private static FieldInfo agent_lifeState;

        private void Awake()
        {
            try
            {
                agent = GetComponent<Agent>();
                if (ReferenceEquals(agent, null))
                {
                    Plugin.Logger.LogError("[RegenerativeJumpResponder] Awake: 未找到Agent组件");
                    return;
                }
                Debugger.Log("[RegenerativeJumpResponder] Awake: agent=" + agent.name + " [" + GetInstanceID() + "]");
                CacheReflectionInfo();
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("[RegenerativeJumpResponder] Awake 异常: " + ex.Message);
                Plugin.Logger.LogError(ex.StackTrace);
            }
        }

        private static void CacheReflectionInfo()
        {
            if (_reflectionAttempted) return;
            _reflectionAttempted = true;

            try
            {
                Type jumpAttackType = null;
                try
                {
                    jumpAttackType = typeof(JumpAttack);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogError("[RegenerativeJumpResponder] JumpAttack 类型加载失败: " + ex.Message + " - 跳劈功能将完全禁用");
                    _jumpAttackTypeExists = false;
                }

                if (_jumpAttackTypeExists && !ReferenceEquals(jumpAttackType, null))
                {
                    jumpAttack_attackSettings = jumpAttackType.GetField("attackSettings", BindingFlags.Instance | BindingFlags.NonPublic);
                    jumpAttack_target = jumpAttackType.GetField("target", BindingFlags.Instance | BindingFlags.NonPublic);
                    jumpAttack_jumpComponent = jumpAttackType.GetField("jumpComponent", BindingFlags.Instance | BindingFlags.NonPublic);
                    jumpAttack_plungeState = jumpAttackType.GetField("plungeState", BindingFlags.Instance | BindingFlags.NonPublic);
                    jumpAttack_plungePrepareState = jumpAttackType.GetField("plungePrepareState", BindingFlags.Instance | BindingFlags.NonPublic);
                    jumpAttack_plungeAttackState = jumpAttackType.GetField("plungeAttackState", BindingFlags.Instance | BindingFlags.NonPublic);
                    jumpAttack_landPos = jumpAttackType.GetField("landPos", BindingFlags.Instance | BindingFlags.NonPublic);
                    jumpAttack_faceDirection = jumpAttackType.GetField("faceDirection", BindingFlags.Instance | BindingFlags.NonPublic);
                    jumpAttack_plungeJumpId = jumpAttackType.GetField("plungeJumpId", BindingFlags.Instance | BindingFlags.NonPublic);
                    jumpAttack_attackAnimId = jumpAttackType.GetField("attackAnimId", BindingFlags.Instance | BindingFlags.NonPublic);
                    jumpAttack_PlungeJump = jumpAttackType.GetMethod("PlungeJump", BindingFlags.Instance | BindingFlags.NonPublic);
                }

                Type jumpComponentType = typeof(JumpComponent);
                if (!ReferenceEquals(jumpComponentType, null))
                {
                    jumpComponent_jumpingState = jumpComponentType.GetField("jumpingState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    jumpComponent_targetPos = jumpComponentType.GetField("targetPos", BindingFlags.Instance | BindingFlags.NonPublic);
                    jumpComponent_agent = jumpComponentType.GetField("agent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    jumpComponent_navSpot = jumpComponentType.GetField("navSpot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    jumpComponent_jumpType = jumpComponentType.GetField("jumpType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                Type agentType = typeof(Agent);
                if (!ReferenceEquals(agentType, null))
                {
                    agent_groundedState = agentType.GetField("groundedState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    agent_deadState = agentType.GetField("deadState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    agent_lifeState = agentType.GetField("lifeState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                _reflectionReady = true;
                Debugger.Log("[RegenerativeJumpResponder] 反射初始化完成，JumpAttack存在=" + _jumpAttackTypeExists);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("[RegenerativeJumpResponder] CacheReflectionInfo 反射初始化异常: " + ex.Message);
                Plugin.Logger.LogError(ex.StackTrace);
                _reflectionReady = false;
            }
        }

        private static Dictionary<Type, FieldInfo[]> _attackerFieldCache = new Dictionary<Type, FieldInfo[]>();

        private static Agent GetAttackerAgent(MonoBehaviour monoAttacker, int depth = 0)
        {
            if (ReferenceEquals(monoAttacker, null))
                return null;

            if (depth > 3)
                return null;

            Agent agent = monoAttacker as Agent;
            if (!ReferenceEquals(agent, null))
                return agent;

            agent = monoAttacker.GetComponent<Agent>();
            if (!ReferenceEquals(agent, null))
                return agent;

            agent = monoAttacker.GetComponentInParent<Agent>();
            if (!ReferenceEquals(agent, null))
                return agent;

            agent = FindAgentInFields(monoAttacker, depth);
            return agent;
        }

        private static Agent FindAgentInFields(MonoBehaviour monoAttacker, int depth = 0)
        {
            Type type = monoAttacker.GetType();

            FieldInfo[] candidateFields;
            if (!_attackerFieldCache.TryGetValue(type, out candidateFields))
            {
                FieldInfo[] allFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var list = new List<FieldInfo>();
                foreach (FieldInfo fi in allFields)
                {
                    if (typeof(Agent).IsAssignableFrom(fi.FieldType) ||
                        typeof(Component).IsAssignableFrom(fi.FieldType) ||
                        typeof(WeakReference).IsAssignableFrom(fi.FieldType))
                    {
                        list.Add(fi);
                    }
                }
                candidateFields = list.ToArray();
                _attackerFieldCache[type] = candidateFields;

                if (candidateFields.Length > 0)
                {
                    Debugger.Log("[RegenerativeJumpResponder] FindAgentInFields: 类型 " + type.Name + " 发现 " + candidateFields.Length + " 个候选字段（含WeakReference）");
                }
            }

            foreach (FieldInfo fi in candidateFields)
            {
                try
                {
                    object val = fi.GetValue(monoAttacker);
                    if (ReferenceEquals(val, null))
                        continue;

                    WeakReference weakRef = val as WeakReference;
                    if (!ReferenceEquals(weakRef, null))
                    {
                        object target = weakRef.Target;
                        if (!ReferenceEquals(target, null))
                        {
                            Agent agentFromWeak = target as Agent;
                            if (!ReferenceEquals(agentFromWeak, null))
                            {
                                Debugger.Log("[RegenerativeJumpResponder] FindAgentInFields: 通过 " + type.Name + "." + fi.Name + " (WeakReference) 获取到 Agent=" + agentFromWeak.name);
                                return agentFromWeak;
                            }
                            Component compFromWeak = target as Component;
                            if (!ReferenceEquals(compFromWeak, null))
                            {
                                agentFromWeak = compFromWeak.GetComponent<Agent>();
                                if (!ReferenceEquals(agentFromWeak, null))
                                {
                                    Debugger.Log("[RegenerativeJumpResponder] FindAgentInFields: 通过 " + type.Name + "." + fi.Name + " (WeakReference->Component) 获取到 Agent=" + agentFromWeak.name);
                                    return agentFromWeak;
                                }
                                agentFromWeak = compFromWeak.GetComponentInParent<Agent>();
                                if (!ReferenceEquals(agentFromWeak, null))
                                {
                                    Debugger.Log("[RegenerativeJumpResponder] FindAgentInFields: 通过 " + type.Name + "." + fi.Name + " (WeakReference->ComponentInParent) 获取到 Agent=" + agentFromWeak.name);
                                    return agentFromWeak;
                                }
                            }
                            GameObject goFromWeak = target as GameObject;
                            if (!ReferenceEquals(goFromWeak, null))
                            {
                                agentFromWeak = goFromWeak.GetComponent<Agent>();
                                if (!ReferenceEquals(agentFromWeak, null))
                                {
                                    Debugger.Log("[RegenerativeJumpResponder] FindAgentInFields: 通过 " + type.Name + "." + fi.Name + " (WeakReference->GameObject) 获取到 Agent=" + agentFromWeak.name);
                                    return agentFromWeak;
                                }
                            }
                        }
                        continue;
                    }

                    Agent agentVal = val as Agent;
                    if (!ReferenceEquals(agentVal, null))
                    {
                        Debugger.Log("[RegenerativeJumpResponder] FindAgentInFields: 通过 " + type.Name + "." + fi.Name + " 获取到 Agent=" + agentVal.name);
                        return agentVal;
                    }

                    GameObject goVal = val as GameObject;
                    if (!ReferenceEquals(goVal, null))
                    {
                        agentVal = goVal.GetComponent<Agent>();
                        if (!ReferenceEquals(agentVal, null))
                        {
                            Debugger.Log("[RegenerativeJumpResponder] FindAgentInFields: 通过 " + type.Name + "." + fi.Name + " (GameObject) 获取到 Agent=" + agentVal.name);
                            return agentVal;
                        }
                        agentVal = goVal.GetComponentInParent<Agent>();
                        if (!ReferenceEquals(agentVal, null))
                        {
                            Debugger.Log("[RegenerativeJumpResponder] FindAgentInFields: 通过 " + type.Name + "." + fi.Name + " (GameObject->ComponentInParent) 获取到 Agent=" + agentVal.name);
                            return agentVal;
                        }
                    }

                    Component compVal = val as Component;
                    if (!ReferenceEquals(compVal, null))
                    {
                        agentVal = compVal.GetComponent<Agent>();
                        if (!ReferenceEquals(agentVal, null))
                        {
                            Debugger.Log("[RegenerativeJumpResponder] FindAgentInFields: 通过 " + type.Name + "." + fi.Name + "->GetComponent 获取到 Agent=" + agentVal.name);
                            return agentVal;
                        }

                        agentVal = compVal.GetComponentInParent<Agent>();
                        if (!ReferenceEquals(agentVal, null))
                        {
                            Debugger.Log("[RegenerativeJumpResponder] FindAgentInFields: 通过 " + type.Name + "." + fi.Name + "->GetComponentInParent 获取到 Agent=" + agentVal.name);
                            return agentVal;
                        }

                        MonoBehaviour nestedMono = compVal as MonoBehaviour;
                        if (!ReferenceEquals(nestedMono, null))
                        {
                            Agent nestedAgent = GetAttackerAgent(nestedMono, depth + 1);
                            if (!ReferenceEquals(nestedAgent, null))
                            {
                                Debugger.Log("[RegenerativeJumpResponder] FindAgentInFields: 通过递归 GetAttackerAgent 从 " + compVal.GetType().Name + " 获取到 Agent=" + nestedAgent.name);
                                return nestedAgent;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] FindAgentInFields 字段 " + fi.Name + " 访问异常: " + ex.Message);
                }
            }

            return null;
        }

        private void Start()
        {
            try
            {
                Debugger.Log("[RegenerativeJumpResponder] Start 被调用，agent=" + (agent?.name ?? "null") + ", reflectionReady=" + _reflectionReady);

                if (ReferenceEquals(agent, null))
                {
                    Plugin.Logger.LogError("[RegenerativeJumpResponder] Start: agent 为 null，无法初始化");
                    return;
                }

                if (!_reflectionReady && _reflectionAttempted)
                {
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] Start: 反射初始化失败，跳劈功能不可用");
                }

                if (!agent.attackResponders.Contains(this))
                {
                    agent.attackResponders.Add(this);
                    Debugger.Log("[JumpResponder] 已加入 attackResponders，当前数量=" + agent.attackResponders.Count);

                    StringBuilder sb = new StringBuilder("[JumpResponder] Responders: ");
                    for (int i = 0; i < agent.attackResponders.Count; i++)
                    {
                        var r = agent.attackResponders[i];
                        if (!ReferenceEquals(r, null))
                            sb.Append(r.GetType().Name);
                        else
                            sb.Append("null");
                        if (i < agent.attackResponders.Count - 1)
                            sb.Append(", ");
                    }
                    Debugger.Log(sb.ToString());
                }
                else
                {
                    Plugin.Logger.LogWarning("[JumpResponder] 已在 attackResponders 中，跳过重复添加");
                }

                if (!_jumpAttackTypeExists)
                {
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] JumpAttack 类型不存在，跳过 JumpAttack 初始化");
                }
                else
                {
                    jumpAttack = agent.GetComponent<JumpAttack>();
                    if (ReferenceEquals(jumpAttack, null))
                    {
                        try
                        {
                            GameObject template = null;
                            if (!ReferenceEquals(LevelStateObjectReferences.dict, null) && LevelStateObjectReferences.dict.TryGetValue("Viking_Twohanded", out UnityEngine.Object refObj))
                            {
                                template = refObj as GameObject;
                                if (ReferenceEquals(template, null))
                                {
                                    Component comp = refObj as Component;
                                    if (!ReferenceEquals(comp, null))
                                        template = comp.gameObject;
                                }
                                if (ReferenceEquals(template, null) && refObj is VikingReference vRef)
                                {
                                    try
                                    {
                                        FieldInfo vikingCloneField = typeof(VikingReference).GetField("vikingClone", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                                        FieldInfo vikingField = typeof(VikingReference).GetField("viking", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                                        Component vikingClone = null;
                                        Component viking = null;
                                        if (!ReferenceEquals(vikingCloneField, null)) vikingClone = vikingCloneField.GetValue(vRef) as Component;
                                        if (!ReferenceEquals(vikingField, null)) viking = vikingField.GetValue(vRef) as Component;
                                        if (!ReferenceEquals(vikingClone, null)) template = vikingClone.gameObject;
                                        else if (!ReferenceEquals(viking, null)) template = viking.gameObject;
                                    }
                                    catch (Exception ex)
                                    {
                                        Plugin.Logger.LogWarning("[RegenerativeJumpResponder] VikingReference 反射失败: " + ex.Message);
                                    }
                                }
                            }

                            if (!ReferenceEquals(template, null))
                            {
                                JumpAttack templateJump = template.GetComponent<JumpAttack>();
                                if (!ReferenceEquals(templateJump, null))
                                {
                                    jumpAttack = agent.gameObject.AddComponent<JumpAttack>();
                                    CopyJumpAttackFields(templateJump, jumpAttack);
                                    Debugger.Log("[RegenerativeJumpResponder] 主模板成功：从 LevelStateObjectReferences 获取 JumpAttack");
                                }
                            }
                            else
                            {
                                Debugger.Log("[RegenerativeJumpResponder] 主模板获取失败：LevelStateObjectReferences 中无 Viking_Twohanded");
                            }
                        }
                        catch (Exception ex)
                        {
                            Debugger.Log("[RegenerativeJumpResponder] 主模板获取异常: " + ex.Message);
                        }

                        if (ReferenceEquals(jumpAttack, null))
                        {
                            Debugger.Log("[RegenerativeJumpResponder] 启动备用方案：Resources.FindObjectsOfTypeAll<JumpAttack>()...");
                            try
                            {
                                JumpAttack[] allJumpAttacks = Resources.FindObjectsOfTypeAll<JumpAttack>();
                                if (!ReferenceEquals(allJumpAttacks, null) && allJumpAttacks.Length > 0)
                                {
                                    Debugger.Log("[RegenerativeJumpResponder] FindObjectsOfTypeAll 找到 " + allJumpAttacks.Length + " 个 JumpAttack 实例");
                                    
                                    JumpAttack sourceTemplate = null;
                                    foreach (var ja in allJumpAttacks)
                                    {
                                        if (ReferenceEquals(ja.gameObject, agent.gameObject))
                                            continue;
                                        sourceTemplate = ja;
                                        break;
                                    }

                                    if (!ReferenceEquals(sourceTemplate, null))
                                    {
                                        jumpAttack = agent.gameObject.AddComponent<JumpAttack>();
                                        CopyJumpAttackFields(sourceTemplate, jumpAttack);
                                        Debugger.Log("[RegenerativeJumpResponder] 备用方案成功：模板源=" + sourceTemplate.name);
                                    }
                                    else
                                    {
                                        Debugger.Log("[RegenerativeJumpResponder] FindObjectsOfTypeAll 未找到非自身的 JumpAttack 实例");
                                    }
                                }
                                else
                                {
                                    Debugger.Log("[RegenerativeJumpResponder] FindObjectsOfTypeAll 返回空，无法获取 JumpAttack 模板");
                                }
                            }
                            catch (Exception ex)
                            {
                                Debugger.Log("[RegenerativeJumpResponder] FindObjectsOfTypeAll 备用方案异常: " + ex.Message);
                            }
                        }

                        if (ReferenceEquals(jumpAttack, null))
                        {
                            Debugger.Log("[RegenerativeJumpResponder] 所有模板获取均失败，执行最终兜底：手动创建...");
                            try
                            {
                                jumpAttack = agent.gameObject.AddComponent<JumpAttack>();
                                Debugger.Log("[RegenerativeJumpResponder] 最终兜底：JumpAttack 已手动创建");
                            }
                            catch (Exception ex)
                            {
                                Plugin.Logger.LogError("[RegenerativeJumpResponder] 最终兜底创建 JumpAttack 失败: " + ex.Message);
                                jumpAttack = null;
                            }
                        }
                    }

                    if (!ReferenceEquals(jumpAttack, null))
                    {
                        try
                        {
                            jumpAttack.Setup(agent);
                            jumpAttack.enabled = false;
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger.LogWarning("[RegenerativeJumpResponder] jumpAttack.Setup 失败: " + ex.Message);
                        }
                    }
                }

                if (ReferenceEquals(jumpAttack, null))
                    Debugger.Log("[RegenerativeJumpResponder] jumpAttack 未初始化，跳劈将不可用");
                else
                    Debugger.Log("[RegenerativeJumpResponder] jumpAttack 已就绪，enabled=" + jumpAttack.enabled);

                Debugger.Log("[RegenerativeJumpResponder] 初始化完成");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("[RegenerativeJumpResponder] Start 初始化异常: " + ex.Message);
                Plugin.Logger.LogError(ex.StackTrace);
            }
        }

        private static void CopyJumpAttackFields(JumpAttack source, JumpAttack destination)
        {
            if (!ReferenceEquals(jumpAttack_attackSettings, null))
                jumpAttack_attackSettings.SetValue(destination, jumpAttack_attackSettings.GetValue(source));
            if (!ReferenceEquals(jumpAttack_plungeState, null))
                jumpAttack_plungeState.SetValue(destination, jumpAttack_plungeState.GetValue(source));
            if (!ReferenceEquals(jumpAttack_plungePrepareState, null))
                jumpAttack_plungePrepareState.SetValue(destination, jumpAttack_plungePrepareState.GetValue(source));
            if (!ReferenceEquals(jumpAttack_plungeAttackState, null))
                jumpAttack_plungeAttackState.SetValue(destination, jumpAttack_plungeAttackState.GetValue(source));
            if (!ReferenceEquals(jumpAttack_landPos, null))
                jumpAttack_landPos.SetValue(destination, jumpAttack_landPos.GetValue(source));
            if (!ReferenceEquals(jumpAttack_faceDirection, null))
                jumpAttack_faceDirection.SetValue(destination, jumpAttack_faceDirection.GetValue(source));
            if (!ReferenceEquals(jumpAttack_plungeJumpId, null))
                jumpAttack_plungeJumpId.SetValue(destination, jumpAttack_plungeJumpId.GetValue(source));
            if (!ReferenceEquals(jumpAttack_attackAnimId, null))
                jumpAttack_attackAnimId.SetValue(destination, jumpAttack_attackAnimId.GetValue(source));

            if (!ReferenceEquals(jumpAttack_jumpComponent, null))
            {
                JumpComponent srcJC = jumpAttack_jumpComponent.GetValue(source) as JumpComponent;
                JumpComponent dstJC = jumpAttack_jumpComponent.GetValue(destination) as JumpComponent;
                if (!ReferenceEquals(srcJC, null) && !ReferenceEquals(dstJC, null))
                {
                    if (!ReferenceEquals(jumpComponent_navSpot, null))
                        jumpComponent_navSpot.SetValue(dstJC, jumpComponent_navSpot.GetValue(srcJC));
                    if (!ReferenceEquals(jumpComponent_jumpType, null))
                        jumpComponent_jumpType.SetValue(dstJC, jumpComponent_jumpType.GetValue(srcJC));
                    if (!ReferenceEquals(jumpComponent_targetPos, null))
                        jumpComponent_targetPos.SetValue(dstJC, jumpComponent_targetPos.GetValue(srcJC));
                }
            }
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            if (!ReferenceEquals(pendingJumpTarget, null) && !ReferenceEquals(jumpAttack, null))
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[RegenerativeJumpResponder] Update 触发跳劈，target=" + pendingJumpTarget.name);
                ExecuteJump(pendingJumpTarget);
                pendingJumpTarget = null;
            }
            else if (!ReferenceEquals(pendingJumpTarget, null))
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] Update 跳过跳劈，jumpAttack=" + (jumpAttack == null ? "null" : "ok") + ", target=" + (pendingJumpTarget == null ? "null" : pendingJumpTarget.name));
            }
        }

        public void ModifyAttack(ref Attack attack)
        {
            if (!this.enabled || !gameObject.activeInHierarchy)
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogWarning("[JumpResponder] ModifyAttack 被调用但组件被禁用或GameObject非活跃");
                return;
            }

            if (BadNorthAPI.Debugger.Enabled)
            {
                Plugin.Logger.LogInfo("[JumpResponder] CRITICAL - ModifyAttack ENTERED");
                Plugin.Logger.LogInfo("[JumpResponder] ModifyAttack called, agent=" + (agent?.name ?? "null") + ", monoAttacker=" + (attack.monoAttacker?.GetType().Name ?? "null") + ", monoAttacker.name=" + (attack.monoAttacker?.name ?? "null"));
            }

            if (ReferenceEquals(agent, null) || !agent.isActiveAndEnabled)
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[JumpResponder] agent无效，退出");
                return;
            }

            if (!_jumpAttackTypeExists)
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[JumpResponder] JumpAttack 类型不存在，跳过跳劈");
                return;
            }

            if (cooldownTimer > 0f)
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[JumpResponder] 冷却中");
                return;
            }

            if (IsAgentDead(agent))
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[JumpResponder] 防御者已死亡，退出");
                return;
            }

            Agent attackerAgent = GetAttackerAgent(attack.monoAttacker);
            if (ReferenceEquals(attackerAgent, null))
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[JumpResponder] 无法从攻击者获取 Agent，退出");
                return;
            }

            if (ReferenceEquals(attackerAgent, agent))
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[JumpResponder] 攻击者是自身，退出");
                return;
            }

            if (IsAgentDead(attackerAgent))
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[JumpResponder] 攻击者已死亡，退出");
                return;
            }

            float distance = Vector3.Distance(agent.transform.position, attackerAgent.transform.position);
            if (distance > 5f)
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[JumpResponder] 距离过远 (" + distance + ")，退出");
                return;
            }

            float heightDiff = Mathf.Abs(agent.transform.position.y - attackerAgent.transform.position.y);
            if (heightDiff > 0.5f)
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[JumpResponder] 高度差过大 (" + heightDiff + ")，退出");
                return;
            }

            if (!IsAgentGrounded(attackerAgent))
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[JumpResponder] 攻击者不在地面上，退出");
                return;
            }

            if (IsSquadMemberJumping())
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[JumpResponder] 同小队有其他单位正在跳跃，退出");
                return;
            }

            // 验证目标的 NavPos 是否有效（飞行单位或特殊状态的目标可能没有有效的 NavPos）
            if (ReferenceEquals(attackerAgent.navPos, null) || ReferenceEquals(attackerAgent.navPos.tri, null))
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[JumpResponder] 目标 " + attackerAgent.name + " NavPos 无效，跳过跳劈反击");
                return;
            }

            pendingJumpTarget = attackerAgent;
            cooldownTimer = cooldownDuration;
            if (BadNorthAPI.Debugger.Enabled)
                Plugin.Logger.LogInfo("[RegenerativeJumpResponder] " + agent.name + " 即将跳劈反击 " + attackerAgent.name);
        }

        private void ExecuteJump(Agent target)
        {
            if (ReferenceEquals(jumpAttack, null) || ReferenceEquals(target, null))
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] ExecuteJump 失败：jumpAttack=" + (jumpAttack == null ? "null" : "ok") + ", target=" + (target == null ? "null" : target.name));
                return;
            }

            // 验证目标的 NavPos 是否有效（飞行单位或特殊状态的目标可能没有有效的 NavPos）
            if (ReferenceEquals(target.navPos, null) || ReferenceEquals(target.navPos.tri, null))
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] 跳过跳劈：目标 " + target.name + " 的 NavPos 无效（可能为飞行单位或处于特殊状态），无法执行跳劈");
                return;
            }

            try
            {
                jumpAttack.enabled = true;
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[RegenerativeJumpResponder] 已启用 jumpAttack");

                if (!ReferenceEquals(jumpAttack_target, null))
                    jumpAttack_target.SetValue(jumpAttack, target);
                else if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] jumpAttack_target 反射字段为 null，将无法设置目标");

                if (!ReferenceEquals(jumpAttack_landPos, null))
                {
                    NavPos landNavPos = new NavPos(target.navPos.tri, target.navPos.pos);
                    jumpAttack_landPos.SetValue(jumpAttack, landNavPos);
                }

                if (!ReferenceEquals(jumpAttack_faceDirection, null))
                {
                    Vector3 direction = (target.transform.position - agent.transform.position).normalized;
                    jumpAttack_faceDirection.SetValue(jumpAttack, direction);
                }

                if (!ReferenceEquals(jumpAttack_jumpComponent, null))
                {
                    JumpComponent jc = jumpAttack_jumpComponent.GetValue(jumpAttack) as JumpComponent;
                    if (!ReferenceEquals(jc, null) && !ReferenceEquals(jumpComponent_agent, null))
                    {
                        Agent currentAgent = jumpComponent_agent.GetValue(jc) as Agent;
                        if (ReferenceEquals(currentAgent, null) || !ReferenceEquals(currentAgent, agent))
                        {
                            jumpComponent_agent.SetValue(jc, agent);
                            if (BadNorthAPI.Debugger.Enabled)
                                Plugin.Logger.LogInfo("[RegenerativeJumpResponder] 已设置 JumpComponent.agent = " + agent.name);
                        }
                    }
                }

                if (!ReferenceEquals(target.brain, null) && !ReferenceEquals(agent.rangeWorry, null))
                {
                    agent.rangeWorry.threat = jumpAttack;
                    agent.rangeWorry.threatComponent = jumpAttack;
                    agent.rangeWorry.distance = Vector3.Distance(agent.transform.position, target.transform.position);
                    agent.rangeWorry.dir = (target.transform.position - agent.transform.position).normalized;
                }

                if (!ReferenceEquals(jumpAttack_PlungeJump, null))
                    jumpAttack_PlungeJump.Invoke(jumpAttack, null);
                else if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] jumpAttack_PlungeJump 方法为 null，跳跃无法执行");

                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogInfo("[RegenerativeJumpResponder] " + agent.name + " 执行跳劈反击，目标 " + target.name);
            }
            catch (Exception ex)
            {
                if (BadNorthAPI.Debugger.Enabled)
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] 跳劈执行失败: " + ex.Message);
                if (!ReferenceEquals(jumpAttack, null))
                    jumpAttack.enabled = false;
            }
        }

        private bool IsAgentDead(Agent targetAgent)
        {
            if (ReferenceEquals(targetAgent, null)) return true;
            if (targetAgent.health <= 0f) return true;
            if (!ReferenceEquals(agent_deadState, null))
            {
                try
                {
                    AgentState deadState = agent_deadState.GetValue(targetAgent) as AgentState;
                    if (!ReferenceEquals(deadState, null) && deadState.active) return true;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] IsAgentDead 反射异常: " + ex.Message);
                }
            }
            return false;
        }

        private bool IsAgentGrounded(Agent targetAgent)
        {
            if (ReferenceEquals(targetAgent, null)) return false;
            if (!ReferenceEquals(agent_groundedState, null))
            {
                try
                {
                    AgentState groundedState = agent_groundedState.GetValue(targetAgent) as AgentState;
                    if (!ReferenceEquals(groundedState, null)) return groundedState.active;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] IsAgentGrounded 反射异常: " + ex.Message);
                }
            }
            return true;
        }

        private bool IsJumpAttackJumping(JumpAttack ja)
        {
            if (ReferenceEquals(ja, null)) return false;
            if (!ReferenceEquals(jumpAttack_jumpComponent, null))
            {
                try
                {
                    JumpComponent jc = jumpAttack_jumpComponent.GetValue(ja) as JumpComponent;
                    if (!ReferenceEquals(jc, null) && !ReferenceEquals(jumpComponent_jumpingState, null))
                    {
                        AgentState jumpingState = jumpComponent_jumpingState.GetValue(jc) as AgentState;
                        if (!ReferenceEquals(jumpingState, null)) return jumpingState.active;
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] IsJumpAttackJumping 反射异常: " + ex.Message);
                }
            }
            if (!ReferenceEquals(jumpAttack_plungeState, null))
            {
                try
                {
                    AgentState plungeState = jumpAttack_plungeState.GetValue(ja) as AgentState;
                    if (!ReferenceEquals(plungeState, null) && plungeState.active) return true;
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] IsJumpAttackJumping plungeState 反射异常: " + ex.Message);
                }
            }
            return false;
        }

        private bool IsSquadMemberJumping()
        {
            if (ReferenceEquals(agent.squad, null)) return false;
            try
            {
                foreach (Agent squadAgent in agent.squad.agents)
                {
                    if (!ReferenceEquals(squadAgent, agent))
                    {
                        JumpAttack otherJump = squadAgent.GetComponent<JumpAttack>();
                        if (!ReferenceEquals(otherJump, null) && otherJump.enabled && IsJumpAttackJumping(otherJump))
                            return true;
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[RegenerativeJumpResponder] IsSquadMemberJumping 异常: " + ex.Message);
            }
            return false;
        }

        private void OnDestroy()
        {
            try
            {
                if (!ReferenceEquals(agent, null) && !ReferenceEquals(agent.attackResponders, null))
                    agent.attackResponders.Remove(this);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[RegenerativeJumpResponder] OnDestroy 移除失败: " + ex.Message);
            }
        }
    }
}
