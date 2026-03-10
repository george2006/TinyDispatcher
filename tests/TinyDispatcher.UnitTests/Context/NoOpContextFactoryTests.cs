using System.Threading.Tasks;
using Xunit;

namespace TinyDispatcher.UnitTests.Context;

public sealed class NoOpContextFactoryTests
{
    [Fact]
    public async Task Create_async_returns_default_no_op_context()
    {
        var context = await NoOpContextFactory.Instance.CreateAsync();

        Assert.Equal(default, context);
    }
}