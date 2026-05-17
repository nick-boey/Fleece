using Spectre.Console.Cli;

namespace Fleece.Cli.Interceptors;

/// <summary>
/// Chains multiple <see cref="ICommandInterceptor"/> instances, executing each in order.
/// </summary>
public sealed class CompositeCommandInterceptor(params ICommandInterceptor[] interceptors) : ICommandInterceptor
{
    public void Intercept(CommandContext context, CommandSettings settings)
    {
        foreach (var interceptor in interceptors)
        {
            interceptor.Intercept(context, settings);
        }
    }
}
