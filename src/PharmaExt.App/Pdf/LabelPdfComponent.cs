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
        const float headerHeightMm = 8;
        const float pharmacyHeightMm = 9;
        const float notesHeightMm = 8;
        const float mixBeforeUseHeightMm = 5;
        const float leftColumnWeight = 0.95f;
        const float rightColumnWeight = 1.05f;
        var detailsHeightMm = _tableHeightMm - headerHeightMm - pharmacyHeightMm;
        var prescriptionHeightMm = detailsHeightMm - notesHeightMm;
        var ingredientsHeightMm = _form.MixBeforeUse ? prescriptionHeightMm - mixBeforeUseHeightMm : prescriptionHeightMm;
        var rowScale = detailsHeightMm / 38f;
        var doctorHeightMm = 6f * rowScale;
        var preparedByHeightMm = 6.5f * rowScale;
        var preparationDateHeightMm = 6f * rowScale;
        var expiryHeightMm = 6f * rowScale;
        var dosageHeightMm = 6f * rowScale;
        var storageHeightMm = 7.5f * rowScale;

        container
            .Width(_tableWidthMm, Unit.Millimetre)
            .Height(_tableHeightMm, Unit.Millimetre)
            .Border(1.5f)
            .DefaultTextStyle(text => text.FontFamily("Arial").FontSize(6))
            .Column(column =>
            {
                column.Item().BorderBottom(1).Height(headerHeightMm, Unit.Millimetre).Row(row =>
                {
                    row.RelativeItem().AlignCenter().AlignMiddle().Text(LabelTypeText()).Bold().FontSize(13);
                    row.RelativeItem().AlignCenter().AlignMiddle().Text($"Nr recepty: {_form.PrescriptionNumber}").Bold().FontSize(11);
                });

                column.Item().BorderBottom(1).Height(pharmacyHeightMm, Unit.Millimetre).Row(row =>
                {
                    row.RelativeItem(leftColumnWeight).BorderRight(1).AlignCenter().AlignMiddle().Text($"{_pharmacy.PharmacyName}\n{_pharmacy.PharmacyAddress}").Bold().FontSize(8).AlignCenter();
                    row.RelativeItem(rightColumnWeight).PaddingLeft(2).Text(text =>
                    {
                        text.Span("Imię i Nazwisko Pacjenta\n");
                        text.Span(_form.PatientName).Bold().FontSize(8);
                    });
                });

                column.Item().Height(detailsHeightMm, Unit.Millimetre).Row(row =>
                {
                    row.RelativeItem(leftColumnWeight).BorderRight(1).Column(left =>
                    {
                        LabelRow(left, "Imię i Nazwisko Lekarza", _form.DoctorName, doctorHeightMm, false);
                        LabelRow(left, "Imię i Nazwisko osoby sporządzającej lek", _form.PreparedByName, preparedByHeightMm);
                        LabelRow(left, "Data sporządzenia", _form.PreparationDate.ToString("dd.MM.yyyy"), preparationDateHeightMm);
                        LabelRow(left, "Termin ważności", _form.ExpiryTermText, expiryHeightMm);
                        LabelRow(left, "Dawkowanie", _form.Dosage, dosageHeightMm);
                        LabelRow(left, "Warunki przechowywania", _form.StorageConditions, storageHeightMm);
                    });

                    row.RelativeItem(rightColumnWeight).Column(right =>
                    {
                        right.Item().Height(prescriptionHeightMm, Unit.Millimetre).Column(prescription =>
                        {
                            prescription.Item().Height(ingredientsHeightMm, Unit.Millimetre).Column(prescriptionContent =>
                            {
                                prescriptionContent.Item().PaddingLeft(2).PaddingTop(1).Text("Rp.").Bold().FontSize(10);
                                prescriptionContent.Item().PaddingLeft(4).PaddingRight(6).PaddingTop(5, Unit.Millimetre).Column(ingredients =>
                                {
                                    foreach (var ingredient in _form.Ingredients.Where(ShouldPrintOnLabel))
                                    {
                                        ingredients.Item().Row(ingredientRow =>
                                        {
                                            ingredientRow.RelativeItem().Text(ingredient.Name).Italic().Bold().FontSize(6.5f);
                                            ingredientRow.ConstantItem(28, Unit.Millimetre).AlignRight().Text(FormatLabelQuantity(ingredient)).Italic().Bold().FontSize(6.5f);
                                        });
                                    }

                                    ingredients.Item().Text(FormatMfLine(_form.LabelMedicineForm)).Italic().Bold().FontSize(6.5f);
                                });
                            });

                            if (_form.MixBeforeUse)
                            {
                                prescription.Item().Height(mixBeforeUseHeightMm, Unit.Millimetre).AlignCenter().AlignMiddle().Text("ZMIESZAĆ PRZED UŻYCIEM").Bold().FontSize(8);
                            }
                        });

                        right.Item().BorderTop(1).Height(notesHeightMm, Unit.Millimetre).Row(notes =>
                        {
                            notes.RelativeItem().PaddingLeft(2).Text("Uwagi").FontSize(6);
                            notes.RelativeItem().AlignCenter().AlignMiddle().Text(_form.ManualNotes).Bold().FontSize(7);
                        });
                    });
                });
            });
    }

    private static void LabelRow(ColumnDescriptor column, string label, string value, float heightMm, bool drawTopBorder = true)
    {
        var item = column.Item().Height(heightMm, Unit.Millimetre);
        if (drawTopBorder)
        {
            item = item.BorderTop(1);
        }

        item.PaddingHorizontal(2).Column(inner =>
        {
            inner.Item().Text(label).FontSize(5);
            inner.Item().AlignCenter().Text(value).Bold().FontSize(8);
        });
    }

    private static bool ShouldPrintOnLabel(ImportedFormIngredient ingredient)
    {
        return !string.Equals(ingredient.Unit.Trim(), "szt", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatLabelQuantity(ImportedFormIngredient ingredient)
    {
        var quantity = ingredient.PrescribedQuantity.ToString("0.0#");
        var unit = ingredient.Unit.Trim();
        return string.IsNullOrWhiteSpace(unit) ? quantity : $"{quantity} {unit}";
    }

    private static string FormatMfLine(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "M.f." : $"M.f. {value.Trim()}";
    }

    private string LabelTypeText() => _form.LabelType == LabelType.Zewnetrznie ? "Z E W N Ę T R Z N I E" : "W E W N Ę T R Z N I E";
}
