shenusing UnityEngine;
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
		private Vector3 _targetPos;
		private int _ammo = 5;
		private float _shotInterval = 0.4f;
		private float _lastShotTime;
		private float _spread = 0.03f;

		private const float ArrowSpeed = 17f;
		private const float ArrowDrag = 0f;
		private const float ArrowGravity = 0f;
		private const float ArrowStartOffset = 0.8f;

		/// <summary>
		/// 配置专注参数（由外部在挂载后立即调用）。
		/// </summary>
		public void Configure(Vector3 targetPos, int ammo, float interval)
		{
			_targetPos = targetPos;
			_ammo = ammo;
			_shotInterval = interval;
			_lastShotTime = 0f;
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

			// 使用泰坦高速直线弹道参数
			ProjectileSettings ps = new ProjectileSettings();
			ps.maxSpeed = ArrowSpeed;
			ps.drag = ArrowDrag;
			ps.gravity = ArrowGravity;
			ps.startOffset = ArrowStartOffset;

			// 稍微偏移目标位置（每个弓箭手瞄准敌人不同部位，避免箭矢堆叠贯穿）
			Vector3 jitteredTarget = _targetPos + Random.insideUnitSphere * 0.2f;

			Vector3 shootPos = archery.ShootPos;
			Vector3 dir = (jitteredTarget - shootPos).normalized;

			// 散布
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