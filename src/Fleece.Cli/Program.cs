using Fleece.Cli.Commands;
using Fleece.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddFleeceCore();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

app.Configure(config =>
{
    config.SetApplicationName("fleece");
    config.SetApplicationVersion("1.0.0");

    config.AddCommand<CreateCommand>("create")
        .WithDescription("Create a new issue")
        .WithExample("create", "--title", "Fix login bug", "--type", "bug", "-p", "1");

    config.AddCommand<ListCommand>("list")
        .WithDescription("List issues with optional filters")
        .WithExample("list", "--status", "open", "--type", "bug");

    config.AddCommand<EditCommand>("edit")
        .WithDescription("Edit an existing issue")
        .WithExample("edit", "abc123", "--status", "complete");

    config.AddCommand<DeleteCommand>("delete")
        .WithDescription("Delete an issue by ID")
        .WithExample("delete", "abc123");

    config.AddCommand<SearchCommand>("search")
        .WithDescription("Search issues by text")
        .WithExample("search", "login");

    config.AddCommand<DiffCommand>("diff")
        .WithDescription("Compare files or show conflicts")
        .WithExample("diff")
        .WithExample("diff", "file1.jsonl", "file2.jsonl");

    config.AddCommand<MergeCommand>("merge")
        .WithDescription("Find duplicates and move older versions to conflicts")
        .WithExample("merge")
        .WithExample("merge", "--dry-run");

    config.AddCommand<ClearConflictsCommand>("clear-conflicts")
        .WithDescription("Clear conflict records for an issue")
        .WithExample("clear-conflicts", "abc123");

    config.AddCommand<MigrateCommand>("migrate")
        .WithDescription("Migrate issues to property-level timestamps format")
        .WithExample("migrate")
        .WithExample("migrate", "--dry-run");

    config.AddCommand<InstallCommand>("install")
        .WithDescription("Install Claude Code hooks");

    config.AddCommand<PrimeCommand>("prime")
        .WithDescription("Print LLM instructions for issue tracking");
});

return await app.RunAsync(args);

// Type registrar for Spectre.Console.Cli DI integration
public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    public ITypeResolver Build() => new TypeResolver(services.BuildServiceProvider());

    public void Register(Type service, Type implementation) =>
        services.AddSingleton(service, implementation);

    public void RegisterInstance(Type service, object implementation) =>
        services.AddSingleton(service, implementation);

    public void RegisterLazy(Type service, Func<object> factory) =>
        services.AddSingleton(service, _ => factory());
}

public sealed class TypeResolver(IServiceProvider provider) : ITypeResolver
{
    public object? Resolve(Type? type) =>
        type is null ? null : provider.GetService(type);
}
