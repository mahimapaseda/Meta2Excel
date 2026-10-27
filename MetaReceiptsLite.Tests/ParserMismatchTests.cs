using System.Collections.Generic;
using MetaReceiptsLite;
using Xunit;

namespace MetaReceiptsLite.Tests
{
    public class ParserMismatchTests
    {
        [Fact]
        public void ItemsTotalNotEqualBilled_SetsMismatchCheck()
        {
            var lines = new List<string>
            {
                "Receipt for TEST",
                "Account ID: 1",
                "Invoice/payment date",
                "16 Oct 2026, 12:22",
                "Payment method",
                "Visa ···· 1111",
                "Reference number: REF1",
                "Transaction ID",
                "90000000000000001-90000000000000002 You're being billed because you've reached your $10.00 payment threshold.",
                "Product type",
                "Meta ads",
                "Campaigns",
                "(A) campaign",
                "From 15 Oct 2026, 00:00 to 16 Oct 2026, 12:22",
                "ad set 100 Impressions $3.00",
                "Invoice no. FBADS-323-999000111"
            };

            var r = ReceiptParser.ParseLines(lines);
            Assert.Equal(10.00m, r.AmountBilled);
            Assert.Equal(3.00m, r.ItemsTotal);
            Assert.StartsWith("Mismatch", r.Check);
            Assert.Contains("3.00", r.Check);
        }
    }
}
