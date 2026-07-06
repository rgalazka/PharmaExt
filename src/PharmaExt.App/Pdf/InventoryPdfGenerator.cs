using PharmaExt.App.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PharmaExt.App.Pdf;

public sealed class InventoryPdfGenerator
{
    public void Generate(IReadOnlyList<InventoryReportItem> rows, PharmacySettings pharmacy, string filePath)
    {
        Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(10, Unit.Millimetre);
                page.DefaultTextStyle(text => text.FontFamily("Arial").FontSize(7));
                page.Content().Column(column =>
                {
                    column.Item().Text(pharmacy.PharmacyName).Bold().FontSize(10);
                    if (!string.IsNullOrWhiteSpace(pharmacy.PharmacyAddress))
                    {
                        column.Item().Text(pharmacy.PharmacyAddress).FontSize(7);
                    }

                    column.Item().PaddingTop(6).AlignCenter().Text($"Magazyn substancji - {DateTime.Today:dd.MM.yyyy}").Bold().FontSize(12);

                    column.Item().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(24);
                            columns.ConstantColumn(55);
                            columns.RelativeColumn();
                            columns.ConstantColumn(58);
                            columns.ConstantColumn(58);
                            columns.ConstantColumn(62);
                        });

                        HeaderCell(table, "Lp.");
                        HeaderCell(table, "Numer");
                        HeaderCell(table, "Nazwa substancji");
                        HeaderCell(table, "Data ważn.");
                        HeaderCell(table, "BLOZ / seria");
                        HeaderCell(table, "Ilość ogólna");

                        for (var index = 0; index < rows.Count; index++)
                        {
                            var reportItem = rows[index];
                            BodyCellCenter(table, (index + 1).ToString(), bold: true);
                            BodyCell(table, reportItem.Item.Number, bold: true);
                            BodyCell(table, reportItem.Item.Name, bold: true);
                            BodyCell(table, "", bold: true);
                            BodyCell(table, reportItem.Item.Bloz7, bold: true);
                            BodyCellRight(table, reportItem.Item.TotalQuantity.ToString("0.#####"), bold: true);

                            foreach (var delivery in reportItem.Deliveries)
                            {
                                DeliveryCell(table, "", delivery.HasShortExpiry);
                                DeliveryCell(table, $"FV: {DefaultIfEmpty(delivery.InvoiceNumber, "brak")}", delivery.HasShortExpiry);
                                DeliveryCell(table, "Dostawa", delivery.HasShortExpiry);
                                DeliveryCell(table, delivery.ExpiryDate?.ToString("dd.MM.yyyy") ?? "", delivery.HasShortExpiry);
                                DeliveryCell(table, delivery.BatchNumber, delivery.HasShortExpiry);
                                DeliveryCell(table, delivery.RemainingQuantity.ToString("0.#####"), delivery.HasShortExpiry, alignRight: true);
                            }
                        }
                    });
                });
            });
        }).GeneratePdf(filePath);
    }

    private static void HeaderCell(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).Background("#E8EEF5").Padding(2).AlignCenter().Text(value).Bold().FontSize(7);
    }

    private static void BodyCell(TableDescriptor table, string value, bool bold = false)
    {
        var text = table.Cell().Border(0.5f).MinHeight(5, Unit.Millimetre).Padding(2).Text(value).FontSize(6.5f);
        if (bold)
        {
            text.Bold();
        }
    }

    private static void BodyCellCenter(TableDescriptor table, string value, bool bold = false)
    {
        var text = table.Cell().Border(0.5f).MinHeight(5, Unit.Millimetre).Padding(2).AlignCenter().Text(value).FontSize(6.5f);
        if (bold)
        {
            text.Bold();
        }
    }

    private static void BodyCellRight(TableDescriptor table, string value, bool bold = false)
    {
        var text = table.Cell().Border(0.5f).MinHeight(5, Unit.Millimetre).Padding(2).AlignRight().Text(value).FontSize(6.5f);
        if (bold)
        {
            text.Bold();
        }
    }

    private static void DeliveryCell(TableDescriptor table, string value, bool shortExpiry, bool alignRight = false)
    {
        var background = shortExpiry ? "#FEE2E2" : "#F8FAFC";
        var cell = table.Cell().Border(0.5f).Background(background).MinHeight(4, Unit.Millimetre).Padding(2);
        var content = alignRight ? cell.AlignRight() : cell;
        content.Text(value).FontSize(6);
    }

    private static string DefaultIfEmpty(string value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
