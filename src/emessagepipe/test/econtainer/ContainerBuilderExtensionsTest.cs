using System;
using System.Collections.Generic;
using GdUnit4;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.MessagePipe.Tests;

/// <summary>
/// Covers <see cref="ContainerBuilderExtensions"/> — the registration surface eMessagePipe adds to
/// VContainer. The assertions target what these extensions decide (which contracts get registered,
/// under which lifetime, and how often), not MessagePipe's own publish/subscribe semantics.
/// </summary>
[TestSuite]
public class ContainerBuilderExtensionsTest
{
    /// <summary>Every shared service <c>RegisterMessagePipe</c> is responsible for has to resolve.</summary>
    [TestCase]
    public void RegisterMessagePipe_RegistersSharedServices()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe();

        using var container = builder.Build();

        AssertThat(container.Resolve<MessagePipeOptions>()).IsSame(options);
        AssertThat(container.Resolve<MessagePipeDiagnosticsInfo>()).IsNotNull();
        AssertThat(container.Resolve<EventFactory>()).IsNotNull();
        AssertThat(container.Resolve<FilterAttachedMessageHandlerFactory>()).IsNotNull();
        AssertThat(container.Resolve<FilterAttachedAsyncMessageHandlerFactory>()).IsNotNull();
        AssertThat(container.Resolve<FilterAttachedRequestHandlerFactory>()).IsNotNull();
        AssertThat(container.Resolve<FilterAttachedAsyncRequestHandlerFactory>()).IsNotNull();
        AssertThat(container.Resolve<AttributeFilterProvider<MessageHandlerFilterAttribute>>()).IsNotNull();
        AssertThat(container.Resolve<AttributeFilterProvider<AsyncMessageHandlerFilterAttribute>>()).IsNotNull();
        AssertThat(container.Resolve<AttributeFilterProvider<RequestHandlerFilterAttribute>>()).IsNotNull();
        AssertThat(container.Resolve<AttributeFilterProvider<AsyncRequestHandlerFilterAttribute>>()).IsNotNull();
        AssertThat(container.Resolve<IServiceProvider>()).IsNotNull();
    }

    /// <summary>The configured options object is the one registered into the scope.</summary>
    [TestCase]
    public void RegisterMessagePipe_AppliesAndRegistersConfiguredOptions()
    {
        var builder = new ContainerBuilder();

        var options = builder.RegisterMessagePipe(o => o.InstanceLifetime = InstanceLifetime.Transient);

        using var container = builder.Build();

        AssertThat(options.InstanceLifetime).IsEqual(InstanceLifetime.Transient);
        AssertThat(container.Resolve<MessagePipeOptions>()).IsSame(options);
    }

    /// <summary>
    /// A global filter added through the options has to be registered as well, or MessagePipe fails
    /// to resolve it on the first publish.
    /// </summary>
    [TestCase]
    public void RegisterMessagePipe_RegistersGlobalFilters()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe(o => o.AddGlobalMessageHandlerFilter<PingMarkingFilter>());
        builder.RegisterMessageBroker<Ping>(options);

        using var container = builder.Build();

        var received = new List<string>();
        using IDisposable subscription = container.Resolve<ISubscriber<Ping>>()
            .Subscribe(message => received.Add(message.Payload));

        container.Resolve<IPublisher<Ping>>().Publish(new Ping("hello"));

        AssertThat(received).ContainsExactly("hello!");
    }

    /// <summary>MessagePipe's open-generic brokers resolve without a per-message registration.</summary>
    [TestCase]
    public void RegisterMessagePipe_ResolvesBrokersForUnregisteredMessageTypes()
    {
        var builder = new ContainerBuilder();
        builder.RegisterMessagePipe();

        using var container = builder.Build();

        var received = new List<string>();
        using IDisposable keyless = container.Resolve<ISubscriber<string>>()
            .Subscribe(message => received.Add(message));
        using IDisposable keyed = container.Resolve<ISubscriber<int, string>>()
            .Subscribe(1, message => received.Add(message));

        container.Resolve<IPublisher<string>>().Publish("keyless");
        container.Resolve<IPublisher<int, string>>().Publish(1, "keyed");

        AssertThat(received).ContainsExactly("keyless", "keyed");
        AssertThat(container.Resolve<IBufferedPublisher<string>>()).IsNotNull();
        AssertThat(container.Resolve<IAsyncPublisher<string>>()).IsNotNull();
    }

    /// <summary>
    /// Auto-registration scans every loaded assembly, so it stays off unless the caller turns it on.
    /// </summary>
    [TestCase]
    public void RegisterMessagePipe_LeavesAutoRegistrationOffByDefault()
    {
        var builder = new ContainerBuilder();

        var options = builder.RegisterMessagePipe();
        var optedIn = new ContainerBuilder().RegisterMessagePipe(o => o.EnableAutoRegistration = true);

        AssertThat(options.EnableAutoRegistration).IsFalse();
        AssertThat(optedIn.EnableAutoRegistration).IsTrue();
    }

    /// <summary>
    /// Registering twice used to fail deep inside <c>Build()</c> with a conflicting-registration
    /// error; it now fails at the call site with an actionable message.
    /// </summary>
    [TestCase]
    public void RegisterMessagePipe_ThrowsWhenCalledTwice()
    {
        var builder = new ContainerBuilder();
        builder.RegisterMessagePipe();

        AssertThrown(() => builder.RegisterMessagePipe()).IsInstanceOf<InvalidOperationException>();
    }

    /// <summary>
    /// A second registration in a child scope would give it its own brokers for every message type,
    /// cutting it off from its parent, so it is rejected as well.
    /// </summary>
    [TestCase]
    public void RegisterMessagePipe_ThrowsInAChildOfARegisteredScope()
    {
        var builder = new ContainerBuilder();
        builder.RegisterMessagePipe();

        using var container = builder.Build();

        AssertThrown(() => container.CreateScope(child => child.RegisterMessagePipe()))
            .IsInstanceOf<InvalidOperationException>();
    }

    /// <summary>All eight keyless broker contracts have to be registered.</summary>
    [TestCase]
    public void RegisterMessageBroker_RegistersEveryKeylessContract()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<string>(options);

        using var container = builder.Build();

        AssertThat(container.Resolve<IPublisher<string>>()).IsNotNull();
        AssertThat(container.Resolve<ISubscriber<string>>()).IsNotNull();
        AssertThat(container.Resolve<IAsyncPublisher<string>>()).IsNotNull();
        AssertThat(container.Resolve<IAsyncSubscriber<string>>()).IsNotNull();
        AssertThat(container.Resolve<IBufferedPublisher<string>>()).IsNotNull();
        AssertThat(container.Resolve<IBufferedSubscriber<string>>()).IsNotNull();
        AssertThat(container.Resolve<IBufferedAsyncPublisher<string>>()).IsNotNull();
        AssertThat(container.Resolve<IBufferedAsyncSubscriber<string>>()).IsNotNull();
    }

    /// <summary>
    /// Broker registrations go through the service-collection facade, so <c>TryAdd</c>-style helpers
    /// called afterwards see them instead of registering a second broker.
    /// </summary>
    [TestCase]
    public void RegisterMessageBroker_IsVisibleToTheServiceCollection()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<string>(options);

        var publishers = new List<ServiceDescriptor>(builder.AsServiceCollection())
            .FindAll(descriptor => descriptor.ServiceType == typeof(IPublisher<string>));

        AssertThat(publishers.Count).IsEqual(1);
    }

    /// <summary>
    /// Publisher and subscriber must share one broker core — the registration detail that a wrong
    /// lifetime or a duplicate core registration would break.
    /// </summary>
    [TestCase]
    public void RegisterMessageBroker_WiresPublisherAndSubscriberToOneCore()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<string>(options);

        using var container = builder.Build();

        var received = new List<string>();
        using IDisposable subscription = container.Resolve<ISubscriber<string>>()
            .Subscribe(message => received.Add(message));

        container.Resolve<IPublisher<string>>().Publish("hello");

        AssertThat(received).Contains("hello");
    }

    /// <summary>
    /// A child scope that registers a broker gets its own; one that does not shares its parent's.
    /// </summary>
    [TestCase]
    public void RegisterMessageBroker_GivesAChildScopeItsOwnBroker()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe();

        using var container = builder.Build();
        using var sharing = container.CreateScope(_ => { });
        using var owning = container.CreateScope(child => child.RegisterMessageBroker<string>(options));

        var sharingReceived = new List<string>();
        var owningReceived = new List<string>();
        using IDisposable sharingSubscription = sharing.Resolve<ISubscriber<string>>()
            .Subscribe(message => sharingReceived.Add(message));
        using IDisposable owningSubscription = owning.Resolve<ISubscriber<string>>()
            .Subscribe(message => owningReceived.Add(message));

        container.Resolve<IPublisher<string>>().Publish("from-parent");

        AssertThat(sharingReceived).ContainsExactly("from-parent");
        AssertThat(owningReceived).IsEmpty();
    }

    /// <summary>All six keyed broker contracts have to be registered.</summary>
    [TestCase]
    public void RegisterMessageBroker_RegistersEveryKeyedContract()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<string, string>(options);

        using var container = builder.Build();

        AssertThat(container.Resolve<IPublisher<string, string>>()).IsNotNull();
        AssertThat(container.Resolve<ISubscriber<string, string>>()).IsNotNull();
        AssertThat(container.Resolve<IAsyncPublisher<string, string>>()).IsNotNull();
        AssertThat(container.Resolve<IAsyncSubscriber<string, string>>()).IsNotNull();
        AssertThat(container.Resolve<MessageBrokerCore<string, string>>()).IsNotNull();
        AssertThat(container.Resolve<AsyncMessageBrokerCore<string, string>>()).IsNotNull();
    }

    /// <summary>The keyed publisher and subscriber must also share one broker core.</summary>
    [TestCase]
    public void RegisterMessageBroker_WiresKeyedPublisherAndSubscriberToOneCore()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<string, string>(options);

        using var container = builder.Build();

        var received = new List<string>();
        using IDisposable subscription = container.Resolve<ISubscriber<string, string>>()
            .Subscribe("key-a", message => received.Add(message));

        container.Resolve<IPublisher<string, string>>().Publish("key-a", "delivered");

        AssertThat(received).Contains("delivered");
    }

    /// <summary><c>options.InstanceLifetime</c> has to reach VContainer through the lifetime mapping.</summary>
    [TestCase]
    public void RegisterMessageBroker_HonoursSingletonInstanceLifetime()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe(o => o.InstanceLifetime = InstanceLifetime.Singleton);
        builder.RegisterMessageBroker<string>(options);

        using var container = builder.Build();

        AssertThat(container.Resolve<MessageBrokerCore<string>>())
            .IsSame(container.Resolve<MessageBrokerCore<string>>());
    }

    /// <inheritdoc cref="RegisterMessageBroker_HonoursSingletonInstanceLifetime" />
    [TestCase]
    public void RegisterMessageBroker_HonoursTransientInstanceLifetime()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe(o => o.InstanceLifetime = InstanceLifetime.Transient);
        builder.RegisterMessageBroker<string>(options);

        using var container = builder.Build();

        AssertThat(container.Resolve<MessageBrokerCore<string>>())
            .IsNotSame(container.Resolve<MessageBrokerCore<string>>());
    }

    /// <summary>Registering a handler also has to register the two entry points for its request type.</summary>
    [TestCase]
    public void RegisterRequestHandler_RegistersHandlerAndEntryPoints()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe();
        builder.RegisterRequestHandler<Ping, int, PingHandler>(options);

        using var container = builder.Build();

        AssertThat(container.Resolve<IRequestHandlerCore<Ping, int>>()).IsNotNull();
        AssertThat(container.Resolve<IRequestHandler<Ping, int>>().Invoke(new Ping("abcd"))).IsEqual(4);
        AssertThat(container.Resolve<IRequestAllHandler<Ping, int>>()).IsNotNull();
    }

    /// <summary>
    /// A second handler for the same request type adds another core but must not re-register the
    /// shared entry points — the <c>Exists</c> guard in the extension.
    /// </summary>
    [TestCase]
    public void RegisterRequestHandler_RegistersSecondHandlerWithoutDuplicatingEntryPoints()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe();
        builder.RegisterRequestHandler<Ping, int, PingHandler>(options);
        builder.RegisterRequestHandler<Ping, int, NegatedPingHandler>(options);

        using var container = builder.Build();

        var responses = container.Resolve<IRequestAllHandler<Ping, int>>().InvokeAll(new Ping("abcd"));

        AssertThat(responses.Length).IsEqual(2);
        AssertThat(new List<int>(responses)).ContainsExactlyInAnyOrder(4, -4);
    }

    /// <summary>The async counterpart of <see cref="RegisterRequestHandler_RegistersHandlerAndEntryPoints" />.</summary>
    [TestCase]
    public void RegisterAsyncRequestHandler_RegistersHandlerAndEntryPoints()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe();
        builder.RegisterAsyncRequestHandler<Ping, int, AsyncPingHandler>(options);

        using var container = builder.Build();

        AssertThat(container.Resolve<IAsyncRequestHandlerCore<Ping, int>>()).IsNotNull();
        AssertThat(container.Resolve<IAsyncRequestAllHandler<Ping, int>>()).IsNotNull();

        var response = container.Resolve<IAsyncRequestHandler<Ping, int>>()
            .InvokeAsync(new Ping("abcd"))
            .GetAwaiter()
            .GetResult();

        AssertThat(response).IsEqual(4);
    }

    /// <summary>The async counterpart of <see cref="RegisterRequestHandler_RegistersSecondHandlerWithoutDuplicatingEntryPoints" />.</summary>
    [TestCase]
    public void RegisterAsyncRequestHandler_RegistersSecondHandlerWithoutDuplicatingEntryPoints()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe();
        builder.RegisterAsyncRequestHandler<Ping, int, AsyncPingHandler>(options);
        builder.RegisterAsyncRequestHandler<Ping, int, NegatedAsyncPingHandler>(options);

        using var container = builder.Build();

        var responses = container.Resolve<IAsyncRequestAllHandler<Ping, int>>()
            .InvokeAllAsync(new Ping("abcd"))
            .GetAwaiter()
            .GetResult();

        AssertThat(new List<int>(responses)).ContainsExactlyInAnyOrder(4, -4);
    }

    /// <summary><c>options.RequestHandlerLifetime</c> has to reach VContainer through the lifetime mapping.</summary>
    [TestCase]
    public void RegisterRequestHandler_HonoursTransientRequestHandlerLifetime()
    {
        var builder = new ContainerBuilder();
        var options = builder.RegisterMessagePipe(o => o.RequestHandlerLifetime = InstanceLifetime.Transient);
        builder.RegisterRequestHandler<Ping, int, PingHandler>(options);

        using var container = builder.Build();

        AssertThat(container.Resolve<IRequestHandlerCore<Ping, int>>())
            .IsNotSame(container.Resolve<IRequestHandlerCore<Ping, int>>());
    }

    /// <summary>Each filter extension registers its filter as transient.</summary>
    [TestCase]
    public void RegisterFilters_RegisterEachFilterAsTransient()
    {
        var builder = new ContainerBuilder();
        builder.RegisterMessagePipe();
        builder.RegisterMessageHandlerFilter<PingMessageFilter>();
        builder.RegisterAsyncMessageHandlerFilter<AsyncPingMessageFilter>();
        builder.RegisterRequestHandlerFilter<PingRequestFilter>();
        builder.RegisterAsyncRequestHandlerFilter<AsyncPingRequestFilter>();

        using var container = builder.Build();

        AssertThat(container.Resolve<PingMessageFilter>()).IsNotSame(container.Resolve<PingMessageFilter>());
        AssertThat(container.Resolve<AsyncPingMessageFilter>()).IsNotNull();
        AssertThat(container.Resolve<PingRequestFilter>()).IsNotNull();
        AssertThat(container.Resolve<AsyncPingRequestFilter>()).IsNotNull();
    }

    /// <summary>
    /// The filter extensions guard on <c>Exists</c>, so registering the same filter twice must not
    /// produce a conflicting registration.
    /// </summary>
    [TestCase]
    public void RegisterFilters_AreIdempotent()
    {
        var builder = new ContainerBuilder();
        builder.RegisterMessagePipe();
        builder.RegisterMessageHandlerFilter<PingMessageFilter>();
        builder.RegisterMessageHandlerFilter<PingMessageFilter>();

        using var container = builder.Build();

        AssertThat(container.Resolve<PingMessageFilter>()).IsNotNull();
    }

    /// <summary>
    /// MessagePipe's own builder extensions register through <see cref="System.IServiceProvider"/>
    /// abstractions, so they only work if eMessagePipe's facade forwards to VContainer.
    /// </summary>
    [TestCase]
    public void ToMessagePipeBuilder_ForwardsRegistrationsToVContainer()
    {
        var builder = new ContainerBuilder();
        builder.RegisterMessagePipe();
        builder.ToMessagePipeBuilder().AddMessageHandlerFilter<PingMessageFilter>();

        using var container = builder.Build();

        AssertThat(container.Resolve<PingMessageFilter>()).IsNotNull();
    }

    /// <summary>The root scope's container becomes GlobalMessagePipe's provider once it is built.</summary>
    [TestCase]
    public void RegisterGlobalMessagePipe_ProvidesGlobalMessagePipe()
    {
        var builder = new ContainerBuilder();
        builder.RegisterMessagePipe();
        builder.RegisterGlobalMessagePipe();

        using var container = builder.Build();

        var received = new List<string>();
        using IDisposable subscription = GlobalMessagePipe.GetSubscriber<Ping>()
            .Subscribe(message => received.Add(message.Payload));
        container.Resolve<IPublisher<Ping>>().Publish(new Ping("global"));

        AssertThat(received).ContainsExactly("global");
    }

    /// <summary>
    /// GlobalMessagePipe cannot be cleared, so after its scope is disposed it has to fail with a
    /// clear message instead of resolving from a disposed container.
    /// </summary>
    [TestCase]
    public void RegisterGlobalMessagePipe_FailsClearlyOnceTheScopeIsDisposed()
    {
        var builder = new ContainerBuilder();
        builder.RegisterMessagePipe();
        builder.RegisterGlobalMessagePipe();

        builder.Build().Dispose();

        AssertThrown(() => GlobalMessagePipe.GetPublisher<Ping>())
            .IsInstanceOf<InvalidOperationException>()
            .StartsWithMessage("The LifetimeScope that called RegisterGlobalMessagePipe has been disposed");
    }

    /// <summary>
    /// Only one live scope may provide GlobalMessagePipe; another may take over once it is disposed.
    /// </summary>
    [TestCase]
    public void RegisterGlobalMessagePipe_RejectsASecondLiveScope()
    {
        var first = new ContainerBuilder();
        first.RegisterMessagePipe();
        first.RegisterGlobalMessagePipe();

        using (first.Build())
        {
            var second = new ContainerBuilder();
            second.RegisterMessagePipe();

            AssertThrown(() => second.RegisterGlobalMessagePipe()).IsInstanceOf<InvalidOperationException>();
        }

        var next = new ContainerBuilder();
        next.RegisterMessagePipe();
        next.RegisterGlobalMessagePipe();

        using var container = next.Build();

        AssertThat(GlobalMessagePipe.GetPublisher<Ping>()).IsNotNull();
    }
}
