using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorthRegenerative
{
    /// <summary>
    /// 跳劈反击组件：当受 Regenerative 特质影响的盾兵单位受到攻击时，
    /// 该组件会触发跳劈反击，跃向攻击者进行攻击。
    /// 不依赖任何兵种类型判断，纯通用 Agent 提取。
    /// 对应原 PlentyTraits 中的 RegenerativeJumpResponder。
    /// </summary>
    public class RegenerativeJumpResponder : MonoBehaviour, IAttackResponder
    {
        private Agent agent;
        private JumpAttack jumpAttack;
        private Agent pendingJumpTarget;
        private float cooldownTimer = 0f;
        private float cooldownDuration = 1.5f;

        // 标志：反射是否就绪，若关键类型不存在则跳过所有逻辑
        private static bool _reflectionReady = false;
        private static bool _reflectionAttempted = false;
        private static bool _jumpAttackTypeExists = true;

        // JumpAttack 私有字段反射
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

        // JumpAttack 私有方法反射
        private static MethodInfo jumpAttack_PlungeJump;

        // JumpComponent 字段反射
        private static FieldInfo jumpComponent_jumpingState;
        private static FieldInfo jumpComponent_targetPos;

        // Agent 状态字段反射
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
                Plugin.Logger.LogInfo("[RegenerativeJumpResponder] Awake: agent=" + agent.name + " [" + GetInstanceID() + "]");
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
                // 检查 JumpAttack 类型是否存在（避免 TypeLoadException）
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
                }

                Type agentType = typeof(Agent);
                if (!ReferenceEquals(agentType, null))
                {
                    agent_groundedState = agentType.GetField("groundedState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    agent_deadState = agentType.GetField("deadState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    agent_lifeState = agentType.GetField("lifeState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                _reflectionReady = true;
                Plugin.Logger.LogInfo("[RegenerativeJumpResponder] 反射初始化完成，JumpAttack存在=" + _jumpAttackTypeExists);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogError("[RegenerativeJumpResponder] CacheReflectionInfo 反射初始化异常: " + ex.Message);
                Plugin.Logger.LogError(ex.StackTrace);
                _reflectionReady = false;
            }
        }

        // 反射缓存：按攻击者类型缓存对应的反射字段
        // key: Type, value: FieldInfo 数组（Agent 类型字段 + Component 类型字段）
        private static Dictionary<Type, FieldInfo[]> _attackerFieldCache = new Dictionary<Type, FieldInfo[]>();

        /// <summary>
        /// 纯通用的攻击者 Agent 提取方法，不依赖任何兵种类型判断。
        /// 查找顺序：直接转换 → GetComponent → GetComponentInParent → 全字段反射扫描
        /// </summary>
        private static Agent GetAttackerAgent(MonoBehaviour monoAttacker)
        {
            if (ReferenceEquals(monoAttacker, null))
                return null;

            // 1. 直接是 Agent
            Agent agent = monoAttacker as Agent;
            if (!ReferenceEquals(agent, null))
                return agent;

            // 2. GetComponent 查找
            agent = monoAttacker.GetComponent<Agent>();
            if (!ReferenceEquals(agent, null))
                return agent;

            // 3. GetComponentInParent 查找（适用于子物体上的攻击脚本）
            agent = monoAttacker.GetComponentInParent<Agent>();
            if (!ReferenceEquals(agent, null))
                return agent;

            // 4. 通用反射扫描：查找攻击者对象上所有可能引用 Agent 的字段
            //    这能处理 Arrow.shooter、Shootable 内部引用等间接情况
            agent = FindAgentInFields(monoAttacker);
            return agent;
        }

        /// <summary>
        /// 通过反射扫描攻击者对象的所有字段，寻找 Agent 引用。
        /// 使用按类型缓存避免重复扫描。
        /// </summary>
        private static Agent FindAgentInFields(MonoBehaviour monoAttacker)
        {
            Type type = monoAttacker.GetType();

            // 尝试从缓存获取字段列表
            FieldInfo[] candidateFields;
            if (!_attackerFieldCache.TryGetValue(type, out candidateFields))
            {
                // 扫描该类型的所有字段，找出 Agent 和 Component 类型的字段
                FieldInfo[] allFields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var list = new System.Collections.Generic.List<FieldInfo>();
                foreach (FieldInfo fi in allFields)
                {
                    if (typeof(Agent).IsAssignableFrom(fi.FieldType) ||
                        typeof(Component).IsAssignableFrom(fi.FieldType))
                    {
                        list.Add(fi);
                    }
                }
                candidateFields = list.ToArray();
                _attackerFieldCache[type] = candidateFields;

                if (candidateFields.Length > 0)
                {
                    Plugin.Logger.LogInfo("[RegenerativeJumpResponder] FindAgentInFields: 类型 " + type.Name + " 发现 " + candidateFields.Length + " 个候选字段");
                }
            }

            // 遍历候选字段，尝试提取 Agent
            foreach (FieldInfo fi in candidateFields)
            {
                try
                {
                    object val = fi.GetValue(monoAttacker);
                    if (ReferenceEquals(val, null))
                        continue;

                    // 字段值直接就是 Agent
                    Agent agentVal = val as Agent;
                    if (!ReferenceEquals(agentVal, null))
                    {
                        Plugin.Logger.LogInfo("[RegenerativeJumpResponder] FindAgentInFields: 通过 " + type.Name + "." + fi.Name + " 获取到 Agent=" + agentVal.name);
                        return agentVal;
                    }

                    // 字段值是 Component，从中获取 Agent
                    Component compVal = val as Component;
                    if (!ReferenceEquals(compVal, null))
                    {
                        agentVal = compVal.GetComponent<Agent>();
                        if (!ReferenceEquals(agentVal, null))
                        {
                            Plugin.Logger.LogInfo("[RegenerativeJumpResponder] FindAgentInFields: 通过 " + type.Name + "." + fi.Name + "->GetComponent 获取到 Agent=" + agentVal.name);
                            return agentVal;
                        }

                        agentVal = compVal.GetComponentInParent<Agent>();
                        if (!ReferenceEquals(agentVal, null))
                        {
                            Plugin.Logger.LogInfo("[RegenerativeJumpResponder] FindAgentInFields: 通过 " + type.Name + "." + fi.Name + "->GetComponentInParent 获取到 Agent=" + agentVal.name);
                            return agentVal;
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
                // 即使在早期 return 也要留下日志
                Plugin.Logger.LogInfo("[RegenerativeJumpResponder] Start 被调用，agent=" + (agent?.name ?? "null") + ", reflectionReady=" + _reflectionReady);

                if (ReferenceEquals(agent, null))
                {
                    Plugin.Logger.LogError("[RegenerativeJumpResponder] Start: agent 为 null，无法初始化");
                    return;
                }

                if (!_reflectionReady && _reflectionAttempted)
                {
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] Start: 反射初始化失败，跳劈功能不可用");
                    // 仍然注册自己以响应 ModifyAttack，但不会执行跳劈
                }

                if (!agent.attackResponders.Contains(this))
                {
                    agent.attackResponders.Add(this);
                    Plugin.Logger.LogInfo("[JumpResponder] 已加入 attackResponders，当前数量=" + agent.attackResponders.Count);

                    // 安全输出列表内容（不使用 String.Join）
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
                    Plugin.Logger.LogInfo(sb.ToString());
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
                                    Plugin.Logger.LogInfo("[RegenerativeJumpResponder] 成功从模板复制 JumpAttack");
                                }
                            }
                            else
                            {
                                Plugin.Logger.LogWarning("[RegenerativeJumpResponder] 无法获取 Viking_Twohanded 模板对象，跳劈功能可能不可用");
                            }
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger.LogWarning("[RegenerativeJumpResponder] 无法从模板复制JumpAttack: " + ex.Message);
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
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] jumpAttack 未初始化，跳劈将不可用");
                else
                    Plugin.Logger.LogInfo("[RegenerativeJumpResponder] jumpAttack 已就绪，enabled=" + jumpAttack.enabled);

                Plugin.Logger.LogInfo("[RegenerativeJumpResponder] 初始化完成");
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
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            if (!ReferenceEquals(pendingJumpTarget, null) && !ReferenceEquals(jumpAttack, null))
            {
                Plugin.Logger.LogInfo("[RegenerativeJumpResponder] Update 触发跳劈，target=" + pendingJumpTarget.name);
                ExecuteJump(pendingJumpTarget);
                pendingJumpTarget = null;
            }
            else if (!ReferenceEquals(pendingJumpTarget, null))
            {
                Plugin.Logger.LogWarning("[RegenerativeJumpResponder] Update 跳过跳劈，jumpAttack=" + (jumpAttack == null ? "null" : "ok") + ", target=" + (pendingJumpTarget == null ? "null" : pendingJumpTarget.name));
            }
        }

        public void ModifyAttack(ref Attack attack)
        {
            if (!this.enabled || !gameObject.activeInHierarchy)
            {
                Plugin.Logger.LogWarning("[JumpResponder] ModifyAttack 被调用但组件被禁用或GameObject非活跃");
                return;
            }

            Plugin.Logger.LogInfo("[JumpResponder] CRITICAL - ModifyAttack ENTERED");
            Plugin.Logger.LogInfo("[JumpResponder] ModifyAttack called, agent=" + (agent?.name ?? "null") + ", monoAttacker=" + (attack.monoAttacker?.GetType().Name ?? "null") + ", monoAttacker.name=" + (attack.monoAttacker?.name ?? "null"));

            if (ReferenceEquals(agent, null) || !agent.isActiveAndEnabled)
            {
                Plugin.Logger.LogInfo("[JumpResponder] agent无效，退出");
                return;
            }

            if (!_jumpAttackTypeExists)
            {
                Plugin.Logger.LogInfo("[JumpResponder] JumpAttack 类型不存在，跳过跳劈");
                return;
            }

            if (cooldownTimer > 0f)
            {
                Plugin.Logger.LogInfo("[JumpResponder] 冷却中");
                return;
            }

            if (IsAgentDead(agent))
            {
                Plugin.Logger.LogInfo("[JumpResponder] 防御者已死亡，退出");
                return;
            }

            // --- 通用攻击者提取（无需区分 Arrow / Shootable / 直接 Agent） ---
            Agent attackerAgent = GetAttackerAgent(attack.monoAttacker);
            if (ReferenceEquals(attackerAgent, null))
            {
                Plugin.Logger.LogInfo("[JumpResponder] 无法从攻击者获取 Agent，退出");
                return;
            }

            // 避免攻击自己
            if (ReferenceEquals(attackerAgent, agent))
            {
                Plugin.Logger.LogInfo("[JumpResponder] 攻击者是自身，退出");
                return;
            }

            if (IsAgentDead(attackerAgent))
            {
                Plugin.Logger.LogInfo("[JumpResponder] 攻击者已死亡，退出");
                return;
            }

            float distance = Vector3.Distance(agent.transform.position, attackerAgent.transform.position);
            if (distance > 5f)
            {
                Plugin.Logger.LogInfo("[JumpResponder] 距离过远 (" + distance + ")，退出");
                return;
            }

            float heightDiff = Mathf.Abs(agent.transform.position.y - attackerAgent.transform.position.y);
            if (heightDiff > 0.5f)
            {
                Plugin.Logger.LogInfo("[JumpResponder] 高度差过大 (" + heightDiff + ")，退出");
                return;
            }

            if (!IsAgentGrounded(attackerAgent))
            {
                Plugin.Logger.LogInfo("[JumpResponder] 攻击者不在地面上，退出");
                return;
            }

            if (IsSquadMemberJumping())
            {
                Plugin.Logger.LogInfo("[JumpResponder] 同小队有其他单位正在跳跃，退出");
                return;
            }

            pendingJumpTarget = attackerAgent;
            cooldownTimer = cooldownDuration;
            Plugin.Logger.LogInfo("[RegenerativeJumpResponder] " + agent.name + " 即将跳劈反击 " + attackerAgent.name);
        }

        private void ExecuteJump(Agent target)
        {
            if (ReferenceEquals(jumpAttack, null) || ReferenceEquals(target, null))
            {
                Plugin.Logger.LogWarning("[RegenerativeJumpResponder] ExecuteJump 失败：jumpAttack=" + (jumpAttack == null ? "null" : "ok") + ", target=" + (target == null ? "null" : target.name));
                return;
            }
            try
            {
                // 1. 启用 JumpAttack 组件，使其 Update 循环开始工作
                jumpAttack.enabled = true;
                Plugin.Logger.LogInfo("[RegenerativeJumpResponder] 已启用 jumpAttack");

                // 2. 设置目标
                if (!ReferenceEquals(jumpAttack_target, null))
                    jumpAttack_target.SetValue(jumpAttack, target);
                else
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] jumpAttack_target 反射字段为 null，将无法设置目标");

                // 3. 设置着陆点（目标位置）
                if (!ReferenceEquals(jumpAttack_landPos, null))
                {
                    NavPos landNavPos = new NavPos(target.navPos.tri, target.navPos.pos);
                    jumpAttack_landPos.SetValue(jumpAttack, landNavPos);
                }

                // 4. 设置面朝方向
                if (!ReferenceEquals(jumpAttack_faceDirection, null))
                {
                    Vector3 direction = (target.transform.position - agent.transform.position).normalized;
                    jumpAttack_faceDirection.SetValue(jumpAttack, direction);
                }

                // 5. 设置威胁对象（用于 AI 等，非必须）
                if (!ReferenceEquals(target.brain, null) && !ReferenceEquals(agent.rangeWorry, null))
                {
                    agent.rangeWorry.threat = jumpAttack;
                    agent.rangeWorry.threatComponent = jumpAttack;
                    agent.rangeWorry.distance = Vector3.Distance(agent.transform.position, target.transform.position);
                    agent.rangeWorry.dir = (target.transform.position - agent.transform.position).normalized;
                }

                // 6. 调用 PlungeJump 启动跳跃状态机
                if (!ReferenceEquals(jumpAttack_PlungeJump, null))
                    jumpAttack_PlungeJump.Invoke(jumpAttack, null);
                else
                    Plugin.Logger.LogWarning("[RegenerativeJumpResponder] jumpAttack_PlungeJump 方法为 null，跳跃无法执行");

                Plugin.Logger.LogInfo("[RegenerativeJumpResponder] " + agent.name + " 执行跳劈反击，目标 " + target.name);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[RegenerativeJumpResponder] 跳劈执行失败: " + ex.Message);
                // 失败时禁用组件，避免残留状态
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
