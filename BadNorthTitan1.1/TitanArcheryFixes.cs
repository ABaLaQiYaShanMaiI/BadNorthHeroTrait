using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;
using Voxels.TowerDefense.Upgrades;

namespace BadNorthTitan
{
    /// <summary>
    /// 泰坦弓箭手索敌、瞄准与弹道修复 v8。
    /// 
    /// 策略：
    /// - 5个核心补丁保持高速直线弹道（GetSight/AimAt/SetupLoS/InSight/Shoot）
    /// - MaybeSetup 阻止 → ArcheryFocusComponent 不初始化，m__1 lambda 不创建（根除 ModifyArrow NPE）
    /// - ShootAt 阻止 → 专注技能不可用
    /// - DoSquadSpawnAction prefix → 处理 m__0 lambda 的空引用
    /// - DirectUpdate finalizer → 兜底抑制异常
    /// 
    /// 【v8 策略】
    /// MaybeSetup 内部创建 m__1 lambda（per-frame 回调），该 lambda 每帧调用 ModifyArrow 
    /// 并触发 NPE，导致 DirectUpdate 中断 → Agent 更新停滞 → 士兵死亡。
    /// Harmony 在 Mono 2.0 CLR 下无法 patch ModifyArrow。
    /// v8 彻底阻止 MaybeSetup（return false），从根源消除 m__1 lambda。
    /// 专注技能对泰坦弓箭手不可用，但5个核心补丁提供的自定义索敌/弹道系统正常工作。
    /// </summary>
    public static class TitanArcheryFixes
    {
        private const float AttackRange = 8f;
        private const float AttackRangeSqr = 64f;

        // 箭矢弹道参数（参考 Giantarcher）
        private const float ArrowSpeed = 17f;
        private const float ArrowDrag = 0f;
        private const float ArrowGravity = 0f;
        private const float ArrowStartOffset = 0.8f;
        private const float SpreadNonFocus = 0.05f;
        private const float BaseCooldown = 4f;
        private const float CooldownExtra = 3.5f;
        private const float CooldownRandomMax = 1.5f;

        // 日志去重
        private static HashSet<int> _loggedSightAgents = new HashSet<int>();
        private static HashSet<int> _loggedAimAgents = new HashSet<int>();
        private static HashSet<int> _loggedSetupAgents = new HashSet<int>();
        private static HashSet<int> _blockedMaybeSetupAgents = new HashSet<int>();
        private static HashSet<int> _blockedDoSquadSpawnAgents = new HashSet<int>();
        private static int _shotCountSinceLastLog = 0;
        private static float _lastShotLogTime = 0f;
        private const float ShotLogInterval = 3f;
        private const int ShotLogBatchSize = 5;

        // 缓存反射字段（ReferenceEquals 规避 Mono 2.0 FieldInfo.op_Inequality 缺失）
        private static FieldInfo _radiusField = null;
        private static bool _radiusFieldAttempted = false;
        private static FieldInfo _sqRadiusField = null;
        private static bool _sqRadiusFieldAttempted = false;
        private static FieldInfo _coolDownTimeField = null;
        private static bool _coolDownTimeFieldAttempted = false;
        private static FieldInfo _agentStateAgentField = null;
        private static bool _agentStateAgentFieldAttempted = false;

        private static bool IsTitanArcher(Agent agent)
        {
            return agent != null
                && agent.isEnglish
                && agent.scale > 1.1f
                && agent.GetComponent<Archery>() != null;
        }

        public static void ApplyPatches(Harmony harmony)
        {
            // 索敌
            harmony.Patch(
                original: AccessTools.Method(typeof(LineOfSight), "GetSight"),
                prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(GetSightPrefix))
            );

            // 瞄准
            harmony.Patch(
                original: AccessTools.Method(typeof(Archery), "AimAt"),
                prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(AimAtPrefix))
            );

            // 扩大索敌半径
            harmony.Patch(
                original: AccessTools.Method(typeof(LineOfSight), "SetupLineOfSight"),
                postfix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(SetupLineOfSightPostfix))
            );

            // 简化视野验证
            harmony.Patch(
                original: AccessTools.Method(typeof(ArcheryTargeter), "InSight"),
                prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(InSightPrefix))
            );

            // 箭矢弹道优化
            harmony.Patch(
                original: AccessTools.Method(typeof(Archery), "Shoot"),
                prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(ShootPrefix))
            );

            // 阻止 MaybeSetup — 根除 m__1 lambda 及 ModifyArrow NPE（v8）
            harmony.Patch(
                original: AccessTools.Method(typeof(ArcheryFocusComponent), "MaybeSetup"),
                prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(MaybeSetupPrefix))
            );

            // 阻止 ShootAt — 专注技能对泰坦不可用（v8）
            harmony.Patch(
                original: AccessTools.Method(typeof(ArcheryFocusComponent), "ShootAt"),
                prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(ShootAtPrefix))
            );

            // DoSquadSpawnAction null 检查 — 处理 m__0 lambda 空引用（v8）
            MethodInfo doSquadSpawnActionMethod = AccessTools.Method(typeof(ArcheryFocusAbility), "DoSquadSpawnAction_Implementation");
            if (!ReferenceEquals(doSquadSpawnActionMethod, null))
            {
                harmony.Patch(
                    original: doSquadSpawnActionMethod,
                    prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(DoSquadSpawnActionPrefix))
                );
            }
            else
            {
                doSquadSpawnActionMethod = AccessTools.Method(typeof(ArcheryFocusAbility), "DoSquadSpawnAction");
                if (!ReferenceEquals(doSquadSpawnActionMethod, null))
                {
                    harmony.Patch(
                        original: doSquadSpawnActionMethod,
                        prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(DoSquadSpawnActionPrefix))
                    );
                }
                else
                {
                    Plugin.Logger.LogWarning("[TitanArcheryFixes] DoSquadSpawnAction 方法未找到，跳过该补丁");
                }
            }

            // 兜底保护 — AgentState.DirectUpdate 异常抑制
            harmony.Patch(
                original: AccessTools.Method(typeof(AgentState), "DirectUpdate"),
                finalizer: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(DirectUpdateFinalizer))
            );

            Plugin.Logger.LogInfo("[TitanArcheryFixes] 9个补丁（v8）: GetSight | AimAt | SetupLoS | InSight | Shoot | MaybeSetup(阻止) | ShootAt(阻止) | DoSquadSpawnAction | DirectUpdate兜底");
        }

        // ────────── 索敌 ──────────
        private static bool GetSightPrefix(LineOfSight __instance, ref LineOfSight.Sight __result)
        {
            if (!IsTitanArcher(__instance.agent)) return true;

            Faction enemyFaction = __instance.agent.faction.enemy;
            if (enemyFaction == null) return true;

            List<Agent> enemies = enemyFaction.agents;
            Agent bestTarget = null;
            float bestDistSqr = float.MaxValue;
            Vector3 chestPos = __instance.agent.chestPos;
            int layerMask = LayerMaster.arrowLow;

            for (int i = 0; i < enemies.Count; i++)
            {
                Agent enemy = enemies[i];
                if (enemy == null || !enemy.gameObject.activeInHierarchy || !enemy.aliveAndGrounded.active)
                    continue;

                float distSqr = (enemy.chestPos - chestPos).sqrMagnitude;
                if (distSqr < AttackRangeSqr && distSqr < bestDistSqr)
                {
                    Vector3 dir = (enemy.chestPos - chestPos).normalized;
                    float dist = Mathf.Sqrt(distSqr);
                    if (!Physics.Raycast(chestPos + Vector3.up * 0.5f, dir, dist * 0.9f, layerMask))
                    {
                        bestDistSqr = distSqr;
                        bestTarget = enemy;
                    }
                }
            }

            if (bestTarget != null)
            {
                LineOfSight.Sight sight = default(LineOfSight.Sight);
                sight.agent = bestTarget;
                sight.mask0 = LayerMaster.arrowLow;
                sight.mask1 = LayerMaster.arrowHigh;
                sight.score = -bestDistSqr;

                __instance.enemies.Clear();
                __instance.enemies.Add(sight);
                __result = sight;

                int targetId = bestTarget.GetInstanceID();
                if (_loggedSightAgents.Add(targetId))
                {
                    float dist = Mathf.Sqrt(bestDistSqr);
                    Plugin.Logger.LogInfo(string.Format(
                        "[Titan Sight] Archer#{0} → {1}#{2} | {3:F1}m",
                        __instance.agent.GetInstanceID(), bestTarget.name, targetId, dist));
                }
                return false;
            }
            return true;
        }

        // ────────── 瞄准 ──────────
        private static bool AimAtPrefix(Archery __instance, Vector3 targetPos, ref bool __result)
        {
            if (!IsTitanArcher(__instance.agent)) return true;

            Vector3 dir = (targetPos - __instance.ShootPos).normalized;
            __instance.aimDirTarget = dir;
            __result = true;

            int agentId = __instance.agent.GetInstanceID();
            if (_loggedAimAgents.Add(agentId))
            {
                float dist = Vector3.Distance(__instance.ShootPos, targetPos);
                Plugin.Logger.LogInfo(string.Format(
                    "[Titan Aim] Archer#{0} dist={1:F1}m dir=({2:F2},{3:F2},{4:F2})",
                    agentId, dist, dir.x, dir.y, dir.z));
            }
            return false;
        }

        // ────────── 扩大索敌半径 ──────────
        private static void SetupLineOfSightPostfix(LineOfSight __instance)
        {
            if (!IsTitanArcher(__instance.agent)) return;

            int agentId = __instance.agent.GetInstanceID();
            if (_loggedSetupAgents.Add(agentId))
            {
                Plugin.Logger.LogInfo(string.Format(
                    "[Titan Setup] Archer#{0} LoS radius={1}m sq={2}",
                    agentId, AttackRange, AttackRangeSqr));
            }

            if (!_radiusFieldAttempted) { _radiusFieldAttempted = true; _radiusField = typeof(LineOfSight).GetField("radius", BindingFlags.Instance | BindingFlags.NonPublic); }
            if (!ReferenceEquals(_radiusField, null)) _radiusField.SetValue(__instance, AttackRange);

            if (!_sqRadiusFieldAttempted) { _sqRadiusFieldAttempted = true; _sqRadiusField = typeof(LineOfSight).GetField("sqRadius", BindingFlags.Instance | BindingFlags.NonPublic); }
            if (!ReferenceEquals(_sqRadiusField, null)) _sqRadiusField.SetValue(__instance, AttackRangeSqr);
        }

        // ────────── 简化视野验证 ──────────
        private static bool InSightPrefix(Vector3 testPosition, Vector3 targeterPosition, ref bool __result)
        {
            if (Vector3.Distance(testPosition, targeterPosition) < AttackRange)
            {
                __result = true;
                return false;
            }
            return true;
        }

        // ────────── 射击优化 ──────────
        private static bool ShootPrefix(Archery __instance, ref Vector3 shootDir, ref ProjectileSettings projectileSettings)
        {
            if (!IsTitanArcher(__instance.agent)) return true;

            ProjectileSettings newSettings = new ProjectileSettings();
            FieldInfo[] fields = typeof(ProjectileSettings).GetFields();
            foreach (FieldInfo fi in fields)
            {
                fi.SetValue(newSettings, fi.GetValue(projectileSettings));
            }
            newSettings.maxSpeed = ArrowSpeed;
            newSettings.drag = ArrowDrag;
            newSettings.gravity = ArrowGravity;
            newSettings.startOffset = ArrowStartOffset;

            Vector3 shootDirOriginal = Vector3.zero;
            FieldInfo targetField = typeof(Archery).GetField("target", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (!ReferenceEquals(targetField, null))
            {
                object targetObj = targetField.GetValue(__instance);
                if (targetObj != null)
                {
                    LineOfSight.Sight sight = (LineOfSight.Sight)targetObj;
                    if (sight.agent != null)
                        shootDirOriginal = (sight.agent.chestPos - __instance.ShootPos).normalized;
                }
            }
            if (shootDirOriginal == Vector3.zero) shootDirOriginal = __instance.transform.forward;

            Vector3 horizontalDir = shootDirOriginal;
            horizontalDir.y = 0f;
            if (horizontalDir != Vector3.zero) __instance.transform.rotation = Quaternion.LookRotation(horizontalDir);
            __instance.aimDirTarget = shootDirOriginal;

            Vector3 shootVelocity = shootDirOriginal * ArrowSpeed;
            shootVelocity += UnityEngine.Random.insideUnitSphere * shootVelocity.magnitude * SpreadNonFocus;
            shootDir = shootVelocity;
            projectileSettings = newSettings;

            float cooldown = BaseCooldown + CooldownExtra + UnityEngine.Random.Range(0f, CooldownRandomMax);
            if (!_coolDownTimeFieldAttempted) { _coolDownTimeFieldAttempted = true; _coolDownTimeField = typeof(Archery).GetField("coolDownTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
            if (!ReferenceEquals(_coolDownTimeField, null)) _coolDownTimeField.SetValue(__instance, Time.time + cooldown);

            _shotCountSinceLastLog++;
            float now = Time.time;
            if (_shotCountSinceLastLog >= ShotLogBatchSize || now - _lastShotLogTime >= ShotLogInterval)
            {
                Plugin.Logger.LogInfo(string.Format(
                    "[Titan Shot] Archer#{0} ×{1} speed={2} grav={3} drag={4} spread={5:F3} cd={6:F1}s",
                    __instance.agent.GetInstanceID(), _shotCountSinceLastLog,
                    ArrowSpeed, ArrowGravity, ArrowDrag, SpreadNonFocus, cooldown));
                _shotCountSinceLastLog = 0;
                _lastShotLogTime = now;
            }

            return true;
        }

        // ────────── 阻止 MaybeSetup（v8） ──────────
        /// <summary>
        /// v8: 彻底阻止泰坦弓箭手的 MaybeSetup。
        /// 阻止后 m__1 lambda 不创建 → ModifyArrow NPE 从根源消除。
        /// 专注技能对泰坦弓箭手不可用。
        /// </summary>
        private static bool MaybeSetupPrefix(ArcheryFocusComponent __instance)
        {
            if (__instance == null || __instance.gameObject == null) return true;

            Archery archery = __instance.GetComponent<Archery>();
            if (archery == null || !IsTitanArcher(archery.agent)) return true;

            int agentId = archery.agent.GetInstanceID();
            if (_blockedMaybeSetupAgents.Add(agentId))
            {
                Plugin.Logger.LogInfo(string.Format(
                    "[Titan FocusFix] Archer#{0} MaybeSetup 已阻止（v8）",
                    agentId));
            }
            return false; // 阻止 MaybeSetup → 根除 m__1 lambda
        }

        // ────────── 阻止 ShootAt（v8） ──────────
        /// <summary>
        /// v8: 阻止 ShootAt，专注技能对泰坦弓箭手不可用。
        /// </summary>
        private static bool ShootAtPrefix(ArcheryFocusComponent __instance)
        {
            if (__instance == null || __instance.gameObject == null) return true;

            Archery archery = __instance.GetComponent<Archery>();
            if (archery == null || !IsTitanArcher(archery.agent)) return true;

            return false; // 阻止 ShootAt → 专注技能不可用
        }

        // ────────── DoSquadSpawnAction null 检查（v8） ──────────
        /// <summary>
        /// v8: DoSquadSpawnAction_Implementation 内部创建 m__0 lambda，
        /// 捕获 ArcheryFocusComponent 引用。MaybeSetup 被阻止时组件未初始化，
        /// 导致 m__0 空引用。此 prefix 对泰坦弓箭手跳过 DoSquadSpawnAction。
        /// </summary>
        private static bool DoSquadSpawnActionPrefix(ArcheryFocusAbility __instance)
        {
            if (__instance == null) return true;

            // 尝试多种可能的字段名找到 agent 引用
            string[] candidateFields = { "agent", "_agent", "heroAgent", "_heroAgent", "owner", "_owner" };
            Agent foundAgent = null;

            foreach (string fieldName in candidateFields)
            {
                FieldInfo fi = typeof(ArcheryFocusAbility).GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!ReferenceEquals(fi, null))
                {
                    foundAgent = fi.GetValue(__instance) as Agent;
                    if (foundAgent != null) break;
                }
            }

            if (foundAgent == null)
            {
                // 回退: 通过组件链查找
                FieldInfo compField = typeof(ArcheryFocusAbility).GetField("archery",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!ReferenceEquals(compField, null))
                {
                    Archery archery = compField.GetValue(__instance) as Archery;
                    if (archery != null)
                        foundAgent = archery.agent;
                }
            }

            if (foundAgent != null && foundAgent.isEnglish && foundAgent.scale > 1.1f
                && foundAgent.GetComponent<Archery>() != null)
            {
                int agentId = foundAgent.GetInstanceID();
                if (_blockedDoSquadSpawnAgents.Add(agentId))
                {
                    Plugin.Logger.LogInfo(string.Format(
                        "[Titan FocusFix] Archer#{0} DoSquadSpawnAction 已阻止（v8）",
                        agentId));
                }
                return false;
            }
            return true;
        }

        // ────────── DirectUpdate 兜底保护 ──────────
        private static Exception DirectUpdateFinalizer(AgentState __instance, Exception __exception)
        {
            if (__exception != null && __instance != null)
            {
                if (!_agentStateAgentFieldAttempted)
                {
                    _agentStateAgentFieldAttempted = true;
                    _agentStateAgentField = typeof(AgentState).GetField("agent",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }

                Agent agent = null;
                if (!ReferenceEquals(_agentStateAgentField, null))
                    agent = _agentStateAgentField.GetValue(__instance) as Agent;

                if (agent != null && agent.isEnglish && agent.scale > 1.1f && agent.GetComponent<Archery>() != null)
                {
                    Plugin.Logger.LogWarning(string.Format(
                        "[Titan FocusFix] Archer#{0} DirectUpdate 异常已抑制: {1}",
                        agent.GetInstanceID(),
                        __exception.GetType().Name));
                    return null;
                }
            }
            return __exception;
        }
    }
}