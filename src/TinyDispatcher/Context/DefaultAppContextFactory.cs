using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher.Context;

namespace TinyDispatcher;

public sealed class DefaultAppContextFactory : IContextFactory<AppContext>
{
    private readonly IEnumerable<IFeatureInitializer> _initializers;

    public DefaultAppContextFactory(IEnumerable<IFeatureInitializer>? initializers = null)
        => _initializers = initializers ?? new List<IFeatureInitializer>();

    public ValueTask<AppContext> CreateAsync(CancellationToken ct = default)
    {
        var features = new FeatureCollection();

        foreach (var init in _initializers)
            init.Initialize(features);

        return new ValueTask<AppContext>(new AppContext(features));
    }
}
