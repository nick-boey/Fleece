using System.Reflection;
using System.Text;
using Fleece.Cli;
using Fleece.Cli.Commands;
using Fleece.Cli.Interceptors;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

Console.OutputEncoding = Encoding.UTF8;

// Get the version from the assembly, which is set during build via -p:Version=
var version = Assembly.GetExecutingAssembly()
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
    ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
    ?? "1.0.0";

var services = CliComposition.BuildServices();

var registrar = new TypeRegistrar(services);
var app = new CommandApp(registrar);

// Create interceptor with lazy service provider resolution
var autoMergeInterceptor = new AutoMergeInterceptor(() => registrar.GetServiceProvider());

app.Configure(config =>
{
    config.SetApplicationName("fleece");
    config.SetApplicationVersion(version);
    config.SetInterceptor(autoMergeInterceptor);

    config.AddCommand<CreateCommand>("create")
        .WithDescription("Create a new issue. --title and --type are required.")
        .WithExample("create", "--title", "Fix login bug", "--type", "bug", "-p", "1");

    config.AddCommand<ListCommand>("list")
        .WithDescription("List issues with optional filters")
        .WithExample("list", "--status", "open", "--type", "bug")
        .WithExample("list", "--tree")
        .WithExample("list", "--tree", "--tree-root", "abc123")
        .WithExample("list", "--next");

    config.AddCommand<EditCommand>("edit")
        .WithDescription("Edit an existing issue. At least one field flag is required.")
        .WithExample("edit", "abc123", "--status", "complete");

    config.AddCommand<DeleteCommand>("delete")
        .WithDescription("Delete an issue by ID")
        .WithExample("delete", "abc123");

    config.AddCommand<CleanCommand>("clean")
        .WithDescription("Permanently remove deleted issues and create tombstone records")
        .WithExample("clean")
        .WithExample("clean", "--dry-run")
        .WithExample("clean", "--include-complete", "--include-archived");

    config.AddCommand<ShowCommand>("show")
        .WithDescription("Show all details of an issue")
        .WithExample("show", "abc123")
        .WithExample("show", "abc123", "--json");

    config.AddCommand<SearchCommand>("search")
        .WithDescription("Search issues by text")
        .WithExample("search", "login");

    config.AddCommand<DiffCommand>("diff")
        .WithDescription("Compare two JSONL issue files")
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
        .WithDescription("Print LLM instructions for issue tracking")
        .WithExample("prime")
        .WithExample("prime", "workflow")
        .WithExample("prime", "commands");

    config.AddCommand<ValidateCommand>("validate")
        .WithDescription("Validate issue dependencies for cycles")
        .WithExample("validate")
        .WithExample("validate", "--json");

    config.AddCommand<CommitCommand>("commit")
        .WithDescription("Commit fleece changes to git")
        .WithExample("commit")
        .WithExample("commit", "-m", "Add new issues")
        .WithExample("commit", "--push")
        .WithExample("commit", "--ci");

    config.AddCommand<DependencyCommand>("dependency")
        .WithDescription("Add or remove parent-child dependency between issues")
        .WithExample("dependency", "--parent", "abc123", "--child", "def456")
        .WithExample("dependency", "--parent", "abc123", "--child", "def456", "--remove")
        .WithExample("dependency", "--parent", "abc123", "--child", "def456", "--first")
        .WithExample("dependency", "--parent", "abc123", "--child", "def456", "--after", "ghi789");

    config.AddCommand<MoveCommand>("move")
        .WithDescription("Move an issue up or down among its siblings")
        .WithExample("move", "abc123", "--up")
        .WithExample("move", "abc123", "--down")
        .WithExample("move", "abc123", "--up", "--parent", "def456");

    config.AddCommand<NextCommand>("next")
        .WithDescription("Find issues that can be worked on next based on dependencies and execution mode")
        .WithExample("next")
        .WithExample("next", "--parent", "abc123")
        .WithExample("next", "--json");

    config.AddCommand<ConfigCommand>("config")
        .WithDescription("View and modify Fleece configuration settings")
        .WithExample("config", "--list")
        .WithExample("config", "--get", "autoMerge")
        .WithExample("config", "--set", "identity=John Doe")
        .WithExample("config", "--global", "--set", "autoMerge=true");

    config.AddCommand<OpenCommand>("open")
        .WithDescription("Set issue status to open")
        .WithExample("open", "abc123")
        .WithExample("open", "abc123", "def456");

    config.AddCommand<ProgressCommand>("progress")
        .WithDescription("Set issue status to progress")
        .WithExample("progress", "abc123")
        .WithExample("progress", "abc123", "def456");

    config.AddCommand<ReviewCommand>("review")
        .WithDescription("Set issue status to review")
        .WithExample("review", "abc123")
        .WithExample("review", "abc123", "def456");

    config.AddCommand<CompleteCommand>("complete")
        .WithDescription("Set issue status to complete")
        .WithExample("complete", "abc123")
        .WithExample("complete", "abc123", "def456");

    config.AddCommand<ArchivedCommand>("archived")
        .WithDescription("Set issue status to archived")
        .WithExample("archived", "abc123")
        .WithExample("archived", "abc123", "def456");

    config.AddCommand<ClosedCommand>("closed")
        .WithDescription("Set issue status to closed")
        .WithExample("closed", "abc123")
        .WithExample("closed", "abc123", "def456");
});

return await app.RunAsync(args);

// Type registrar for Spectre.Console.Cli DI integration
public sealed class TypeRegistrar(IServiceCollection services) : ITypeRegistrar
{
    private IServiceProvider? _serviceProvider;

    public ITypeResolver Build()
    {
        _serviceProvider = services.BuildServiceProvider();
        return new TypeResolver(_serviceProvider);
    }

    public IServiceProvider GetServiceProvider() =>
        _serviceProvider ?? throw new InvalidOperationException("Service provider not built yet. Call Build() first.");

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
