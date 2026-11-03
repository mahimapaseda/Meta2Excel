using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MetaReceiptsLite
{
    /// <summary>
    /// Resolves spend to a branch from the Ad Set name (not the campaign code).
    /// Example: ad set "Kuru - OCT ad set 02" under campaign (Amp) → Kurunegala.
    /// </summary>
    public static class BranchResolver
    {
        private static readonly Regex RxAdSetPrefix = new Regex(
            @"^\s*([A-Za-z]+)",
            RegexOptions.CultureInvariant | RegexOptions.Compiled);

        // Any spelling → full branch name
        private static readonly Dictionary<string, string> NameByAlias =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "Kur", "Kurunegala" },
                { "Kuru", "Kurunegala" },
                { "Anu", "Anuradhapura" },
                { "Amp", "Ampara" },
                { "Gal", "Galle" },
                { "Bad", "Badulla" },
                { "Kan", "Kandy" },
                { "Gam", "Gampaha" },
                { "Rat", "Ratnapura" },
                { "Mat", "Matara" },
                { "Neg", "Negombo" },
                { "Nug", "Nugegoda" },
                { "Keg", "Kegalle" },
                { "Nuw", "Nuwara Eliya" },
                { "Hor", "Horana" },
                { "Wen", "Wennappuwa" },
                { "Kul", "Kuliyapitiya" },
                { "Kir", "Kiribathgoda" },
                { "Amb", "Ambalangoda" },
                { "Mal", "Malabe" },
                { "Ban", "Bandarawela" }
            };

        /// <summary>
        /// Token from Ad Set (preferred) or Campaign code.
        /// </summary>
        public static string ExtractCode(LineItem item)
        {
            if (item == null) return string.Empty;

            string fromAd = ExtractAdSetCode(item.AdSet);
            if (!string.IsNullOrEmpty(fromAd))
                return fromAd;

            if (!string.IsNullOrWhiteSpace(item.CampaignCode))
                return item.CampaignCode.Trim();

            return string.Empty;
        }

        /// <summary>
        /// Full branch name for reporting (Kur / Kuru → Kurunegala).
        /// Unknown codes are returned unchanged.
        /// </summary>
        public static string ResolveName(LineItem item)
        {
            return LookupName(ExtractCode(item));
        }

        public static string ExtractAdSetCode(string adSet)
        {
            if (string.IsNullOrWhiteSpace(adSet)) return string.Empty;
            var m = RxAdSetPrefix.Match(adSet);
            return m.Success ? m.Groups[1].Value : string.Empty;
        }

        public static string LookupName(string codeOrAlias)
        {
            if (string.IsNullOrWhiteSpace(codeOrAlias)) return string.Empty;
            string name;
            if (NameByAlias.TryGetValue(codeOrAlias.Trim(), out name))
                return name;
            return codeOrAlias.Trim();
        }
    }
}
