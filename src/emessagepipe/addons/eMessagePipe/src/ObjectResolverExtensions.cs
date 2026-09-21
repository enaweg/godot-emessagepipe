using System;
using Enaweg.MessagePipe.VContainer;
using VContainer;

namespace Enaweg.MessagePipe;

/// <summary>
/// Bridges a VContainer scope to the <c>Microsoft.Extensions.DependencyInjection</c> abstractions
/// MessagePipe expects at resolve time.
/// </summary>
public static class ObjectResolverExtensions
{
    /// <summary>
    /// Wraps the resolver in an <see cref="IServiceProvider"/> that forwards lookups to VContainer.
    /// </summary>
    /// <param name="resolver">The VContainer scope to expose.</param>
    /// <returns>An <see cref="IServiceProvider"/> view over <paramref name="resolver"/>.</returns>
    public static IServiceProvider AsServiceProvider(this IObjectResolver resolver)
    {
        return new ObjectResolverProxy(resolver);
    }
}
