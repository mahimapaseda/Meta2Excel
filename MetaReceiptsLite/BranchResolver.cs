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

        // Any spelling → full branch name (short codes + longer variants)
        private static readonly Dictionary<string, string> NameByAlias =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // Kurunegala
                { "Kur", "Kurunegala" },
                { "Kuru", "Kurunegala" },
                { "Kurun", "Kurunegala" },
                { "Kurune", "Kurunegala" },
                { "Kurunegala", "Kurunegala" },
                // Gampaha
                { "Gam", "Gampaha" },
                { "Gamp", "Gampaha" },
                { "Gampa", "Gampaha" },
                { "Gampaha", "Gampaha" },
                // Anuradhapura
                { "Anu", "Anuradhapura" },
                { "Anura", "Anuradhapura" },
                { "Anurad", "Anuradhapura" },
                { "Anuradhapura", "Anuradhapura" },
                // Ampara
                { "Amp", "Ampara" },
                { "Ampar", "Ampara" },
                { "Ampara", "Ampara" },
                // Galle
                { "Gal", "Galle" },
                { "Galle", "Galle" },
                // Badulla
                { "Bad", "Badulla" },
                { "Badu", "Badulla" },
                { "Badulla", "Badulla" },
                // Kandy
                { "Kan", "Kandy" },
                { "Kand", "Kandy" },
                { "Kandy", "Kandy" },
                // Ratnapura
                { "Rat", "Ratnapura" },
                { "Ratn", "Ratnapura" },
                { "Ratnapura", "Ratnapura" },
                // Matara
                { "Mat", "Matara" },
                { "Mata", "Matara" },
                { "Matara", "Matara" },
                // Negombo
                { "Neg", "Negombo" },
                { "Nego", "Negombo" },
                { "Negombo", "Negombo" },
                // Nugegoda
                { "Nug", "Nugegoda" },
                { "Nuge", "Nugegoda" },
                { "Nugegoda", "Nugegoda" },
                // Kegalle
                { "Keg", "Kegalle" },
                { "Kega", "Kegalle" },
                { "Kegalle", "Kegalle" },
                // Nuwara Eliya
                { "Nuw", "Nuwara Eliya" },
                { "Nuwa", "Nuwara Eliya" },
                { "Nuwara", "Nuwara Eliya" },
                // Horana
                { "Hor", "Horana" },
                { "Hora", "Horana" },
                { "Horana", "Horana" },
                // Wennappuwa
                { "Wen", "Wennappuwa" },
                { "Wenn", "Wennappuwa" },
                { "Wennappuwa", "Wennappuwa" },
                // Kuliyapitiya
                { "Kul", "Kuliyapitiya" },
                { "Kuli", "Kuliyapitiya" },
                { "Kuliyapitiya", "Kuliyapitiya" },
                // Kiribathgoda
                { "Kir", "Kiribathgoda" },
                { "Kiri", "Kiribathgoda" },
                { "Kiribathgoda", "Kiribathgoda" },
                // Ambalangoda
                { "Amb", "Ambalangoda" },
                { "Ambal", "Ambalangoda" },
                { "Ambalangoda", "Ambalangoda" },
                // Malabe
                { "Mal", "Malabe" },
                { "Mala", "Malabe" },
                { "Malabe", "Malabe" },
                // Bandarawela
                { "Ban", "Bandarawela" },
                { "Band", "Bandarawela" },
                { "Bandarawela", "Bandarawela" }
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
