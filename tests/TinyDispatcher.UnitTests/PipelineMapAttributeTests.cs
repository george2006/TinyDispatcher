#nullable enable

using System;
using TinyDispatcher;
using Xunit;

namespace TinyDispatcher.UnitTests;

public sealed class PipelineMapAttributeTests
{
    [Fact]
    public void Constructor_preserves_pipeline_composition()
    {
        var commands = new[] { typeof(TestCommand) };
        var middlewares = new[]
        {
            typeof(GlobalMiddleware),
            typeof(PolicyMiddleware),
            typeof(OperationMiddleware)
        };

        var attribute = new TinyDispatcherPipelineMapAttribute(
            typeof(TestPipeline),
            typeof(TestContext),
            commands,
            middlewares,
            globalMiddlewareCount: 1,
            policyType: typeof(TestPolicy),
            policyMiddlewareCount: 1);

        Assert.Equal(typeof(TestPipeline), attribute.PipelineType);
        Assert.Equal(typeof(TestContext), attribute.ContextType);
        Assert.Equal(commands, attribute.CommandTypes);
        Assert.Equal(middlewares, attribute.MiddlewareTypes);
        Assert.Equal(1, attribute.GlobalMiddlewareCount);
        Assert.Equal(typeof(TestPolicy), attribute.PolicyType);
        Assert.Equal(1, attribute.PolicyMiddlewareCount);
    }

    [Fact]
    public void Constructor_rejects_source_counts_beyond_the_middleware_collection()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new TinyDispatcherPipelineMapAttribute(
                typeof(TestPipeline),
                typeof(TestContext),
                new[] { typeof(TestCommand) },
                new[] { typeof(GlobalMiddleware) },
                globalMiddlewareCount: 1,
                policyType: typeof(TestPolicy),
                policyMiddlewareCount: 1));

        Assert.Contains("exceed", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Constructor_rejects_policy_middleware_without_a_policy_type()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new TinyDispatcherPipelineMapAttribute(
                typeof(TestPipeline),
                typeof(TestContext),
                new[] { typeof(TestCommand) },
                new[] { typeof(PolicyMiddleware) },
                globalMiddlewareCount: 0,
                policyType: null,
                policyMiddlewareCount: 1));

        Assert.Equal("policyType", exception.ParamName);
    }

    [Fact]
    public void Constructor_rejects_pipeline_without_commands()
    {
        var exception = Assert.Throws<ArgumentException>(() =>
            new TinyDispatcherPipelineMapAttribute(
                typeof(TestPipeline),
                typeof(TestContext),
                Array.Empty<Type>(),
                Array.Empty<Type>(),
                globalMiddlewareCount: 0,
                policyType: null,
                policyMiddlewareCount: 0));

        Assert.Equal("commandTypes", exception.ParamName);
    }

    private sealed class TestPipeline;

    private sealed class TestContext;

    private sealed class TestCommand;

    private sealed class TestPolicy;

    private sealed class GlobalMiddleware;

    private sealed class PolicyMiddleware;

    private sealed class OperationMiddleware;
}
