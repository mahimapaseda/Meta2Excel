using System;
using System.Collections.Generic;

namespace MetaReceiptsLite
{
    /// <summary>
    /// Tracks already-imported receipts. Prefers Transaction ID; falls back to Invoice No.
    /// </summary>
    public sealed class ReceiptDeduper
    {
        private readonly HashSet<string> _transactionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _invoiceNos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public bool TryRegister(Receipt receipt, out string reason)
        {
            reason = null;
            if (receipt == null)
            {
                reason = "null receipt";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(receipt.TransactionId))
            {
                if (_transactionIds.Contains(receipt.TransactionId))
                {
                    reason = "duplicate (Transaction ID " + receipt.TransactionId + ")";
                    return false;
                }
                _transactionIds.Add(receipt.TransactionId);
                if (!string.IsNullOrWhiteSpace(receipt.InvoiceNo))
                    _invoiceNos.Add(receipt.InvoiceNo);
                return true;
            }

            if (!string.IsNullOrWhiteSpace(receipt.InvoiceNo))
            {
                if (_invoiceNos.Contains(receipt.InvoiceNo))
                {
                    reason = "duplicate (Invoice No " + receipt.InvoiceNo + ")";
                    return false;
                }
                _invoiceNos.Add(receipt.InvoiceNo);
                return true;
            }

            // No key — allow but note (caller may still want to import once)
            return true;
        }

        public void Unregister(Receipt receipt)
        {
            if (receipt == null) return;
            if (!string.IsNullOrWhiteSpace(receipt.TransactionId))
                _transactionIds.Remove(receipt.TransactionId);
            if (!string.IsNullOrWhiteSpace(receipt.InvoiceNo))
                _invoiceNos.Remove(receipt.InvoiceNo);
        }

        public void Clear()
        {
            _transactionIds.Clear();
            _invoiceNos.Clear();
        }
    }
}
