using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
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
    private readonly QualityDocumentPdfGenerator _qualityDocumentPdfGenerator;
    private readonly SettlementReportPdfGenerator _settlementReportPdfGenerator;
    private readonly SettlementInvoicePdfGenerator _settlementInvoicePdfGenerator;
    private readonly InventoryPdfGenerator _inventoryPdfGenerator;
    private bool _isLoadingCompositionGroups;
    private IReadOnlyDictionary<string, string> _backgroundCompositionKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    private ImportedForm? _selectedForm;
    private QualityDocumentTreeItem? _selectedQualityTreeItem;
    private QualityDocument? _selectedQualityDocument;
    private QualityDocument? _selectedRecipeInstruction;
    private InventoryItemRow? _selectedInventoryItem;
    private SettlementReportRow? _selectedSettlementRow;
    private AnalysisPrescriptionRow? _selectedAnalysisRow;
    private string _selectedRecipeInstructionText = "";
    private BatchEditFieldOption? _selectedBatchEditField;
    private string _batchEditValue = "";
    private string _qualityDocumentSearchText = "";
    private string _recipeInstructionSearchText = "";
    private string _maxUsedQuantityDeviationPercentText = "";
    private string _formsSummary = "";
    private bool _showOnlyEditedForms;
    private bool _showOnlyFormsWithoutInstruction;
    private bool _showOnlyFormsWithoutPdf;
    private bool _showOnlyCompositionGroups;
    private bool _isSearching;

    public MainViewModel()
    {
        _localDatabase = new LocalDatabaseService();
        _localDatabase.Initialize();

        Settings = _localDatabase.LoadSettings();
        _maxUsedQuantityDeviationPercentText = FormatDecimal(Settings.MaxUsedQuantityDeviationPercent);
        _prescriptionReader = new MockFirebirdPrescriptionReader(Settings);
        _protocolPdfGenerator = new ProtocolPdfGenerator();
        _labelPdfGenerator = new BrotherLabelPdfGenerator();
        _qualityDocumentPdfGenerator = new QualityDocumentPdfGenerator();
        _settlementReportPdfGenerator = new SettlementReportPdfGenerator();
        _settlementInvoicePdfGenerator = new SettlementInvoicePdfGenerator();
        _inventoryPdfGenerator = new InventoryPdfGenerator();

        SearchDateFrom = DateTime.Today.AddDays(-7);
        SearchDateTo = DateTime.Today;
        SettlementDateFrom = SearchDateFrom;
        SettlementDateTo = SearchDateTo;
        AnalysisDateFrom = SearchDateFrom;
        AnalysisDateTo = SearchDateTo;
        ResetLocalDateFrom = SearchDateFrom;
        ResetLocalDateTo = SearchDateTo;

        SearchCommand = new RelayCommand(Search, () => !IsSearching);
        RefreshFirebirdRangeCommand = new RelayCommand(RefreshFirebirdRange, () => !IsSearching);
        GenerateSettlementReportCommand = new RelayCommand(GenerateSettlementReport, () => !IsSearching);
        GenerateSettlementReportPdfCommand = new RelayCommand(GenerateSettlementReportPdf, () => SettlementRows.Count > 0);
        GenerateSelectedSettlementInvoiceCommand = new RelayCommand(GenerateSelectedSettlementInvoice, () => SettlementRows.Any(row => !row.MissingInvoiceNumber));
        GenerateAllSettlementInvoicesCommand = new RelayCommand(GenerateAllSettlementInvoices, () => SettlementRows.Any(row => !row.MissingInvoiceNumber));
        GenerateAnalysisCommand = new RelayCommand(GenerateAnalysis, () => !IsSearching);
        LoadInventoryCommand = new RelayCommand(LoadInventory, () => !IsSearching);
        GenerateInventoryPdfCommand = new RelayCommand(GenerateInventoryPdf, () => InventoryItems.Count > 0 && !IsSearching);
        SaveCommand = new RelayCommand(Save);
        TestFirebirdConnectionCommand = new RelayCommand(TestFirebirdConnection);
        GenerateProtocolPdfCommand = new RelayCommand(GenerateProtocolPdf, () => SelectedForm is not null);
        GenerateBatchPdfCommand = new RelayCommand(GenerateBatchPdf, () => ImportedForms.Count > 0);
        GenerateExternalLabelsCommand = new RelayCommand(() => GenerateLabels(LabelType.Zewnetrznie));
        GenerateInternalLabelsCommand = new RelayCommand(() => GenerateLabels(LabelType.Wewnetrznie));
        ResetLocalDataCommand = new RelayCommand(ResetLocalData);
        ResetLocalDataRangeCommand = new RelayCommand(ResetLocalDataRange);
        ApplyBatchEditCommand = new RelayCommand(ApplyBatchEdit, CanApplyBatchEdit);
        CopyCheckedFieldsCommand = new RelayCommand(CopyCheckedFields, CanCopyCheckedFields);
        AcceptRecipeInstructionCommand = new RelayCommand(AcceptRecipeInstruction, CanAcceptRecipeInstruction);
        AutoAcceptRecipeInstructionCommand = new RelayCommand(AutoAcceptRecipeInstruction, CanAutoAcceptRecipeInstruction);
        ImportQualityDocumentsCommand = new RelayCommand(ImportQualityDocuments);
        OpenQualityDocumentCommand = new RelayCommand(OpenQualityDocument, () => SelectedQualityDocument is not null);
        UpdateQualityDocumentCommand = new RelayCommand(UpdateQualityDocument, () => SelectedQualityDocument is not null);
        ExpireQualityDocumentCommand = new RelayCommand(ExpireQualityDocument, () => SelectedQualityDocument is not null && SelectedQualityDocument.Status != "Wygaszony");
        GenerateQualityDocumentPdfCommand = new RelayCommand(GenerateQualityDocumentPdf, () => SelectedQualityDocument is not null);
        SaveQualityDocumentMetadataCommand = new RelayCommand(SaveQualityDocumentMetadata, () => SelectedQualityDocument is not null);
        CreateProgramBackupCommand = new RelayCommand(CreateProgramBackup);
        ExportProgramDataCommand = new RelayCommand(ExportProgramData);
        ImportProgramDataCommand = new RelayCommand(ImportProgramData);
        SelectedBatchEditField = BatchEditFields.FirstOrDefault();
        CollectionViewSource.GetDefaultView(ImportedForms).Filter = FilterImportedForm;
        UpdateFormsSummary();
        LoadQualityDocuments();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string AppVersion { get; } = "PharmaExt 0.9.2026-07-02";
    public AppSettings Settings { get; }
    public DateTime? SearchDateFrom { get; set; }
    public DateTime? SearchDateTo { get; set; }
    public DateTime? SettlementDateFrom { get; set; }
    public DateTime? SettlementDateTo { get; set; }
    public string SettlementAddress { get; set; } = "";
    public DateTime? AnalysisDateFrom { get; set; }
    public DateTime? AnalysisDateTo { get; set; }
    public string AnalysisAddress { get; set; } = "";
    public string InventorySearchText { get; set; } = "";
    public bool InventoryOnlyNonZero { get; set; }
    public DateTime? ResetLocalDateFrom { get; set; }
    public DateTime? ResetLocalDateTo { get; set; }
    public string SearchAddress { get; set; } = "";
    public string FormsSummary
    {
        get => _formsSummary;
        private set
        {
            if (_formsSummary == value)
            {
                return;
            }

            _formsSummary = value;
            OnPropertyChanged();
        }
    }

    public bool ShowOnlyEditedForms
    {
        get => _showOnlyEditedForms;
        set
        {
            if (_showOnlyEditedForms == value)
            {
                return;
            }

            _showOnlyEditedForms = value;
            OnPropertyChanged();
            RefreshFormsView();
        }
    }

    public bool ShowOnlyFormsWithoutInstruction
    {
        get => _showOnlyFormsWithoutInstruction;
        set
        {
            if (_showOnlyFormsWithoutInstruction == value)
            {
                return;
            }

            _showOnlyFormsWithoutInstruction = value;
            OnPropertyChanged();
            RefreshFormsView();
        }
    }

    public bool ShowOnlyFormsWithoutPdf
    {
        get => _showOnlyFormsWithoutPdf;
        set
        {
            if (_showOnlyFormsWithoutPdf == value)
            {
                return;
            }

            _showOnlyFormsWithoutPdf = value;
            OnPropertyChanged();
            RefreshFormsView();
        }
    }

    public bool ShowOnlyCompositionGroups
    {
        get => _showOnlyCompositionGroups;
        set
        {
            if (_showOnlyCompositionGroups == value)
            {
                return;
            }

            _showOnlyCompositionGroups = value;
            OnPropertyChanged();
            RefreshFormsView();
        }
    }

    public string MaxUsedQuantityDeviationPercentText
    {
        get => _maxUsedQuantityDeviationPercentText;
        set
        {
            _maxUsedQuantityDeviationPercentText = value;
            if (TryParseDecimal(value, out var parsedValue))
            {
                Settings.MaxUsedQuantityDeviationPercent = parsedValue;
            }

            OnPropertyChanged();
        }
    }

    public ObservableCollection<ImportedForm> FoundPrescriptions { get; } = new();
    public ObservableCollection<ImportedForm> ImportedForms { get; } = new();
    public ObservableCollection<ImportedForm> SelectedImportedForms { get; } = new();
    public ObservableCollection<SettlementReportRow> SettlementRows { get; } = new();
    public ObservableCollection<AnalysisPrescriptionRow> AnalysisRows { get; } = new();
    public ObservableCollection<InventoryItemRow> InventoryItems { get; } = new();
    public ObservableCollection<InventoryDeliveryRow> InventoryDeliveries { get; } = new();
    public ObservableCollection<QualityDocumentTreeItem> QualityDocumentTree { get; } = new();
    public ObservableCollection<QualityDocument> QualityDocuments { get; } = new();
    public ObservableCollection<QualityDocument> RecipeInstructions { get; } = new();
    public ObservableCollection<QualityContentLine> QualityContentLines { get; } = new();
    public BatchEditFieldOption[] BatchEditFields { get; } =
    [
        new("Typ etykiety", BatchEditField.LabelType),
        new("Rozmiar etykiety", BatchEditField.LabelSize),
        new("M.f. / Postac leku", BatchEditField.LabelMedicineForm),
        new("Data waznosci", BatchEditField.MedicineExpiryDate),
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
    public bool CopyMedicineExpiryDate { get; set; }
    public bool CopyMixBeforeUse { get; set; }
    public bool CopyManualCalculations { get; set; }
    public bool CopyManualPreparationDescription { get; set; }
    public bool CopyManualQualityControl { get; set; }
    public bool CopyDosage { get; set; }
    public bool CopyManualNotes { get; set; }
    public decimal SettlementLimitTotal => SettlementRows.Sum(row => row.LimitAmount);
    public decimal SettlementOverLimitTotal => SettlementRows.Sum(row => row.OverLimitAmount);
    public decimal SettlementPaymentTotal => SettlementRows.Sum(row => row.PaymentAmount);
    public decimal AnalysisTotalNet => AnalysisRows.Sum(row => row.TotalNet);
    public decimal AnalysisTotalGross => AnalysisRows.Sum(row => row.TotalGross);
    public decimal AnalysisPatientPaymentTotal => AnalysisRows.Sum(row => row.PatientPayment);
    public decimal AnalysisIngredientsNetTotal => AnalysisRows.Sum(row => row.IngredientsNet);
    public decimal AnalysisIngredientsGrossTotal => AnalysisRows.Sum(row => row.IngredientsGross);
    public decimal AnalysisTaxAndMarginNetTotal => AnalysisRows.Sum(row => row.TaxAndMarginNet);
    public decimal AnalysisTaxAndMarginGrossTotal => AnalysisRows.Sum(row => row.TaxAndMarginGross);
    public decimal AnalysisPurchaseVatTotal => AnalysisRows.Sum(row => row.PurchaseVat);
    public decimal AnalysisSalesVatTotal => AnalysisRows.Sum(row => row.SalesVat);
    public decimal AnalysisVatDifferenceTotal => AnalysisPurchaseVatTotal - AnalysisSalesVatTotal;
    public decimal AnalysisNetProfitTotal => AnalysisRows.Sum(row => row.NetProfit);
    public decimal AnalysisProfitMinusVatTotal => AnalysisRows.Sum(row => row.ProfitMinusVat);
    public Brush AnalysisVatDifferenceBrush => AnalysisPrescriptionRow.GetDifferenceBrush(AnalysisVatDifferenceTotal);
    public Brush AnalysisProfitMinusVatBrush => AnalysisPrescriptionRow.GetProfitBrush(AnalysisProfitMinusVatTotal);

    public string QualityDocumentSearchText
    {
        get => _qualityDocumentSearchText;
        set
        {
            if (_qualityDocumentSearchText == value)
            {
                return;
            }

            _qualityDocumentSearchText = value;
            OnPropertyChanged();
            BuildQualityDocumentTree();
        }
    }

    public string RecipeInstructionSearchText
    {
        get => _recipeInstructionSearchText;
        set
        {
            if (_recipeInstructionSearchText == value)
            {
                return;
            }

            _recipeInstructionSearchText = value;
            OnPropertyChanged();
            BuildRecipeInstructions();
        }
    }

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
            (RefreshFirebirdRangeCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateSettlementReportCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateAnalysisCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (LoadInventoryCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateInventoryPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ImportedForm? SelectedForm
    {
        get => _selectedForm;
        set
        {
            if (!ReferenceEquals(_selectedForm, value))
            {
                SaveEditedFormSilently(_selectedForm);
            }

            _selectedForm = value;
            LoadSelectedFormIngredients();
            OnPropertyChanged();
            (GenerateProtocolPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (CopyCheckedFieldsCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AcceptRecipeInstructionCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (AutoAcceptRecipeInstructionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public QualityDocument? SelectedRecipeInstruction
    {
        get => _selectedRecipeInstruction;
        set
        {
            _selectedRecipeInstruction = value;
            SelectedRecipeInstructionText = value is null ? "" : ExtractRecipeInstructionText(value.ContentText);
            OnPropertyChanged();
            (AcceptRecipeInstructionCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string SelectedRecipeInstructionText
    {
        get => _selectedRecipeInstructionText;
        private set
        {
            _selectedRecipeInstructionText = value;
            OnPropertyChanged();
        }
    }

    public InventoryItemRow? SelectedInventoryItem
    {
        get => _selectedInventoryItem;
        set
        {
            _selectedInventoryItem = value;
            OnPropertyChanged();
            LoadSelectedInventoryDeliveries();
        }
    }

    public SettlementReportRow? SelectedSettlementRow
    {
        get => _selectedSettlementRow;
        set
        {
            _selectedSettlementRow = value;
            OnPropertyChanged();
            (GenerateSelectedSettlementInvoiceCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public AnalysisPrescriptionRow? SelectedAnalysisRow
    {
        get => _selectedAnalysisRow;
        set
        {
            _selectedAnalysisRow = value;
            OnPropertyChanged();
        }
    }

    public QualityDocumentTreeItem? SelectedQualityTreeItem
    {
        get => _selectedQualityTreeItem;
        set
        {
            _selectedQualityTreeItem = value;
            SelectedQualityDocument = value?.Document;
            OnPropertyChanged();
        }
    }

    public QualityDocument? SelectedQualityDocument
    {
        get => _selectedQualityDocument;
        private set
        {
            _selectedQualityDocument = value;
            BuildQualityContentLines();
            OnPropertyChanged();
            (OpenQualityDocumentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (UpdateQualityDocumentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ExpireQualityDocumentCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateQualityDocumentPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (SaveQualityDocumentMetadataCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ICommand SearchCommand { get; }
    public ICommand RefreshFirebirdRangeCommand { get; }
    public ICommand GenerateSettlementReportCommand { get; }
    public ICommand GenerateSettlementReportPdfCommand { get; }
    public ICommand GenerateSelectedSettlementInvoiceCommand { get; }
    public ICommand GenerateAllSettlementInvoicesCommand { get; }
    public ICommand GenerateAnalysisCommand { get; }
    public ICommand LoadInventoryCommand { get; }
    public ICommand GenerateInventoryPdfCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand TestFirebirdConnectionCommand { get; }
    public ICommand GenerateProtocolPdfCommand { get; }
    public ICommand GenerateBatchPdfCommand { get; }
    public ICommand GenerateExternalLabelsCommand { get; }
    public ICommand GenerateInternalLabelsCommand { get; }
    public ICommand ResetLocalDataCommand { get; }
    public ICommand ResetLocalDataRangeCommand { get; }
    public ICommand ApplyBatchEditCommand { get; }
    public ICommand CopyCheckedFieldsCommand { get; }
    public ICommand AcceptRecipeInstructionCommand { get; }
    public ICommand AutoAcceptRecipeInstructionCommand { get; }
    public ICommand ImportQualityDocumentsCommand { get; }
    public ICommand OpenQualityDocumentCommand { get; }
    public ICommand UpdateQualityDocumentCommand { get; }
    public ICommand ExpireQualityDocumentCommand { get; }
    public ICommand GenerateQualityDocumentPdfCommand { get; }
    public ICommand SaveQualityDocumentMetadataCommand { get; }
    public ICommand CreateProgramBackupCommand { get; }
    public ICommand ExportProgramDataCommand { get; }
    public ICommand ImportProgramDataCommand { get; }

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

    private bool FilterImportedForm(object item)
    {
        if (item is not ImportedForm form)
        {
            return false;
        }

        if (ShowOnlyEditedForms && !form.IsEditedThisSession)
        {
            return false;
        }

        if (ShowOnlyFormsWithoutInstruction && form.HasInstruction)
        {
            return false;
        }

        if (ShowOnlyFormsWithoutPdf && form.HasGeneratedPdf)
        {
            return false;
        }

        if (ShowOnlyCompositionGroups && string.IsNullOrWhiteSpace(form.CompositionGroupLabel))
        {
            return false;
        }

        return true;
    }

    private void RefreshFormsView()
    {
        CollectionViewSource.GetDefaultView(ImportedForms).Refresh();
        UpdateFormsSummary();
    }

    private void UpdateFormsSummary()
    {
        var total = ImportedForms.Count;
        var edited = ImportedForms.Count(form => form.IsEditedThisSession);
        var withoutInstruction = ImportedForms.Count(form => !form.HasInstruction);
        var withoutPdf = ImportedForms.Count(form => !form.HasGeneratedPdf);
        var grouped = ImportedForms.Count(form => !string.IsNullOrWhiteSpace(form.CompositionGroupLabel));
        FormsSummary = $"Recepty: {total} | edytowane: {edited} | bez IR: {withoutInstruction} | bez PDF: {withoutPdf} | podobny skład: {grouped}";
    }

    private void RegisterFormForUiUpdates(ImportedForm form)
    {
        form.PropertyChanged -= ImportedForm_PropertyChanged;
        form.PropertyChanged += ImportedForm_PropertyChanged;
    }

    private void ImportedForm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ImportedForm.IsEditedThisSession)
            or nameof(ImportedForm.HasInstruction)
            or nameof(ImportedForm.HasGeneratedPdf)
            or nameof(ImportedForm.CompositionGroupLabel))
        {
            RefreshFormsView();
        }
    }

    private void SaveEditedFormSilently(ImportedForm? form)
    {
        if (form is null || !form.IsEditedThisSession)
        {
            return;
        }

        try
        {
            _localDatabase.UpsertImportedForm(form);
            form.EnableEditTracking();
            RefreshFormsView();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udalo sie automatycznie zapisac recepty {form.PrescriptionNumber}.\n\n{exception.Message}",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    public string EnsureSelectedQualityPreviewPath()
    {
        if (SelectedQualityDocument is null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(SelectedQualityDocument.PreviewHtmlPath) && File.Exists(SelectedQualityDocument.PreviewHtmlPath))
        {
            return SelectedQualityDocument.PreviewHtmlPath;
        }

        if (RelinkQualityDocumentFiles(SelectedQualityDocument, rebuildPreview: true))
        {
            OnPropertyChanged(nameof(SelectedQualityDocument));
        }
        if (!string.IsNullOrWhiteSpace(SelectedQualityDocument.PreviewHtmlPath) && File.Exists(SelectedQualityDocument.PreviewHtmlPath))
        {
            return SelectedQualityDocument.PreviewHtmlPath;
        }

        if (!File.Exists(SelectedQualityDocument.FilePath))
        {
            return "";
        }

        SelectedQualityDocument.PreviewHtmlPath = CreateQualityDocumentPreview(
            SelectedQualityDocument.FilePath,
            SelectedQualityDocument.Code,
            SelectedQualityDocument.Version);

        return SelectedQualityDocument.PreviewHtmlPath;
    }

    private void LoadQualityDocuments()
    {
        QualityDocuments.Clear();
        foreach (var document in _localDatabase.LoadQualityDocuments())
        {
            QualityDocuments.Add(document);
        }

        BuildRecipeInstructions();
        BuildQualityDocumentTree();
    }

    private void BuildRecipeInstructions()
    {
        RecipeInstructions.Clear();
        foreach (var document in QualityDocuments
            .Where(document => string.Equals(document.CategoryCode, "IR", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(document.Status, "Wygaszony", StringComparison.OrdinalIgnoreCase)
                && MatchesRecipeInstructionSearch(document))
            .OrderBy(document => document.Code))
        {
            RecipeInstructions.Add(document);
        }

        if (SelectedRecipeInstruction is not null && !RecipeInstructions.Any(document => document.Id == SelectedRecipeInstruction.Id))
        {
            SelectedRecipeInstruction = null;
        }
    }

    private bool MatchesRecipeInstructionSearch(QualityDocument document)
    {
        return RecipeInstructionSearchService.Matches(document, RecipeInstructionSearchText);
    }

    private void BuildQualityDocumentTree()
    {
        QualityDocumentTree.Clear();
        QualityDocumentTree.Add(new QualityDocumentTreeItem("Ksi\u0119ga Jako\u015bci"));

        var filteredDocuments = QualityDocuments
            .Where(MatchesQualityDocumentSearch)
            .ToList();

        foreach (var category in filteredDocuments
            .OrderBy(document => GetQualityCategorySort(document.CategoryCode))
            .ThenBy(document => document.CategoryCode)
            .GroupBy(document => new { document.CategoryCode, document.CategoryName }))
        {
            var categoryNode = new QualityDocumentTreeItem($"{category.Key.CategoryCode} - {category.Key.CategoryName}");
            foreach (var document in category.OrderBy(document => document.Code))
            {
                categoryNode.Children.Add(new QualityDocumentTreeItem($"{document.Code} {document.Title}", document));
            }

            QualityDocumentTree.Add(categoryNode);
        }

        if (string.IsNullOrWhiteSpace(QualityDocumentSearchText)
            && QualityDocuments.All(document => !string.Equals(document.CategoryCode, "IR", StringComparison.OrdinalIgnoreCase)))
        {
            QualityDocumentTree.Add(new QualityDocumentTreeItem("IR - Instrukcje wykonania leku"));
        }
    }

    private bool MatchesQualityDocumentSearch(QualityDocument document)
    {
        var terms = QualityDocumentSearchText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (terms.Length == 0)
        {
            return true;
        }

        var haystack = string.Join(" ", document.Code, document.Title, document.CategoryName, document.ContentText);
        return terms.All(term => haystack.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static int GetQualityCategorySort(string categoryCode)
    {
        return categoryCode.ToUpperInvariant() switch
        {
            "SOP" => 10,
            "IO" => 20,
            "IT" => 30,
            "FRM" => 40,
            "REJ" => 50,
            "ZAL" => 60,
            "IR" => 100,
            _ => 90
        };
    }

    private void BuildQualityContentLines()
    {
        QualityContentLines.Clear();
        if (SelectedQualityDocument is null)
        {
            return;
        }

        foreach (var line in ParseQualityContentLines(SelectedQualityDocument.ContentText))
        {
            QualityContentLines.Add(line);
        }
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

                return reader.Search(searchDateFrom, searchDateTo, searchAddress);
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

    private async void RefreshFirebirdRange()
    {
        if (string.IsNullOrWhiteSpace(Settings.Firebird.DatabasePath))
        {
            MessageBox.Show("Ustaw ścieżkę bazy Firebird przed odświeżeniem zakresu.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsSearching = true;
            var searchDateFrom = SearchDateFrom;
            var searchDateTo = SearchDateTo;
            var searchAddress = SearchAddress;

            var addedCount = await Task.Run(() =>
            {
                var existingIds = _localDatabase.GetImportedSourcePrescriptionIds();
                var reader = new FirebirdPrescriptionReader(Settings);
                var firebirdResults = reader.Search(searchDateFrom, searchDateTo, searchAddress);
                var newForms = firebirdResults
                    .Where(form => !existingIds.Contains(form.SourcePrescriptionId))
                    .ToList();

                if (newForms.Count == 0)
                {
                    return 0;
                }

                reader.LoadIngredients(newForms);
                foreach (var form in newForms)
                {
                    _localDatabase.UpsertImportedForm(form);
                }

                return newForms.Count;
            });

            var refreshedResults = _localDatabase.SearchImportedForms(searchDateFrom, searchDateTo, searchAddress);
            FoundPrescriptions.Clear();
            var displayNumber = 1;
            foreach (var form in refreshedResults)
            {
                form.DisplayNumber = displayNumber++;
                FoundPrescriptions.Add(form);
            }

            ImportFoundPrescriptions();

            MessageBox.Show(
                addedCount == 0
                    ? "Sprawdzono Firebird. Nie znaleziono nowych recept w tym zakresie."
                    : $"Sprawdzono Firebird. Dodano nowe recepty: {addedCount}.",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udało się odświeżyć zakresu z Firebirda.\n\n{exception.Message}",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async void GenerateSettlementReport()
    {
        if (string.IsNullOrWhiteSpace(Settings.Firebird.DatabasePath))
        {
            MessageBox.Show("Ustaw sciezke bazy Firebird przed wygenerowaniem rozliczen.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsSearching = true;
            var dateFrom = SettlementDateFrom;
            var dateTo = SettlementDateTo;
            var address = SettlementAddress;
            var rows = await Task.Run(() => new FirebirdPrescriptionReader(Settings).LoadSettlementReport(dateFrom, dateTo, address));

            SettlementRows.Clear();
            var displayNumber = 1;
            foreach (var row in rows)
            {
                row.DisplayNumber = displayNumber++;
                SettlementRows.Add(row);
            }

            OnPropertyChanged(nameof(SettlementLimitTotal));
            OnPropertyChanged(nameof(SettlementOverLimitTotal));
            OnPropertyChanged(nameof(SettlementPaymentTotal));
            (GenerateSettlementReportPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (GenerateAllSettlementInvoicesCommand as RelayCommand)?.RaiseCanExecuteChanged();
            SelectedSettlementRow = SettlementRows.FirstOrDefault(row => !row.MissingInvoiceNumber);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udalo sie wygenerowac raportu rozliczen.\n\n{exception.Message}",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsSearching = false;
        }
    }

    private void GenerateSelectedSettlementInvoice()
    {
        var selectedInvoiceGroups = GetCheckedInvoiceGroups().ToList();
        if (selectedInvoiceGroups.Count > 1)
        {
            MessageBox.Show("Zaznaczono kilka faktur. Użyj przycisku Faktury PDF.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var invoiceRows = selectedInvoiceGroups.Count == 1
            ? selectedInvoiceGroups[0]
            : SelectedSettlementRow is null || SelectedSettlementRow.MissingInvoiceNumber
                ? new List<SettlementReportRow>()
                : GetInvoiceRows(SelectedSettlementRow).ToList();

        if (invoiceRows.Count == 0)
        {
            MessageBox.Show("Zaznacz fakturę checkboxem albo kliknij wiersz z numerem faktury.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            invoiceRows = ReloadSettlementInvoiceRows(invoiceRows[0]);
            if (invoiceRows.Count == 0)
            {
                MessageBox.Show("Nie udało się odświeżyć danych faktury z Firebird.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Directory.CreateDirectory(Settings.OutputDirectory);
            var filePath = CreateSettlementInvoicePath(invoiceRows[0]);
            _settlementInvoicePdfGenerator.Generate(invoiceRows, Settings.Pharmacy, filePath);
            MessageBox.Show($"Wygenerowano fakturę PDF:\n{Path.GetFullPath(filePath)}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            ShowPdfError(exception);
        }
    }

    private void GenerateAllSettlementInvoices()
    {
        try
        {
            var groups = GetCheckedInvoiceGroups().ToList();
            if (groups.Count == 0)
            {
                groups = SettlementRows
                    .Where(row => !row.MissingInvoiceNumber)
                    .GroupBy(GetInvoiceGroupKey)
                    .Select(group => group.ToList())
                    .ToList();
            }

            if (groups.Count == 0)
            {
                MessageBox.Show("Brak pozycji z numerem faktury.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Directory.CreateDirectory(Settings.OutputDirectory);
            var invoiceGroups = new List<IReadOnlyList<SettlementReportRow>>();
            foreach (var invoiceRows in groups)
            {
                var freshInvoiceRows = ReloadSettlementInvoiceRows(invoiceRows[0]);
                if (freshInvoiceRows.Count == 0)
                {
                    freshInvoiceRows = invoiceRows;
                }

                invoiceGroups.Add(freshInvoiceRows);
            }

            var filePath = CreateSettlementInvoicesPath();
            _settlementInvoicePdfGenerator.GenerateMany(invoiceGroups, Settings.Pharmacy, filePath);

            MessageBox.Show($"Wygenerowano jeden plik PDF z fakturami: {invoiceGroups.Count}\n{Path.GetFullPath(filePath)}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            ShowPdfError(exception);
        }
    }

    private void GenerateSettlementReportPdf()
    {
        try
        {
            var rows = SettlementRows.ToList();
            if (rows.Count == 0)
            {
                MessageBox.Show("Brak danych rozliczeniowych do wygenerowania PDF.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            Directory.CreateDirectory(Settings.OutputDirectory);
            var filePath = Path.Combine(Settings.OutputDirectory, $"rozliczenia_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            _settlementReportPdfGenerator.Generate(rows, Settings.Pharmacy, SettlementDateFrom, SettlementDateTo, filePath);
            MessageBox.Show($"Wygenerowano PDF rozliczeń:\n{Path.GetFullPath(filePath)}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            ShowPdfError(exception);
        }
    }

    private IEnumerable<SettlementReportRow> GetInvoiceRows(SettlementReportRow selectedRow)
    {
        var key = GetInvoiceGroupKey(selectedRow);
        return SettlementRows.Where(row => !row.MissingInvoiceNumber && GetInvoiceGroupKey(row) == key);
    }

    private List<SettlementReportRow> ReloadSettlementInvoiceRows(SettlementReportRow invoiceRow)
    {
        var dateFrom = SettlementDateFrom ?? invoiceRow.SaleDate;
        var dateTo = SettlementDateTo ?? invoiceRow.SaleDate;
        var freshRows = new FirebirdPrescriptionReader(Settings).LoadSettlementReport(dateFrom, dateTo, "");
        var key = GetInvoiceGroupKey(invoiceRow);
        var rows = freshRows
            .Where(row => !row.MissingInvoiceNumber && GetInvoiceGroupKey(row) == key)
            .ToList();

        if (rows.Count == 0 && !string.IsNullOrWhiteSpace(invoiceRow.InvoiceNumber))
        {
            rows = freshRows
                .Where(row => string.Equals(row.InvoiceNumber, invoiceRow.InvoiceNumber, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return rows;
    }

    private IEnumerable<List<SettlementReportRow>> GetCheckedInvoiceGroups()
    {
        var checkedKeys = SettlementRows
            .Where(row => row.IsInvoiceSelected && !row.MissingInvoiceNumber)
            .Select(GetInvoiceGroupKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return SettlementRows
            .Where(row => !row.MissingInvoiceNumber && checkedKeys.Contains(GetInvoiceGroupKey(row)))
            .GroupBy(GetInvoiceGroupKey)
            .Select(group => group.ToList());
    }

    private static string GetInvoiceGroupKey(SettlementReportRow row)
    {
        return string.IsNullOrWhiteSpace(row.InvoiceDocumentId)
            ? row.InvoiceNumber.Trim()
            : row.InvoiceDocumentId.Trim();
    }

    private string CreateSettlementInvoicePath(SettlementReportRow row)
    {
        var invoiceNumber = CreateSafeFileNamePart(row.InvoiceNumber);
        var date = row.InvoiceIssueDate == default ? DateTime.Today : row.InvoiceIssueDate;
        return Path.Combine(Settings.OutputDirectory, $"faktura_{invoiceNumber}_{date:yyyyMMdd}.pdf");
    }

    private string CreateSettlementInvoicesPath()
    {
        var dateFrom = SettlementDateFrom ?? SettlementRows.Where(row => !row.MissingInvoiceNumber).Select(row => row.SaleDate).DefaultIfEmpty(DateTime.Today).Min();
        var dateTo = SettlementDateTo ?? SettlementRows.Where(row => !row.MissingInvoiceNumber).Select(row => row.SaleDate).DefaultIfEmpty(DateTime.Today).Max();
        return Path.Combine(Settings.OutputDirectory, $"faktury_{dateFrom:yyyyMMdd}_{dateTo:yyyyMMdd}_{DateTime.Now:HHmmss}.pdf");
    }

    private async void GenerateAnalysis()
    {
        if (string.IsNullOrWhiteSpace(Settings.Firebird.DatabasePath))
        {
            MessageBox.Show("Ustaw sciezke bazy Firebird przed pobraniem analiz.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsSearching = true;
            var dateFrom = AnalysisDateFrom;
            var dateTo = AnalysisDateTo;
            var address = AnalysisAddress;
            var rows = await Task.Run(() => new FirebirdPrescriptionReader(Settings).LoadAnalysisReport(dateFrom, dateTo, address));

            AnalysisRows.Clear();
            foreach (var row in rows)
            {
                AnalysisRows.Add(row);
            }

            SelectedAnalysisRow = AnalysisRows.FirstOrDefault();
            NotifyAnalysisTotalsChanged();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udalo sie pobrac analizy recept.\n\n{exception.Message}",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsSearching = false;
        }
    }

    private void NotifyAnalysisTotalsChanged()
    {
        OnPropertyChanged(nameof(AnalysisTotalNet));
        OnPropertyChanged(nameof(AnalysisTotalGross));
        OnPropertyChanged(nameof(AnalysisPatientPaymentTotal));
        OnPropertyChanged(nameof(AnalysisIngredientsNetTotal));
        OnPropertyChanged(nameof(AnalysisIngredientsGrossTotal));
        OnPropertyChanged(nameof(AnalysisTaxAndMarginNetTotal));
        OnPropertyChanged(nameof(AnalysisTaxAndMarginGrossTotal));
        OnPropertyChanged(nameof(AnalysisPurchaseVatTotal));
        OnPropertyChanged(nameof(AnalysisSalesVatTotal));
        OnPropertyChanged(nameof(AnalysisVatDifferenceTotal));
        OnPropertyChanged(nameof(AnalysisNetProfitTotal));
        OnPropertyChanged(nameof(AnalysisProfitMinusVatTotal));
        OnPropertyChanged(nameof(AnalysisVatDifferenceBrush));
        OnPropertyChanged(nameof(AnalysisProfitMinusVatBrush));
    }

    private async void LoadInventory()
    {
        if (string.IsNullOrWhiteSpace(Settings.Firebird.DatabasePath))
        {
            MessageBox.Show("Ustaw sciezke bazy Firebird przed pobraniem magazynu.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsSearching = true;
            var searchText = InventorySearchText;
            var onlyNonZero = InventoryOnlyNonZero;
            var rows = await Task.Run(() => new FirebirdPrescriptionReader(Settings).LoadInventoryItems(searchText));

            InventoryItems.Clear();
            InventoryDeliveries.Clear();
            foreach (var row in rows.Where(row => !onlyNonZero || row.TotalQuantity != 0))
            {
                InventoryItems.Add(row);
            }

            SelectedInventoryItem = InventoryItems.FirstOrDefault();
            (GenerateInventoryPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udalo sie pobrac danych magazynu z Firebird.\n\n{exception.Message}",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async void GenerateInventoryPdf()
    {
        var items = InventoryItems.ToList();
        if (items.Count == 0)
        {
            MessageBox.Show("Brak pozycji magazynu do wygenerowania PDF.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(Settings.Firebird.DatabasePath))
        {
            MessageBox.Show("Ustaw sciezke bazy Firebird przed wygenerowaniem PDF magazynu.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            IsSearching = true;
            var reportItems = await Task.Run(() =>
            {
                var reader = new FirebirdPrescriptionReader(Settings);
                return items
                    .Select(item => new InventoryReportItem
                    {
                        Item = item,
                        Deliveries = reader.LoadInventoryDeliveries(item.FirebirdId)
                    })
                    .ToList();
            });

            Directory.CreateDirectory(Settings.OutputDirectory);
            var filePath = Path.Combine(Settings.OutputDirectory, $"magazyn_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
            _inventoryPdfGenerator.Generate(reportItems, Settings.Pharmacy, filePath);
            MessageBox.Show($"Wygenerowano PDF magazynu:\n{Path.GetFullPath(filePath)}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            ShowPdfError(exception);
        }
        finally
        {
            IsSearching = false;
        }
    }

    private async void LoadSelectedInventoryDeliveries()
    {
        InventoryDeliveries.Clear();
        if (SelectedInventoryItem is null || string.IsNullOrWhiteSpace(Settings.Firebird.DatabasePath))
        {
            return;
        }

        try
        {
            var itemId = SelectedInventoryItem.FirebirdId;
            var rows = await Task.Run(() => new FirebirdPrescriptionReader(Settings).LoadInventoryDeliveries(itemId));
            InventoryDeliveries.Clear();
            foreach (var row in rows)
            {
                InventoryDeliveries.Add(row);
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udalo sie pobrac dostaw dla wybranej pozycji.\n\n{exception.Message}",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void ImportQualityDocuments()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Wybierz dokumenty Word do importu",
            Filter = "Dokumenty Word (*.docx)|*.docx",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var importedCount = 0;
        var skippedFiles = new List<string>();
        foreach (var fileName in dialog.FileNames)
        {
            if (!TryCreateQualityDocumentFromFile(fileName, out var document))
            {
                skippedFiles.Add(Path.GetFileName(fileName));
                continue;
            }

            document.FilePath = CopyQualityDocumentFile(fileName, document.Code, document.Version);
            document.ContentText = ExtractDocxText(fileName);
            document.Title = ExtractQualityDocumentTitle(document.ContentText, document.Code, document.Title);
            document.PreviewHtmlPath = CreateQualityDocumentPreview(fileName, document.Code, document.Version);
            _localDatabase.UpsertQualityDocument(document);
            importedCount++;
        }

        LoadQualityDocuments();

        var message = $"Zaimportowano dokumenty: {importedCount}.";
        if (skippedFiles.Count > 0)
        {
            message += $"\nPominieto nierozpoznane pliki: {skippedFiles.Count}.";
        }

        MessageBox.Show(message, "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void OpenQualityDocument()
    {
        if (SelectedQualityDocument is null || string.IsNullOrWhiteSpace(SelectedQualityDocument.FilePath))
        {
            return;
        }

        if (!File.Exists(SelectedQualityDocument.FilePath))
        {
            MessageBox.Show("Nie znaleziono pliku dokumentu.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = SelectedQualityDocument.FilePath,
            UseShellExecute = true
        });
    }

    private void UpdateQualityDocument()
    {
        if (SelectedQualityDocument is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = $"Wybierz nowa wersje dokumentu {SelectedQualityDocument.Code}",
            Filter = "Dokumenty Word (*.docx)|*.docx",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var nextVersion = IncrementVersion(SelectedQualityDocument.Version);
        SelectedQualityDocument.Version = nextVersion;
        SelectedQualityDocument.FilePath = CopyQualityDocumentFile(dialog.FileName, SelectedQualityDocument.Code, nextVersion);
        SelectedQualityDocument.ContentText = ExtractDocxText(dialog.FileName);
        SelectedQualityDocument.Title = ExtractQualityDocumentTitle(
            SelectedQualityDocument.ContentText,
            SelectedQualityDocument.Code,
            SelectedQualityDocument.Title);
        SelectedQualityDocument.PreviewHtmlPath = CreateQualityDocumentPreview(dialog.FileName, SelectedQualityDocument.Code, nextVersion);
        SelectedQualityDocument.Status = "Obowiazujacy";
        SelectedQualityDocument.UpdatedAt = DateTime.Now;
        SelectedQualityDocument.ExpiredAt = null;
        _localDatabase.UpdateQualityDocumentVersion(SelectedQualityDocument);
        LoadQualityDocuments();

        MessageBox.Show($"Dodano nowa wersje dokumentu: {SelectedQualityDocument.Code} v{nextVersion}.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExpireQualityDocument()
    {
        if (SelectedQualityDocument is null)
        {
            return;
        }

        var result = MessageBox.Show(
            $"Wygasic dokument {SelectedQualityDocument.Code}?",
            "PharmaExt",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        _localDatabase.ExpireQualityDocument(SelectedQualityDocument.Id);
        LoadQualityDocuments();
    }

    private void GenerateQualityDocumentPdf()
    {
        if (SelectedQualityDocument is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Settings.OutputDirectory);
            var safeCode = CreateSafeFileNamePart(SelectedQualityDocument.Code);
            var filePath = Path.Combine(Settings.OutputDirectory, $"{safeCode}_v{SelectedQualityDocument.Version}.pdf");
            _localDatabase.SaveQualityDocumentMetadata(SelectedQualityDocument);
            _qualityDocumentPdfGenerator.Generate(SelectedQualityDocument, filePath);
            MessageBox.Show($"Wygenerowano PDF dokumentu:\n{Path.GetFullPath(filePath)}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            ShowPdfError(exception);
        }
    }

    private void SaveQualityDocumentMetadata()
    {
        if (SelectedQualityDocument is null)
        {
            return;
        }

        try
        {
            _localDatabase.SaveQualityDocumentMetadata(SelectedQualityDocument);
            BuildRecipeInstructions();
            BuildQualityDocumentTree();
            MessageBox.Show("Zapisano metryke dokumentu.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Nie udalo sie zapisac metryki dokumentu.\n\n{exception.Message}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExportProgramData()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Eksport danych PharmaExt",
            Filter = "Archiwum ZIP (*.zip)|*.zip",
            FileName = $"PharmaExt_backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            WriteProgramDataArchive(dialog.FileName);
            MessageBox.Show($"Wyeksportowano dane programu:\n{dialog.FileName}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Nie udalo sie wyeksportowac danych programu.\n\n{exception.Message}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CreateProgramBackup()
    {
        try
        {
            var backupDirectory = Path.Combine(LocalDatabaseService.GetDefaultDataDirectory(), "Backups");
            Directory.CreateDirectory(backupDirectory);
            var backupPath = Path.Combine(backupDirectory, $"PharmaExt_backup_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
            WriteProgramDataArchive(backupPath);
            MessageBox.Show($"Utworzono kopie zapasowa:\n{backupPath}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Nie udalo sie utworzyc kopii zapasowej.\n\n{exception.Message}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void WriteProgramDataArchive(string archivePath)
    {
        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        var databasePath = LocalDatabaseService.GetDefaultDatabasePath();
        if (File.Exists(databasePath))
        {
            var exportDatabasePath = Path.Combine(Path.GetTempPath(), $"pharmaext_export_{Guid.NewGuid():N}.sqlite");
            try
            {
                _localDatabase.CreateDatabaseBackup(exportDatabasePath);
                LocalDatabaseService.ClearConnectionPools();
                AddFileToArchiveWithRetry(archive, exportDatabasePath, "pharmaext.sqlite");
            }
            finally
            {
                if (File.Exists(exportDatabasePath))
                {
                    DeleteFileWithRetry(exportDatabasePath);
                }
            }
        }

        AddDirectoryToArchive(archive, GetQualityDocumentsDirectory(), "QualityDocuments");
        AddDirectoryToArchive(archive, GetQualityDocumentPreviewsDirectory(), "QualityPreviews");

        if (File.Exists(DiagnosticLogService.FirebirdLogPath))
        {
            AddFileToArchiveWithRetry(archive, DiagnosticLogService.FirebirdLogPath, "Logs/firebird.log");
        }
    }

    private static void AddDirectoryToArchive(ZipArchive archive, string directory, string entryPrefix)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        foreach (var file in Directory.GetFiles(directory, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(directory, file).Replace('\\', '/');
            AddFileToArchiveWithRetry(archive, file, $"{entryPrefix}/{relativePath}");
        }
    }

    private void ImportProgramData()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Import danych PharmaExt",
            Filter = "Archiwum ZIP (*.zip)|*.zip",
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var result = MessageBox.Show(
            "Import danych zastapi lokalna baze i dokumenty jakosci. Kontynuowac?",
            "PharmaExt",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var databasePath = LocalDatabaseService.GetDefaultDatabasePath();
            var appDataDirectory = Path.GetDirectoryName(databasePath)!;
            Directory.CreateDirectory(appDataDirectory);

            using var archive = ZipFile.OpenRead(dialog.FileName);
            var databaseEntry = archive.GetEntry("pharmaext.sqlite");
            if (databaseEntry is not null)
            {
                var importDatabasePath = Path.Combine(appDataDirectory, "pharmaext.sqlite.import");
                var backupDatabasePath = Path.Combine(appDataDirectory, "pharmaext.sqlite.before_import");
                if (File.Exists(importDatabasePath))
                {
                    File.Delete(importDatabasePath);
                }

                databaseEntry.ExtractToFile(importDatabasePath, overwrite: true);
                LocalDatabaseService.ClearConnectionPools();

                if (File.Exists(databasePath))
                {
                    try
                    {
                        File.Copy(databasePath, backupDatabasePath, overwrite: true);
                    }
                    catch (IOException)
                    {
                        // Kopia bezpieczenstwa jest pomocnicza; import moze kontynuowac po zamknieciu poola SQLite.
                    }
                }

                ReplaceFileWithRetry(importDatabasePath, databasePath);
                File.Delete(importDatabasePath);
            }

            var documentsDirectory = GetQualityDocumentsDirectory();
            Directory.CreateDirectory(documentsDirectory);
            foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith("QualityDocuments/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(entry.Name)))
            {
                var relativePath = entry.FullName["QualityDocuments/".Length..].Replace('/', Path.DirectorySeparatorChar);
                var targetPath = Path.Combine(documentsDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                entry.ExtractToFile(targetPath, overwrite: true);
            }

            var previewsDirectory = GetQualityDocumentPreviewsDirectory();
            Directory.CreateDirectory(previewsDirectory);
            foreach (var entry in archive.Entries.Where(entry => entry.FullName.StartsWith("QualityPreviews/", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(entry.Name)))
            {
                var relativePath = entry.FullName["QualityPreviews/".Length..].Replace('/', Path.DirectorySeparatorChar);
                var targetPath = Path.Combine(previewsDirectory, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                entry.ExtractToFile(targetPath, overwrite: true);
            }

            _localDatabase.Initialize();
            FoundPrescriptions.Clear();
            ImportedForms.Clear();
            SelectedImportedForms.Clear();
            SelectedForm = null;
            RefreshFormsView();
            LoadQualityDocuments();
            RelinkImportedQualityDocuments(rebuildMissingPreviews: true);
            MessageBox.Show("Zaimportowano dane programu.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show($"Nie udalo sie zaimportowac danych programu.\n\n{exception.Message}", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RelinkImportedQualityDocuments(bool rebuildMissingPreviews)
    {
        foreach (var document in QualityDocuments)
        {
            RelinkQualityDocumentFiles(document, rebuildMissingPreviews);
        }

        BuildQualityDocumentTree();
    }

    private bool RelinkQualityDocumentFiles(QualityDocument document, bool rebuildPreview)
    {
        var changed = false;
        var localDocumentPath = FindLocalQualityDocumentFile(document);
        if (!string.IsNullOrWhiteSpace(localDocumentPath)
            && !string.Equals(document.FilePath, localDocumentPath, StringComparison.OrdinalIgnoreCase))
        {
            document.FilePath = localDocumentPath;
            changed = true;
        }

        var localPreviewPath = GetExpectedQualityPreviewPath(document.Code, document.Version);
        if (File.Exists(localPreviewPath)
            && !string.Equals(document.PreviewHtmlPath, localPreviewPath, StringComparison.OrdinalIgnoreCase))
        {
            document.PreviewHtmlPath = localPreviewPath;
            changed = true;
        }

        if (rebuildPreview
            && !string.IsNullOrWhiteSpace(document.FilePath)
            && File.Exists(document.FilePath)
            && !File.Exists(document.PreviewHtmlPath))
        {
            document.PreviewHtmlPath = CreateQualityDocumentPreview(document.FilePath, document.Code, document.Version);
            changed = true;
        }

        if (changed)
        {
            _localDatabase.SaveQualityDocumentPaths(document);
        }

        return changed;
    }

    private static void AddFileToArchiveWithRetry(ZipArchive archive, string filePath, string entryName)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                using var sourceStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
                using var entryStream = entry.Open();
                sourceStream.CopyTo(entryStream);
                return;
            }
            catch (IOException exception)
            {
                lastException = exception;
                LocalDatabaseService.ClearConnectionPools();
                Thread.Sleep(200 * attempt);
            }
        }

        throw new IOException($"Nie udalo sie dodac pliku do eksportu: {filePath}", lastException);
    }

    private static void DeleteFileWithRetry(string filePath)
    {
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                LocalDatabaseService.ClearConnectionPools();
                File.Delete(filePath);
                return;
            }
            catch (IOException) when (attempt < 5)
            {
                Thread.Sleep(200 * attempt);
            }
        }

        File.Delete(filePath);
    }

    private static void ReplaceFileWithRetry(string sourcePath, string targetPath)
    {
        Exception? lastException = null;
        for (var attempt = 1; attempt <= 5; attempt++)
        {
            try
            {
                LocalDatabaseService.ClearConnectionPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                File.Copy(sourcePath, targetPath, overwrite: true);
                return;
            }
            catch (IOException exception)
            {
                lastException = exception;
                Thread.Sleep(200 * attempt);
            }
        }

        throw new IOException("Nie udalo sie podmienic lokalnej bazy SQLite. Zamknij inne okna programu i sprobuj ponownie.", lastException);
    }

    private void ImportFoundPrescriptions()
    {
        _backgroundCompositionKeys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        ImportedForms.Clear();
        foreach (var form in FoundPrescriptions)
        {
            form.EnableEditTracking();
            RegisterFormForUiUpdates(form);
            ImportedForms.Add(form);
        }

        AssignCompositionGroupColors();
        RefreshFormsView();
        SelectedForm = null;
        (GenerateBatchPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
        _ = LoadCompositionGroupsInBackground();
    }

    private async Task LoadCompositionGroupsInBackground()
    {
        if (_isLoadingCompositionGroups || string.IsNullOrWhiteSpace(Settings.Firebird.DatabasePath))
        {
            return;
        }

        var formsToLoad = ImportedForms
            .Where(form => !form.IngredientsLoaded)
            .ToList();

        if (formsToLoad.Count == 0)
        {
            return;
        }

        try
        {
            _isLoadingCompositionGroups = true;
            var compositionKeys = await Task.Run(() => new FirebirdPrescriptionReader(Settings).LoadCompositionKeys(formsToLoad));
            _backgroundCompositionKeys = compositionKeys;
            AssignCompositionGroupColors();
        }
        catch
        {
            // Kolorowanie jest pomocnicze; ewentualny błąd pokaże się przy ręcznym wyborze recepty.
        }
        finally
        {
            _isLoadingCompositionGroups = false;
        }
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
            .Select(form => new { Form = form, Key = BuildCompositionKeyWithBackgroundData(form) })
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

    private string BuildCompositionKeyWithBackgroundData(ImportedForm form)
    {
        var key = BuildCompositionKey(form);
        if (!string.IsNullOrWhiteSpace(key))
        {
            return key;
        }

        return !string.IsNullOrWhiteSpace(form.SourcePrescriptionId)
            && _backgroundCompositionKeys.TryGetValue(form.SourcePrescriptionId, out var backgroundKey)
            ? backgroundKey
            : "";
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
        if (!TryParseDecimal(MaxUsedQuantityDeviationPercentText, out var maxDeviationPercent))
        {
            MessageBox.Show("Maks. odchył ilości użytej wpisz jako liczbę, np. 0,6.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Settings.MaxUsedQuantityDeviationPercent = maxDeviationPercent;
        MaxUsedQuantityDeviationPercentText = FormatDecimal(maxDeviationPercent);
        _localDatabase.SaveSettings(Settings);
        foreach (var form in ImportedForms)
        {
            EnsureIngredientsLoaded(form);
            _localDatabase.UpsertImportedForm(form);
            form.EnableEditTracking();
        }

        RefreshFormsView();
        MessageBox.Show("Zapisano dane lokalnie.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private static bool TryParseDecimal(string value, out decimal parsedValue)
    {
        var normalizedValue = value.Trim().Replace(',', '.');
        return decimal.TryParse(normalizedValue, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedValue);
    }

    private static string FormatDecimal(decimal value)
    {
        return value.ToString("0.###", CultureInfo.CurrentCulture);
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
            RefreshFormsView();
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

    private void ResetLocalDataRange()
    {
        if (ResetLocalDateFrom is null || ResetLocalDateTo is null)
        {
            MessageBox.Show("Wybierz date od i date do.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dateFrom = ResetLocalDateFrom.Value.Date;
        var dateTo = ResetLocalDateTo.Value.Date;
        if (dateFrom > dateTo)
        {
            MessageBox.Show("Data od nie moze byc pozniejsza niz data do.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Wyczyscic lokalnie zapisane formularze z zakresu {dateFrom:dd/MM/yyyy} - {dateTo:dd/MM/yyyy}?\n\nUstawienia programu zostana zachowane.",
            "PharmaExt",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var deletedCount = _localDatabase.ResetLocalData(dateFrom, dateTo);
            RemoveVisibleFormsInDateRange(dateFrom, dateTo);

            MessageBox.Show(
                $"Wyczyszczono lokalne formularze z zakresu dat: {deletedCount}.",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Nie udalo sie wyczyscic lokalnej bazy formularzy z zakresu dat.\n\n{exception.Message}",
                "PharmaExt",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RemoveVisibleFormsInDateRange(DateTime dateFrom, DateTime dateTo)
    {
        var importedToRemove = ImportedForms
            .Where(form => form.PreparationDate.Date >= dateFrom && form.PreparationDate.Date <= dateTo)
            .ToList();
        foreach (var form in importedToRemove)
        {
            ImportedForms.Remove(form);
        }

        var foundToRemove = FoundPrescriptions
            .Where(form => form.PreparationDate.Date >= dateFrom && form.PreparationDate.Date <= dateTo)
            .ToList();
        foreach (var form in foundToRemove)
        {
            FoundPrescriptions.Remove(form);
        }

        SelectedForm = null;
        AssignDisplayNumbers(FoundPrescriptions);
        AssignDisplayNumbers(ImportedForms);
        (GenerateBatchPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
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

            RefreshFormsView();
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

    private bool CanAcceptRecipeInstruction() => SelectedForm is not null && SelectedRecipeInstruction is not null;

    private bool CanAutoAcceptRecipeInstruction() => RecipeInstructions.Count > 0 && (SelectedForm is not null || SelectedImportedForms.Count > 0);

    private void AcceptRecipeInstruction()
    {
        if (SelectedForm is null || SelectedRecipeInstruction is null)
        {
            return;
        }

        ApplyRecipeInstruction(SelectedForm, SelectedRecipeInstruction);
        OnPropertyChanged(nameof(SelectedForm));

        MessageBox.Show(
            $"Wpisano instrukcję {SelectedRecipeInstruction.Code} do pól Obliczenia i Opis wykonania oraz tytuł do pola Uwagi.",
            "PharmaExt",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void AutoAcceptRecipeInstruction()
    {
        var forms = SelectedImportedForms.Count > 0
            ? SelectedImportedForms.ToList()
            : SelectedForm is not null ? [SelectedForm] : [];

        if (forms.Count == 0)
        {
            return;
        }

        var instructionCompositions = RecipeInstructions
            .Select(document => new { Document = document, Names = BuildInstructionIngredientSet(document) })
            .Where(item => item.Names.Count > 0)
            .ToList();

        if (instructionCompositions.Count == 0)
        {
            MessageBox.Show("Nie udało się odczytać składów z instrukcji IR.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var matched = 0;
        var notMatched = 0;
        var errors = 0;
        foreach (var form in forms)
        {
            try
            {
                EnsureIngredientsLoaded(form);
                var formNames = BuildFormInstructionMatchSet(form);
                var match = formNames.Count == 0
                    ? null
                    : instructionCompositions
                        .Where(item => formNames.All(name => item.Names.Contains(name)))
                        .OrderBy(item => item.Names.Count)
                        .ThenBy(item => item.Document.Code)
                        .FirstOrDefault();

                if (match is not null)
                {
                    ApplyRecipeInstruction(form, match.Document);
                    matched++;
                }
                else
                {
                    notMatched++;
                }
            }
            catch
            {
                errors++;
            }
        }

        OnPropertyChanged(nameof(SelectedForm));
        MessageBox.Show(
            $"Automatyczne przypisanie instrukcji zakończone.\n\nDopasowano: {matched}\nBez dopasowania: {notMatched}\nBłędy pobierania składników: {errors}",
            "PharmaExt",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private static void ApplyRecipeInstruction(ImportedForm form, QualityDocument instruction)
    {
        var instructionText = $"Zgodnie z instrukcją numer: {instruction.Code}";
        form.ManualCalculations = instructionText;
        form.ManualPreparationDescription = instructionText;
        form.ManualNotes = instruction.Title;
    }

    private static HashSet<string> BuildFormInstructionMatchSet(ImportedForm form)
    {
        return form.Ingredients
            .Where(ingredient => IsGramUnit(ingredient.Unit))
            .Select(ingredient => NormalizeInstructionIngredientName(ingredient.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static HashSet<string> BuildInstructionIngredientSet(QualityDocument instruction)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? previousCandidateName = null;
        foreach (var rawLine in instruction.ContentText
            .Replace("\r\n", "\n")
            .Split('\n'))
        {
            var line = StripQualityMarkers(rawLine.Trim());
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var inlineName = ExtractInstructionIngredientName(line);
            if (!string.IsNullOrWhiteSpace(inlineName))
            {
                names.Add(inlineName);
                previousCandidateName = null;
                continue;
            }

            if (IsInstructionQuantityOnlyLine(line) && !string.IsNullOrWhiteSpace(previousCandidateName))
            {
                names.Add(previousCandidateName);
                previousCandidateName = null;
                continue;
            }

            previousCandidateName = IsInstructionIngredientNameCandidate(line)
                ? NormalizeInstructionIngredientName(line)
                : null;
        }

        return names;
    }

    private static string ExtractInstructionIngredientName(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return "";
        }

        var normalized = Normalize(line).Replace(".", "");
        if (normalized is "rp" or "mf" || normalized.StartsWith("mf ", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        if (!Regex.IsMatch(line, @"\d+([,.]\d+)?", RegexOptions.IgnoreCase))
        {
            return "";
        }

        var quantityMatch = Regex.Match(line, @"\s+\d+([,.]\d+)?\s*(g|mg|ml|op|szt|kropl|%)?\s*$", RegexOptions.IgnoreCase);
        if (!quantityMatch.Success)
        {
            return "";
        }

        var name = line[..quantityMatch.Index].Trim();
        name = Regex.Replace(name, @"^\d+[\).\-\s]*", "").Trim();
        return NormalizeInstructionIngredientName(name);
    }

    private static bool IsInstructionQuantityOnlyLine(string line)
    {
        return Regex.IsMatch(line.Trim(), @"^(ad\s+)?[≈~]?\s*\d+([,.]\d+)?\s*(g|mg|ml|op|szt|kropl|%)?\s*$", RegexOptions.IgnoreCase);
    }

    private static bool IsInstructionIngredientNameCandidate(string line)
    {
        if (IsQualityHeading(line) || IsNumberedQualitySectionHeading(line))
        {
            return false;
        }

        var normalized = Normalize(line).Replace(".", "");
        if (normalized is "rp" or "mf" or "ds"
            || normalized.Contains("skladnik", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("ilosc", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("rola", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("uwagi", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("postac", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("substancja czynna", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("podloze", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("nosnik", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("dane wejsciowe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (Regex.IsMatch(line, @"\d+([,.]\d+)?", RegexOptions.IgnoreCase))
        {
            return false;
        }

        var normalizedName = NormalizeInstructionIngredientName(line);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return false;
        }

        return normalizedName.Contains(' ', StringComparison.Ordinal)
            || normalizedName.EndsWith("um", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("i", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("ae", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("liq", StringComparison.OrdinalIgnoreCase)
            || normalizedName.EndsWith("gel", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeInstructionIngredientName(string value)
    {
        var text = Normalize(value)
            .Replace("(rec)", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" rec ", " ", StringComparison.OrdinalIgnoreCase)
            .Replace("subst", "", StringComparison.OrdinalIgnoreCase);

        text = Regex.Replace(text, @"[^a-z0-9]+", " ");
        text = Regex.Replace(text, @"\s+", " ").Trim();
        text = Regex.Replace(text, @"\bvit\b", "vitaminum", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\bd3\b", "d3", RegexOptions.IgnoreCase);

        text = text switch
        {
            "devikap" => "vitaminum d3 liq",
            "vigantol" => "vitaminum d3 liq",
            "colecalciferolum" => "vitaminum d3 liq",
            "vitaminum d3" => "vitaminum d3 liq",
            "vitaminum d3 liq" => "vitaminum d3 liq",
            "vitaminum e medana" => "vitaminum e liq",
            "vitaminum e" => "vitaminum e liq",
            "vitaminum e liq" => "vitaminum e liq",
            "levomentholum" => "mentholum",
            _ => text
        };

        return text;
    }

    private static bool IsGramUnit(string unit)
    {
        var normalizedUnit = NormalizeCompositionText(unit);
        return normalizedUnit == "g";
    }

    private void CopyCheckedFieldsGeneratedUnused()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFieldValues(ImportedForm source, ImportedForm target)
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
            target.DrugForm = source.LabelMedicineForm;
        }

        if (CopyMedicineExpiryDate)
        {
            target.MedicineExpiryDate = source.MedicineExpiryDate;
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

    private void ClearCopyCheckboxes()
    {
        CopyLabelType = false;
        CopyLabelSize = false;
        CopyLabelMedicineForm = false;
        CopyMedicineExpiryDate = false;
        CopyMixBeforeUse = false;
        CopyManualCalculations = false;
        CopyManualPreparationDescription = false;
        CopyManualQualityControl = false;
        CopyDosage = false;
        CopyManualNotes = false;
    }

    private void CopyCheckedFieldsOld()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFieldsDuplicateMarker()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFieldsLegacy()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFieldsUnused()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFieldsOriginal()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFieldsBackup()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFieldsTemp()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFields2()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFields3()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFields4()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFields5()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFields6()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFields7()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFields8()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFields9()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void CopyCheckedFields10()
    {
        if (SelectedForm is null || SelectedImportedForms.Count == 0)
        {
            return;
        }

        foreach (var target in SelectedImportedForms)
        {
            CopyCheckedFieldValues(SelectedForm, target);
        }

        ClearCopyCheckboxes();
        MessageBox.Show("Skopiowano zaznaczone pola do wybranych recept.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
    }

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
        RefreshFormsView();

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
            || CopyMedicineExpiryDate
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
        CopyMedicineExpiryDate = false;
        CopyMixBeforeUse = false;
        CopyManualCalculations = false;
        CopyManualPreparationDescription = false;
        CopyManualQualityControl = false;
        CopyDosage = false;
        CopyManualNotes = false;

        OnPropertyChanged(nameof(CopyLabelType));
        OnPropertyChanged(nameof(CopyLabelSize));
        OnPropertyChanged(nameof(CopyLabelMedicineForm));
        OnPropertyChanged(nameof(CopyMedicineExpiryDate));
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

        if (CopyMedicineExpiryDate)
        {
            target.ExpiryTermText = source.ExpiryTermText;
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
            case BatchEditField.MedicineExpiryDate:
                form.MedicineExpiryDate = ParseDate(value);
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

    private static DateTime ParseDate(string value)
    {
        var trimmedValue = value.Trim();
        var formats = new[] { "dd.MM.yyyy", "dd/MM/yyyy", "yyyy-MM-dd" };
        if (DateTime.TryParseExact(trimmedValue, formats, null, System.Globalization.DateTimeStyles.None, out var parsedDate)
            || DateTime.TryParse(trimmedValue, out parsedDate))
        {
            return parsedDate.Date;
        }

        throw new InvalidOperationException("Podaj date w formacie dd.mm.rrrr albo dd/mm/rrrr.");
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
            SelectedForm.Status = FormStatus.PdfGenerated;
            _localDatabase.UpsertImportedForm(SelectedForm);
            SelectedForm.EnableEditTracking();
            RefreshFormsView();
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
            foreach (var form in forms)
            {
                form.Status = FormStatus.PdfGenerated;
                _localDatabase.UpsertImportedForm(form);
                form.EnableEditTracking();
            }

            RefreshFormsView();
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

    private static void AssignDisplayNumbers(IEnumerable<ImportedForm> forms)
    {
        var displayNumber = 1;
        foreach (var form in forms)
        {
            form.DisplayNumber = displayNumber++;
        }
    }

    private static bool TryCreateQualityDocumentFromFile(string filePath, out QualityDocument document)
    {
        document = new QualityDocument();
        var fileName = Path.GetFileNameWithoutExtension(filePath);
        var match = Regex.Match(fileName, @"^(?<code>(SOP|IO|IT|FRM|REJ|ZA[ŁL]|IR)-\d+)\s*(?<title>.*)$", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        var code = match.Groups["code"].Value.ToUpperInvariant().Replace("ZAŁ", "ZAL");
        var title = match.Groups["title"].Value.Trim(' ', '-', '_');
        if (string.IsNullOrWhiteSpace(title))
        {
            title = code;
        }

        var categoryCode = code.Split('-')[0];
        document = new QualityDocument
        {
            Code = code,
            CategoryCode = categoryCode,
            CategoryName = GetQualityCategoryName(categoryCode),
            Title = title,
            Version = "1.0",
            Status = "Obowiazujacy",
            ImportedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        return true;
    }

    private static string CopyQualityDocumentFile(string sourceFilePath, string code, string version)
    {
        var documentsDirectory = GetQualityDocumentsDirectory();
        Directory.CreateDirectory(documentsDirectory);
        var extension = Path.GetExtension(sourceFilePath);
        var targetPath = GetExpectedQualityDocumentPath(code, version, extension);
        File.Copy(sourceFilePath, targetPath, overwrite: true);
        return targetPath;
    }

    private static string CreateQualityDocumentPreview(string sourceFilePath, string code, string version)
    {
        var previewDirectory = GetQualityDocumentPreviewsDirectory();
        Directory.CreateDirectory(previewDirectory);
        var targetPath = GetExpectedQualityPreviewPath(code, version);

        try
        {
            ConvertDocxToHtmlWithWord(sourceFilePath, targetPath);
        }
        catch
        {
            CreateFallbackPreviewHtml(sourceFilePath, targetPath);
        }

        return targetPath;
    }

    private static string FindLocalQualityDocumentFile(QualityDocument document)
    {
        var documentsDirectory = GetQualityDocumentsDirectory();
        var expectedDocx = GetExpectedQualityDocumentPath(document.Code, document.Version, ".docx");
        if (File.Exists(expectedDocx))
        {
            return expectedDocx;
        }

        if (!Directory.Exists(documentsDirectory))
        {
            return "";
        }

        var safeCode = CreateSafeFileNamePart(document.Code);
        var safeVersion = CreateSafeFileNamePart(document.Version);
        return Directory
            .GetFiles(documentsDirectory, $"{safeCode}_v{safeVersion}.*", SearchOption.TopDirectoryOnly)
            .FirstOrDefault() ?? "";
    }

    private static string GetExpectedQualityDocumentPath(string code, string version, string extension)
    {
        var safeCode = CreateSafeFileNamePart(code);
        var safeVersion = CreateSafeFileNamePart(version);
        return Path.Combine(GetQualityDocumentsDirectory(), $"{safeCode}_v{safeVersion}{extension}");
    }

    private static string GetExpectedQualityPreviewPath(string code, string version)
    {
        var safeCode = CreateSafeFileNamePart(code);
        var safeVersion = CreateSafeFileNamePart(version);
        return Path.Combine(GetQualityDocumentPreviewsDirectory(), $"{safeCode}_v{safeVersion}.html");
    }

    private static void ConvertDocxToHtmlWithWord(string sourceFilePath, string targetPath)
    {
        var wordType = Type.GetTypeFromProgID("Word.Application")
            ?? throw new InvalidOperationException("Microsoft Word nie jest zainstalowany.");

        dynamic? wordApplication = null;
        dynamic? document = null;
        try
        {
            wordApplication = Activator.CreateInstance(wordType)
                ?? throw new InvalidOperationException("Nie udalo sie uruchomic Microsoft Word.");
            wordApplication.Visible = false;
            document = wordApplication.Documents.Open(Path.GetFullPath(sourceFilePath), ReadOnly: true, Visible: false);
            document.SaveAs2(Path.GetFullPath(targetPath), 10);
        }
        finally
        {
            if (document is not null)
            {
                document.Close(false);
            }

            if (wordApplication is not null)
            {
                wordApplication.Quit(false);
            }
        }
    }

    private static void CreateFallbackPreviewHtml(string sourceFilePath, string targetPath)
    {
        var text = ExtractDocxText(sourceFilePath);
        var paragraphs = ParseQualityContentLines(text);
        var body = string.Join(Environment.NewLine, paragraphs.Select(line =>
        {
            var escaped = System.Net.WebUtility.HtmlEncode(line.Text);
            return line.IsBold ? $"<p><strong>{escaped}</strong></p>" : $"<p>{escaped}</p>";
        }));

        File.WriteAllText(targetPath, $$"""
            <!doctype html>
            <html>
            <head>
                <meta charset="utf-8">
                <style>
                    body { font-family: Arial, sans-serif; font-size: 12pt; margin: 24px; }
                    p { margin: 0 0 12px 0; }
                </style>
            </head>
            <body>{{body}}</body>
            </html>
            """);
    }

    private static string ExtractDocxText(string filePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(filePath);
            var documentEntry = archive.GetEntry("word/document.xml");
            if (documentEntry is null)
            {
                return "";
            }

            using var stream = documentEntry.Open();
            var document = XDocument.Load(stream);
            var wordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            var textName = XName.Get("t", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
            var paragraphName = XName.Get("p", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
            var runName = XName.Get("r", wordNamespace);
            var boldName = XName.Get("b", wordNamespace);
            var paragraphs = document
                .Descendants(paragraphName)
                .Select(paragraph =>
                {
                    var text = NormalizeQualityDocumentText(string.Concat(paragraph.Descendants(textName).Select(text => text.Value)));
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        return "";
                    }

                    var textRuns = paragraph.Descendants(runName)
                        .Where(run => run.Descendants(textName).Any(textElement => !string.IsNullOrWhiteSpace(textElement.Value)))
                        .ToList();
                    var isBold = textRuns.Count > 0 && textRuns.All(run => run.Descendants(boldName).Any());
                    if (isBold || IsQualityHeading(text))
                    {
                        return $"**{text}**";
                    }

                    return text;
                })
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .ToList();

            return string.Join(Environment.NewLine + Environment.NewLine, paragraphs);
        }
        catch
        {
            return "";
        }
    }

    private static string ExtractQualityDocumentTitle(string contentText, string code, string fallbackTitle)
    {
        var lines = contentText
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => StripQualityMarkers(line.Trim()))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Take(30)
            .ToList();

        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var match = Regex.Match(line, $"^{Regex.Escape(code)}\\s*[-â€“â€”:]?\\s*(?<title>.+)$", RegexOptions.IgnoreCase);
            if (match.Success || ContainsQualityDocumentCode(line, code))
            {
                var title = CleanQualityDocumentTitle(match.Success ? match.Groups["title"].Value : RemoveQualityDocumentCode(line, code), code);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }

                var nextTitle = lines.Skip(index + 1)
                    .Select(candidate => CleanQualityDocumentTitle(candidate, code))
                    .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));
                if (!string.IsNullOrWhiteSpace(nextTitle))
                {
                    return nextTitle;
                }
            }
        }

        var firstUsefulLine = lines
            .Select(line => CleanQualityDocumentTitle(line, code))
            .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));

        return string.IsNullOrWhiteSpace(firstUsefulLine) ? fallbackTitle : firstUsefulLine;
    }

    private static bool ContainsQualityDocumentCode(string value, string code)
    {
        return NormalizeCodeLikeText(value).Contains(NormalizeCodeLikeText(code), StringComparison.OrdinalIgnoreCase);
    }

    private static string RemoveQualityDocumentCode(string value, string code)
    {
        var codePattern = string.Join(@"\s*", code.ToCharArray().Select(character => Regex.Escape(character.ToString())));
        return Regex.Replace(value, codePattern, "", RegexOptions.IgnoreCase)
            .Trim(' ', '-', '_', ':');
    }

    private static string CleanQualityDocumentTitle(string value, string code)
    {
        var title = value.Trim(' ', '-', '_', ':');
        if (string.IsNullOrWhiteSpace(title))
        {
            return "";
        }

        if (title.Equals(code, StringComparison.OrdinalIgnoreCase)
            || NormalizeCodeLikeText(title).Equals(NormalizeCodeLikeText(code), StringComparison.OrdinalIgnoreCase)
            || title.Equals("Spis tresci", StringComparison.OrdinalIgnoreCase)
            || title.Equals("Spis treści", StringComparison.OrdinalIgnoreCase)
            || IsGenericQualityDocumentTitle(title)
            || title.StartsWith("Wersja", StringComparison.OrdinalIgnoreCase)
            || title.StartsWith("Status", StringComparison.OrdinalIgnoreCase))
        {
            return "";
        }

        return title;
    }

    private static string NormalizeCodeLikeText(string value)
    {
        return Regex.Replace(value, @"[^A-Za-z0-9]", "").ToUpperInvariant();
    }

    private static bool IsGenericQualityDocumentTitle(string title)
    {
        var normalizedTitle = Normalize(title);
        return normalizedTitle is "instrukcja wykonania leku recepturowego"
            or "instrukcja wykonania leku"
            or "dokument"
            or "ksiega jakosci"
            || normalizedTitle.StartsWith("instrukcja wykonania leku recepturowego ", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeQualityDocumentText(string value)
    {
        var text = Regex.Replace(value.Trim(), @"\s+", " ");
        text = Regex.Replace(text, @"^(?<code>(SOP|IO|IT|FRM|REJ|ZA[ŁL]|IR)-\d+)(?<title>\S)", "${code} ${title}", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(?<!\s)(Wersja\s*:)", " $1", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"(?<!\s)(Status\s*:)", " $1", RegexOptions.IgnoreCase);
        return text;
    }

    private static bool IsQualityHeading(string text)
    {
        return Regex.IsMatch(text, @"^\d+\.\s+\S")
            || Regex.IsMatch(text, @"^[A-ZĄĆĘŁŃÓŚŹŻ0-9\s\-]{12,}$");
    }

    private static IReadOnlyList<QualityContentLine> ParseQualityContentLines(string content)
    {
        return content
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line =>
            {
                var isBold = line.StartsWith("**", StringComparison.Ordinal) && line.EndsWith("**", StringComparison.Ordinal) && line.Length > 4;
                var text = isBold ? line[2..^2] : line;
                return new QualityContentLine(text, isBold || IsQualityHeading(text));
            })
            .ToList();
    }

    private static string ExtractRecipeInstructionText(string content)
    {
        var lines = content
            .Replace("\r\n", "\n")
            .Split('\n')
            .Select(line => StripQualityMarkers(line.Trim()))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
        {
            return "";
        }

        var startIndex = FindRecipeInstructionStartIndex(lines);
        if (startIndex < 0)
        {
            return string.Join(Environment.NewLine, lines.Take(18));
        }

        var recipeLines = new List<string> { lines[startIndex] };
        for (var index = startIndex + 1; index < lines.Count; index++)
        {
            var line = lines[index];
            if (recipeLines.Count > 1 && IsNumberedQualitySectionHeading(line))
            {
                break;
            }

            if (recipeLines.Count > 1 && IsQualityHeading(line) && !IsRecipeSectionHeader(line))
            {
                break;
            }

            recipeLines.Add(line);
            if (recipeLines.Count >= 18)
            {
                break;
            }
        }

        return string.Join(Environment.NewLine, recipeLines);
    }

    private static int FindRecipeInstructionStartIndex(IReadOnlyList<string> lines)
    {
        var bestIndex = -1;
        var bestScore = 0;

        for (var index = 0; index < lines.Count; index++)
        {
            if (!IsRecipeSectionHeader(lines[index]))
            {
                continue;
            }

            var score = ScoreRecipeSectionCandidate(lines, index);
            if (score > bestScore)
            {
                bestIndex = index;
                bestScore = score;
            }
        }

        if (bestScore < 4)
        {
            return -1;
        }

        if (!IsRpLine(lines[bestIndex]))
        {
            var rpOffset = lines
                .Skip(bestIndex + 1)
                .Take(8)
                .Select((line, offset) => new { line, offset })
                .FirstOrDefault(item => IsRpLine(item.line));
            if (rpOffset is not null)
            {
                return bestIndex + 1 + rpOffset.offset;
            }
        }

        return bestIndex;
    }

    private static int ScoreRecipeSectionCandidate(IReadOnlyList<string> lines, int index)
    {
        var score = 0;
        var current = Normalize(lines[index]).Replace(".", "");
        if (current == "rp" || current.StartsWith("rp ", StringComparison.OrdinalIgnoreCase))
        {
            score += 8;
        }

        var lookAhead = lines.Skip(index + 1).Take(14).ToList();
        if (lookAhead.Any(IsRpLine))
        {
            score += 8;
        }

        score += Math.Min(4, lookAhead.Count(IsLikelyRecipeIngredientLine));

        if (IsLikelyTableOfContentsCandidate(lines, index))
        {
            score -= 6;
        }

        return score;
    }

    private static bool IsRpLine(string line)
    {
        var normalized = Normalize(line).Replace(".", "");
        return normalized == "rp" || normalized.StartsWith("rp ", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLikelyRecipeIngredientLine(string line)
    {
        if (IsQualityHeading(line))
        {
            return false;
        }

        return Regex.IsMatch(line, @"[A-Za-z].*\d+([,.]\d+)?\s*(g|mg|ml|op|szt|kropl|%)?$", RegexOptions.IgnoreCase);
    }

    private static bool IsNumberedQualitySectionHeading(string line)
    {
        return Regex.IsMatch(line, @"^\d+(\.\d+)+\.?\s+\S")
            || Regex.IsMatch(line, @"^\d+\.\s+\S");
    }

    private static bool IsLikelyTableOfContentsCandidate(IReadOnlyList<string> lines, int index)
    {
        var lookAhead = lines.Skip(index + 1).Take(8).ToList();
        if (lookAhead.Any(IsRpLine) || lookAhead.Any(IsLikelyRecipeIngredientLine))
        {
            return false;
        }

        return lookAhead.Count(line => Regex.IsMatch(line, @"^\d+(\.\d+)*\.?\s+\S")) >= 2;
    }

    private static bool IsRecipeSectionHeader(string line)
    {
        var normalized = Normalize(line).Replace(".", "");
        return normalized.StartsWith("rp", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("sklad leku", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("sklad", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("receptura", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("oryginalny przepis", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("przepis", StringComparison.OrdinalIgnoreCase);
    }

    private static string StripQualityMarkers(string line)
    {
        return line.StartsWith("**", StringComparison.Ordinal) && line.EndsWith("**", StringComparison.Ordinal) && line.Length > 4
            ? line[2..^2].Trim()
            : line;
    }

    private static string GetQualityDocumentsDirectory()
    {
        return Path.Combine(LocalDatabaseService.GetDefaultDataDirectory(), "QualityDocuments");
    }

    private static string GetQualityDocumentPreviewsDirectory()
    {
        return Path.Combine(LocalDatabaseService.GetDefaultDataDirectory(), "QualityPreviews");
    }

    private static string GetQualityCategoryName(string categoryCode)
    {
        return categoryCode.ToUpperInvariant() switch
        {
            "SOP" => "Procedury Operacyjne Standardowe",
            "IO" => "Instrukcje Obslugi",
            "IT" => "Instrukcje Technologiczne",
            "FRM" => "Formularze",
            "REJ" => "Rejestry",
            "ZAL" => "Zalaczniki",
            "IR" => "Instrukcje wykonania leku",
            _ => "Inne"
        };
    }

    private static string IncrementVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length == 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
        {
            return $"{major}.{minor + 1}";
        }

        return "1.1";
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed record BatchEditFieldOption(string DisplayName, BatchEditField Field);

public sealed class QualityDocumentTreeItem
{
    public QualityDocumentTreeItem(string name, QualityDocument? document = null)
    {
        Name = name;
        Document = document;
    }

    public string Name { get; }
    public QualityDocument? Document { get; }
    public ObservableCollection<QualityDocumentTreeItem> Children { get; } = new();
}

public sealed record QualityContentLine(string Text, bool IsBold);

public enum BatchEditField
{
    LabelType,
    LabelSize,
    LabelMedicineForm,
    MedicineExpiryDate,
    Dosage,
    ManualCalculations,
    ManualPreparationDescription,
    ManualQualityControl,
    ManualFinalAssessment,
    ManualNotes,
    MixBeforeUse
}
