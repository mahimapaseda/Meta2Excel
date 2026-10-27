using System.Globalization;
using MetaReceiptsLite;
using Xunit;

namespace MetaReceiptsLite.Tests
{
    public class CurrencyDetectorTests
    {
        [Theory]
        [InlineData("$24.00", 24.00, "USD", "$")]
        [InlineData("€24,00", 24.00, "EUR", "€")]
        [InlineData("24.00 USD", 24.00, "USD", "$")]
        [InlineData("LKR 1,200.00", 1200.00, "LKR", "Rs")]
        [InlineData("£11.50", 11.50, "GBP", "£")]
        [InlineData("1.234,56 EUR", 1234.56, "EUR", "€")]
        [InlineData("Rs. 500.00", 500.00, "LKR", "Rs")]
        [InlineData("₹99.99", 99.99, "INR", "₹")]
        public void TryParseMoney_CommonFormats(string text, double expectedAmount, string code, string symbol)
        {
            var r = CurrencyDetector.TryParseMoney(text);
            Assert.True(r.Success, "Failed to parse: " + text);
            Assert.Equal((decimal)expectedAmount, r.Amount);
            Assert.Equal(code, r.CurrencyCode);
            Assert.Equal(symbol, r.CurrencySymbol);
        }

        [Fact]
        public void TryParseFromThreshold_ExtractsAmount()
        {
            var r = CurrencyDetector.TryParseFromThreshold(
                "You're being billed because you've reached your $24.00 payment threshold.");
            Assert.True(r.Success);
            Assert.Equal(24.00m, r.Amount);
            Assert.Equal("USD", r.CurrencyCode);
        }

        [Fact]
        public void PreferIsoOverBareDollar()
        {
            var receipt = new Receipt
            {
                CurrencyCode = "USD",
                CurrencySymbol = "$",
                AmountBilled = 10m
            };
            receipt.Items.Add(new LineItem { Cost = 10m, CurrencyCode = "USD" });
            CurrencyDetector.ApplyIsoPreference(receipt, "AUD");
            Assert.Equal("AUD", receipt.CurrencyCode);
            Assert.Equal("A$", receipt.CurrencySymbol);
            Assert.Equal("AUD", receipt.Items[0].CurrencyCode);
        }

        [Fact]
        public void ExcelMoneyFormat_UsesSymbol()
        {
            Assert.Equal("\"€\"#,##0.00", CurrencyDetector.ExcelMoneyFormat("€"));
            Assert.Equal("\"$\"#,##0.00", CurrencyDetector.ExcelMoneyFormat("$"));
        }

        [Fact]
        public void ParseNumber_ThousandsAndDecimals()
        {
            Assert.Equal(1234.56m, CurrencyDetector.ParseNumber("1,234.56"));
            Assert.Equal(1234.56m, CurrencyDetector.ParseNumber("1.234,56"));
            Assert.Equal(1200m, CurrencyDetector.ParseNumber("1,200"));
        }

        [Fact]
        public void FormatMoney_Invariant()
        {
            var prev = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                Assert.Equal("$24.00", CurrencyDetector.FormatMoney(24m, "$"));
            }
            finally
            {
                CultureInfo.CurrentCulture = prev;
            }
        }
    }
}
