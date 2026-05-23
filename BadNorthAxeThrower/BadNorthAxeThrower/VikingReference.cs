namespace Voxels.TowerDefense
{
    // 占位VikingReference类，仅用于编译通过
    public class VikingReference
    {
        public Viking viking = new Viking();
    }

    public class Viking
    {
        public Agent agent = new Agent();
    }

    public class Agent : UnityEngine.MonoBehaviour
    {
        public T GetComponent<T>() where T : class, new() => new T();
    }
}
