using System;
using System.Runtime.CompilerServices;

namespace BNAPI
{
    // 自定义委托，替代 System.Action
    public delegate void CustomTermsAddedDelegate();

    public static class CustomText
    {
        public static event CustomTermsAddedDelegate CustomTermsAdded;

        internal static void ApplyHooks()
        {
            On.I2.Loc.LocalizationManager.hook_AddSource hook_AddSource;
            if ((hook_AddSource = CustomText.Hooks.LocalizationManager_AddSourceHook) == null)
            {
                hook_AddSource = (CustomText.Hooks.LocalizationManager_AddSourceHook = new On.I2.Loc.LocalizationManager.hook_AddSource(CustomText.LocalizationManager_AddSource));
            }
            On.I2.Loc.LocalizationManager.AddSource += hook_AddSource;
        }

        private static void LocalizationManager_AddSource(On.I2.Loc.LocalizationManager.orig_AddSource orig, I2.Loc.LanguageSourceData source)
        {
            orig.Invoke(source);
            CustomTermsAddedDelegate customTermsAdded = CustomText.CustomTermsAdded;
            if (customTermsAdded != null)
            {
                customTermsAdded();
            }
            source.UpdateDictionary(false);
        }

        public static void AddCustomTerm(string term, string text)
        {
            try
            {
                I2.Loc.LanguageSourceData languageSourceData = I2.Loc.LocalizationManager.Sources[0];

                // 防重复保护：遍历 mTerms 检查术语是否已存在（避免使用可能不存在的 ContainsTerm 方法）
                if (languageSourceData.mTerms?.Exists(t => t.Term == term) == true)
                {
                    Plugin.logger.LogInfo(string.Concat(new string[]
                    {
                        "TERM \"",
                        term,
                        "\" 已存在，跳过注册（已翻译为 \"",
                        text,
                        "\")"
                    }));
                    return;
                }

                languageSourceData.AddTerm(term);
                languageSourceData.mTerms[languageSourceData.mTerms.Count - 1].SetTranslation(0, text, null);
                Plugin.logger.LogInfo(string.Concat(new string[]
                {
                    "TRANSLATED \"",
                    term,
                    "\" TO \"",
                    text,
                    "\""
                }));
            }
            catch (Exception ex)
            {
                Plugin.logger.LogError(string.Concat(new string[]
                {
                    "FAILED TO TRANSLATE \"",
                    term,
                    "\""
                }));
                Plugin.logger.LogError(ex);
            }
        }

        [CompilerGenerated]
        private static class Hooks
        {
            public static On.I2.Loc.LocalizationManager.hook_AddSource LocalizationManager_AddSourceHook;
        }
    }
}
