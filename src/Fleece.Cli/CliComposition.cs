using System.IO.Abstractions;
using Fleece.Cli.Commands;
using Fleece.Core.Extensions;
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
        ("clean",      typeof(CleanCommand)),
        ("show",       typeof(ShowCommand)),
        ("search",     typeof(SearchCommand)),
        ("diff",       typeof(DiffCommand)),
        ("merge",      typeof(MergeCommand)),
        ("migrate",    typeof(MigrateCommand)),
        ("install",    typeof(InstallCommand)),
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
        ("archived",   typeof(ArchivedCommand)),
        ("closed",     typeof(ClosedCommand)),
    };

    public static IServiceCollection BuildServices(string? basePath = null, IFileSystem? fileSystem = null)
    {
        var services = new ServiceCollection();
        services.AddFleeceInMemoryService(basePath, fileSystem);
        var fs = fileSystem ?? new Testably.Abstractions.RealFileSystem();
        services.AddSingleton(new BasePathProvider(basePath ?? fs.Directory.GetCurrentDirectory()));
        services.AddSingleton<IAnsiConsole>(_ => AnsiConsole.Console);
        return services;
    }
}
