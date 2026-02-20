using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Handlers;

internal sealed class HandlerRegistrationsPlan
{
    public HandlerRegistrationsPlan(
        string @namespace,
        bool isEnabled,
        string commandContextFqn,
        HandlerContract[] commands,
        QueryHandlerContract[] queries)
    {
        Namespace = @namespace;
        IsEnabled = isEnabled;
        CommandContextFqn = commandContextFqn;
        Commands = commands;
        Queries = queries;
    }

    public string Namespace { get; }

    public bool IsEnabled { get; }

    // Only meaningful when IsEnabled == true
    public string CommandContextFqn { get; }

    public HandlerContract[] Commands { get; }

    public QueryHandlerContract[] Queries { get; }

    public bool ShouldInsertBlankLineBetweenCommandAndQueryBlocks =>
        Commands.Length > 0 && Queries.Length > 0;

    public static HandlerRegistrationsPlan Disabled(string @namespace) =>
        new(
            @namespace: @namespace,
            isEnabled: false,
            commandContextFqn: string.Empty,
            commands: System.Array.Empty<HandlerContract>(),
            queries: System.Array.Empty<QueryHandlerContract>());
}