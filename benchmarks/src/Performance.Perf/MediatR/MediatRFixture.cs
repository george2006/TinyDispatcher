using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging; // optional, but AddLogging needs package anyway
using Performance.Shared;
using System;

namespace Performance.Mediatr;

public sealed class MediatRFixture
{
    private ServiceProvider _sp = default!;
    private IMediator _mediator = default!;

    public void Build()
    {
        var services = new ServiceCollection();

        // Fixes: ILoggerFactory required by MediatR licensing accessor
        services.AddLogging();

        // Register MediatR + handler in this assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MediatRFixture).Assembly));

        // Register behaviors declaratively (NO loops, NO runtime logic)
#if MW1 || MW2 || MW5 ||MW10
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

        _sp = services.BuildServiceProvider(validateScopes: false);
        _mediator = _sp.GetRequiredService<IMediator>();
    }

    public Task Send(PingRequest request, CancellationToken ct = default)
        => _mediator.Send(request, ct);

    // --- Request wrapper (keeps Shared free of MediatR references)
    public sealed record PingRequest : IRequest<Unit>;

    // --- Handler
    public sealed class PingHandler : IRequestHandler<PingRequest, Unit>
    {
        public Task<Unit> Handle(PingRequest request, CancellationToken cancellationToken)
        {
            BlackHole.Consume(1);
            return Task.FromResult(Unit.Value);
        }
    }

    // --- Behaviors (5 distinct types; order matters)
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