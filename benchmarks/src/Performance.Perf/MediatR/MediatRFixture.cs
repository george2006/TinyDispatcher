using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Performance.Shared;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Performance.Mediatr;

public sealed class MediatRFixture
{
    public ServiceProvider ServiceProvider { get; private set; } = default!;

    public void Build()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(PingHandler).Module.ModuleHandle);

        var services = new ServiceCollection();

        // Fixes: ILoggerFactory required by MediatR licensing accessor (and common deps)
        services.AddLogging();

        // Register MediatR + handler in this assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MediatRFixture).Assembly));

        // Behaviors (declarative, no loops, no runtime logic)
#if MW1 || MW2 || MW5 || MW10
        services.AddScoped<IPipelineBehavior<PingRequest, Unit>, Behavior0>();
#endif

#if MW2 || MW5 || MW10
        services.AddScoped<IPipelineBehavior<PingRequest, Unit>, Behavior1>();
#endif

#if MW5 || MW10
        services.AddScoped<IPipelineBehavior<PingRequest, Unit>, Behavior2>();
        services.AddScoped<IPipelineBehavior<PingRequest, Unit>, Behavior3>();
        services.AddScoped<IPipelineBehavior<PingRequest, Unit>, Behavior4>();
#endif

#if MW10
        services.AddScoped<IPipelineBehavior<PingRequest, Unit>, Behavior5>();
        services.AddScoped<IPipelineBehavior<PingRequest, Unit>, Behavior6>();
        services.AddScoped<IPipelineBehavior<PingRequest, Unit>, Behavior7>();
        services.AddScoped<IPipelineBehavior<PingRequest, Unit>, Behavior8>();
        services.AddScoped<IPipelineBehavior<PingRequest, Unit>, Behavior9>();
#endif

#if MW0
        // none
#elif !(MW1 || MW2 || MW5 || MW10)
#error Define one of: MW0, MW1, MW2, MW5, MW10
#endif

        ServiceProvider = services.BuildServiceProvider(validateScopes: true);
    }

    public void Cleanup()
    {
        ServiceProvider.Dispose();
    }

    public ScopeRunner CreateRunner(IServiceProvider scopedProvider) => new(scopedProvider);

    public readonly struct ScopeRunner
    {
        private readonly IMediator _mediator;

        public ScopeRunner(IServiceProvider sp)
            => _mediator = sp.GetRequiredService<IMediator>();

        public Task Send(PingRequest request, CancellationToken ct = default)
            => _mediator.Send(request, ct);
    }

    public sealed record PingRequest : IRequest<Unit>;

    public sealed class PingHandler : IRequestHandler<PingRequest, Unit>
    {
        public Task<Unit> Handle(PingRequest request, CancellationToken cancellationToken)
        {
            BlackHole.Consume(1);
            return Unit.Task;
        }
    }

    private abstract class BaseBehavior : IPipelineBehavior<PingRequest, Unit>
    {
        public async Task<Unit> Handle(
            PingRequest request,
            RequestHandlerDelegate<Unit> next,
            CancellationToken cancellationToken)
        {
            BlackHole.Consume(2);
            var result = await next().ConfigureAwait(false);
            BlackHole.Consume(3);
            return result;
        }
    }

    private sealed class Behavior0 : BaseBehavior { }
    private sealed class Behavior1 : BaseBehavior { }
    private sealed class Behavior2 : BaseBehavior { }
    private sealed class Behavior3 : BaseBehavior { }
    private sealed class Behavior4 : BaseBehavior { }
    private sealed class Behavior5 : BaseBehavior { }
    private sealed class Behavior6 : BaseBehavior { }
    private sealed class Behavior7 : BaseBehavior { }
    private sealed class Behavior8 : BaseBehavior { }
    private sealed class Behavior9 : BaseBehavior { }
}