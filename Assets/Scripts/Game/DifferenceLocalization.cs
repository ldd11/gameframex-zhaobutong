using System;
using System.Globalization;
using System.Runtime.InteropServices;
using GameFrameX.Runtime;
using UnityEngine;

namespace GameFrameX.Startup.Application
{
    public static class DifferenceLocalization
    {
        [Serializable] sealed class Table { public Entry[] entries; }
        [Serializable] sealed class Entry
        {
            public string key, English, ChineseSimplified, ChineseTraditional, French, German, Indonesian, Japanese, Korean;
            public string PortugueseBrazil, PortuguesePortugal, Russian, SpanishLatinAmerica, SpanishSpain, Spanish, Thai, Vietnamese, Hindi;
        }

        // Startup translations are built into the client and are not replaced by hot updates.
        const string StartupJson = @"[
{""key"":""startup_tip_01"",""English"":""Hiding a few surprises..."",""ChineseSimplified"":""正在藏好一些小惊喜……"",""ChineseTraditional"":""正在藏好一些小驚喜……"",""French"":""Cachant quelques surprises..."",""German"":""Ein paar Überraschungen verbergen sich..."",""Indonesian"":""Menyembunyikan beberapa kejutan..."",""Japanese"":""いくつかの驚きを隠しています..."",""Korean"":""몇 가지 놀라운 사실을 숨기고 있습니다..."",""PortugueseBrazil"":""Escondendo algumas surpresas..."",""PortuguesePortugal"":""Escondendo algumas surpresas..."",""Russian"":""Скрываем несколько сюрпризов..."",""SpanishLatinAmerica"":""Escondiendo algunas sorpresas..."",""SpanishSpain"":""Escondiendo algunas sorpresas..."",""Thai"":""ซ่อนความประหลาดใจเล็กน้อย..."",""Vietnamese"":""Ẩn giấu một vài điều bất ngờ..."",""Hindi"":""कुछ आश्चर्य छुपा रहा हूँ...""},
{""key"":""startup_tip_02"",""English"":""Getting the tiny details just right..."",""ChineseSimplified"":""正在仔细调整每一个小细节……"",""ChineseTraditional"":""正在仔細調整每一個小細節……"",""French"":""Obtenir les petits détails justes..."",""German"":""Die kleinsten Details genau richtig hinbekommen..."",""Indonesian"":""Mendapatkan detail kecil dengan tepat..."",""Japanese"":""細部まで正確に仕上げます..."",""Korean"":""세세한 부분까지 제대로 파악하는 중..."",""PortugueseBrazil"":""Acertando os mínimos detalhes..."",""PortuguesePortugal"":""Acertando nos mais pequenos detalhes..."",""Russian"":""Правильно прорисовываем мелкие детали..."",""SpanishLatinAmerica"":""Consiguiendo los pequeños detalles a la perfección..."",""SpanishSpain"":""Consiguiendo los pequeños detalles a la perfección..."",""Thai"":""เก็บรายละเอียดเล็กๆ น้อยๆ ให้ถูกต้อง..."",""Vietnamese"":""Bắt đúng từng chi tiết nhỏ..."",""Hindi"":""छोटी-छोटी जानकारियों को बिल्कुल सही तरीके से प्राप्त करना...""},
{""key"":""startup_tip_03"",""English"":""Your next “Aha!” moment is loading..."",""ChineseSimplified"":""下一个恍然大悟的瞬间即将到来……"",""ChineseTraditional"":""下一個恍然大悟的瞬間即將到來……"",""French"":""Votre prochain « Aha ! » le moment est en cours de chargement..."",""German"":""Ihr nächstes „Aha!“ Moment wird geladen..."",""Indonesian"":""\""Aha!\"" saat sedang memuat..."",""Japanese"":""次の「ああ！」ロード中です..."",""Korean"":""당신의 다음 \""아하!\"" 지금 로드 중입니다..."",""PortugueseBrazil"":""Seu próximo “Aha!” momento está carregando..."",""PortuguesePortugal"":""O seu próximo “Aha!” momento está a carregar..."",""Russian"":""Ваше следующее «Ага!» момент загружается..."",""SpanishLatinAmerica"":""Tu próximo \""¡Ajá!\"" momento se esta cargando..."",""SpanishSpain"":""Tu próximo \""¡Ajá!\"" momento se esta cargando..."",""Thai"":""“อ๊า!” ต่อไปของคุณ กำลังโหลดสักครู่..."",""Vietnamese"":""“Aha!” Tiếp theo của bạn khoảnh khắc đang tải..."",""Hindi"":""आपका अगला \""अहा!\"" पल लोड हो रहा है...""},
{""key"":""startup_tip_04"",""English"":""A little looking, a lot of joy."",""ChineseSimplified"":""用心找一找，收获满满的快乐。"",""ChineseTraditional"":""用心找一找，收穫滿滿的快樂。"",""French"":""Un peu de recherche, beaucoup de joie."",""German"":""Ein wenig schauen, viel Freude."",""Indonesian"":""Sedikit melihat, banyak kegembiraan."",""Japanese"":""少し見て、たくさんの喜び。"",""Korean"":""조금 보고, 많은 기쁨을 누리세요."",""PortugueseBrazil"":""Um pouco de olhar, muita alegria."",""PortuguesePortugal"":""Um pouco de olhar, muita alegria."",""Russian"":""Немного поиска, много радости."",""SpanishLatinAmerica"":""Un poco de mirada, mucha alegría."",""SpanishSpain"":""Un poco de mirada, mucha alegría."",""Thai"":""มองเพียงเล็กน้อยก็มีความสุขมาก"",""Vietnamese"":""Một chút nhìn, rất nhiều niềm vui."",""Hindi"":""थोड़ी सी तलाश, ढेर सारा आनंद.""},
{""key"":""download_size"",""English"":""Downloading {0}"",""ChineseSimplified"":""正在下载{0}"",""ChineseTraditional"":""正在下載{0}"",""French"":""Téléchargement de {0}"",""German"":""{0} wird heruntergeladen"",""Indonesian"":""Mengunduh {0}"",""Japanese"":""{0}をダウンロード中"",""Korean"":""{0} 다운로드 중"",""PortugueseBrazil"":""Baixando {0}"",""PortuguesePortugal"":""Descarregando {0}"",""Russian"":""Загрузка {0}"",""SpanishLatinAmerica"":""Descargando {0}"",""SpanishSpain"":""Descargando {0}"",""Thai"":""กำลังดาวน์โหลด {0}"",""Vietnamese"":""Đang tải xuống {0}"",""Hindi"":""{0} डाउनलोड हो रहा है""}
]";

        public static string LanguageCode { get; private set; } = "en";
#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern IntPtr DifferencePreferredLanguage();
#endif

        public static void Initialize()
        {
            LanguageCode = ResolveLanguage(SystemLocale(), UnityEngine.Application.systemLanguage);
            LoadJson(StartupJson, true);
        }

        public static void LoadJson(string json) => LoadJson(json, false);

        static void LoadJson(string json, bool startup)
        {
            Table table;
            try { table = JsonUtility.FromJson<Table>("{\"entries\":" + json + "}"); }
            catch (Exception e) { Debug.LogWarning("Localization data could not be read: " + e.Message); return; }
            if (table?.entries == null) { Debug.LogWarning("Localization data has no entries."); return; }
            foreach (var entry in table.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.key)) continue;
                if (!startup && (entry.key == "startup_tip_01" || entry.key == "startup_tip_02" ||
                    entry.key == "startup_tip_03" || entry.key == "startup_tip_04" || entry.key == "download_size")) continue;
                var value = Translation(entry, LanguageCode);
                if (string.IsNullOrEmpty(value)) value = entry.English;
                if (string.IsNullOrEmpty(value)) continue;
                if (GameApp.Localization.HasRawString(entry.key)) GameApp.Localization.RemoveRawString(entry.key);
                GameApp.Localization.AddRawString(entry.key, value);
            }
        }

        static string SystemLocale()
        {
#if UNITY_IOS && !UNITY_EDITOR
            try
            {
                var value = DifferencePreferredLanguage();
                if (value != IntPtr.Zero)
                {
                    var language = Marshal.PtrToStringAnsi(value);
                    if (!string.IsNullOrEmpty(language)) return language;
                }
            }
            catch (Exception e) { Debug.LogWarning("Could not read iOS locale: " + e.Message); }
#endif
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var locale = new AndroidJavaClass("java.util.Locale"))
                using (var current = locale.CallStatic<AndroidJavaObject>("getDefault"))
                    return current.Call<string>("toLanguageTag");
            }
            catch (Exception e) { Debug.LogWarning("Could not read Android locale: " + e.Message); }
#endif
            var name = CultureInfo.CurrentUICulture.Name;
            return string.IsNullOrEmpty(name) ? CultureInfo.CurrentCulture.Name : name;
        }

        public static string ResolveLanguage(string locale, SystemLanguage systemLanguage)
        {
            var value = (locale ?? "").Replace('_', '-').ToLowerInvariant();
            if (value == "zh" || value.StartsWith("zh-"))
            {
                if (value.Contains("-hans")) return "zh-CN";
                if (value.Contains("-hant")) return "zh-TW";
                return value.Contains("-tw") || value.Contains("-hk") || value.Contains("-mo") ? "zh-TW" : "zh-CN";
            }
            if (value == "pt" || value.StartsWith("pt-")) return value.Contains("-br") ? "pt-BR" : "pt-PT";
            if (value == "es" || value.StartsWith("es-")) return value.Contains("-es") ? "es-ES" : "es-419";
            foreach (var code in new[] { "en", "fr", "de", "id", "ja", "ko", "ru", "th", "vi", "hi" })
                if (value == code || value.StartsWith(code + "-")) return code;
            if (!string.IsNullOrEmpty(value)) return "en";
            switch (systemLanguage)
            {
                case SystemLanguage.ChineseTraditional: return "zh-TW";
                case SystemLanguage.Chinese: case SystemLanguage.ChineseSimplified: return "zh-CN";
                case SystemLanguage.French: return "fr";
                case SystemLanguage.German: return "de";
                case SystemLanguage.Indonesian: return "id";
                case SystemLanguage.Japanese: return "ja";
                case SystemLanguage.Korean: return "ko";
                case SystemLanguage.Portuguese: return "pt-PT";
                case SystemLanguage.Russian: return "ru";
                case SystemLanguage.Spanish: return "es-419";
                case SystemLanguage.Thai: return "th";
                case SystemLanguage.Vietnamese: return "vi";
                default: return "en";
            }
        }

        static string Translation(Entry e, string code)
        {
            switch (code)
            {
                case "zh-CN": return e.ChineseSimplified;
                case "zh-TW": return e.ChineseTraditional;
                case "fr": return e.French;
                case "de": return e.German;
                case "id": return e.Indonesian;
                case "ja": return e.Japanese;
                case "ko": return e.Korean;
                case "pt-BR": return e.PortugueseBrazil;
                case "pt-PT": return e.PortuguesePortugal;
                case "ru": return e.Russian;
                case "es-419": return e.SpanishLatinAmerica;
                case "es-ES": return string.IsNullOrEmpty(e.SpanishSpain) ? e.Spanish : e.SpanishSpain;
                case "th": return e.Thai;
                case "vi": return e.Vietnamese;
                case "hi": return e.Hindi;
                default: return e.English;
            }
        }
    }
}
