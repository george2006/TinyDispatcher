using TinyDispatcher.Context;

internal sealed class NoOpContextFactory : IContextFactory<NoOpContext>
{
    public static readonly NoOpContextFactory Instance = new();

    private NoOpContextFactory() { }

    public ValueTask<NoOpContext> CreateAsync(CancellationToken ct = default)
        => new(default(NoOpContext));
}