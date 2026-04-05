using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System;

namespace MAPF_Highway;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (Config.ShowDisplay)
        {
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args, ShutdownMode.OnMainWindowClose);
        }
        else
        {
            SimulationRunner.RunBlocking();
        }
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<MapApp>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}