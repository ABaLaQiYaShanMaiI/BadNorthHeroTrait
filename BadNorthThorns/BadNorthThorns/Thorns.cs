using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthThorns
{
    public class Thorns : HeroUpgradeDefinition, IAttackResponder
    {
        public static readonly string THORNS_ID = "Hero_Trait_Thorns";

        public Thorns()
        {
            Plugin.Logger.LogInfo("THORNS CREATED");
            this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
            this.upgradeType.typeEnum = 4;
            this.upgradeType.canBeStartItem = true;
            this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
            this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
            this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
            this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
            this.affectsPortrait = false;
            base.name = THORNS_ID;
            this.nameTerm = "NACU/TRAIT/THORNS/NAME";
            this.shortDescription = "NACU/TRAIT/THORNS/DESCSHORT";
            this.infoSprite = CustomSprites.Sprites["trait_thorns"];
            HeroUpgradeDefinition.Level[] array = new HeroUpgradeDefinition.Level[1];
            int num = 0;
            HeroUpgradeDefinition.Level level = default(HeroUpgradeDefinition.Level);
            level.cost = 0;
            level.description = "NACU/TRAIT/THORNS/DESC";
            array[num] = level;
            this.levels = array;
        }

        public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
        {
            base.OnAppliedToSquad(squad, upgradeLevel);
            squad.onAgentCreated += this.AddThing;
        }

        public void AddThing(Agent agent)
        {
            if (!agent.attackResponders.Contains(this))
            {
                agent.attackResponders.Add(this);
            }
        }

        public void ModifyAttack(ref Attack attack)
        {
            CloseCombatBrain closeCombatBrain = attack.monoAttacker as CloseCombatBrain;
            if (closeCombatBrain != null)
            {
                closeCombatBrain.agent.DealDamage(new Attack(
                    1.5f, 1f, 1f, -attack.direction, attack.pos,
                    closeCombatBrain, closeCombatBrain.enSquad,
                    "Sfx/English/Sword",
                    ScriptableObjectSingleton<PrefabManager>.instance.hitEffect
                ));
            }
        }
    }
}
