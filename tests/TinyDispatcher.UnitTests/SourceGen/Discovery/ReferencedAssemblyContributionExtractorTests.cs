#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator.Models;
using TinyDispatcher.SourceGen.Generator.Extraction;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.Discovery;

public sealed class ReferencedAssemblyContributionExtractorTests
{
    [Fact]
    public void Extract_reads_handler_contributions_from_referenced_assemblies()
    {
        var referencedAssembly = CreateMetadataReference(@"
using TinyDispatcher;

[assembly: TinyDispatcherHandlerContributionAttribute(
    typeof(ExternalApp.CreateOrder),
    typeof(ExternalApp.CreateOrderHandler),
    typeof(ExternalApp.AppContext))]

namespace ExternalApp
{
    public sealed class AppContext { }
    public sealed class CreateOrder : ICommand { }
    public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, AppContext>
    {
        public System.Threading.Tasks.Task HandleAsync(CreateOrder command, AppContext context, System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;
    }
}
");

        var compilation = CSharpCompilation.Create(
            assemblyName: "Host",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText("namespace Host { public sealed class Marker { } }") },
            references: CreateBaseReferences().Concat(new[] { referencedAssembly }),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var contributions = new ReferencedAssemblyContributionExtractor().Extract(compilation);
        var assembly = Assert.Single(
            contributions.Assemblies,
            candidate => candidate.AssemblyName == "ExternalContrib");

        var command = Assert.Single(
            assembly.Commands,
            c => c.MessageTypeFqn == "global::ExternalApp.CreateOrder");
        Assert.Equal("global::ExternalApp.CreateOrder", command.MessageTypeFqn);
        Assert.Equal("global::ExternalApp.CreateOrderHandler", command.HandlerTypeFqn);
        Assert.Equal("global::ExternalApp.AppContext", command.ContextTypeFqn);
        Assert.Equal("ExternalContrib", assembly.AssemblyName);
    }

    [Fact]
    public void Extract_reads_context_per_command_pipeline_and_policy_contributions_from_referenced_assemblies()
    {
        var referencedAssembly = CreateMetadataReference(@"
using TinyDispatcher;

[assembly: TinyDispatcherAssemblyContextContributionAttribute(typeof(ExternalApp.AppContext))]
[assembly: TinyDispatcherPipelineContributionAttribute(
    new System.Type[] { typeof(ExternalApp.OrderMiddleware<,>) },
    CommandType = typeof(ExternalApp.CreateOrder))]
[assembly: TinyDispatcherPolicyContributionAttribute(
    typeof(ExternalApp.OrderPolicy),
    new System.Type[] { typeof(ExternalApp.PolicyMiddleware<,>) },
    new System.Type[] { typeof(ExternalApp.CreateOrder) })]

namespace ExternalApp
{
    public sealed class AppContext { }
    public sealed class CreateOrder : ICommand { }
    public sealed class OrderPolicy { }
    public sealed class OrderMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext> where TCommand : ICommand
    {
        public System.Threading.Tasks.ValueTask InvokeAsync(TCommand command, TContext context, TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime, System.Threading.CancellationToken ct)
            => runtime.NextAsync(command, context, ct);
    }
    public sealed class PolicyMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext> where TCommand : ICommand
    {
        public System.Threading.Tasks.ValueTask InvokeAsync(TCommand command, TContext context, TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime, System.Threading.CancellationToken ct)
            => runtime.NextAsync(command, context, ct);
    }
}
");

        var compilation = CSharpCompilation.Create(
            assemblyName: "Host",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText("namespace Host { public sealed class Marker { } }") },
            references: CreateBaseReferences().Concat(new[] { referencedAssembly }),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var contributions = new ReferencedAssemblyContributionExtractor().Extract(compilation);
        var assembly = Assert.Single(
            contributions.Assemblies,
            candidate => candidate.AssemblyName == "ExternalContrib");

        Assert.Equal("global::ExternalApp.AppContext", assembly.ContextTypeFqn);
        Assert.True(assembly.PerCommand.TryGetValue("global::ExternalApp.CreateOrder", out var perCommand));
        Assert.Equal("global::ExternalApp.OrderMiddleware", Assert.Single(perCommand).OpenTypeFqn);

        Assert.True(assembly.Policies.TryGetValue("global::ExternalApp.OrderPolicy", out var policy));
        Assert.Equal("global::ExternalApp.PolicyMiddleware", Assert.Single(policy.Middlewares).OpenTypeFqn);
        Assert.Equal("global::ExternalApp.CreateOrder", Assert.Single(policy.Commands));
    }

    [Fact]
    public void Extract_groups_contributions_per_referenced_assembly()
    {
        var first = CreateMetadataReference(@"
using TinyDispatcher;
[assembly: TinyDispatcherAssemblyContextContributionAttribute(typeof(Orders.OrderContext))]
[assembly: TinyDispatcherHandlerContributionAttribute(typeof(Orders.CreateOrder), typeof(Orders.CreateOrderHandler), typeof(Orders.OrderContext))]
namespace Orders
{
    public sealed class OrderContext { }
    public sealed class CreateOrder : ICommand { }
    public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, OrderContext>
    {
        public System.Threading.Tasks.Task HandleAsync(CreateOrder command, OrderContext context, System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;
    }
}
", "OrdersContrib");

        var second = CreateMetadataReference(@"
using TinyDispatcher;
[assembly: TinyDispatcherAssemblyContextContributionAttribute(typeof(Billing.BillingContext))]
[assembly: TinyDispatcherHandlerContributionAttribute(typeof(Billing.CapturePayment), typeof(Billing.CapturePaymentHandler), typeof(Billing.BillingContext))]
namespace Billing
{
    public sealed class BillingContext { }
    public sealed class CapturePayment : ICommand { }
    public sealed class CapturePaymentHandler : ICommandHandler<CapturePayment, BillingContext>
    {
        public System.Threading.Tasks.Task HandleAsync(CapturePayment command, BillingContext context, System.Threading.CancellationToken cancellationToken = default)
            => System.Threading.Tasks.Task.CompletedTask;
    }
}
", "BillingContrib");

        var compilation = CSharpCompilation.Create(
            assemblyName: "Host",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText("namespace Host { public sealed class Marker { } }") },
            references: CreateBaseReferences().Concat(new[] { first, second }),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var contributions = new ReferencedAssemblyContributionExtractor().Extract(compilation);

        Assert.Collection(
            contributions.Assemblies
                .Where(a => a.AssemblyName is "BillingContrib" or "OrdersContrib")
                .OrderBy(a => a.AssemblyName),
            orders =>
            {
                Assert.Equal("BillingContrib", orders.AssemblyName);
                Assert.Equal("global::Billing.BillingContext", orders.ContextTypeFqn);
                Assert.Equal("global::Billing.CapturePayment", Assert.Single(orders.Commands).MessageTypeFqn);
            },
            billing =>
            {
                Assert.Equal("OrdersContrib", billing.AssemblyName);
                Assert.Equal("global::Orders.OrderContext", billing.ContextTypeFqn);
                Assert.Equal("global::Orders.CreateOrder", Assert.Single(billing.Commands).MessageTypeFqn);
            });
    }

    private static MetadataReference CreateMetadataReference(string source, string assemblyName = "ExternalContrib")
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: CreateBaseReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Select(d => d.ToString())));
        stream.Position = 0;
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static IEnumerable<MetadataReference> CreateBaseReferences()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Cast<MetadataReference>()
            .Distinct();
    }
}
