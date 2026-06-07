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
    /// 泰坦弓箭手索敌、瞄准与弹道修复 v5（生产版本）。
    /// 
    /// 策略：
    /// - 5个核心补丁保持高速直线弹道（GetSight/AimAt/SetupLoS/InSight/Shoot）
    /// - MaybeSetup 阻止（源头防止 ModifyArrow 空引用）
    /// - ShootAt 阻止（防止玩家手动点专注技能崩溃）
    /// 
    /// 【ModifyArrow 不可补丁】
    /// 诊断证实：Harmony 对 ArcheryFocusAbility.ModifyArrow 的动态方法包装在
    /// Mono 2.0 CLR 下触发 FieldInfo.op_Inequality MissingMethodException（Harmony 自身问题）。
    /// 因此不能对 ModifyArrow 打补丁，必须从 MaybeSetup 源头阻止。
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

            // 阻止 MaybeSetup — 源头关闭 Arrow 修改循环
            harmony.Patch(
                original: AccessTools.Method(typeof(ArcheryFocusComponent), "MaybeSetup"),
                prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(MaybeSetupPrefix))
            );

            // 阻止 ShootAt — 防止玩家点击专注技能崩溃
            harmony.Patch(
                original: AccessTools.Method(typeof(ArcheryFocusComponent), "ShootAt"),
                prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(ShootAtPrefix))
            );

            Plugin.Logger.LogInfo("[TitanArcheryFixes] 7个补丁（v5）: GetSight | AimAt | SetupLoS | InSight | Shoot | MaybeSetup | ShootAt");
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

        // ────────── 阻止 MaybeSetup ──────────
        /// <summary>
        /// 源头阻止 ArcheryFocusComponent 初始化，关闭 ModifyArrow 每帧循环。
        /// 不禁用组件（不设置 enabled=false）以保持引用有效。
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
                    "[Titan FocusFix] Archer#{0} MaybeSetup 已阻止",
                    agentId));
            }
            return false;
        }

        // ────────── 阻止 ShootAt ──────────
        /// <summary>
        /// 阻止玩家手动点击专注技能按钮的 ShootAt 调用。
        /// 签名: ShootAt(ArcheryFocusAbility, Settings, Vector3, Vector3)
        /// </summary>
        private static bool ShootAtPrefix(ArcheryFocusComponent __instance)
        {
            if (__instance == null || __instance.gameObject == null) return true;

            Archery archery = __instance.GetComponent<Archery>();
            if (archery == null || !IsTitanArcher(archery.agent)) return true;

            return false;
        }
    }
}