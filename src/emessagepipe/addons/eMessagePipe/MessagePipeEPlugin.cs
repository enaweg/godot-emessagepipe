#if TOOLS
using Godot;
using Enaweg.Plugin;


namespace Enaweg.MessagePipe;

[Tool]
public partial class MessagePipeEPlugin : EditorPlugin, IEEditorPlugin
{
    public EditorPlugin GodotPlugin => this;

    /// <summary>
    /// The MessagePipe version this plugin's integration source is written against. Kept in sync
    /// with the <c>MessagePipe</c> package references in <c>eMessagePipe.csproj</c>.
    /// </summary>
    const string MessagePipeVersion = "1.8.2";

    void IEEditorPlugin.CreateRecipe(IEEditorPluginBuilder builder)
    {
        var srcDirectory = $"{this.GetPluginDirectory()}/.src";

        builder
            .AddPluginDependency("eContainer")
            .AddNuget("MessagePipe", MessagePipeVersion)
            .AddNuget("MessagePipe.Analyzer", MessagePipeVersion)
            .AddDirectory(srcDirectory);
    }

    public override void _EnablePlugin()
    {
        base._EnablePlugin();
        this.EnableEPlugin();
    }

    public override void _DisablePlugin()
    {
        this.DisableEPlugin();
        base._DisablePlugin();
    }
}
#endif