// Author: ABaLaQiYaShanMaiI
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthThorns
{
    /// <summary>
    /// 荆棘特质定义 - 只负责定义和 OnAppliedToSquad 分发。
    /// 反伤逻辑由独立的 ThornsResponder 组件处理，
    /// 实现"定义"与"行为实例"分离。
    /// </summary>
    public class Thorns : HeroUpgradeDefinition
    {
        public static readonly string THORNS_ID = "Hero_Trait_Thorns";

        public Thorns()
        {
            this.upgradeType = TraitHelper.CreateTraitUpgradeType();
            this.upgradeType.canBeStartItem = true;
            TraitHelper.SetupBaseDefinition(this, THORNS_ID,
                "ABaLaQiYaShanMaiI/TRAIT/THORNS/NAME",
                "ABaLaQiYaShanMaiI/TRAIT/THORNS/DESCSHORT",
                CustomSprites.Sprites["trait_thorns"],
                TraitHelper.CreateSingleLevel("ABaLaQiYaShanMaiI/TRAIT/THORNS/DESC"));
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);

            // 为新创建的 Agent 附加荆棘响应器（防重复订阅）
            squad.onAgentCreated -= this.AttachThornsResponder;
            squad.onAgentCreated += this.AttachThornsResponder;

            // 为现有 Agent 附加荆棘响应器
            foreach (Agent agent in squad.agents)
            {
                this.AttachThornsResponder(agent);
            }

            Debugger.Log("[Thorns] 已应用到小队 " + (squad != null ? squad.name : "null"));
        }

        /// <summary>
        /// 为 Agent 附加荆棘响应器组件。
        /// 使用 GetOrAddComponent 防止重复挂载。
        /// </summary>
        private void AttachThornsResponder(Agent agent)
        {
            if (ReferenceEquals(agent, null))
                return;

            ComponentHelper.GetOrAddComponent<ThornsResponder>(agent.gameObject);
            Debugger.Log("[Thorns] 已为 " + agent.name + " 附加荆棘响应器");
        }
    }
}