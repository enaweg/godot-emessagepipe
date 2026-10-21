using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using VContainer;

namespace Enaweg.MessagePipe.VContainer;

/// <summary>
/// Translates MessagePipe's lifetimes into <c>Microsoft.Extensions.DependencyInjection</c>'s, and
/// those into VContainer's <see cref="Lifetime"/>.
/// </summary>
internal static class LifetimeMapping
{
    /// <summary>
    /// Maps a MessagePipe <see cref="InstanceLifetime"/> to the equivalent service-collection lifetime.
    /// </summary>
    /// <param name="lifetime">The MessagePipe lifetime to translate.</param>
    /// <returns>The matching <see cref="ServiceLifetime"/>; <see cref="ServiceLifetime.Transient"/> for unknown values.</returns>
    internal static ServiceLifetime ToServiceLifetime(InstanceLifetime lifetime) => lifetime switch
    {
        InstanceLifetime.Singleton => ServiceLifetime.Singleton,
        InstanceLifetime.Scoped => ServiceLifetime.Scoped,
        _ => ServiceLifetime.Transient,
    };

    /// <summary>
    /// Maps a <see cref="ServiceLifetime"/> to the equivalent VContainer lifetime.
    /// </summary>
    /// <param name="lifetime">The service-collection lifetime to translate.</param>
    /// <returns>The matching <see cref="Lifetime"/>; <see cref="Lifetime.Transient"/> for unknown values.</returns>
    internal static Lifetime ToVContainer(ServiceLifetime lifetime) => lifetime switch
    {
        ServiceLifetime.Singleton => Lifetime.Singleton,
        ServiceLifetime.Scoped => Lifetime.Scoped,
        _ => Lifetime.Transient,
    };
}
