using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MetaReceiptsLite;
using Xunit;

namespace MetaReceiptsLite.Tests
{
    public class ParserSamplePdfTests
    {
        private static string SamplesDir()
        {
            var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Samples");
            if (!Directory.Exists(dir))
                dir = Path.Combine(Directory.GetCurrentDirectory(), "Samples");
            return dir;
        }

        [Fact]
        public void Oct16_FourReceipts_MatchAcceptance()
        {
            var dir = SamplesDir();
            Assert.True(Directory.Exists(dir), "Samples folder missing: " + dir);

            var files = Directory.GetFiles(dir, "sample-usd-*.pdf").OrderBy(f => f).ToArray();
            Assert.Equal(4, files.Length);

            var receipts = new List<Receipt>();
            foreach (var f in files)
                receipts.Add(ReceiptParser.ParseFile(f));

            Assert.Equal(4, receipts.Count);
            Assert.All(receipts, r => Assert.Equal("USD", r.CurrencyCode));
            Assert.All(receipts, r => Assert.Equal("OK", r.Check));
            Assert.All(receipts, r => Assert.Equal(20, r.ItemCount));

            var billed = receipts.Select(r => r.AmountBilled).OrderByDescending(x => x).ToList();
            Assert.Equal(24.00m, billed[0]);
            Assert.Equal(11.00m, billed[1]);
            Assert.Equal(11.00m, billed[2]);
            Assert.Equal(11.00m, billed[3]);

            Assert.Equal(80, receipts.Sum(r => r.ItemCount));
            Assert.Equal(57.00m, receipts.Sum(r => r.AmountBilled));

            // All same payment day
            Assert.All(receipts, r => Assert.Equal(new DateTime(2026, 10, 16).Date, r.InvoiceDate.Date));

            // Invoice numbers present
            Assert.All(receipts, r => Assert.StartsWith("FBADS-", r.InvoiceNo));
            Assert.All(receipts, r => Assert.False(string.IsNullOrEmpty(r.TransactionId)));
        }

        [Fact]
        public void DuplicateTransactionId_CanBeDetected()
        {
            var dir = SamplesDir();
            var file = Path.Combine(dir, "sample-usd-24.pdf");
            Assert.True(File.Exists(file), "Missing " + file);
            var a = ReceiptParser.ParseFile(file);
            var b = ReceiptParser.ParseFile(file);
            Assert.Equal(a.TransactionId, b.TransactionId);
            Assert.False(string.IsNullOrEmpty(a.TransactionId));
        }
    }
}
