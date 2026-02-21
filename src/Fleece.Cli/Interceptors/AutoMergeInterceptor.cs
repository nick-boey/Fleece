using Fleece.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Interceptors;

/// <summary>
/// Command interceptor that automatically merges issue files before commands execute.
/// This prevents the "Multiple unmerged issue files found" error by running merge transparently.
/// </summary>
public sealed class AutoMergeInterceptor : ICommandInterceptor
{
    private readonly Func<IServiceProvider> _serviceProviderFactory;
    private IServiceProvider? _serviceProvider;

    /// <summary>
    /// Commands that should skip auto-merge to avoid recursion or because they don't need merged files.
    /// </summary>
    private static readonly HashSet<string> SkipCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "merge",      // Would cause recursion
        "diff",       // Works with specific files
        "install",    // Git hook installation
        "prime",      // Documentation output
        "config",     // Settings management
        "migrate",    // Migration has its own file handling
        "commit"      // Git operations
    };

    public AutoMergeInterceptor(Func<IServiceProvider> serviceProviderFactory)
    {
        _serviceProviderFactory = serviceProviderFactory;
    }

    private IServiceProvider ServiceProvider => _serviceProvider ??= _serviceProviderFactory();

    public void Intercept(CommandContext context, CommandSettings settings)
    {
        // Skip if this is a command that shouldn't trigger auto-merge
        var commandName = context.Name ?? string.Empty;
        if (ShouldSkipCommand(commandName))
        {
            return;
        }

        // Run the async method synchronously since Intercept is not async
        InterceptAsync().GetAwaiter().GetResult();
    }

    private bool ShouldSkipCommand(string commandName)
    {
        // Skip for known commands that don't need auto-merge
        if (SkipCommands.Contains(commandName))
        {
            return true;
        }

        // Skip for empty command name (help, version, etc.)
        if (string.IsNullOrEmpty(commandName))
        {
            return true;
        }

        return false;
    }

    private async Task InterceptAsync()
    {
        var settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
        var storageService = ServiceProvider.GetRequiredService<IStorageService>();
        var mergeService = ServiceProvider.GetRequiredService<IMergeService>();

        // Check if auto-merge is enabled
        var effectiveSettings = await settingsService.GetEffectiveSettingsAsync();
        if (!effectiveSettings.AutoMerge)
        {
            return;
        }

        // Check if merge is needed
        var (hasMultiple, _) = await storageService.HasMultipleUnmergedFilesAsync();
        if (!hasMultiple)
        {
            return;
        }

        // Perform the merge
        var mergedCount = await mergeService.FindAndResolveDuplicatesAsync(dryRun: false);

        // Show one-liner output if merge occurred
        if (mergedCount > 0)
        {
            AnsiConsole.MarkupLine($"[dim]Auto-merged {mergedCount} issue(s)[/]");
        }
    }
}
