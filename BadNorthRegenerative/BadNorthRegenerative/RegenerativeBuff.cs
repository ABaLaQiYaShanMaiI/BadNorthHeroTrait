using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthRegenerative
{
    /// <summary>
    /// 标记组件：挂载到受 Regenerative 特质影响的盾兵单位上。
    /// 用于标识该单位已接受双刀改造，供其他系统（如 RegenerativeJumpResponder）判断。
    /// </summary>
    public class RegenerativeBuff : MonoBehaviour
    {
        private Agent agent;

        private void Awake()
        {
            agent = GetComponent<Agent>();
        }

        private void Start()
        {
            if (agent != null)
            {
                Plugin.Logger.LogInfo(string.Format("[RegenerativeBuff] 已挂载到 {0}", agent.name));
            }
        }
    }
}
