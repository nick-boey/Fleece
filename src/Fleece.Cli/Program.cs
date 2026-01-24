using System.Reflection;
using Fleece.Cli.Commands;
using Fleece.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

var services = new ServiceCollection();
services.AddFleeceCore();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

// Get the version from the assembly, which is set during build via -p:Version=
var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "1.0.0";

app.Configure(config =>
{
    config.SetApplicationName("fleece");
    config.SetApplicationVersion(version);

    config.AddCommand<CreateCommand>("create")
        .WithDescription("Create a new issue. Run without options to open an interactive editor with a YAML template.")
        .WithExample("create")
        .WithExample("create", "--title", "Fix login bug", "--type", "bug", "-p", "1");

    config.AddCommand<ListCommand>("list")
        .WithDescription("List issues with optional filters")
        .WithExample("list", "--status", "open", "--type", "bug");

    config.AddCommand<TreeCommand>("tree")
        .WithDescription("Display issues in a tree view based on parent-child relationships")
        .WithExample("tree")
        .WithExample("tree", "--status", "open");

    config.AddCommand<EditCommand>("edit")
        .WithDescription("Edit an existing issue. Run with only an ID to open an interactive editor with the issue's current values.")
        .WithExample("edit", "abc123")
        .WithExample("edit", "abc123", "--status", "complete");

    config.AddCommand<DeleteCommand>("delete")
        .WithDescription("Delete an issue by ID")
        .WithExample("delete", "abc123");

    config.AddCommand<SearchCommand>("search")
        .WithDescription("Search issues by text")
        .WithExample("search", "login");

    config.AddCommand<DiffCommand>("diff")
        .WithDescription("Show change history or compare two JSONL files")
        .WithExample("diff")
        .WithExample("diff", "abc123")
        .WithExample("diff", "--user", "john")
        .WithExample("diff", "file1.jsonl", "file2.jsonl");

    config.AddCommand<MergeCommand>("merge")
        .WithDescription("Find and merge duplicate issues")
        .WithExample("merge")
        .WithExample("merge", "--dry-run");

    config.AddCommand<MigrateCommand>("migrate")
        .WithDescription("Migrate issues to property-level timestamps format")
        .WithExample("migrate")
        .WithExample("migrate", "--dry-run");

    config.AddCommand<InstallCommand>("install")
        .WithDescription("Install Claude Code hooks");

    config.AddCommand<PrimeCommand>("prime")
        .WithDescription("Print LLM instructions for issue tracking");

    config.AddCommand<QuestionCommand>("question")
        .WithDescription("Manage questions on an issue")
        .WithExample("question", "abc123", "--list")
        .WithExample("question", "abc123", "--ask", "What is the expected behavior?")
        .WithExample("question", "abc123", "--answer", "Q12345", "--text", "It should return a 200 status");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Validate issue dependencies for cycles")
        .WithExample("validate")
        .WithExample("validate", "--json");
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
