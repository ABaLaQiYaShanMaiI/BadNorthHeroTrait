// Author: ABaLaQiYaShanMaiI
using System.Reflection;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorthTitan
{
	/// <summary>
	/// 泰坦专注射击助手 — 极简实现。
	/// 
	/// 不继承 ChildComponent，不使用 AgentState，不注册 OnUpdate 委托。
	/// 仅用 Unity Update() 驱动射击，弹药耗尽后自动销毁。
	/// 
	/// 由 TitanArcheryFixes.DoTargetedActionPrefix 在玩家点击专注按钮时挂载。
	/// 运行时日志受 Plugin.EnableGameplayLog 开关控制（默认关闭）。
	/// 
	/// 关键修复：
	///   1. 每次 Shoot() 前清除 coolDownTime → 绕过原版冷却门
	///   2. 确保 target.mask0/mask1 包含 arrowLow → 弹丸可检测地形、防止穿透
	///   3. 散布减小至 0.03f（vs 原 0.5f）→ 更集中命中
	/// </summary>
	public class TitanFocusHelper : MonoBehaviour
	{
		private Vector3 _shootDir;           // 统一射击方向（已归一化）
		private int _ammo = 8;
		private float _shotInterval = 0.04f;
		private float _lastShotTime;
		private float _spread = 0.03f;

		// 从原版 archery settings 复制（保留地形/敌人碰撞层掩码）
		private ProjectileSettings _baseSettings;

		private const float ArrowSpeed = 17f;
		private const float ArrowDrag = 0f;
		private const float ArrowGravity = 0f;
		private const float ArrowStartOffset = 0.8f;

		// ── 日志门控 ──
		private static void GameplayLog(string message) => Debugger.Log(Plugin.EnableGameplayLog, message);
		private static void GameplayLogWarn(string message) => Debugger.LogWarning(Plugin.EnableGameplayLog, message);

		// 反射缓存：ProjectileSettings 字段列表（用于全字段拷贝）
		private static FieldInfo[] _psFields = null;
		private static bool _psFieldsAttempted = false;
		// 反射缓存：Archery.coolDownTime（每次射击前清零以绕过原版冷却门）
		private static FieldInfo _tfhCoolDownField = null;
		private static bool _tfhCoolDownFieldAttempted = false;
		// 反射缓存：Archery.target（用于修复碰撞掩码）
		private static FieldInfo _tfhTargetField = null;
		private static bool _tfhTargetFieldAttempted = false;

		/// <summary>
		/// 配置专注参数（由外部在挂载后立即调用）。
		/// shootDir = 从小队中心到目标的统一方向（已归一化，含垂直角度）。
		/// baseSettings = 从 archery 组件获取的原版 ProjectileSettings（保留碰撞层）。
		/// </summary>
		public void Configure(Vector3 shootDir, int ammo, float interval, ProjectileSettings baseSettings)
		{
			_shootDir = shootDir;
			_ammo = ammo;
			_shotInterval = interval;
			_lastShotTime = Time.time - interval;
			_baseSettings = baseSettings;
			GameplayLog(string.Format("[Titan FocusHelper] Configure → ammo={0}, interval={1:F3}, spread={2:F2}, shoot_dir=({3:F3},{4:F3},{5:F3}), _lastShotTime={6:F2}",
				_ammo, _shotInterval, _spread, _shootDir.x, _shootDir.y, _shootDir.z, _lastShotTime));
		}

		void OnEnable()
		{
			GameplayLog(string.Format("[Titan FocusHelper] OnEnable → ammo={0}, interval={1:F3}, spread={2:F2} (等待 Configure 注入 _shootDir)",
				_ammo, _shotInterval, _spread));
		}

		void Update()
		{
			if (_ammo <= 0)
			{
				GameplayLog(string.Format("[Titan FocusHelper] 弹药耗尽，销毁自身 (GameObject={0})", gameObject.name));
				Destroy(this);
				return;
			}

			if (Time.time - _lastShotTime < _shotInterval)
				return;

			Archery archery = GetComponent<Archery>();
			if (ReferenceEquals(archery, null))
			{
				GameplayLogWarn(string.Format("[Titan FocusHelper] Archery 组件为 null！销毁 (GameObject={0})", gameObject.name));
				Destroy(this);
				return;
			}

			float timeSinceLastShot = Time.time - _lastShotTime;
			_lastShotTime = Time.time;

			// ── 1. 清除 Archery 冷却时间，绕过原版射击门控 ──
			float cooldownBefore = 0f;
			if (!_tfhCoolDownFieldAttempted)
			{
				_tfhCoolDownFieldAttempted = true;
				_tfhCoolDownField = typeof(Archery).GetField("coolDownTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (!ReferenceEquals(_tfhCoolDownField, null))
			{
				cooldownBefore = (float)_tfhCoolDownField.GetValue(archery);
				_tfhCoolDownField.SetValue(archery, 0f);
			}

			// ── 2. 确保 target.mask0/mask1 包含地形层，防止弹丸穿透地形 ──
			if (!_tfhTargetFieldAttempted)
			{
				_tfhTargetFieldAttempted = true;
				_tfhTargetField = typeof(Archery).GetField("target",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (!ReferenceEquals(_tfhTargetField, null))
			{
				object targetObj = _tfhTargetField.GetValue(archery);
				if (targetObj != null)
				{
					LineOfSight.Sight sight = (LineOfSight.Sight)targetObj;
					sight.mask0 |= LayerMaster.arrowLow;
					sight.mask1 |= LayerMaster.arrowLow;
					_tfhTargetField.SetValue(archery, sight);
				}
				else
				{
					LineOfSight.Sight fallback = default(LineOfSight.Sight);
					fallback.mask0 = LayerMaster.arrowLow;
					fallback.mask1 = LayerMaster.arrowLow;
					fallback.agent = null;
					fallback.score = 0f;
					_tfhTargetField.SetValue(archery, fallback);
				}
			}

			// ── 3. 转向到射击方向的水平朝向 ──
			Vector3 horizontalDir = _shootDir;
			horizontalDir.y = 0f;
			if (horizontalDir != Vector3.zero)
				archery.transform.rotation = Quaternion.LookRotation(horizontalDir);

			// ── 4. 从原版设置全字段拷贝（保留碰撞层等） ──
			ProjectileSettings ps = new ProjectileSettings();
			if (!ReferenceEquals(_baseSettings, null))
			{
				if (!_psFieldsAttempted)
				{
					_psFieldsAttempted = true;
					_psFields = typeof(ProjectileSettings).GetFields();
				}
				if (!ReferenceEquals(_psFields, null))
				{
					foreach (FieldInfo fi in _psFields)
						fi.SetValue(ps, fi.GetValue(_baseSettings));
				}
			}
			// 覆盖为泰坦高速直线弹道参数
			ps.maxSpeed = ArrowSpeed;
			ps.drag = ArrowDrag;
			ps.gravity = ArrowGravity;
			ps.startOffset = ArrowStartOffset;

			// ── 5. 施加微散布后射击 ──
			Vector3 dir = _shootDir;
			if (_spread > 0f)
			{
				Vector3 spreadVec = Random.insideUnitSphere * _spread;
				dir += spreadVec;
				dir.Normalize();
				GameplayLog(string.Format("[Titan FocusHelper] 射击 #{0}/{4}: 剩余={1}, 间隔={2:F4}s, cooldown前={3:F2}, spread=({5:F3},{6:F3},{7:F3}), shoot_dir=({8:F3},{9:F3},{10:F3})",
					_ammo, _ammo - 1, timeSinceLastShot, cooldownBefore,
					_ammo, spreadVec.x, spreadVec.y, spreadVec.z,
					dir.x, dir.y, dir.z));
			}
			else
			{
				GameplayLog(string.Format("[Titan FocusHelper] 射击 #{0}/{4}: 剩余={1}, 间隔={2:F4}s, cooldown前={3:F2}, shoot_dir=({5:F3},{6:F3},{7:F3})",
					_ammo, _ammo - 1, timeSinceLastShot, cooldownBefore,
					_ammo, dir.x, dir.y, dir.z));
			}

			archery.Shoot(dir * ArrowSpeed, ps);
			_ammo--;
		}
	}
}