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
}
