using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;
using Voxels.TowerDefense.Upgrades;

namespace BadNorthTitan
{
    /// <summary>
    /// 巨弓手专注射击 - 泰坦弓箭手专属的专注技能。
    /// 
    /// 完全替代原版 ArcheryFocusAbility，使用泰坦的高速直线弹道，
    /// 彻底消除 ModifyArrow NPE 崩溃。不使用 Harmony 拦截 ModifyArrow。
    /// 
    /// 架构：
    /// - TitanFocusAbility: 继承 NavSpotTargetableAbility，负责UI交互与目标选择
    /// - TitanFocusSettings: 每级数据（弹药、伤害、击退等）
    /// - TitanFocusComponent: 继承 ChildComponent<Archery>，管理每帧射击
    /// - TitanFocusHandler: 静态方法，由 Titan.cs 的 TitanizeArchery 和 TitanArcheryFixes 的 Harmony 补丁调用
    /// </summary>

    /// <summary>
    /// 泰坦专注射击的等级设置
    /// </summary>
    [Serializable]
    public struct TitanFocusSettings
    {
        /// <summary>弹药数量（每轮专注可射击次数）</summary>
        public int ammo;

        /// <summary>攻击参数（伤害/击退/眩晕）</summary>
        public AttackSettings attackSettings;

        /// <summary>额外击退倍率（巨弓特有）</summary>
        public float knockbackMultiplier;

        /// <summary>弹道设置引用</summary>
        public ProjectileSettings projectileSettings;

        /// <summary>射击间隔（秒）</summary>
        public float shotInterval;

        /// <summary>最大射程</summary>
        public float maxRange;

        /// <summary>散布系数</summary>
        public float spread;

        /// <summary>穿透数（0=不穿透）</summary>
        public int pierceCount;

        public static TitanFocusSettings CreateDefault(int level)
        {
            TitanFocusSettings s = new TitanFocusSettings();
            s.ammo = 3 + level;
            s.attackSettings = new AttackSettings
            {
                damage = 3f + level * 1.5f,
                knockback = 2f + level,
                stun = 1f + level * 0.5f,
                launchImpulse = 0f
            };
            s.knockbackMultiplier = 1.5f;
            s.projectileSettings = new ProjectileSettings
            {
                maxSpeed = 17f,
                drag = 0f,
                gravity = 0f,
                startOffset = 0.8f
            };
            s.shotInterval = 0.4f;
            s.maxRange = 10f;
            s.spread = 0.03f;
            s.pierceCount = 1;
            return s;
        }
    }

    /// <summary>
    /// 泰坦专注射击技能 — 替代 ArcheryFocusAbility。
    /// 
    /// 挂在泰坦弓箭手的 Agent GameObject 上。
    /// 当玩家激活专注技能时，DoTargetedAction 被调用，
    /// 为每个弓箭手配置 TitanFocusComponent。
    /// </summary>
    public class TitanFocusAbility : NavSpotTargetableAbility, IThreat
    {
        // ── 序列化字段 ──
        [SerializeField] private TitanFocusSettings[] _levelSettings;

        // ── 运行状态 ──
        private TitanFocusSettings _currentSettings;
        private List<TitanFocusComponent> _activeComponents = new List<TitanFocusComponent>();
        private bool _initialized = false;

        // ── 弹道计算器（指向 Titan.cs 中设置的泰坦直线弹道） ──
        private TrajectoryUtility _trajectoryCalculator;

        // ── 反射缓存 ──
        private static FieldInfo _agentField = null;
        private static bool _agentFieldAttempted = false;

        public TitanFocusSettings CurrentSettings
        {
            get { return _currentSettings; }
            set { _currentSettings = value; }
        }

        public TrajectoryUtility TrajectoryCalculator
        {
            get { return _trajectoryCalculator; }
            set { _trajectoryCalculator = value; }
        }

        /// <summary>
        /// 初始化 — 可从 Titan.cs 的 TitanizeArchery 中调用，
        /// 传入泰坦的弹道计算器和等级数据。
        /// </summary>
        public void InitializeFromTitan(TrajectoryUtility trajectoryCalc, int upgradeLevel)
        {
            _trajectoryCalculator = trajectoryCalc;

            if (_levelSettings == null || _levelSettings.Length == 0)
            {
                _levelSettings = new TitanFocusSettings[4];
                for (int i = 0; i < 4; i++)
                {
                    _levelSettings[i] = TitanFocusSettings.CreateDefault(i);
                }
            }

            int idx = Mathf.Clamp(upgradeLevel, 0, _levelSettings.Length - 1);
            _currentSettings = _levelSettings[idx];

            _initialized = true;

            Plugin.Logger.LogInfo(string.Format(
                "[TitanFocusAbility] 已初始化: level={0}, ammo={1}, speed={2}, damage={3}",
                upgradeLevel,
                _currentSettings.ammo,
                _currentSettings.projectileSettings.maxSpeed,
                _currentSettings.attackSettings.damage));
        }

        /// <summary>
        /// 生命周期初始化 — 模拟原版 DoSquadSpawnAction_Implementation。
        /// 不再使用 delegate 订阅（Mono 2.0 不支持 mod 程序集的 System.Action），
        /// 改为 TitanFocusComponent 停用时主动回调 NotifyComponentFinished()。
        /// </summary>
        public void SetupLifecycleMonitor()
        {
            // 不再反射调用基类 DoSquadSpawnAction_Implementation（会引发异常，且非必要）
            // 基类必要的初始化（如音频、UI 引用）在游戏自行调用 DoSquadSpawnAction 时完成，我们无需模拟。
            Plugin.Logger.LogInfo("[TitanFocusAbility] 生命周期监控已就绪（回调模式）");
        }

        /// <summary>
        /// 每帧驱动所有活跃的 TitanFocusComponent，执行射击逻辑和弹药消耗。
        /// 替代 ChildComponent.Update()（在 Mono 2.0 / 特定 Unity 版本中可能不被调用）。
        /// </summary>
        void Update()
        {
            if (_activeComponents == null) return;
            // 从后向前遍历，避免移除元素时的索引错乱
            for (int i = _activeComponents.Count - 1; i >= 0; i--)
            {
                TitanFocusComponent comp = _activeComponents[i];
                if (comp == null || !comp.IsActive)
                {
                    _activeComponents.RemoveAt(i);
                    if (comp == null)
                    {
                        Plugin.Logger.LogWarning("[TitanFocusAbility] 发现一个已销毁的 TitanFocusComponent，已移除");
                    }
                    // 检查是否所有组件都已结束
                    if (_activeComponents.Count == 0)
                    {
                        CallOnEnded();
                    }
                    continue;
                }
                comp.FocusUpdateTick();
            }
        }

        /// <summary>
        /// 反射调用基类 OnEnded() 以正常结束技能。
        /// </summary>
        private void CallOnEnded()
        {
            try
            {
                MethodInfo endedMethod = typeof(NavSpotTargetableAbility).GetMethod("OnEnded",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (!ReferenceEquals(endedMethod, null))
                {
                    endedMethod.Invoke(this, null);
                    Plugin.Logger.LogInfo("[TitanFocusAbility] 所有弓箭手专注完毕，已自动调用 OnEnded()");
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[TitanFocusAbility] OnEnded 调用失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 由 TitanFocusComponent 在其停用时回调。
        /// 当所有活跃组件都已停用，自动调用 OnEnded() 结束技能。
        /// </summary>
        public void NotifyComponentFinished(TitanFocusComponent comp)
        {
            if (ReferenceEquals(comp, null)) return;
            _activeComponents.Remove(comp);

            if (_activeComponents.Count == 0)
            {
                try
                {
                    MethodInfo endedMethod = typeof(NavSpotTargetableAbility).GetMethod("OnEnded",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (!ReferenceEquals(endedMethod, null))
                    {
                        endedMethod.Invoke(this, null);
                        Plugin.Logger.LogInfo("[TitanFocusAbility] 所有弓箭手专注完毕，已调用 OnEnded()");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning("[TitanFocusAbility] OnEnded 调用失败: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 当玩家点击专注技能按钮时被游戏引擎调用。
        /// 为小队中每个弓箭手激活 TitanFocusComponent。
        /// </summary>
        protected override void DoTargetedAction(NavSpot heroNavSpot, NavSpot target)
        {
            if (!_initialized)
            {
                Plugin.Logger.LogWarning("[TitanFocusAbility] DoTargetedAction 调用时未初始化，跳过");
                return;
            }

            // 获取当前 Agent
            Agent agent = GetAgent();
            if (ReferenceEquals(agent, null))
            {
                Plugin.Logger.LogWarning("[TitanFocusAbility] 无法获取 Agent 引用");
                return;
            }

            // ★ 修复 2：激活专注技能时，强制清扫当前小队所有弓箭手身上可能残留的原版组件及死亡委托
            // 1) 先逐 Agent 移除原版组件（含 OnUpdate 清理）
            EnglishSquad squad = agent.squad as EnglishSquad;
            if (!ReferenceEquals(squad, null))
            {
                foreach (Agent squadAgent in squad.agents)
                {
                    if (ReferenceEquals(squadAgent, null)) continue;
                    if (!IsTitanArcher(squadAgent)) continue;
                    TitanFocusHandler.RemoveOriginalFocusComponents(squadAgent);
                }
            }
            // 2) 再触发一次全局清理（按类型名精准剔除所有 ArcheryFocus 闭包残留）
            Plugin.DoGlobalCleanup();

            // 获取目标位置
            Vector3 targetPos = Vector3.zero;
            if (!ReferenceEquals(target, null))
            {
                targetPos = target.transform.position;
            }
            else if (!ReferenceEquals(heroNavSpot, null))
            {
                targetPos = heroNavSpot.transform.position;
            }

            _activeComponents.Clear();

            // 计算目标方向（全体弓箭手共享）
            Vector3 focusDir = Vector3.zero;
            if (targetPos != Vector3.zero && agent.chestPos != Vector3.zero)
            {
                focusDir = (targetPos - agent.chestPos).normalized;
            }
            else
            {
                focusDir = agent.transform.forward;
            }

            // 为每个泰坦弓箭手激活专注组件
            foreach (Agent squadAgent in squad.agents)
            {
                if (ReferenceEquals(squadAgent, null)) continue;
                if (!IsTitanArcher(squadAgent)) continue;

                TitanFocusComponent focusComp = squadAgent.GetComponent<TitanFocusComponent>();
                if (ReferenceEquals(focusComp, null))
                {
                    Archery archery = squadAgent.GetComponent<Archery>();
                    if (ReferenceEquals(archery, null)) continue;

                    // 尝试作为 ChildComponent 获取
                    focusComp = archery.GetComponent<TitanFocusComponent>();
                }

                if (ReferenceEquals(focusComp, null))
                {
                    Plugin.Logger.LogWarning(string.Format(
                        "[TitanFocusAbility] Agent #{0} 缺少 TitanFocusComponent，跳过",
                        squadAgent.GetInstanceID()));
                    continue;
                }

                focusComp.Activate(_currentSettings, focusDir, targetPos);
                _activeComponents.Add(focusComp);

                Plugin.Logger.LogInfo(string.Format(
                    "[TitanFocusAbility] 激活专注: Archer#{0}, target={1}",
                    squadAgent.GetInstanceID(),
                    targetPos));
            }

            Plugin.Logger.LogInfo(string.Format(
                "[TitanFocusAbility] DoTargetedAction 完成: {0} 个弓箭手已激活",
                _activeComponents.Count));
        }

        /// <summary>
        /// 箭矢效果 — 替代原版 ModifyArrow。
        /// 直接内联到 TitanFocusComponent 的射击逻辑中，避免 Harmony 对 ModifyArrow 的补丁。
        /// </summary>
        public static void TitanizeArrow(Arrow arrow, TitanFocusSettings settings)
        {
            if (ReferenceEquals(arrow, null)) return;

            try
            {
                // 设置箭矢的落地效果（穿透/击飞等）
                // 使用反射访问 Arrow 的内部属性
                Type arrowType = typeof(Arrow);

                // 尝试设置穿透
                FieldInfo pierceField = arrowType.GetField("pierceCount",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!ReferenceEquals(pierceField, null))
                {
                    pierceField.SetValue(arrow, settings.pierceCount);
                }

                // 尝试设置额外击退
                FieldInfo kbField = arrowType.GetField("knockbackMultiplier",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (!ReferenceEquals(kbField, null))
                {
                    kbField.SetValue(arrow, settings.knockbackMultiplier);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[TitanFocusAbility] TitanizeArrow 异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 通过反射获取 Agent 引用
        /// </summary>
        private Agent GetAgent()
        {
            if (!_agentFieldAttempted)
            {
                _agentFieldAttempted = true;
                Type baseType = typeof(NavSpotTargetableAbility);
                // 尝试常见的字段名
                string[] candidates = { "agent", "_agent", "heroAgent", "owner" };
                foreach (string name in candidates)
                {
                    _agentField = baseType.GetField(name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (!ReferenceEquals(_agentField, null)) break;
                }
            }

            if (!ReferenceEquals(_agentField, null))
            {
                return _agentField.GetValue(this) as Agent;
            }

            // 回退：通过 GameObject 查找
            Agent agent = GetComponent<Agent>();
            if (!ReferenceEquals(agent, null)) return agent;

            agent = GetComponentInParent<Agent>();
            return agent;
        }

        /// <summary>
        /// 判断是否为泰坦弓箭手
        /// </summary>
        public static bool IsTitanArcher(Agent agent)
        {
            return agent != null
                && agent.isEnglish
                && agent.scale > 1.1f
                && agent.GetComponent<Archery>() != null;
        }

        // ── IThreat 实现 ──
        public float threatValue { get; set; }

        public float GetThreat()
        {
            return threatValue;
        }

        public Vector3 GetPos(Agent other)
        {
            Agent agent = GetAgent();
            if (ReferenceEquals(agent, null)) return Vector3.zero;
            return agent.chestPos;
        }

        public Vector3 GetThreatDir(Agent other)
        {
            Agent agent = GetAgent();
            if (ReferenceEquals(agent, null)) return Vector3.forward;
            if (ReferenceEquals(other, null)) return agent.transform.forward;
            return (other.chestPos - agent.chestPos).normalized;
        }

        public float GetThreatDistance(Agent other)
        {
            Agent agent = GetAgent();
            if (ReferenceEquals(agent, null) || ReferenceEquals(other, null)) return 0f;
            return Vector3.Distance(agent.chestPos, other.chestPos);
        }

        public bool GetTreatValid(Agent other)
        {
            Agent agent = GetAgent();
            if (ReferenceEquals(agent, null) || ReferenceEquals(other, null)) return false;
            return true;
        }

        // ── 目标有效距离（非 override，基类可能不声明 virtual） ──
        public float GetTargetRange()
        {
            if (_initialized)
                return _currentSettings.maxRange;
            return 8f;
        }

        public float GetAbilityRange()
        {
            return GetTargetRange();
        }
    }

    /// <summary>
    /// 泰坦专注射击组件 — 替代 ArcheryFocusComponent。
    /// 
    /// 挂载在每个泰坦弓箭手的 Archery 组件上（作为 ChildComponent）。
    /// 管理 AgentState 和每帧射击逻辑，使用泰坦高速直线弹道。
    /// 
    /// 关键：不使用 ModifyArrow，避免 Mono 2.0 下 Harmony 补丁崩溃。
    /// </summary>
    public class TitanFocusComponent : ChildComponent<Archery>
    {
        // ── 专注状态 ──
        public AgentState focusState;
        private TitanFocusSettings _settings;
        private Vector3 _focusDirection;
        private Vector3 _focusTarget;
        private int _remainingAmmo;
        private float _lastShotTime;
        private bool _isActive;

        // ── 反射缓存（预留，当前版本使用组件直接查找） ──

        public bool IsActive
        {
            get { return _isActive; }
        }

        /// <summary>
        /// 激活专注模式
        /// </summary>
        public void Activate(TitanFocusSettings settings, Vector3 focusDir, Vector3 targetPos)
        {
            _settings = settings;
            _focusDirection = focusDir;
            _focusTarget = targetPos;
            _remainingAmmo = settings.ammo;
            _lastShotTime = 0f;
            _isActive = true;

            // 激活 AgentState
            if (ReferenceEquals(focusState, null))
            {
                SetupFocusState();
            }

            if (!ReferenceEquals(focusState, null))
            {
                focusState.SetActive(true);
            }

            Plugin.Logger.LogInfo(string.Format(
                "[TitanFocusComponent] Archer#{0} 专注已激活: ammo={1}, dir=({2:F2},{3:F2},{4:F2})",
                base.manager.agent.GetInstanceID(),
                _remainingAmmo,
                _focusDirection.x, _focusDirection.y, _focusDirection.z));
        }

        // ── 回引 TitanFocusAbility（用于通知生命周期结束） ──
        private TitanFocusAbility _owningAbility;

        /// <summary>
        /// 绑定所属的 TitanFocusAbility，停用时会自动回调 NotifyComponentFinished。
        /// </summary>
        public void SetOwningAbility(TitanFocusAbility ability)
        {
            _owningAbility = ability;
        }

        /// <summary>
        /// 停用专注模式 — 自动回调 TitanFocusAbility.NotifyComponentFinished()。
        /// </summary>
        public void Deactivate()
        {
            // 先记录状态（防止重入）
            bool wasActive = _isActive;
            _isActive = false;
            _remainingAmmo = 0;

            if (!ReferenceEquals(focusState, null))
            {
                focusState.SetActive(false);
            }

            Plugin.Logger.LogInfo(string.Format(
                "[TitanFocusComponent] Archer#{0} 专注已停用",
                base.manager.agent.GetInstanceID()));

            // 回调 TitanFocusAbility，通知组件已完成
            if (wasActive && !ReferenceEquals(_owningAbility, null))
            {
                _owningAbility.NotifyComponentFinished(this);
            }
        }

        /// <summary>
        /// 设置 AgentState（替代原版 MaybeSetup 的逻辑）。
        /// 不使用 OnUpdate delegate（Mono 2.0 兼容性），改为 Unity Update() 轮询。
        /// </summary>
        public void SetupFocusState()
        {
            if (ReferenceEquals(base.manager, null))
            {
                Plugin.Logger.LogWarning("[TitanFocusComponent] Archery manager 为空，无法设置专注状态");
                return;
            }

            try
            {
                // 创建专注 AgentState，父级为 aiming
                focusState = new AgentState("TitanFocus", base.manager.aiming, false, true);

                // 不注册 OnUpdate delegate（Mono 2.0 TypeLoadException 风险）
                // FocusUpdate 逻辑由 Unity Update() 轮询驱动

                Plugin.Logger.LogInfo(string.Format(
                    "[TitanFocusComponent] Archer#{0} 专注状态已创建（轮询模式）",
                    base.manager.agent.GetInstanceID()));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning("[TitanFocusComponent] 创建专注状态失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 每帧专注逻辑 — 由 TitanFocusAbility.Update() 驱动。
        /// ChildComponent.Update() 在特定 Unity/Mono 版本中可能不被调用，
        /// 因此将驱动入口放在 TitanFocusAbility（必定继承 MonoBehaviour 且 Update() 有效）。
        /// </summary>
        public void FocusUpdateTick()
        {
            if (_remainingAmmo <= 0)
            {
                Deactivate();
                return;
            }

            Archery archery = base.manager;
            Agent agent = archery.agent;

            TrajectoryUtility trajectoryCalc = GetTrajectoryCalculator();

            if (!archery.AimAt(trajectoryCalc, _focusTarget, Vector3.zero))
            {
                Plugin.Logger.LogInfo(string.Format(
                    "[TitanFocusComponent] Archer#{0} 瞄准失败，停用专注",
                    agent.GetInstanceID()));
                focusState.SetActive(false);
                Deactivate();
                return;
            }

            float now = Time.time;
            if (now - _lastShotTime < _settings.shotInterval)
                return;

            _lastShotTime = now;

            try
            {
                ProjectileSettings ps = _settings.projectileSettings;
                Vector3 shootDir = archery.aimDir;
                if (_settings.spread > 0f)
                {
                    Vector3 spreadOffset = UnityEngine.Random.insideUnitSphere * _settings.spread;
                    shootDir += spreadOffset;
                    shootDir.Normalize();
                }

                Arrow arrow = archery.Shoot(shootDir, ps) as Arrow;
                if (!ReferenceEquals(arrow, null))
                {
                    TitanFocusAbility.TitanizeArrow(arrow, _settings);
                    _remainingAmmo--;

                    Plugin.Logger.LogInfo(string.Format(
                        "[TitanFocusComponent] Archer#{0} 专注射击: ammoRemaining={1}/{2}, dir=({3:F2},{4:F2},{5:F2})",
                        agent.GetInstanceID(), _remainingAmmo, _settings.ammo,
                        shootDir.x, shootDir.y, shootDir.z));
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning(string.Format(
                    "[TitanFocusComponent] Archer#{0} 射击异常: {1}",
                    agent.GetInstanceID(), ex.Message));
            }

            if (_remainingAmmo <= 0)
            {
                Plugin.Logger.LogInfo(string.Format(
                    "[TitanFocusComponent] Archer#{0} 弹药耗尽，专注结束",
                    agent.GetInstanceID()));
                Deactivate();
            }
        }

        /// <summary>
        /// 获取弹道计算器 — 优先使用泰坦的直线弹道。
        /// </summary>
        private TrajectoryUtility GetTrajectoryCalculator()
        {
            // 查找 Agent 上的 TitanFocusAbility
            if (!ReferenceEquals(base.manager, null) && !ReferenceEquals(base.manager.agent, null))
            {
                TitanFocusAbility ability = base.manager.agent.GetComponent<TitanFocusAbility>();
                if (!ReferenceEquals(ability, null) && !ReferenceEquals(ability.TrajectoryCalculator, null))
                {
                    return ability.TrajectoryCalculator;
                }
            }

            // 回退：使用 Archery 自带的 trajectoryCalculator
            if (!ReferenceEquals(base.manager, null))
            {
                try
                {
                    FieldInfo trajField = typeof(Archery).GetField("trajectoryCalculator",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (!ReferenceEquals(trajField, null))
                    {
                        TrajectoryUtility traj = trajField.GetValue(base.manager) as TrajectoryUtility;
                        if (!ReferenceEquals(traj, null))
                            return traj;
                    }
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// 对外暴露的手动射击方法。
        /// 替代 ArcheryFocusComponent.ShootAt，由 Harmony 补丁重定向调用。
        /// </summary>
        public void CustomShootAt(TitanFocusAbility ability, TitanFocusSettings settings,
            Vector3 targetPos, Vector3 delta)
        {
            _settings = settings;
            _focusTarget = targetPos;

            if (ReferenceEquals(base.manager, null)) return;

            Vector3 dir = (targetPos - base.manager.ShootPos).normalized;
            _focusDirection = dir;
            _remainingAmmo = settings.ammo;
            _lastShotTime = 0f;
            _isActive = true;

            if (ReferenceEquals(focusState, null))
            {
                SetupFocusState();
            }

            if (!ReferenceEquals(focusState, null))
            {
                focusState.SetActive(true);
            }

            Plugin.Logger.LogInfo(string.Format(
                "[TitanFocusComponent] Archer#{0} ShootAt 重定向: ammo={1}, target={2}",
                base.manager.agent.GetInstanceID(),
                _remainingAmmo,
                targetPos));
        }
    }

    /// <summary>
    /// 静态辅助类：处理 TitanFocusAbility/TitanFocusComponent 的创建和注入。
    /// 由 Titan.cs 的 TitanizeArchery 和 TitanArcheryFixes 的 Harmony 补丁调用。
    /// </summary>
    public static class TitanFocusHandler
    {
        // ── AgentState.OnUpdate 反射缓存 ──
        private static FieldInfo _agentStateOnUpdateField = null;
        private static bool _agentStateOnUpdateAttempted = false;

        // ── NavSpotTargetableAbility.active 反射缓存 ──
        private static FieldInfo _activeFieldCache = null;
        private static bool _activeFieldAttempted = false;

        // ── ArcheryFocusComponent 的 NavSpotTargetableAbility 派生关联字段名候选 ──
        private static readonly string[] _abilityStateFieldCandidates = { "active", "_active", "agentState", "_agentState" };

        // ── ArcheryFocusComponent 可能的 focusState/state 字段名候选 ──
        private static readonly string[] _componentStateFieldCandidates = { "focusState", "_focusState", "focus", "_focus", "state", "_state", "activeState", "_activeState" };

        /// <summary>
        /// 为泰坦弓箭手 Agent 添加 TitanFocusAbility 和 TitanFocusComponent。
        /// 在 Titan.cs 的 TitanizeArchery 中调用。
        /// </summary>
        public static void SetupTitanFocus(Agent agent, TrajectoryUtility trajectoryCalc, int upgradeLevel)
        {
            if (ReferenceEquals(agent, null)) return;
            if (!TitanFocusAbility.IsTitanArcher(agent)) return;

            // 移除原版组件（如果存在）—— 先清理 OnUpdate 委托，再 Destroy
            RemoveOriginalFocusComponents(agent);

            // 防止重复添加 TitanFocusAbility
            TitanFocusAbility existing = agent.GetComponent<TitanFocusAbility>();
            if (!ReferenceEquals(existing, null))
            {
                UnityEngine.Object.Destroy(existing);
                Plugin.Logger.LogInfo(string.Format(
                    "[TitanFocusHandler] 已移除旧的 TitanFocusAbility 从 Archer#{0}", agent.GetInstanceID()));
            }

            // 添加 TitanFocusAbility
            TitanFocusAbility focusAbility = agent.gameObject.AddComponent<TitanFocusAbility>();
            focusAbility.InitializeFromTitan(trajectoryCalc, upgradeLevel);
            focusAbility.SetupLifecycleMonitor();

            // 添加 TitanFocusComponent 到 Archery，并绑定 OwningAbility
            Archery archery = agent.GetComponent<Archery>();
            if (!ReferenceEquals(archery, null))
            {
                // 防止重复添加 TitanFocusComponent
                TitanFocusComponent existingComp = archery.GetComponent<TitanFocusComponent>();
                if (!ReferenceEquals(existingComp, null))
                {
                    UnityEngine.Object.Destroy(existingComp);
                }

                try
                {
                    TitanFocusComponent focusComp = archery.gameObject.AddComponent<TitanFocusComponent>();
                    if (!ReferenceEquals(focusComp, null))
                    {
                        // 绑定 OwningAbility，使组件停用时能回调 TitanFocusAbility
                        focusComp.SetOwningAbility(focusAbility);
                        Plugin.Logger.LogInfo(string.Format(
                            "[TitanFocusHandler] TitanFocusComponent 已添加到 Archer#{0}（已绑定 OwningAbility）",
                            agent.GetInstanceID()));
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning(string.Format(
                        "[TitanFocusHandler] 添加 TitanFocusComponent 失败: {0}", ex.Message));
                }
            }

            Plugin.Logger.LogInfo(string.Format(
                "[TitanFocusHandler] TitanFocusAbility 已安装到 Archer#{0}, level={1}",
                agent.GetInstanceID(),
                upgradeLevel));
        }

        /// <summary>
        /// 清空 AgentState 上的 OnUpdate 委托。
        /// 用于在销毁原版组件前切除已注册的生命周期回调，防止 NPE 循环。
        /// </summary>
        private static void ClearAgentStateOnUpdate(object stateObj)
        {
            if (ReferenceEquals(stateObj, null)) return;

            if (!_agentStateOnUpdateAttempted)
            {
                _agentStateOnUpdateAttempted = true;
                Type agentStateType = stateObj.GetType();
                // AgentState 上的回调字段可能叫 OnUpdate / onUpdate / _onUpdate / updateDelegate 等
                string[] candidates = { "OnUpdate", "onUpdate", "_onUpdate", "updateDelegate", "_updateDelegate" };
                foreach (string name in candidates)
                {
                    _agentStateOnUpdateField = agentStateType.GetField(name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (!ReferenceEquals(_agentStateOnUpdateField, null))
                        break;
                }
            }

            if (!ReferenceEquals(_agentStateOnUpdateField, null))
            {
                try
                {
                    _agentStateOnUpdateField.SetValue(stateObj, null);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning(string.Format(
                        "[TitanFocusHandler] 清理 AgentState.OnUpdate 失败: {0}", ex.Message));
                }
            }
        }

        /// <summary>
        /// 从能力的 active 字段获取其 AgentState 并清空 OnUpdate。
        /// 适用于 NavSpotTargetableAbility（含 ArcheryFocusAbility）及其子类。
        /// </summary>
        private static void CleanAbilityOnUpdateDelegate(Component ability)
        {
            if (ReferenceEquals(ability, null)) return;

            if (!_activeFieldAttempted)
            {
                _activeFieldAttempted = true;
                Type baseType = typeof(NavSpotTargetableAbility);
                foreach (string name in _abilityStateFieldCandidates)
                {
                    _activeFieldCache = baseType.GetField(name,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                    if (!ReferenceEquals(_activeFieldCache, null))
                        break;
                }
                // 如果基类上没找到，直接从 ability 的运行时类型上找
                if (ReferenceEquals(_activeFieldCache, null))
                {
                    foreach (string name in _abilityStateFieldCandidates)
                    {
                        _activeFieldCache = ability.GetType().GetField(name,
                            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                        if (!ReferenceEquals(_activeFieldCache, null))
                            break;
                    }
                }
            }

            if (!ReferenceEquals(_activeFieldCache, null))
            {
                try
                {
                    object stateObj = _activeFieldCache.GetValue(ability);
                    if (!ReferenceEquals(stateObj, null))
                    {
                        ClearAgentStateOnUpdate(stateObj);
                        Plugin.Logger.LogInfo(string.Format(
                            "[TitanFocusHandler] 已清理 {0} 的 active.OnUpdate 委托",
                            ability.GetType().Name));
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning(string.Format(
                        "[TitanFocusHandler] 清理 {0} active 字段失败: {1}",
                        ability.GetType().Name, ex.Message));
                }
            }
        }

        /// <summary>
        /// 从 ArcheryFocusComponent 中查找其关联的 AgentState 并清空 OnUpdate。
        /// ArcheryFocusComponent 可能在 MaybeSetup 中创建了 focusState 并注册了 OnUpdate。
        /// </summary>
        private static void CleanComponentOnUpdateDelegate(Component focusComp)
        {
            if (ReferenceEquals(focusComp, null)) return;

            Type compType = focusComp.GetType();
            foreach (string fieldName in _componentStateFieldCandidates)
            {
                FieldInfo fi = compType.GetField(fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy);
                if (ReferenceEquals(fi, null)) continue;

                try
                {
                    object stateObj = fi.GetValue(focusComp);
                    if (!ReferenceEquals(stateObj, null))
                    {
                        ClearAgentStateOnUpdate(stateObj);
                        Plugin.Logger.LogInfo(string.Format(
                            "[TitanFocusHandler] 已清理 {0}.{1} 的 OnUpdate 委托",
                            compType.Name, fieldName));
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning(string.Format(
                        "[TitanFocusHandler] 清理 {0}.{1} 失败: {2}",
                        compType.Name, fieldName, ex.Message));
                }
            }
        }

        /// <summary>
        /// 移除原版 ArcheryFocusAbility 和 ArcheryFocusComponent。
        /// 
        /// 关键安全措施：在 Destroy 之前反射获取能力的 active AgentState，
        /// 将其 OnUpdate 委托置为 null，彻底切断原版匿名回调（m__0），
        /// 防止 Destroy 后残留委托导致的 NullReferenceException 循环。
        /// </summary>
        public static void RemoveOriginalFocusComponents(Agent agent)
        {
            if (ReferenceEquals(agent, null)) return;

            // ═══════════════════════════════════════════
            // 第一步：清理 Agent 上的 ArcheryFocusAbility
            // ═══════════════════════════════════════════
            Component[] components = agent.GetComponents<Component>();
            foreach (Component comp in components)
            {
                if (ReferenceEquals(comp, null)) continue;
                string typeName = comp.GetType().Name;
                if (typeName == "ArcheryFocusAbility" || typeName.Contains("ArcheryFocus"))
                {
                    // ★ 关键修复：Destroy 前清理 active.OnUpdate 委托
                    CleanAbilityOnUpdateDelegate(comp);

                    try
                    {
                        UnityEngine.Object.Destroy(comp);
                        Plugin.Logger.LogInfo(string.Format(
                            "[TitanFocusHandler] 已移除 {0} 从 Archer#{1}",
                            typeName, agent.GetInstanceID()));
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning(string.Format(
                            "[TitanFocusHandler] 销毁 {0} 失败: {1}", typeName, ex.Message));
                    }
                }
            }

            // ═══════════════════════════════════════════
            // 第二步：清理 Archery 子对象上的 ArcheryFocusComponent
            // ═══════════════════════════════════════════
            Archery archery = agent.GetComponent<Archery>();
            if (!ReferenceEquals(archery, null))
            {
                Component[] archeryComps = archery.GetComponents<Component>();
                foreach (Component comp in archeryComps)
                {
                    if (ReferenceEquals(comp, null)) continue;
                    string typeName = comp.GetType().Name;
                    if (typeName == "ArcheryFocusComponent" || typeName.Contains("ArcheryFocus"))
                    {
                        // ★ 关键修复：Destroy 前清理 focusState.OnUpdate 委托
                        CleanComponentOnUpdateDelegate(comp);

                        try
                        {
                            UnityEngine.Object.Destroy(comp);
                            Plugin.Logger.LogInfo(string.Format(
                                "[TitanFocusHandler] 已移除 {0} 从 Archery",
                                typeName));
                        }
                        catch (Exception ex)
                        {
                            Plugin.Logger.LogWarning(string.Format(
                                "[TitanFocusHandler] 销毁 {0} 失败: {1}", typeName, ex.Message));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 查找 Agent 上的 TitanFocusComponent（可能在 Agent 自身或 Archery 子组件上）。
        /// </summary>
        public static TitanFocusComponent FindFocusComponent(Agent agent)
        {
            if (ReferenceEquals(agent, null)) return null;

            // 直接在 Agent 上查找
            TitanFocusComponent comp = agent.GetComponent<TitanFocusComponent>();
            if (!ReferenceEquals(comp, null)) return comp;

            // 在 Archery 子组件上查找
            Archery archery = agent.GetComponent<Archery>();
            if (!ReferenceEquals(archery, null))
            {
                comp = archery.GetComponent<TitanFocusComponent>();
            }

            return comp;
        }
    }
}