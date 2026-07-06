using System.Windows.Media;

namespace PharmaExt.App.Models;

public sealed class SettlementReportRow
{
    private static readonly Brush MissingInvoiceBrush = new SolidColorBrush(Color.FromRgb(254, 226, 226));

    public int DisplayNumber { get; set; }
    public DateTime SaleDate { get; set; }
    public DateTime InvoiceIssueDate { get; set; }
    public DateTime InvoicePaymentDate { get; set; }
    public string InvoiceDocumentId { get; set; } = "";
    public string PrescriptionNumber { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string PatientAddress { get; set; } = "";
    public string PatientTaxId { get; set; } = "";
    public string PatientPersonalId { get; set; } = "";
    public string InvoiceNumber { get; set; } = "";
    public string InvoiceItemName { get; set; } = "Lek niejałowy";
    public string PayerId { get; set; } = "";
    public string PayerName { get; set; } = "";
    public string PayerAddress { get; set; } = "";
    public string PayerTaxId { get; set; } = "";
    public string PayerPersonalId { get; set; } = "";
    public string InvoiceNotes { get; set; } = "";
    public string SellerName { get; set; } = "";
    public string SellerOwnerName { get; set; } = "";
    public string SellerAddress { get; set; } = "";
    public string SellerTaxId { get; set; } = "";
    public string SellerRegon { get; set; } = "";
    public string SellerPhone { get; set; } = "";
    public string SellerFax { get; set; } = "";
    public string SellerEmail { get; set; } = "";
    public string SellerBankName { get; set; } = "";
    public string SellerBankAccount { get; set; } = "";
    public bool IsInvoiceSelected { get; set; }
    public decimal LimitAmount { get; set; }
    public decimal OverLimitAmount { get; set; }
    public decimal PaymentAmount { get; set; }
    public decimal InvoiceNetAmount { get; set; }
    public decimal InvoiceGrossAmount { get; set; }
    public bool MissingInvoiceNumber => string.Equals(InvoiceNumber, "brak", StringComparison.OrdinalIgnoreCase);
    public string PayerDisplayName => string.IsNullOrWhiteSpace(PayerName) ? PayerId : PayerName;
    public Brush RowBrush => MissingInvoiceNumber ? MissingInvoiceBrush : Brushes.Transparent;
}
