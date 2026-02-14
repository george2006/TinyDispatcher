using TinyDispatcher.Pipeline;

namespace TinyDispatcher;

public interface ICommandPipeline<TCommand, TContext> where TCommand : ICommand
{
    ValueTask ExecuteAsync(TCommand command, TContext context, ICommandHandler<TCommand, TContext> handler, CancellationToken ct = default);
}


///// <summary>
///// Policy pipeline for a command. Lower precedence than ICommandPipeline, higher than global.
///// </summary>
//public interface IPolicyCommandPipeline<TCommand, TContext> : ICommandPipeline<TCommand, TContext>
//    where TCommand : ICommand
//{
//}

///// <summary>
///// Global pipeline for a command. Lowest precedence (still above direct handler call).
///// </summary>
//public interface IGlobalCommandPipeline<TCommand, TContext> : ICommandPipeline<TCommand, TContext>
//    where TCommand : ICommand
//{
//}
