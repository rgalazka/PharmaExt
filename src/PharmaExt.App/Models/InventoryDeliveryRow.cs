namespace PharmaExt.App.Models;

using System.Windows.Media;

public sealed class InventoryDeliveryRow
{
    private static readonly Brush ShortExpiryBrush = new SolidColorBrush(Color.FromRgb(254, 226, 226));

    public string InvoiceNumber { get; set; } = "";
    public DateTime? ExpiryDate { get; set; }
    public string BatchNumber { get; set; } = "";
    public decimal RemainingQuantity { get; set; }
    public bool HasShortExpiry => ExpiryDate.HasValue && ExpiryDate.Value.Date <= DateTime.Today.AddMonths(3);
    public Brush RowBrush => HasShortExpiry ? ShortExpiryBrush : Brushes.Transparent;
}
