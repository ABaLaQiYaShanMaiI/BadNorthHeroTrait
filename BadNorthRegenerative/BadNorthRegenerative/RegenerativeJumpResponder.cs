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
            if (agent == null)
            {
                Plugin.Logger.LogError("[RegenerativeJumpResponder] Awake: 未找到Agent组件");
                return;
            }

            // 缓存反射信息
            CacheReflectionInfo();
        }

        private static void CacheReflectionInfo()
        {
            if (shooterField != null) return; // 已缓存

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
            if (agent == null) return;

            // 将自己插入 attackResponders 首位
            if (!agent.attackResponders.Contains(this))
            {
                agent.attackResponders.Insert(0, this);
            }

            // 尝试获取或创建 JumpAttack 组件
            jumpAttack = agent.GetComponent<JumpAttack>();
            if (jumpAttack == null)
            {
                // 从 Viking_Twohanded 参考对象复制
                try
                {
                    GameObject template = LevelStateObjectReferences.dict["Viking_Twohanded"] as GameObject;
                    if (template != null)
                    {
                        JumpAttack templateJump = template.GetComponent<JumpAttack>();
                        if (templateJump != null)
                        {
                            jumpAttack = agent.gameObject.AddComponent<JumpAttack>();
                            // 通过反射复制私有字段
                            CopyJumpAttackFields(templateJump, jumpAttack);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"[RegenerativeJumpResponder] 无法从模板复制JumpAttack: {ex.Message}");
                }
            }

            if (jumpAttack != null)
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
            if (jumpAttack_attackSettings != null)
                jumpAttack_attackSettings.SetValue(destination, jumpAttack_attackSettings.GetValue(source));
            if (jumpAttack_plungeState != null)
                jumpAttack_plungeState.SetValue(destination, jumpAttack_plungeState.GetValue(source));
            if (jumpAttack_plungePrepareState != null)
                jumpAttack_plungePrepareState.SetValue(destination, jumpAttack_plungePrepareState.GetValue(source));
            if (jumpAttack_plungeAttackState != null)
                jumpAttack_plungeAttackState.SetValue(destination, jumpAttack_plungeAttackState.GetValue(source));
            if (jumpAttack_landPos != null)
                jumpAttack_landPos.SetValue(destination, jumpAttack_landPos.GetValue(source));
            if (jumpAttack_faceDirection != null)
                jumpAttack_faceDirection.SetValue(destination, jumpAttack_faceDirection.GetValue(source));
            if (jumpAttack_plungeJumpId != null)
                jumpAttack_plungeJumpId.SetValue(destination, jumpAttack_plungeJumpId.GetValue(source));
            if (jumpAttack_attackAnimId != null)
                jumpAttack_attackAnimId.SetValue(destination, jumpAttack_attackAnimId.GetValue(source));
        }

        private void Update()
        {
            if (cooldownTimer > 0f)
            {
                cooldownTimer -= Time.deltaTime;
            }

            if (pendingJumpTarget != null && jumpAttack != null)
            {
                ExecuteJump(pendingJumpTarget);
                pendingJumpTarget = null;
            }
        }

        public void ModifyAttack(ref Attack attack)
        {
            if (agent == null || !agent.isActiveAndEnabled) return;
            if (cooldownTimer > 0f) return;

            // 检查攻击来源是否为远程投射物
            Shootable shootable = attack.monoAttacker as Shootable;
            if (shootable == null) return;

            // 检查防御者存活
            if (IsAgentDead(agent)) return;

            // 获取远程攻击者的 Agent
            Agent attackerAgent = GetShooterAgent(shootable);
            if (attackerAgent == null) return;
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

            Plugin.Logger.LogInfo($"[RegenerativeJumpResponder] {agent.name} 即将跳劈反击 {attackerAgent.name}");
        }

        private void ExecuteJump(Agent target)
        {
            if (jumpAttack == null || target == null) return;

            try
            {
                // 通过反射设置目标
                if (jumpAttack_target != null)
                {
                    jumpAttack_target.SetValue(jumpAttack, target);
                }

                // 设置落点（使用目标的 NavPos 位置）
                if (jumpAttack_landPos != null)
                {
                    NavPos landNavPos = new NavPos(target.navPos.tri, target.navPos.pos);
                    jumpAttack_landPos.SetValue(jumpAttack, landNavPos);
                }
                if (jumpAttack_faceDirection != null)
                {
                    Vector3 direction = (target.transform.position - agent.transform.position).normalized;
                    jumpAttack_faceDirection.SetValue(jumpAttack, direction);
                }

                // 向目标施加威胁（使用 Agent.rangeWorry）
                if (target.brain != null && agent.rangeWorry != null)
                {
                    agent.rangeWorry.threat = jumpAttack;
                    agent.rangeWorry.threatComponent = jumpAttack;
                    agent.rangeWorry.distance = Vector3.Distance(agent.transform.position, target.transform.position);
                    agent.rangeWorry.dir = (target.transform.position - agent.transform.position).normalized;
                }

                // 通过反射调用 PlungeJump 私有方法
                if (jumpAttack_PlungeJump != null)
                {
                    jumpAttack_PlungeJump.Invoke(jumpAttack, null);
                }

                Plugin.Logger.LogInfo($"[RegenerativeJumpResponder] {agent.name} 执行跳劈反击");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[RegenerativeJumpResponder] 跳劈执行失败: {ex.Message}");
            }
        }

        private Agent GetShooterAgent(Shootable shootable)
        {
            try
            {
                // 尝试通过反射获取 shooter 字段
                if (shooterField != null)
                {
                    object shooter = shooterField.GetValue(shootable);
                    if (shooter is WeakReference weakRef && targetProperty != null)
                    {
                        return targetProperty.GetValue(weakRef, null) as Agent;
                    }
                }

                // 回退：尝试通过 agent 字段
                if (agentField != null)
                {
                    return agentField.GetValue(shootable) as Agent;
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"[RegenerativeJumpResponder] 获取攻击者Agent失败: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// 通过反射检查 Agent 是否死亡
        /// </summary>
        private bool IsAgentDead(Agent targetAgent)
        {
            if (targetAgent == null) return true;

            // 检查 health <= 0
            if (targetAgent.health <= 0f) return true;

            // 通过反射检查 deadState
            if (agent_deadState != null)
            {
                AgentState deadState = agent_deadState.GetValue(targetAgent) as AgentState;
                if (deadState != null && deadState.active) return true;
            }

            return false;
        }

        /// <summary>
        /// 通过反射检查 Agent 是否在地面上
        /// </summary>
        private bool IsAgentGrounded(Agent targetAgent)
        {
            if (targetAgent == null) return false;

            if (agent_groundedState != null)
            {
                AgentState groundedState = agent_groundedState.GetValue(targetAgent) as AgentState;
                if (groundedState != null)
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
            if (ja == null) return false;

            // 通过 JumpComponent 的 jumpingState 判断
            if (jumpAttack_jumpComponent != null)
            {
                JumpComponent jc = jumpAttack_jumpComponent.GetValue(ja) as JumpComponent;
                if (jc != null && jumpComponent_jumpingState != null)
                {
                    AgentState jumpingState = jumpComponent_jumpingState.GetValue(jc) as AgentState;
                    if (jumpingState != null)
                    {
                        return jumpingState.active;
                    }
                }
            }

            // 回退：检查 plungeState
            if (jumpAttack_plungeState != null)
            {
                AgentState plungeState = jumpAttack_plungeState.GetValue(ja) as AgentState;
                if (plungeState != null && plungeState.active) return true;
            }

            return false;
        }

        private bool IsSquadMemberJumping()
        {
            if (agent.squad == null) return false;

            foreach (Agent squadAgent in agent.squad.agents)
            {
                if (squadAgent != agent)
                {
                    JumpAttack otherJump = squadAgent.GetComponent<JumpAttack>();
                    if (otherJump != null && otherJump.enabled && IsJumpAttackJumping(otherJump))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void OnDestroy()
        {
            if (agent != null && agent.attackResponders != null)
            {
                agent.attackResponders.Remove(this);
            }
        }
    }
}
