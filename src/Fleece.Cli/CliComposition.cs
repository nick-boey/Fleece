using System.IO.Abstractions;
using Fleece.Cli.Commands;
using Fleece.Cli.Workflows;
using Fleece.Core.Extensions;
using Fleece.Core.Models;
using Fleece.Core.Services.Interfaces;
using Fleece.GitHub;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;

namespace Fleece.Cli;

public static class CliComposition
{
    public static readonly IReadOnlyList<(string Name, Type CommandType)> Commands = new (string, Type)[]
    {
        ("create",     typeof(CreateCommand)),
        ("list",       typeof(ListCommand)),
        ("edit",       typeof(EditCommand)),
        ("delete",     typeof(DeleteCommand)),
        ("show",       typeof(ShowCommand)),
        ("search",     typeof(SearchCommand)),
        ("migrate",    typeof(MigrateCommand)),
        ("install",    typeof(InstallCommand)),
        ("seal",       typeof(SealCommand)),
        ("auth",       typeof(AuthCommand)),
        ("promote",    typeof(PromoteCommand)),
        ("absorb",     typeof(AbsorbCommand)),
        ("openspec dependencies", typeof(OpenSpecDependenciesCommand)),
        ("prime",      typeof(PrimeCommand)),
        ("validate",   typeof(ValidateCommand)),
        ("commit",     typeof(CommitCommand)),
        ("dependency", typeof(DependencyCommand)),
        ("move",       typeof(MoveCommand)),
        ("next",       typeof(NextCommand)),
        ("config",     typeof(ConfigCommand)),
        ("open",       typeof(OpenCommand)),
        ("progress",   typeof(ProgressCommand)),
        ("review",     typeof(ReviewCommand)),
        ("complete",   typeof(CompleteCommand)),
        ("closed",     typeof(ClosedCommand)),
    };

    public static IServiceCollection BuildServices(
        string? basePath = null,
        IFileSystem? fileSystem = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var services = new ServiceCollection();
        services.AddFleeceInMemoryService(basePath, fileSystem);
        services.AddFleeceGitHub();
        var fs = fileSystem ?? new Testably.Abstractions.RealFileSystem();
        services.AddSingleton(new BasePathProvider(basePath ?? fs.Directory.GetCurrentDirectory()));
        services.AddSingleton<IAnsiConsole>(_ => AnsiConsole.Console);

        // Resolve the durable-tracker workflow from the configured `tracker` setting. The factory is
        // lazy, so it reads the effective setting (and resolves IGitHubService / the console) at
        // command-execution time — after tests substitute their console or a fake IGitHubService.
        // In Linear mode GitHubTrackerWorkflow is never constructed, so IGitHubService is never
        // resolved and the path stays hermetic (no credential, no fake GitHub service required).
        services.AddSingleton<ITrackerWorkflow>(sp =>
        {
            var settingsService = sp.GetRequiredService<ISettingsService>();
            var effective = settingsService.GetEffectiveSettingsAsync().GetAwaiter().GetResult();
            return string.Equals(effective.Tracker, Trackers.Linear, StringComparison.Ordinal)
                ? ActivatorUtilities.CreateInstance<LinearTrackerWorkflow>(sp)
                : ActivatorUtilities.CreateInstance<GitHubTrackerWorkflow>(sp);
        });

        // Lets test composition substitute services (e.g. a fake IGitHubService) before the
        // provider is built. Registrations here win because DI resolves the last registration.
        configureServices?.Invoke(services);
        return services;
    }
}
