#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Validation;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class ReferencedContributionConflictValidatorTests
{
    [Fact]
    public void Validate_reports_DISP413_when_referenced_assembly_conflicts_with_local_per_command_pipeline()
    {
        var context = CreateContext(
            localPipeline: new PipelineConfig(
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty.Add(
                    "global::MyApp.CreateOrder",
                    ImmutableArray.Create(new MiddlewareRef("global::MyApp.LocalMiddleware", 2))),
                ImmutableDictionary<string, PolicySpec>.Empty),
            referencedContributions: Referenced(
                new ReferencedAssemblyContribution(
                    "OrdersContrib",
                    "global::MyApp.AppContext",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray.Create(new PerCommandMiddlewareFinding(
                        "global::MyApp.CreateOrder",
                        ImmutableArray.Create(new MiddlewareRef("global::Orders.SharedMiddleware", 2)))),
                    ImmutableArray<PolicyFinding>.Empty)));

        var diagnostics = new DiagnosticBag();

        new ReferencedContributionConflictValidator().Validate(context, diagnostics);

        var diagnostic = Assert.Single(diagnostics.ToImmutable());
        Assert.Equal("DISP413", diagnostic.Id);
    }

    [Fact]
    public void Validate_reports_DISP413_when_referenced_assemblies_contribute_same_per_command_pipeline()
    {
        var context = CreateContext(
            localPipeline: PipelineConfig.Empty,
            referencedContributions: Referenced(
                new ReferencedAssemblyContribution(
                    "OrdersContrib",
                    "global::MyApp.AppContext",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray.Create(new PerCommandMiddlewareFinding(
                        "global::MyApp.CreateOrder",
                        ImmutableArray.Create(new MiddlewareRef("global::Orders.SharedMiddleware", 2)))),
                    ImmutableArray<PolicyFinding>.Empty),
                new ReferencedAssemblyContribution(
                    "BillingContrib",
                    "global::MyApp.AppContext",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray.Create(new PerCommandMiddlewareFinding(
                        "global::MyApp.CreateOrder",
                        ImmutableArray.Create(new MiddlewareRef("global::Billing.SharedMiddleware", 2)))),
                    ImmutableArray<PolicyFinding>.Empty)));

        var diagnostics = new DiagnosticBag();

        new ReferencedContributionConflictValidator().Validate(context, diagnostics);

        var diagnostic = Assert.Single(diagnostics.ToImmutable());
        Assert.Equal("DISP413", diagnostic.Id);
    }

    [Fact]
    public void Validate_reports_DISP413_when_single_referenced_assembly_repeats_same_command_target()
    {
        var context = CreateContext(
            localPipeline: PipelineConfig.Empty,
            referencedContributions: Referenced(
                new ReferencedAssemblyContribution(
                    "OrdersContrib",
                    "global::MyApp.AppContext",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray.Create(
                        new PerCommandMiddlewareFinding(
                            "global::MyApp.CreateOrder",
                            ImmutableArray.Create(new MiddlewareRef("global::Orders.FirstMiddleware", 2))),
                        new PerCommandMiddlewareFinding(
                            "global::MyApp.CreateOrder",
                            ImmutableArray.Create(new MiddlewareRef("global::Orders.SecondMiddleware", 2)))),
                    ImmutableArray<PolicyFinding>.Empty)));

        var diagnostics = new DiagnosticBag();

        new ReferencedContributionConflictValidator().Validate(context, diagnostics);

        var diagnostic = Assert.Single(diagnostics.ToImmutable());
        Assert.Equal("DISP413", diagnostic.Id);
    }

    [Fact]
    public void Validate_does_not_report_DISP413_for_referenced_command_in_other_context()
    {
        var context = CreateContext(
            localPipeline: new PipelineConfig(
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty.Add(
                    "global::MyApp.CreateOrder",
                    ImmutableArray.Create(new MiddlewareRef("global::MyApp.LocalMiddleware", 2))),
                ImmutableDictionary<string, PolicySpec>.Empty),
            referencedContributions: Referenced(
                new ReferencedAssemblyContribution(
                    "MixedContrib",
                    null,
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray.Create(new PerCommandMiddlewareFinding(
                        "global::MyApp.CreateOrder",
                        ImmutableArray.Create(new MiddlewareRef("global::Mixed.OtherMiddleware", 2)),
                        "global::OtherApp.OtherContext")),
                    ImmutableArray<PolicyFinding>.Empty)));

        var diagnostics = new DiagnosticBag();

        new ReferencedContributionConflictValidator().Validate(context, diagnostics);

        Assert.Empty(diagnostics.ToImmutable());
    }

    [Fact]
    public void Validate_reports_DISP414_when_referenced_assembly_conflicts_with_local_policy()
    {
        var context = CreateContext(
            localPipeline: new PipelineConfig(
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty.Add(
                    "global::MyApp.SharedPolicy",
                    new PolicySpec(
                        "global::MyApp.SharedPolicy",
                        ImmutableArray<MiddlewareRef>.Empty,
                        ImmutableArray<string>.Empty))),
            referencedContributions: Referenced(
                new ReferencedAssemblyContribution(
                    "OrdersContrib",
                    "global::MyApp.AppContext",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray<PerCommandMiddlewareFinding>.Empty,
                    ImmutableArray.Create(new PolicyFinding(
                        "global::MyApp.SharedPolicy",
                        ImmutableArray<MiddlewareRef>.Empty,
                        ImmutableArray<string>.Empty)))));

        var diagnostics = new DiagnosticBag();

        new ReferencedContributionConflictValidator().Validate(context, diagnostics);

        var diagnostic = Assert.Single(diagnostics.ToImmutable());
        Assert.Equal("DISP414", diagnostic.Id);
    }

    [Fact]
    public void Validate_reports_DISP414_when_single_referenced_assembly_repeats_same_policy_type()
    {
        var context = CreateContext(
            localPipeline: PipelineConfig.Empty,
            referencedContributions: Referenced(
                new ReferencedAssemblyContribution(
                    "OrdersContrib",
                    "global::MyApp.AppContext",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray<PerCommandMiddlewareFinding>.Empty,
                    ImmutableArray.Create(
                        new PolicyFinding(
                            "global::MyApp.SharedPolicy",
                            ImmutableArray<MiddlewareRef>.Empty,
                            ImmutableArray<string>.Empty),
                        new PolicyFinding(
                            "global::MyApp.SharedPolicy",
                            ImmutableArray<MiddlewareRef>.Empty,
                            ImmutableArray<string>.Empty)))));

        var diagnostics = new DiagnosticBag();

        new ReferencedContributionConflictValidator().Validate(context, diagnostics);

        var diagnostic = Assert.Single(diagnostics.ToImmutable());
        Assert.Equal("DISP414", diagnostic.Id);
    }

    [Fact]
    public void Validate_reports_DISP414_when_referenced_assemblies_define_same_policy()
    {
        var context = CreateContext(
            localPipeline: PipelineConfig.Empty,
            referencedContributions: Referenced(
                new ReferencedAssemblyContribution(
                    "OrdersContrib",
                    "global::MyApp.AppContext",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray<PerCommandMiddlewareFinding>.Empty,
                    ImmutableArray.Create(new PolicyFinding(
                        "global::MyApp.SharedPolicy",
                        ImmutableArray<MiddlewareRef>.Empty,
                        ImmutableArray<string>.Empty))),
                new ReferencedAssemblyContribution(
                    "BillingContrib",
                    "global::MyApp.AppContext",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray<PerCommandMiddlewareFinding>.Empty,
                    ImmutableArray.Create(new PolicyFinding(
                        "global::MyApp.SharedPolicy",
                        ImmutableArray<MiddlewareRef>.Empty,
                        ImmutableArray<string>.Empty)))));

        var diagnostics = new DiagnosticBag();

        new ReferencedContributionConflictValidator().Validate(context, diagnostics);

        var diagnostic = Assert.Single(diagnostics.ToImmutable());
        Assert.Equal("DISP414", diagnostic.Id);
    }

    [Fact]
    public void Validate_does_not_report_DISP414_for_referenced_policy_in_other_context()
    {
        var context = CreateContext(
            localPipeline: new PipelineConfig(
                ImmutableArray<MiddlewareRef>.Empty,
                ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
                ImmutableDictionary<string, PolicySpec>.Empty.Add(
                    "global::MyApp.SharedPolicy",
                    new PolicySpec(
                        "global::MyApp.SharedPolicy",
                        ImmutableArray<MiddlewareRef>.Empty,
                        ImmutableArray.Create("global::MyApp.CreateOrder")))),
            referencedContributions: Referenced(
                new ReferencedAssemblyContribution(
                    "MixedContrib",
                    null,
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray<PerCommandMiddlewareFinding>.Empty,
                    ImmutableArray.Create(new PolicyFinding(
                        "global::MyApp.SharedPolicy",
                        ImmutableArray<MiddlewareRef>.Empty,
                        ImmutableArray.Create("global::MyApp.CreateOrder"),
                        "global::OtherApp.OtherContext")))));

        var diagnostics = new DiagnosticBag();

        new ReferencedContributionConflictValidator().Validate(context, diagnostics);

        Assert.Empty(diagnostics.ToImmutable());
    }

    [Fact]
    public void Validate_skips_conflict_checks_for_non_host_projects()
    {
        var context = new GeneratorValidationContext.Builder(
                EmptyDiscovery(),
                new DiagnosticsCatalog())
            .WithHostGate(isHost: false)
            .WithExpectedContext(string.Empty)
            .WithLocalPipelineConfig(PipelineConfig.Empty)
            .WithReferencedContributions(Referenced(
                new ReferencedAssemblyContribution(
                    "OrdersContrib",
                    "global::MyApp.AppContext",
                    ImmutableArray<MiddlewareRef>.Empty,
                    ImmutableArray.Create(new PerCommandMiddlewareFinding(
                        "global::MyApp.CreateOrder",
                        ImmutableArray.Create(new MiddlewareRef("global::Orders.SharedMiddleware", 2)))),
                    ImmutableArray<PolicyFinding>.Empty)))
            .WithPipelineConfig(PipelineConfig.Empty)
            .Build();

        var diagnostics = new DiagnosticBag();

        new ReferencedContributionConflictValidator().Validate(context, diagnostics);

        Assert.Empty(diagnostics.ToImmutable());
    }

    private static GeneratorValidationContext CreateContext(
        PipelineConfig localPipeline,
        ReferencedAssemblyContributions referencedContributions)
    {
        return new GeneratorValidationContext.Builder(
                EmptyDiscovery(),
                new DiagnosticsCatalog())
            .WithHostGate(isHost: true)
            .WithExpectedContext("global::MyApp.AppContext")
            .WithLocalPipelineConfig(localPipeline)
            .WithReferencedContributions(referencedContributions)
            .WithPipelineConfig(PipelineConfig.Empty)
            .Build();
    }

    private static DiscoveryResult EmptyDiscovery()
    {
        return new DiscoveryResult(
            ImmutableArray<HandlerContract>.Empty,
            ImmutableArray<QueryHandlerContract>.Empty);
    }

    private static ReferencedAssemblyContributions Referenced(params ReferencedAssemblyContribution[] assemblies)
    {
        return new ReferencedAssemblyContributions(
            ImmutableArray.Create(assemblies),
            ImmutableArray<ReferencedHandlerContribution>.Empty);
    }
}
