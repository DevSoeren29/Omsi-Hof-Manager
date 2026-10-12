using System.Text;
using System.Windows;
using System.Windows.Threading;

namespace OmsiHofManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // OMSI-Dateien sind meist in Windows-1252 kodiert.
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);
    }

    private static void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show($"Ein unerwarteter Fehler ist aufgetreten:\n\n{e.Exception.Message}",
            "OMSI Hof Manager", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
