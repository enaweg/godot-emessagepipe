using System;
using VContainer;

namespace Enaweg.MessagePipe.VContainer;

/// <summary>
/// The provider <see cref="ContainerBuilderExtensions.RegisterGlobalMessagePipe"/> hands to
/// MessagePipe's <c>GlobalMessagePipe</c>, which can only be replaced, never cleared.
/// </summary>
/// <remarks>
/// Once the scope that registered it is disposed, lookups fail with a clear message instead of
/// reaching into a disposed container.
/// </remarks>
internal sealed class GlobalServiceProvider : IServiceProvider
{
    IObjectResolver? resolver;

    public GlobalServiceProvider(IObjectResolver resolver)
    {
        this.resolver = resolver;
    }

    /// <summary>Whether the scope behind this provider has been disposed.</summary>
    public bool IsReleased => resolver is null;

    /// <summary>Detaches the provider from its disposed scope.</summary>
    public void Release() => resolver = null;

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">The scope behind this provider has been disposed.</exception>
    public object? GetService(Type serviceType)
    {
        var current = resolver
            ?? throw new InvalidOperationException(
                "The LifetimeScope that called RegisterGlobalMessagePipe has been disposed, so GlobalMessagePipe "
                + "has no container until another scope calls RegisterGlobalMessagePipe.");

        return current.TryResolve(serviceType, out var service) ? service : null;
    }
}
