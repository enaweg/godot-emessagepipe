<div align="center">

<img src="./design/featured.webp" alt="eMessagePipe logo" width="50%"/>

# eMessagePipe

**[MessagePipe](https://github.com/Cysharp/MessagePipe) integration for [Godot](https://godotengine.org/) .NET projects
using [eContainer](https://github.com/enaweg/godot-econtainer).**

[![CI](https://github.com/enaweg/godot-emessagepipe/actions/workflows/ci-pr.yml/badge.svg)](https://github.com/enaweg/godot-emessagepipe/actions/workflows/ci-pr.yml)
![Godot 4.7.2](https://img.shields.io/badge/Godot-v4.7.2-202020?logo=godot-engine&logoColor=blue&color=darkgreen&labelColor=202020)

![Dotnet 8](https://img.shields.io/badge/8-02020?logo=dotnet&logoSize=auto&logoColor=purple&color=darkgreen&labelColor=E0E0E0)

**NOTE**: This project is experimental and still a work in progress.

</div>

## What is MessagePipe?

[MessagePipe](https://github.com/Cysharp/MessagePipe) is Cysharp's high-performance in-memory/distributed messaging
library for .NET and Unity. Instead of wiring senders directly to receivers (or to Godot signals), a component asks the
DI container for a typed publisher or subscriber and talks to a *message type*. Publisher and subscriber never learn
about each other, which keeps scenes, services and systems decoupled and independently testable.

eMessagePipe integrates MessagePipe 1.8.2 into eContainer's scopes, so all of the below resolves out of the same
`LifetimeScope` as the rest of your registrations.

### Major features

+ **Typed publish/subscribe** — `IPublisher<TMessage>` / `ISubscriber<TMessage>` replace events and signal strings with
  a message type. Subscribing returns an `IDisposable`; `DisposableBag` bundles many subscriptions into one dispose call,
  which maps cleanly onto a node's `_ExitTree`.
+ **Keyed (topic) brokers** — `IPublisher<TKey, TMessage>` / `ISubscriber<TKey, TMessage>` route the same message type by
  key, so one broker can serve many entities, channels or states.
+ **Async publish/subscribe** — `IAsyncPublisher<TMessage>` / `IAsyncSubscriber<TMessage>` await every handler.
  `MessagePipeOptions.DefaultAsyncPublishStrategy` selects `Parallel` or `Sequential` handler execution, and
  `PublishAsync` accepts a `CancellationToken`.
+ **Buffered brokers** — `IBufferedPublisher<TMessage>` / `IBufferedSubscriber<TMessage>` retain the most recent message
  and replay it to new subscribers, which suits state-like values (current health, current game phase) where a late
  subscriber still needs the last known value.
+ **Request/response handlers** — `IRequestHandler<TRequest, TResponse>` and its async counterpart give a mediator-style
  one-to-one call; `IRequestAllHandler<TRequest, TResponse>` fans the request out to every registered handler and
  collects the responses.
+ **Filters** — `MessageHandlerFilter<T>`, `AsyncMessageHandlerFilter<T>`, `RequestHandlerFilter<T>` and their async
  variants form an ordered middleware pipeline around handlers, for cross-cutting concerns such as logging, validation,
  predicate-based filtering or de-duplication. Filters are themselves resolved from the container and can be attached
  globally, per subscription, or by attribute.
+ **Performance-oriented design** — handler invocation goes through array-based broker cores rather than reflection or
  delegate chains, keeping publish paths allocation-light; MessagePipe's own benchmarks put it well ahead of plain C#
  events and Rx-style pipelines.
+ **Diagnostics** — `MessagePipeDiagnosticsInfo` reports live subscription counts, and
  `MessagePipeOptions.EnableCaptureStackTrace` records where each subscription was created, which makes leaked
  subscriptions findable.
+ **`MessagePipe.Analyzer`** — a Roslyn analyzer, installed alongside the library, whose `MPA001` diagnostic reports a
  discarded `IDisposable` from `Subscribe` — the most common source of subscription leaks.

Two upstream notes specific to this integration: VContainer resolves closed generics only, so every message type is
registered explicitly (see [Examples](#examples)) rather than through MessagePipe's open-generic auto-registration; and
MessagePipe's distributed transports (Redis, in-process, WebSocket, gRPC/MagicOnion) ship as separate packages that
eMessagePipe does not install.

## Requirements

The current CI-tested configuration uses:

+ [Godot 4.7.2 .NET](https://godotengine.org/download/archive/4.7.2-stable/)
+ [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
+ [ePlugin Framework](https://github.com/enaweg/godot-epluginframework)
+ [eContainer](https://github.com/enaweg/godot-econtainer)

The project targets `net8.0`.

## Installation

1. Install and enable [ePlugin Framework](https://github.com/enaweg/godot-epluginframework)
   and [eContainer](https://github.com/enaweg/godot-econtainer) in your Godot .NET project.
2. Download the latest eMessagePipe release and extract the archive's `addons/eMessagePipe` directory into your Godot
   project's `addons` directory. For development, clone this repository instead.
3. Open the project in the Godot .NET editor and enable **eMessagePipe** under **Project > Project Settings > Plugins**.
4. Let ePlugin complete the package installation and project reload.

The plugin adds the `MessagePipe` and `MessagePipe.Analyzer` NuGet packages to the Godot C# project. A distributed
release keeps the integration source in `.src`; ePlugin makes it available while the plugin is enabled.

The repository also contains a sample project in `src/emessagepipe`.

## Features

+ MessagePipe registration for eContainer's VContainer-based `LifetimeScope`
+ Publish/subscribe brokers: synchronous, asynchronous, and buffered variants
+ Keyed publish/subscribe brokers
+ Request handlers (synchronous and asynchronous) and MessagePipe filters
+ Self-installing NuGet packages via ePlugin Framework

## Motivation

MessagePipe is registered through `IServiceCollection`, while eContainer builds its scopes with VContainer's
`IContainerBuilder`. eMessagePipe bridges the two so `IPublisher<T>` and `ISubscriber<T>` resolve out of the same
eContainer scope as everything else, with no separate service provider to keep in sync with the scene tree.

## Testing

This project needs more testing to move forward. Feel free to participate and provide feedback.

The current CI configuration builds and tests pull requests with Godot 4.7.2 and .NET 8.

Tested combinations:

+ Godot 4.7.2 + .NET 8 (CI-tested)

To build and run the tests locally:

```bash
cd src/emessagepipe
dotnet restore eMessagePipe.sln
dotnet build eMessagePipe.sln --configuration Debug --no-restore
dotnet test eMessagePipe.sln --configuration Debug --no-build --settings .runsettings
```

Godot must be available when running the tests.
See the [CI workflow](https://github.com/enaweg/godot-emessagepipe/blob/main/.github/workflows/ci-pr.yml)
for the complete headless test setup.

gdUnit is used as the test framework.

## Examples

### Example Code (Registration)

Register MessagePipe in an eContainer `LifetimeScope`, then register every message type used by that scope.

```C#
using Enaweg.Container.Godot;
using Enaweg.MessagePipe;
using VContainer;

public partial class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        var options = builder.RegisterMessagePipe();

        // closed message-type services for PlayerDied
        builder.RegisterMessageBroker<PlayerDied>(options);

        // keyed publishers and subscribers
        builder.RegisterMessageBroker<string, PlayerDied>(options);
    }
}
```

`RegisterMessageBroker<TMessage>(options)` registers the following closed message-type services:

+ `IPublisher<TMessage>` and `ISubscriber<TMessage>`
+ `IAsyncPublisher<TMessage>` and `IAsyncSubscriber<TMessage>`
+ `IBufferedPublisher<TMessage>` and `IBufferedSubscriber<TMessage>`
+ `IBufferedAsyncPublisher<TMessage>` and `IBufferedAsyncSubscriber<TMessage>`

Request handlers and filters are available through `RegisterRequestHandler`, `RegisterAsyncRequestHandler`, and the
corresponding `Register*Filter` extensions.

### Example Code (Publishing)

Resolve or inject `IPublisher<TMessage>` and `ISubscriber<TMessage>` from the same VContainer scope:

```C#
using MessagePipe;

public sealed class ScoreService
{
    private readonly IPublisher<PlayerDied> playerDied;

    public ScoreService(IPublisher<PlayerDied> playerDied)
    {
        this.playerDied = playerDied;
    }

    public void OnPlayerDied()
    {
        playerDied.Publish(new PlayerDied());
    }
}
```

## Related projects

+ [godot-epluginframework](https://github.com/enaweg/godot-epluginframework)
+ [godot-econtainer](https://github.com/enaweg/godot-econtainer)
+ [godot-elogger](https://github.com/enaweg/godot-elogger)

## Contribute

Feel free to contribute with documentation, testing, or pull requests.

## Roadmap

* stabilize current API
* improve documentation
* expand automated testing

## Commercial Support

Commercial services are available from [Enaweg](https://www.enaweg.at). If you need consulting, implementation
assistance, or tailored development services, please get in touch through their website.

## License

Licensed under the [MIT license](LICENSE).
