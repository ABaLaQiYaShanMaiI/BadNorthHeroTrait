// Author: ABaLaQiYaShanMaiI
// 通用专注射击助手 — 极简实现
// 不继承 ChildComponent，不使用 AgentState，不注册 OnUpdate 委托。
// 仅用 Unity Update() 驱动射击，弹药耗尽后自动销毁。
//
// 由 ArcheryCrashFix.ShootAtPrefix 在玩家点击专注按钮时挂载。
// 弹道参数完全继承自原版 ArcheryFocusComponent.Settings，不做任何覆盖——
// 保留其他 mod 对弹丸速度/重力/阻力等参数的定制。
using System.Reflection;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace BadNorthArcheryFix
{
	public class FocusFixHelper : MonoBehaviour
	{
		private Vector3 _shootDir;
		private int _ammo = 8;
		private float _shotInterval = 0.04f;
		private float _lastShotTime;
		private float _spread = 0.5f;

		private ProjectileSettings _baseSettings;

		private static FieldInfo[] _psFields = null;
		private static bool _psFieldsAttempted = false;
		private static FieldInfo _coolDownField = null;
		private static bool _coolDownFieldAttempted = false;

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

			// 清除原版冷却时间，绕过硬直期防止 Shoot 内部拒绝
			if (!_coolDownFieldAttempted)
			{
				_coolDownFieldAttempted = true;
				_coolDownField = typeof(Archery).GetField("coolDownTime",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (!ReferenceEquals(_coolDownField, null))
				_coolDownField.SetValue(archery, 0f);

			// 转向到统一射击方向的水平朝向
			Vector3 horizontalDir = _shootDir;
			horizontalDir.y = 0f;
			if (horizontalDir != Vector3.zero)
				archery.transform.rotation = Quaternion.LookRotation(horizontalDir);

			// 从原版 ArcheryFocusComponent.Settings 拷贝 ProjectileSettings
			// 不做任何覆盖——保留其他 mod 对弹丸参数的定制
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

			// 对统一方向施加散布
			Vector3 dir = _shootDir;
			if (_spread > 0f)
			{
				dir += Random.insideUnitSphere * _spread;
				dir.Normalize();
			}

			// 速度 = 从 baseSettings 继承的 maxSpeed（未被覆盖）
			archery.Shoot(dir * ps.maxSpeed, ps);
			_ammo--;
		}
	}
}