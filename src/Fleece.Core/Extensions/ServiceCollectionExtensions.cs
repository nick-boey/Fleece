using Fleece.Core.Serialization;
using Fleece.Core.Services;
using Fleece.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Fleece.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFleeceCore(this IServiceCollection services, string? basePath = null)
    {
        basePath ??= Directory.GetCurrentDirectory();

        // Register settings service early so other services can depend on it
        services.AddSingleton<ISettingsService>(sp => new SettingsService(basePath));
        services.AddSingleton<IGitConfigService>(sp =>
            new GitConfigService(sp.GetRequiredService<ISettingsService>()));
        services.AddSingleton<IGitService>(sp => new GitService(basePath));

        // Internal infrastructure services
        services.AddSingleton<IJsonlSerializer, JsonlSerializer>();
        services.AddSingleton<ISchemaValidator, SchemaValidator>();
        services.AddSingleton<IIdGenerator, GuidIdGenerator>();
        services.AddSingleton<IStorageService>(sp =>
            new JsonlStorageService(
                basePath,
                sp.GetRequiredService<IJsonlSerializer>(),
                sp.GetRequiredService<ISchemaValidator>()));

        // DiffService (standalone utility for file comparison)
        services.AddSingleton<IDiffService, DiffService>();

        // SyncStatusService (internal, used by FleeceService)
        services.AddSingleton(sp =>
            new SyncStatusService(
                basePath,
                sp.GetRequiredService<IJsonlSerializer>(),
                sp.GetRequiredService<IGitService>()));

        // Unified service
        services.AddSingleton<IFleeceService>(sp =>
            new FleeceService(
                sp.GetRequiredService<IStorageService>(),
                sp.GetRequiredService<IIdGenerator>(),
                sp.GetRequiredService<IGitConfigService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<SyncStatusService>()));

        return services;
    }

    /// <summary>
    /// Registers the Fleece in-memory cached issue service along with all core services.
    /// The in-memory service provides fast reads from a ConcurrentDictionary cache,
    /// queues writes for asynchronous persistence, and watches for external file changes.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="basePath">
    /// The project base path containing the <c>.fleece/</c> directory.
    /// Defaults to the current working directory if not specified.
    /// </param>
    public static IServiceCollection AddFleeceInMemoryService(this IServiceCollection services, string? basePath = null)
    {
        basePath ??= Directory.GetCurrentDirectory();

        services.AddFleeceCore(basePath);

        services.AddSingleton<IssueSerializationQueueService>();
        services.AddSingleton<IIssueSerializationQueue>(sp =>
        {
            var queue = sp.GetRequiredService<IssueSerializationQueueService>();
            queue.StartProcessing();
            return queue;
        });
        services.AddSingleton<IFleeceInMemoryService>(sp =>
            new FleeceInMemoryService(
                sp.GetRequiredService<IFleeceService>(),
                sp.GetRequiredService<IIssueSerializationQueue>(),
                basePath));

        return services;
    }
}
