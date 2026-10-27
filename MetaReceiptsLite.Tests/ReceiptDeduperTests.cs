using MetaReceiptsLite;
using Xunit;

namespace MetaReceiptsLite.Tests
{
    public class ReceiptDeduperTests
    {
        [Fact]
        public void DuplicateTransactionId_IsRejected()
        {
            var d = new ReceiptDeduper();
            string reason;
            Assert.True(d.TryRegister(new Receipt { TransactionId = "111-222", InvoiceNo = "A" }, out reason));
            Assert.False(d.TryRegister(new Receipt { TransactionId = "111-222", InvoiceNo = "B" }, out reason));
            Assert.Contains("Transaction ID", reason);
        }

        [Fact]
        public void DuplicateInvoiceNo_WhenNoTransactionId_IsRejected()
        {
            var d = new ReceiptDeduper();
            string reason;
            Assert.True(d.TryRegister(new Receipt { InvoiceNo = "FBADS-1" }, out reason));
            Assert.False(d.TryRegister(new Receipt { InvoiceNo = "FBADS-1" }, out reason));
            Assert.Contains("Invoice No", reason);
        }

        [Fact]
        public void Unregister_AllowsReimport()
        {
            var d = new ReceiptDeduper();
            var r = new Receipt { TransactionId = "AAA-BBB", InvoiceNo = "INV1" };
            string reason;
            Assert.True(d.TryRegister(r, out reason));
            d.Unregister(r);
            Assert.True(d.TryRegister(r, out reason));
        }
    }
}
