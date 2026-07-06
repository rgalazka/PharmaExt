namespace PharmaExt.App.Models;

public sealed class InventoryItemRow
{
    public int FirebirdId { get; set; }
    public string Number { get; set; } = "";
    public string Name { get; set; } = "";
    public string Bloz7 { get; set; } = "";
    public decimal TotalQuantity { get; set; }
}
