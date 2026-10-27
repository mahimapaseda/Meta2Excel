using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using MetaReceiptsLite;
using Xunit;

namespace MetaReceiptsLite.Tests
{
    public class ExcelExporterTests
    {
        [Fact]
        public void Export_CreatesSheets_Formulas_AndTextIds()
        {
            var receipts = new List<Receipt>();
            var items = new List<LineItem>();

            var r1 = MakeReceipt("FBADS-323-1", "111-222", 24m, "USD", "$", 2);
            var r2 = MakeReceipt("FBADS-323-2", "333-444", 11m, "USD", "$", 1);
            var r3 = MakeReceipt("FBADS-323-3", "555-666", 10m, "EUR", "€", 1);
            receipts.Add(r1);
            receipts.Add(r2);
            receipts.Add(r3);
            items.AddRange(r1.Items);
            items.AddRange(r2.Items);
            items.AddRange(r3.Items);

            var path = Path.Combine(Path.GetTempPath(), "mrl_xlsx_" + Guid.NewGuid().ToString("N") + ".xlsx");
            try
            {
                ExcelExporter.Export(path, receipts, items);

                using (var wb = new XLWorkbook(path))
                {
                    Assert.NotNull(wb.Worksheet("Receipts"));
                    Assert.NotNull(wb.Worksheet("Line Items"));
                    Assert.NotNull(wb.Worksheet("Campaign Summary"));
                    Assert.NotNull(wb.Worksheet("Daily Spend"));

                    var receiptsWs = wb.Worksheet("Receipts");
                    Assert.Equal("Transaction ID", receiptsWs.Cell(1, 4).GetString());
                    // Long IDs stored as text
                    Assert.Equal("111-222", receiptsWs.Cell(2, 4).GetString());
                    Assert.Equal("1234567890123456", receiptsWs.Cell(2, 7).GetString());

                    var li = wb.Worksheet("Line Items");
                    Assert.Equal("CPM", li.Cell(1, 11).GetString());
                    var formula = li.Cell(2, 11).FormulaA1;
                    Assert.Contains("IF(I2=0,0,J2/I2*1000)", formula.Replace(" ", ""));

                    var daily = wb.Worksheet("Daily Spend");
                    Assert.Contains("payment date", daily.Cell(1, 1).GetString());

                    // Pictures embedded for currencies
                    Assert.True(daily.Pictures.Count >= 1);
                }
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        private static Receipt MakeReceipt(string invoice, string txn, decimal billed, string code, string symbol, int itemCount)
        {
            var r = new Receipt
            {
                InvoiceNo = invoice,
                InvoiceDate = new DateTime(2026, 10, 16, 12, 0, 0),
                ReferenceNumber = "REF",
                TransactionId = txn,
                PaymentMethod = "Visa ···· 1111",
                AccountName = "TEST",
                AccountId = "1234567890123456",
                ProductType = "Meta ads",
                CurrencyCode = code,
                CurrencySymbol = symbol,
                AmountBilled = billed,
                SourceFile = invoice + ".pdf"
            };
            decimal each = billed / itemCount;
            for (int i = 0; i < itemCount; i++)
            {
                var it = new LineItem
                {
                    InvoiceNo = invoice,
                    InvoiceDate = r.InvoiceDate,
                    CampaignCode = "C" + i,
                    CampaignName = "campaign " + i,
                    PeriodFrom = r.InvoiceDate.Date,
                    PeriodTo = r.InvoiceDate,
                    AdSet = "ad set " + i,
                    Impressions = 1000 * (i + 1),
                    Cost = each,
                    CurrencyCode = code
                };
                r.Items.Add(it);
            }
            r.ItemsTotal = billed;
            r.TotalImpressions = r.Items.Sum(x => (long)x.Impressions);
            r.Check = "OK";
            return r;
        }
    }
}
