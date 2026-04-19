using Microsoft.Extensions.DependencyInjection;
using Spectre.Console.Cli;

namespace Fleece.Cli;

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
