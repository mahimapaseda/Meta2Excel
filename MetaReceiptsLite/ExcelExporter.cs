using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;

namespace MetaReceiptsLite
{
    public static class ExcelExporter
    {
        private static readonly XLColor HeaderBg = XLColor.FromHtml("#1F4E78");

        public static void Export(string path, IList<Receipt> receipts, IList<LineItem> items)
        {
            if (receipts == null) receipts = new List<Receipt>();
            if (items == null) items = new List<LineItem>();

            using (var wb = new XLWorkbook())
            {
                WriteReceiptsSheet(wb, receipts);
                WriteLineItemsSheet(wb, items);
                WriteCampaignSummarySheet(wb, items);
                WriteDailySpendSheet(wb, receipts);

                wb.SaveAs(path);
            }
        }

        private static void WriteReceiptsSheet(XLWorkbook wb, IList<Receipt> receipts)
        {
            var ws = wb.Worksheets.Add("Receipts");
            string[] headers =
            {
                "Invoice No", "Date", "Reference", "Transaction ID", "Payment Method",
                "Account", "Account ID", "Product", "Currency", "Amount Billed",
                "Line Items", "Impressions", "Check", "Source File"
            };
            WriteHeader(ws, headers);

            double[] widths = { 22, 18, 14, 36, 20, 28, 20, 12, 10, 14, 12, 12, 18, 40 };
            for (int c = 0; c < widths.Length; c++)
                ws.Column(c + 1).Width = widths[c];

            for (int i = 0; i < receipts.Count; i++)
            {
                var r = receipts[i];
                int row = i + 2;
                ws.Cell(row, 1).Value = r.InvoiceNo;
                ws.Cell(row, 2).Value = r.InvoiceDate;
                ws.Cell(row, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                ws.Cell(row, 3).Value = r.ReferenceNumber;
                ws.Cell(row, 4).Value = r.TransactionId;
                ws.Cell(row, 4).Style.NumberFormat.Format = "@";
                ws.Cell(row, 5).Value = r.PaymentMethod;
                ws.Cell(row, 6).Value = r.AccountName;
                ws.Cell(row, 7).Value = r.AccountId;
                ws.Cell(row, 7).Style.NumberFormat.Format = "@";
                ws.Cell(row, 8).Value = r.ProductType;
                ws.Cell(row, 9).Value = r.CurrencyCode;
                ws.Cell(row, 10).Value = r.AmountBilled;
                ws.Cell(row, 10).Style.NumberFormat.Format = CurrencyDetector.ExcelMoneyFormat(r.CurrencySymbol);
                ws.Cell(row, 11).Value = r.ItemCount;
                ws.Cell(row, 12).Value = r.TotalImpressions;
                ws.Cell(row, 13).Value = r.Check;
                ws.Cell(row, 14).Value = r.SourceFile;
            }

            int lastData = receipts.Count + 1;
            if (receipts.Count > 0)
            {
                ws.Range(1, 1, lastData, headers.Length).SetAutoFilter();
                ws.SheetView.FreezeRows(1);

                var currencies = receipts.Select(r => r.CurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
                int totalRow = lastData + 1;
                foreach (var cur in currencies)
                {
                    var sym = receipts.First(r => string.Equals(r.CurrencyCode, cur, StringComparison.OrdinalIgnoreCase)).CurrencySymbol;
                    ws.Cell(totalRow, 8).Value = "TOTAL";
                    ws.Cell(totalRow, 8).Style.Font.Bold = true;
                    ws.Cell(totalRow, 9).Value = cur;
                    ws.Cell(totalRow, 9).Style.Font.Bold = true;
                    ws.Cell(totalRow, 10).FormulaA1 =
                        "SUMIF(I2:I" + lastData + ",I" + totalRow + ",J2:J" + lastData + ")";
                    ws.Cell(totalRow, 10).Style.NumberFormat.Format = CurrencyDetector.ExcelMoneyFormat(sym);
                    ws.Cell(totalRow, 10).Style.Font.Bold = true;
                    ws.Cell(totalRow, 12).FormulaA1 =
                        "SUMIF(I2:I" + lastData + ",I" + totalRow + ",L2:L" + lastData + ")";
                    ws.Cell(totalRow, 12).Style.Font.Bold = true;
                    ws.Range(totalRow, 1, totalRow, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    totalRow++;
                }
            }
        }

        private static void WriteLineItemsSheet(XLWorkbook wb, IList<LineItem> items)
        {
            var ws = wb.Worksheets.Add("Line Items");
            string[] headers =
            {
                "Invoice No", "Receipt Date", "Campaign", "Campaign Name",
                "Period From", "Period To", "Ad Set", "Currency",
                "Impressions", "Cost", "CPM"
            };
            WriteHeader(ws, headers);

            double[] widths = { 22, 18, 12, 28, 18, 18, 28, 10, 12, 12, 12 };
            for (int c = 0; c < widths.Length; c++)
                ws.Column(c + 1).Width = widths[c];

            for (int i = 0; i < items.Count; i++)
            {
                var it = items[i];
                int row = i + 2;
                ws.Cell(row, 1).Value = it.InvoiceNo;
                ws.Cell(row, 2).Value = it.InvoiceDate;
                ws.Cell(row, 2).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                ws.Cell(row, 3).Value = it.CampaignCode;
                ws.Cell(row, 4).Value = it.CampaignName;
                ws.Cell(row, 5).Value = it.PeriodFrom;
                ws.Cell(row, 5).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                ws.Cell(row, 6).Value = it.PeriodTo;
                ws.Cell(row, 6).Style.DateFormat.Format = "yyyy-mm-dd hh:mm";
                ws.Cell(row, 7).Value = it.AdSet;
                ws.Cell(row, 8).Value = it.CurrencyCode;
                ws.Cell(row, 9).Value = it.Impressions;
                ws.Cell(row, 10).Value = it.Cost;
                var sym = CurrencyDetector.SymbolForCode(it.CurrencyCode);
                ws.Cell(row, 10).Style.NumberFormat.Format = CurrencyDetector.ExcelMoneyFormat(sym);
                // CPM = IF(I2=0,0,J2/I2*1000)
                ws.Cell(row, 11).FormulaA1 = "=IF(I" + row + "=0,0,J" + row + "/I" + row + "*1000)";
                ws.Cell(row, 11).Style.NumberFormat.Format = "0.00";
            }

            int lastData = items.Count + 1;
            if (items.Count > 0)
            {
                ws.Range(1, 1, lastData, headers.Length).SetAutoFilter();
                ws.SheetView.FreezeRows(1);

                var currencies = items.Select(x => x.CurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
                int totalRow = lastData + 1;
                foreach (var cur in currencies)
                {
                    var sym = CurrencyDetector.SymbolForCode(cur);
                    ws.Cell(totalRow, 7).Value = "TOTAL";
                    ws.Cell(totalRow, 7).Style.Font.Bold = true;
                    ws.Cell(totalRow, 8).Value = cur;
                    ws.Cell(totalRow, 8).Style.Font.Bold = true;
                    ws.Cell(totalRow, 9).FormulaA1 =
                        "SUMIF(H2:H" + lastData + ",H" + totalRow + ",I2:I" + lastData + ")";
                    ws.Cell(totalRow, 9).Style.Font.Bold = true;
                    ws.Cell(totalRow, 10).FormulaA1 =
                        "SUMIF(H2:H" + lastData + ",H" + totalRow + ",J2:J" + lastData + ")";
                    ws.Cell(totalRow, 10).Style.NumberFormat.Format = CurrencyDetector.ExcelMoneyFormat(sym);
                    ws.Cell(totalRow, 10).Style.Font.Bold = true;
                    ws.Range(totalRow, 1, totalRow, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    totalRow++;
                }
            }
        }

        private static void WriteCampaignSummarySheet(XLWorkbook wb, IList<LineItem> items)
        {
            var ws = wb.Worksheets.Add("Campaign Summary");
            string[] headers = { "Campaign", "Currency", "Impressions", "Cost", "CPM", "Share of Spend (%)" };
            WriteHeader(ws, headers);

            double[] widths = { 14, 10, 14, 14, 12, 18 };
            for (int c = 0; c < widths.Length; c++)
                ws.Column(c + 1).Width = widths[c];

            var groups = items
                .GroupBy(x => new { Code = x.CampaignCode ?? "", Cur = x.CurrencyCode ?? "" })
                .Select(g => new
                {
                    Campaign = g.Key.Code,
                    Currency = g.Key.Cur,
                    Cost = g.Sum(x => x.Cost)
                })
                .OrderByDescending(x => x.Cost)
                .ToList();

            int liLast = items.Count + 1; // Line Items data last row
            for (int i = 0; i < groups.Count; i++)
            {
                var g = groups[i];
                int row = i + 2;
                ws.Cell(row, 1).Value = g.Campaign;
                ws.Cell(row, 2).Value = g.Currency;
                // SUMIFS over Line Items: Campaign col C, Currency col H, Impressions I, Cost J
                string camp = "A" + row;
                string cur = "B" + row;
                ws.Cell(row, 3).FormulaA1 =
                    "SUMIFS('Line Items'!I:I,'Line Items'!C:C," + camp + ",'Line Items'!H:H," + cur + ")";
                ws.Cell(row, 4).FormulaA1 =
                    "SUMIFS('Line Items'!J:J,'Line Items'!C:C," + camp + ",'Line Items'!H:H," + cur + ")";
                ws.Cell(row, 4).Style.NumberFormat.Format = CurrencyDetector.ExcelMoneyFormat(CurrencyDetector.SymbolForCode(g.Currency));
                ws.Cell(row, 5).FormulaA1 = "=IF(C" + row + "=0,0,D" + row + "/C" + row + "*1000)";
                ws.Cell(row, 5).Style.NumberFormat.Format = "0.00";
                // Share relative to same currency total on Line Items
                ws.Cell(row, 6).FormulaA1 =
                    "=IF(SUMIF('Line Items'!H:H,B" + row + ",'Line Items'!J:J)=0,0,D" + row +
                    "/SUMIF('Line Items'!H:H,B" + row + ",'Line Items'!J:J))";
                ws.Cell(row, 6).Style.NumberFormat.Format = "0.00%";
            }

            int lastData = groups.Count + 1;
            if (groups.Count > 0)
            {
                ws.Range(1, 1, lastData, headers.Length).SetAutoFilter();
                ws.SheetView.FreezeRows(1);

                var currencies = groups.Select(x => x.Currency).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(c => c).ToList();
                int totalRow = lastData + 1;
                foreach (var cur in currencies)
                {
                    ws.Cell(totalRow, 1).Value = "TOTAL";
                    ws.Cell(totalRow, 1).Style.Font.Bold = true;
                    ws.Cell(totalRow, 2).Value = cur;
                    ws.Cell(totalRow, 2).Style.Font.Bold = true;
                    ws.Cell(totalRow, 3).FormulaA1 =
                        "SUMIF(B2:B" + lastData + ",B" + totalRow + ",C2:C" + lastData + ")";
                    ws.Cell(totalRow, 3).Style.Font.Bold = true;
                    ws.Cell(totalRow, 4).FormulaA1 =
                        "SUMIF(B2:B" + lastData + ",B" + totalRow + ",D2:D" + lastData + ")";
                    ws.Cell(totalRow, 4).Style.NumberFormat.Format = CurrencyDetector.ExcelMoneyFormat(CurrencyDetector.SymbolForCode(cur));
                    ws.Cell(totalRow, 4).Style.Font.Bold = true;
                    ws.Range(totalRow, 1, totalRow, headers.Length).Style.Border.TopBorder = XLBorderStyleValues.Thin;
                    totalRow++;
                }
            }

            // silence unused warning in older analyzers
            GC.KeepAlive(liLast);
        }

        private static void WriteDailySpendSheet(XLWorkbook wb, IList<Receipt> receipts)
        {
            var ws = wb.Worksheets.Add("Daily Spend");
            ws.Cell(1, 1).Value = "Note: Daily spend is the total amount billed grouped by the receipt payment date (date part of Invoice/payment date), per currency. Receipts do not include true per-day campaign spend.";
            ws.Range(1, 1, 1, 6).Merge();
            ws.Cell(1, 1).Style.Font.Italic = true;
            ws.Cell(1, 1).Style.Alignment.WrapText = true;
            ws.Row(1).Height = 36;

            string[] headers = { "Date", "Currency", "Receipts", "Amount Billed", "Impressions", "Cumulative Billed" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(3, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = HeaderBg;
            }

            double[] widths = { 14, 10, 10, 14, 14, 16 };
            for (int c = 0; c < widths.Length; c++)
                ws.Column(c + 1).Width = widths[c];

            var daily = receipts
                .GroupBy(r => new { Day = r.InvoiceDate.Date, Cur = r.CurrencyCode ?? "" })
                .Select(g => new
                {
                    Date = g.Key.Day,
                    Currency = g.Key.Cur,
                    Symbol = g.First().CurrencySymbol,
                    Count = g.Count(),
                    Amount = g.Sum(x => x.AmountBilled),
                    Impressions = g.Sum(x => x.TotalImpressions)
                })
                .OrderBy(x => x.Currency)
                .ThenBy(x => x.Date)
                .ToList();

            // Cumulative per currency
            var cum = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            int receiptsLast = receipts.Count + 1;

            for (int i = 0; i < daily.Count; i++)
            {
                var d = daily[i];
                int row = i + 4;
                if (!cum.ContainsKey(d.Currency)) cum[d.Currency] = 0m;
                cum[d.Currency] += d.Amount;

                ws.Cell(row, 1).Value = d.Date;
                ws.Cell(row, 1).Style.DateFormat.Format = "yyyy-mm-dd";
                ws.Cell(row, 2).Value = d.Currency;
                // COUNTIFS / SUMIFS over Receipts sheet: Date col B, Currency col I, Amount J, Impressions L
                // Date match: use date value — ClosedXML stores dates as OADate; formula compares date part
                ws.Cell(row, 3).Value = d.Count;
                ws.Cell(row, 4).Value = d.Amount;
                ws.Cell(row, 4).Style.NumberFormat.Format = CurrencyDetector.ExcelMoneyFormat(d.Symbol);
                ws.Cell(row, 5).Value = d.Impressions;
                ws.Cell(row, 6).Value = cum[d.Currency];
                ws.Cell(row, 6).Style.NumberFormat.Format = CurrencyDetector.ExcelMoneyFormat(d.Symbol);
            }

            int lastData = daily.Count + 3;
            if (daily.Count > 0)
            {
                ws.Range(3, 1, lastData, headers.Length).SetAutoFilter();
                ws.SheetView.FreezeRows(3);

                // Replace count/amount with formulas referencing Receipts where practical
                for (int i = 0; i < daily.Count; i++)
                {
                    var d = daily[i];
                    int row = i + 4;
                    // Use SUMIFS with currency; date equality via INT of receipt date
                    // Simpler reliable approach: keep computed values (already set) — also add formulas for Amount using SUMIFS on currency+date text
                    string dateLit = d.Date.ToString("yyyy-mm-dd", CultureInfo.InvariantCulture);
                    ws.Cell(row, 3).FormulaA1 =
                        "COUNTIFS(Receipts!I:I,B" + row + ",Receipts!B:B,\">=\"&A" + row + ",Receipts!B:B,\"<\"&(A" + row + "+1))";
                    ws.Cell(row, 4).FormulaA1 =
                        "SUMIFS(Receipts!J:J,Receipts!I:I,B" + row + ",Receipts!B:B,\">=\"&A" + row + ",Receipts!B:B,\"<\"&(A" + row + "+1))";
                    ws.Cell(row, 5).FormulaA1 =
                        "SUMIFS(Receipts!L:L,Receipts!I:I,B" + row + ",Receipts!B:B,\">=\"&A" + row + ",Receipts!B:B,\"<\"&(A" + row + "+1))";
                    // Cumulative: sum of Amount Billed for same currency up to this row's date
                    ws.Cell(row, 6).FormulaA1 =
                        "SUMIFS(Receipts!J:J,Receipts!I:I,B" + row + ",Receipts!B:B,\"<=\"&(A" + row + "+0.9999))";
                    GC.KeepAlive(dateLit);
                }
            }

            // Charts per currency stacked vertically
            var byCur = daily.GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key).ToList();
            int chartTopRow = lastData + 3;
            foreach (var g in byCur)
            {
                var points = new List<DailySpendPoint>();
                decimal running = 0m;
                foreach (var d in g.OrderBy(x => x.Date))
                {
                    running += d.Amount;
                    points.Add(new DailySpendPoint { Date = d.Date, Amount = d.Amount, Cumulative = running });
                }

                var sym = g.First().Symbol;
                byte[] png = DailySpendChart.RenderPng(g.Key, sym, points, 900, 360);
                using (var ms = new MemoryStream(png))
                {
                    var pic = ws.AddPicture(ms);
                    pic.MoveTo(ws.Cell(chartTopRow, 8));
                    // Approximate placement: ClosedXML scales by intrinsic size
                }
                chartTopRow += 20; // stack vertically (~360px ≈ 18-20 rows)
            }

            GC.KeepAlive(receiptsLast);
        }

        private static void WriteHeader(IXLWorksheet ws, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor = HeaderBg;
            }
        }
    }
}
