using System.Windows;
using System.Diagnostics;
using System.IO;

namespace Git_Heatmap;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (e.Args.Length == 0)
        {
            base.OnStartup(e);
            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            return;
        }

        var cliPath = Path.Combine(AppContext.BaseDirectory, "GitHeatmap.Cli.exe");
        if (!File.Exists(cliPath))
        {
            var fallbackExitCode = Task.Run(() => CommandLineHandler.RunAsync(e.Args)).GetAwaiter().GetResult();
            Environment.Exit(fallbackExitCode);
            return;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = cliPath,
            Arguments = BuildArgumentString(e.Args),
            UseShellExecute = false
        };

        using var process = Process.Start(startInfo);
        process?.WaitForExit();
        var exitCode = process?.ExitCode ?? 1;
        Environment.Exit(exitCode);
    }

    private static string BuildArgumentString(IReadOnlyList<string> args)
    {
        return string.Join(" ", args.Select(EscapeArgument));
    }

    private static string EscapeArgument(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "\"\"";
        }

        if (!value.Any(char.IsWhiteSpace) && !value.Contains('"'))
        {
            return value;
        }

        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
