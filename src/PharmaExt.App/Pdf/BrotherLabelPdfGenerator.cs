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
                var pageWidth = form.LabelSize == LabelSize.Duza ? 180 : 160;
                var pageHeight = form.LabelSize == LabelSize.Duza ? 88 : 76;
                var tableHeight = form.LabelSize == LabelSize.Duza ? 82 : 70;

                document.Page(page =>
                {
                    page.Size(pageWidth, pageHeight, Unit.Millimetre);
                    page.Margin(0);
                    page.Content()
                        .AlignCenter()
                        .AlignMiddle()
                        .Component(new LabelPdfComponent(form, pharmacy, pageWidth, tableHeight));
                });
            }
        }).GeneratePdf(filePath);
    }
}
