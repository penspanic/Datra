using System;
using System.Collections.Generic;
using System.Linq;

namespace Datra.Localization
{
    /// <summary>
    /// ISO 639-1 language codes for localization
    /// </summary>
    public enum LanguageCode
    {
        /// <summary>
        /// English (en)
        /// </summary>
        En,
        
        /// <summary>
        /// Korean (ko)
        /// </summary>
        Ko,
        
        /// <summary>
        /// Japanese (ja)
        /// </summary>
        Ja,
        
        /// <summary>
        /// Chinese Simplified (zh-CN)
        /// </summary>
        ZhCN,
        
        /// <summary>
        /// Chinese Traditional (zh-TW)
        /// </summary>
        ZhTW,
        
        /// <summary>
        /// Spanish (es)
        /// </summary>
        Es,
        
        /// <summary>
        /// French (fr)
        /// </summary>
        Fr,
        
        /// <summary>
        /// German (de)
        /// </summary>
        De,
        
        /// <summary>
        /// Italian (it)
        /// </summary>
        It,
        
        /// <summary>
        /// Portuguese (pt)
        /// </summary>
        Pt,
        
        /// <summary>
        /// Russian (ru)
        /// </summary>
        Ru,
        
        /// <summary>
        /// Arabic (ar)
        /// </summary>
        Ar,
        
        /// <summary>
        /// Dutch (nl)
        /// </summary>
        Nl,
        
        /// <summary>
        /// Polish (pl)
        /// </summary>
        Pl,
        
        /// <summary>
        /// Turkish (tr)
        /// </summary>
        Tr,
        
        /// <summary>
        /// Thai (th)
        /// </summary>
        Th,
        
        /// <summary>
        /// Vietnamese (vi)
        /// </summary>
        Vi,
        
        /// <summary>
        /// Indonesian (id)
        /// </summary>
        Id,
        
        /// <summary>
        /// Hindi (hi)
        /// </summary>
        Hi,
        
        /// <summary>
        /// Swedish (sv)
        /// </summary>
        Sv
    }
    
    /// <summary>
    /// Helper methods for LanguageCode
    /// </summary>
    public static class LanguageCodeExtensions
    {
        private static readonly Dictionary<LanguageCode, string> CodeToIso = new Dictionary<LanguageCode, string>
        {
            { LanguageCode.En, "en" },
            { LanguageCode.Ko, "ko" },
            { LanguageCode.Ja, "ja" },
            { LanguageCode.ZhCN, "zh-CN" },
            { LanguageCode.ZhTW, "zh-TW" },
            { LanguageCode.Es, "es" },
            { LanguageCode.Fr, "fr" },
            { LanguageCode.De, "de" },
            { LanguageCode.It, "it" },
            { LanguageCode.Pt, "pt" },
            { LanguageCode.Ru, "ru" },
            { LanguageCode.Ar, "ar" },
            { LanguageCode.Nl, "nl" },
            { LanguageCode.Pl, "pl" },
            { LanguageCode.Tr, "tr" },
            { LanguageCode.Th, "th" },
            { LanguageCode.Vi, "vi" },
            { LanguageCode.Id, "id" },
            { LanguageCode.Hi, "hi" },
            { LanguageCode.Sv, "sv" }
        };
        
        private static readonly Dictionary<string, LanguageCode> IsoToCode = 
            CodeToIso.ToDictionary(kvp => kvp.Value, kvp => kvp.Key, StringComparer.OrdinalIgnoreCase);
        
        private static readonly Dictionary<LanguageCode, string> CodeToDisplayName = new Dictionary<LanguageCode, string>
        {
            { LanguageCode.En, "English" },
            { LanguageCode.Ko, "한국어" },
            { LanguageCode.Ja, "日本語" },
            { LanguageCode.ZhCN, "简体中文" },
            { LanguageCode.ZhTW, "繁體中文" },
            { LanguageCode.Es, "Español" },
            { LanguageCode.Fr, "Français" },
            { LanguageCode.De, "Deutsch" },
            { LanguageCode.It, "Italiano" },
            { LanguageCode.Pt, "Português" },
            { LanguageCode.Ru, "Русский" },
            { LanguageCode.Ar, "العربية" },
            { LanguageCode.Nl, "Nederlands" },
            { LanguageCode.Pl, "Polski" },
            { LanguageCode.Tr, "Türkçe" },
            { LanguageCode.Th, "ไทย" },
            { LanguageCode.Vi, "Tiếng Việt" },
            { LanguageCode.Id, "Bahasa Indonesia" },
            { LanguageCode.Hi, "हिन्दी" },
            { LanguageCode.Sv, "Svenska" }
        };
        
        /// <summary>
        /// Converts LanguageCode to ISO 639-1 string
        /// </summary>
        public static string ToIsoCode(this LanguageCode code)
        {
            return CodeToIso.TryGetValue(code, out var iso) ? iso : "en";
        }
        
        /// <summary>
        /// Converts ISO 639-1 string to LanguageCode
        /// </summary>
        public static LanguageCode? FromIsoCode(string isoCode)
        {
            if (string.IsNullOrEmpty(isoCode))
                return null;
                
            return IsoToCode.TryGetValue(isoCode, out var code) ? code : (LanguageCode?)null;
        }
        
        /// <summary>
        /// Gets the native display name for the language
        /// </summary>
        public static string GetDisplayName(this LanguageCode code)
        {
            return CodeToDisplayName.TryGetValue(code, out var name) ? name : code.ToString();
        }
        
        /// <summary>
        /// Gets the file name for this language code (e.g., "en.csv", "ko.csv")
        /// </summary>
        public static string GetFileName(this LanguageCode code, string extension = ".csv")
        {
            return $"{code.ToIsoCode()}{extension}";
        }
        
        /// <summary>
        /// Tries to parse a language code from various formats
        /// </summary>
        public static LanguageCode? TryParse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;
            
            // Try parsing as enum name
            if (Enum.TryParse<LanguageCode>(value, true, out var enumResult))
                return enumResult;
            
            // Try parsing as ISO code
            return FromIsoCode(value);
        }
    }
}