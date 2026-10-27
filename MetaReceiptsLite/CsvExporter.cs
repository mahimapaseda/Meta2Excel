using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace MetaReceiptsLite
{
    public static class CsvExporter
    {
        public static void ExportLineItems(string path, IList<LineItem> items)
        {
            using (var sw = NewWriter(path))
            {
                WriteLine(sw, "Invoice No", "Receipt Date", "Campaign", "Campaign Name", "Period From", "Period To",
                    "Ad Set", "Currency", "Impressions", "Cost", "CPM");
                if (items == null) return;
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    WriteLine(sw,
                        it.InvoiceNo,
                        FormatDate(it.InvoiceDate),
                        it.CampaignCode,
                        it.CampaignName,
                        FormatDate(it.PeriodFrom),
                        FormatDate(it.PeriodTo),
                        it.AdSet,
                        it.CurrencyCode,
                        it.Impressions.ToString(CultureInfo.InvariantCulture),
                        it.Cost.ToString("0.00", CultureInfo.InvariantCulture),
                        it.Cpm.ToString("0.######", CultureInfo.InvariantCulture));
                }
            }
        }

        public static void ExportReceipts(string path, IList<Receipt> receipts)
        {
            using (var sw = NewWriter(path))
            {
                WriteLine(sw, "Invoice No", "Date", "Reference", "Transaction ID", "Payment Method",
                    "Account", "Account ID", "Product", "Currency", "Amount Billed",
                    "Line Items", "Impressions", "Check", "Source File");
                if (receipts == null) return;
                for (int i = 0; i < receipts.Count; i++)
                {
                    var r = receipts[i];
                    WriteLine(sw,
                        r.InvoiceNo,
                        FormatDate(r.InvoiceDate),
                        r.ReferenceNumber,
                        r.TransactionId,
                        r.PaymentMethod,
                        r.AccountName,
                        r.AccountId,
                        r.ProductType,
                        r.CurrencyCode,
                        r.AmountBilled.ToString("0.00", CultureInfo.InvariantCulture),
                        r.ItemCount.ToString(CultureInfo.InvariantCulture),
                        r.TotalImpressions.ToString(CultureInfo.InvariantCulture),
                        r.Check,
                        r.SourceFile);
                }
            }
        }

        public static void ExportBoth(string folderOrZip, IList<Receipt> receipts, IList<LineItem> items, bool asZip)
        {
            if (asZip)
            {
                var zipPath = folderOrZip;
                if (!zipPath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    zipPath = Path.Combine(folderOrZip, "meta-receipts-export.zip");

                var dir = Path.GetDirectoryName(zipPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var tmp = Path.Combine(Path.GetTempPath(), "MetaReceiptsLite_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tmp);
                try
                {
                    var receiptsPath = Path.Combine(tmp, "receipts.csv");
                    var itemsPath = Path.Combine(tmp, "line-items.csv");
                    ExportReceipts(receiptsPath, receipts);
                    ExportLineItems(itemsPath, items);
                    if (File.Exists(zipPath)) File.Delete(zipPath);
                    ZipFile.CreateFromDirectory(tmp, zipPath);
                }
                finally
                {
                    try { Directory.Delete(tmp, true); } catch { /* ignore */ }
                }
            }
            else
            {
                if (!Directory.Exists(folderOrZip))
                    Directory.CreateDirectory(folderOrZip);
                ExportReceipts(Path.Combine(folderOrZip, "receipts.csv"), receipts);
                ExportLineItems(Path.Combine(folderOrZip, "line-items.csv"), items);
            }
        }

        public static string Escape(string value)
        {
            if (value == null) return string.Empty;
            bool needQuotes = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!needQuotes) return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static StreamWriter NewWriter(string path)
        {
            var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            // UTF-8 with BOM
            return new StreamWriter(fs, new UTF8Encoding(true));
        }

        private static void WriteLine(StreamWriter sw, params string[] fields)
        {
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sw.Write(',');
                sw.Write(Escape(fields[i]));
            }
            sw.WriteLine();
        }

        private static string FormatDate(DateTime dt)
        {
            if (dt == default(DateTime)) return string.Empty;
            return dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
    }
}
