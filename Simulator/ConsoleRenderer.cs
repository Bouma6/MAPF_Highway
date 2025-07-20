using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Threading.Tasks;
using System.IO;

namespace MAPF_Highway;

public class MapApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ConsoleRenderer.Init();
            desktop.MainWindow = ConsoleRenderer.Window;

            // Run simulation
            var runner = new SimulationRunner();
            _ = runner.RunAsync(Config.Steps);
        }

        base.OnFrameworkInitializationCompleted();
    }
}

public static class ConsoleRenderer
{
    public static Window Window;
    private static TextBlock _textBlock;
    private static bool _initialized = false;
    public static void Init()
    {
        int mapHeight;
        int mapWidth;
        try
        {
            string[] lines = File.ReadAllLines(Config.MapName);
            
            if (lines.Length == 0)
                throw new ArgumentException("Map file is empty.");
            
            mapWidth = lines[5].Length;
            mapHeight = lines.Length-4;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing map: {ex.Message}");
            throw;
        }
        
        if (_initialized) return;

        const int charWidth = 10;
        const int charHeight = 20;
        const int margin = 40;

        _textBlock = new TextBlock
        {
            Text = "Waiting for map...",
            FontFamily = new Avalonia.Media.FontFamily("Consolas, Courier New, Monospace"),
            FontSize = 16,
            Foreground = Avalonia.Media.Brushes.White,
            Background = Avalonia.Media.Brushes.Black,
            TextWrapping = Avalonia.Media.TextWrapping.NoWrap,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };

        Window = new Window
        {
            Title = "ASCII Map",
            Width = mapWidth * charWidth + margin,
            Height = mapHeight * charHeight + margin,
            Background = Avalonia.Media.Brushes.Black,
            Content = _textBlock
        };

        _initialized = true;
    }



    public static void UpdateText(string newText)
    {
        Console.WriteLine("Updating text");
        Init();

        _textBlock.Text = newText;

        if (!Window.IsVisible)
        {
            Window.Show();
        }
        Console.WriteLine("Showing window");
    }

    public static async Task StartAvalonia()
    {
        Init();
        await Task.Run(() =>
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(new string[0]);
        });
    }

    private static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<MapApp>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}
