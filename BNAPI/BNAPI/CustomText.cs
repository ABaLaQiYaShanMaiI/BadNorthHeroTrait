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
            if (ReferenceEquals(hook_AddSource = CustomText.Hooks.LocalizationManager_AddSourceHook, null))
            {
                hook_AddSource = (CustomText.Hooks.LocalizationManager_AddSourceHook = new On.I2.Loc.LocalizationManager.hook_AddSource(CustomText.LocalizationManager_AddSource));
            }
            On.I2.Loc.LocalizationManager.AddSource += hook_AddSource;
        }

        private static void LocalizationManager_AddSource(On.I2.Loc.LocalizationManager.orig_AddSource orig, I2.Loc.LanguageSourceData source)
        {
            orig.Invoke(source);
            CustomTermsAddedDelegate customTermsAdded = CustomText.CustomTermsAdded;
            if (!ReferenceEquals(customTermsAdded, null))
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

                // 防重复保护：使用传统 null 检查和循环查找（避免 ?. 运算符在 CLR 2.0 下的兼容性问题）
                bool termExists = false;
                if (languageSourceData.mTerms != null)
                {
                    for (int i = 0; i < languageSourceData.mTerms.Count; i++)
                    {
                        if (languageSourceData.mTerms[i].Term == term)
                        {
                            termExists = true;
                            break;
                        }
                    }
                }

                if (termExists)
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
