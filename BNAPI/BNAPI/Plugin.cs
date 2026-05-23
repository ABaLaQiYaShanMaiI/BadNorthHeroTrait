using System;
using BepInEx;
using BepInEx.Logging;

namespace BNAPI
{
	// Token: 0x02000007 RID: 7
	[BepInPlugin("nacu.bnapi", "BN Mod API", "1.0.1")]
	public class Plugin : BaseUnityPlugin
	{
		// Token: 0x0600000F RID: 15 RVA: 0x00002564 File Offset: 0x00000764
		public void OnEnable()
		{
			Plugin.logger = base.Logger;
			CustomText.ApplyHooks();
			CustomTraits.ApplyHooks();
			Plugin.logger.LogInfo("API loaded");
		}

		// Token: 0x04000005 RID: 5
		public static ManualLogSource logger;

		// Token: 0x04000006 RID: 6
		public const string VERSION = "1.0.1";
	}
}
