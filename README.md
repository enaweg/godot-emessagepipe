# eMessagePipe

[MessagePipe](https://github.com/Cysharp/MessagePipe) integration for Godot .NET projects that use [eContainer](https://github.com/enaweg/godot-econtainer) and VContainer.

eMessagePipe is an [ePlugin Framework](https://github.com/enaweg/godot-epluginframework) plugin. When enabled from the Godot editor, it adds the MessagePipe NuGet packages and exposes the integration source in the consuming project. It provides VContainer registration extensions for MessagePipe's publish/subscribe, keyed publish/subscribe, and request-handler APIs.

> This project is experimental and a work in progress.

## Requirements

The current CI-tested configuration uses:

- [Godot 4.7.2 .NET](https://godotengine.org/download/archive/4.7.2-stable/)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [ePlugin Framework](https://github.com/enaweg/godot-epluginframework)
- [eContainer](https://github.com/enaweg/godot-econtainer)

The project targets `net8.0`.

## Installation

1. Install and enable ePlugin Framework and eContainer in your Godot .NET project.
2. Download an eMessagePipe release and copy its `addons/eMessagePipe` directory into your project's `addons` directory. For development, clone this repository instead.
3. Open the project in the Godot .NET editor and enable **eMessagePipe** under **Project > Project Settings > Plugins**.
4. Let ePlugin complete the package installation and project reload.

The plugin adds `MessagePipe` and `MessagePipe.Analyzer` to the Godot C# project. A distributed release keeps the integration source in `.src`; ePlugin makes it available while the plugin is enabled.

## Usage

Register MessagePipe in an eContainer `LifetimeScope`, then register every message type used by that scope.

```csharp
using Enaweg.MessagePipe;
using VContainer;

public partial class GameLifetimeScope : LifetimeScope
{
    protected override void Configure(IContainerBuilder builder)
    {
        var options = builder.RegisterMessagePipe();
        builder.RegisterMessageBroker<PlayerDied>(options);
    }
}
```

Resolve or inject `IPublisher<TMessage>` and `ISubscriber<TMessage>` from the same VContainer scope:

```csharp
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

### Available registrations

`RegisterMessageBroker<TMessage>(options)` registers the following closed message-type services:

- `IPublisher<TMessage>` and `ISubscriber<TMessage>`
- `IAsyncPublisher<TMessage>` and `IAsyncSubscriber<TMessage>`
- `IBufferedPublisher<TMessage>` and `IBufferedSubscriber<TMessage>`
- `IBufferedAsyncPublisher<TMessage>` and `IBufferedAsyncSubscriber<TMessage>`

Use `RegisterMessageBroker<TKey, TMessage>(options)` for keyed publishers and subscribers. Request handlers and filters are available through `RegisterRequestHandler`, `RegisterAsyncRequestHandler`, and the corresponding `Register*Filter` extensions.

## Testing

The CI workflow builds and tests pull requests with Godot 4.7.2 and .NET 8. Run the same .NET build and test commands locally from the Godot project directory:

```bash
cd src/emessagepipe
dotnet restore eMessagePipe.sln
dotnet build eMessagePipe.sln --configuration Debug --no-restore
dotnet test eMessagePipe.sln --configuration Debug --no-build --settings .runsettings
```

Godot must be available when running gdUnit4 tests. See the [pull-request workflow](.github/workflows/ci-pr.yml) for the complete headless CI setup.

## Contribute

Contributions, issue reports, and feedback are welcome.

## License

Licensed under the [MIT License](LICENSE).
