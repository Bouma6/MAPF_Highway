using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Documents;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System.Diagnostics;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using FrameWork;

namespace MAPF_Highway;

public class MapApp : Application
{
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            ConsoleRenderer.Init();
            desktop.MainWindow = ConsoleRenderer.Window;

            // Same simulation loop as headless (thread pool); map refresh is marshalled to the UI thread inside RunAsync.
            SimulationRunner.RunInBackgroundForDisplay();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

public static class ConsoleRenderer
{
    public static Window Window = null!;
    private static TextBlock _textBlock = null!;
    private static bool _initialized = false;

    private static readonly Color[] TaskColors = new[]
    {
        Color.FromRgb(255, 255, 0),   
        Color.FromRgb(255, 235, 59),  
        Color.FromRgb(255, 213, 79),  
        Color.FromRgb(255, 193, 7),   
        Color.FromRgb(255, 171, 64),  
        Color.FromRgb(255, 152, 0),   
        Color.FromRgb(255, 138, 101), 
        Color.FromRgb(255, 112, 67),  
        Color.FromRgb(244, 67, 54),   
        Color.FromRgb(229, 57, 53),   
        Color.FromRgb(211, 47, 47),   
        Color.FromRgb(198, 40, 40),   
        Color.FromRgb(183, 28, 28),   
        Color.FromRgb(255, 205, 210), 
        Color.FromRgb(255, 245, 157), 
        Color.FromRgb(255, 224, 130)  
    };

    private static readonly Color[] RobotColors = new[]
    {
        Color.FromRgb(227, 242, 253), 
        Color.FromRgb(21, 101, 192),  
        Color.FromRgb(100, 181, 246), 
        Color.FromRgb(187, 222, 251), 
        Color.FromRgb(144, 202, 249), 
        Color.FromRgb(66, 165, 245),  
        Color.FromRgb(33, 150, 243),  
        Color.FromRgb(30, 136, 229),  
        Color.FromRgb(25, 118, 210),  
        Color.FromRgb(13, 71, 161),   
        Color.FromRgb(41, 121, 255),  
        Color.FromRgb(68, 138, 255)   
    };

    public static void Init()
    {
        int mapHeight;
        int mapWidth;
        try
        {
            string[] lines = File.ReadAllLines(Config.MapPath);
            
            if (lines.Length < 5)
                throw new ArgumentException("Map file must have at least 5 lines (header + at least 1 map row).");
            
            mapWidth = lines[4].Length;
            mapHeight = lines.Length - 4;
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
    
    private static readonly Dictionary<Color, SolidColorBrush> _brushCache = new();

    private static SolidColorBrush GetBrush(Color c)
    {
        if (!_brushCache.TryGetValue(c, out var brush))
        {
            brush = new SolidColorBrush(c);
            _brushCache[c] = brush;
        }
        return brush;
    }

    public static void UpdateColoredMap(Map map, TaskMaster taskMaster, RobotMaster robotMaster)
    {
        Init();

        var positionToTaskIndex = new Dictionary<Position, int>();
        for (int i = 0; i < taskMaster.Tasks.Count; i++)
        {
            var task = taskMaster.Tasks[i];
            positionToTaskIndex[task.Pickup] = i;
            positionToTaskIndex[task.Destination] = i;
        }

        var robotByPosition = new Dictionary<Position, RobotId>();
        foreach (var robot in robotMaster)
            robotByPosition[robot.Position] = robot.RobotId;

        _textBlock.Inlines!.Clear();

        var batch = new System.Text.StringBuilder(map.Width + 1);
        Color batchColor = Colors.White;

        void FlushBatch()
        {
            if (batch.Length == 0) return;
            _textBlock.Inlines!.Add(new Run(batch.ToString()) { Foreground = GetBrush(batchColor) });
            batch.Clear();
        }

        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                var position = new Position(y, x);
                char symbol;
                Color color;

                if (robotByPosition.TryGetValue(position, out var robotId))
                {
                    symbol = MapSymbols.Robot.ToSymbol();
                    color = RobotColors[robotId.AsInt % RobotColors.Length];
                }
                else if (positionToTaskIndex.TryGetValue(position, out var taskIndex))
                {
                    var task = taskMaster.Tasks[taskIndex];
                    symbol = position.Equals(task.Pickup) ? MapSymbols.Pickup.ToSymbol() : MapSymbols.Destination.ToSymbol();
                    color = TaskColors[taskIndex % TaskColors.Length];
                }
                else
                {
                    symbol = map[x, y].ToSymbol();
                    color = Colors.White;
                }

                if (color != batchColor && batch.Length > 0)
                    FlushBatch();
                batchColor = color;
                batch.Append(symbol);
            }
            batch.Append('\n');
        }
        FlushBatch();

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
