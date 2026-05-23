using UnityEngine;

namespace Voxels.TowerDefense.Ballistics
{
    // 占位AxeThrowing类，仅用于编译通过
    public class AxeThrowing : MonoBehaviour
    {
        public int ammo;
        public AttackSettings attackSettings = new AttackSettings();
        public AudioClip prepareSound;
        public GameObject throwingAxePrefab;
        public object trajectoryUtility;
        public void Setup() { }
    }

    // 必须为 public 且同级，供外部直接引用
    public class AttackSettings
    {
        public float damage = 1f;
        public float knockback = 1f;
        public float stun = 1f;
        public float launchImpulse = 1f;
    }
}
