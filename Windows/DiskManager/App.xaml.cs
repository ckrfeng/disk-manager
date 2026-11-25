using System.Windows;
using DiskCore;

namespace DiskManager;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // The elevated copy started by "Scan as Administrator" only scans and exits, without any window.
        if (e.Args.Length == 3 && e.Args[0] == HelperScan.Flag)
        {
            Shutdown(HelperScan.Run(e.Args[1], e.Args[2]));
            return;
        }
        base.OnStartup(e);
        MainWindow = new MainWindow();
        MainWindow.Show();
    }
}
