using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
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

    public MainViewModel()
    {
        Settings = new AppSettings();
        _prescriptionReader = new MockFirebirdPrescriptionReader(Settings);
        _localDatabase = new LocalDatabaseService();
        _protocolPdfGenerator = new ProtocolPdfGenerator();
        _labelPdfGenerator = new BrotherLabelPdfGenerator();

        SearchDateFrom = DateTime.Today.AddDays(-7);
        SearchDateTo = DateTime.Today;

        SearchCommand = new RelayCommand(Search);
        SaveCommand = new RelayCommand(Save);
        GenerateProtocolPdfCommand = new RelayCommand(GenerateProtocolPdf, () => SelectedForm is not null);
        GenerateExternalLabelsCommand = new RelayCommand(() => GenerateLabels(LabelType.Zewnetrznie));
        GenerateInternalLabelsCommand = new RelayCommand(() => GenerateLabels(LabelType.Wewnetrznie));

        _localDatabase.Initialize();
        Search();
        ImportFoundPrescriptions();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppSettings Settings { get; }
    public DateTime? SearchDateFrom { get; set; }
    public DateTime? SearchDateTo { get; set; }
    public string SearchAddress { get; set; } = "";
    public ObservableCollection<ImportedForm> FoundPrescriptions { get; } = new();
    public ObservableCollection<ImportedForm> ImportedForms { get; } = new();
    public LabelType[] LabelTypes { get; } = Enum.GetValues<LabelType>();
    public LabelSize[] LabelSizes { get; } = Enum.GetValues<LabelSize>();

    public ImportedForm? SelectedForm
    {
        get => _selectedForm;
        set
        {
            _selectedForm = value;
            OnPropertyChanged();
            (GenerateProtocolPdfCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public ICommand SearchCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand GenerateProtocolPdfCommand { get; }
    public ICommand GenerateExternalLabelsCommand { get; }
    public ICommand GenerateInternalLabelsCommand { get; }

    private void Search()
    {
        FoundPrescriptions.Clear();
        var results = _prescriptionReader.Search(SearchDateFrom, SearchDateTo, SearchAddress);
        foreach (var form in results)
        {
            FoundPrescriptions.Add(form);
        }
    }

    private void ImportFoundPrescriptions()
    {
        ImportedForms.Clear();
        foreach (var form in FoundPrescriptions)
        {
            ImportedForms.Add(form);
        }

        SelectedForm = ImportedForms.FirstOrDefault();
    }

    private void Save()
    {
        _localDatabase.SaveSettings(Settings);
        foreach (var form in ImportedForms)
        {
            _localDatabase.UpsertImportedForm(form);
        }

        MessageBox.Show("Zapisano dane lokalnie.", "PharmaExt", MessageBoxButton.OK, MessageBoxImage.Information);
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
