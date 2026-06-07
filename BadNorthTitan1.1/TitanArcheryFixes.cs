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
    /// 泰坦弓箭手索敌、瞄准与弹道修复 v6。
    /// 
    /// 策略：
    /// - 5个核心补丁保持高速直线弹道（GetSight/AimAt/SetupLoS/InSight/Shoot）
    /// - MaybeSetup 放行 → ArcheryFocusComponent 正常初始化（修复 NPE 循环）
    /// - ShootAt 放行 → 允许使用专注技能
    /// - DirectUpdate finalizer → 兜底抑制异常
    /// 
    /// 【根因分析】
    /// v5 阻止 MaybeSetup 导致 ArcheryFocusComponent 未初始化。
    /// ArcheryFocusAbility.DoSquadSpawnAction_Implementation 创建的 lambda (m__0)
    /// 捕获了未初始化的组件引用，在 AgentState.DirectUpdate 中每帧触发 NPE。
    /// v6 放行 MaybeSetup 让组件正常初始化，从根源消除空引用。
    /// 
    /// 【ModifyArrow】
    /// Harmony 在 Mono 2.0 CLR 下无法打补丁到 ArcheryFocusAbility.ModifyArrow。
    /// v6 放行 MaybeSetup 后 ModifyArrow 正常执行，若泰坦的高速弹道参数
    /// 与其不兼容导致崩溃，由 DirectUpdate finalizer 兜底抑制。
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

            // 放行 MaybeSetup — 允许组件初始化（v6 修复 DoSquadSpawnAction 空引用）
            harmony.Patch(
                original: AccessTools.Method(typeof(ArcheryFocusComponent), "MaybeSetup"),
                prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(MaybeSetupPrefix))
            );

            // 放行 ShootAt — 允许使用专注技能（v6 修复）
            harmony.Patch(
                original: AccessTools.Method(typeof(ArcheryFocusComponent), "ShootAt"),
                prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(ShootAtPrefix))
            );

            // 兜底保护 — AgentState.DirectUpdate 异常抑制（ModifyArrow 不兼容时兜底）
            harmony.Patch(
                original: AccessTools.Method(typeof(AgentState), "DirectUpdate"),
                finalizer: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(DirectUpdateFinalizer))
            );

            Plugin.Logger.LogInfo("[TitanArcheryFixes] 8个补丁（v6）: GetSight | AimAt | SetupLoS | InSight | Shoot | MaybeSetup(放行) | ShootAt(放行) | DirectUpdate兜底");
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

        // ────────── 放行 MaybeSetup（v6） ──────────
        /// <summary>
        /// v6 修复：放行 MaybeSetup，允许 ArcheryFocusComponent 正常初始化。
        /// 阻止 MaybeSetup 会导致 SquadSpawn 状态机的 lambda 捕获未初始化的
        /// 组件引用，进而每帧触发 NullReferenceException。
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
                    "[Titan FocusFix] Archer#{0} MaybeSetup 已放行（v6）",
                    agentId));
            }
            return true;
        }

        // ────────── 放行 ShootAt（v6） ──────────
        /// <summary>
        /// v6 修复：放行 ShootAt，允许使用专注技能。
        /// 签名: ShootAt(ArcheryFocusAbility, Settings, Vector3, Vector3)
        /// </summary>
        private static bool ShootAtPrefix(ArcheryFocusComponent __instance)
        {
            if (__instance == null || __instance.gameObject == null) return true;

            Archery archery = __instance.GetComponent<Archery>();
            if (archery == null || !IsTitanArcher(archery.agent)) return true;

            return true;
        }

        // ────────── DirectUpdate 兜底保护（v6 新增） ──────────
        /// <summary>
        /// 捕获并抑制 AgentState.DirectUpdate 中因 SquadSpawn 状态机残留学循环
        /// 而抛出的任何异常（主要是 NullReferenceException）。
        /// 这是最后兜底，在 DoSquadSpawnAction 补丁找不到方法时提供安全保护。
        /// </summary>
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
                    return null; // 抑制异常
                }
            }
            return __exception; // 非泰坦的异常正常抛出
        }
    }
}
