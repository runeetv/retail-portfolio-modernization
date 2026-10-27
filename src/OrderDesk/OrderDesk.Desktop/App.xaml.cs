using System.Windows;
using System.Windows.Threading;

namespace OrderDesk.Desktop;

public partial class App : System.Windows.Application
{
    public App()
    {
        DispatcherUnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"An unexpected error occurred:\n\n{e.Exception.Message}",
            "OrderDesk", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
