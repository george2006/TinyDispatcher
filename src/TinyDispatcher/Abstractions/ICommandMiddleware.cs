using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher.Pipeline;

namespace TinyDispatcher;

//public interface ICommandMiddleware<TCommand, TContext>
//    where TCommand : ICommand
//{
//    Task InvokeAsync(
//        TCommand command,
//        TContext ctx,
//        CommandDelegate<TCommand, TContext> next,
//        CancellationToken ct);
//}

public interface ICommandMiddleware<TCommand, TContext> where TCommand : ICommand
{
    ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct = default);
}

