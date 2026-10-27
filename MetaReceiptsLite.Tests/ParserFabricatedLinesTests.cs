using System.Collections.Generic;
using MetaReceiptsLite;
using Xunit;

namespace MetaReceiptsLite.Tests
{
    public class ParserFabricatedLinesTests
    {
        private static List<string> BaseHeader(string thresholdMoney, string txn = "11111111111111111-22222222222222222")
        {
            return new List<string>
            {
                "Receipt for TEST ACCOUNT",
                "Account ID: 1234567890",
                "Invoice/payment date",
                "16 Oct 2026, 12:22",
                "Payment method",
                "Visa ···· 1234",
                "Reference number: ABC123XYZ",
                "Transaction ID",
                txn + " You're being billed because you've reached your " + thresholdMoney + " payment threshold.",
                "Product type",
                "Meta ads",
                "Campaigns"
            };
        }

        [Fact]
        public void ParseLines_Eur_DecimalComma()
        {
            var lines = BaseHeader("€24,00", "30000000000000001-30000000000000002");
            lines.Add("(A) campaign euro");
            lines.Add("From 15 Oct 2026, 00:00 to 16 Oct 2026, 12:22");
            lines.Add("ad set one 1.000 Impressions €10,00");
            lines.Add("(B) campaign euro");
            lines.Add("From 15 Oct 2026, 00:00 to 16 Oct 2026, 12:22");
            lines.Add("ad set two 2.000 Impressions €14,00");
            lines.Add("Invoice no. FBADS-323-999000001");

            var r = ReceiptParser.ParseLines(lines);
            Assert.Equal("EUR", r.CurrencyCode);
            Assert.Equal("€", r.CurrencySymbol);
            Assert.Equal(24.00m, r.AmountBilled);
            Assert.Equal(2, r.ItemCount);
            Assert.Equal(24.00m, r.ItemsTotal);
            Assert.Equal("OK", r.Check);
            Assert.All(r.Items, it => Assert.Equal("EUR", it.CurrencyCode));
        }

        [Fact]
        public void ParseLines_Gbp()
        {
            var lines = BaseHeader("£11.00", "40000000000000001-40000000000000002");
            lines.Add("(X) campaign gbp");
            lines.Add("From 15 Oct 2026, 00:00 to 16 Oct 2026, 12:22");
            lines.Add("uk ad set 500 Impressions £11.00");
            lines.Add("Invoice no. FBADS-323-999000002");

            var r = ReceiptParser.ParseLines(lines);
            Assert.Equal("GBP", r.CurrencyCode);
            Assert.Equal("£", r.CurrencySymbol);
            Assert.Equal(11.00m, r.AmountBilled);
            Assert.Equal("OK", r.Check);
        }

        [Fact]
        public void ParseLines_Lkr()
        {
            var lines = BaseHeader("LKR 1,200.00", "50000000000000001-50000000000000002");
            lines.Add("(L) campaign lkr");
            lines.Add("From 15 Oct 2026, 00:00 to 16 Oct 2026, 12:22");
            lines.Add("colombo ad set 10,000 Impressions LKR 1,200.00");
            lines.Add("Invoice no. FBADS-323-999000003");

            var r = ReceiptParser.ParseLines(lines);
            Assert.Equal("LKR", r.CurrencyCode);
            Assert.Equal(1200.00m, r.AmountBilled);
            Assert.Equal("OK", r.Check);
        }

        [Fact]
        public void ParseLines_IgnoresCampaignSubtotal()
        {
            var lines = BaseHeader("$5.00", "60000000000000001-60000000000000002");
            lines.Add("(Amp) campaign 01 OCT - 12.10.26");
            lines.Add("$99.99"); // campaign subtotal — must be ignored
            lines.Add("From 15 Oct 2026, 00:00 to 16 Oct 2026, 12:22");
            lines.Add("amp - oct ad set 01 100 Impressions $5.00");
            lines.Add("Invoice no. FBADS-323-999000004");

            var r = ReceiptParser.ParseLines(lines);
            Assert.Equal(1, r.ItemCount);
            Assert.Equal(5.00m, r.ItemsTotal);
            Assert.Equal("OK", r.Check);
        }

        [Fact]
        public void ParseLines_NoCampaigns_Throws()
        {
            var lines = BaseHeader("$1.00", "70000000000000001-70000000000000002");
            lines.Add("Invoice no. FBADS-323-999000005");
            Assert.Throws<System.InvalidOperationException>(() => ReceiptParser.ParseLines(lines));
        }

        [Fact]
        public void ParseLines_NegAdSetOddSpacing()
        {
            var lines = BaseHeader("$0.65", "80000000000000001-80000000000000002");
            lines.Add("(Neg) campaign 01 OCT - 12.10.26");
            lines.Add("From 15 Oct 2026, 00:00 to 16 Oct 2026, 12:22");
            lines.Add("neg- oct ad set 01 1,029 Impressions $0.65");
            lines.Add("Invoice no. FBADS-323-999000006");

            var r = ReceiptParser.ParseLines(lines);
            Assert.Equal("neg- oct ad set 01", r.Items[0].AdSet);
            Assert.Equal(1029, r.Items[0].Impressions);
            Assert.Equal(0.65m, r.Items[0].Cost);
        }

        [Fact]
        public void Currencies_AreNotCrossSummed_InModels()
        {
            var eur = ReceiptParser.ParseLines(BuildSingle("€10,00", "90000000000000001-90000000000000002", "€10,00", "EUR"));
            var usd = ReceiptParser.ParseLines(BuildSingle("$10.00", "90000000000000003-90000000000000004", "$10.00", "USD"));
            Assert.Equal("EUR", eur.CurrencyCode);
            Assert.Equal("USD", usd.CurrencyCode);
            Assert.NotEqual(eur.CurrencyCode, usd.CurrencyCode);
        }

        private static List<string> BuildSingle(string threshold, string txn, string cost, string unused)
        {
            var lines = BaseHeader(threshold, txn);
            lines.Add("(Z) campaign");
            lines.Add("From 15 Oct 2026, 00:00 to 16 Oct 2026, 12:22");
            lines.Add("ad 100 Impressions " + cost);
            lines.Add("Invoice no. FBADS-323-999000099");
            return lines;
        }
    }
}
