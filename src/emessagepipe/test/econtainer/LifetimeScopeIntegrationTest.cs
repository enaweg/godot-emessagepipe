using System.Collections.Generic;
using Enaweg.Container.Godot;
using GdUnit4;
using MessagePipe;
using VContainer;
using static GdUnit4.Assertions;

namespace Enaweg.MessagePipe.Tests;

/// <summary>
/// Covers MessagePipe registered in real eContainer <see cref="LifetimeScope"/> nodes, whose
/// containers nest along the scene tree under the eContainer autoload.
/// </summary>
[TestSuite]
[RequireGodotRuntime]
public class LifetimeScopeIntegrationTest
{
    /// <summary>A child scope node shares its parent's brokers and can add request handlers of its own.</summary>
    [TestCase]
    public void ChildScope_SharesItsParentsBrokers()
    {
        MessagePipeOptions? options = null;
        var parent = LifetimeScope.Create(builder => options = builder.RegisterMessagePipe());
        try
        {
            var child = parent.CreateChild(builder => builder.RegisterRequestHandler<Ping, int, PingHandler>(options!));

            var received = new List<string>();
            using var subscription = child.Container.Resolve<ISubscriber<Ping>>()
                .Subscribe(message => received.Add(message.Payload));
            parent.Container.Resolve<IPublisher<Ping>>().Publish(new Ping("from-parent"));

            AssertThat(received).ContainsExactly("from-parent");
            AssertThat(child.Container.Resolve<IRequestHandler<Ping, int>>().Invoke(new Ping("abc"))).IsEqual(3);
        }
        finally
        {
            parent.Free();
        }
    }

    /// <summary>A child scope node that registers its own broker no longer hears its parent's messages.</summary>
    [TestCase]
    public void ChildScope_WithItsOwnBrokerIsIsolated()
    {
        MessagePipeOptions? options = null;
        var parent = LifetimeScope.Create(builder => options = builder.RegisterMessagePipe());
        try
        {
            var child = parent.CreateChild(builder => builder.RegisterMessageBroker<Ping>(options!));

            var received = new List<string>();
            using var subscription = child.Container.Resolve<ISubscriber<Ping>>()
                .Subscribe(message => received.Add(message.Payload));
            parent.Container.Resolve<IPublisher<Ping>>().Publish(new Ping("from-parent"));
            child.Container.Resolve<IPublisher<Ping>>().Publish(new Ping("from-child"));

            AssertThat(received).ContainsExactly("from-child");
        }
        finally
        {
            parent.Free();
        }
    }

    /// <summary>A subscription added to a scope node with <c>AddTo</c> ends when that node is freed.</summary>
    [TestCase]
    public void AddTo_EndsTheSubscriptionWhenTheScopeNodeIsFreed()
    {
        var parent = LifetimeScope.Create(builder => builder.RegisterMessagePipe());
        try
        {
            var child = parent.CreateChild(_ => { });

            var received = new List<string>();
            child.Container.Resolve<ISubscriber<Ping>>()
                .Subscribe(message => received.Add(message.Payload))
                .AddTo(child);
            var publisher = parent.Container.Resolve<IPublisher<Ping>>();

            publisher.Publish(new Ping("before"));
            child.Free();
            publisher.Publish(new Ping("after"));

            AssertThat(received).ContainsExactly("before");
        }
        finally
        {
            parent.Free();
        }
    }
}
