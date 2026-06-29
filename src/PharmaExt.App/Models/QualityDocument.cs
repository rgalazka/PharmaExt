namespace PharmaExt.App.Models;

public sealed class QualityDocument
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string CategoryCode { get; set; } = "";
    public string CategoryName { get; set; } = "";
    public string Title { get; set; } = "";
    public string Version { get; set; } = "1.0";
    public string Status { get; set; } = "Obowiazujacy";
    public string FilePath { get; set; } = "";
    public string ContentText { get; set; } = "";
    public string PreviewHtmlPath { get; set; } = "";
    public DateTime IssueDate { get; set; } = new(2026, 6, 1);
    public DateTime EffectiveDate { get; set; } = new(2026, 6, 1);
    public DateTime ImportedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public DateTime? ExpiredAt { get; set; }
}
