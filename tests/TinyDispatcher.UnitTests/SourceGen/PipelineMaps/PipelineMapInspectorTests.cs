#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Emitters.PipelineMaps;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.PipelineMaps;

public sealed class PipelineMapInspectorTests
{
    [Fact]
    public void InspectCommand_when_multiple_policies_target_command_uses_first_policy_in_stable_order()
    {
        var globals = ImmutableArray<MiddlewareRef>.Empty;
        var perCommand = ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty;
        var policies = CreatePoliciesInReverseInsertionOrder();
        var options = FakeOptions();

        var sut = new PipelineMapInspector(globals, perCommand, policies, options);

        var descriptor = sut.InspectCommand(new HandlerContract(
            MessageTypeFqn: "global::MyApp.Commands.Checkout",
            HandlerTypeFqn: "global::MyApp.Handlers.CheckoutHandler"));

        Assert.Single(descriptor.PoliciesApplied);
        Assert.Equal("global::MyApp.Policies.AlphaPolicy", descriptor.PoliciesApplied[0]);

        Assert.Single(descriptor.Middlewares);
        Assert.Equal("global::MyApp.Middleware.AlphaPolicyMiddleware", descriptor.Middlewares[0].MiddlewareFullName);
        Assert.Equal("policy:global::MyApp.Policies.AlphaPolicy", descriptor.Middlewares[0].Source);
    }

    private static ImmutableDictionary<string, PolicySpec> CreatePoliciesInReverseInsertionOrder()
    {
        var builder = ImmutableDictionary.CreateBuilder<string, PolicySpec>();

        builder.Add(
            "global::MyApp.Policies.ZuluPolicy",
            CreatePolicy(
                "global::MyApp.Policies.ZuluPolicy",
                "global::MyApp.Middleware.ZuluPolicyMiddleware"));

        builder.Add(
            "global::MyApp.Policies.AlphaPolicy",
            CreatePolicy(
                "global::MyApp.Policies.AlphaPolicy",
                "global::MyApp.Middleware.AlphaPolicyMiddleware"));

        return builder.ToImmutable();
    }

    private static PolicySpec CreatePolicy(string policyTypeFqn, string middlewareTypeFqn)
    {
        return new PolicySpec(
            PolicyTypeFqn: policyTypeFqn,
            Middlewares: ImmutableArray.Create(Middleware(middlewareTypeFqn)),
            Commands: ImmutableArray.Create("global::MyApp.Commands.Checkout"));
    }

    private static MiddlewareRef Middleware(string openTypeFqn)
    {
        return new MiddlewareRef(
            OpenTypeSymbol: default!,
            OpenTypeFqn: openTypeFqn,
            Arity: 2);
    }

    private static GeneratorOptions FakeOptions()
    {
        return new GeneratorOptions(
            GeneratedNamespace: "MyApp.Generated",
            EmitDiExtensions: false,
            EmitHandlerRegistrations: false,
            IncludeNamespacePrefix: null,
            CommandContextType: "global::MyApp.AppContext",
            EmitPipelineMap: true,
            PipelineMapFormat: "json");
    }
}
