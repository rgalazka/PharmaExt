using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PharmaExt.App.Models;

public sealed class ImportedForm : INotifyPropertyChanged
{
    private string _drugForm = "";
    private string _dosage = "";
    private LabelType _labelType = LabelType.Zewnetrznie;
    private LabelSize _labelSize = LabelSize.Duza;
    private string _labelMedicineForm = "Solutio";
    private string _manualCalculations = "";
    private string _manualPreparationDescription = "";
    private string _manualQualityControl = "";
    private string _manualFinalAssessment = "";
    private string _manualNotes = "";
    private bool _mixBeforeUse;
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
    public string ExpiryTermText { get; set; } = "14 dni";
    public string StorageConditions { get; set; } = "";
    public FormStatus Status { get; set; } = FormStatus.Imported;
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

    public string ManualCalculations
    {
        get => _manualCalculations;
        set => SetEditableProperty(ref _manualCalculations, value);
    }

    public string ManualPreparationDescription
    {
        get => _manualPreparationDescription;
        set => SetEditableProperty(ref _manualPreparationDescription, value);
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
}
