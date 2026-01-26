using System.Threading;
using System.Threading.Tasks;

namespace TinyDispatcher.Context;

public interface IContextFactory<TContext>
{
    ValueTask<TContext> CreateAsync(CancellationToken ct = default);
}
