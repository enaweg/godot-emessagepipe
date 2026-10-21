using System.Collections.Generic;
using Godot;
using MessagePipe;
using VContainer;

namespace Enaweg.MessagePipe.Tests;

/// <summary>
/// A node that subscribes the way the README shows: injected by eContainer, handled on the main
/// thread and tied to its own lifetime.
/// </summary>
public partial class InjectedSubscriberNode : Node
{
    public List<string> Received { get; } = new();

    [Inject]
    public void Construct(ISubscriber<Ping> pings)
    {
        pings
            .Subscribe(message => Received.Add(message.Payload), new MainThreadMessageHandlerFilter<Ping> { Owner = this })
            .AddTo(this);
    }
}
