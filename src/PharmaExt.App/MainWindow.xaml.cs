using System.IO;
using System.Windows;
using System.Windows.Controls;
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
