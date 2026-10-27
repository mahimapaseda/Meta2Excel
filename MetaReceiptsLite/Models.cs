using System;
using System.Collections.Generic;

namespace MetaReceiptsLite
{
    public sealed class Receipt
    {
        public string SourceFile { get; set; }
        public string AccountName { get; set; }
        public string AccountId { get; set; }
        public string InvoiceNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string ReferenceNumber { get; set; }
        public string TransactionId { get; set; }
        public string PaymentMethod { get; set; }
        public string ProductType { get; set; }
        public string CurrencyCode { get; set; }
        public string CurrencySymbol { get; set; }
        public decimal AmountBilled { get; set; }
        public List<LineItem> Items { get; set; }
        public int ItemCount
        {
            get { return Items == null ? 0 : Items.Count; }
        }
        public decimal ItemsTotal { get; set; }
        public long TotalImpressions { get; set; }
        public string Check { get; set; }

        public Receipt()
        {
            SourceFile = string.Empty;
            AccountName = string.Empty;
            AccountId = string.Empty;
            InvoiceNo = string.Empty;
            ReferenceNumber = string.Empty;
            TransactionId = string.Empty;
            PaymentMethod = string.Empty;
            ProductType = string.Empty;
            CurrencyCode = string.Empty;
            CurrencySymbol = string.Empty;
            Check = string.Empty;
            Items = new List<LineItem>();
        }
    }

    public sealed class LineItem
    {
        public string InvoiceNo { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string CampaignCode { get; set; }
        public string CampaignName { get; set; }
        public DateTime PeriodFrom { get; set; }
        public DateTime PeriodTo { get; set; }
        public string AdSet { get; set; }
        public int Impressions { get; set; }
        public decimal Cost { get; set; }
        public string CurrencyCode { get; set; }

        public decimal Cpm
        {
            get
            {
                if (Impressions <= 0) return 0m;
                return Cost / Impressions * 1000m;
            }
        }

        public string CampaignDisplay
        {
            get
            {
                if (string.IsNullOrEmpty(CampaignCode)) return CampaignName ?? string.Empty;
                return "(" + CampaignCode + ") " + (CampaignName ?? string.Empty);
            }
        }

        public LineItem()
        {
            InvoiceNo = string.Empty;
            CampaignCode = string.Empty;
            CampaignName = string.Empty;
            AdSet = string.Empty;
            CurrencyCode = string.Empty;
        }
    }

    public sealed class ParseFileResult
    {
        public Receipt Receipt { get; set; }
        public string Error { get; set; }
        public bool Success { get { return Receipt != null && string.IsNullOrEmpty(Error); } }
    }
}
