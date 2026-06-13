using Fleece.Core.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Fleece.GitHub;

/// <summary>
/// DI registration for the OctoKit-backed GitHub integration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the OctoKit-backed <see cref="IGitHubService"/>. Call after the core services
    /// are registered so that <see cref="ISettingsService"/> and <see cref="IGitService"/> resolve.
    /// </summary>
    public static IServiceCollection AddFleeceGitHub(this IServiceCollection services)
    {
        services.AddSingleton<IGitHubService>(sp =>
            new GitHubService(
                sp.GetRequiredService<ISettingsService>(),
                sp.GetRequiredService<IGitService>()));
        return services;
    }
}
