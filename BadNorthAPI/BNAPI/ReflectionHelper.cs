// Author: ABaLaQiYaShanMaiI
using System;
using System.Reflection;

namespace BadNorthAPI
{
    /// <summary>
    /// 统一反射字段读写包装。
    /// 提供类型安全的 GetField/SetField 方法，减少各 Trait 重复反射代码。
    /// </summary>
    public static class ReflectionHelper
    {
        /// <summary>
        /// 获取实例上的字段值（自动搜索 Public + NonPublic + Instance）。
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="instance">实例对象</param>
        /// <param name="fieldName">字段名称</param>
        /// <param name="callerTag">调用方标识（用于日志）</param>
        /// <returns>字段值，如果未找到则返回 null</returns>
        public static object GetFieldValue<T>(T instance, string fieldName, string callerTag = null) where T : class
        {
            if (ReferenceEquals(instance, null))
                return null;

            FieldInfo field = typeof(T).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (ReferenceEquals(field, null))
            {
                string tag = string.IsNullOrEmpty(callerTag) ? "[ReflectionHelper]" : "[" + callerTag + "]";
                Plugin.Logger.LogWarning(string.Format("{0} 反射字段 {1} 未找到", tag, fieldName));
                return null;
            }
            return field.GetValue(instance);
        }

        /// <summary>
        /// 设置实例上的字段值（自动搜索 Public + NonPublic + Instance）。
        /// </summary>
        /// <typeparam name="T">目标类型</typeparam>
        /// <param name="instance">实例对象</param>
        /// <param name="fieldName">字段名称</param>
        /// <param name="value">要设置的值</param>
        /// <param name="callerTag">调用方标识（用于日志）</param>
        /// <returns>是否成功设置</returns>
        public static bool SetFieldValue<T>(T instance, string fieldName, object value, string callerTag = null) where T : class
        {
            if (ReferenceEquals(instance, null))
                return false;

            FieldInfo field = typeof(T).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (ReferenceEquals(field, null))
            {
                string tag = string.IsNullOrEmpty(callerTag) ? "[ReflectionHelper]" : "[" + callerTag + "]";
                Plugin.Logger.LogWarning(string.Format("{0} 反射字段 {1} 未找到，无法设置", tag, fieldName));
                return false;
            }
            field.SetValue(instance, value);
            return true;
        }

        /// <summary>
        /// 安全地从 source 复制字段值到 target（同一类型）。
        /// </summary>
        public static void CopyField<T>(T source, T target, string fieldName, string callerTag = null) where T : class
        {
            object value = GetFieldValue(source, fieldName, callerTag);
            if (!ReferenceEquals(value, null))
            {
                SetFieldValue(target, fieldName, value, callerTag);
            }
        }
    }
}