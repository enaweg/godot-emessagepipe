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
        var eContainerSrcDirectory = $"{this.GetPluginDirectory()}/.src-econtainer";

        builder
            .AddNuget("MessagePipe", MessagePipeVersion)
            .AddNuget("MessagePipe.Analyzer", MessagePipeVersion)
            .AddOptionalPluginDependency("eContainer", null, recipe =>
                recipe.AddDirectory(eContainerSrcDirectory));
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
