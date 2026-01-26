using TinyDispatcher.Pipeline;

namespace TinyDispatcher;

/// <summary>
/// Command pipeline override for a specific command.
/// </summary>
public interface ICommandPipeline<TCommand, TContext> : ICommandPipelineInvoker<TCommand, TContext>
    where TCommand : ICommand
{
}

/// <summary>
/// Policy pipeline for a command. Lower precedence than ICommandPipeline, higher than global.
/// </summary>
public interface IPolicyCommandPipeline<TCommand, TContext> : ICommandPipeline<TCommand, TContext>
    where TCommand : ICommand
{
}

/// <summary>
/// Global pipeline for a command. Lowest precedence (still above direct handler call).
/// </summary>
public interface IGlobalCommandPipeline<TCommand, TContext> : ICommandPipeline<TCommand, TContext>
    where TCommand : ICommand
{
}
