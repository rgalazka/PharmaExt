using PharmaExt.App.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PharmaExt.App.Pdf;

public sealed class BrotherLabelPdfGenerator
{
    public void GenerateBrotherLabels(IReadOnlyList<ImportedForm> forms, PharmacySettings pharmacy, string filePath)
    {
        Document.Create(document =>
        {
            foreach (var form in forms)
            {
                var pageHeight = form.LabelSize == LabelSize.Duza ? 120 : 100;
                var tableWidth = form.LabelSize == LabelSize.Duza ? 55 : 40;

                document.Page(page =>
                {
                    page.Size(62, pageHeight, Unit.Millimetre);
                    page.Margin(0);
                    page.Content()
                        .AlignCenter()
                        .AlignMiddle()
                        .Component(new LabelPdfComponent(form, pharmacy, tableWidth, pageHeight));
                });
            }
        }).GeneratePdf(filePath);
    }
}
