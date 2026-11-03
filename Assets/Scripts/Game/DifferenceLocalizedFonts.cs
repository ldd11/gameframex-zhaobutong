#if ENABLE_UI_UGUI
using UnityEngine;
using UnityEngine.UI;

namespace GameFrameX.Startup.Application
{
    public static class DifferenceLocalizedFonts
    {
        public static void Apply(GameObject root)
        {
            if (!root) return;
            var language = DifferenceLocalization.LanguageCode ?? "en";
            string fontName;
            if (language.StartsWith("zh") || language.StartsWith("ja") || language.StartsWith("ko"))
                fontName = "NotoSansCJKsc-Medium";
            else if (language.StartsWith("th")) fontName = "NotoSansThai-Regular";
            else if (language.StartsWith("hi")) fontName = "NotoSansDevanagari-Regular";
            else return;

            var font = Resources.Load<Font>("Localization/Fonts/" + fontName);
            if (!font) { Debug.LogWarning("Missing localization font: " + fontName); return; }
            foreach (var text in root.GetComponentsInChildren<Text>(true)) text.font = font;
        }
    }
}
#endif
