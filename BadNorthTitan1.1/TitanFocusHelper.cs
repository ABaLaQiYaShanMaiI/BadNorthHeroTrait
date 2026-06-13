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
		private int _ammo = 5;
		private float _shotInterval = 0.4f;
		private float _lastShotTime;
		private float _spread = 0.03f;

		// 从原版 archery settings 复制（保留地形/敌人碰撞层掩码）
		private ProjectileSettings _baseSettings;

		private const float ArrowSpeed = 17f;
		private const float ArrowDrag = 0f;
		private const float ArrowGravity = 0f;
		private const float ArrowStartOffset = 0.8f;

		// 反射缓存：ProjectileSettings 字段列表（用于全字段拷贝）
		private static FieldInfo[] _psFields = null;
		private static bool _psFieldsAttempted = false;

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
		}

		void Update()
		{
			if (_ammo <= 0)
			{
				Destroy(this);
				return;
			}

			if (Time.time - _lastShotTime < _shotInterval)
				return;

			Archery archery = GetComponent<Archery>();
			if (ReferenceEquals(archery, null))
			{
				Destroy(this);
				return;
			}

			_lastShotTime = Time.time;

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
				dir += Random.insideUnitSphere * _spread;
				dir.Normalize();
			}

			archery.Shoot(dir * ArrowSpeed, ps);
			_ammo--;
		}
	}
}