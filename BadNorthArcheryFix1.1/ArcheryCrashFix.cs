using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;
using Voxels.TowerDefense.Upgrades;

namespace BadNorthArcheryFix
{
	public static class ArcheryCrashFix
	{
		private const float GiantScaleThreshold = 1.0f;

		private static void Log(string msg)
		{
			if (Plugin.EnableGameplayLog)
				Plugin.Logger.LogInfo(msg);
		}

		private static void LogWarn(string msg)
		{
			if (Plugin.EnableGameplayLog)
				Plugin.Logger.LogWarning(msg);
		}

		private static bool IsGiantArcher(Agent agent)
		{
			return agent != null
				&& agent.isEnglish
				&& agent.scale > GiantScaleThreshold
				&& agent.GetComponent<Archery>() != null;
		}

		// ── 反射缓存 ──
		private static FieldInfo _targetField = null;
		private static bool _targetFieldAttempted = false;

		public static void ApplyPatches(Harmony harmony)
		{
			Plugin.Logger.LogInfo("[ArcheryFix] === 开始注册 4 个 Harmony 补丁 ===");

			// [1/4] Shoot Prefix → 修复巨人弓箭手每发弹丸的碰撞掩码
			harmony.Patch(
				original: AccessTools.Method(typeof(Archery), "Shoot"),
				prefix: new HarmonyMethod(typeof(ArcheryCrashFix), nameof(ShootPrefix))
			);
			Plugin.Logger.LogInfo("[ArcheryFix]   [1/4] Shoot Prefix 已注册（碰撞掩码修复）");

			// [2/4] 放行 MaybeSetup（确保组件初始化）
			harmony.Patch(
				original: AccessTools.Method(typeof(ArcheryFocusComponent), "MaybeSetup"),
				prefix: new HarmonyMethod(typeof(ArcheryCrashFix), nameof(MaybeSetupPrefix))
			);
			Plugin.Logger.LogInfo("[ArcheryFix]   [2/4] MaybeSetup Prefix 已注册");

			// [3/4] 拦截 ShootAt → 挂载自建聚焦（FocusFixHelper 内部自带掩码修复）
			harmony.Patch(
				original: AccessTools.Method(typeof(ArcheryFocusComponent), "ShootAt"),
				prefix: new HarmonyMethod(typeof(ArcheryCrashFix), nameof(ShootAtPrefix))
			);
			Plugin.Logger.LogInfo("[ArcheryFix]   [3/4] ShootAt Prefix 已注册");

			// [4/4] 兜底抑制 DirectUpdate 异常
			harmony.Patch(
				original: AccessTools.Method(typeof(AgentState), "DirectUpdate"),
				finalizer: new HarmonyMethod(typeof(ArcheryCrashFix), nameof(DirectUpdateFinalizer))
			);
			Plugin.Logger.LogInfo("[ArcheryFix]   [4/4] DirectUpdate Finalizer 已注册");

			Plugin.Logger.LogInfo("[ArcheryFix] === 4 个补丁注册完成 ===");
		}

		// ═══════════════════════════════════════════════════════════════
		// 补丁 1/4：Shoot Prefix — 修复巨人弓箭手普通射击时的碰撞掩码
		//    解决问题：确保 target.mask1 包含 arrowLow（Voxels+Houses+ArrowBlocker）
		//    防止弹丸穿透地形直接落水
		// ═══════════════════════════════════════════════════════════════
		private static bool ShootPrefix(
			Archery __instance,
			ref Vector3 shootDir,
			ref ProjectileSettings projectileSettings)
		{
			Agent agent = __instance.agent;
			if (agent == null) return true;
			if (!IsGiantArcher(agent)) return true;

			try
			{
				// 确保 Archery.target 有正确的 mask0/mask1
				// Shootable.Shoot 内部会从 target.mask0/mask1 读取碰撞掩码
				if (!_targetFieldAttempted)
				{
					_targetFieldAttempted = true;
					_targetField = typeof(Archery).GetField("target",
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
				}

				if (!ReferenceEquals(_targetField, null))
				{
					object targetObj = _targetField.GetValue(__instance);
					if (targetObj != null)
					{
						LineOfSight.Sight sight = (LineOfSight.Sight)targetObj;
						// 强制 mask0 包含地形层（上升段也检测地形，防止从低处射击穿过高地）
						sight.mask0 |= LayerMaster.arrowLow;
						// 强制 mask1 包含地形层（下降段检测地形 — 这是最关键的修复）
						sight.mask1 |= LayerMaster.arrowLow;
						_targetField.SetValue(__instance, sight);
					}
					else
					{
						// target 为空 → 构造一个最小可行的 Sight
						LineOfSight.Sight fallback = default(LineOfSight.Sight);
						fallback.mask0 = LayerMaster.arrowLow;
						fallback.mask1 = LayerMaster.arrowLow; // 确保下降段能检测 Voxels
						fallback.agent = null;
						fallback.score = 0f;
						_targetField.SetValue(__instance, fallback);
					}
				}

				return true;
			}
			catch (Exception ex)
			{
				LogWarn(string.Format("[ArcheryFix] ShootPrefix 异常: {0} — 穿透放行", ex.Message));
				return true;
			}
		}

		// ═══════════════════════════════════════════════════════════════
		// 补丁 2/4：MaybeSetup — 放行，确保 ArcheryFocusComponent 正常初始化
		// ═══════════════════════════════════════════════════════════════
		private static bool MaybeSetupPrefix(ArcheryFocusComponent __instance)
		{
			return true;
		}

		// ═══════════════════════════════════════════════════════════════
		// 补丁 3/4：ShootAt — 拦截专注技能，挂载自建 FocusFixHelper
		// ═══════════════════════════════════════════════════════════════
		private static bool ShootAtPrefix(
			ArcheryFocusComponent __instance,
			ArcheryFocusAbility focusAbility,
			ArcheryFocusComponent.Settings settings,
			Vector3 targetPos,
			Vector3 targetDelta)
		{
			if (__instance == null || __instance.gameObject == null) return true;

			Archery archery = __instance.GetComponent<Archery>();
			if (ReferenceEquals(archery, null)) return true;

			Agent agent = archery.agent;
			if (!IsGiantArcher(agent)) return true;

			try
			{
				FocusFixHelper existing = agent.GetComponent<FocusFixHelper>();
				if (!ReferenceEquals(existing, null))
					UnityEngine.Object.Destroy(existing);

				Vector3 shootDir = (targetPos - agent.chestPos).normalized;

				ProjectileSettings baseSettings = null;
				if (!ReferenceEquals(settings, null))
				{
					FieldInfo psField = settings.GetType().GetField("projectileSettings",
						BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (!ReferenceEquals(psField, null))
						baseSettings = psField.GetValue(settings) as ProjectileSettings;
				}

				FocusFixHelper helper = agent.gameObject.AddComponent<FocusFixHelper>();
				helper.Configure(shootDir, 8, 0.2f, baseSettings);

				Log(string.Format("[ArcheryFix] ShootAt 已重定向: Agent#{0}", agent.GetInstanceID()));

				return false;
			}
			catch (Exception ex)
			{
				LogWarn(string.Format("[ArcheryFix] ShootAtPrefix 异常 (Agent#{0}): {1} — 穿透放行",
					agent.GetInstanceID(), ex.Message));
				return true;
			}
		}

		// ═══════════════════════════════════════════════════════════════
		// 补丁 4/4：DirectUpdate Finalizer — 兜底抑制每帧 ModifyArrow NPE
		// ═══════════════════════════════════════════════════════════════
		private static void DirectUpdateFinalizer(AgentState __instance, Exception __exception)
		{
			if (!ReferenceEquals(__exception, null))
			{
				// 静默抑制异常
			}
		}
	}
}