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
            ? new LabelPdfComponent(form, pharmacy, 180, 82)
            : new LabelPdfComponent(form, pharmacy, 160, 70);
    }

    private static void ComposeProtocol(IContainer container, ImportedForm form, PharmacySettings pharmacy)
    {
        container.Column(column =>
        {
            column.Item().Border(1).Padding(4).Column(header =>
            {
                header.Item().Text("PROTOKÓŁ SPORZĄDZENIA LEKU RECEPTUROWEGO").Bold().FontSize(12);
                header.Item().Text("Zgodnie z monografią Leki sporządzane w aptece").FontSize(6);
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
                    Cell(table, "Data sporządzenia", form.PreparationDate.ToString("dd.MM.yyyy"));
                    Cell(table, "Postać leku", form.DrugForm);
                    Cell(table, "Termin ważności leku", form.ExpiryTermText);
                    Cell(table, "Osoba sporządzająca", form.PreparedByName);
                    Cell(table, "Lekarz", form.DoctorName);
                });
            });

            Section(column, "2. SKŁAD LEKU I UŻYTE SUROWCE", section =>
            {
                section.Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(18);
                        columns.RelativeColumn(2.1f);
                        columns.RelativeColumn(0.8f);
                        columns.ConstantColumn(28);
                        columns.RelativeColumn(0.8f);
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                        columns.RelativeColumn(1.2f);
                    });

                    HeaderCell(table, "Lp.");
                    HeaderCell(table, "Składnik");
                    HeaderCell(table, "Ilość przepisana");
                    HeaderCell(table, "Jedn.");
                    HeaderCell(table, "Ilość użyta");
                    HeaderCell(table, "Nr serii");
                    HeaderCell(table, "Termin ważności");
                    HeaderCell(table, "Producent");

                    var rows = Math.Max(10, form.Ingredients.Count);
                    for (var index = 0; index < rows; index++)
                    {
                        var ingredient = form.Ingredients.ElementAtOrDefault(index);
                        BodyCellCenter(table, (index + 1).ToString());
                        BodyCell(table, ingredient?.Name ?? "");
                        BodyCellRight(table, ingredient is null ? "" : ingredient.PrescribedQuantity.ToString("0.##"));
                        BodyCellCenter(table, ingredient?.Unit ?? "");
                        BodyCellRight(table, ingredient?.UsedQuantity?.ToString("0.###") ?? "");
                        BodyCell(table, ingredient?.BatchNumber ?? "");
                        BodyCell(table, ingredient?.ExpiryDate?.ToString("dd.MM.yyyy") ?? "");
                        BodyCell(table, ingredient?.ManufacturerSupplier ?? "");
                    }
                });
            });

            Section(column, "3. OBLICZENIA I SPRAWDZENIE DAWEK MAKSYMALNYCH", section =>
            {
                LoweredText(section.Height(18, Unit.Millimetre), DefaultIfEmpty(form.ManualCalculations, "Zgodnie z instrukcją numer: ____________________"));
            });

            Section(column, "4. OPIS WYKONANIA", section =>
            {
                LoweredText(section.Height(22, Unit.Millimetre), DefaultIfEmpty(form.ManualPreparationDescription, "Zgodnie z instrukcją numer: ____________________"));
            });

            Section(column, "5. WARUNKI PRZECHOWYWANIA I WYDAJNOŚĆ", section =>
            {
                LoweredText(section.Height(13, Unit.Millimetre), form.StorageConditions);
            });

            Section(column, "6. KONTROLA KOŃCOWA", section =>
            {
                LoweredText(section.Height(26, Unit.Millimetre), DefaultIfEmpty(form.ManualQualityControl, "Nieprawidłowości nie stwierdzono"));
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

                    BodyCell(table, $"Lek sporządził: {form.PreparedByName}\n\nPodpis:");
                    BodyCell(table, "Sprawdził / osoba odpowiedzialna:\n\nPodpis:");
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

    private static void BodyCellRight(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).MinHeight(10).Padding(2).AlignRight().Text(value).FontSize(6);
    }

    private static void BodyCellCenter(TableDescriptor table, string value)
    {
        table.Cell().Border(0.5f).MinHeight(10).Padding(2).AlignCenter().Text(value).FontSize(6);
    }

    private static void LoweredText(IContainer container, string value)
    {
        container.PaddingTop(3, Unit.Millimetre).PaddingLeft(3).PaddingRight(3).PaddingBottom(3).Text(value);
    }

    private static string DefaultIfEmpty(string value, string defaultValue)
    {
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value;
    }
}
