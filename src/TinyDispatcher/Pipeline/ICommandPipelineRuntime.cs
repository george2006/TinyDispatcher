using System;
using System.Collections.Generic;
using System.Text;

namespace TinyDispatcher.Pipeline;

public interface ICommandPipelineRuntime<TCommand, TContext> where TCommand : ICommand
{
    ValueTask NextAsync(TCommand command, TContext context, CancellationToken ct = default);
}
