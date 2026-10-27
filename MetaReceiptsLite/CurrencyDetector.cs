using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MetaReceiptsLite
{
    public sealed class MoneyParseResult
    {
        public decimal Amount { get; set; }
        public string CurrencyCode { get; set; }
        public string CurrencySymbol { get; set; }
        public bool Success { get; set; }

        public MoneyParseResult()
        {
            CurrencyCode = string.Empty;
            CurrencySymbol = string.Empty;
        }
    }

    public static class CurrencyDetector
    {
        private static readonly Dictionary<string, string> SymbolToCode = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "$", "USD" },
            { "€", "EUR" },
            { "£", "GBP" },
            { "¥", "JPY" },
            { "₹", "INR" },
            { "Rs", "LKR" },
            { "Rs.", "LKR" }
        };

        private static readonly Dictionary<string, string> CodeToSymbol = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "USD", "$" },
            { "AUD", "A$" },
            { "CAD", "C$" },
            { "SGD", "S$" },
            { "EUR", "€" },
            { "GBP", "£" },
            { "JPY", "¥" },
            { "INR", "₹" },
            { "LKR", "Rs" },
            { "AED", "AED" }
        };

        private static readonly HashSet<string> KnownCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "USD", "EUR", "GBP", "LKR", "INR", "AUD", "CAD", "SGD", "AED", "JPY", "NZD", "CHF", "HKD", "MYR", "PHP", "THB", "ZAR"
        };

        // Matches money tokens: optional currency before/after amount
        private static readonly Regex MoneyToken = new Regex(
            @"(?:(?<pre>(?:USD|EUR|GBP|LKR|INR|AUD|CAD|SGD|AED|JPY|NZD|CHF|HKD|Rs\.?|[€£¥₹$]))\s*)?" +
            @"(?<num>\d{1,3}(?:[.,]\d{3})*(?:[.,]\d+)?|\d+(?:[.,]\d+)?)" +
            @"(?:\s*(?<post>(?:USD|EUR|GBP|LKR|INR|AUD|CAD|SGD|AED|JPY|NZD|CHF|HKD|Rs\.?|[€£¥₹$])))?",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex ThresholdSentence = new Regex(
            @"reached your\s+(.+?)\s+payment threshold",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        public static MoneyParseResult TryParseMoney(string text)
        {
            var result = new MoneyParseResult();
            if (string.IsNullOrWhiteSpace(text)) return result;

            var m = MoneyToken.Match(text.Trim());
            if (!m.Success) return result;

            return FromMatch(m);
        }

        public static MoneyParseResult TryParseFromThreshold(string line)
        {
            var result = new MoneyParseResult();
            if (string.IsNullOrWhiteSpace(line)) return result;

            var tm = ThresholdSentence.Match(line);
            if (!tm.Success) return result;

            return TryParseMoney(tm.Groups[1].Value);
        }

        public static MoneyParseResult Prefer(MoneyParseResult primary, MoneyParseResult fallback)
        {
            if (primary != null && primary.Success) return primary;
            if (fallback != null && fallback.Success) return fallback;
            return primary ?? fallback ?? new MoneyParseResult();
        }

        /// <summary>
        /// Prefer ISO code over bare "$" when both appear on a receipt.
        /// </summary>
        public static void ApplyIsoPreference(Receipt receipt, string isoFromText)
        {
            if (receipt == null || string.IsNullOrEmpty(isoFromText)) return;
            if (!KnownCodes.Contains(isoFromText)) return;

            receipt.CurrencyCode = isoFromText.ToUpperInvariant();
            receipt.CurrencySymbol = SymbolForCode(receipt.CurrencyCode);
            if (receipt.Items != null)
            {
                for (int i = 0; i < receipt.Items.Count; i++)
                    receipt.Items[i].CurrencyCode = receipt.CurrencyCode;
            }
        }

        public static string FindIsoCode(string text)
        {
            if (string.IsNullOrEmpty(text)) return null;
            foreach (var code in KnownCodes)
            {
                // Word-boundary-ish match
                var idx = text.IndexOf(code, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) continue;
                bool beforeOk = idx == 0 || !char.IsLetterOrDigit(text[idx - 1]);
                int end = idx + code.Length;
                bool afterOk = end >= text.Length || !char.IsLetterOrDigit(text[end]);
                if (beforeOk && afterOk) return code.ToUpperInvariant();
            }
            return null;
        }

        public static string SymbolForCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return "$";
            string s;
            if (CodeToSymbol.TryGetValue(code, out s)) return s;
            return code;
        }

        public static string ExcelMoneyFormat(string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) symbol = "$";
            // Escape quotes inside symbol if needed
            var escaped = symbol.Replace("\"", "\"\"");
            return "\"" + escaped + "\"#,##0.00";
        }

        public static string FormatMoney(decimal amount, string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) symbol = "$";
            return symbol + amount.ToString("0.00", CultureInfo.InvariantCulture);
        }

        public static decimal ParseNumber(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0m;
            raw = raw.Trim();

            int lastComma = raw.LastIndexOf(',');
            int lastDot = raw.LastIndexOf('.');

            if (lastComma >= 0 && lastDot >= 0)
            {
                // Both separators: the last one is decimal if followed by 1-2 digits typically
                if (lastComma > lastDot)
                {
                    // European: 1.234,56
                    var cleaned = raw.Replace(".", "").Replace(',', '.');
                    return decimal.Parse(cleaned, CultureInfo.InvariantCulture);
                }
                else
                {
                    // US: 1,234.56
                    var cleaned = raw.Replace(",", "");
                    return decimal.Parse(cleaned, CultureInfo.InvariantCulture);
                }
            }

            if (lastComma >= 0)
            {
                int digitsAfter = raw.Length - lastComma - 1;
                if (digitsAfter == 3)
                {
                    // thousands: 1,200
                    return decimal.Parse(raw.Replace(",", ""), CultureInfo.InvariantCulture);
                }
                // decimal comma: 24,00
                return decimal.Parse(raw.Replace(',', '.'), CultureInfo.InvariantCulture);
            }

            if (lastDot >= 0)
            {
                int digitsAfter = raw.Length - lastDot - 1;
                if (digitsAfter == 3 && raw.IndexOf('.') == lastDot)
                {
                    // Could be thousands 1.200 with no decimal — treat as thousands only if no currency decimal expected
                    // Prefer decimal when 1-2 digits after; when exactly 3, treat as thousands for integer-like amounts
                    // For money we usually have 2 decimals; 3 after dot with single separator → thousands in EU integer style
                    // Keep as decimal if looks like money with 2 digits; for 3 digits use thousands.
                    return decimal.Parse(raw.Replace(".", ""), CultureInfo.InvariantCulture);
                }
                return decimal.Parse(raw, CultureInfo.InvariantCulture);
            }

            return decimal.Parse(raw, CultureInfo.InvariantCulture);
        }

        private static MoneyParseResult FromMatch(Match m)
        {
            var result = new MoneyParseResult();
            var num = m.Groups["num"].Value;
            if (string.IsNullOrEmpty(num)) return result;

            string pre = m.Groups["pre"].Success ? m.Groups["pre"].Value.Trim() : null;
            string post = m.Groups["post"].Success ? m.Groups["post"].Value.Trim() : null;

            string code = null;
            string symbol = null;
            ResolveCurrency(pre, ref code, ref symbol);
            // Prefer ISO from either side; symbol fills gaps
            string code2 = null, symbol2 = null;
            ResolveCurrency(post, ref code2, ref symbol2);

            if (!string.IsNullOrEmpty(code2) && KnownCodes.Contains(code2))
                code = code2;
            else if (string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(code2))
                code = code2;

            if (string.IsNullOrEmpty(symbol) && !string.IsNullOrEmpty(symbol2))
                symbol = symbol2;

            if (string.IsNullOrEmpty(code) && !string.IsNullOrEmpty(symbol))
            {
                string mapped;
                if (SymbolToCode.TryGetValue(symbol, out mapped))
                    code = mapped;
                else
                    code = "USD";
            }

            if (string.IsNullOrEmpty(symbol) && !string.IsNullOrEmpty(code))
                symbol = SymbolForCode(code);

            if (string.IsNullOrEmpty(code))
            {
                code = "USD";
                symbol = "$";
            }

            try
            {
                result.Amount = ParseNumber(num);
                result.CurrencyCode = code.ToUpperInvariant();
                result.CurrencySymbol = symbol ?? SymbolForCode(result.CurrencyCode);
                result.Success = true;
            }
            catch (FormatException)
            {
                result.Success = false;
            }
            catch (OverflowException)
            {
                result.Success = false;
            }

            return result;
        }

        private static void ResolveCurrency(string token, ref string code, ref string symbol)
        {
            if (string.IsNullOrEmpty(token)) return;

            if (KnownCodes.Contains(token))
            {
                code = token.ToUpperInvariant();
                symbol = SymbolForCode(code);
                return;
            }

            if (SymbolToCode.ContainsKey(token))
            {
                code = SymbolToCode[token];
                symbol = SymbolForCode(code);
                return;
            }

            // Bare symbol characters / Rs variants
            if (token.Length == 1 || token.StartsWith("Rs", StringComparison.OrdinalIgnoreCase))
            {
                string mapped;
                if (SymbolToCode.TryGetValue(token, out mapped))
                {
                    code = mapped;
                    symbol = SymbolForCode(code);
                }
                else
                {
                    symbol = token;
                }
            }
        }
    }
}
