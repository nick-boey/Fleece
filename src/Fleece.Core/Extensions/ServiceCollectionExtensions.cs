using Fleece.Core.EventSourcing.Services;
using Fleece.Core.EventSourcing.Services.Interfaces;
using Fleece.Core.Serialization;
using Fleece.Core.Services;
using Fleece.Core.Services.GraphLayout;
using Fleece.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Abstractions;
using Testably.Abstractions;

namespace Fleece.Core.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFleeceCore(
        this IServiceCollection services,
        string? basePath = null,
        IFileSystem? fileSystem = null)
    {
        var fs = fileSystem ?? new RealFileSystem();
        basePath ??= fs.Directory.GetCurrentDirectory();

        services.AddSingleton(fs);

        // Register settings service early so other services can depend on it
        services.AddSingleton<ISettingsService>(sp => new SettingsService(basePath, sp.GetRequiredService<IFileSystem>()));
        services.AddSingleton<IGitConfigService>(sp =>
            new GitConfigService(sp.GetRequiredService<ISettingsService>()));
        services.AddSingleton<IGitService>(sp => new GitService(basePath));

        // Internal infrastructure services
        services.AddSingleton<IJsonlSerializer, JsonlSerializer>();
        services.AddSingleton<ISchemaValidator, SchemaValidator>();
        services.AddSingleton<IIdGenerator, GuidIdGenerator>();

        // Event-sourced storage stack: per-issue append-only logs in .fleece/issues/.
        services.AddSingleton<IEventStore>(sp =>
            new EventStore(basePath, sp.GetRequiredService<IFileSystem>()));
        services.AddSingleton<IReplayEngine>(sp =>
            new ReplayEngine(sp.GetRequiredService<IEventStore>()));
        services.AddSingleton<IEventSourcedStorageService>(sp =>
            new EventSourcedStorageService(
                sp.GetRequiredService<IEventStore>(),
                sp.GetRequiredService<IReplayEngine>()));
        services.AddSingleton<IMigrationService>(sp =>
            new EventSourcing.Services.Legacy.MigrationService(
                basePath,
                sp.GetRequiredService<IFileSystem>()));

        // Legacy IStorageService surface, satisfied by the event-sourced adapter.
        // Reads replay every per-issue log; writes are diffed and emitted as events.
        services.AddSingleton<IStorageService>(sp =>
            new EventSourcedStorageAdapter(
                sp.GetRequiredService<IEventSourcedStorageService>(),
                sp.GetRequiredService<IGitConfigService>(),
                basePath,
                sp.GetRequiredService<IFileSystem>()));

        // SyncStatusService (internal, used by FleeceService)
        services.AddSingleton(sp =>
            new SyncStatusService(
                basePath,
                sp.GetRequiredService<IJsonlSerializer>(),
                sp.GetRequiredService<IGitService>(),
                sp.GetRequiredService<IFileSystem>()));

        // Unified service
        services.AddSingleton<IFleeceService>(sp =>
            new FleeceService(
                sp.GetRequiredService<IStorageService>(),
                sp.GetRequiredService<IIdGenerator>(),
                sp.GetRequiredService<IGitConfigService>(),
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<SyncStatusService>()));

        // Branch-lifecycle: seal archives the issue set and clears the live logs.
        services.AddSingleton<ISealService>(sp =>
            new SealService(
                sp.GetRequiredService<IFleeceService>(),
                sp.GetRequiredService<IEventStore>(),
                basePath,
                sp.GetRequiredService<IFileSystem>()));

        // Graph layout: generic engine + Fleece-specific issue adapter
        services.AddSingleton<IGraphLayoutService, GraphLayoutService>();
        services.AddSingleton<IIssueLayoutService, IssueLayoutService>();

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
    /// <param name="fileSystem">
    /// Optional filesystem abstraction. Defaults to <see cref="RealFileSystem"/> when not provided.
    /// </param>
    public static IServiceCollection AddFleeceInMemoryService(
        this IServiceCollection services,
        string? basePath = null,
        IFileSystem? fileSystem = null)
    {
        var fs = fileSystem ?? new RealFileSystem();
        basePath ??= fs.Directory.GetCurrentDirectory();

        services.AddFleeceCore(basePath, fs);

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
                basePath,
                sp.GetRequiredService<IFileSystem>()));

        return services;
    }
}
