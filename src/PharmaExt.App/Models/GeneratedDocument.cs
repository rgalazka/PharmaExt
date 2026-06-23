namespace PharmaExt.App.Models;

public sealed class GeneratedDocument
{
    public int Id { get; set; }
    public GeneratedDocumentType DocumentType { get; set; }
    public string FilePath { get; set; } = "";
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public int? ImportedFormId { get; set; }
}
