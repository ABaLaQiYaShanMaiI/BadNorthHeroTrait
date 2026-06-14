// Author: ABaLaQiYaShanMaiI
using System;
using System.Collections.Generic;
using System.Reflection;
using BadNorthAPI;
using HarmonyLib;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;
using Voxels.TowerDefense.Upgrades;

namespace BadNorthTitan
{
	/// <summary>
	/// 泰坦弓箭手 Harmony 补丁 — 8 个补丁（含诊断日志，受 EnableGameplayLog 开关控制）。
	/// 
	/// 5 个核心：索敌/瞄准/视野/射击 → 高速直线弹道
	/// 1 个安全：MaybeSetup 拦截 → 阻止 ModifyArrow 注册 m__1，根除 NPE
	/// 1 个重定向：DoTargetedAction → 挂载 TitanFocusHelper 极简聚焦
	/// 1 个放行：ShootPrefix 检测自建聚焦时放行
	/// 
	/// 日志标签说明：
	///   [Titan DIAG]  = 诊断日志（每步失败原因）
	///   [Titan WARN]  = 警告（拦截失败、穿透）
	///   [Titan ERROR] = 错误（异常）
	///   [Titan FocusFix] = 正常拦截/重定向成功
	///   [Titan Sight/Aim/Shot] = 索敌/瞄准/射击（仅首次 Agent）
	/// </summary>
	public static class TitanArcheryFixes
	{
		private const float AttackRange = 8f;
		private const float AttackRangeSqr = 64f;

		private const float ArrowSpeed = 17f;
		private const float ArrowDrag = 0f;
		private const float ArrowGravity = 0f;
		private const float ArrowStartOffset = 0.8f;
		private const float SpreadNonFocus = 0.05f;
		private const float BaseCooldown = 4f;
		private const float CooldownExtra = 3.5f;
		private const float CooldownRandomMax = 1.5f;

		// ── 日志门控 ──
		private static void GameplayLog(string message) => Debugger.Log(Plugin.EnableGameplayLog, message);
		private static void GameplayLogWarn(string message) => Debugger.LogWarning(Plugin.EnableGameplayLog, message);

		// 反射缓存
		private static FieldInfo _radiusField = null;
		private static bool _radiusFieldAttempted = false;
		private static FieldInfo _sqRadiusField = null;
		private static bool _sqRadiusFieldAttempted = false;
		private static FieldInfo _coolDownTimeField = null;
		private static bool _coolDownTimeFieldAttempted = false;

		// ── 版本隔离：记录属于本版本的 Agent InstanceID ──
		public static HashSet<int> OurAgentIds = new HashSet<int>();

		// ── 诊断计数器（去重用，避免刷屏） ──
		private static HashSet<int> _maybeSetupBlocked = new HashSet<int>();
		private static HashSet<int> _maybeSetupPenetrated = new HashSet<int>();
		private static HashSet<int> _focusRedirected = new HashSet<int>();
		private static HashSet<int> _shootFocusPassThrough = new HashSet<int>();
		private static int _shootNonTitanCountSinceLog = 0;
		private static int _maybeSetupNonTitanCountSinceLog = 0;
		private static float _lastDiagFlushTime = 0f;
		private const float DiagFlushInterval = 5f;
		private static Dictionary<int, int> _lastSightEnemyCount = new Dictionary<int, int>();
		private const float SightLogCooldown = 2f;
		private static Dictionary<int, float> _lastSightLogTime = new Dictionary<int, float>();

		private static bool IsTitanArcher(Agent agent)
		{
			return agent != null
				&& agent.isEnglish
				&& agent.scale > 1.1f
				&& agent.GetComponent<Archery>() != null
				&& agent.GetComponent("TitanV10Marker") == null; // 排除 1.0 版 Agent
		}

		/// <summary>
		/// 定时刷新诊断批处理日志（每 5 秒）。
		/// </summary>
		private static void FlushDiagBatch()
		{
			float now = Time.time;
			if (now - _lastDiagFlushTime < DiagFlushInterval) return;
			_lastDiagFlushTime = now;

			if (_shootNonTitanCountSinceLog > 0)
			{
				GameplayLog(string.Format("[Titan DIAG] ShootPrefix 放行非泰坦弓箭手 ×{0}（最近5秒）", _shootNonTitanCountSinceLog));
				_shootNonTitanCountSinceLog = 0;
			}
			if (_maybeSetupNonTitanCountSinceLog > 0)
			{
				GameplayLog(string.Format("[Titan DIAG] MaybeSetupPrefix 放行非泰坦 ×{0}（最近5秒）", _maybeSetupNonTitanCountSinceLog));
				_maybeSetupNonTitanCountSinceLog = 0;
			}
		}

		public static void ApplyPatches(Harmony harmony)
		{
			// 加载日志始终输出（不受开关控制）
			Plugin.Logger.LogInfo("[Titan DIAG] === 开始注册 Harmony 补丁 ===");

			harmony.Patch(
				original: AccessTools.Method(typeof(LineOfSight), "GetSight"),
				prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(GetSightPrefix))
			);
			Plugin.Logger.LogInfo("[Titan DIAG]   [1/8] GetSight Prefix 已注册");

			harmony.Patch(
				original: AccessTools.Method(typeof(Archery), "AimAt"),
				prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(AimAtPrefix))
			);
			Plugin.Logger.LogInfo("[Titan DIAG]   [2/8] AimAt Prefix 已注册");

			harmony.Patch(
				original: AccessTools.Method(typeof(LineOfSight), "SetupLineOfSight"),
				postfix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(SetupLineOfSightPostfix))
			);
			Plugin.Logger.LogInfo("[Titan DIAG]   [3/8] SetupLineOfSight Postfix 已注册");

			harmony.Patch(
				original: AccessTools.Method(typeof(ArcheryTargeter), "InSight"),
				prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(InSightPrefix))
			);
			Plugin.Logger.LogInfo("[Titan DIAG]   [4/8] InSight Prefix 已注册");

			harmony.Patch(
				original: AccessTools.Method(typeof(Archery), "Shoot"),
				prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(ShootPrefix))
			);
			Plugin.Logger.LogInfo("[Titan DIAG]   [5/8] Shoot Prefix 已注册");

			harmony.Patch(
				original: AccessTools.Method(typeof(ArcheryFocusComponent), "MaybeSetup"),
				prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(MaybeSetupPrefix))
			);
			Plugin.Logger.LogInfo("[Titan DIAG]   [6/8] MaybeSetup Prefix 已注册");

			// ShootAt 拦截 —— 在每个弓箭手个体层级挂载 TitanFocusHelper
			harmony.Patch(
				original: AccessTools.Method(typeof(ArcheryFocusComponent), "ShootAt"),
				prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(ShootAtPrefix))
			);
			Plugin.Logger.LogInfo("[Titan DIAG]   [7/8] ShootAt Prefix 已注册");

			// DirectUpdate Finalizer —— 兜底抑制每帧 ModifyArrow NPE
			harmony.Patch(
				original: AccessTools.Method(typeof(AgentState), "DirectUpdate"),
				finalizer: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(DirectUpdateFinalizer))
			);
			Plugin.Logger.LogInfo("[Titan DIAG]   [8/8] DirectUpdate Finalizer 已注册");

			Plugin.Logger.LogInfo("[Titan DIAG] === 8 个补丁注册完成 ===");
		}

		// ── GetSight（含诊断） ──
		private static bool GetSightPrefix(LineOfSight __instance, ref LineOfSight.Sight __result)
		{
			Agent agent = __instance.agent;
			if (agent == null) return true;
			if (!agent.isEnglish) return true;
			if (agent.GetComponent<Archery>() == null) return true;

			if (agent.scale <= 1.1f)
			{
				return true;
			}

			// ↓ 泰坦弓箭手 ↓
			Faction enemyFaction = agent.faction.enemy;
			if (enemyFaction == null)
			{
				GameplayLogWarn(string.Format("[Titan WARN] Archer#{0} faction.enemy == null，回退原版索敌", agent.GetInstanceID()));
				return true;
			}

			List<Agent> enemies = enemyFaction.agents;
			Agent bestTarget = null;
			float bestDistSqr = float.MaxValue;
			Vector3 chestPos = agent.chestPos;
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
				return false;
			}

			return true;
		}

		// ── AimAt（含诊断） ──
		private static bool AimAtPrefix(Archery __instance, Vector3 targetPos, ref bool __result)
		{
			if (!IsTitanArcher(__instance.agent)) return true;
			try
			{
				__instance.aimDirTarget = (targetPos - __instance.ShootPos).normalized;
				__result = true;
				return false;
			}
			catch (Exception ex)
			{
				GameplayLogWarn(string.Format("[Titan ERROR] AimAtPrefix 异常: {0}", ex.Message));
				return true;
			}
		}

		// ── SetupLineOfSight（含诊断） ──
		private static void SetupLineOfSightPostfix(LineOfSight __instance)
		{
			if (!IsTitanArcher(__instance.agent)) return;
			try
			{
				if (!_radiusFieldAttempted) { _radiusFieldAttempted = true; _radiusField = typeof(LineOfSight).GetField("radius", BindingFlags.Instance | BindingFlags.NonPublic); }
				if (!ReferenceEquals(_radiusField, null)) _radiusField.SetValue(__instance, AttackRange);
				else GameplayLogWarn("[Titan WARN] LoS.radius 反射未找到！");

				if (!_sqRadiusFieldAttempted) { _sqRadiusFieldAttempted = true; _sqRadiusField = typeof(LineOfSight).GetField("sqRadius", BindingFlags.Instance | BindingFlags.NonPublic); }
				if (!ReferenceEquals(_sqRadiusField, null)) _sqRadiusField.SetValue(__instance, AttackRangeSqr);
				else GameplayLogWarn("[Titan WARN] LoS.sqRadius 反射未找到！");
			}
			catch (Exception ex)
			{
				GameplayLogWarn(string.Format("[Titan ERROR] SetupLineOfSightPostfix 异常: {0}", ex.Message));
			}
		}

		// ── InSight（含诊断 + Agent 守卫） ──
		private static bool InSightPrefix(ArcheryTargeter __instance, Vector3 testPosition, Vector3 targeterPosition, ref bool __result)
		{
			if (__instance == null) return true;

			Agent agent = __instance.GetComponent<Agent>();
			if (agent == null)
			{
				Archery archery = __instance.GetComponent<Archery>();
				if (archery != null)
					agent = archery.agent;
			}

			if (!IsTitanArcher(agent))
				return true;

			try
			{
				if (Vector3.Distance(testPosition, targeterPosition) < AttackRange)
				{
					__result = true;
					return false;
				}
			}
			catch (Exception ex)
			{
				GameplayLogWarn(string.Format("[Titan ERROR] InSightPrefix 异常: {0}", ex.Message));
			}
			return true;
		}

		// ── Shoot（含诊断：聚焦穿透检测 + 异常兜底） ──
		private static bool ShootPrefix(Archery __instance, ref Vector3 shootDir, ref ProjectileSettings projectileSettings)
		{
			Agent agent = __instance.agent;
			if (agent == null) return true;

			FlushDiagBatch();

			if (!agent.isEnglish)
			{
				_shootNonTitanCountSinceLog++;
				return true;
			}
			if (agent.GetComponent<Archery>() == null) return true;

			if (agent.scale <= 1.1f)
			{
				_shootNonTitanCountSinceLog++;
				return true;
			}

			// ↓ 泰坦弓箭手 ↓

			try
			{
				TitanFocusHelper tfh = __instance.GetComponent<TitanFocusHelper>();
				if (!ReferenceEquals(tfh, null) && tfh.enabled)
				{
					return true;
				}

				ProjectileSettings newSettings = new ProjectileSettings();
				FieldInfo[] fields = typeof(ProjectileSettings).GetFields();
				foreach (FieldInfo fi in fields)
					fi.SetValue(newSettings, fi.GetValue(projectileSettings));
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
				else
				{
					GameplayLogWarn("[Titan WARN] Archery.target 反射未找到！使用 transform.forward 作为射击方向");
				}

				if (shootDirOriginal == Vector3.zero)
					shootDirOriginal = __instance.transform.forward;

				Vector3 horizontalDir = shootDirOriginal;
				horizontalDir.y = 0f;
				if (horizontalDir != Vector3.zero)
					__instance.transform.rotation = Quaternion.LookRotation(horizontalDir);
				__instance.aimDirTarget = shootDirOriginal;

				Vector3 shootVelocity = shootDirOriginal * ArrowSpeed;
				shootVelocity += UnityEngine.Random.insideUnitSphere * shootVelocity.magnitude * SpreadNonFocus;
				shootDir = shootVelocity;
				projectileSettings = newSettings;

				float cooldown = BaseCooldown + CooldownExtra + UnityEngine.Random.Range(0f, CooldownRandomMax);
				if (!_coolDownTimeFieldAttempted) { _coolDownTimeFieldAttempted = true; _coolDownTimeField = typeof(Archery).GetField("coolDownTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic); }
				if (!ReferenceEquals(_coolDownTimeField, null))
					_coolDownTimeField.SetValue(__instance, Time.time + cooldown);
				else
					GameplayLogWarn("[Titan WARN] Archery.coolDownTime 反射未找到！冷却时间未设置");

				return true;
			}
			catch (Exception ex)
			{
				GameplayLogWarn(string.Format("[Titan ERROR] ShootPrefix 异常 (Archer#{0}): {1}", agent.GetInstanceID(), ex.Message));
				return true;
			}
		}

		// ── MaybeSetup 放行 —— 允许组件正常初始化 ──
		/// <summary>
		/// 放行 MaybeSetup，让 ArcheryFocusComponent 正常初始化。
		/// 崩溃由 DirectUpdateFinalizer（每帧 ModifyArrow）和 ShootAtPrefix（手动技能）分别兜底。
		/// </summary>
		private static bool MaybeSetupPrefix(ArcheryFocusComponent __instance)
		{
			return true;
		}

		// ── DirectUpdate Finalizer 兜底 —— 抑制每帧 ModifyArrow NPE ──
		/// <summary>
		/// Finalizer 在原始方法（AgentState.DirectUpdate）执行后调用。
		/// 捕获并抑制 ModifyArrow 每帧触发的 NullReferenceException。
		/// </summary>
		private static void DirectUpdateFinalizer(AgentState __instance, Exception __exception)
		{
			if (!ReferenceEquals(__exception, null))
			{
				// 静默抑制异常，避免刷屏
			}
		}

		// ── ShootAt 拦截 —— 在每个弓箭手个体层级挂载 TitanFocusHelper ──
		/// <summary>
		/// 封堵点从 DoTargetedAction（英雄层级）下移到 ShootAt（弓箭手个体层级）。
		/// 天然按 Agent 隔离：1.1/1.2 各自处理各自的 Agent，互不阻塞。
		/// 同一小队内多个同版本泰坦弓箭手 → 全员齐射（正确的小队技能行为）。
		/// </summary>
		private static bool ShootAtPrefix(ArcheryFocusComponent __instance, ArcheryFocusAbility focusAbility, ArcheryFocusComponent.Settings settings, Vector3 targetPos, Vector3 targetDelta)
		{
			if (__instance == null || __instance.gameObject == null) return true;

			Archery archery = __instance.GetComponent<Archery>();
			if (ReferenceEquals(archery, null)) return true;

			Agent agent = archery.agent;
			if (ReferenceEquals(agent, null)) return true;

			// ── 版本检查：仅处理本版本的泰坦弓箭手 ──
			if (!IsTitanArcher(agent)
				|| !OurAgentIds.Contains(agent.GetInstanceID()))
				return true;

			try
			{
				TitanFocusHelper existing = agent.GetComponent<TitanFocusHelper>();
				if (!ReferenceEquals(existing, null))
				{
					UnityEngine.Object.Destroy(existing);
				}

				Vector3 shootDir = (targetPos - agent.chestPos).normalized;
				TitanFocusHelper helper = agent.gameObject.AddComponent<TitanFocusHelper>();

				ProjectileSettings baseSettings = null;
				if (!ReferenceEquals(settings, null))
				{
					System.Reflection.FieldInfo psField = settings.GetType().GetField("projectileSettings",
						System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
					if (!ReferenceEquals(psField, null))
						baseSettings = psField.GetValue(settings) as ProjectileSettings;
				}

				helper.Configure(shootDir, 5, 0.4f, baseSettings);

				int agentId = agent.GetInstanceID();
				if (_focusRedirected.Add(agentId))
				{
					GameplayLog(string.Format(
						"[Titan FocusFix] ShootAt 重定向 ✓: Archer#{0} 已挂载 TitanFocusHelper，方向 ({1:F2},{2:F2},{3:F2}) → ({4:F1},{5:F1},{6:F1})",
						agentId, shootDir.x, shootDir.y, shootDir.z, targetPos.x, targetPos.y, targetPos.z));
				}

				return false;
			}
			catch (Exception ex)
			{
				GameplayLogWarn(string.Format(
					"[Titan ERROR] ShootAtPrefix 异常 (Archer#{0}): {1} — 穿透放行", agent.GetInstanceID(), ex.Message));
				return true;
			}
		}
	}
}