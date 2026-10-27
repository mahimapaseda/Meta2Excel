using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using MetaReceiptsLite;
using Xunit;

namespace MetaReceiptsLite.Tests
{
    public class CsvExporterTests
    {
        [Theory]
        [InlineData("plain", "plain")]
        [InlineData("a,b", "\"a,b\"")]
        [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
        [InlineData("line1\nline2", "\"line1\nline2\"")]
        public void Escape_Rfc4180(string input, string expected)
        {
            Assert.Equal(expected, CsvExporter.Escape(input));
        }

        [Fact]
        public void ExportLineItems_StreamsUtf8BomAndInvariantDecimals()
        {
            var path = Path.Combine(Path.GetTempPath(), "mrl_items_" + Guid.NewGuid().ToString("N") + ".csv");
            try
            {
                var items = new List<LineItem>
                {
                    new LineItem
                    {
                        InvoiceNo = "FBADS-1",
                        InvoiceDate = new DateTime(2026, 10, 16, 12, 22, 0),
                        CampaignCode = "Amp",
                        CampaignName = "campaign",
                        PeriodFrom = new DateTime(2026, 10, 15, 0, 0, 0),
                        PeriodTo = new DateTime(2026, 10, 16, 12, 22, 0),
                        AdSet = "ad, \"set\"",
                        Impressions = 5085,
                        Cost = 1.93m,
                        CurrencyCode = "USD"
                    }
                };
                CsvExporter.ExportLineItems(path, items);

                var bytes = File.ReadAllBytes(path);
                Assert.True(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);

                var text = File.ReadAllText(path, new UTF8Encoding(true));
                Assert.Contains("1.93", text);
                Assert.Contains("2026-10-16 12:22", text);
                Assert.Contains("\"ad, \"\"set\"\"\"", text);
                Assert.Contains("USD", text);
                Assert.DoesNotContain("1,93", text);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
