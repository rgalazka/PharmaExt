using PharmaExt.App.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PharmaExt.App.Pdf;

public sealed class SettlementReportPdfGenerator
{
    public void Generate(
        IReadOnlyList<SettlementReportRow> rows,
        PharmacySettings pharmacy,
        DateTime? dateFrom,
        DateTime? dateTo,
        string filePath)
    {
        const float paymentColumnWidth = 62;

        Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(12, Unit.Millimetre);
                page.DefaultTextStyle(text => text.FontFamily("Arial").FontSize(7));
                page.Content().Column(column =>
                {
                    column.Item().AlignLeft().Text("Wystawca:").Bold().FontSize(8);
                    column.Item().AlignLeft().Text(pharmacy.PharmacyName).Bold().FontSize(10);
                    if (!string.IsNullOrWhiteSpace(pharmacy.PharmacyAddress))
                    {
                        column.Item().AlignLeft().Text(pharmacy.PharmacyAddress).FontSize(7);
                    }

                    column.Item().PaddingTop(6).Text("Płatnik:").Bold().FontSize(8);
                    column.Item().Text(FormatPayer(rows)).FontSize(8);

                    column.Item().PaddingTop(6).AlignCenter().Text($"Zestawienie faktur za okres {FormatDateRange(dateFrom, dateTo)}").Bold().FontSize(12);

                    column.Item().PaddingTop(7).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(20);
                            columns.ConstantColumn(52);
                            columns.RelativeColumn(1.8f);
                            columns.RelativeColumn(2.4f);
                            columns.RelativeColumn(1.1f);
                            columns.RelativeColumn(1.3f);
                            columns.ConstantColumn(paymentColumnWidth);
                        });

                        HeaderCell(table, "Lp.");
                        HeaderCell(table, "Data sprzedaży");
                        HeaderCell(table, "Imię i nazwisko");
                        HeaderCell(table, "Adres");
                        HeaderCell(table, "Nr recepty");
                        HeaderCell(table, "Nr faktury");
                        HeaderCell(table, "Kwota do zapłaty");

                        foreach (var row in rows)
                        {
                            BodyCellCenter(table, row.DisplayNumber.ToString());
                            BodyCellCenter(table, row.SaleDate.ToString("dd.MM.yyyy"));
                            BodyCell(table, row.PatientName);
                            BodyCell(table, row.PatientAddress);
                            BodyCell(table, row.PrescriptionNumber);
                            BodyCell(table, row.InvoiceNumber);
                            BodyCellRight(table, row.PaymentAmount.ToString("N2"));
                        }
                    });

                    var total = rows.Sum(row => row.PaymentAmount);
                    column.Item().PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().PaddingRight(8).AlignRight().AlignMiddle().Text("Łącznie do zapłaty:").Bold().FontSize(8);
                        row.ConstantItem(paymentColumnWidth)
                            .Border(0.7f)
                            .Padding(3)
                            .AlignRight()
                            .Text($"{total:N2}")
                            .Bold()
                            .FontSize(8);
                    });
                });
            });
        }).GeneratePdf(filePath);
    }

    private static string FormatDateRange(DateTime? dateFrom, DateTime? dateTo)
    {
        var fromText = (dateFrom?.Date ?? DateTime.Today.AddDays(-7)).ToString("dd.MM.yyyy");
        var toText = (dateTo?.Date ?? DateTime.Today).ToString("dd.MM.yyyy");
        return $"{fromText} - {toText}";
    }

    private static string FormatPayer(IReadOnlyList<SettlementReportRow> rows)
    {
        var payerIds = rows
            .Select(row => row.PayerDisplayName.Trim())
            .Where(payerId => !string.IsNullOrWhiteSpace(payerId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return payerIds.Count == 0 ? "brak" : string.Join(", ", payerIds);
    }

    private static void HeaderCell(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).Background("#E8EEF5").Padding(2).AlignCenter().Text(value).Bold().FontSize(7);
    }

    private static void BodyCell(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).MinHeight(5, Unit.Millimetre).Padding(2).Text(value).FontSize(6.5f);
    }

    private static void BodyCellCenter(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).MinHeight(5, Unit.Millimetre).Padding(2).AlignCenter().Text(value).FontSize(6.5f);
    }

    private static void BodyCellRight(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).MinHeight(5, Unit.Millimetre).Padding(2).AlignRight().Text(value).FontSize(6.5f);
    }
}
