using System.Windows;
using OrderDesk.Desktop.ViewModels;

namespace OrderDesk.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            try { await vm.InitializeAsync(); }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not load data. Ensure LegacyRetailDB is provisioned (run DbProvisioner).\n\n{ex.Message}",
                    "OrderDesk", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
