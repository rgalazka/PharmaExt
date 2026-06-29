using PharmaExt.App.Models;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace PharmaExt.App.Pdf;

public sealed class BrotherLabelPdfGenerator
{
    public void GenerateBrotherLabels(IReadOnlyList<ImportedForm> forms, PharmacySettings pharmacy, string filePath, LabelSize? labelSizeOverride = null)
    {
        Document.Create(document =>
        {
            foreach (var form in forms)
            {
                var labelSize = labelSizeOverride ?? form.LabelSize;
                var pageWidth = labelSize == LabelSize.Duza ? 180 : 140;
                var pageHeight = labelSize == LabelSize.Duza ? 88 : 76;
                var tableHeight = labelSize == LabelSize.Duza ? 80 : 50;
                var topPadding = labelSize == LabelSize.Duza ? 0.5f : 3f;

                document.Page(page =>
                {
                    page.Size(pageWidth, pageHeight, Unit.Millimetre);
                    page.Margin(0);
                    page.Content()
                        .PaddingTop(topPadding, Unit.Millimetre)
                        .AlignCenter()
                        .AlignTop()
                        .Component(new LabelPdfComponent(form, pharmacy, pageWidth, tableHeight));
                });
            }
        }).GeneratePdf(filePath);
    }
}
