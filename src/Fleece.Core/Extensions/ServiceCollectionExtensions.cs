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

        services.AddSingleton<IJsonlSerializer, JsonlSerializer>();
        services.AddSingleton<IIdGenerator, Sha256IdGenerator>();
        services.AddSingleton<IStorageService>(sp =>
            new JsonlStorageService(basePath, sp.GetRequiredService<IJsonlSerializer>()));
        services.AddSingleton<IGitConfigService, GitConfigService>();
        services.AddSingleton<IChangeService, ChangeService>();
        services.AddSingleton<IIssueService, IssueService>();
        services.AddSingleton<IMergeService, MergeService>();
        services.AddSingleton<IMigrationService, MigrationService>();
        services.AddSingleton<IValidationService, ValidationService>();

        return services;
    }
}
