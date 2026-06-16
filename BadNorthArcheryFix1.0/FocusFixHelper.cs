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
		private float _shotInterval = 0.2f;
		private float _lastShotTime;
		private float _spread = 0.2f;

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

			if (!_coolDownFieldAttempted)
			{
				_coolDownFieldAttempted = true;
				_coolDownField = typeof(Archery).GetField("coolDownTime",
					BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			}
			if (!ReferenceEquals(_coolDownField, null))
				_coolDownField.SetValue(archery, 0f);

			Vector3 horizontalDir = _shootDir;
			horizontalDir.y = 0f;
			if (horizontalDir != Vector3.zero)
				archery.transform.rotation = Quaternion.LookRotation(horizontalDir);

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