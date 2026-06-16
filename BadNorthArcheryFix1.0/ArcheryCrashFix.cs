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

		public static void ApplyPatches(Harmony harmony)
		{
			Plugin.Logger.LogInfo("[ArcheryFix] === 开始注册 3 个 Harmony 补丁（仅专注技能防崩溃） ===");

			// [1/3] 放行 MaybeSetup（确保组件初始化）
			harmony.Patch(
				original: AccessTools.Method(typeof(ArcheryFocusComponent), "MaybeSetup"),
				prefix: new HarmonyMethod(typeof(ArcheryCrashFix), nameof(MaybeSetupPrefix))
			);
			Plugin.Logger.LogInfo("[ArcheryFix]   [1/3] MaybeSetup Prefix 已注册");

			// [2/3] 拦截 ShootAt → 挂载自建聚焦
			harmony.Patch(
				original: AccessTools.Method(typeof(ArcheryFocusComponent), "ShootAt"),
				prefix: new HarmonyMethod(typeof(ArcheryCrashFix), nameof(ShootAtPrefix))
			);
			Plugin.Logger.LogInfo("[ArcheryFix]   [2/3] ShootAt Prefix 已注册");

			// [3/3] 兜底抑制 DirectUpdate 异常
			harmony.Patch(
				original: AccessTools.Method(typeof(AgentState), "DirectUpdate"),
				finalizer: new HarmonyMethod(typeof(ArcheryCrashFix), nameof(DirectUpdateFinalizer))
			);
			Plugin.Logger.LogInfo("[ArcheryFix]   [3/3] DirectUpdate Finalizer 已注册");

			Plugin.Logger.LogInfo("[ArcheryFix] === 3 个补丁注册完成 ===");
		}

		// ═══════════════════════════════════════════════════════════════
		// 补丁 1/3：MaybeSetup — 放行，确保 ArcheryFocusComponent 正常初始化
		// ═══════════════════════════════════════════════════════════════
		private static bool MaybeSetupPrefix(ArcheryFocusComponent __instance)
		{
			return true;
		}

		// ═══════════════════════════════════════════════════════════════
		// 补丁 2/3：ShootAt — 拦截专注技能，挂载自建 FocusFixHelper
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
		// 补丁 3/3：DirectUpdate Finalizer — 兜底抑制每帧 ModifyArrow NPE
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