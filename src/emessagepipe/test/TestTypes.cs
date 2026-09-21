using System;
using System.Threading;
using System.Threading.Tasks;
using MessagePipe;

namespace Enaweg.MessagePipe.Tests;

/// <summary>A message type used only by these tests.</summary>
public sealed class Ping
{
	public Ping(string payload) => Payload = payload;

	public string Payload { get; }
}

/// <summary>A request handler used only by these tests.</summary>
public sealed class PingHandler : IRequestHandler<Ping, int>
{
	public int Invoke(Ping request) => request.Payload.Length;
}

/// <summary>A second handler for the same request type, used to exercise "register all" behaviour.</summary>
public sealed class NegatedPingHandler : IRequestHandler<Ping, int>
{
	public int Invoke(Ping request) => -request.Payload.Length;
}

/// <summary>An async request handler used only by these tests.</summary>
public sealed class AsyncPingHandler : IAsyncRequestHandler<Ping, int>
{
	public ValueTask<int> InvokeAsync(Ping request, CancellationToken cancellationToken = default)
		=> new(request.Payload.Length);
}

/// <summary>A message handler filter used only by these tests.</summary>
public sealed class PingMessageFilter : MessageHandlerFilter<Ping>
{
	public override void Handle(Ping message, Action<Ping> next) => next(message);
}

/// <summary>An async message handler filter used only by these tests.</summary>
public sealed class AsyncPingMessageFilter : AsyncMessageHandlerFilter<Ping>
{
	public override ValueTask HandleAsync(
		Ping message,
		CancellationToken cancellationToken,
		Func<Ping, CancellationToken, ValueTask> next)
		=> next(message, cancellationToken);
}

/// <summary>A request handler filter used only by these tests.</summary>
public sealed class PingRequestFilter : RequestHandlerFilter<Ping, int>
{
	public override int Invoke(Ping request, Func<Ping, int> next) => next(request);
}

/// <summary>An async request handler filter used only by these tests.</summary>
public sealed class AsyncPingRequestFilter : AsyncRequestHandlerFilter<Ping, int>
{
	public override ValueTask<int> InvokeAsync(
		Ping request,
		CancellationToken cancellationToken,
		Func<Ping, CancellationToken, ValueTask<int>> next)
		=> next(request, cancellationToken);
}

/// <summary>A plain service used to exercise the service-collection and service-provider bridges.</summary>
public sealed class Recorder
{
}

/// <summary>Contract used to check that a descriptor's service type, not its implementation type, is registered.</summary>
public interface IRecorder
{
}

/// <summary>Implementation behind <see cref="IRecorder"/>.</summary>
public sealed class RecorderImplementation : IRecorder
{
}
