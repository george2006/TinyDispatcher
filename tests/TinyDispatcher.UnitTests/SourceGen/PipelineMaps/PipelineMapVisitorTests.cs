#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Options;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.PipelineMaps;

public sealed class PipelineMapVisitorTests
{
    [Fact]
    public void Visitor_preserves_resolved_pipeline_order_source_and_policy()
    {
        var steps = ImmutableArray.Create(
            new MiddlewareStep(
                Middleware("global::MyApp.Middleware.GlobalMiddleware"),
                PipelineStepSource.Global),
            new MiddlewareStep(
                Middleware("global::MyApp.Middleware.PolicyMiddleware"),
                PipelineStepSource.Policy,
                "global::MyApp.Policies.CheckoutPolicy"),
            new MiddlewareStep(
                Middleware("global::MyApp.Middleware.OperationMiddleware"),
                PipelineStepSource.Operation));
        var pipeline = new ResolvedPipeline(
            new HandlerContract(
                MessageTypeFqn: "global::MyApp.Commands.Checkout",
                HandlerTypeFqn: "global::MyApp.Handlers.CheckoutHandler",
                ContextTypeFqn: "global::MyApp.AppContext"),
            new PipelineDefinition(
                ClassName: "TinyDispatcherPipeline_Checkout",
                IsOpenGeneric: false,
                CommandType: "global::MyApp.Commands.Checkout",
                Steps: steps));
        var visitor = new PipelineMapVisitor();

        visitor.Visit(pipeline);

        Assert.True(visitor.TryGetDescriptor(
            "global::MyApp.Commands.Checkout",
            out var descriptor));

        Assert.Single(descriptor.PoliciesApplied);
        Assert.Equal(
            "global::MyApp.Policies.CheckoutPolicy",
            descriptor.PoliciesApplied[0]);
        Assert.Equal(3, descriptor.Middlewares.Count);
        Assert.Equal("global", descriptor.Middlewares[0].Source);
        Assert.Equal(
            "policy:global::MyApp.Policies.CheckoutPolicy",
            descriptor.Middlewares[1].Source);
        Assert.Equal("per-command", descriptor.Middlewares[2].Source);
    }

    private static MiddlewareRef Middleware(string openTypeFqn)
    {
        return new MiddlewareRef(
            OpenTypeFqn: openTypeFqn,
            Arity: 2);
    }
}

