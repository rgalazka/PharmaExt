using System.Collections.ObjectModel;

namespace PharmaExt.App.Models;

public sealed class ImportedForm
{
    public int Id { get; set; }
    public string SourcePrescriptionId { get; set; } = "";
    public string PrescriptionNumber { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string PatientAddress { get; set; } = "";
    public string DoctorName { get; set; } = "";
    public string PreparedByName { get; set; } = "";
    public DateTime PreparationDate { get; set; } = DateTime.Today;
    public string DrugForm { get; set; } = "";
    public string ExpiryTermText { get; set; } = "14 dni";
    public string Dosage { get; set; } = "";
    public string StorageConditions { get; set; } = "";
    public LabelType LabelType { get; set; } = LabelType.Zewnetrznie;
    public LabelSize LabelSize { get; set; } = LabelSize.Duza;
    public string ManualCalculations { get; set; } = "";
    public string ManualPreparationDescription { get; set; } = "";
    public string ManualQualityControl { get; set; } = "";
    public string ManualFinalAssessment { get; set; } = "";
    public string ManualNotes { get; set; } = "";
    public FormStatus Status { get; set; } = FormStatus.Imported;
    public ObservableCollection<ImportedFormIngredient> Ingredients { get; set; } = new();
}
