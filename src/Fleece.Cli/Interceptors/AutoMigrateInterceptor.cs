using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Interceptors;

/// <summary>
/// Command interceptor that automatically runs schema migration before commands execute
/// when the snapshot uses a legacy property format (e.g. "sortOrder" instead of "lexOrder").
/// </summary>
public sealed class AutoMigrateInterceptor : ICommandInterceptor
{
    private readonly Func<IServiceProvider> _serviceProviderFactory;
    private IServiceProvider? _serviceProvider;

    private static readonly HashSet<string> SkipCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "migrate",  // Would cause recursion
        "diff",
        "install",
        "prime",
    };

    public AutoMigrateInterceptor(Func<IServiceProvider> serviceProviderFactory)
    {
        _serviceProviderFactory = serviceProviderFactory;
    }

    private IServiceProvider ServiceProvider => _serviceProvider ??= _serviceProviderFactory();

    public void Intercept(CommandContext context, CommandSettings settings)
    {
        var commandName = context.Name ?? string.Empty;
        if (string.IsNullOrEmpty(commandName) || SkipCommands.Contains(commandName))
        {
            return;
        }

        InterceptAsync().GetAwaiter().GetResult();
    }

    private async Task InterceptAsync()
    {
        var migration = ServiceProvider.GetRequiredService<IMigrationService>();

        if (!await migration.IsMigrationNeededAsync())
        {
            return;
        }

        var gitConfig = ServiceProvider.GetRequiredService<IGitConfigService>();
        var by = gitConfig.GetUserName() ?? Environment.UserName;
        await migration.MigrateAsync(by);

        var console = ServiceProvider.GetRequiredService<IAnsiConsole>();
        console.MarkupLine("[dim]Auto-migrated snapshot format[/]");
    }
}
