# 文件夹名：PlentyTraits（魔改版�?

**解析文件�?*�?3�?*总字符数**�?1,731

---

---

## 📄 AxeThrower.cs

**文件大小**: 6.8 KB  
**字符�?*: 6,795

```csharp
using System;
using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;
using Voxels.TowerDefense.SpriteMagic;

namespace PlentyTraits
{
	// Token: 0x02000002 RID: 2
	public class AxeThrower : HeroUpgradeDefinition
	{
		// Token: 0x06000001 RID: 1 RVA: 0x000022B8 File Offset: 0x000004B8
		public AxeThrower()
		{
			this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
			this.upgradeType.typeEnum = HeroUpgradeTypeEnum.Trait;
			this.upgradeType.canBeStartItem = true;
			this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
			this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
			this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
			this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
			this.affectsPortrait = false;
			base.name = AxeThrower.AXETHROWER_ID;
			this.nameTerm = "NACU/TRAIT/AXE/NAME";
			this.shortDescription = "NACU/TRAIT/AXE/DESCSHORT";
			this.infoSprite = CustomSprites.Sprites["trueaxe"];
			this.levels = new HeroUpgradeDefinition.Level[]
			{
				new HeroUpgradeDefinition.Level
				{
					cost = 0,
					description = "NACU/TRAIT/AXE/DESC"
				}
			};
		}

		// Token: 0x06000002 RID: 2 RVA: 0x0000239C File Offset: 0x0000059C
		public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
		{
			base.OnAppliedToSquad(squad, upgradeLevel);
			squad.onAgentSpawned += this.auv;
			this.ChangeSprite(squad.minionPrefab);
			this.nmsl(squad.heroAgent, squad.level);
			squad.heroAgent.GetOrAddComponent<LineOfSight>();
			AxeThrowing component = (LevelStateObjectReferences.dict["Viking_AxeThrower"] as VikingReference).viking.agent.GetComponent<AxeThrowing>();
			Archery component2 = (LevelStateObjectReferences.dict["Viking_TankArcher"] as VikingReference).viking.agent.GetComponent<Archery>();
			squad.heroAgent.gameObject.AddComponent<AxeThrowing>();
			AxeThrowing component3 = squad.heroAgent.GetComponent<AxeThrowing>();
			component3.prepareSound = component.prepareSound;
			component3.throwingAxePrefab = component.throwingAxePrefab;
			component3.trajectoryUtility = component2.trajectoryCalculator;
			component3.attackSettings = component.attackSettings;
			switch (squad.hero.squadLevel)
			{
			case 0:
			{
				component3.ammo = 500;
				AxeThrowing axeThrowing = component3;
				axeThrowing.attackSettings.launchImpulse = axeThrowing.attackSettings.launchImpulse * 1.5f;
				break;
			}
			case 1:
			{
				component3.ammo = 800;
				AxeThrowing axeThrowing2 = component3;
				axeThrowing2.attackSettings.damage = axeThrowing2.attackSettings.damage * 3f;
				AxeThrowing axeThrowing3 = component3;
				axeThrowing3.attackSettings.knockback = axeThrowing3.attackSettings.knockback * 2f;
				AxeThrowing axeThrowing4 = component3;
				axeThrowing4.attackSettings.stun = axeThrowing4.attackSettings.stun * 2f;
				break;
			}
			case 2:
			{
				component3.ammo = 1100;
				AxeThrowing axeThrowing5 = component3;
				axeThrowing5.attackSettings.damage = axeThrowing5.attackSettings.damage * 4f;
				AxeThrowing axeThrowing6 = component3;
				axeThrowing6.attackSettings.launchImpulse = axeThrowing6.attackSettings.launchImpulse * 3f;
				AxeThrowing axeThrowing7 = component3;
				axeThrowing7.attackSettings.knockback = axeThrowing7.attackSettings.knockback * 3f;
				AxeThrowing axeThrowing8 = component3;
				axeThrowing8.attackSettings.stun = axeThrowing8.attackSettings.stun * 4f;
				break;
			}
			default:
			{
				component3.ammo = 1400;
				AxeThrowing axeThrowing9 = component3;
				axeThrowing9.attackSettings.damage = axeThrowing9.attackSettings.damage * 6f;
				AxeThrowing axeThrowing10 = component3;
				axeThrowing10.attackSettings.launchImpulse = axeThrowing10.attackSettings.launchImpulse * 4f;
				AxeThrowing axeThrowing11 = component3;
				axeThrowing11.attackSettings.knockback = axeThrowing11.attackSettings.knockback * 4f;
				AxeThrowing axeThrowing12 = component3;
				axeThrowing12.attackSettings.stun = axeThrowing12.attackSettings.stun * 6f;
				break;
			}
			}
			component3.Setup();
		}

		// Token: 0x06000004 RID: 4 RVA: 0x00002674 File Offset: 0x00000874
		private void nmsl(Agent agent, int squadLevel)
		{
			float[] array = new float[]
			{
				1.175f,
				1.2f,
				1.225f,
				1.25f
			};
			float scale = agent.scale;
			agent.scale = array[squadLevel];
			agent.hurtSound = "Sfx/English/Tank/Hurt";
			Swordsman component = agent.GetComponent<Swordsman>();
			for (int i = 0; i < 4; i++)
			{
				component.damageLevels[i] = 10f;
				component.knockbackLevels[i] = 25f;
				component.stunLevels[i] = 10f;
			}
			agent.health = 10f;
			agent.maxSpeed = 3f;
			float[] armor = new float[]
			{
				15f,
				20f,
				25f,
				30f
			};
			agent.GetComponent<Armor>().armor = armor;
			agent.GetComponent<Stun>().stunMultiplier = 1E-06f;
			agent.body.baseMoveSoundRef = "Sfx/English/Tank/Move";
			component.swordSound = "Sfx/English/Tank";
			component.swingSound = "Sfx/English/Tank/Swing";
			agent.GetComponent<Death>().deathSound = "Sfx/English/Tank/Die";
		}

		// Token: 0x06000005 RID: 5 RVA: 0x0000276C File Offset: 0x0000096C
		public void auv(Agent agent)
		{
			Swordsman component = agent.GetComponent<Swordsman>();
			if (!agent.GetComponent<AxeThrowing>() && component)
			{
				agent.GetOrAddComponent<LineOfSight>();
				AxeThrowing component2 = (LevelStateObjectReferences.dict["Viking_AxeThrower"] as VikingReference).viking.agent.GetComponent<AxeThrowing>();
				Archery component3 = (LevelStateObjectReferences.dict["Viking_Archer"] as VikingReference).viking.agent.GetComponent<Archery>();
				AxeThrowing axeThrowing = agent.gameObject.AddComponent<AxeThrowing>();
				axeThrowing.prepareSound = component2.prepareSound;
				axeThrowing.throwingAxePrefab = component2.throwingAxePrefab;
				axeThrowing.trajectoryUtility = component3.trajectoryCalculator;
				axeThrowing.attackSettings = component2.attackSettings;
				axeThrowing.ammo = 500;
				axeThrowing.attackSettings.knockback = axeThrowing.attackSettings.knockback * 2f;
				axeThrowing.attackSettings.stun = axeThrowing.attackSettings.stun * 1.5f;
				axeThrowing.Setup();
			}
		}

		// Token: 0x06000006 RID: 6 RVA: 0x00002870 File Offset: 0x00000A70
		private void ChangeSprite(Agent agent)
		{
			SpriteAnimator componentInChildren = (LevelStateObjectReferences.dict["Viking_AxeThrower"] as VikingReference).viking.agent.GetComponentInChildren<SpriteAnimator>();
			agent.GetComponentInChildren<SpriteAnimator>().sprite = componentInChildren.sprite;
			agent.GetComponentInChildren<SpriteAnimator>().sprite2 = componentInChildren.sprite2;
		}

		// Token: 0x04000001 RID: 1
		public static readonly string AXETHROWER_ID = "Hero_Trait_AxeThrower";
	}
}
```
---

## 📄 Charge.cs

**文件大小**: 1.5 KB  
**字符�?*: 1,466

```csharp
using System;
using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;

namespace PlentyTraits
{
	// Token: 0x02000045 RID: 69
	public class Charge : HeroUpgradeDefinition
	{
		// Token: 0x0600003A RID: 58 RVA: 0x00003FA8 File Offset: 0x000021A8
		public Charge()
		{
			this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
			this.upgradeType.typeEnum = HeroUpgradeTypeEnum.Item;
			this.upgradeType.canBeStartItem = true;
			this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
			this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
			this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
			this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
			this.affectsPortrait = false;
			base.name = Charge.Charge_ID;
			this.nameTerm = "ABaLaQiYaShanMaiI/ITEM/CHARGE/NAME";
			this.shortDescription = "ABaLaQiYaShanMaiI/ITEM/CHARGE/DESCSHORT";
			this.infoSprite = CustomSprites.Sprites["charge"];
			this.levels = new HeroUpgradeDefinition.Level[]
			{
				new HeroUpgradeDefinition.Level
				{
					cost = 0,
					description = "ABaLaQiYaShanMaiI/ITEM/CHARGE/DESC"
				}
			};
		}

		// Token: 0x0600003B RID: 59 RVA: 0x00002292 File Offset: 0x00000492
		public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
		{
			squad.onAgentSpawned += new AxeThrower().auv;
		}

		// Token: 0x0400003C RID: 60
		public static readonly string Charge_ID = "Hero_Item_Charge";
	}
}
```
---

## 📄 CheaperClass.cs

**文件大小**: 1.5 KB  
**字符�?*: 1,459

```csharp
using System;
using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.upgrades;

namespace PlentyTraits
{
	// Token: 0x02000003 RID: 3
	public class CheaperClass : HeroTraitCheaperUpgrades
	{
		// Token: 0x06000007 RID: 7 RVA: 0x000028C4 File Offset: 0x00000AC4
		public CheaperClass()
		{
			Plugin.logger.LogInfo("CHEAPERCLASS CREATED");
			this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
			this.upgradeType.typeEnum = HeroUpgradeTypeEnum.Trait;
			this.upgradeType.canBeStartItem = true;
			this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
			this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
			this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
			this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
			this.affectsPortrait = false;
			base.name = CheaperClass.CHEAPERCLASS_ID;
			this.nameTerm = "NACU/TRAIT/CCLASS/NAME";
			this.shortDescription = "NACU/TRAIT/CCLASS/DESCSHORT";
			this.infoSprite = CustomSprites.Sprites["mesugaki"];
			this.levels = new HeroUpgradeDefinition.Level[]
			{
				new HeroUpgradeDefinition.Level
				{
					cost = 0,
					description = "NACU/TRAIT/CCLASS/DESC"
				}
			};
			this.discount = 0.4f;
			this.affectsType = HeroUpgradeTypeEnum.Class;
		}

		// Token: 0x04000002 RID: 2
		public static readonly string CHEAPERCLASS_ID = "Hero_Trait_CheaperClass";
	}
}
```
---

## 📄 Creeper.cs

**文件大小**: 3.5 KB  
**字符�?*: 3,438

```csharp
using System;
using System.Collections.Generic;
using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Upgrades;

namespace PlentyTraits
{
	// Token: 0x02000024 RID: 36
	public class Creeper : HeroUpgradeDefinition
	{
		// Token: 0x06000025 RID: 37 RVA: 0x000035FC File Offset: 0x000017FC
		public Creeper()
		{
			this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
			this.upgradeType.typeEnum = HeroUpgradeTypeEnum.Trait;
			this.upgradeType.canBeStartItem = true;
			this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
			this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
			this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
			this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
			this.affectsPortrait = false;
			base.name = Creeper.CREEPER_ID;
			this.nameTerm = "ABaLaQiYaShanMaiI/TRAIT/CREEPER/NAME";
			this.shortDescription = "ABaLaQiYaShanMaiI/TRAIT/CREEPER/DESCSHORT";
			this.infoSprite = CustomSprites.Sprites["creeper"];
			this.levels = new HeroUpgradeDefinition.Level[]
			{
				new HeroUpgradeDefinition.Level
				{
					cost = 0,
					description = "ABaLaQiYaShanMaiI/TRAIT/CREEPER/DESC"
				}
			};
		}

		// Token: 0x06000026 RID: 38 RVA: 0x00003700 File Offset: 0x00001900
		public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
		{
			squad.maxCount *= 2;
			squad.onAgentSpawned += this.Small;
			base.OnAppliedToSquad(squad, upgradeLevel);
			ReplenishAbility upgrade = squad.upgradeManager.GetUpgrade<ReplenishAbility>();
			if (upgrade)
			{
				upgrade.replenishTime *= 0.4f;
			}
		}

		// Token: 0x06000028 RID: 40 RVA: 0x0000375C File Offset: 0x0000195C
		private void CauseExplode(Agent agent)
		{
			Vector3 chestPos = agent.chestPos;
			if (agent.deadState.active)
			{
				this.DeathExplode(chestPos);
			}
		}

		// Token: 0x06000029 RID: 41 RVA: 0x00002219 File Offset: 0x00000419
		private void DeathExplode(Vector3 vector)
		{
		}

		// Token: 0x0600002A RID: 42 RVA: 0x00003784 File Offset: 0x00001984
		private void ModifyFloatList(List<float> list, float multiplier)
		{
			for (int i = 0; i < list.Count; i++)
			{
				int index = i;
				list[index] *= multiplier;
			}
		}

		// Token: 0x0600002B RID: 43 RVA: 0x000037B8 File Offset: 0x000019B8
		private void ModifyFloatList(float[] list, float multiplier)
		{
			for (int i = 0; i < list.Length; i++)
			{
				list[i] *= multiplier;
			}
		}

		// Token: 0x0600002C RID: 44 RVA: 0x000037E0 File Offset: 0x000019E0
		public void Small(Agent agent)
		{
			agent.scale = 0.85f;
			agent.maxHealth *= 0.6f;
			agent.health *= 0.6f;
			agent.maxSpeed *= 1.4f;
			Swordsman component = agent.GetComponent<Swordsman>();
			if (component)
			{
				this.ModifyFloatList(component.damageLevels, 0.6f);
				this.ModifyFloatList(component.knockbackLevels, 0.6f);
				this.ModifyFloatList(component.stunLevels, 0.6f);
			}
			Archery component2 = agent.GetComponent<Archery>();
			if (component2)
			{
				for (int i = 0; i < component2._archerySettings.Length; i++)
				{
					component2._archerySettings[i].spread = component2._archerySettings[i].spread * 1.1f;
				}
			}
		}

		// Token: 0x04000026 RID: 38
		public static readonly string CREEPER_ID = "Hero_Trait_Creeper";

		// Token: 0x04000027 RID: 39
		[SerializeField]
		private AttackSettings attackSettings = new AttackSettings(3f, 2.5f, 0f, 2.5f);
	}
}
```
---

## 📄 Flyer.cs

**文件大小**: 6.4 KB  
**字符�?*: 6,388

```csharp
using System;
using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Ballistics;

namespace PlentyTraits
{
	// Token: 0x02000025 RID: 37
	public class Flyer : HeroUpgradeDefinition
	{
		// Token: 0x0600002D RID: 45 RVA: 0x000038BC File Offset: 0x00001ABC
		public Flyer()
		{
			this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
			this.upgradeType.typeEnum = HeroUpgradeTypeEnum.Trait;
			this.upgradeType.canBeStartItem = true;
			this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
			this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
			this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
			this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
			this.affectsPortrait = false;
			base.name = Flyer.FLYER_ID;
			this.nameTerm = "ABaLaQiYaShanMaiI/TRAIT/FLYER/NAME";
			this.shortDescription = "ABaLaQiYaShanMaiI/TRAIT/FLYER/DESCSHORT";
			this.infoSprite = CustomSprites.Sprites["mystory"];
			this.levels = new HeroUpgradeDefinition.Level[]
			{
				new HeroUpgradeDefinition.Level
				{
					cost = 0,
					description = "ABaLaQiYaShanMaiI/TRAIT/FLYER/DESC"
				}
			};
		}

		// Token: 0x0600002E RID: 46 RVA: 0x0000221B File Offset: 0x0000041B
		public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
		{
			this.Heroaxe(squad.heroAgent);
			this.HeroEagle(squad.heroAgent);
			this.Slow(squad.minionPrefab);
			this.CopyTwoHanded(squad.heroAgent);
		}

		// Token: 0x06000030 RID: 48 RVA: 0x000039A0 File Offset: 0x00001BA0
		public void Heroaxe(Agent agent)
		{
			if (!agent.GetComponent<AxeThrowing>())
			{
				agent.GetOrAddComponent<LineOfSight>();
				AxeThrowing component = (LevelStateObjectReferences.dict["Viking_AxeThrower"] as VikingReference).viking.agent.GetComponent<AxeThrowing>();
				AxeThrowing axeThrowing = agent.gameObject.AddComponent<AxeThrowing>();
				axeThrowing.prepareSound = component.prepareSound;
				axeThrowing.throwingAxePrefab = component.throwingAxePrefab;
				axeThrowing.trajectoryUtility = component.trajectoryUtility;
				axeThrowing.attackSettings = component.attackSettings;
				axeThrowing.ammo = 500;
				axeThrowing.attackSettings.damage = axeThrowing.attackSettings.damage * 0.5f;
				axeThrowing.attackSettings.launchImpulse = 9f;
				axeThrowing.Setup();
			}
		}

		// Token: 0x06000031 RID: 49 RVA: 0x00003A64 File Offset: 0x00001C64
		public void HeroJump(Agent agent)
		{
			if (!agent.GetComponent<JumpAttack>())
			{
				JumpAttack component = (LevelStateObjectReferences.dict["Viking_Twohanded"] as VikingReference).viking.agent.GetComponent<JumpAttack>();
				JumpAttack jumpAttack = agent.gameObject.AddComponent<JumpAttack>();
				jumpAttack.fabricLaunchID = component.fabricLaunchID;
				jumpAttack.fabricLandHitID = component.fabricLandHitID;
				jumpAttack.fabricLandMissID = component.fabricLandMissID;
				jumpAttack.fabricLandShieldID = component.fabricLandShieldID;
				jumpAttack.attackSettings = component.attackSettings;
				jumpAttack.attackAnimId = component.attackAnimId;
				jumpAttack.plungeJumpId = component.plungeJumpId;
				jumpAttack.attackSettings.damage = jumpAttack.attackSettings.damage * 0.9f;
				jumpAttack.attackSettings.launchImpulse = 9f;
				JumpAttack jumpAttack2 = jumpAttack;
				jumpAttack2.attackSettings.stun = jumpAttack2.attackSettings.stun * 3f;
				jumpAttack.landPos = component.landPos;
				if (!agent.GetComponent<JumpComponent>())
				{
					agent.gameObject.AddComponent<JumpComponent>();
				}
				jumpAttack.Setup();
				Swordsman component2 = agent.GetComponent<Swordsman>();
				if (component2 != null && !component2.actions.Contains(jumpAttack))
				{
					component2.actions.Add(jumpAttack);
				}
			}
		}

		// Token: 0x06000032 RID: 50 RVA: 0x00003BA4 File Offset: 0x00001DA4
		private void HeroEagle(Agent agent)
		{
			int num = 3;
			agent.GetComponent<Swordsman>().knockbackLevels[num] *= 2f;
			agent.GetComponent<Swordsman>().Wannafly = 5f;
			agent.maxSpeed = 8f;
		}

		// Token: 0x06000033 RID: 51 RVA: 0x00003BE8 File Offset: 0x00001DE8
		private void Slow(Agent agent)
		{
			agent.maxSpeed = 2.5f;
			Swordsman component = agent.GetComponent<Swordsman>();
			Spear component2 = agent.GetComponent<Spear>();
			Archery component3 = agent.GetComponent<Archery>();
			if (component)
			{
				component.Wannafly = 5f;
			}
			if (component2)
			{
				component2.spearfly = 4f;
			}
			if (component3)
			{
				component3.archerfly = 3f;
			}
		}

		// Token: 0x06000034 RID: 52 RVA: 0x00003434 File Offset: 0x00001634
		private void CopyAnimatorParameters(Animator source, Animator target)
		{
			foreach (AnimatorControllerParameter animatorControllerParameter in source.parameters)
			{
				if (animatorControllerParameter.type == AnimatorControllerParameterType.Bool)
				{
					target.SetBool(animatorControllerParameter.name, source.GetBool(animatorControllerParameter.name));
				}
				else if (animatorControllerParameter.type == AnimatorControllerParameterType.Float)
				{
					target.SetFloat(animatorControllerParameter.name, source.GetFloat(animatorControllerParameter.name));
				}
				else if (animatorControllerParameter.type == AnimatorControllerParameterType.Int)
				{
					target.SetInteger(animatorControllerParameter.name, source.GetInteger(animatorControllerParameter.name));
				}
				else if (animatorControllerParameter.type == AnimatorControllerParameterType.Trigger && source.GetBool(animatorControllerParameter.name))
				{
					target.SetTrigger(animatorControllerParameter.name);
				}
			}
		}

		// Token: 0x06000035 RID: 53 RVA: 0x00003C50 File Offset: 0x00001E50
		private void CopyTwoHanded(Agent myAgent)
		{
			Animator component = (LevelStateObjectReferences.dict["Viking_Twohanded"] as VikingReference).viking.agent.GetComponent<Animator>();
			RuntimeAnimatorController runtimeAnimatorController = component.runtimeAnimatorController;
			Animator component2 = myAgent.GetComponent<Animator>();
			component2.runtimeAnimatorController = runtimeAnimatorController;
			this.CopyAnimatorParameters(component, component2);
			component2.updateMode = component.updateMode;
			component2.cullingMode = component.cullingMode;
			component2.applyRootMotion = component.applyRootMotion;
		}

		// Token: 0x04000028 RID: 40
		public static readonly string FLYER_ID = "Hero_Trait_Flyer";
	}
}
```
---

## 📄 Jumper.cs

**文件大小**: 6.0 KB  
**字符�?*: 5,981

```csharp
using System;
using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.SpriteMagic;

namespace PlentyTraits
{
	// Token: 0x02000023 RID: 35
	public class Jumper : HeroUpgradeDefinition
	{
		// Token: 0x0600001E RID: 30 RVA: 0x00003154 File Offset: 0x00001354
		public Jumper()
		{
			this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
			this.upgradeType.typeEnum = HeroUpgradeTypeEnum.Trait;
			this.upgradeType.canBeStartItem = true;
			this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
			this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
			this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
			this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
			this.affectsPortrait = false;
			base.name = Jumper.JUMPER_ID;
			this.nameTerm = "ABaLaQiYaShanMaiI/TRAIT/JUMP/NAME";
			this.shortDescription = "ABaLaQiYaShanMaiI/TRAIT/JUMP/DESCSHORT";
			this.infoSprite = CustomSprites.Sprites["jump"];
			this.levels = new HeroUpgradeDefinition.Level[]
			{
				new HeroUpgradeDefinition.Level
				{
					cost = 0,
					description = "ABaLaQiYaShanMaiI/TRAIT/JUMP/DESC"
				}
			};
		}

		// Token: 0x0600001F RID: 31 RVA: 0x00003238 File Offset: 0x00001438
		public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
		{
			this.Ewaixue(squad.minionPrefab, squad.level);
			this.Ewaixue(squad.heroAgent, squad.level);
			this.CopyTwoHanded(squad.minionPrefab);
			this.CopyTwoHanded(squad.heroAgent);
			squad.onAgentSpawned += this.AddJumpAttackToAgent;
		}

		// Token: 0x06000021 RID: 33 RVA: 0x00003294 File Offset: 0x00001494
		public void AddJumpAttackToAgent(Agent agent)
		{
			Swordsman component = agent.GetComponent<Swordsman>();
			if (!agent.GetComponent<JumpAttack>() && component)
			{
				JumpAttack component2 = (LevelStateObjectReferences.dict["Viking_Twohanded"] as VikingReference).viking.agent.GetComponent<JumpAttack>();
				JumpAttack jumpAttack = agent.gameObject.AddComponent<JumpAttack>();
				jumpAttack.fabricLaunchID = component2.fabricLaunchID;
				jumpAttack.fabricLandHitID = component2.fabricLandHitID;
				jumpAttack.fabricLandMissID = component2.fabricLandMissID;
				jumpAttack.fabricLandShieldID = component2.fabricLandShieldID;
				jumpAttack.attackSettings = component2.attackSettings;
				jumpAttack.attackAnimId = component2.attackAnimId;
				jumpAttack.plungeJumpId = component2.plungeJumpId;
				jumpAttack.attackSettings.damage = jumpAttack.attackSettings.damage * 1f;
				JumpAttack jumpAttack2 = jumpAttack;
				jumpAttack2.attackSettings.stun = jumpAttack2.attackSettings.stun * 3f;
				jumpAttack.landPos = component2.landPos;
				if (!agent.GetComponent<JumpComponent>())
				{
					agent.gameObject.AddComponent<JumpComponent>();
				}
				jumpAttack.Setup();
				Swordsman component3 = agent.GetComponent<Swordsman>();
				if (component3 != null && !component3.actions.Contains(jumpAttack))
				{
					component3.actions.Add(jumpAttack);
				}
			}
		}

		// Token: 0x06000022 RID: 34 RVA: 0x000033DC File Offset: 0x000015DC
		public void Ewaixue(Agent agent, int level)
		{
			Swordsman component = agent.GetComponent<Swordsman>();
			if (component && level == 0)
			{
				component.agent.health = 3f;
				component.agent.maxHealth = 3f;
			}
			if (component)
			{
				component.agent.maxSpeed = 3.5f;
			}
		}

		// Token: 0x06000023 RID: 35 RVA: 0x00003434 File Offset: 0x00001634
		private void CopyAnimatorParameters(Animator source, Animator target)
		{
			foreach (AnimatorControllerParameter animatorControllerParameter in source.parameters)
			{
				if (animatorControllerParameter.type == AnimatorControllerParameterType.Bool)
				{
					target.SetBool(animatorControllerParameter.name, source.GetBool(animatorControllerParameter.name));
				}
				else if (animatorControllerParameter.type == AnimatorControllerParameterType.Float)
				{
					target.SetFloat(animatorControllerParameter.name, source.GetFloat(animatorControllerParameter.name));
				}
				else if (animatorControllerParameter.type == AnimatorControllerParameterType.Int)
				{
					target.SetInteger(animatorControllerParameter.name, source.GetInteger(animatorControllerParameter.name));
				}
				else if (animatorControllerParameter.type == AnimatorControllerParameterType.Trigger && source.GetBool(animatorControllerParameter.name))
				{
					target.SetTrigger(animatorControllerParameter.name);
				}
			}
		}

		// Token: 0x06000024 RID: 36 RVA: 0x000034F0 File Offset: 0x000016F0
		public void CopyTwoHanded(Agent myAgent)
		{
			if (myAgent.GetComponent<Swordsman>())
			{
				Animator component = (LevelStateObjectReferences.dict["Viking_Twohanded"] as VikingReference).viking.agent.GetComponent<Animator>();
				RuntimeAnimatorController runtimeAnimatorController = component.runtimeAnimatorController;
				Animator component2 = myAgent.GetComponent<Animator>();
				component2.runtimeAnimatorController = runtimeAnimatorController;
				this.CopyAnimatorParameters(component, component2);
				component2.updateMode = component.updateMode;
				component2.cullingMode = component.cullingMode;
				component2.applyRootMotion = component.applyRootMotion;
				Swordsman component3 = (LevelStateObjectReferences.dict["Viking_Twohanded"] as VikingReference).viking.agent.GetComponent<Swordsman>();
				Swordsman component4 = myAgent.GetComponent<Swordsman>();
				component4.swingSound = component3.swingSound;
				component4.swordSound = component3.swordSound;
				SpriteAnimator componentInChildren = (LevelStateObjectReferences.dict["Viking_Twohanded"] as VikingReference).viking.agent.GetComponentInChildren<SpriteAnimator>();
				myAgent.GetComponentInChildren<SpriteAnimator>().sprite = componentInChildren.sprite;
				myAgent.GetComponentInChildren<SpriteAnimator>().sprite2 = componentInChildren.sprite2;
			}
		}

		// Token: 0x04000025 RID: 37
		public static readonly string JUMPER_ID = "Hero_Trait_Jumper";
	}
}
```
---

## ⏭️ PlentyTraits.csproj

**不支持的格式** | **文件大小**: 3.1 KB

> *This file format is not supported.*
---

## 📄 Plugin.cs

**文件大小**: 5.4 KB  
**字符�?*: 5,081

```csharp
using System;
using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using BNAPI;
using On.Voxels.TowerDefense.Upgrades;
using UnityEngine;
using Voxels.TowerDefense.Upgrades;

namespace PlentyTraits
{
	// Token: 0x02000004 RID: 4
	[BepInDependency("nacu.bnapi", 1)]
	[BepInPlugin("nacu.plentytraits", "Plenty Traits", "1.0")]
	public class Plugin : BaseUnityPlugin
	{
		// Token: 0x06000009 RID: 9 RVA: 0x000029CC File Offset: 0x00000BCC
		public void OnEnable()
		{
			Plugin.logger = base.Logger;
			string text = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) + "\\";
			CustomSprites.AddCustomSprite(text, "trueaxe");
			CustomSprites.AddCustomSprite(text, "trait_thorns");
			CustomSprites.AddCustomSprite(text, "mesugaki");
			CustomSprites.AddCustomSprite(text, "trait_regenerative");
			CustomSprites.AddCustomSprite(text, "jump");
			CustomSprites.AddCustomSprite(text, "creeper");
			CustomSprites.AddCustomSprite(text, "mystory");
			CustomSprites.AddCustomSprite(text, "titan");
			CustomSprites.AddCustomSprite(text, "charge");
			CustomTraits.RegisterTrait(ScriptableObject.CreateInstance<AxeThrower>(), AxeThrower.AXETHROWER_ID, true);
			CustomTraits.RegisterTrait(ScriptableObject.CreateInstance<Thorns>(), Thorns.THORNS_ID, false);
			CustomTraits.RegisterTrait(ScriptableObject.CreateInstance<Regenerative>(), Regenerative.REGENERATIVE_ID, false);
			CustomTraits.RegisterTrait(ScriptableObject.CreateInstance<CheaperClass>(), CheaperClass.CHEAPERCLASS_ID, true);
			CustomTraits.RegisterTrait(ScriptableObject.CreateInstance<Jumper>(), Jumper.JUMPER_ID, true);
			CustomTraits.RegisterTrait(ScriptableObject.CreateInstance<Creeper>(), Creeper.CREEPER_ID, true);
			CustomTraits.RegisterTrait(ScriptableObject.CreateInstance<Flyer>(), Flyer.FLYER_ID, true);
			CustomTraits.RegisterTrait(ScriptableObject.CreateInstance<Titan>(), Titan.Titan_ID, true);
			CustomTraits.RegisterTrait(ScriptableObject.CreateInstance<Charge>(), Charge.Charge_ID, true);
			CustomText.CustomTermsAdded += this.AddCustomTerms;
			HouseTargetableAbility.GetNotificationTerm += new HouseTargetableAbility.hook_GetNotificationTerm(this.HouseTargetableAbility_GetNotificationTerm);
			Plugin.logger.LogInfo("Plenty Traits loaded");
		}

		// Token: 0x0600000A RID: 10 RVA: 0x00002B38 File Offset: 0x00000D38
		private string HouseTargetableAbility_GetNotificationTerm(HouseTargetableAbility.orig_GetNotificationTerm orig, HouseTargetableAbility self, out string pn, out string pv)
		{
			bool isBanned = self.isBanned;
			string result;
			if (isBanned)
			{
				string text;
				pv = (text = null);
				pn = text;
				result = self.bannedTooltip;
			}
			else
			{
				result = orig.Invoke(self, ref pn, ref pv);
			}
			return result;
		}

		// Token: 0x0600000B RID: 11 RVA: 0x00002B74 File Offset: 0x00000D74
		private void AddCustomTerms()
		{
			CustomText.AddCustomTerm("NACU/TRAIT/AXE/NAME", "投斧大队");
			CustomText.AddCustomTerm("NACU/TRAIT/AXE/DESCSHORT", "步兵全给我扔");
			CustomText.AddCustomTerm("NACU/TRAIT/AXE/DESC", "扔斧头\n升级步兵全都�?);
			CustomText.AddCustomTerm("NACU/TRAIT/THORNS/NAME", "荆棘");
			CustomText.AddCustomTerm("NACU/TRAIT/THORNS/DESCSHORT", "反弹近战和跳�?);
			CustomText.AddCustomTerm("NACU/TRAIT/THORNS/DESC", "近战和跳劈攻击会被反伤，不计入击杀�?);
			CustomText.AddCustomTerm("NACU/TRAIT/CCLASS/NAME", "快速精�?);
			CustomText.AddCustomTerm("NACU/TRAIT/CCLASS/DESCSHORT", "常规升级更便�?);
			CustomText.AddCustomTerm("NACU/TRAIT/CCLASS/DESC", "英雄兵种升级打六�?);
			CustomText.AddCustomTerm("NACU/TRAIT/REGENERATIVE/NAME", "医疗训练");
			CustomText.AddCustomTerm("NACU/TRAIT/REGENERATIVE/DESCSHORT", "单位再生血�?);
			CustomText.AddCustomTerm("NACU/TRAIT/REGENERATIVE/DESC", "All units passively regenerate lost health.\nSquad can't replenish at houses.");
			CustomText.AddCustomTerm("NACU/HERO_TRAITS/REGENERATIVE/ABILITY_TOOLTIP", "Medical Training squads can't replenish.");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/JUMP/NAME", "跳劈大队");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/JUMP/DESCSHORT", "像双刀一样劈");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/JUMP/DESC", "步兵可以像双刀一样跳着劈砍");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/CREEPER/NAME", "短人部队");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/CREEPER/DESCSHORT", "霍克斯矮子为您效�?);
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/CREEPER/DESC", "士兵体型小数值低，但人数多恢复快");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/FLYER/NAME", "神鹰");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/FLYER/DESCSHORT", "让敌人飞起来");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/FLYER/DESC", "产生让敌人升天的力量");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/TITAN/NAME", "泰坦");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/TITAN/DESCSHORT", "真正的巨人之力，盾弓皆可，升级后起效");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/TRAIT/TITAN/DESC", "步兵弓箭手都可以是巨�?);
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/ITEM/CHARGE/NAME", "盾冲");
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/ITEM/CHARGE/DESCSHORT", "步兵习得新技�?);
			CustomText.AddCustomTerm("ABaLaQiYaShanMaiI/ITEM/CHARGE/DESC", "步兵可以用盾撞击");
		}

		// Token: 0x04000003 RID: 3
		public static ManualLogSource logger;

		// Token: 0x04000004 RID: 4
		public const string VERSION = "1.0";
	}
}
```
---

## 📄 Regenerative.cs

**文件大小**: 3.0 KB  
**字符�?*: 3,019

```csharp
using System;
using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;
using Voxels.TowerDefense.Upgrades;

namespace PlentyTraits
{
	// Token: 0x02000005 RID: 5
	public class Regenerative : HeroUpgradeDefinition
	{
		// Token: 0x0600000D RID: 13 RVA: 0x00002D28 File Offset: 0x00000F28
		public Regenerative()
		{
			Plugin.logger.LogInfo("REGENERATIVE CREATED");
			this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
			this.upgradeType.typeEnum = HeroUpgradeTypeEnum.Trait;
			this.upgradeType.canBeStartItem = true;
			this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
			this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
			this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
			this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
			this.affectsPortrait = false;
			base.name = Regenerative.REGENERATIVE_ID;
			this.nameTerm = "NACU/TRAIT/REGENERATIVE/NAME";
			this.shortDescription = "NACU/TRAIT/REGENERATIVE/DESCSHORT";
			this.infoSprite = CustomSprites.Sprites["trait_regenerative"];
			this.levels = new HeroUpgradeDefinition.Level[]
			{
				new HeroUpgradeDefinition.Level
				{
					cost = 0,
					description = "NACU/TRAIT/REGENERATIVE/DESC"
				}
			};
		}

		// Token: 0x0600000E RID: 14 RVA: 0x00002E1C File Offset: 0x0000101C
		public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
		{
			base.OnAppliedToSquad(squad, upgradeLevel);
			squad.heroAgent.GetOrAddComponent<SelfHealing>();
			squad.minionPrefab.GetOrAddComponent<SelfHealing>();
			ReplenishAbility upgrade = squad.upgradeManager.GetUpgrade<ReplenishAbility>();
			if (upgrade)
			{
				upgrade.BanAbility("NACU/HERO_TRAITS/REGENERATIVE/ABILITY_TOOLTIP", null, null);
			}
		}

		// Token: 0x06000010 RID: 16 RVA: 0x00002E6C File Offset: 0x0000106C
		private void truegiaont(Agent agent)
		{
			agent.scale = 1.25f;
			Swordsman component = agent.GetComponent<Swordsman>();
			Archery component2 = agent.GetComponent<Archery>();
			if (component)
			{
				for (int i = 0; i < 4; i++)
				{
					component.damageLevels[i] += 0.3f;
					component.knockbackLevels[i] *= 2f;
					component.stunLevels[i] += 0.2f;
					component.agent.maxSpeed = 3.5f;
				}
			}
			if (component2)
			{
				agent.maxSpeed = 2.5f;
				Archery component3 = (LevelStateObjectReferences.dict["Viking_TankArcher"] as VikingReference).viking.agent.GetComponent<Archery>();
				component2.arrowPrefab = component3.arrowPrefab;
				component2.drawSound = component3.drawSound;
				component2.shootSound = component3.shootSound;
				component2.trajectoryCalculator = component3.trajectoryCalculator;
				component2.Setup();
			}
			float[] armor = new float[]
			{
				3f,
				5f,
				7f,
				8f
			};
			agent.GetComponent<Armor>().armor = armor;
			agent.GetComponent<Stun>().stunMultiplier = 1E-06f;
		}

		// Token: 0x04000005 RID: 5
		public static readonly string REGENERATIVE_ID = "Hero_Trait_Regenerative";
	}
}
```
---

## 📄 SelfHealing.cs

**文件大小**: 667 B  
**字符�?*: 637

```csharp
using System;
using UnityEngine;
using Voxels.TowerDefense;

namespace PlentyTraits
{
	// Token: 0x02000006 RID: 6
	public class SelfHealing : AgentComponent
	{
		// Token: 0x06000012 RID: 18 RVA: 0x00002122 File Offset: 0x00000322
		public override void Setup()
		{
			this.healing = new AgentState("SelfHealing", base.agent.aliveAndGrounded, true, false);
			this.healing.OnUpdate += delegate()
			{
				base.agent.health = Mathf.Min(base.agent.maxHealth, base.agent.health + this.healingRate);
			};
		}

		// Token: 0x04000006 RID: 6
		public AgentState healing;

		// Token: 0x04000007 RID: 7
		public float healingRate = 1.2f;
	}
}
```
---

## 📄 Thorns.cs

**文件大小**: 2.6 KB  
**字符�?*: 2,567

```csharp
using System;
using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;

namespace PlentyTraits
{
	// Token: 0x02000007 RID: 7
	public class Thorns : HeroUpgradeDefinition, IAttackResponder
	{
		// Token: 0x06000014 RID: 20 RVA: 0x00002F98 File Offset: 0x00001198
		public Thorns()
		{
			Plugin.logger.LogInfo("THORNS CREATED");
			this.upgradeType = ScriptableObject.CreateInstance<HeroUpgradeType>();
			this.upgradeType.typeEnum = HeroUpgradeTypeEnum.Trait;
			this.upgradeType.canBeStartItem = true;
			this.upgradeType.unknownNameTerm = "META_INVENTORY/UNKNOWN/TRAIT/NAME";
			this.upgradeType.unknownDescriptionTerm = "META_INVENTORY/UNKNOWN/TRAIT/DESC";
			this.upgradeType.startItemLockedTerm = "META_INVENTORY/START/TRAIT/LOCKED";
			this.upgradeType.startItemUnlockedTerm = "META_INVENTORY/START/TRAIT/UNLOCKED";
			this.affectsPortrait = false;
			base.name = Thorns.THORNS_ID;
			this.nameTerm = "NACU/TRAIT/THORNS/NAME";
			this.shortDescription = "NACU/TRAIT/THORNS/DESCSHORT";
			this.infoSprite = CustomSprites.Sprites["trait_thorns"];
			this.levels = new HeroUpgradeDefinition.Level[]
			{
				new HeroUpgradeDefinition.Level
				{
					cost = 0,
					description = "NACU/TRAIT/THORNS/DESC"
				}
			};
		}

		// Token: 0x06000015 RID: 21 RVA: 0x0000218A File Offset: 0x0000038A
		public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
		{
			base.OnAppliedToSquad(squad, upgradeLevel);
			squad.onAgentCreated += this.AddThing;
		}

		// Token: 0x06000016 RID: 22 RVA: 0x000021A6 File Offset: 0x000003A6
		public void AddThing(Agent agent)
		{
			if (!agent.attackResponders.Contains(this))
			{
				agent.attackResponders.Add(this);
			}
		}

		// Token: 0x06000017 RID: 23 RVA: 0x0000308C File Offset: 0x0000128C
		public void ModifyAttack(ref Attack attack)
		{
			CloseCombatBrain closeCombatBrain = attack.monoAttacker as CloseCombatBrain;
			JumpAttack jumpAttack = attack.monoAttacker as JumpAttack;
			if (closeCombatBrain != null)
			{
				closeCombatBrain.agent.DealDamage(new Attack(1.5f, 1.5f, 2.5f, -attack.direction, attack.pos, closeCombatBrain, closeCombatBrain.enSquad, "Sfx/English/Sword", ScriptableObjectSingleton<PrefabManager>.instance.hitEffect));
			}
			if (jumpAttack != null)
			{
				jumpAttack.agent.DealDamage(new Attack(1.5f, 1.5f, 2.5f, -attack.direction, attack.pos, jumpAttack, jumpAttack.enSquad, "Sfx/English/Sword", ScriptableObjectSingleton<PrefabManager>.instance.hitEffect));
			}
		}

		// Token: 0x04000008 RID: 8
		public static readonly string THORNS_ID = "Hero_Trait_Thorns";
	}
}
```
---

## 📄 Titan.cs

**文件大小**: 3.8 KB  
**字符�?*: 3,800

```csharp
using System;
using BNAPI;
using UnityEngine;
using Voxels.TowerDefense;

namespace PlentyTraits
{
	// Token: 0x02000038 RID: 56
	public class Titan : HeroUpgradeDefinition
	{
		// Token: 0x06000036 RID: 54 RVA: 0x00003CC4 File Offset: 0x00001EC4
		public Titan()
		{
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
			this.infoSprite = CustomSprites.Sprites["titan"];
			this.levels = new HeroUpgradeDefinition.Level[]
			{
				new HeroUpgradeDefinition.Level
				{
					cost = 0,
					description = "ABaLaQiYaShanMaiI/TRAIT/TITAN/DESC"
				}
			};
		}

		// Token: 0x06000037 RID: 55 RVA: 0x00002259 File Offset: 0x00000459
		public override void OnAppliedToSquad(EnglishSquad squad, int upgradeLevel)
		{
			if (squad.level >= 1)
			{
				squad.maxCount = squad.maxCount / 2 + 1;
				squad.onAgentSpawned += this.Titanize;
			}
		}

		// Token: 0x06000039 RID: 57 RVA: 0x00003DA8 File Offset: 0x00001FA8
		private void Titanize(Agent agent)
		{
			agent.scale = 1.25f;
			Swordsman component = agent.GetComponent<Swordsman>();
			Archery component2 = agent.GetComponent<Archery>();
			if (component)
			{
				for (int i = 0; i < 4; i++)
				{
					component.damageLevels[i] *= 2f;
					component.knockbackLevels[i] *= 1.5f;
					component.stunLevels[i] *= 1.5f;
					component.agent.maxSpeed = 3f;
				}
				float[] armor = new float[]
				{
					3f,
					5f,
					7f,
					8f
				};
				agent.GetComponent<Armor>().armor = armor;
			}
			if (component2)
			{
				agent.maxSpeed = 2.5f;
				Archery component3 = (LevelStateObjectReferences.dict["Viking_TankArcher"] as VikingReference).viking.agent.GetComponent<Archery>();
				component2.arrowPrefab = component3.arrowPrefab;
				component2.drawSound = component3.drawSound;
				component2.shootSound = component3.shootSound;
				component2.trajectoryCalculator = component3.trajectoryCalculator;
				for (int j = 0; j < component2._archerySettings.Length; j++)
				{
					Archery.ArcherySettings[] archerySettings = component2._archerySettings;
					int num = j;
					archerySettings[num].cooldown = archerySettings[num].cooldown * 1.3f;
					Archery.ArcherySettings[] archerySettings2 = component2._archerySettings;
					int num2 = j;
					archerySettings2[num2].spread = archerySettings2[num2].spread * 0.4f;
					Archery.ArcherySettings[] archerySettings3 = component2._archerySettings;
					int num3 = j;
					archerySettings3[num3].attackSettings.damage = archerySettings3[num3].attackSettings.damage * 1.5f;
					Archery.ArcherySettings[] archerySettings4 = component2._archerySettings;
					int num4 = j;
					archerySettings4[num4].attackSettings.knockback = archerySettings4[num4].attackSettings.knockback * 1.1f;
					Archery.ArcherySettings[] archerySettings5 = component2._archerySettings;
					int num5 = j;
					archerySettings5[num5].attackSettings.stun = archerySettings5[num5].attackSettings.stun * 1.1f;
				}
				component2.Setup();
				float[] armor2 = new float[]
				{
					2f,
					3f,
					4f,
					5f
				};
				agent.GetComponent<Armor>().armor = armor2;
			}
			agent.GetComponent<Stun>().stunMultiplier = 1E-06f;
		}

		// Token: 0x04000032 RID: 50
		public static readonly string Titan_ID = "Hero_Trait_Titan";
	}
}
```
---

## 📄 Properties/AssemblyInfo.cs

**文件大小**: 335 B  
**字符�?*: 321

```csharp
using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;

[assembly: AssemblyVersion("1.0.0.0")]
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
```
---

## 📄 System/Runtime/Versioning/TargetFrameworkAttribute.cs

**文件大小**: 807 B  
**字符�?*: 779

```csharp
using System;

namespace System.Runtime.Versioning
{
	// Token: 0x02000008 RID: 8
	public class TargetFrameworkAttribute : Attribute
	{
		// Token: 0x17000001 RID: 1
		// (get) Token: 0x06000019 RID: 25 RVA: 0x000021CE File Offset: 0x000003CE
		// (set) Token: 0x0600001A RID: 26 RVA: 0x000021D6 File Offset: 0x000003D6
		public string FrameworkName { get; set; }

		// Token: 0x17000002 RID: 2
		// (get) Token: 0x0600001B RID: 27 RVA: 0x000021DF File Offset: 0x000003DF
		// (set) Token: 0x0600001C RID: 28 RVA: 0x000021E7 File Offset: 0x000003E7
		public string FrameworkDisplayName { get; set; }

		// Token: 0x0600001D RID: 29 RVA: 0x000021F0 File Offset: 0x000003F0
		public TargetFrameworkAttribute(string frameworkName)
		{
			this.FrameworkName = frameworkName;
		}
	}
}
```
