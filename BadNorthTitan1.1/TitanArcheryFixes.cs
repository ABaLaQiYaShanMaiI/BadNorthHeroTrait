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

			// DoTargetedAction 重定向
			MethodInfo dtMethod = AccessTools.Method(typeof(ArcheryFocusAbility), "DoTargetedAction");
			string methodName = "DoTargetedAction";
			if (ReferenceEquals(dtMethod, null))
			{
				dtMethod = AccessTools.Method(typeof(ArcheryFocusAbility), "DoTargetedAction_Implementation");
				methodName = "DoTargetedAction_Implementation";
			}
			if (!ReferenceEquals(dtMethod, null))
			{
				harmony.Patch(
					original: dtMethod,
					prefix: new HarmonyMethod(typeof(TitanArcheryFixes), nameof(DoTargetedActionPrefix))
				);
				Plugin.Logger.LogInfo(string.Format("[Titan DIAG]   [7/8] {0} Prefix 已注册", methodName));
			}
			else
			{
				Plugin.Logger.LogWarning("[Titan WARN] ⚠ DoTargetedAction 方法未找到！原版专注射击将正常执行（可能崩溃）");
			}

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

		// ── MaybeSetup 拦截 + 穿透告警 ──
		private static bool MaybeSetupPrefix(ArcheryFocusComponent __instance)
		{
			if (__instance == null || __instance.gameObject == null)
			{
				GameplayLogWarn("[Titan WARN] MaybeSetupPrefix: __instance 或 gameObject 为 null！放行");
				return true;
			}

			Archery archery = __instance.GetComponent<Archery>();
			if (ReferenceEquals(archery, null))
			{
				GameplayLog(string.Format("[Titan DIAG] MaybeSetupPrefix: ArcheryFocusComponent 位于非 Archery GameObject '{0}' — 放行", __instance.gameObject.name));
				return true;
			}

			Agent agent = archery.agent;
			if (agent == null)
			{
				GameplayLogWarn("[Titan WARN] MaybeSetupPrefix: archery.agent 为 null！放行");
				return true;
			}

			FlushDiagBatch();

			if (!agent.isEnglish || agent.scale <= 1.1f)
			{
				_maybeSetupNonTitanCountSinceLog++;
				return true;
			}

			// ↓ 泰坦弓箭手 ↓

			int agentId = agent.GetInstanceID();
			bool alreadyBlocked = !_maybeSetupBlocked.Add(agentId);

			try
			{
				if (!alreadyBlocked)
				{
					GameplayLog(string.Format(
						"[Titan FocusFix] Archer#{0} MaybeSetup 已拦截 ✓（m__1 被阻止注册到 AgentState.OnUpdate）",
						agentId));
				}
				return false;
			}
			catch (Exception ex)
			{
				GameplayLogWarn(string.Format(
					"[Titan ERROR] MaybeSetupPrefix 返回 false 时异常 (Archer#{0}): {1} — 穿透放行！",
					agentId, ex.Message));
				_maybeSetupPenetrated.Add(agentId);
				return true;
			}
		}

		// ── DoTargetedAction 重定向 + 多路 Agent 探测 ──
		/// <summary>
		/// 从 ArcheryFocusAbility 中探测 Agent（多路回退策略）。
		/// 
		/// 路径优先级：
		///   1. NavSpotTargetableAbility 的 hero/_hero/heroObj → hero.agent/_agent
		///   2. NavSpotTargetableAbility 的 agent/_agent/owner
		///   3. __instance.GetComponent[InParent]<Agent>()
		///   4. heroNavSpot.squad → 检查每个 Agent 是否为泰坦弓箭手
		///   5. 全局扫描所有 EnglishSquad 中的泰坦弓箭手 Agent
		/// </summary>
		private static Agent FindTitanAgentFromAbility(ArcheryFocusAbility ability, NavSpot heroNavSpot)
		{
			string[] heroFieldNames = { "hero", "_hero", "heroObj", "_heroObj", "heroAgent", "_heroAgent" };
			string[] agentFieldNames = { "agent", "_agent", "owner", "_owner", "heroAgent", "_heroAgent" };
			string[] heroAgentFieldNames = { "agent", "_agent", "heroAgent", "_heroAgent" };

			Type abilityType = typeof(NavSpotTargetableAbility);
			Type archeryFocusType = typeof(ArcheryFocusAbility);

			foreach (string hfName in heroFieldNames)
			{
				FieldInfo hf = abilityType.GetField(hfName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (ReferenceEquals(hf, null))
					hf = archeryFocusType.GetField(hfName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (ReferenceEquals(hf, null)) continue;

				object hero = hf.GetValue(ability);
				if (hero == null) continue;

				Type heroType = hero.GetType();
				foreach (string agName in heroAgentFieldNames)
				{
					FieldInfo ag = heroType.GetField(agName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (ReferenceEquals(ag, null)) continue;
					Agent agent = ag.GetValue(hero) as Agent;
					if (!ReferenceEquals(agent, null) && IsTitanArcher(agent))
					{
						GameplayLog(string.Format("[Titan DIAG] Agent 探测成功: hero.{0} → Archer#{1}", hfName, agent.GetInstanceID()));
						return agent;
					}
				}
			}

			foreach (string agName in agentFieldNames)
			{
				FieldInfo ag = abilityType.GetField(agName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (ReferenceEquals(ag, null))
					ag = archeryFocusType.GetField(agName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
				if (ReferenceEquals(ag, null)) continue;

				Agent agent = ag.GetValue(ability) as Agent;
				if (!ReferenceEquals(agent, null) && IsTitanArcher(agent))
				{
					GameplayLog(string.Format("[Titan DIAG] Agent 探测成功: ability.{0} → Archer#{1}", agName, agent.GetInstanceID()));
					return agent;
				}
			}

			Agent compAgent = ability.GetComponent<Agent>();
			if (!ReferenceEquals(compAgent, null) && IsTitanArcher(compAgent))
			{
				GameplayLog(string.Format("[Titan DIAG] Agent 探测成功: GetComponent<Agent>() → Archer#{0}", compAgent.GetInstanceID()));
				return compAgent;
			}
			compAgent = ability.GetComponentInParent<Agent>();
			if (!ReferenceEquals(compAgent, null) && IsTitanArcher(compAgent))
			{
				GameplayLog(string.Format("[Titan DIAG] Agent 探测成功: GetComponentInParent<Agent>() → Archer#{0}", compAgent.GetInstanceID()));
				return compAgent;
			}

			if (!ReferenceEquals(heroNavSpot, null))
			{
				EnglishSquad squadFromSpot = null;
				FieldInfo squadField = typeof(NavSpot).GetField("squad", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				if (!ReferenceEquals(squadField, null))
					squadFromSpot = squadField.GetValue(heroNavSpot) as EnglishSquad;
				if (!ReferenceEquals(squadFromSpot, null))
				{
					foreach (Agent a in squadFromSpot.agents)
					{
						if (IsTitanArcher(a))
						{
							GameplayLog(string.Format("[Titan DIAG] Agent 探测成功: heroNavSpot(reflect).squad → Archer#{0}", a.GetInstanceID()));
							return a;
						}
					}
				}
			}

			EnglishSquad[] allSquads = Resources.FindObjectsOfTypeAll<EnglishSquad>();
			foreach (EnglishSquad sq in allSquads)
			{
				if (ReferenceEquals(sq, null)) continue;
				foreach (Agent a in sq.agents)
				{
					if (IsTitanArcher(a))
					{
						GameplayLog(string.Format("[Titan DIAG] Agent 探测成功: 全局扫描 → Archer#{0} (squad={1})", a.GetInstanceID(), sq.name));
						return a;
					}
				}
			}

			GameplayLogWarn("[Titan WARN] ⚠ 所有 5 条 Agent 探测路径均失败！");
			return null;
		}

		private static bool DoTargetedActionPrefix(ArcheryFocusAbility __instance, NavSpot heroNavSpot, NavSpot target)
		{
			if (__instance == null)
			{
				GameplayLogWarn("[Titan WARN] DoTargetedActionPrefix: __instance == null！穿透放行");
				return true;
			}

			try
			{
				Vector3 targetPos = Vector3.zero;
				if (!ReferenceEquals(target, null))
					targetPos = target.transform.position;
				else if (!ReferenceEquals(heroNavSpot, null))
					targetPos = heroNavSpot.transform.position;
				if (targetPos == Vector3.zero)
				{
					GameplayLogWarn("[Titan WARN] DoTargetedActionPrefix: targetPos == zero！穿透放行");
					return true;
				}

				// ── 直接从 heroNavSpot 获取小队，避免全局扫描跨小队串台 ──
				EnglishSquad squad = null;
				if (!ReferenceEquals(heroNavSpot, null))
				{
					FieldInfo squadField = typeof(NavSpot).GetField("squad", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (!ReferenceEquals(squadField, null))
						squad = squadField.GetValue(heroNavSpot) as EnglishSquad;
				}
				if (ReferenceEquals(squad, null))
				{
					// ── 回退：全局扫描所有 EnglishSquad 中的泰坦弓箭手 ──
					EnglishSquad[] allSquads = Resources.FindObjectsOfTypeAll<EnglishSquad>();
					foreach (EnglishSquad sq in allSquads)
					{
						if (ReferenceEquals(sq, null)) continue;
						foreach (Agent a in sq.agents)
						{
							if (ReferenceEquals(a, null)) continue;
							if (IsTitanArcher(a) && TitanArcheryFixes.OurAgentIds.Contains(a.GetInstanceID())
								&& a.GetComponent("TitanV12Marker") == null)
							{
								squad = sq;
								break;
							}
						}
						if (!ReferenceEquals(squad, null)) break;
					}
				}
				if (ReferenceEquals(squad, null))
				{
					GameplayLogWarn("[Titan WARN] DoTargetedActionPrefix: 无法找到任何泰坦弓箭手小队！穿透放行");
					return true;
				}

				// 检查该小队是否有本版本的泰坦弓箭手（且不含 TitanV12Marker）
				bool hasOurArcher = false;
				foreach (Agent squadAgent in squad.agents)
				{
					if (ReferenceEquals(squadAgent, null)) continue;
					if (IsTitanArcher(squadAgent) && TitanArcheryFixes.OurAgentIds.Contains(squadAgent.GetInstanceID())
						&& squadAgent.GetComponent("TitanV12Marker") == null)
					{
						hasOurArcher = true;
						break;
					}
				}
				if (!hasOurArcher)
				{
					GameplayLog(string.Format("[Titan DIAG] DoTargetedActionPrefix: 小队 {0} 没有 1.1 版泰坦弓箭手 — 放行", squad.name));
					return true;
				}

			int cleaned = 0;
				foreach (Agent squadAgent in squad.agents)
				{
					if (ReferenceEquals(squadAgent, null)) continue;
					if (!IsTitanArcher(squadAgent)) continue;
					if (!TitanArcheryFixes.OurAgentIds.Contains(squadAgent.GetInstanceID())) continue;
					TitanFocusHelper existing = squadAgent.GetComponent<TitanFocusHelper>();
					if (!ReferenceEquals(existing, null))
					{
						UnityEngine.Object.Destroy(existing);
						cleaned++;
					}
				}
				Vector3 squadCenter = Vector3.zero;
				int archerCountForCenter = 0;
				foreach (Agent squadAgent in squad.agents)
				{
					if (ReferenceEquals(squadAgent, null)) continue;
					if (!IsTitanArcher(squadAgent)) continue;
					if (!TitanArcheryFixes.OurAgentIds.Contains(squadAgent.GetInstanceID())) continue;
					squadCenter += squadAgent.transform.position;
					archerCountForCenter++;
				}
				if (archerCountForCenter > 0)
					squadCenter /= archerCountForCenter;
				else
					squadCenter = squad.agents[0].transform.position;

				Vector3 unifiedDir = (targetPos - squadCenter).normalized;

				ProjectileSettings baseSettings = null;
				foreach (Agent squadAgent in squad.agents)
				{
					if (ReferenceEquals(squadAgent, null)) continue;
					if (!IsTitanArcher(squadAgent)) continue;
					Archery archeryComp = squadAgent.GetComponent<Archery>();
					if (!ReferenceEquals(archeryComp, null))
					{
						System.Reflection.FieldInfo settingsField = typeof(Archery).GetField("_archerySettings",
							System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
						if (!ReferenceEquals(settingsField, null))
						{
							object settingsArray = settingsField.GetValue(archeryComp);
							if (settingsArray is System.Array arr && arr.Length > 0)
							{
								object firstSetting = arr.GetValue(0);
								if (firstSetting != null)
								{
									System.Reflection.FieldInfo psField = firstSetting.GetType().GetField("projectileSettings",
										System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
									if (!ReferenceEquals(psField, null))
										baseSettings = psField.GetValue(firstSetting) as ProjectileSettings;
								}
							}
						}
						break;
					}
				}

				int count = 0;
				foreach (Agent squadAgent in squad.agents)
				{
					if (ReferenceEquals(squadAgent, null)) continue;
					if (!IsTitanArcher(squadAgent)) continue;
					if (!TitanArcheryFixes.OurAgentIds.Contains(squadAgent.GetInstanceID())) continue;

					TitanFocusHelper helper = squadAgent.gameObject.AddComponent<TitanFocusHelper>();
					helper.Configure(unifiedDir, 5, 0.4f, baseSettings);
					count++;
				}

				int logAgentId = 0;
				foreach (Agent a in squad.agents)
				{
					if (IsTitanArcher(a) && OurAgentIds.Contains(a.GetInstanceID()))
					{
						logAgentId = a.GetInstanceID();
						break;
					}
				}
				if (_focusRedirected.Add(logAgentId))
				{
					GameplayLog(string.Format(
						"[Titan FocusFix] DoTargetedAction 重定向 ✓: {0} 个 Archer 已挂载 TitanFocusHelper，方向 ({1:F2},{2:F2},{3:F2}) → ({4:F1},{5:F1},{6:F1})",
						count, unifiedDir.x, unifiedDir.y, unifiedDir.z, targetPos.x, targetPos.y, targetPos.z));
				}
				else
				{
					GameplayLog(string.Format(
						"[Titan FocusFix] DoTargetedAction 重定向（再次）: {0} 个 Archer，方向 ({1:F2},{2:F2},{3:F2})",
						count, unifiedDir.x, unifiedDir.y, unifiedDir.z));
				}

				return false;
			}
			catch (Exception ex)
			{
				GameplayLogWarn(string.Format(
					"[Titan ERROR] DoTargetedActionPrefix 异常: {0} — 穿透放行（原版可能崩溃）", ex.Message));
				return true;
			}
		}
	}
}