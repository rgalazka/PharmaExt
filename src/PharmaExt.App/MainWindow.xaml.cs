using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using PharmaExt.App.ViewModels;

namespace PharmaExt.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;
        FirebirdPasswordBox.Password = viewModel.Settings.Firebird.Password;
    }

    private void FirebirdPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.Settings.Firebird.Password = passwordBox.Password;
        }
    }

    private void ImportedFormsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is DataGrid dataGrid)
        {
            viewModel.SetSelectedImportedForms(dataGrid.SelectedItems.OfType<Models.ImportedForm>());
        }
    }

    private void BrowseFirebirdDatabase_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Wybierz bazę Firebird",
            Filter = "Baza Firebird (*.fdb)|*.fdb|Wszystkie pliki (*.*)|*.*",
            CheckFileExists = true
        };

        var currentPath = viewModel.Settings.Firebird.DatabasePath;
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var currentDirectory = Path.GetDirectoryName(currentPath);
            if (!string.IsNullOrWhiteSpace(currentDirectory) && Directory.Exists(currentDirectory))
            {
                dialog.InitialDirectory = currentDirectory;
            }
        }

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        viewModel.Settings.Firebird.DatabasePath = dialog.FileName;
        FirebirdDatabasePathTextBox.Text = dialog.FileName;
        FirebirdDatabasePathTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private void BrowseOutputDirectory_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var dialog = new OpenFolderDialog
        {
            Title = "Wybierz katalog zapisu PDF",
            Multiselect = false
        };

        var currentPath = viewModel.Settings.OutputDirectory;
        if (!string.IsNullOrWhiteSpace(currentPath))
        {
            var fullPath = Path.GetFullPath(currentPath);
            if (Directory.Exists(fullPath))
            {
                dialog.InitialDirectory = fullPath;
            }
        }

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        viewModel.Settings.OutputDirectory = dialog.FolderName;
        OutputDirectoryTextBox.Text = dialog.FolderName;
        OutputDirectoryTextBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private void QualityDocumentTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is MainViewModel viewModel && e.NewValue is QualityDocumentTreeItem item)
        {
            viewModel.SelectedQualityTreeItem = item;
            NavigateQualityPreview(viewModel);
        }
    }

    private void NavigateQualityPreview(MainViewModel viewModel)
    {
        var previewPath = viewModel.EnsureSelectedQualityPreviewPath();
        if (string.IsNullOrWhiteSpace(previewPath) || !File.Exists(previewPath))
        {
            QualityPreviewBrowser.NavigateToString("<html><body style='font-family: Arial; margin: 16px;'>Brak podgladu dokumentu.</body></html>");
            return;
        }

        QualityPreviewBrowser.Navigate(new Uri(Path.GetFullPath(previewPath)));
    }
}
