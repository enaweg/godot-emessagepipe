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
/// <see cref="RegisterMessagePipe(IContainerBuilder, Action{MessagePipeOptions})"/> runs MessagePipe's
/// own <c>AddMessagePipe</c> through <see cref="ContainerBuilderProxy"/>, so its open-generic
/// publishers, subscribers and request-handler entry points resolve for every message type.
/// <see cref="RegisterMessageBroker{TMessage}"/> and friends add closed registrations on top, which
/// gives a child scope its own broker instead of falling back to its parent's.
/// </remarks>
public static class ContainerBuilderExtensions
{
    /// <summary>The provider the live <see cref="RegisterGlobalMessagePipe"/> scope handed to GlobalMessagePipe.</summary>
    static GlobalServiceProvider? globalProvider;

    /// <summary>
    /// Registers MessagePipe's shared services into the scope with default options.
    /// </summary>
    /// <param name="builder">The scope's container builder.</param>
    /// <returns>
    /// The <see cref="MessagePipeOptions"/> instance registered into the scope. Pass it to the
    /// <c>RegisterMessageBroker</c> / <c>RegisterRequestHandler</c> overloads.
    /// </returns>
    /// <exception cref="InvalidOperationException">MessagePipe is already registered on this builder or a parent scope.</exception>
    public static MessagePipeOptions RegisterMessagePipe(this IContainerBuilder builder)
    {
        return RegisterMessagePipe(builder, _ => { });
    }

    /// <summary>
    /// Registers MessagePipe's shared services into the scope, configuring the options first.
    /// </summary>
    /// <param name="builder">The scope's container builder.</param>
    /// <param name="configure">
    /// Callback applied to the options before they are registered. Global filters added here are
    /// registered too. <see cref="MessagePipeOptions.EnableAutoRegistration"/> starts out
    /// <see langword="false"/>, because it scans every loaded assembly; set it to opt in.
    /// </param>
    /// <returns>
    /// The <see cref="MessagePipeOptions"/> instance registered into the scope. Pass it to the
    /// <c>RegisterMessageBroker</c> / <c>RegisterRequestHandler</c> overloads.
    /// </returns>
    /// <exception cref="InvalidOperationException">MessagePipe is already registered on this builder or a parent scope.</exception>
    /// <remarks>
    /// A second registration in a child scope would give that scope its own brokers for every
    /// message type, silently cutting it off from messages published in its parents, so it is
    /// rejected. Use <see cref="RegisterMessageBroker{TMessage}"/> to give a child scope its own
    /// broker for a specific message type.
    /// </remarks>
    public static MessagePipeOptions RegisterMessagePipe(this IContainerBuilder builder, Action<MessagePipeOptions> configure)
    {
        if (builder.Exists(typeof(MessagePipeOptions), includeInterfaceTypes: true, findParentScopes: true))
        {
            throw new InvalidOperationException(
                "MessagePipe is already registered on this container builder or one of its parent scopes. Call "
                + "RegisterMessagePipe once, in the outermost LifetimeScope that uses MessagePipe, and pass the "
                + "MessagePipeOptions it returns to the RegisterMessageBroker and RegisterRequestHandler overloads; "
                + "RegisterMessageBroker gives a child scope its own broker for a message type.");
        }

        MessagePipeOptions? registered = null;
        builder.AsServiceCollection().AddMessagePipe(options =>
        {
            options.EnableAutoRegistration = false;
            configure(options);
            registered = options;
        });

        // Microsoft DI provides IServiceProvider itself; VContainer does not.
        builder.Register<IServiceProvider, ObjectResolverProxy>(Lifetime.Scoped);

        return registered
            ?? throw new InvalidOperationException("MessagePipe did not apply the options callback.");
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
        var services = builder.AsServiceCollection();

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
        where TKey : notnull
    {
        var lifetime = GetLifetime(options.InstanceLifetime);
        var services = builder.AsServiceCollection();

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
        where THandler : class, IRequestHandlerCore<TRequest, TResponse>
    {
        var lifetime = GetLifetime(options.RequestHandlerLifetime);
        var services = builder.AsServiceCollection();

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
        where THandler : class, IAsyncRequestHandlerCore<TRequest, TResponse>
    {
        var lifetime = GetLifetime(options.RequestHandlerLifetime);
        var services = builder.AsServiceCollection();

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
        return RegisterFilter<T>(builder);
    }

    /// <summary>Registers an async message handler filter, unless it is already registered.</summary>
    /// <typeparam name="T">The filter type.</typeparam>
    /// <param name="builder">The scope's container builder.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static IContainerBuilder RegisterAsyncMessageHandlerFilter<T>(this IContainerBuilder builder)
        where T : class, IAsyncMessageHandlerFilter
    {
        return RegisterFilter<T>(builder);
    }

    /// <summary>Registers a request handler filter, unless it is already registered.</summary>
    /// <typeparam name="T">The filter type.</typeparam>
    /// <param name="builder">The scope's container builder.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static IContainerBuilder RegisterRequestHandlerFilter<T>(this IContainerBuilder builder)
        where T : class, IRequestHandlerFilter
    {
        return RegisterFilter<T>(builder);
    }

    /// <summary>Registers an async request handler filter, unless it is already registered.</summary>
    /// <typeparam name="T">The filter type.</typeparam>
    /// <param name="builder">The scope's container builder.</param>
    /// <returns><paramref name="builder"/>, for chaining.</returns>
    public static IContainerBuilder RegisterAsyncRequestHandlerFilter<T>(this IContainerBuilder builder)
        where T : class, IAsyncRequestHandlerFilter
    {
        return RegisterFilter<T>(builder);
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
    /// <exception cref="InvalidOperationException">
    /// Another scope that called this method has not been disposed yet.
    /// </exception>
    /// <remarks>
    /// MessagePipe cannot clear its global provider. Once this scope is disposed, GlobalMessagePipe
    /// throws <see cref="InvalidOperationException"/> until another scope calls this method.
    /// </remarks>
    public static IContainerBuilder RegisterGlobalMessagePipe(this IContainerBuilder builder)
    {
        if (globalProvider is { IsReleased: false })
        {
            throw new InvalidOperationException(
                "GlobalMessagePipe is already provided by another LifetimeScope that has not been disposed. "
                + "Call RegisterGlobalMessagePipe from the root LifetimeScope only.");
        }

        GlobalServiceProvider? provider = null;
        builder.RegisterBuildCallback(resolver =>
        {
            provider = new GlobalServiceProvider(resolver);
            globalProvider = provider;
            GlobalMessagePipe.SetProvider(provider);
        });
        builder.RegisterDisposeCallback(_ => provider?.Release());
        return builder;
    }

    static IContainerBuilder RegisterFilter<T>(IContainerBuilder builder)
        where T : class
    {
        if (!builder.Exists(typeof(T), true))
        {
            builder.AsServiceCollection().AddTransient<T>();
        }

        return builder;
    }

    static ServiceLifetime GetLifetime(InstanceLifetime lifetime) => LifetimeMapping.ToServiceLifetime(lifetime);

    static void Add(this IServiceCollection services, Type type, ServiceLifetime lifetime)
    {
        services.Add(new ServiceDescriptor(type, type, lifetime));
    }

    static void Add(this IServiceCollection services, Type serviceType, Type implementationType, ServiceLifetime lifetime)
    {
        services.Add(new ServiceDescriptor(serviceType, implementationType, lifetime));
    }
}
