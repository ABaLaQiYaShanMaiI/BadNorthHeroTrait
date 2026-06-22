using System.Reflection;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorthArcheryFix
{
	/// <summary>
	/// 巨人弓箭手专注射击助手 — 极简自建聚焦。
	/// 
	/// v1.1 新增：每次 Shoot() 前修复 Archery.target 碰撞掩码，防止弹丸穿透地形。
	/// </summary>
	public class FocusFixHelper : MonoBehaviour
	{
		private Vector3 _shootDir;
		private int _ammo = 8;
		private float _shotInterval = 0.2f;
		private float _lastShotTime;
		private float _spread = 0.03f;

		private ProjectileSettings _baseSettings;

		// ── 反射缓存 ──
		private static FieldInfo _coolDownField = null;
		private static bool _coolDownFieldAttempted = false;
		private static FieldInfo _targetField = null;
		private static bool _targetFieldAttempted = false;
		private static FieldInfo[] _psFields = null;
		private static bool _psFieldsAttempted = false;

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

		public void Configure(Vector3 shootDir, int ammo, float interval, ProjectileSettings baseSettings)
		{
			_shootDir = shootDir;
			_ammo = ammo;
			_shotInterval = interval;
			_lastShotTime = Time.time - interval;
			_baseSettings = baseSettings;
			Log(string.Format("[FocusFixHelper] Configure → ammo={0}, interval={1:F3}, spread={2:F2}",
				_ammo, _shotInterval, _spread));
		}

		void OnEnable()
		{
			Log(string.Format("[FocusFixHelper] OnEnable → ammo={0}, interval={1:F3}", _ammo, _shotInterval));
		}

		void Update()
		{
			if (_ammo <= 0)
			{
				Log("[FocusFixHelper] 弹药耗尽，销毁自身");
				Destroy(this);
				return;
			}

			if (Time.time - _lastShotTime < _shotInterval)
				return;

			Archery archery = GetComponent<Archery>();
			if (ReferenceEquals(archery, null))
			{
				LogWarn("[FocusFixHelper] Archery 组件为 null，销毁");
				Destroy(this);
				return;
			}

			_lastShotTime = Time.time;

			// ── 1. 清除 Archery 冷却时间，绕过原版射击门控 ──
			if (!_coolDownFieldAttempted)
			{
				_coolDownFieldAttempted = true;
				_coolDownField = typeof(Archery).GetField("coolDownTime",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (!ReferenceEquals(_coolDownField, null))
				_coolDownField.SetValue(archery, 0f);

			// ── 2. 确保 Archery.target 的碰撞掩码包含地形层 ──
			if (!_targetFieldAttempted)
			{
				_targetFieldAttempted = true;
				_targetField = typeof(Archery).GetField("target",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (!ReferenceEquals(_targetField, null))
			{
				object targetObj = _targetField.GetValue(archery);
				if (targetObj != null)
				{
					LineOfSight.Sight sight = (LineOfSight.Sight)targetObj;
					sight.mask0 |= LayerMaster.arrowLow;
					sight.mask1 |= LayerMaster.arrowLow;
					_targetField.SetValue(archery, sight);
				}
				else
				{
					LineOfSight.Sight fallback = default(LineOfSight.Sight);
					fallback.mask0 = LayerMaster.arrowLow;
					fallback.mask1 = LayerMaster.arrowLow;
					fallback.agent = null;
					fallback.score = 0f;
					_targetField.SetValue(archery, fallback);
				}
			}

			// ── 3. 转向到射击方向的水平朝向 ──
			Vector3 horizontalDir = _shootDir;
			horizontalDir.y = 0f;
			if (horizontalDir != Vector3.zero)
				archery.transform.rotation = Quaternion.LookRotation(horizontalDir);

			// ── 4. 从原版设置全字段拷贝 ──
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

			// ── 5. 施加微散布后射击 ──
			Vector3 dir = _shootDir;
			if (_spread > 0f)
			{
				dir += Random.insideUnitSphere * _spread;
				dir.Normalize();
			}

			archery.Shoot(dir * ps.maxSpeed, ps);
			_ammo--;
		}
	}
}