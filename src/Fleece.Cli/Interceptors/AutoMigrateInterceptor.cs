using System.IO.Abstractions;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Fleece.Cli.Interceptors;

/// <summary>
/// Command interceptor that automatically runs schema migration before commands execute
/// when the snapshot uses a legacy property format (e.g. "sortOrder" instead of "lexOrder").
/// It also surfaces a non-destructive warning when a legacy durable
/// <c>.fleece/issues.jsonl</c> snapshot (the pre-v4 durable layout) is present, pointing
/// the user at <c>fleece prime v4-migration</c>.
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
        // Non-destructive: warn on a legacy durable snapshot regardless of command so the
        // hint reaches the user no matter what they run.
        WarnIfLegacyDurableSnapshotPresent();

        var commandName = context.Name ?? string.Empty;
        if (string.IsNullOrEmpty(commandName) || SkipCommands.Contains(commandName))
        {
            return;
        }

        InterceptAsync().GetAwaiter().GetResult();
    }

    /// <summary>
    /// Prints a non-destructive warning when a pre-v4 durable snapshot file
    /// (<c>.fleece/issues.jsonl</c>) is present. v4 stores issues as per-issue logs under
    /// <c>.fleece/issues/</c>, so the snapshot file only exists in legacy repositories. No
    /// data is read, converted, or deleted.
    /// </summary>
    private void WarnIfLegacyDurableSnapshotPresent()
    {
        var fileSystem = ServiceProvider.GetRequiredService<IFileSystem>();
        var basePath = ServiceProvider.GetRequiredService<BasePathProvider>();
        var legacySnapshot = fileSystem.Path.Combine(basePath.BasePath, ".fleece", "issues.jsonl");

        if (!fileSystem.File.Exists(legacySnapshot))
        {
            return;
        }

        var console = ServiceProvider.GetRequiredService<IAnsiConsole>();
        console.MarkupLine(
            "[yellow]Legacy Fleece issues detected ([/][yellow].fleece/issues.jsonl[/][yellow]).[/] " +
            "Run [green]fleece prime v4-migration[/] to migrate long-running issues to GitHub Issues. " +
            "[dim](No data has been changed.)[/]");
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
        // convertDurableLayout: false — the interceptor only ever auto-migrates the hashed-file
        // layout. The durable snapshot is converted solely by an explicit `fleece migrate`, so
        // it is never silently consumed on an unrelated command (even alongside hashed files).
        await migration.MigrateAsync(by, convertDurableLayout: false);

        var console = ServiceProvider.GetRequiredService<IAnsiConsole>();
        console.MarkupLine("[dim]Auto-migrated snapshot format[/]");
    }
}
