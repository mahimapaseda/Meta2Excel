using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace MetaReceiptsLite
{
    public static class ReceiptParser
    {
        private const double YTolerance = 3.0;

        private static readonly Regex RxReceiptFor = new Regex(@"^Receipt for\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex RxAccountId = new Regex(@"Account ID:\s*(\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex RxDateTime = new Regex(@"^(\d{1,2}\s+[A-Za-z]{3}\s+\d{4}),\s*(\d{2}:\d{2})$", RegexOptions.CultureInvariant);
        private static readonly Regex RxReference = new Regex(@"Reference number:\s*([A-Z0-9]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex RxTransactionId = new Regex(@"(\d{10,}-\d{10,})", RegexOptions.CultureInvariant);
        private static readonly Regex RxInvoiceNo = new Regex(@"Invoice no\.\s*(FBADS-\d+-\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex RxPaymentCard = new Regex(@"^((?:Mastercard|Visa|Amex|American Express|PayPal|[A-Za-z]+))\s+[·•\.]{2,}\s*(\d{4})$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex RxCampaignTitle = new Regex(@"^\(([^)]+)\)\s+(.+)$", RegexOptions.CultureInvariant);
        private static readonly Regex RxPeriod = new Regex(
            @"^From\s+(\d{1,2}\s+[A-Za-z]{3}\s+\d{4},\s*\d{2}:\d{2})\s+to\s+(\d{1,2}\s+[A-Za-z]{3}\s+\d{4},\s*\d{2}:\d{2})$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex RxAdSet = new Regex(
            @"^(.+?)\s+([\d.,]+)\s+Impressions\s+(.+)$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static readonly Regex RxLoneAmountOnly = new Regex(
            @"^(?:[€£¥₹$]|Rs\.?|USD|EUR|GBP|LKR|INR)?\s*[\d.,]+\s*(?:USD|EUR|GBP|LKR|INR)?$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en-US");

        public static Receipt ParseFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Path is required.", "path");
            if (!File.Exists(path))
                throw new FileNotFoundException("PDF not found.", path);

            var lines = new List<string>();
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
                using (var doc = PdfDocument.Open(fs))
                {
                    foreach (var page in doc.GetPages())
                    {
                        lines.AddRange(GetLinesFromPage(page));
                        // page objects released when leaving iteration / disposing doc
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name.IndexOf("Encrypt", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (ex.Message != null && ex.Message.IndexOf("password", StringComparison.OrdinalIgnoreCase) >= 0))
                    throw new InvalidOperationException("PDF is password-protected.", ex);
                throw new InvalidOperationException("Unable to read PDF: " + ex.Message, ex);
            }

            var receipt = ParseLines(lines);
            receipt.SourceFile = Path.GetFileName(path);
            return receipt;
        }

        public static IEnumerable<string> GetLinesFromPage(Page page)
        {
            var words = page.GetWords().ToList();
            if (words.Count == 0) yield break;

            // Group by Y (top of word), tolerance ~3pt; PDF Y increases upward
            var ordered = words.OrderByDescending(w => w.BoundingBox.Bottom).ThenBy(w => w.BoundingBox.Left).ToList();
            var groups = new List<List<Word>>();
            List<Word> current = null;
            double currentY = 0;

            foreach (var w in ordered)
            {
                double y = w.BoundingBox.Bottom;
                if (current == null || Math.Abs(y - currentY) > YTolerance)
                {
                    current = new List<Word>();
                    groups.Add(current);
                    currentY = y;
                }
                current.Add(w);
            }

            foreach (var g in groups)
            {
                var sorted = g.OrderBy(w => w.BoundingBox.Left).ToList();
                var text = string.Join(" ", sorted.Select(w => w.Text)).Trim();
                if (text.Length > 0)
                    yield return text;
            }
        }

        public static Receipt ParseLines(IEnumerable<string> lines)
        {
            if (lines == null) throw new ArgumentNullException("lines");

            var receipt = new Receipt();
            string campaignCode = null;
            string campaignName = null;
            DateTime? periodFrom = null;
            DateTime? periodTo = null;
            bool inCampaigns = false;
            bool expectInvoiceDate = false;
            bool expectPaymentMethod = false;
            bool expectTransactionId = false;
            bool expectProductType = false;
            string foundIso = null;

            foreach (var raw in lines)
            {
                var line = raw == null ? string.Empty : raw.Trim();
                if (line.Length == 0) continue;

                var iso = CurrencyDetector.FindIsoCode(line);
                if (iso != null) foundIso = iso;

                // Header fields (also appear before Campaigns)
                Match m;

                m = RxReceiptFor.Match(line);
                if (m.Success)
                {
                    receipt.AccountName = m.Groups[1].Value.Trim();
                    continue;
                }

                m = RxAccountId.Match(line);
                if (m.Success)
                {
                    receipt.AccountId = m.Groups[1].Value;
                    continue;
                }

                if (line.IndexOf("Invoice/payment date", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    expectInvoiceDate = true;
                    // Date might be on same line after label
                    var rest = line.Substring(line.IndexOf("Invoice/payment date", StringComparison.OrdinalIgnoreCase) + "Invoice/payment date".Length).Trim();
                    if (rest.Length > 0 && TryParseReceiptDate(rest, out var dtSame))
                    {
                        receipt.InvoiceDate = dtSame;
                        expectInvoiceDate = false;
                    }
                    continue;
                }

                if (expectInvoiceDate)
                {
                    if (TryParseReceiptDate(line, out var dt))
                    {
                        receipt.InvoiceDate = dt;
                        expectInvoiceDate = false;
                        continue;
                    }
                }

                if (line.StartsWith("Payment method", StringComparison.OrdinalIgnoreCase))
                {
                    expectPaymentMethod = true;
                    var rest = line.Substring("Payment method".Length).Trim();
                    // Strip status words like Paid
                    rest = Regex.Replace(rest, @"\bPaid\b", "", RegexOptions.IgnoreCase).Trim();
                    if (rest.Length > 0 && !rest.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                    {
                        var card = RxPaymentCard.Match(rest);
                        if (card.Success)
                        {
                            receipt.PaymentMethod = card.Groups[1].Value + " ···· " + card.Groups[2].Value;
                            expectPaymentMethod = false;
                        }
                    }
                    continue;
                }

                if (expectPaymentMethod)
                {
                    var card = RxPaymentCard.Match(line);
                    if (card.Success)
                    {
                        receipt.PaymentMethod = card.Groups[1].Value + " ···· " + card.Groups[2].Value;
                        expectPaymentMethod = false;
                        continue;
                    }
                    if (line.Equals("Paid", StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                m = RxReference.Match(line);
                if (m.Success)
                {
                    receipt.ReferenceNumber = m.Groups[1].Value;
                    // Merged Y-line may include billed amount on the right
                    var afterRef = line.Substring(m.Index + m.Length).Trim();
                    if (afterRef.Length > 0 && receipt.AmountBilled == 0m)
                    {
                        var money = CurrencyDetector.TryParseMoney(afterRef);
                        if (money.Success)
                        {
                            receipt.AmountBilled = money.Amount;
                            receipt.CurrencyCode = money.CurrencyCode;
                            receipt.CurrencySymbol = money.CurrencySymbol;
                        }
                    }
                    continue;
                }

                if (line.StartsWith("Transaction ID", StringComparison.OrdinalIgnoreCase))
                {
                    expectTransactionId = true;
                    var rest = line.Substring("Transaction ID".Length).Trim();
                    if (rest.Length > 0)
                        ConsumeTransactionAndThreshold(rest, receipt, ref expectTransactionId);
                    continue;
                }

                if (expectTransactionId)
                {
                    ConsumeTransactionAndThreshold(line, receipt, ref expectTransactionId);
                    continue;
                }

                // Threshold sentence may appear alone or merged
                if (line.IndexOf("payment threshold", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var thr = CurrencyDetector.TryParseFromThreshold(line);
                    if (thr.Success)
                    {
                        receipt.AmountBilled = thr.Amount;
                        receipt.CurrencyCode = thr.CurrencyCode;
                        receipt.CurrencySymbol = thr.CurrencySymbol;
                    }
                    m = RxTransactionId.Match(line);
                    if (m.Success && string.IsNullOrEmpty(receipt.TransactionId))
                        receipt.TransactionId = m.Groups[1].Value;
                    continue;
                }

                if (line.StartsWith("Product type", StringComparison.OrdinalIgnoreCase))
                {
                    expectProductType = true;
                    var rest = line.Substring("Product type".Length).Trim();
                    if (rest.Length > 0)
                    {
                        receipt.ProductType = rest;
                        expectProductType = false;
                    }
                    continue;
                }

                if (expectProductType)
                {
                    receipt.ProductType = line;
                    expectProductType = false;
                    continue;
                }

                m = RxInvoiceNo.Match(line);
                if (m.Success)
                {
                    receipt.InvoiceNo = m.Groups[1].Value;
                    continue;
                }

                if (line.Equals("Campaigns", StringComparison.OrdinalIgnoreCase))
                {
                    inCampaigns = true;
                    continue;
                }

                if (!inCampaigns) continue;

                // Campaign title
                m = RxCampaignTitle.Match(line);
                if (m.Success)
                {
                    campaignCode = m.Groups[1].Value.Trim();
                    campaignName = m.Groups[2].Value.Trim();
                    periodFrom = null;
                    periodTo = null;
                    continue;
                }

                // Period
                m = RxPeriod.Match(line);
                if (m.Success)
                {
                    periodFrom = ParsePeriodDate(m.Groups[1].Value);
                    periodTo = ParsePeriodDate(m.Groups[2].Value);
                    continue;
                }

                // Ad-set row
                m = RxAdSet.Match(line);
                if (m.Success)
                {
                    var adSet = m.Groups[1].Value.Trim();
                    var impressionsRaw = m.Groups[2].Value;
                    var costRaw = m.Groups[3].Value.Trim();
                    var money = CurrencyDetector.TryParseMoney(costRaw);
                    if (!money.Success)
                        continue;

                    int impressions = ParseImpressions(impressionsRaw);
                    var item = new LineItem
                    {
                        CampaignCode = campaignCode ?? string.Empty,
                        CampaignName = campaignName ?? string.Empty,
                        PeriodFrom = periodFrom ?? default(DateTime),
                        PeriodTo = periodTo ?? default(DateTime),
                        AdSet = adSet,
                        Impressions = impressions,
                        Cost = money.Amount,
                        CurrencyCode = money.CurrencyCode
                    };
                    receipt.Items.Add(item);

                    if (string.IsNullOrEmpty(receipt.CurrencyCode))
                    {
                        receipt.CurrencyCode = money.CurrencyCode;
                        receipt.CurrencySymbol = money.CurrencySymbol;
                    }
                    continue;
                }

                // Ignore lone campaign subtotal amounts so totals are not double-counted
                if (IsLoneCampaignSubtotal(line))
                    continue;
            }

            if (foundIso != null)
                CurrencyDetector.ApplyIsoPreference(receipt, foundIso);

            Finalize(receipt);

            if (receipt.ItemCount == 0)
                throw new InvalidOperationException("Not a Meta receipt (no campaign rows found).");

            return receipt;
        }

        private static void ConsumeTransactionAndThreshold(string line, Receipt receipt, ref bool expectTransactionId)
        {
            var m = RxTransactionId.Match(line);
            if (m.Success)
            {
                receipt.TransactionId = m.Groups[1].Value;
                expectTransactionId = false;
            }

            if (line.IndexOf("payment threshold", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                var thr = CurrencyDetector.TryParseFromThreshold(line);
                if (thr.Success)
                {
                    receipt.AmountBilled = thr.Amount;
                    receipt.CurrencyCode = thr.CurrencyCode;
                    receipt.CurrencySymbol = thr.CurrencySymbol;
                }
                expectTransactionId = false;
            }
            else if (!m.Success)
            {
                // Still waiting for txn id line
            }
            else
            {
                // txn found; remainder may have threshold
                var rest = line.Substring(m.Index + m.Length).Trim();
                if (rest.Length > 0 && rest.IndexOf("threshold", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    var thr = CurrencyDetector.TryParseFromThreshold(rest);
                    if (thr.Success)
                    {
                        receipt.AmountBilled = thr.Amount;
                        receipt.CurrencyCode = thr.CurrencyCode;
                        receipt.CurrencySymbol = thr.CurrencySymbol;
                    }
                }
            }
        }

        private static bool IsLoneCampaignSubtotal(string line)
        {
            if (!RxLoneAmountOnly.IsMatch(line)) return false;
            // Must look like a money amount, not a bare integer campaign code
            var money = CurrencyDetector.TryParseMoney(line);
            return money.Success;
        }

        private static void Finalize(Receipt receipt)
        {
            if (string.IsNullOrEmpty(receipt.CurrencyCode))
            {
                receipt.CurrencyCode = "USD";
                receipt.CurrencySymbol = "$";
            }
            if (string.IsNullOrEmpty(receipt.CurrencySymbol))
                receipt.CurrencySymbol = CurrencyDetector.SymbolForCode(receipt.CurrencyCode);

            decimal total = 0m;
            long impressions = 0;
            for (int i = 0; i < receipt.Items.Count; i++)
            {
                var item = receipt.Items[i];
                item.InvoiceNo = receipt.InvoiceNo;
                item.InvoiceDate = receipt.InvoiceDate;
                if (string.IsNullOrEmpty(item.CurrencyCode))
                    item.CurrencyCode = receipt.CurrencyCode;
                total += item.Cost;
                impressions += item.Impressions;
            }

            receipt.ItemsTotal = total;
            receipt.TotalImpressions = impressions;

            if (Math.Abs(receipt.ItemsTotal - receipt.AmountBilled) < 0.005m)
                receipt.Check = "OK";
            else
                receipt.Check = "Mismatch (items " + receipt.ItemsTotal.ToString("0.00", CultureInfo.InvariantCulture) + ")";
        }

        private static int ParseImpressions(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0;
            // Remove thousands separators
            var cleaned = raw.Replace(",", "").Replace(".", "");
            // If European thousands used dots and there was a decimal, ParseNumber logic — impressions are integers
            // Prefer: strip all non-digits except if last separator had 3 digits → thousands
            if (raw.IndexOf(',') >= 0 && raw.IndexOf('.') >= 0)
            {
                // unlikely for impressions; strip both separators' thousands form
                cleaned = raw.Replace(",", "").Replace(".", "");
            }
            else if (raw.IndexOf('.') >= 0 && raw.LastIndexOf('.') == raw.Length - 4)
            {
                cleaned = raw.Replace(".", "");
            }
            else if (raw.IndexOf(',') >= 0)
            {
                cleaned = raw.Replace(",", "");
            }
            else
            {
                cleaned = raw;
            }

            int n;
            if (int.TryParse(cleaned, NumberStyles.Integer, CultureInfo.InvariantCulture, out n))
                return n;
            return 0;
        }

        private static bool TryParseReceiptDate(string text, out DateTime dt)
        {
            dt = default(DateTime);
            var m = RxDateTime.Match(text.Trim());
            if (!m.Success)
            {
                // Try embedded
                m = Regex.Match(text, @"(\d{1,2}\s+[A-Za-z]{3}\s+\d{4}),\s*(\d{2}:\d{2})");
                if (!m.Success) return false;
            }
            return DateTime.TryParseExact(
                m.Groups[1].Value + " " + m.Groups[2].Value,
                "d MMM yyyy HH:mm",
                En,
                DateTimeStyles.None,
                out dt);
        }

        private static DateTime ParsePeriodDate(string text)
        {
            DateTime dt;
            if (DateTime.TryParseExact(
                text.Trim(),
                "d MMM yyyy, HH:mm",
                En,
                DateTimeStyles.None,
                out dt))
                return dt;
            if (DateTime.TryParseExact(
                text.Trim(),
                "d MMM yyyy,HH:mm",
                En,
                DateTimeStyles.None,
                out dt))
                return dt;
            return default(DateTime);
        }
    }
}
