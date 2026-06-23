using System.Collections.ObjectModel;
using PharmaExt.App.Models;

namespace PharmaExt.App.Services;

public sealed class MockFirebirdPrescriptionReader : IFirebirdPrescriptionReader
{
    private readonly AppSettings _settings;

    public MockFirebirdPrescriptionReader(AppSettings settings)
    {
        _settings = settings;
    }

    public IReadOnlyList<ImportedForm> Search(DateTime? dateFrom, DateTime? dateTo, string address)
    {
        return
        [
            CreateSampleForm("2026/001", "Jan Kowalski", LabelType.Zewnetrznie, LabelSize.Duza),
            CreateSampleForm("2026/002", "Anna Nowak", LabelType.Wewnetrznie, LabelSize.Mala)
        ];
    }

    private ImportedForm CreateSampleForm(string prescriptionNumber, string patientName, LabelType labelType, LabelSize labelSize)
    {
        return new ImportedForm
        {
            SourcePrescriptionId = prescriptionNumber,
            PrescriptionNumber = prescriptionNumber,
            PatientName = patientName,
            PatientAddress = "99-100 Leczyca, ul. Przykladowa 10",
            DoctorName = "Jerzy Jablonski",
            PreparedByName = "Katarzyna Galazka",
            PreparationDate = DateTime.Today,
            DrugForm = "Plyn",
            ExpiryTermText = "14 dni",
            Dosage = labelType == LabelType.Zewnetrznie ? "2 x dziennie do plukania" : "wedlug zalecen lekarza",
            StorageConditions = "w suchym chlodnym miejscu, temp 2-8 st.C",
            LabelType = labelType,
            LabelSize = labelSize,
            ManualCalculations = "Sprawdzono dawki maksymalne.",
            ManualPreparationDescription = "Wykonano zgodnie z procedura.",
            ManualQualityControl = "Bez zastrzezen.",
            ManualFinalAssessment = "Zgodny z wymaganiami.",
            ManualNotes = labelType == LabelType.Zewnetrznie ? "PLYN Z ANTYBIOTYKIEM" : "",
            Ingredients = new ObservableCollection<ImportedFormIngredient>
            {
                new() { Lp = 1, Name = "Neomycini sulfas", PrescribedQuantity = 1.50m, Unit = "g", BatchNumber = "A001" },
                new() { Lp = 2, Name = "Gentamycini sulfas", PrescribedQuantity = 0.60m, Unit = "g", BatchNumber = "B002" },
                new() { Lp = 3, Name = "Aqua", PrescribedQuantity = 247.90m, Unit = "g", BatchNumber = "C003" }
            }
        };
    }
}
