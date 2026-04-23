namespace TinyDispatcher.Bootstrap;

public sealed class CommandHandlerDescriptor
{
    public CommandHandlerDescriptor(
        string CommandTypeFqn,
        string HandlerTypeFqn,
        string ContextTypeFqn)
    {
        this.CommandTypeFqn = CommandTypeFqn;
        this.HandlerTypeFqn = HandlerTypeFqn;
        this.ContextTypeFqn = ContextTypeFqn;
    }

    public string CommandTypeFqn { get; }

    public string HandlerTypeFqn { get; }

    public string ContextTypeFqn { get; }
}
