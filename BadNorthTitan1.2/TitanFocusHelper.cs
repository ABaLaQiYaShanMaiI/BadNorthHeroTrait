// Author: ABaLaQiYaShanMaiI
using System.Reflection;
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
	/// </summary>
	public class TitanFocusHelper : MonoBehaviour
	{
		private Vector3 _shootDir;           // 统一射击方向（已归一化）
		private int _ammo = 25;
		private float _shotInterval = 0.01f;
		private float _lastShotTime;
		private float _spread = 0.5f;

		// 从原版 archery settings 复制（保留地形/敌人碰撞层掩码）
		private ProjectileSettings _baseSettings;

		private const float ArrowSpeed = 17f;
		private const float ArrowDrag = 0f;
		private const float ArrowGravity = 0f;
		private const float ArrowStartOffset = 0.8f;

		// 反射缓存：ProjectileSettings 字段列表（用于全字段拷贝）
		private static FieldInfo[] _psFields = null;
		private static bool _psFieldsAttempted = false;
		// 反射缓存：Archery.coolDownTime（每次射击前清零以绕过原版冷却门）
		private static FieldInfo _tfhCoolDownField = null;
		private static bool _tfhCoolDownFieldAttempted = false;

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
			_lastShotTime = 0f;
			_baseSettings = baseSettings;
		}

		void OnEnable()
		{
			_lastShotTime = 0f;
			Plugin.Logger.LogInfo(string.Format("[Titan FocusHelper] OnEnable → ammo={0}, interval={1:F3}, spread={2:F2}, dir=({3:F2},{4:F2},{5:F2})",
				_ammo, _shotInterval, _spread, _shootDir.x, _shootDir.y, _shootDir.z));
		}

		void Update()
		{
			if (_ammo <= 0)
			{
				Plugin.Logger.LogInfo(string.Format("[Titan FocusHelper] 弹药耗尽，销毁自身 (GameObject={0})", gameObject.name));
				Destroy(this);
				return;
			}

			if (Time.time - _lastShotTime < _shotInterval)
				return;

			Archery archery = GetComponent<Archery>();
			if (ReferenceEquals(archery, null))
			{
				Plugin.Logger.LogWarning(string.Format("[Titan FocusHelper] Archery 组件为 null！销毁 (GameObject={0})", gameObject.name));
				Destroy(this);
				return;
			}

			float timeSinceLastShot = Time.time - _lastShotTime;
			_lastShotTime = Time.time;

			// 记录射击前的冷却状态
			float cooldownBefore = 0f;
			if (!_tfhCoolDownFieldAttempted)
			{
				_tfhCoolDownFieldAttempted = true;
				_tfhCoolDownField = typeof(Archery).GetField("coolDownTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (!ReferenceEquals(_tfhCoolDownField, null))
				cooldownBefore = (float)_tfhCoolDownField.GetValue(archery);

			// 清除原版冷却时间，绕过硬直期防止 Shoot 内部拒绝
			if (!ReferenceEquals(_tfhCoolDownField, null))
				_tfhCoolDownField.SetValue(archery, 0f);

			// 每次射击前自动转向到统一射击方向的水平朝向
			Vector3 horizontalDir = _shootDir;
			horizontalDir.y = 0f;
			if (horizontalDir != Vector3.zero)
				archery.transform.rotation = Quaternion.LookRotation(horizontalDir);

			// 从原版设置拷贝全部字段（保留地形/敌人碰撞层掩码）
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

			// 对统一方向施加散布 → 平行齐射带微偏
			Vector3 dir = _shootDir;
			if (_spread > 0f)
			{
				Vector3 spreadVec = Random.insideUnitSphere * _spread;
				dir += spreadVec;
				dir.Normalize();
				Plugin.Logger.LogInfo(string.Format("[Titan FocusHelper] 射击 #{0}: 剩余={1}, 间隔={2:F4}s, cooldown前={3:F2}, spread=({4:F3},{5:F3},{6:F3}), dir=({7:F3},{8:F3},{9:F3})",
					25 - _ammo + 1, _ammo - 1, timeSinceLastShot, cooldownBefore,
					spreadVec.x, spreadVec.y, spreadVec.z,
					dir.x, dir.y, dir.z));
			}
			else
			{
				Plugin.Logger.LogInfo(string.Format("[Titan FocusHelper] 射击 #{0}: 剩余={1}, 间隔={2:F4}s, cooldown前={3:F2}, dir=({4:F3},{5:F3},{6:F3})",
					25 - _ammo + 1, _ammo - 1, timeSinceLastShot, cooldownBefore,
					dir.x, dir.y, dir.z));
			}

			archery.Shoot(dir * ArrowSpeed, ps);
			_ammo--;
		}
	}
}