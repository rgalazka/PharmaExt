using PharmaExt.App.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PharmaExt.App.Pdf;

public sealed class ProtocolPdfGenerator
{
    public void GenerateProtocolWithA4Label(ImportedForm form, PharmacySettings pharmacy, string filePath)
    {
        Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(10, Unit.Millimetre);
                page.DefaultTextStyle(text => text.FontFamily("Arial").FontSize(7));
                page.Content().Element(container => ComposeProtocol(container, form, pharmacy));
            });

            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(10, Unit.Millimetre);
                page.DefaultTextStyle(text => text.FontFamily("Arial").FontSize(8));
                page.Content().AlignCenter().AlignMiddle().Component(CreateA4LabelComponent(form, pharmacy));
            });
        }).GeneratePdf(filePath);
    }

    private static LabelPdfComponent CreateA4LabelComponent(ImportedForm form, PharmacySettings pharmacy)
    {
        return form.LabelSize == LabelSize.Duza
            ? new LabelPdfComponent(form, pharmacy, 55, 120)
            : new LabelPdfComponent(form, pharmacy, 40, 100);
    }

    private static void ComposeProtocol(IContainer container, ImportedForm form, PharmacySettings pharmacy)
    {
        container.Column(column =>
        {
            column.Item().Border(1).Padding(4).Column(header =>
            {
                header.Item().Text("PROTOKOL SPORZADZENIA LEKU RECEPTUROWEGO").Bold().FontSize(12);
                header.Item().Text("Zgodnie z monografia Leki sporzadzane w aptece").FontSize(6);
            });

            Section(column, "1. IDENTYFIKACJA", section =>
            {
                section.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    Cell(table, "Apteka", $"{pharmacy.PharmacyName}, {pharmacy.PharmacyAddress}");
                    Cell(table, "Nr recepty", form.PrescriptionNumber);
                    Cell(table, "Pacjent / oznaczenie recepty", form.PatientName);
                    Cell(table, "Data sporzadzenia", form.PreparationDate.ToString("dd.MM.yyyy"));
                    Cell(table, "Postac leku", form.DrugForm);
                    Cell(table, "Termin waznosci leku", form.ExpiryTermText);
                    Cell(table, "Osoba sporzadzajaca", form.PreparedByName);
                    Cell(table, "Lekarz", form.DoctorName);
                });
            });

            Section(column, "2. SKLAD LEKU I UZYTE SUROWCE", section =>
            {
                section.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(20);
                        columns.RelativeColumn(2);
                        columns.RelativeColumn();
                        columns.ConstantColumn(35);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    HeaderCell(table, "Lp.");
                    HeaderCell(table, "Skladnik");
                    HeaderCell(table, "Ilosc przepisana");
                    HeaderCell(table, "Jedn.");
                    HeaderCell(table, "Ilosc uzyta");
                    HeaderCell(table, "Nr serii");
                    HeaderCell(table, "Termin waznosci");

                    var rows = Math.Max(10, form.Ingredients.Count);
                    for (var index = 0; index < rows; index++)
                    {
                        var ingredient = form.Ingredients.ElementAtOrDefault(index);
                        BodyCell(table, (index + 1).ToString());
                        BodyCell(table, ingredient?.Name ?? "");
                        BodyCell(table, ingredient is null ? "" : ingredient.PrescribedQuantity.ToString("0.##"));
                        BodyCell(table, ingredient?.Unit ?? "");
                        BodyCell(table, ingredient?.UsedQuantity?.ToString("0.##") ?? "");
                        BodyCell(table, ingredient?.BatchNumber ?? "");
                        BodyCell(table, ingredient?.ExpiryDate?.ToString("dd.MM.yyyy") ?? "");
                    }
                });
            });

            Section(column, "3. OBLICZENIA I SPRAWDZENIE DAWEK MAKSYMALNYCH", section =>
            {
                section.Height(18, Unit.Millimetre).Padding(3).Text(form.ManualCalculations);
            });

            Section(column, "4. OPIS WYKONANIA", section =>
            {
                section.Height(22, Unit.Millimetre).Padding(3).Text(form.ManualPreparationDescription);
            });

            Section(column, "5. WARUNKI PRZECHOWYWANIA I WYDAJNOSC", section =>
            {
                section.Height(13, Unit.Millimetre).Padding(3).Text(form.StorageConditions);
            });

            Section(column, "6. KONTROLA KONCOWA", section =>
            {
                section.Height(26, Unit.Millimetre).Padding(3).Text(form.ManualQualityControl);
            });

            Section(column, "7. PODPISY", section =>
            {
                section.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    BodyCell(table, $"Lek sporzadzil: {form.PreparedByName}\n\nPodpis:");
                    BodyCell(table, "Sprawdzil / osoba odpowiedzialna:\n\nPodpis:");
                });
            });
        });
    }

    private static void Section(ColumnDescriptor column, string title, Action<IContainer> content)
    {
        column.Item().PaddingTop(3).Column(section =>
        {
            section.Item().Background("#1F4E79").Padding(2).Text(title).FontColor(Colors.White).Bold().FontSize(7);
            section.Item().Border(1).Element(content);
        });
    }

    private static void Cell(TableDescriptor table, string label, string value)
    {
        table.Cell().Border(0.5f).Padding(2).Text(text =>
        {
            text.Span(label + ": ").Bold();
            text.Span(value);
        });
    }

    private static void HeaderCell(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).Background("#E8EEF5").Padding(2).AlignCenter().Text(value).Bold().FontSize(6);
    }

    private static void BodyCell(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).MinHeight(10).Padding(2).Text(value).FontSize(6);
    }
}
