using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PharmaExt.App.Models;

public sealed class ImportedForm : INotifyPropertyChanged
{
    private string _drugForm = "";
    private string _dosage = "";
    private LabelType _labelType = LabelType.Zewnetrznie;
    private LabelSize _labelSize = LabelSize.Duza;
    private string _labelMedicineForm = "Solutio";
    private string _expiryTermText = "14 dni";
    private string _manualCalculations = "";
    private string _manualPreparationDescription = "";
    private string _manualQualityControl = "";
    private string _manualFinalAssessment = "";
    private string _manualNotes = "";
    private bool _mixBeforeUse;
    private FormStatus _status = FormStatus.Imported;
    private bool _isEditedThisSession;
    private string _compositionGroupBrush = "Transparent";
    private int _compositionGroupNumber;
    private bool _trackEdits;

    public event PropertyChangedEventHandler? PropertyChanged;

    public int DisplayNumber { get; set; }
    public int Id { get; set; }
    public string FirebirdPatientId { get; set; } = "";
    public string FirebirdDoctorId { get; set; } = "";
    public string SourcePrescriptionId { get; set; } = "";
    public string PrescriptionNumber { get; set; } = "";
    public string PrescriptionOrderNumber { get; set; } = "";
    public string PrescriptionBarcode { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string PatientAddress { get; set; } = "";
    public string DoctorName { get; set; } = "";
    public string PreparedByName { get; set; } = "";
    public DateTime? AcceptanceDate { get; set; }
    public DateTime PreparationDate { get; set; } = DateTime.Today;
    public DateTime? SaleDate { get; set; }
    public string StorageConditions { get; set; } = "";
    public bool IngredientsLoaded { get; set; }
    public ObservableCollection<ImportedFormIngredient> Ingredients { get; set; } = new();

    public string DrugForm
    {
        get => _drugForm;
        set => SetEditableProperty(ref _drugForm, value);
    }

    public string Dosage
    {
        get => _dosage;
        set => SetEditableProperty(ref _dosage, value);
    }

    public LabelType LabelType
    {
        get => _labelType;
        set => SetEditableProperty(ref _labelType, value);
    }

    public LabelSize LabelSize
    {
        get => _labelSize;
        set => SetEditableProperty(ref _labelSize, value);
    }

    public string LabelMedicineForm
    {
        get => _labelMedicineForm;
        set => SetEditableProperty(ref _labelMedicineForm, value);
    }

    public string ExpiryTermText
    {
        get => _expiryTermText;
        set
        {
            if (EqualityComparer<string>.Default.Equals(_expiryTermText, value))
            {
                return;
            }

            _expiryTermText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(MedicineExpiryDate));
            MarkEdited();
        }
    }

    public DateTime? MedicineExpiryDate
    {
        get => TryParseDate(ExpiryTermText, out var date) ? date : null;
        set => ExpiryTermText = value?.ToString("dd.MM.yyyy") ?? "";
    }

    public string ManualCalculations
    {
        get => _manualCalculations;
        set
        {
            SetEditableProperty(ref _manualCalculations, value);
            OnPropertyChanged(nameof(HasInstruction));
        }
    }

    public string ManualPreparationDescription
    {
        get => _manualPreparationDescription;
        set
        {
            SetEditableProperty(ref _manualPreparationDescription, value);
            OnPropertyChanged(nameof(HasInstruction));
        }
    }

    public string ManualQualityControl
    {
        get => _manualQualityControl;
        set => SetEditableProperty(ref _manualQualityControl, value);
    }

    public string ManualFinalAssessment
    {
        get => _manualFinalAssessment;
        set => SetEditableProperty(ref _manualFinalAssessment, value);
    }

    public string ManualNotes
    {
        get => _manualNotes;
        set => SetEditableProperty(ref _manualNotes, value);
    }

    public bool MixBeforeUse
    {
        get => _mixBeforeUse;
        set => SetEditableProperty(ref _mixBeforeUse, value);
    }

    public FormStatus Status
    {
        get => _status;
        set
        {
            if (_status == value)
            {
                return;
            }

            _status = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasGeneratedPdf));
            MarkEdited();
        }
    }

    public bool HasInstruction =>
        ContainsInstructionCode(ManualCalculations)
        || ContainsInstructionCode(ManualPreparationDescription);

    public bool HasGeneratedPdf => Status == FormStatus.PdfGenerated;

    public bool IsEditedThisSession
    {
        get => _isEditedThisSession;
        private set
        {
            if (_isEditedThisSession == value)
            {
                return;
            }

            _isEditedThisSession = value;
            OnPropertyChanged();
        }
    }

    public string CompositionGroupBrush
    {
        get => _compositionGroupBrush;
        private set
        {
            if (_compositionGroupBrush == value)
            {
                return;
            }

            _compositionGroupBrush = value;
            OnPropertyChanged();
        }
    }

    public int CompositionGroupSort => _compositionGroupNumber == 0 ? int.MaxValue : _compositionGroupNumber;

    public string CompositionGroupLabel => _compositionGroupNumber == 0 ? "" : _compositionGroupNumber.ToString();

    public void SetCompositionGroupBrush(string brush)
    {
        CompositionGroupBrush = brush;
    }

    public void SetCompositionGroup(int groupNumber, string brush)
    {
        if (_compositionGroupNumber != groupNumber)
        {
            _compositionGroupNumber = groupNumber;
            OnPropertyChanged(nameof(CompositionGroupSort));
            OnPropertyChanged(nameof(CompositionGroupLabel));
        }

        CompositionGroupBrush = brush;
    }

    public void EnableEditTracking(bool resetEditedFlag = true)
    {
        _trackEdits = true;
        if (resetEditedFlag)
        {
            IsEditedThisSession = false;
        }

        foreach (var ingredient in Ingredients)
        {
            ingredient.EnableEditTracking(MarkEdited);
        }
    }

    public void MarkEdited()
    {
        if (_trackEdits)
        {
            IsEditedThisSession = true;
        }
    }

    private void SetEditableProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(propertyName);
        MarkEdited();
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private static bool ContainsInstructionCode(string value)
    {
        return Regex.IsMatch(value, @"\bIR\s*-\s*\d{1,4}\b", RegexOptions.IgnoreCase);
    }

    private static bool TryParseDate(string value, out DateTime date)
    {
        var formats = new[] { "dd.MM.yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
        return DateTime.TryParseExact(value, formats, null, System.Globalization.DateTimeStyles.None, out date)
            || DateTime.TryParse(value, out date);
    }
}
