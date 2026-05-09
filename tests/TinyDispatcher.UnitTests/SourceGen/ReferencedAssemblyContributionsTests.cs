#nullable enable

using System.Collections.Immutable;
using System.Linq;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class ReferencedAssemblyContributionsTests
{
    [Fact]
    public void EnumerateCommands_filters_handlers_by_expected_context()
    {
        var contributions = new ReferencedAssemblyContributions(
            ImmutableArray.Create(new ReferencedAssemblyContribution(
                    AssemblyName: "ExternalApp",
                    ContextTypeFqn: null,
                    Globals: ImmutableArray<MiddlewareRef>.Empty,
                    PerCommandMiddlewareFindings: ImmutableArray<PerCommandMiddlewareFinding>.Empty,
                    PolicyFindings: ImmutableArray<PolicyFinding>.Empty,
                    Handlers: ImmutableArray.Create(
                        new ReferencedHandlerContribution(
                            ContextTypeFqn: null,
                            Handler: new HandlerContract(
                                MessageTypeFqn: "global::ExternalApp.CreateOrder",
                                HandlerTypeFqn: "global::ExternalApp.CreateOrderHandler",
                                ContextTypeFqn: "global::ExternalApp.OrderContext")),
                        new ReferencedHandlerContribution(
                            ContextTypeFqn: null,
                            Handler: new HandlerContract(
                                MessageTypeFqn: "global::ExternalApp.CapturePayment",
                                HandlerTypeFqn: "global::ExternalApp.CapturePaymentHandler",
                                ContextTypeFqn: "global::ExternalApp.BillingContext"))))));

        var commands = contributions
            .EnumerateCommands("global::ExternalApp.OrderContext")
            .ToArray();

        var command = Assert.Single(commands);
        Assert.Equal("global::ExternalApp.CreateOrder", command.MessageTypeFqn);
    }
}
