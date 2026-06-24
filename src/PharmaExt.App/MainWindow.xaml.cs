using System.Windows;
using System.Windows.Controls;
using PharmaExt.App.ViewModels;

namespace PharmaExt.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }

    private void FirebirdPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel && sender is PasswordBox passwordBox)
        {
            viewModel.Settings.Firebird.Password = passwordBox.Password;
        }
    }
}
