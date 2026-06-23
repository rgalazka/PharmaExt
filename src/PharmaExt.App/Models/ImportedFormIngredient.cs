namespace PharmaExt.App.Models;

public sealed class ImportedFormIngredient
{
    public int Id { get; set; }
    public int ImportedFormId { get; set; }
    public int Lp { get; set; }
    public string Name { get; set; } = "";
    public decimal PrescribedQuantity { get; set; }
    public string Unit { get; set; } = "";
    public decimal? UsedQuantity { get; set; }
    public string BatchNumber { get; set; } = "";
    public DateTime? ExpiryDate { get; set; }
    public string ManufacturerSupplier { get; set; } = "";
}
