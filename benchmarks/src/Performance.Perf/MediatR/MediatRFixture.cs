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
    private ServiceProvider _sp = default!;
    private IServiceScope _scope = default!;
    private IMediator _mediator = default!;

    public void Build()
    {
        RuntimeHelpers.RunModuleConstructor(typeof(PingHandler).Module.ModuleHandle);

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MediatRFixture).Assembly));

        // Behaviors (no loops, no runtime logic)
#if MW1 || MW2 || MW5 || MW10
        services.AddTransient<IPipelineBehavior<PingRequest, Unit>, Behavior0>();
#endif

#if MW2 || MW5 || MW10
        services.AddTransient<IPipelineBehavior<PingRequest, Unit>, Behavior1>();
#endif

#if MW5 || MW10
        services.AddTransient<IPipelineBehavior<PingRequest, Unit>, Behavior2>();
        services.AddTransient<IPipelineBehavior<PingRequest, Unit>, Behavior3>();
        services.AddTransient<IPipelineBehavior<PingRequest, Unit>, Behavior4>();
#endif

#if MW10
        services.AddTransient<IPipelineBehavior<PingRequest, Unit>, Behavior5>();
        services.AddTransient<IPipelineBehavior<PingRequest, Unit>, Behavior6>();
        services.AddTransient<IPipelineBehavior<PingRequest, Unit>, Behavior7>();
        services.AddTransient<IPipelineBehavior<PingRequest, Unit>, Behavior8>();
        services.AddTransient<IPipelineBehavior<PingRequest, Unit>, Behavior9>();
#endif

#if MW0
        // none
#elif !(MW1 || MW2 || MW5 || MW10)
#error Define one of: MW0, MW1, MW2, MW5, MW10
#endif

        _sp = services.BuildServiceProvider(validateScopes: true);

        // Create ONE scope for the benchmark instance
        _scope = _sp.CreateScope();
        _mediator = _scope.ServiceProvider.GetRequiredService<IMediator>();
    }

    public void Cleanup()
    {
        _scope.Dispose();
        _sp.Dispose();
    }

    public Task Send(PingRequest request, CancellationToken ct = default)
        => _mediator.Send(request, ct);

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