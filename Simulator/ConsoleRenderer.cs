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
            desktop.MainWindow = ConsoleRenderer.Window;
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
        if (_initialized) return;

        _textBlock = new TextBlock
        {
            Text = "Waiting for map...",
            FontFamily = "Consolas",
            FontSize = 14,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch
        };

        Window = new Window
        {
            Title = "ASCII Map",
            Width = 800,
            Height = 600,
            Content = _textBlock
        };

        _initialized = true;
    }

    public static void UpdateText(string newText)
    {
        Init();

        _textBlock.Text = newText;

        if (!Window.IsVisible)
        {
            Window.Show();
        }
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
