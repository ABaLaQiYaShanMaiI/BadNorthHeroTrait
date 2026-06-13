// Author: ABaLaQiYaShanMaiI
using UnityEngine;

namespace BadNorthAPI
{
    /// <summary>
    /// 统一的 GetOrAddComponent 工具。
    /// 避免重复组件挂载，确保每个 GameObject 上每种组件只存在一个实例。
    /// </summary>
    public static class ComponentHelper
    {
        /// <summary>
        /// 获取指定 GameObject 上的组件。如果不存在则先添加。
        /// 使用 ReferenceEquals 进行 null 检查以兼容 Mono 2.0。
        /// </summary>
        /// <typeparam name="T">组件类型</typeparam>
        /// <param name="go">目标 GameObject</param>
        /// <returns>已存在的或新添加的组件实例</returns>
        public static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            if (ReferenceEquals(go, null))
                return null;

            T comp = go.GetComponent<T>();
            if (ReferenceEquals(comp, null))
            {
                comp = go.AddComponent<T>();
            }
            return comp;
        }

        /// <summary>
        /// 检查 GameObject 上是否已有指定类型的组件。
        /// </summary>
        public static bool HasComponent<T>(GameObject go) where T : Component
        {
            if (ReferenceEquals(go, null))
                return false;
            return !ReferenceEquals(go.GetComponent<T>(), null);
        }
    }
}