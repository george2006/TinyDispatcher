using System.Threading;
using System.Threading.Tasks;

namespace TinyDispatcher;

/// <summary>
/// Represents the command pipeline for a given (TCommand, TContext).
/// Implementations are generated at compile time by TinyDispatcher.
/// </summary>
public delegate Task CommandDelegate<TCommand, TContext>(
    TCommand command,
    TContext ctx,
    CancellationToken ct)
    where TCommand : ICommand;
