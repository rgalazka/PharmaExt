namespace PharmaExt.App.Models;

public sealed class InventoryReportItem
{
    public InventoryItemRow Item { get; set; } = new();
    public IReadOnlyList<InventoryDeliveryRow> Deliveries { get; set; } = [];
}
