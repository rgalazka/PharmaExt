using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using PharmaExt.App.Models;
using PharmaExt.App.Pdf;
using PharmaExt.App.Services;

namespace PharmaExt.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IFirebirdPrescriptionReader _prescriptionReader;
    private readonly LocalDatabaseService _localDatabase;
    private readonly ProtocolPdfGenerator _protocolPdfGenerator;
    private readonly BrotherLabelPdfGenerator _labelPdfGenerator;
    private ImportedForm? _selectedForm;
    private BatchEditFieldOption? _selectedBatchEditField;
    private string _batchEditValue = "";
    private bool _isSearching;

    public MainViewModel()
    {
        _localDatabase = new LocalDatabaseService();
        _localDatabase.Initialize();

        Settings = _localDatabase.LoadSettings();
        _prescriptionReader = new MockFirebirdPrescriptionReader(Settings);
        _protocolPdfGenerator = new ProtocolPdfGenerator();
        _labelPdfGenerator = new BrotherLabelPdfGenerator();

        SearchDateFrom = DateTime.Today.AddDays(-7);
        SearchDateTo = DateTime.Today;

        SearchCommand = new RelayCommand(Search, () => !IsSearching);
        SaveCommand = new RelayCommand(Save);
        TestFirebirdConnectionCommand = new RelayCommand(TestFirebirdConnection);
        GenerateProtocolPdfCommand = new RelayCommand(GenerateProtocolPdf, () => SelectedForm is not null);
        GenerateBatchPdfCommand = new RelayCommand(GenerateBatchPdf, () => ImportedForms.Count > 0);
        GenerateExternalLabelsCommand = new RelayCommand(() => GenerateLabels(LabelType.Zewnetrznie));
        GenerateInternalLabelsCommand = new RelayCommand(() => GenerateLabels(LabelType.Wewnetrznie));
        ResetLocalDataCommand = new RelayCommand(ResetLocalData);
        ApplyBatchEditCommand = new RelayCommand(ApplyBatchEdit, CanApplyBatchEdit);
        CopyCheckedFieldsCommand = new RelayCommand(CopyCheckedFields, CanCopyCheckedFields);
        SelectedBatchEditField = BatchEditFields.FirstOrDefault();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppSettings Settings { get; }
    public DateTime? SearchDateFrom { get; set; }
    public DateTime? SearchDateTo { get; set; }
    public string SearchAddress { get; set; } = "";
    public ObservableCollection<ImportedForm> FoundPrescriptions { get; } = new();
    public ObservableCollection<ImportedForm> ImportedForms { get; } = new();
    public ObservableCollection<ImportedForm> SelectedImportedForms { get; } = new();
    public BatchEditFieldOption[] BatchEditFields { get; } =
    [
        new("Typ etykiety", BatchEditField.LabelType),
        new("Rozmiar etykiety", BatchEditField.LabelSize),
        new("M.f. / Postac leku", BatchEditField.LabelMedicineForm),
        new("Dawkowanie", BatchEditField.Dosage),
        new("Obliczenia", BatchEditField.ManualCalculations),
        new("Opis wykonania", BatchEditField.ManualPreparationDescription),
        new("Kontrola koncowa", BatchEditField.ManualQualityControl),
        new("Ocena koncowa", BatchEditField.ManualFinalAssessment),
        new("Uwagi", BatchEditField.ManualNotes),
        new("ZMIESZAC PRZED UZYCIEM", BatchEditField.MixBeforeUse)
    ];
    public LabelType[] LabelTypes { get; } = Enum.GetValues<LabelType>();
    public LabelSize[] LabelSizes { get; } = Enum.GetValues<LabelSize>();
    public LabelMedicineFormOption[] LabelMedicineFormOptions { get; } =
    [
        new("Unguentum", "maść"),
        new("Pasta", "pasta"),
        new("Cremor", "krem"),
        new("Gelum", "żel"),
        new("Solutio", "roztwór"),
        new("Suspensio", "zawiesina"),
        new("Emulsio", "emulsja"),
        new("Pulvis", "proszek"),
        new("Capsula", "kapsułka"),
        new("Suppositorium", "czopek"),
        new("Guttae", "krople"),
        new("Mixtura", "mieszanka"),
        new("Linimentum", "mazidło"),
        new("Suppositoria", "czopki"),
        new("Globuli vaginales", "globulki dopochwowe"),
        new("Pulveres (Pulv.)", "proszki"),
        new("Guttae ophthalmicae", "krople do oczu")
    ];
    public string[] FirebirdCharsets { get; } = ["DOMYSLNE", "NONE", "ISO8859_2", "WIN1250", "UTF8"];
    public bool CopyLabelType { get; set; }
    public bool CopyLabelSize { get; set; }
    public bool CopyLabelMedicineForm { get; set; }
    public bool CopyMixBeforeUse { get; set; }
    public bool CopyManualCalculations { get; set; }
    public bool CopyManualPreparationDescription { get; set; }
    public bool CopyManualQualityControl { get; set; }
    public bool CopyDosage { get; set; }
    public bool CopyManualNotes { get; set; }

    public BatchEditFieldOption? SelectedBatchEditField
    {
        get => _selectedBatchEditField;
        set
        {
            _selectedBatchEditField = value;
            OnPropertyChanged();
            (ApplyBatchEditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string BatchEditValue
    {
        get => _batchEditValue;
        set
        {
            _batchEditValue = value;
            OnPropertyChanged();
        }
    }

    public bool IsSearching
    {
        get => _isSearching;
        private set
        {
            if (_isSearching == value)
            {
                return;
            }

            _isSearching = value;
            OnPropertyChanged();
            (SearchCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ImportedForm? SelectedForm
    {
        get => _selectedForm;
        set
        {
            _selectedForm = value;
            LoadSelectedFormIngredients();
            OnPropertyChanged();
            (GenerateProtocolPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CopyCheckedFieldsCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ICommand SearchCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand TestFirebirdConnectionCommand { get; }
    public ICommand GenerateProtocolPdfCommand { get; }
    public ICommand GenerateBatchPdfCommand { get; }
    public ICommand GenerateExternalLabelsCommand { get; }
    public ICommand GenerateInternalLabelsCommand { get; }
    public ICommand ResetLocalDataCommand { get; }
    public ICommand ApplyBatchEditCommand { get; }
    public ICommand CopyCheckedFieldsCommand { get; }

    public void SetSelectedImportedForms(IEnumerable<ImportedForm> forms)
    {
        SelectedImportedForms.Clear();
        foreach (var form in forms)
        {
            SelectedImportedForms.Add(form);
        }

        OnPropertyChanged(nameof(SelectedImportedForms));
        (ApplyBatchEditCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (CopyCheckedFieldsCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private async void Search()
    {
        try
        {
            IsSearching = true;
            FoundPrescriptions.Clear();
            var reader = string.IsNullOrWhiteSpace(Settings.Firebird.DatabasePath)
                ? _prescriptionReader
                : new FirebirdPrescriptionReader(Settings);
            var searchDateFrom = SearchDateFrom;
            var searchDateTo = SearchDateTo;
            var searchAddress = SearchAddress;
            var results = await Task.Run(() =>
            {
                var localResults = _localDatabase.SearchImportedForms(searchDateFrom, searchDateTo, searchAddress);
                if (localResults.Count > 0)
                {
                    return localResults;
                }

                var firebirdResults = reader.Search(searchDateFrom, searchDateTo, searchAddress);
                if (reader is FirebirdPrescriptionReader firebirdReader)
                {
                    firebirdReader.LoadIngredients(firebirdResults);
                }

                return firebirdResults;
            });

            var displayNumber = 1;
            foreach (var form in results)
            {
                form.DisplayNumber = displayNumber++;
                FoundPrescriptions.Add(form);
            }

            ImportFoundPrescriptions();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udalo sie pobrac recept z Firebird.\n\n{exception.Message}",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsSearching = false;
        }
    }

    private void ImportFoundPrescriptions()
    {
        ImportedForms.Clear();
        foreach (var form in FoundPrescriptions)
        {
            form.EnableEditTracking();
            ImportedForms.Add(form);
        }

        AssignCompositionGroupColors();
        SelectedForm = null;
        (GenerateBatchPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    private void LoadSelectedFormIngredients()
    {
        if (_selectedForm is null || _selectedForm.IngredientsLoaded || string.IsNullOrWhiteSpace(Settings.Firebird.DatabasePath))
        {
            return;
        }

        try
        {
            var reader = new FirebirdPrescriptionReader(Settings);
            reader.LoadIngredients(_selectedForm);
            _selectedForm.IngredientsLoaded = true;
            _selectedForm.EnableEditTracking(resetEditedFlag: false);
            AssignCompositionGroupColors();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udalo sie pobrac skladnikow recepty.\n\n{exception.Message}",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void EnsureIngredientsLoaded(ImportedForm form)
    {
        if (form.IngredientsLoaded || string.IsNullOrWhiteSpace(Settings.Firebird.DatabasePath))
        {
            return;
        }

        var reader = new FirebirdPrescriptionReader(Settings);
        reader.LoadIngredients(form);
        form.IngredientsLoaded = true;
        form.EnableEditTracking(resetEditedFlag: false);
        AssignCompositionGroupColors();
    }

    private void AssignCompositionGroupColors()
    {
        var colors = new[]
        {
            "#E0F2FE",
            "#FEF3C7",
            "#FCE7F3",
            "#EDE9FE",
            "#DCFCE7",
            "#FFE4E6",
            "#CCFBF1",
            "#F3E8FF",
            "#E2E8F0",
            "#ECFCCB"
        };

        foreach (var form in ImportedForms)
        {
            form.SetCompositionGroup(0, "Transparent");
        }

        var groups = ImportedForms
            .Select(form => new { Form = form, Key = BuildCompositionKey(form) })
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .GroupBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .OrderBy(group => group.Key)
            .ToList();

        for (var index = 0; index < groups.Count; index++)
        {
            var color = colors[index % colors.Length];
            var groupNumber = index + 1;
            foreach (var item in groups[index])
            {
                item.Form.SetCompositionGroup(groupNumber, color);
            }
        }
    }

    private static string BuildCompositionKey(ImportedForm form)
    {
        var ingredientNames = form.Ingredients
            .Where(ingredient => IsCompositionUnit(ingredient.Unit))
            .Select(ingredient => NormalizeCompositionText(ingredient.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return ingredientNames.Count == 0 ? "" : string.Join("|", ingredientNames);
    }

    private static bool IsCompositionUnit(string unit)
    {
        var normalizedUnit = NormalizeCompositionText(unit);
        return normalizedUnit is "g" or "op";
    }

    private static string NormalizeCompositionText(string value)
    {
        var normalized = value.Trim().ToLowerInvariant();
        while (normalized.Contains("  ", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("  ", " ");
        }

        return normalized;
    }

    private void Save()
    {
        _localDatabase.SaveSettings(Settings);
        foreach (var form in ImportedForms)
        {
            EnsureIngredientsLoaded(form);
            _localDatabase.UpsertImportedForm(form);
        }

        MessageBox.Show("Zapisano dane lokalnie.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void TestFirebirdConnection()
    {
        try
        {
            var reader = new FirebirdPrescriptionReader(Settings);
            reader.TestConnection();

            MessageBox.Show(
                $"Polaczenie z baza Firebird dziala.\n\nKodowanie: {Settings.Firebird.Charset}",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udalo sie polaczyc z baza Firebird.\n\n{exception.Message}",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ResetLocalData()
    {
        var result = MessageBox.Show(
            "Wyczyscic lokalnie zapisane formularze, skladniki i historie wygenerowanych dokumentow?\n\nUstawienia programu zostana zachowane.",
            "PharmaExt",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            _localDatabase.ResetLocalData();
            FoundPrescriptions.Clear();
            ImportedForms.Clear();
            SelectedForm = null;
            (GenerateBatchPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();

            MessageBox.Show(
                "Wyczyszczono lokalna baze formularzy. Przy kolejnym wyszukiwaniu dane zostana pobrane z Firebird.",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udalo sie wyczyscic lokalnej bazy formularzy.\n\n{exception.Message}",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private bool CanApplyBatchEdit() => SelectedImportedForms.Count > 0 && SelectedBatchEditField is not null;

    private void ApplyBatchEdit()
    {
        if (SelectedBatchEditField is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var form in SelectedImportedForms)
            {
                ApplyBatchValue(form, SelectedBatchEditField.Field, BatchEditValue);
            }

            CollectionViewSource.GetDefaultView(ImportedForms).Refresh();
            OnPropertyChanged(nameof(SelectedForm));

            MessageBox.Show(
                $"Ustawiono pole dla zaznaczonych recept: {SelectedImportedForms.Count}. Kliknij Zapisz, zeby utrwalic zmiany.",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udalo sie ustawic pola zbiorczo.\n\n{exception.Message}",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private bool CanCopyCheckedFields() => SelectedForm is not null && SelectedImportedForms.Count > 0;

    private void CopyCheckedFields()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        if (!HasCheckedCopyFields())
        {
            MessageBox.Show(
                "Zaznacz przynajmniej jedno pole do skopiowania.",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var changedCount = 0;
        foreach (var target in SelectedImportedForms.Where(form => !ReferenceEquals(form, SelectedForm)))
        {
            CopyCheckedValues(SelectedForm, target);
            changedCount++;
        }

        ClearCheckedCopyFields();
        CollectionViewSource.GetDefaultView(ImportedForms).Refresh();

        MessageBox.Show(
            $"Skopiowano zaznaczone pola do recept: {changedCount}. Kliknij Zapisz, zeby utrwalic zmiany.",
            "PharmaExt",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private bool HasCheckedCopyFields()
    {
        return CopyLabelType
            || CopyLabelSize
            || CopyLabelMedicineForm
            || CopyMixBeforeUse
            || CopyManualCalculations
            || CopyManualPreparationDescription
            || CopyManualQualityControl
            || CopyDosage
            || CopyManualNotes;
    }

    private void ClearCheckedCopyFields()
    {
        CopyLabelType = false;
        CopyLabelSize = false;
        CopyLabelMedicineForm = false;
        CopyMixBeforeUse = false;
        CopyManualCalculations = false;
        CopyManualPreparationDescription = false;
        CopyManualQualityControl = false;
        CopyDosage = false;
        CopyManualNotes = false;

        OnPropertyChanged(nameof(CopyLabelType));
        OnPropertyChanged(nameof(CopyLabelSize));
        OnPropertyChanged(nameof(CopyLabelMedicineForm));
        OnPropertyChanged(nameof(CopyMixBeforeUse));
        OnPropertyChanged(nameof(CopyManualCalculations));
        OnPropertyChanged(nameof(CopyManualPreparationDescription));
        OnPropertyChanged(nameof(CopyManualQualityControl));
        OnPropertyChanged(nameof(CopyDosage));
        OnPropertyChanged(nameof(CopyManualNotes));
    }

    private void CopyCheckedValues(ImportedForm source, ImportedForm target)
    {
        if (CopyLabelType)
        {
            target.LabelType = source.LabelType;
        }

        if (CopyLabelSize)
        {
            target.LabelSize = source.LabelSize;
        }

        if (CopyLabelMedicineForm)
        {
            target.LabelMedicineForm = source.LabelMedicineForm;
            target.DrugForm = source.DrugForm;
        }

        if (CopyMixBeforeUse)
        {
            target.MixBeforeUse = source.MixBeforeUse;
        }

        if (CopyManualCalculations)
        {
            target.ManualCalculations = source.ManualCalculations;
        }

        if (CopyManualPreparationDescription)
        {
            target.ManualPreparationDescription = source.ManualPreparationDescription;
        }

        if (CopyManualQualityControl)
        {
            target.ManualQualityControl = source.ManualQualityControl;
        }

        if (CopyDosage)
        {
            target.Dosage = source.Dosage;
        }

        if (CopyManualNotes)
        {
            target.ManualNotes = source.ManualNotes;
        }
    }

    private static void ApplyBatchValue(ImportedForm form, BatchEditField field, string value)
    {
        switch (field)
        {
            case BatchEditField.LabelType:
                form.LabelType = ParseLabelType(value);
                break;
            case BatchEditField.LabelSize:
                form.LabelSize = ParseLabelSize(value);
                break;
            case BatchEditField.LabelMedicineForm:
                form.LabelMedicineForm = value.Trim();
                form.DrugForm = value.Trim();
                break;
            case BatchEditField.Dosage:
                form.Dosage = value;
                break;
            case BatchEditField.ManualCalculations:
                form.ManualCalculations = value;
                break;
            case BatchEditField.ManualPreparationDescription:
                form.ManualPreparationDescription = value;
                break;
            case BatchEditField.ManualQualityControl:
                form.ManualQualityControl = value;
                break;
            case BatchEditField.ManualFinalAssessment:
                form.ManualFinalAssessment = value;
                break;
            case BatchEditField.ManualNotes:
                form.ManualNotes = value;
                break;
            case BatchEditField.MixBeforeUse:
                form.MixBeforeUse = ParseBoolean(value);
                break;
            default:
                throw new InvalidOperationException("Nieznane pole edycji zbiorczej.");
        }
    }

    private static LabelType ParseLabelType(string value)
    {
        var normalized = Normalize(value);
        if (normalized.StartsWith("zew", StringComparison.OrdinalIgnoreCase))
        {
            return LabelType.Zewnetrznie;
        }

        if (normalized.StartsWith("wew", StringComparison.OrdinalIgnoreCase))
        {
            return LabelType.Wewnetrznie;
        }

        throw new InvalidOperationException("Dla typu etykiety wpisz: zew albo wew.");
    }

    private static LabelSize ParseLabelSize(string value)
    {
        var normalized = Normalize(value);
        if (normalized.StartsWith("mal", StringComparison.OrdinalIgnoreCase))
        {
            return LabelSize.Mala;
        }

        if (normalized.StartsWith("duz", StringComparison.OrdinalIgnoreCase))
        {
            return LabelSize.Duza;
        }

        throw new InvalidOperationException("Dla rozmiaru etykiety wpisz: mala albo duza.");
    }

    private static bool ParseBoolean(string value)
    {
        var normalized = Normalize(value);
        return normalized switch
        {
            "tak" or "t" or "true" or "1" or "yes" => true,
            "nie" or "n" or "false" or "0" or "no" => false,
            _ => throw new InvalidOperationException("Dla pola tak/nie wpisz: tak albo nie.")
        };
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant()
            .Replace("ą", "a")
            .Replace("ć", "c")
            .Replace("ę", "e")
            .Replace("ł", "l")
            .Replace("ń", "n")
            .Replace("ó", "o")
            .Replace("ś", "s")
            .Replace("ż", "z")
            .Replace("ź", "z");
    }

    private void GenerateProtocolPdf()
    {
        if (SelectedForm is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Settings.OutputDirectory);
            var prescriptionNumber = CreateSafeFileNamePart(SelectedForm.PrescriptionNumber);
            var filePath = Path.Combine(Settings.OutputDirectory, $"protokol_{prescriptionNumber}.pdf");
            _protocolPdfGenerator.GenerateProtocolWithA4Label(SelectedForm, Settings.Pharmacy, filePath);
            MessageBox.Show($"Wygenerowano PDF:\n{Path.GetFullPath(filePath)}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            ShowPdfError(exception);
        }
    }

    private void GenerateBatchPdf()
    {
        try
        {
            var forms = ImportedForms.ToList();
            if (forms.Count == 0)
            {
                MessageBox.Show("Brak formularzy do wygenerowania.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            foreach (var form in forms)
            {
                EnsureIngredientsLoaded(form);
            }

            Directory.CreateDirectory(Settings.OutputDirectory);
            var filePath = Path.Combine(Settings.OutputDirectory, $"protokoly_i_naklejki_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            _protocolPdfGenerator.GenerateProtocolsWithA4Labels(forms, Settings.Pharmacy, filePath);
            MessageBox.Show($"Wygenerowano PDF zbiorczy:\n{Path.GetFullPath(filePath)}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            ShowPdfError(exception);
        }
    }

    private void GenerateLabels(LabelType labelType)
    {
        try
        {
            Directory.CreateDirectory(Settings.OutputDirectory);
            var fileName = labelType == LabelType.Zewnetrznie ? "naklejki_zewnetrzne.pdf" : "naklejki_wewnetrzne.pdf";
            var filePath = Path.Combine(Settings.OutputDirectory, fileName);
            var forms = ImportedForms.Where(form => form.LabelType == labelType).ToList();

            if (forms.Count == 0)
            {
                MessageBox.Show("Brak formularzy dla wybranego typu etykiety.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _labelPdfGenerator.GenerateBrotherLabels(forms, Settings.Pharmacy, filePath);
            MessageBox.Show($"Wygenerowano PDF:\n{Path.GetFullPath(filePath)}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            ShowPdfError(exception);
        }
    }

    private static string CreateSafeFileNamePart(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var safeValue = new string(value.Select(character => invalidChars.Contains(character) ? '_' : character).ToArray()).Trim();

        return string.IsNullOrWhiteSpace(safeValue) ? "bez_numeru" : safeValue;
    }

    private static void ShowPdfError(Exception exception)
    {
        MessageBox.Show(
            $"Nie udalo sie wygenerowac PDF.\n\n{exception.Message}",
            "PharmaExt",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record BatchEditFieldOption(string DisplayName, BatchEditField Field);

public enum BatchEditField
{
    LabelType,
    LabelSize,
    LabelMedicineForm,
    Dosage,
    ManualCalculations,
    ManualPreparationDescription,
    ManualQualityControl,
    ManualFinalAssessment,
    ManualNotes,
    MixBeforeUse
}
