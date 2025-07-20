using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.Threading.Tasks;
using Avalonia.Controls;

namespace MAPF_Highway;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<MapApp>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}