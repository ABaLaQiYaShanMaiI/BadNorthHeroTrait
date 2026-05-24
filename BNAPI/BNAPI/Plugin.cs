using System;
using System.Text;
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

			// 使用 StringBuilder 替代 string.Join 避免 Mono CLR 2.0 兼容性问题
			StringBuilder sb = new StringBuilder("======== BNAPI 已就绪，特性ID: ");
			for (int i = 0; i < CustomTraits.startingTraits.Count; i++)
			{
				if (i > 0) sb.Append(", ");
				sb.Append(CustomTraits.startingTraits[i]);
			}
			sb.Append(" ========");
			Plugin.logger.LogInfo(sb.ToString());
		}

		// Token: 0x04000005 RID: 5
		public static ManualLogSource logger;

		// Token: 0x04000006 RID: 6
		public const string VERSION = "1.0.1";
	}
}
