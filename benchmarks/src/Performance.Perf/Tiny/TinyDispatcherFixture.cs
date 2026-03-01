using Microsoft.Extensions.DependencyInjection;
using Performance.Shared;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using TinyDispatcher.Pipeline;

namespace Performance.Tiny;

public record PingCommand() : ICommand;

public sealed class TinyDispatcherFixture
{
    public ServiceProvider ServiceProvider { get; private set; } = default!;

    public void Build()
    {
        // Ensures source-gen module initializers / static init is done upfront
        RuntimeHelpers.RunModuleConstructor(typeof(PingHandlerNoOpContext).Module.ModuleHandle);

        var services = new ServiceCollection();

        TinyBenchmarkRegistration.AddGenerated(services);

        services.UseTinyDispatcher<TinyDispatcher.AppContext>(cfg =>
        {
#if MW0
            // no middleware
#elif MW1
            cfg.UseGlobalMiddleware(typeof(Middleware0<,>));
#elif MW2
            cfg.UseGlobalMiddleware(typeof(Middleware0<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware1<,>));
#elif MW5
            cfg.UseGlobalMiddleware(typeof(Middleware0<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware1<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware2<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware3<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware4<,>));
#elif MW10
            cfg.UseGlobalMiddleware(typeof(Middleware0<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware1<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware2<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware3<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware4<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware5<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware6<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware7<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware8<,>));
            cfg.UseGlobalMiddleware(typeof(Middleware9<,>));
#else
#error Define one of: MW0, MW1, MW2, MW5, MW10
#endif
        });

        ServiceProvider = services.BuildServiceProvider(validateScopes: true);
    }

    public void Cleanup()
    {
        ServiceProvider.Dispose();
    }

    public ScopeRunner CreateRunner(IServiceProvider scopedProvider) => new(scopedProvider);

    public readonly struct ScopeRunner
    {
        private readonly IDispatcher<TinyDispatcher.AppContext> _dispatcher;

        public ScopeRunner(IServiceProvider sp)
            => _dispatcher = sp.GetRequiredService<IDispatcher<TinyDispatcher.AppContext>>();

        public Task Dispatch(PingCommand command, CancellationToken ct = default)
            => _dispatcher.DispatchAsync(command, ct);
    }

    // --- Handler

    public sealed class PingHandlerNoOpContext : ICommandHandler<PingCommand, NoOpContext>
    {
        public Task HandleAsync(PingCommand command, NoOpContext context, CancellationToken cancellationToken = default)
        {
            BlackHole.Consume(1);
            return Task.CompletedTask;
        }
    }

    public sealed class PingHandlerAppContext : ICommandHandler<PingCommand, TinyDispatcher.AppContext>
    {
        public Task HandleAsync(PingCommand command, TinyDispatcher.AppContext context, CancellationToken cancellationToken = default)
        {
            BlackHole.Consume(1);
            return Task.CompletedTask;
        }
    }
}

// --- Middleware

public abstract class Base<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
    where TCommand : ICommand
{
    public async ValueTask InvokeAsync(
        TCommand command,
        TContext context,
        ICommandPipelineRuntime<TCommand, TContext> runtime,
        CancellationToken ct)
    {
        BlackHole.Consume(2);
        await runtime.NextAsync(command, context, ct).ConfigureAwait(false);
        BlackHole.Consume(3);
    }
}

public sealed class Middleware0<TCommand, TContext> : Base<TCommand, TContext> where TCommand : ICommand;
public sealed class Middleware1<TCommand, TContext> : Base<TCommand, TContext> where TCommand : ICommand;
public sealed class Middleware2<TCommand, TContext> : Base<TCommand, TContext> where TCommand : ICommand;
public sealed class Middleware3<TCommand, TContext> : Base<TCommand, TContext> where TCommand : ICommand;
public sealed class Middleware4<TCommand, TContext> : Base<TCommand, TContext> where TCommand : ICommand;
public sealed class Middleware5<TCommand, TContext> : Base<TCommand, TContext> where TCommand : ICommand;
public sealed class Middleware6<TCommand, TContext> : Base<TCommand, TContext> where TCommand : ICommand;
public sealed class Middleware7<TCommand, TContext> : Base<TCommand, TContext> where TCommand : ICommand;
public sealed class Middleware8<TCommand, TContext> : Base<TCommand, TContext> where TCommand : ICommand;
public sealed class Middleware9<TCommand, TContext> : Base<TCommand, TContext> where TCommand : ICommand;