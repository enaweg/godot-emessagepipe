using System;
using Enaweg.MessagePipe.VContainer;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using VContainer;

namespace Enaweg.MessagePipe;

/// <summary>
/// Registers MessagePipe into a VContainer <see cref="IContainerBuilder"/>.
/// </summary>
/// <remarks>
/// This mirrors MessagePipe's own <c>ServiceCollectionExtensions</c>, minus the open-generic
/// registrations: VContainer resolves closed generics only, so every message type has to be
/// registered explicitly with <see cref="RegisterMessageBroker{TMessage}"/> and friends.
/// </remarks>
public static class ContainerBuilderExtensions
{
    /// <summary>
    /// Registers MessagePipe's shared services into the scope with default options.
    /// </summary>
    /// <param name="builder">The scope's container builder.</param>
    /// <returns>
    /// The <see cref="MessagePipeOptions"/> instance registered into the scope. Pass it to the
    /// <c>RegisterMessageBroker</c> / <c>RegisterRequestHandler</c> overloads.
    /// </returns>
    /// <exception cref="InvalidOperationException">MessagePipe is already registered on this builder.</exception>
    public static MessagePipeOptions RegisterMessagePipe(this IContainerBuilder builder)
    {
        return RegisterMessagePipe(builder, _ => { });
    }

    /// <summary>
    /// Registers MessagePipe's shared services into the scope, configuring the options first.
    /// </summary>
    /// <param name="builder">The scope's container builder.</param>
    /// <param name="configure">Callback applied to the options before they are registered.</param>
    /// <returns>
    /// The <see cref="MessagePipeOptions"/> instance registered into the scope. Pass it to the
    /// <c>RegisterMessageBroker</c> / <c>RegisterRequestHandler</c> overloads.
    /// </returns>
    /// <exception cref="InvalidOperationException">MessagePipe is already registered on this builder.</exception>
    public static MessagePipeOptions RegisterMessagePipe(this IContainerBuilder builder, Action<MessagePipeOptions> configure)
    {
        if (builder.Exists(typeof(MessagePipeOptions), true))
        {
            throw new InvalidOperationException(
                "MessagePipe is already registered on this container builder. Call RegisterMessagePipe once "
                + "per LifetimeScope and pass the MessagePipeOptions it returns to the RegisterMessageBroker "
                + "and RegisterRequestHandler overloads.");
        }

        var options = new MessagePipeOptions();
        configure(options);

        // Keep the options descriptor visible to later IServiceCollection-backed MessagePipe
        // extensions (for example AddInMemoryDistributedMessageBroker).
        new ContainerBuilderProxy(builder).Add(ServiceDescriptor.Singleton(options));
        builder.Register<MessagePipeDiagnosticsInfo>(Lifetime.Singleton);
        builder.Register<AttributeFilterProvider<MessageHandlerFilterAttribute>>(Lifetime.Singleton);
        builder.Register<AttributeFilterProvider<AsyncMessageHandlerFilterAttribute>>(Lifetime.Singleton);
        builder.Register<AttributeFilterProvider<RequestHandlerFilterAttribute>>(Lifetime.Singleton);
        builder.Register<AttributeFilterProvider<AsyncRequestHandlerFilterAttribute>>(Lifetime.Singleton);
        builder.Register<FilterAttachedMessageHandlerFactory>(Lifetime.Singleton);
        builder.Register<FilterAttachedAsyncMessageHandlerFactory>(Lifetime.Singleton);
        builder.Register<FilterAttachedRequestHandlerFactory>(Lifetime.Singleton);
        builder.Register<FilterAttachedAsyncRequestHandlerFactory>(Lifetime.Singleton);
        builder.Register<EventFactory>(Lifetime.Singleton);

        builder.Register<IServiceProvider, ObjectResolverProxy>(Lifetime.Scoped);

        return options;
    }

    /// <summary>
    /// Registers the keyless publisher/subscriber pair for <typeparamref name="TMessage"/>, in its
    /// plain, async, buffered and buffered-async forms.
    /// </summary>
    /// <typeparam name="TMessage">The message type to register a broker for.</typeparam>
    /// <param name="builder">The scope's container builder.</param>
    /// <param name="options">The options returned by <see cref="RegisterMessagePipe(IContainerBuilder)"/>.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static IContainerBuilder RegisterMessageBroker<TMessage>(this IContainerBuilder builder, MessagePipeOptions options)
    {
        var lifetime = GetLifetime(options.InstanceLifetime);
        var services = new ContainerBuilderProxy(builder);

        // keyless PubSub
        services.Add(typeof(MessageBrokerCore<TMessage>), lifetime);
        services.Add(typeof(IPublisher<TMessage>), typeof(MessageBroker<TMessage>), lifetime);
        services.Add(typeof(ISubscriber<TMessage>), typeof(MessageBroker<TMessage>), lifetime);

        // keyless PubSub async
        services.Add(typeof(AsyncMessageBrokerCore<TMessage>), lifetime);
        services.Add(typeof(IAsyncPublisher<TMessage>), typeof(AsyncMessageBroker<TMessage>), lifetime);
        services.Add(typeof(IAsyncSubscriber<TMessage>), typeof(AsyncMessageBroker<TMessage>), lifetime);

        // keyless buffered PubSub
        services.Add(typeof(BufferedMessageBrokerCore<TMessage>), lifetime);
        services.Add(typeof(IBufferedPublisher<TMessage>), typeof(BufferedMessageBroker<TMessage>), lifetime);
        services.Add(typeof(IBufferedSubscriber<TMessage>), typeof(BufferedMessageBroker<TMessage>), lifetime);

        // keyless buffered PubSub async
        services.Add(typeof(BufferedAsyncMessageBrokerCore<TMessage>), lifetime);
        services.Add(typeof(IBufferedAsyncPublisher<TMessage>), typeof(BufferedAsyncMessageBroker<TMessage>), lifetime);
        services.Add(typeof(IBufferedAsyncSubscriber<TMessage>), typeof(BufferedAsyncMessageBroker<TMessage>), lifetime);

        return builder;
    }

    /// <summary>
    /// Registers the keyed publisher/subscriber pair for <typeparamref name="TMessage"/>, in its
    /// plain and async forms.
    /// </summary>
    /// <typeparam name="TKey">The key messages are published and subscribed under.</typeparam>
    /// <typeparam name="TMessage">The message type to register a broker for.</typeparam>
    /// <param name="builder">The scope's container builder.</param>
    /// <param name="options">The options returned by <see cref="RegisterMessagePipe(IContainerBuilder)"/>.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static IContainerBuilder RegisterMessageBroker<TKey, TMessage>(this IContainerBuilder builder, MessagePipeOptions options)
    {
        var lifetime = GetLifetime(options.InstanceLifetime);
        var services = new ContainerBuilderProxy(builder);

        // keyed PubSub
        services.Add(typeof(MessageBrokerCore<TKey, TMessage>), lifetime);
        services.Add(typeof(IPublisher<TKey, TMessage>), typeof(MessageBroker<TKey, TMessage>), lifetime);
        services.Add(typeof(ISubscriber<TKey, TMessage>), typeof(MessageBroker<TKey, TMessage>), lifetime);

        // keyed PubSub async
        services.Add(typeof(AsyncMessageBrokerCore<TKey, TMessage>), lifetime);
        services.Add(typeof(IAsyncPublisher<TKey, TMessage>), typeof(AsyncMessageBroker<TKey, TMessage>), lifetime);
        services.Add(typeof(IAsyncSubscriber<TKey, TMessage>), typeof(AsyncMessageBroker<TKey, TMessage>), lifetime);

        return builder;
    }

    /// <summary>
    /// Registers <typeparamref name="THandler"/> as a request handler, along with the
    /// <c>IRequestHandler</c> and <c>IRequestAllHandler</c> entry points for the request type.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The concrete handler to register.</typeparam>
    /// <param name="builder">The scope's container builder.</param>
    /// <param name="options">The options returned by <see cref="RegisterMessagePipe(IContainerBuilder)"/>.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static IContainerBuilder RegisterRequestHandler<TRequest, TResponse, THandler>(this IContainerBuilder builder, MessagePipeOptions options)
        where THandler : IRequestHandler
    {
        var lifetime = GetLifetime(options.RequestHandlerLifetime);
        var services = new ContainerBuilderProxy(builder);

        services.Add(typeof(IRequestHandlerCore<TRequest, TResponse>), typeof(THandler), lifetime);
        if (!builder.Exists(typeof(IRequestHandler<TRequest, TResponse>), true))
        {
            services.Add(typeof(IRequestHandler<TRequest, TResponse>), typeof(RequestHandler<TRequest, TResponse>), lifetime);
            services.Add(typeof(IRequestAllHandler<TRequest, TResponse>), typeof(RequestAllHandler<TRequest, TResponse>), lifetime);
        }

        return builder;
    }

    /// <summary>
    /// Registers <typeparamref name="THandler"/> as an async request handler, along with the
    /// <c>IAsyncRequestHandler</c> and <c>IAsyncRequestAllHandler</c> entry points for the request type.
    /// </summary>
    /// <typeparam name="TRequest">The request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <typeparam name="THandler">The concrete handler to register.</typeparam>
    /// <param name="builder">The scope's container builder.</param>
    /// <param name="options">The options returned by <see cref="RegisterMessagePipe(IContainerBuilder)"/>.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    /// <remarks>
    /// The handler is also recorded in MessagePipe's static <see cref="AsyncRequestHandlerRegistory"/>,
    /// which is process-global and outlives any individual scope.
    /// </remarks>
    public static IContainerBuilder RegisterAsyncRequestHandler<TRequest, TResponse, THandler>(this IContainerBuilder builder, MessagePipeOptions options)
        where THandler : IAsyncRequestHandler
    {
        var lifetime = GetLifetime(options.RequestHandlerLifetime);
        var services = new ContainerBuilderProxy(builder);

        services.Add(typeof(IAsyncRequestHandlerCore<TRequest, TResponse>), typeof(THandler), lifetime);
        if (!builder.Exists(typeof(IAsyncRequestHandler<TRequest, TResponse>), true))
        {
            services.Add(typeof(IAsyncRequestHandler<TRequest, TResponse>), typeof(AsyncRequestHandler<TRequest, TResponse>), lifetime);
            services.Add(typeof(IAsyncRequestAllHandler<TRequest, TResponse>), typeof(AsyncRequestAllHandler<TRequest, TResponse>), lifetime);
        }

        AsyncRequestHandlerRegistory.Add(typeof(TRequest), typeof(TResponse), typeof(THandler));
        return builder;
    }

    /// <summary>Registers a message handler filter, unless it is already registered.</summary>
    /// <typeparam name="T">The filter type.</typeparam>
    /// <param name="builder">The scope's container builder.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static IContainerBuilder RegisterMessageHandlerFilter<T>(this IContainerBuilder builder)
        where T : class, IMessageHandlerFilter
    {
        if (!builder.Exists(typeof(T), true))
        {
            builder.Register<T>(Lifetime.Transient);
        }
        return builder;
    }

    /// <summary>Registers an async message handler filter, unless it is already registered.</summary>
    /// <typeparam name="T">The filter type.</typeparam>
    /// <param name="builder">The scope's container builder.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static IContainerBuilder RegisterAsyncMessageHandlerFilter<T>(this IContainerBuilder builder)
        where T : class, IAsyncMessageHandlerFilter
    {
        if (!builder.Exists(typeof(T), true))
        {
            builder.Register<T>(Lifetime.Transient);
        }
        return builder;
    }

    /// <summary>Registers a request handler filter, unless it is already registered.</summary>
    /// <typeparam name="T">The filter type.</typeparam>
    /// <param name="builder">The scope's container builder.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static IContainerBuilder RegisterRequestHandlerFilter<T>(this IContainerBuilder builder)
        where T : class, IRequestHandlerFilter
    {
        if (!builder.Exists(typeof(T), true))
        {
            builder.Register<T>(Lifetime.Transient);
        }
        return builder;
    }

    /// <summary>Registers an async request handler filter, unless it is already registered.</summary>
    /// <typeparam name="T">The filter type.</typeparam>
    /// <param name="builder">The scope's container builder.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static IContainerBuilder RegisterAsyncRequestHandlerFilter<T>(this IContainerBuilder builder)
        where T : class, IAsyncRequestHandlerFilter
    {
        if (!builder.Exists(typeof(T), true))
        {
            builder.Register<T>(Lifetime.Transient);
        }

        return builder;
    }

    /// <summary>
    /// Exposes the container builder as an <see cref="IServiceCollection"/> whose registrations are
    /// forwarded to VContainer.
    /// </summary>
    /// <param name="builder">The scope's container builder.</param>
    /// <returns>An <see cref="IServiceCollection"/> view over <paramref name="builder"/>.</returns>
    /// <remarks>
    /// VContainer cannot un-register, so the returned collection throws
    /// <see cref="NotSupportedException"/> from <c>Remove</c>, <c>RemoveAt</c> and <c>Clear</c>.
    /// </remarks>
    public static IServiceCollection AsServiceCollection(this IContainerBuilder builder)
    {
        return new ContainerBuilderProxy(builder);
    }

    /// <summary>
    /// Exposes the container builder as an <see cref="IMessagePipeBuilder"/>, so MessagePipe's own
    /// builder extensions register into VContainer.
    /// </summary>
    /// <param name="builder">The scope's container builder.</param>
    /// <returns>An <see cref="IMessagePipeBuilder"/> backed by <paramref name="builder"/>.</returns>
    public static IMessagePipeBuilder ToMessagePipeBuilder(this IContainerBuilder builder)
    {
        return new MessagePipeBuilder(builder.AsServiceCollection());
    }

    /// <summary>
    /// Sets the built VContainer resolver as MessagePipe's process-wide provider. Call this from
    /// the root LifetimeScope when using GlobalMessagePipe or the MessagePipe diagnostics window.
    /// </summary>
    /// <param name="builder">The root scope's container builder.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static IContainerBuilder RegisterGlobalMessagePipe(this IContainerBuilder builder)
    {
        builder.RegisterBuildCallback(resolver => GlobalMessagePipe.SetProvider(resolver.AsServiceProvider()));
        return builder;
    }

    static Lifetime GetLifetime(InstanceLifetime lifetime) => LifetimeMapping.ToVContainer(lifetime);
}
