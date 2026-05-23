using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthRegenerative
{
    /// <summary>
    /// 标记组件：挂载到受 Regenerative 特质影响的矛兵单位上。
    /// 用于标识该单位已接受不抬枪改造（移除盾牌），供其他系统判断。
    /// </summary>
    public class RegenerativeSpearBuff : MonoBehaviour
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
                Plugin.Logger.LogInfo($"[RegenerativeSpearBuff] 已挂载到 {agent.name}");
            }
        }
    }
}
