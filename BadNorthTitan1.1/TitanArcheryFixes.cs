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
    /// 泰坦弓箭手索敌、瞄准与弹道修复 v10 — 专注射击自建技能版。
    /// 
    /// v10 策略（巨弓手专注射击）：
    /// - 5个核心补丁保持高速直线弹道（GetSight/AimAt/SetupLoS/InSight/Shoot）
    /// - 新增 TitanFocusAbility + TitanFocusComponent 替代原版 ArcheryFocusAbility/ArcheryFocusComponent
    /// - MaybeSetup Prefix → 对泰坦弓箭手重定向到 TitanFocusComponent.SetupFocusState()
    /// - ShootAt Prefix → 对泰坦弓箭手重定向到 TitanFocusComponent.CustomShootAt()
    /// - DoSquadSpawnAction Prefix → 使用 TitanAgentRegistry 识别（解决 scale 时序问题）
    /// - DoTargetedAction Prefix → 使用 TitanAgentRegistry 识别，重定向到 TitanFocusAbility
    /// 
    /// v10 关键修复：
    /// - TitanAgentRegistry 在 OnAppliedToSquad 中提前注册 Agent，确保 Harmony 前缀在 Titanize() 设置 scale 之前即可正确拦截
    /// - TitanFocusHandler.RemoveOriginalFocusComponents 在 Destroy 原版组件前清理 AgentState.OnUpdate 委托，根除 m__0 NPE 循环
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

        private static bool IsTitanArcher(Agent agent)
        {
            return agent != null
                && agent.isEnglish
                && agent.scale > 1.1f
                && agent.GetComponent<Archery>() != null;
        }

        /// <summary>
        /// 通过反射从 ArcheryFocusComponent 获取其关联的 Agent。
        /// </summary>
        private static Agent GetAgentFromFocusComponent(Component focusComp)
        {
            if (ReferenceEquals(focusComp, null)) return null;

            // 先尝试直接获取 Archery
            Archery archery = focusComp.GetComponent<Archery>();
            if (!ReferenceEquals(archery, null) && !ReferenceEquals(archery.agent, null))
                return archery.agent;

            // 回退：向上查找 Agent
            Agent agent = focusComp.GetComponentInParent<Agent>();
            return agent;
        }

        public static void ApplyPatches(Harmony harmony)
        {
            // ── 5个核心补丁：索敌与弹道 ──

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

            // ── 专注射击重定向补丁（v10） ──

            // MaybeSetup → 重定向到 TitanFocusComponent.SetupFocusState
            harmony.Patch(
                original: AccessTools.Method(typeof(ArcheryFocusComponent), "MaybeSetup"),
                prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(MaybeSetupRedirectPrefix))
            );

            // ShootAt → 重定向到 TitanFocusComponent.CustomShootAt
            harmony.Patch(
                original: AccessTools.Method(typeof(ArcheryFocusComponent), "ShootAt"),
                prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(ShootAtRedirectPrefix))
            );

            // DoSquadSpawnAction → 使用 TitanAgentRegistry 识别，对泰坦跳过原版流程
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

            // DoTargetedAction → 使用 TitanAgentRegistry 识别，重定向到 TitanFocusAbility
            MethodInfo doTargetedActionMethod = AccessTools.Method(typeof(ArcheryFocusAbility), "DoTargetedAction_Implementation");
            if (!ReferenceEquals(doTargetedActionMethod, null))
            {
                harmony.Patch(
                    original: doTargetedActionMethod,
                    prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(DoTargetedActionRedirectPrefix))
                );
            }
            else
            {
                doTargetedActionMethod = AccessTools.Method(typeof(ArcheryFocusAbility), "DoTargetedAction");
                if (!ReferenceEquals(doTargetedActionMethod, null))
                {
                    harmony.Patch(
                        original: doTargetedActionMethod,
                        prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(DoTargetedActionRedirectPrefix))
                    );
                }
                else
                {
                    Plugin.Logger.LogWarning("[TitanArcheryFixes] DoTargetedAction 方法未找到，跳过该补丁");
                }
            }

            Plugin.Logger.LogInfo("[TitanArcheryFixes] 10个补丁（v10）: GetSight | AimAt | SetupLoS | InSight | Shoot | MaybeSetup(重定向) | ShootAt(重定向) | DoSquadSpawnAction | DoTargetedAction(重定向) | 自建TitanFocusAbility");
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

            // 检查是否处于自建专注状态 → 放行，保留 TitanFocusComponent 自身的 ProjectileSettings
            TitanFocusComponent tfComp = TitanFocusHandler.FindFocusComponent(__instance.agent);
            if (!ReferenceEquals(tfComp, null) && tfComp.IsActive)
            {
                return true; // 不修改弹道参数，专注使用自身设置
            }

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

        // ────────── MaybeSetup 重定向（v10） ──────────
        private static bool MaybeSetupRedirectPrefix(ArcheryFocusComponent __instance)
        {
            if (__instance == null || __instance.gameObject == null) return true;

            Agent agent = GetAgentFromFocusComponent(__instance);
            if (agent == null || !IsTitanArcher(agent)) return true;

            TitanFocusComponent titanFocus = TitanFocusHandler.FindFocusComponent(agent);
            if (ReferenceEquals(titanFocus, null))
            {
                Plugin.Logger.LogWarning(string.Format(
                    "[Titan FocusFix] Archer#{0} 缺失 TitanFocusComponent，跳过 MaybeSetup 重定向",
                    agent.GetInstanceID()));
                return false;
            }

            titanFocus.SetupFocusState();

            int agentId = agent.GetInstanceID();
            if (_blockedMaybeSetupAgents.Add(agentId))
            {
                Plugin.Logger.LogInfo(string.Format(
                    "[Titan FocusFix] Archer#{0} MaybeSetup 已重定向到 TitanFocusComponent（v10）",
                    agentId));
            }
            return false;
        }

        // ────────── ShootAt 重定向（v10） ──────────
        private static bool ShootAtRedirectPrefix(ArcheryFocusComponent __instance,
            object focusAbility, object settings, Vector3 targetPos, Vector3 targetDelta)
        {
            if (__instance == null || __instance.gameObject == null) return true;

            Agent agent = GetAgentFromFocusComponent(__instance);
            if (agent == null || !IsTitanArcher(agent)) return true;

            TitanFocusComponent titanFocus = TitanFocusHandler.FindFocusComponent(agent);
            TitanFocusAbility titanAbility = agent.GetComponent<TitanFocusAbility>();

            if (ReferenceEquals(titanFocus, null) || ReferenceEquals(titanAbility, null))
            {
                return false;
            }

            TitanFocusSettings focusSettings = titanAbility.CurrentSettings;

            // 修复 3：仅从原版 settings 中提取 attackSettings，不覆盖 ammo
            // ammo 由 TitanFocusSettings.CreateDefault(level) 定义（3+level）
            if (!ReferenceEquals(settings, null))
            {
                try
                {
                    Type settingsType = settings.GetType();
                    FieldInfo atkField = settingsType.GetField("attackSettings",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (!ReferenceEquals(atkField, null))
                    {
                        object atkObj = atkField.GetValue(settings);
                        if (atkObj is AttackSettings atk)
                        {
                            focusSettings.attackSettings = atk;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning("[Titan FocusFix] ShootAt 设置提取异常: " + ex.Message);
                }
            }

            titanFocus.CustomShootAt(titanAbility, focusSettings, targetPos, targetDelta);

            Plugin.Logger.LogInfo(string.Format(
                "[Titan FocusFix] Archer#{0} ShootAt 已重定向到 TitanFocusComponent（v10）",
                agent.GetInstanceID()));

            return false;
        }

        // ────────── DoSquadSpawnAction 跳过（v11） ──────────
        /// <summary>
        /// v11: 通过 Hero → Squad 检测是否具有 Titan 特质，不再依赖 Agent 反射。
        /// TitanFocusAbility 自己管理初始化和生命周期。
        /// </summary>
        private static bool DoSquadSpawnActionPrefix(ArcheryFocusAbility __instance)
        {
            if (TryGetSquadFromAbility(__instance, out EnglishSquad squad))
            {
                if (SquadHasTitanTrait(squad))
                {
                    Plugin.Logger.LogInfo("[Titan FocusFix] DoSquadSpawnAction 已阻止（通过英雄特质检测）");
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// 从 ArcheryFocusAbility 通过反射 hero 字段获取所属 Squad。
        /// </summary>
        private static bool TryGetSquadFromAbility(ArcheryFocusAbility ability, out EnglishSquad squad)
        {
            squad = null;
            if (ability == null) return false;

            // 反射获取 NavSpotTargetableAbility.hero
            var heroField = typeof(NavSpotTargetableAbility).GetField("hero",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (!ReferenceEquals(heroField, null))
            {
                var hero = heroField.GetValue(ability);
                if (hero != null)
                {
                    var squadField = hero.GetType().GetField("squad",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (!ReferenceEquals(squadField, null))
                    {
                        squad = squadField.GetValue(hero) as EnglishSquad;
                        if (squad != null) return true;
                    }

                    // 回退：尝试 _squad 字段名
                    var squadField2 = hero.GetType().GetField("_squad",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (!ReferenceEquals(squadField2, null))
                    {
                        squad = squadField2.GetValue(hero) as EnglishSquad;
                        if (squad != null) return true;
                    }
                }
            }

            return squad != null;
        }

        /// <summary>
        /// 检查 Squad 是否含有 Titan 特质（通过 TitanAgentRegistry 检测 squad 中的 archery agent）。
        /// </summary>
        private static bool SquadHasTitanTrait(EnglishSquad squad)
        {
            if (squad == null) return false;

            foreach (Agent agent in squad.agents)
            {
                if (TitanAgentRegistry.IsTitanArcherAgent(agent))
                    return true;
            }
            return false;
        }

        // ────────── DoTargetedAction 重定向（v10） ──────────
        /// <summary>
        /// v10: 使用 TitanAgentRegistry 识别泰坦弓箭手。
        /// 当玩家点击专注技能按钮时，原版 DoTargetedAction 会被拦截，
        /// 转而调用自建 TitanFocusAbility 的逻辑。
        /// </summary>
        private static bool DoTargetedActionRedirectPrefix(ArcheryFocusAbility __instance,
            NavSpot heroNavSpot, NavSpot target)
        {
            if (__instance == null) return true;

            Agent foundAgent = null;

            string[] candidateFields = { "agent", "_agent", "heroAgent", "_heroAgent", "owner", "_owner" };
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
                FieldInfo compField = typeof(ArcheryFocusAbility).GetField("archery",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!ReferenceEquals(compField, null))
                {
                    Archery archery = compField.GetValue(__instance) as Archery;
                    if (archery != null)
                        foundAgent = archery.agent;
                }
            }

            // v10: 使用 TitanAgentRegistry 代替 scale > 1.1f 检测
            if (foundAgent != null && TitanAgentRegistry.IsTitanArcherAgent(foundAgent))
            {
                TitanFocusAbility titanAbility = foundAgent.GetComponent<TitanFocusAbility>();
                if (!ReferenceEquals(titanAbility, null))
                {
                    try
                    {
                        MethodInfo dtAction = typeof(TitanFocusAbility).GetMethod("DoTargetedAction",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (!ReferenceEquals(dtAction, null))
                        {
                            dtAction.Invoke(titanAbility, new object[] { heroNavSpot, target });
                        }
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning("[Titan FocusFix] DoTargetedAction 反射调用失败: " + ex.Message);
                    }

                    Plugin.Logger.LogInfo(string.Format(
                        "[Titan FocusFix] Archer#{0} DoTargetedAction 已重定向到 TitanFocusAbility（v10）",
                        foundAgent.GetInstanceID()));
                    return false;
                }
                else
                {
                    // 缺失自有 TitanFocusAbility，阻止原版流程（兜底安全）
                    return false;
                }
            }
            return true;
        }
    }
}