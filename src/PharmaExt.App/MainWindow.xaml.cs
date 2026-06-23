using System.Windows;
using PharmaExt.App.ViewModels;

namespace PharmaExt.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel();
    }
}
