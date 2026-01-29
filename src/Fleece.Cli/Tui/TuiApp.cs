using Fleece.Cli.Tui.Views;
using Fleece.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Terminal.Gui;

namespace Fleece.Cli.Tui;

/// <summary>
/// Main TUI application entry point.
/// </summary>
public sealed class TuiApp
{
    private readonly IServiceProvider _services;

    public TuiApp(IServiceProvider services)
    {
        _services = services;
    }

    /// <summary>
    /// Runs the TUI application.
    /// </summary>
    /// <returns>Exit code (0 for success).</returns>
    public int Run()
    {
        Application.Init();

        try
        {
            // Check for merge conflicts before starting
            var storageService = _services.GetRequiredService<IStorageService>();
            var (hasMultiple, message) = storageService.HasMultipleUnmergedFilesAsync().GetAwaiter().GetResult();

            if (hasMultiple)
            {
                Application.Shutdown();
                Console.Error.WriteLine(message);
                return 1;
            }

            var mainWindow = new MainWindow(_services);
            Application.Run(mainWindow);
            mainWindow.Dispose();

            return 0;
        }
        catch (Exception ex)
        {
            Application.Shutdown();
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            Application.Shutdown();
        }
    }
}
