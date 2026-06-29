using PharmaExt.App.Models;
using QuestPDF.Fluent;
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
        var isCompact = _tableHeightMm <= 55;
        var headerHeightMm = isCompact ? 6f : 8f;
        var pharmacyHeightMm = isCompact ? 7f : 9f;
        var notesHeightMm = isCompact ? 5f : 8f;
        var mixBeforeUseHeightMm = isCompact ? 3.5f : 5f;
        var headerTypeFontSize = isCompact ? 9f : 13f;
        var headerNumberFontSize = isCompact ? 8f : 11f;
        var pharmacyFontSize = isCompact ? 5.5f : 8f;
        var patientLabelFontSize = isCompact ? 4.8f : 6f;
        var patientNameFontSize = isCompact ? 6f : 8f;
        var rowLabelFontSize = isCompact ? 4.3f : 5f;
        var rowValueFontSize = isCompact ? 5.5f : 8f;
        var rpFontSize = isCompact ? 8f : 10f;
        var ingredientFontSize = isCompact ? 5f : 6.5f;
        var notesLabelFontSize = isCompact ? 4.6f : 6f;
        var notesTextFontSize = isCompact ? 5.2f : 7f;
        var leftColumnPaddingMm = isCompact ? 0.4f : 2f;
        const float leftColumnWeight = 0.95f;
        const float rightColumnWeight = 1.05f;
        var detailsHeightMm = _tableHeightMm - headerHeightMm - pharmacyHeightMm;
        var prescriptionHeightMm = detailsHeightMm - notesHeightMm;
        var ingredientsHeightMm = _form.MixBeforeUse ? prescriptionHeightMm - mixBeforeUseHeightMm : prescriptionHeightMm;
        var doctorHeightMm = isCompact ? 5f : 6f * detailsHeightMm / 38f;
        var preparedByHeightMm = isCompact ? 5.5f : 6.5f * detailsHeightMm / 38f;
        var preparationDateHeightMm = isCompact ? 4.5f : 6f * detailsHeightMm / 38f;
        var expiryHeightMm = isCompact ? 4.5f : 6f * detailsHeightMm / 38f;
        var dosageHeightMm = isCompact ? 5f : 6f * detailsHeightMm / 38f;
        var storageHeightMm = detailsHeightMm - doctorHeightMm - preparedByHeightMm - preparationDateHeightMm - expiryHeightMm - dosageHeightMm;

        container
            .Width(_tableWidthMm, Unit.Millimetre)
            .Height(_tableHeightMm, Unit.Millimetre)
            .Border(1.5f)
            .DefaultTextStyle(text => text.FontFamily("Arial").FontSize(6))
            .Column(column =>
            {
                column.Item().BorderBottom(1).Height(headerHeightMm, Unit.Millimetre).Row(row =>
                {
                    row.RelativeItem().AlignCenter().AlignMiddle().Text(LabelTypeText()).Bold().FontSize(headerTypeFontSize);
                    row.RelativeItem().AlignCenter().AlignMiddle().Text($"Nr recepty: {_form.PrescriptionNumber}").Bold().FontSize(headerNumberFontSize);
                });

                column.Item().BorderBottom(1).Height(pharmacyHeightMm, Unit.Millimetre).Row(row =>
                {
                    row.RelativeItem(leftColumnWeight)
                        .BorderRight(1)
                        .AlignCenter()
                        .AlignMiddle()
                        .Text($"{_pharmacy.PharmacyName}\n{_pharmacy.PharmacyAddress}")
                        .Bold()
                        .FontSize(pharmacyFontSize)
                        .AlignCenter();

                    row.RelativeItem(rightColumnWeight).PaddingLeft(2).Column(patient =>
                    {
                        patient.Item().Text("Imi\u0119 i Nazwisko Pacjenta").FontSize(patientLabelFontSize);
                        patient.Item()
                            .PaddingTop(isCompact ? 0.6f : 1.2f, Unit.Millimetre)
                            .AlignCenter()
                            .Text(_form.PatientName)
                            .Bold()
                            .FontSize(patientNameFontSize);
                    });
                });

                column.Item().Height(detailsHeightMm, Unit.Millimetre).Row(row =>
                {
                    row.RelativeItem(leftColumnWeight).BorderRight(1).Column(left =>
                    {
                        LabelRow(left, "Imi\u0119 i Nazwisko Lekarza", _form.DoctorName, doctorHeightMm, rowLabelFontSize, rowValueFontSize, leftColumnPaddingMm, false);
                        LabelRow(left, "Imi\u0119 i Nazwisko osoby sporz\u0105dzaj\u0105cej lek", _form.PreparedByName, preparedByHeightMm, rowLabelFontSize, rowValueFontSize, leftColumnPaddingMm);
                        LabelRow(left, "Data sporz\u0105dzenia", _form.PreparationDate.ToString("dd.MM.yyyy"), preparationDateHeightMm, rowLabelFontSize, rowValueFontSize, leftColumnPaddingMm);
                        LabelRow(left, "Termin wa\u017cno\u015bci", _form.ExpiryTermText, expiryHeightMm, rowLabelFontSize, rowValueFontSize, leftColumnPaddingMm);
                        LabelRow(left, "Dawkowanie", _form.Dosage, dosageHeightMm, rowLabelFontSize, rowValueFontSize, leftColumnPaddingMm);
                        LabelRow(left, "Warunki przechowywania", _form.StorageConditions, storageHeightMm, rowLabelFontSize, rowValueFontSize, leftColumnPaddingMm);
                    });

                    row.RelativeItem(rightColumnWeight).Column(right =>
                    {
                        right.Item().Height(prescriptionHeightMm, Unit.Millimetre).Column(prescription =>
                        {
                            prescription.Item().Height(ingredientsHeightMm, Unit.Millimetre).Column(prescriptionContent =>
                            {
                                prescriptionContent.Item().PaddingLeft(2).PaddingTop(1).Text("Rp.").Bold().FontSize(rpFontSize);
                                prescriptionContent.Item().PaddingLeft(4).PaddingRight(6).PaddingTop(isCompact ? 2.2f : 5f, Unit.Millimetre).Column(ingredients =>
                                {
                                    foreach (var ingredient in _form.Ingredients.Where(ShouldPrintOnLabel))
                                    {
                                        ingredients.Item().Row(ingredientRow =>
                                        {
                                            ingredientRow.RelativeItem().Text(ingredient.Name).Italic().Bold().FontSize(ingredientFontSize);
                                            ingredientRow.ConstantItem(28, Unit.Millimetre).AlignRight().Text(FormatLabelQuantity(ingredient)).Italic().Bold().FontSize(ingredientFontSize);
                                        });
                                    }

                                    ingredients.Item().Text(FormatMfLine(_form.LabelMedicineForm)).Italic().Bold().FontSize(ingredientFontSize);
                                });
                            });

                            if (_form.MixBeforeUse)
                            {
                                prescription.Item().Height(mixBeforeUseHeightMm, Unit.Millimetre).AlignCenter().AlignMiddle().Text("ZMIESZA\u0106 PRZED U\u017bYCIEM").Bold().FontSize(isCompact ? 5.5f : 8f);
                            }
                        });

                        right.Item().BorderTop(1).Height(notesHeightMm, Unit.Millimetre).Row(notes =>
                        {
                            notes.RelativeItem().PaddingLeft(2).Text("Uwagi").FontSize(notesLabelFontSize);
                            notes.RelativeItem().AlignCenter().AlignMiddle().Text(_form.ManualNotes).Bold().FontSize(notesTextFontSize);
                        });
                    });
                });
            });
    }

    private static void LabelRow(ColumnDescriptor column, string label, string value, float heightMm, float labelFontSize, float valueFontSize, float horizontalPaddingMm, bool drawTopBorder = true)
    {
        var item = column.Item().Height(heightMm, Unit.Millimetre);
        if (drawTopBorder)
        {
            item = item.BorderTop(1);
        }

        item.PaddingHorizontal(horizontalPaddingMm, Unit.Millimetre).Column(inner =>
        {
            inner.Item().Text(label).FontSize(labelFontSize);
            inner.Item().AlignCenter().Text(value).Bold().FontSize(valueFontSize);
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

    private string LabelTypeText() => _form.LabelType == LabelType.Zewnetrznie
        ? "Z E W N \u0118 T R Z N I E"
        : "W E W N \u0118 T R Z N I E";
}
