using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BadNorthAPI;
using HarmonyLib;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthTitan
{
    [BepInDependency("nacu.bnapi.modular", BepInDependency.DependencyFlags.HardDependency)]
    [BepInPlugin("nacu.badnorthtitan1.1", "Bad North - Titan Trait 1.1", "1.1")]
    public class Plugin : BaseUnityPlugin
    {
        public static ManualLogSource Logger;

        private float _cleanupTimer;

        public void OnEnable()
        {
            Logger = base.Logger;
            string modPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + Path.DirectorySeparatorChar;

            // 1. Load custom sprite
            CustomSprites.AddCustomSprite(modPath, "trait_titan");

            // 2. Register trait
            CustomTraits.RegisterTrait(
                ScriptableObject.CreateInstance<Titan>(),
                Titan.Titan_ID,
                true  // alwaysUnlocked
            );

            // 3. Add localization
            CustomText.CustomTermsAdded += AddCustomTerms;

            // 4. Apply Titan archery fixes (sight/aim patches for giant archers)
            Harmony harmony = new Harmony("nacu.badnorthtitan1.1.archeryfix");
            TitanArcheryFixes.ApplyPatches(harmony);

            // 5. 立即执行一次全局清理；后续由 Update() 每 2 秒驱动
            DoGlobalCleanup();
            _cleanupTimer = 0f;

            Logger.LogInfo(string.Format("======== BadNorthTitan 1.1 已就绪，特性ID: {0} ========", Titan.Titan_ID));
        }

        void Update()
        {
            _cleanupTimer += Time.deltaTime;
            if (_cleanupTimer >= 2f)
            {
                _cleanupTimer = 0f;
                DoGlobalCleanup();
            }
        }

        private void AddCustomTerms()
        {
            CustomText.AddCustomTerm("YYYYY/TRAIT/TITAN/NAME", "泰坦");
            CustomText.AddCustomTerm("YYYYY/TRAIT/TITAN/DESCSHORT", "真正的巨人之力，盾弓皆可，升级后起效");
            CustomText.AddCustomTerm("YYYYY/TRAIT/TITAN/DESC", "步兵与弓箭手皆可获得泰坦之力。\n大幅提升伤害、护甲与抗性，但小队人数减半。\n需要小队达到1级后解锁。");
        }

        /// <summary>
        /// 强制清理入口（public static），可由 TitanFocusAbility.DoTargetedAction 直接调用。
        /// </summary>
        public static void ForceCleanupNow()
        {
            DoGlobalCleanup();
        }

        /// <summary>
        /// 全局委托清理 — 由 Update() 每 2 秒驱动，也可通过 ForceCleanupNow() 主动触发。
        /// 扫描所有 Agent / Squad / Ability 的 AgentState.OnUpdate 委托，
        /// 剔除 Target 为 null / 已销毁 / 类型名含 ArcheryFocus 的死亡回调，
        /// 根除 ArcheryFocusAbility.<DoSquadSpawnAction_Implementation>m__0 NPE 刷屏。
        /// </summary>
        public static void DoGlobalCleanup()
        {
            int cleaned = 0;
            var visitedStates = new HashSet<object>();

            string[] stateFieldCandidates = { "active", "_active", "agentState", "_agentState",
                "focusState", "_focusState", "aiming", "_aiming", "state", "_state",
                "heroState", "_heroState", "agentStateRoot", "_agentStateRoot" };

            // ── 扫描所有 Agent ──
            var agents = Resources.FindObjectsOfTypeAll<Agent>();
            foreach (var agent in agents)
            {
                if (ReferenceEquals(agent, null)) continue;
                Component[] components = agent.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (ReferenceEquals(comp, null)) continue;
                    foreach (string fieldName in stateFieldCandidates)
                    {
                        var field = comp.GetType().GetField(fieldName,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (ReferenceEquals(field, null)) continue;
                        object stateObj;
                        try { stateObj = field.GetValue(comp); }
                        catch { continue; }
                        if (ReferenceEquals(stateObj, null) || !visitedStates.Add(stateObj)) continue;
                        CleanAgentStateOnUpdate(stateObj, ref cleaned);
                    }
                }
            }

            // ── 扫描所有 Squad ──
            var squads = Resources.FindObjectsOfTypeAll<EnglishSquad>();
            foreach (var squad in squads)
            {
                if (ReferenceEquals(squad, null)) continue;
                Component[] components = squad.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (ReferenceEquals(comp, null)) continue;
                    foreach (string fieldName in stateFieldCandidates)
                    {
                        var field = comp.GetType().GetField(fieldName,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (ReferenceEquals(field, null)) continue;
                        object stateObj;
                        try { stateObj = field.GetValue(comp); }
                        catch { continue; }
                        if (ReferenceEquals(stateObj, null) || !visitedStates.Add(stateObj)) continue;
                        CleanAgentStateOnUpdate(stateObj, ref cleaned);
                    }
                }
            }

            // ── 扫描所有含 Ability/Focus 的 MonoBehaviour（覆盖 ArcheryFocusAbility 等） ──
            var allBehaviours = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
            foreach (var mb in allBehaviours)
            {
                if (ReferenceEquals(mb, null)) continue;
                string typeName = mb.GetType().Name;
                if (!typeName.Contains("Ability") && !typeName.Contains("Focus"))
                    continue;

                foreach (string fieldName in stateFieldCandidates)
                {
                    var field = mb.GetType().GetField(fieldName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (ReferenceEquals(field, null)) continue;
                    object stateObj;
                    try { stateObj = field.GetValue(mb); }
                    catch { continue; }
                    if (ReferenceEquals(stateObj, null) || !visitedStates.Add(stateObj)) continue;
                    CleanAgentStateOnUpdate(stateObj, ref cleaned);
                }
            }

            if (cleaned > 0 && Logger != null)
            {
                Logger.LogInfo(string.Format(
                    "[Titan] 全局清理：本轮移除 {0} 个死亡委托", cleaned));
            }
        }

        /// <summary>
        /// 清理单个 AgentState 上的 OnUpdate 死亡委托。
        /// 
        /// 四重检测：
        /// 1. Delegate.Target == null（标准 .NET null）
        /// 2. Delegate.Target 是已 Destroy 的 UnityEngine.Object（假 null）
        /// 3. Delegate.Target 的类型名包含 "ArcheryFocusAbility" 或 "ArcheryFocusComponent"
        ///    （编译器生成的闭包 DisplayClass FullName，如 +<>c__DisplayClassX）
        /// 4. 诊断日志：首次遇到非 null 且未知类型的委托时打印其 FullName（帮助排查遗漏）
        /// </summary>
        public static void CleanAgentStateOnUpdate(object stateObj, ref int cleanedCount)
        {
            if (ReferenceEquals(stateObj, null)) return;
            Type stateType = stateObj.GetType();

            string[] onUpdateCandidates = { "OnUpdate", "onUpdate", "_onUpdate", "updateDelegate" };
            FieldInfo onUpdateField = null;
            foreach (string name in onUpdateCandidates)
            {
                onUpdateField = stateType.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!ReferenceEquals(onUpdateField, null)) break;
            }
            if (ReferenceEquals(onUpdateField, null)) return;

            Delegate del;
            try { del = onUpdateField.GetValue(stateObj) as Delegate; }
            catch { return; }
            if (ReferenceEquals(del, null)) return;

            var list = del.GetInvocationList();
            var alive = new List<Delegate>();

            // 用于诊断的去重 set（只在首次遇到未知类型时打印一次）
            var unknownLogged = new HashSet<string>();

            foreach (var d in list)
            {
                bool isDead = false;

                // ── 检测 1：标准 .NET null ──
                if (d.Target == null)
                {
                    isDead = true;
                }
                // ── 检测 2：Unity 已 Destroy 的假 null ──
                else if (d.Target is UnityEngine.Object unityTarget)
                {
                    isDead = (unityTarget == null);
                }

                // ── 检测 3：按类型名精准剔除 ArcheryFocus 闭包 ──
                if (!isDead && d.Target != null)
                {
                    string fullName = d.Target.GetType().FullName;
                    if (fullName != null &&
                        (fullName.Contains("ArcheryFocusAbility") || fullName.Contains("ArcheryFocusComponent")))
                    {
                        isDead = true;
                    }
                    // ── 诊断：打印未匹配的委托类型（帮助排查遗漏的残留类型） ──
                    else if (fullName != null && unknownLogged.Add(fullName) && Logger != null)
                    {
                        Logger.LogWarning(string.Format(
                            "[Titan] [诊断] 存活委托 Target 类型: {0} （未被过滤）",
                            fullName));
                    }
                }

                if (isDead) continue;
                alive.Add(d);
            }

            if (alive.Count < list.Length)
            {
                cleanedCount += list.Length - alive.Count;
                try
                {
                    if (alive.Count > 0)
                        onUpdateField.SetValue(stateObj, Delegate.Combine(alive.ToArray()));
                    else
                        onUpdateField.SetValue(stateObj, null);
                }
                catch { }
            }
        }
    }
}