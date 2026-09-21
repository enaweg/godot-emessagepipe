#if TOOLS
using Godot;
using Enaweg.Plugin;


namespace Enaweg.MessagePipe;

[Tool]
public partial class MessagePipeEPlugin : EditorPlugin, IEEditorPlugin
{
    public EditorPlugin GodotPlugin => this;

    void IEEditorPlugin.CreateRecipe(IEEditorPluginBuilder builder)
    {
        var srcDirectory = $"{this.GetPluginDirectory()}/.src";
        
        builder
            .AddNuget("MessagePipe")
            .AddNuget("MessagePipe.Analyzer")
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