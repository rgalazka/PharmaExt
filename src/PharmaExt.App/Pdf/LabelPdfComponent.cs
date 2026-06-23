using PharmaExt.App.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PharmaExt.App.Pdf;

public sealed class LabelPdfComponent : IComponent
{
    private readonly ImportedForm _form;
    private readonly PharmacySettings _pharmacy;
    private readonly float _tableWidthMm;
    private readonly float _tableHeightMm;

    public LabelPdfComponent(ImportedForm form, PharmacySettings pharmacy, float tableWidthMm, float tableHeightMm)
    {
        _form = form;
        _pharmacy = pharmacy;
        _tableWidthMm = tableWidthMm;
        _tableHeightMm = tableHeightMm;
    }

    public void Compose(IContainer container)
    {
        container
            .Width(_tableWidthMm, Unit.Millimetre)
            .Height(_tableHeightMm, Unit.Millimetre)
            .Border(1.5f)
            .DefaultTextStyle(text => text.FontFamily("Arial").FontSize(7))
            .Column(column =>
            {
                column.Item().BorderBottom(1).Height(13, Unit.Millimetre).Row(row =>
                {
                    row.RelativeItem().AlignCenter().AlignMiddle().Text(LabelTypeText()).Bold().FontSize(18).CharacterSpacing(4);
                    row.RelativeItem().AlignCenter().AlignMiddle().Text($"Nr recepty: {_form.PrescriptionNumber}").Bold().FontSize(14);
                });

                column.Item().Height(18, Unit.Millimetre).Row(row =>
                {
                    row.RelativeItem().BorderRight(1).AlignCenter().AlignMiddle().Text($"{_pharmacy.PharmacyName}\n{_pharmacy.PharmacyAddress}").Bold().FontSize(11).AlignCenter();
                    row.RelativeItem().PaddingLeft(2).Text(text =>
                    {
                        text.Span("Imie i Nazwisko Pacjenta\n");
                        text.Span(_form.PatientName).Bold().FontSize(10);
                    });
                });

                column.Item().Extend().Row(row =>
                {
                    row.RelativeItem().BorderRight(1).Column(left =>
                    {
                        LabelRow(left, "Imie i Nazwisko Lekarza", _form.DoctorName, 13);
                        LabelRow(left, "Imie i Nazwisko osoby sporzadzajacej lek", _form.PreparedByName, 14);
                        LabelRow(left, "Data sporzadzenia", _form.PreparationDate.ToString("dd.MM.yyyy"), 13);
                        LabelRow(left, "Termin waznosci", _form.ExpiryTermText, 12);
                        LabelRow(left, "Dawkowanie", _form.Dosage, 14);
                        LabelRow(left, "Warunki przechowywania", _form.StorageConditions, 0);
                    });

                    row.RelativeItem().Column(right =>
                    {
                        right.Item().Padding(2).Text("Rp.").Bold().FontSize(14);
                        right.Item().PaddingHorizontal(3).Extend().Text(BuildIngredientsText()).Italic().Bold().FontSize(8);
                        right.Item().BorderTop(1).Height(15, Unit.Millimetre).Padding(2).Text(text =>
                        {
                            text.Span("Uwagi\n");
                            text.Span(_form.ManualNotes).Bold().FontSize(9);
                        });
                    });
                });
            });
    }

    private static void LabelRow(ColumnDescriptor column, string label, string value, float heightMm)
    {
        var item = heightMm > 0 ? column.Item().Height(heightMm, Unit.Millimetre) : column.Item().Extend();
        item.BorderTop(1).Padding(2).Column(inner =>
        {
            inner.Item().Text(label).FontSize(7);
            inner.Item().AlignCenter().Text(value).Bold().FontSize(10);
        });
    }

    private string BuildIngredientsText()
    {
        return string.Join("\n", _form.Ingredients.Select(ingredient =>
            $"{ingredient.Name}    {ingredient.PrescribedQuantity:0.##} {ingredient.Unit}"));
    }

    private string LabelTypeText() => _form.LabelType == LabelType.Zewnetrznie ? "ZEWNETRZNIE" : "WEWNETRZNIE";
}
