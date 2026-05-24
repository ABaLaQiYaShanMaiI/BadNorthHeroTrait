using System;
using System.Reflection;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorthRegenerative
{
    /// <summary>
    /// 跳劈反击组件：当受 Regenerative 特质影响的盾兵单位被远程攻击时，
    /// 该组件会触发跳劈反击，跃向远程攻击者进行攻击。
    /// 对应原 PlentyTraits 中的 RegenerativeJumpResponder。
    /// </summary>
    public class RegenerativeJumpResponder : MonoBehaviour, IAttackResponder
    {
        private Agent agent;
        private JumpAttack jumpAttack;
        private Agent pendingJumpTarget;
        private float cooldownTimer = 0f;
        private float cooldownDuration = 1.5f;

        // 通过反射缓存的字段信息
        private static FieldInfo shooterField;
        private static PropertyInfo targetProperty;
        private static FieldInfo agentField;

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
            agent = GetComponent<Agent>();
            if (ReferenceEquals(agent, null))
            {
                Plugin.Logger.LogError("[RegenerativeJumpResponder] Awake: 未找到Agent组件");
                return;
            }

            // 缓存反射信息
            CacheReflectionInfo();
        }

        private static void CacheReflectionInfo()
        {
            // 使用 ReferenceEquals 避免 Mono 2.0 下 FieldInfo.op_Inequality 缺失问题
            if (!ReferenceEquals(shooterField, null)) return; // 已缓存

            Type shootableType = typeof(Shootable);
            shooterField = shootableType.GetField("shooter", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Type weakRefType = typeof(WeakReference);
            targetProperty = weakRefType.GetProperty("Target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            agentField = shootableType.GetField("agent", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            // JumpAttack 私有字段
            Type jumpAttackType = typeof(JumpAttack);
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

            // JumpAttack 私有方法
            jumpAttack_PlungeJump = jumpAttackType.GetMethod("PlungeJump", BindingFlags.Instance | BindingFlags.NonPublic);

            // JumpComponent 字段
            Type jumpComponentType = typeof(JumpComponent);
            jumpComponent_jumpingState = jumpComponentType.GetField("jumpingState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            jumpComponent_targetPos = jumpComponentType.GetField("targetPos", BindingFlags.Instance | BindingFlags.NonPublic);

            // Agent 状态字段
            Type agentType = typeof(Agent);
            agent_groundedState = agentType.GetField("groundedState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            agent_deadState = agentType.GetField("deadState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            agent_lifeState = agentType.GetField("lifeState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        }

        private void Start()
        {
            if (ReferenceEquals(agent, null)) return;

            // 将自己插入 attackResponders 首位
            if (!agent.attackResponders.Contains(this))
            {
                agent.attackResponders.Insert(0, this);
                Plugin.Logger.LogInfo($"[JumpResponder] 已加入 attackResponders，当前数量={agent.attackResponders.Count}");
            }

            // 尝试获取或创建 JumpAttack 组件
            jumpAttack = agent.GetComponent<JumpAttack>();
            if (ReferenceEquals(jumpAttack, null))
            {
                // 从 Viking_Twohanded 参考对象复制
                // 三级容错：LevelStateObjectReferences.dict 的值可能不是 GameObject 类型
                try
                {
                    GameObject template = null;
                    if (LevelStateObjectReferences.dict.TryGetValue("Viking_Twohanded", out UnityEngine.Object refObj))
                    {
                        // 第一级：直接 as GameObject
                        template = refObj as GameObject;
                        
                        // 第二级：如果是 Component，获取其 gameObject
                        if (ReferenceEquals(template, null))
                        {
                            Component comp = refObj as Component;
                            if (!ReferenceEquals(comp, null))
                            {
                                template = comp.gameObject;
                            }
                        }
                        
                        // 第三级：如果是 VikingReference（参考 AxeThrower 的反射逻辑），尝试获取 viking 或 vikingClone 的 gameObject
                        if (ReferenceEquals(template, null) && refObj is VikingReference vRef)
                        {
                            try
                            {
                                FieldInfo vikingCloneField = typeof(VikingReference).GetField("vikingClone",
                                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                                FieldInfo vikingField = typeof(VikingReference).GetField("viking",
                                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                                
                                Component vikingClone = null;
                                Component viking = null;
                                if (!ReferenceEquals(vikingCloneField, null))
                                    vikingClone = vikingCloneField.GetValue(vRef) as Component;
                                if (!ReferenceEquals(vikingField, null))
                                    viking = vikingField.GetValue(vRef) as Component;
                                
                                if (!ReferenceEquals(vikingClone, null))
                                    template = vikingClone.gameObject;
                                else if (!ReferenceEquals(viking, null))
                                    template = viking.gameObject;
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
                            // 通过反射复制私有字段
                            CopyJumpAttackFields(templateJump, jumpAttack);
                        }
                    }
                    else
                    {
                        Plugin.Logger.LogWarning("[RegenerativeJumpResponder] 无法获取 Viking_Twohanded 模板对象，跳劈功能可能不可用");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning(string.Format("[RegenerativeJumpResponder] 无法从模板复制JumpAttack: {0}", ex.Message));
                }
            }

            if (!ReferenceEquals(jumpAttack, null))
            {
                // 调用 Setup 初始化
                jumpAttack.Setup(agent);
                // 禁用追击跳劈逻辑，避免与反击行为冲突
                jumpAttack.enabled = false;
            }

            Plugin.Logger.LogInfo("[RegenerativeJumpResponder] 初始化完成");
        }

        /// <summary>
        /// 通过反射从模板 JumpAttack 复制私有字段到目标 JumpAttack
        /// </summary>
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
                ExecuteJump(pendingJumpTarget);
                pendingJumpTarget = null;
            }
        }

        public void ModifyAttack(ref Attack attack)
        {
            // 每次调用都记录，方便确认是否触发（初期可保留，调试后改为条件输出）
            Plugin.Logger.LogDebug($"[JumpResponder] ModifyAttack called, agent={agent?.name}, monoAttacker={attack.monoAttacker?.GetType().Name}, monoAttacker.name={attack.monoAttacker?.name}");

            if (ReferenceEquals(agent, null) || !agent.isActiveAndEnabled) return;
            if (cooldownTimer > 0f) return;

            // 检查攻击来源是否为远程投射物
            Shootable shootable = attack.monoAttacker as Shootable;
            if (ReferenceEquals(shootable, null))
            {
                // 可能不是 Shootable，尝试直接取 Agent
                Agent directAttacker = attack.monoAttacker?.GetComponent<Agent>();
                if (ReferenceEquals(directAttacker, null)) return;
                // 补充处理...
                return; // 目前仍按原逻辑，可后续扩展
            }

            // 检查防御者存活
            if (IsAgentDead(agent)) return;

            // 获取远程攻击者的 Agent
            Agent attackerAgent = GetShooterAgent(shootable);
            if (ReferenceEquals(attackerAgent, null))
            {
                // 备选：直接通过 Shootable 所在物体的 Agent 组件获取
                attackerAgent = shootable.GetComponent<Agent>();
                if (ReferenceEquals(attackerAgent, null))
                {
                    Plugin.Logger.LogWarning("[JumpResponder] 无法获取攻击者Agent");
                    return;
                }
            }
            if (IsAgentDead(attackerAgent)) return;

            // 检查距离
            float distance = Vector3.Distance(agent.transform.position, attackerAgent.transform.position);
            if (distance > 5f) return;

            // 检查高度差
            float heightDiff = Mathf.Abs(agent.transform.position.y - attackerAgent.transform.position.y);
            if (heightDiff > 0.5f) return;

            // 检查攻击者是否在地面上
            if (!IsAgentGrounded(attackerAgent)) return;

            // 检查同小队内没有其他单位正在跳跃
            if (IsSquadMemberJumping()) return;

            // 设置跳劈目标
            pendingJumpTarget = attackerAgent;
            cooldownTimer = cooldownDuration;

            Plugin.Logger.LogInfo(string.Format("[RegenerativeJumpResponder] {0} 即将跳劈反击 {1}", agent.name, attackerAgent.name));
        }

        private void ExecuteJump(Agent target)
        {
            if (ReferenceEquals(jumpAttack, null) || ReferenceEquals(target, null)) return;

            try
            {
                // 通过反射设置目标
                if (!ReferenceEquals(jumpAttack_target, null))
                {
                    jumpAttack_target.SetValue(jumpAttack, target);
                }

                // 设置落点（使用目标的 NavPos 位置）
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

                // 向目标施加威胁（使用 Agent.rangeWorry）
                if (!ReferenceEquals(target.brain, null) && !ReferenceEquals(agent.rangeWorry, null))
                {
                    agent.rangeWorry.threat = jumpAttack;
                    agent.rangeWorry.threatComponent = jumpAttack;
                    agent.rangeWorry.distance = Vector3.Distance(agent.transform.position, target.transform.position);
                    agent.rangeWorry.dir = (target.transform.position - agent.transform.position).normalized;
                }

                // 通过反射调用 PlungeJump 私有方法
                if (!ReferenceEquals(jumpAttack_PlungeJump, null))
                {
                    jumpAttack_PlungeJump.Invoke(jumpAttack, null);
                }

                Plugin.Logger.LogInfo(string.Format("[RegenerativeJumpResponder] {0} 执行跳劈反击", agent.name));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning(string.Format("[RegenerativeJumpResponder] 跳劈执行失败: {0}", ex.Message));
            }
        }

        private Agent GetShooterAgent(Shootable shootable)
        {
            try
            {
                // 优先使用反射获取 shooter 字段
                if (!ReferenceEquals(shooterField, null))
                {
                    object shooter = shooterField.GetValue(shootable);
                    WeakReference weakRef = shooter as WeakReference;
                    if (!ReferenceEquals(weakRef, null) && !ReferenceEquals(targetProperty, null))
                    {
                        return targetProperty.GetValue(weakRef, null) as Agent;
                    }
                }

                // 回退：尝试通过 agent 字段
                if (!ReferenceEquals(agentField, null))
                {
                    return agentField.GetValue(shootable) as Agent;
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning(string.Format("[RegenerativeJumpResponder] 获取攻击者Agent失败: {0}", ex.Message));
            }

            // 最终回退：直接查找 Shootable 所在物体的 Agent
            return shootable.GetComponent<Agent>();
        }

        /// <summary>
        /// 通过反射检查 Agent 是否死亡
        /// </summary>
        private bool IsAgentDead(Agent targetAgent)
        {
            if (ReferenceEquals(targetAgent, null)) return true;

            // 检查 health <= 0
            if (targetAgent.health <= 0f) return true;

            // 通过反射检查 deadState
            if (!ReferenceEquals(agent_deadState, null))
            {
                AgentState deadState = agent_deadState.GetValue(targetAgent) as AgentState;
                if (!ReferenceEquals(deadState, null) && deadState.active) return true;
            }

            return false;
        }

        /// <summary>
        /// 通过反射检查 Agent 是否在地面上
        /// </summary>
        private bool IsAgentGrounded(Agent targetAgent)
        {
            if (ReferenceEquals(targetAgent, null)) return false;

            if (!ReferenceEquals(agent_groundedState, null))
            {
                AgentState groundedState = agent_groundedState.GetValue(targetAgent) as AgentState;
                if (!ReferenceEquals(groundedState, null))
                {
                    return groundedState.active;
                }
            }

            // 回退：默认认为在地面上
            return true;
        }

        /// <summary>
        /// 通过反射检查 JumpAttack 是否正在跳跃
        /// </summary>
        private bool IsJumpAttackJumping(JumpAttack ja)
        {
            if (ReferenceEquals(ja, null)) return false;

            // 通过 JumpComponent 的 jumpingState 判断
            if (!ReferenceEquals(jumpAttack_jumpComponent, null))
            {
                JumpComponent jc = jumpAttack_jumpComponent.GetValue(ja) as JumpComponent;
                if (!ReferenceEquals(jc, null) && !ReferenceEquals(jumpComponent_jumpingState, null))
                {
                    AgentState jumpingState = jumpComponent_jumpingState.GetValue(jc) as AgentState;
                    if (!ReferenceEquals(jumpingState, null))
                    {
                        return jumpingState.active;
                    }
                }
            }

            // 回退：检查 plungeState
            if (!ReferenceEquals(jumpAttack_plungeState, null))
            {
                AgentState plungeState = jumpAttack_plungeState.GetValue(ja) as AgentState;
                if (!ReferenceEquals(plungeState, null) && plungeState.active) return true;
            }

            return false;
        }

        private bool IsSquadMemberJumping()
        {
            if (ReferenceEquals(agent.squad, null)) return false;

            foreach (Agent squadAgent in agent.squad.agents)
            {
                if (!ReferenceEquals(squadAgent, agent))
                {
                    JumpAttack otherJump = squadAgent.GetComponent<JumpAttack>();
                    if (!ReferenceEquals(otherJump, null) && otherJump.enabled && IsJumpAttackJumping(otherJump))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            if (!ReferenceEquals(agent, null) && !ReferenceEquals(agent.attackResponders, null))
            {
                agent.attackResponders.Remove(this);
            }
        }
    }
}
