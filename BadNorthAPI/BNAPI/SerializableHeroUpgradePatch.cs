using System;
using System.Reflection;
using HarmonyLib;
using Voxels.TowerDefense;

namespace BadNorthAPI
{
    /// <summary>
    /// Harmony 补丁：修复 SerializableHeroUpgrade.PreSave 的空引用崩溃
    /// 
    /// 问题：BNAPI 通过反射创建的 SerializableHeroUpgrade 对象，
    /// 在游戏存档序列化时 PreSave 回调访问了未初始化的字段导致 NRE。
    /// 
    /// 解决：使用 Harmony finalizer 捕获并抑制 PreSave 中的异常。
    /// </summary>
    internal static class SerializableHeroUpgradePatch
    {
        private static bool _applied = false;
        private static bool _errorLogged = false;
        private static Harmony _harmonyInstance;

        /// <summary>
        /// 应用 SerializableHeroUpgrade.PreSave 的保护性补丁
        /// 创建独立的 Harmony 实例，不依赖外部传入
        /// </summary>
        public static void Apply()
        {
            if (_applied) return;

            try
            {
                _harmonyInstance = new Harmony("ABaLaQiYaShanMaiI.bnapi.modular.presavefix");

                MethodInfo preSaveMethod = typeof(SerializableHeroUpgrade).GetMethod(
                    "PreSave",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );

                if (!ReferenceEquals(preSaveMethod, null))
                {
                    _harmonyInstance.Patch(
                        original: preSaveMethod,
                        prefix: new HarmonyMethod(typeof(SerializableHeroUpgradePatch), nameof(PreSavePrefix)),
                        finalizer: new HarmonyMethod(typeof(SerializableHeroUpgradePatch), nameof(PreSaveFinalizer))
                    );
                    _applied = true;
                    Plugin.Logger.LogInfo("[BadNorthAPI] SerializableHeroUpgrade.PreSave protection patch applied.");
                }
                else
                {
                    Plugin.Logger.LogWarning("[BadNorthAPI] SerializableHeroUpgrade.PreSave method not found, protection patch skipped.");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning(string.Format("[BadNorthAPI] Failed to apply PreSave protection patch: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Prefix: 跳过定义字段为 null 的实例，阻止 PreSave 执行
        /// </summary>
        private static bool PreSavePrefix(SerializableHeroUpgrade __instance)
        {
            if (ReferenceEquals(__instance, null))
                return false;

            // 反射获取 definition 属性（game 使用 property 而非 field）
            try
            {
                PropertyInfo defProp = typeof(SerializableHeroUpgrade).GetProperty(
                    "definition",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                );

                if (!ReferenceEquals(defProp, null))
                {
                    object def = defProp.GetValue(__instance, null);
                    if (ReferenceEquals(def, null))
                    {
                        // definition 为 null 时 PreSave 必定崩溃，安全跳过
                        return false;
                    }
                }
            }
            catch
            {
                // 反射失败，放行让原始方法执行
            }

            return true;
        }

        /// <summary>
        /// Finalizer: 捕获并抑制 PreSave 中的任何异常
        /// 返回 null 通知 Harmony 异常已被处理
        /// </summary>
        private static Exception PreSaveFinalizer(Exception __exception)
        {
            if (__exception != null)
            {
                if (!_errorLogged)
                {
                    _errorLogged = true;
                    Plugin.Logger.LogWarning(string.Format(
                        "[BadNorthAPI] SerializableHeroUpgrade.PreSave exception suppressed (此错误仅显示一次): {0}",
                        __exception.Message
                    ));
                }
                return null;
            }
            return null;
        }
    }
}