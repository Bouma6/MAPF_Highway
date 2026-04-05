using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Planner;

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
            var runner = new SimulationRunner();
            runner.RunAsync(Config.SimulationStepCount).GetAwaiter().GetResult();
        }
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<MapApp>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}