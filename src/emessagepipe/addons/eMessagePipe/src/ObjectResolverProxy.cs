using System;
using VContainer;

namespace Enaweg.MessagePipe.VContainer;

/// <summary>
/// Exposes a VContainer <see cref="IObjectResolver"/> as the <see cref="IServiceProvider"/> that
/// MessagePipe resolves handler filters and request handlers through.
/// </summary>
public sealed class ObjectResolverProxy : IServiceProvider
{
    readonly IObjectResolver resolver;

    /// <summary>Creates a provider backed by the given VContainer scope.</summary>
    /// <param name="resolver">The resolver every lookup is delegated to.</param>
    public ObjectResolverProxy(IObjectResolver resolver)
    {
        this.resolver = resolver;
    }

    /// <summary>Resolves a service from the underlying VContainer scope.</summary>
    /// <param name="serviceType">The contract to resolve.</param>
    /// <returns>
    /// The resolved service, or <see langword="null"/> when <paramref name="serviceType"/> is not
    /// registered — as <see cref="IServiceProvider"/> requires, so that callers using
    /// <c>GetRequiredService</c> report the missing service themselves.
    /// </returns>
    public object? GetService(Type serviceType)
    {
        return resolver.TryResolve(serviceType, out var service) ? service : null;
    }
}
