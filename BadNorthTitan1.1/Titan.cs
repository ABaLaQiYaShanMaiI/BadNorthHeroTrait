// Author: ABaLaQiYaShanMaiI
using System;
using System.Reflection;
using BadNorthAPI;
using UnityEngine;
using Voxels.TowerDefense;

namespace BadNorthTitan
{
	/// <summary>
	/// 泰坦 (Titan) - 真正的巨人之力，盾弓皆可，升级后起效。
	/// 保留 TankArcher 箭矢外观 + trajectoryCalculator；
	/// 专注射击由 TitanArcheryFixes + TitanFocusHelper 安全接管。
	/// </summary>
	public class Titan : HeroUpgradeDefinition
	{
		private static FieldInfo _arrowPrefabField = null;
		private static FieldInfo _trajectoryCalculatorField = null;
		private static FieldInfo _vikingField = null;
		private static FieldInfo _vikingCloneField = null;
		private static bool _fieldsAttempted = false;

		public Titan()
		{
			Debugger.Log("TITAN CREATED");
			this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
			this.upgradeType.typeEnum = HeroUpgradeTypeEnum.Trait;
			this.upgradeType.canBeStartItem = true;
			this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
			this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
			this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
			this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
			this.affectsPortrait = false;
			base.name = Titan.Titan_ID;
			this.nameTerm = "ABaLaQiYaShanMaiI/TRAIT/TITAN/NAME";
			this.shortDescription = "ABaLaQiYaShanMaiI/TRAIT/TITAN/DESCSHORT";
			this.infoSprite = CustomSprites.Sprites["trait_titan"];
			this.levels = new HeroUpgradeDefinition.Level[]
			{
				new HeroUpgradeDefinition.Level
				{
					cost = 0,
					description = "ABaLaQiYaShanMaiI/TRAIT/TITAN/DESC"
				}
			};
		}

		private static void EnsureReflectionFields()
		{
			if (_fieldsAttempted) return;
			_fieldsAttempted = true;

			_arrowPrefabField = typeof(Archery).GetField("arrowPrefab",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_trajectoryCalculatorField = typeof(Archery).GetField("trajectoryCalculator",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_vikingField = typeof(VikingReference).GetField("viking",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			_vikingCloneField = typeof(VikingReference).GetField("vikingClone",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		}

		public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
		{
			if (squad.level >= 1)
			{
				squad.maxCount = squad.maxCount / 2 + 1;
				squad.onAgentSpawned += this.Titanize;
			}
		}

		private void Titanize(Agent agent)
		{
			if (ReferenceEquals(agent, null)) return;

			agent.scale = 1.25f;
			Swordsman component = agent.GetComponent<Swordsman>();
			Archery component2 = agent.GetComponent<Archery>();

			if (!ReferenceEquals(component, null))
			{
				for (int i = 0; i < component.damageLevels.Length; i++)
				{
					component.damageLevels[i] *= 2f;
					component.knockbackLevels[i] *= 1.5f;
					component.stunLevels[i] *= 1.5f;
				}
				agent.maxSpeed = 3f;
				float[] armor = new float[] { 3f, 5f, 7f, 8f };
				Armor armorComp = agent.GetComponent<Armor>();
				if (!ReferenceEquals(armorComp, null))
					armorComp.armor = armor;
			}

			if (!ReferenceEquals(component2, null))
			{
				agent.maxSpeed = 2.5f;

				try
				{
					EnsureReflectionFields();
					if (!ReferenceEquals(LevelStateObjectReferences.dict, null) &&
						LevelStateObjectReferences.dict.TryGetValue("Viking_TankArcher", out UnityEngine.Object reference) &&
						reference is VikingReference vikingRef)
					{
						Component vikingTemplate = null;
						if (!ReferenceEquals(_vikingCloneField, null))
							vikingTemplate = _vikingCloneField.GetValue(vikingRef) as Component;
						if (ReferenceEquals(vikingTemplate, null) && !ReferenceEquals(_vikingField, null))
							vikingTemplate = _vikingField.GetValue(vikingRef) as Component;

						if (!ReferenceEquals(vikingTemplate, null))
						{
							Archery templateArchery = vikingTemplate.gameObject.GetComponent<Archery>();
							if (!ReferenceEquals(templateArchery, null))
							{
								if (!ReferenceEquals(_arrowPrefabField, null))
									_arrowPrefabField.SetValue(component2, _arrowPrefabField.GetValue(templateArchery));
								if (!ReferenceEquals(_trajectoryCalculatorField, null))
									_trajectoryCalculatorField.SetValue(component2, _trajectoryCalculatorField.GetValue(templateArchery));

								component2.drawSound = templateArchery.drawSound;
								component2.shootSound = templateArchery.shootSound;
							}
						}
					}
				}
				catch (Exception ex)
				{
					Plugin.Logger.LogWarning("[Titan] 模板复制失败: " + ex.Message);
				}

				for (int j = 0; j < component2._archerySettings.Length; j++)
				{
					component2._archerySettings[j].cooldown *= 1.3f;
					component2._archerySettings[j].spread *= 0.4f;
					component2._archerySettings[j].attackSettings.damage *= 1.5f;
					component2._archerySettings[j].attackSettings.knockback *= 1.1f;
					component2._archerySettings[j].attackSettings.stun *= 1.1f;
				}
				component2.Setup();

				float[] armor2 = new float[] { 2f, 3f, 4f, 5f };
				Armor armorComp2 = agent.GetComponent<Armor>();
				if (!ReferenceEquals(armorComp2, null))
					armorComp2.armor = armor2;
			}

			Stun stunComp = agent.GetComponent<Stun>();
			if (!ReferenceEquals(stunComp, null))
				stunComp.stunMultiplier = 1E-06f;
		}

		public static readonly string Titan_ID = "Hero_Trait_Titan";
	}
}