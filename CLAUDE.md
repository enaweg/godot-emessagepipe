# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

A Godot 4.7 (C#) project. The Godot project root is `src/emessagepipe/` (not the repo root) — `eMessagePipe.csproj` and `project.godot` live there. The project itself is mostly a testbed/host for a set of custom Godot editor plugins developed in-repo under `src/emessagepipe/addons/`:

- **eContainer** — a VContainer-based dependency injection container for Godot (`Enaweg.Container` / `Enaweg.Container.Godot` namespaces).
- **eMessagePipe** — wires the [MessagePipe](https://github.com/Cysharp/MessagePipe) pub/sub library into eContainer's DI container (`Enaweg.MessagePipe` namespace).
- **eLogger** — wires [ZLogger](https://github.com/Cysharp/ZLogger) into Godot's console/debugger (`Enaweg.Logger` namespace).
- **ePlugin** — the framework the other three plugins are built on (`Enaweg.Plugin` namespace). It provides `IEEditorPlugin`/`IEEditorPluginBuilder` for declaring what a plugin needs (autoloads, NuGet packages, project references, directories to toggle) and an internal engine that applies/reverses that "recipe" when a plugin is enabled/disabled in the Godot editor, driving `dotnet` (add/remove package, add/remove project reference, solution edits) under the hood.
- **gdUnit4** — vendored third-party unit test framework/runner for Godot + C#.

## Commands

All commands run from `src/emessagepipe/` (the Godot project / `dotnet` project root):

```bash
cd src/emessagepipe
dotnet build          # build the solution
dotnet test           # run the C# test suite (via gdUnit4's dotnet test adapter)
```

`dotnet test` with a filter runs a single test, e.g. `dotnet test --filter "FullyQualifiedName~ClassName.MethodName"`.

These are the same commands the in-editor `ePlugin` framework itself invokes (see `addons/ePlugin/Internal/Dotnet/DotnetCli10.cs` / `DotnetCli9.cs`) when a plugin is enabled/disabled, so they are the canonical way to build/test outside the editor too.

Opening/running the project itself requires the Godot 4.7 editor with .NET/Mono support (not scriptable from the CLI in this environment).

## Architecture notes

### Editor plugins are self-installing via `ePlugin`

Each addon (`eContainer`, `eMessagePipe`, `eLogger`) is a normal Godot `EditorPlugin` that also implements `IEEditorPlugin` from `ePlugin`. Instead of manually editing `.csproj`/`.sln`/`project.godot`, each plugin's `CreateRecipe(IEEditorPluginBuilder builder)` declaratively lists what it needs (`AddNuget`, `AddDirectory`, `AddAutoload`, `AddPluginDependency`, `AddProject`), and calls `this.EnableEPlugin()` / `this.DisableEPlugin()` from `_EnablePlugin`/`_DisablePlugin`. `ePlugin`'s internal `EEditorPluginBuilder`/`EEditorPluginRecipe` apply that recipe (installing NuGet packages, wiring project references, registering autoloads, toggling directory visibility) when the plugin is toggled on in the editor, and reverse it when toggled off. All of this code is guarded by `#if TOOLS` — it only runs in the editor, not in exported/runtime builds.

### eContainer's DI/lifetime-scope model

`LifetimeScope` (`addons/eContainer/src/Runtime/Godot/LifetimeScope.cs`) is a `Node` that owns a VContainer `IObjectResolver`. Scopes form a tree mirroring the Godot scene tree: a scope's `Parent` is resolved either explicitly (`ParentReference`), by scene search for a given type, or defaults to the singleton `RootLifetimeScope` (an autoload registered in `project.godot` via `eContainer.tscn`, instantiated as `addons/eContainer/eContainer.tscn`). Building a child scope calls `Parent.Container.CreateScope(...)`, so container lifetimes nest with the scene tree.

Because Godot nodes can enter the tree before their intended parent scope exists, `LifetimeScope._EnterTree` catches `VContainerParentTypeReferenceNotFound` and enqueues itself on `RootLifetimeScope`'s static waiting list; `RootLifetimeScope` retries queued scopes as new nodes/scenes enter the tree (`OnChildEnteredTreeRoot` / `ReadyWaitingChildren`).

Registration entry point: `LifetimeScope.Configure(IContainerBuilder)` (override per scope) plus any `IInstaller`s queued via `LifetimeScope.Enqueue(...)`/`EnqueueParent(...)`. Every scope auto-registers itself and an `EntryPointDispatcher` (see `EntryPointsBuilder`), which drives `IInitializable`/`IPostInitializable`/`ITickable`/`IPhysicsTickable` implementations registered in the container.

### eMessagePipe integration

`ContainerBuilderExtensions.RegisterMessagePipe(this IContainerBuilder)` bridges VContainer's `IContainerBuilder` to MessagePipe's `IServiceCollection`-based registration via a `ContainerBuilderProxy`/`ObjectResolverProxy` shim (`Enaweg.MessagePipe.VContainer` namespace), so `IPublisher<T>`/`ISubscriber<T>` (and keyed/async/buffered/request-handler variants) resolve out of the same eContainer scope as everything else.

### No application code yet

`test.tscn` is currently an empty placeholder scene set as `run/main_scene`. There is no game/application code under `src/emessagepipe/` outside the `addons/` plugins themselves, and no tests have been written yet (gdUnit4 is installed and wired into `eMessagePipe.csproj` via the `gdUnit4.test.adapter`/`gdUnit4.analyzers`/`Microsoft.NET.Test.Sdk` package references, ready for `dotnet test` once tests exist).
