using Enaweg.Container.Godot;
using MessagePipe;
using VContainer;

namespace Enaweg.MessagePipe.Tests;

/// <summary>
/// A child scope that owns its <see cref="Ping"/> broker the way the README shows: it takes the
/// options from its parent's container.
/// </summary>
public partial class BrokerOwningLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        var options = Parent.Container.Resolve<MessagePipeOptions>();
        builder.RegisterMessageBroker<Ping>(options);
    }
}
