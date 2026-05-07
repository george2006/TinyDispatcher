#nullable enable

using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class ReferencedContributionGeneratorDiagnosticsTests
{
    [Fact]
    public void Emits_DISP413_for_repeated_referenced_pipeline_contribution_declarations()
    {
        var referencedAssembly = CreateMetadataReference(@"
using TinyDispatcher;

[assembly: TinyDispatcherAssemblyContextContributionAttribute(typeof(ExternalApp.AppContext))]
[assembly: TinyDispatcherPipelineContributionAttribute(
    new System.Type[] { typeof(ExternalApp.FirstMiddleware<,>) },
    CommandType = typeof(ExternalApp.CreateOrder))]
[assembly: TinyDispatcherPipelineContributionAttribute(
    new System.Type[] { typeof(ExternalApp.SecondMiddleware<,>) },
    CommandType = typeof(ExternalApp.CreateOrder))]

namespace ExternalApp
{
    public sealed class AppContext { }
    public sealed class CreateOrder : ICommand { }

    public sealed class FirstMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public System.Threading.Tasks.ValueTask InvokeAsync(
            TCommand command,
            TContext context,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            System.Threading.CancellationToken ct)
            => runtime.NextAsync(command, context, ct);
    }

    public sealed class SecondMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public System.Threading.Tasks.ValueTask InvokeAsync(
            TCommand command,
            TContext context,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            System.Threading.CancellationToken ct)
            => runtime.NextAsync(command, context, ct);
    }
}
");

        var (_, diagnostics) = Run(CreateHostCompilation(referencedAssembly));

        Assert.Contains(diagnostics, d => d.Id == "DISP413");
    }

    [Fact]
    public void Emits_DISP414_for_repeated_referenced_policy_contribution_declarations()
    {
        var referencedAssembly = CreateMetadataReference(@"
using TinyDispatcher;

[assembly: TinyDispatcherAssemblyContextContributionAttribute(typeof(ExternalApp.AppContext))]
[assembly: TinyDispatcherPolicyContributionAttribute(
    typeof(ExternalApp.OrderPolicy),
    new System.Type[] { typeof(ExternalApp.PolicyMiddleware<,>) },
    new System.Type[] { typeof(ExternalApp.CreateOrder) })]
[assembly: TinyDispatcherPolicyContributionAttribute(
    typeof(ExternalApp.OrderPolicy),
    new System.Type[] { typeof(ExternalApp.PolicyMiddleware<,>) },
    new System.Type[] { typeof(ExternalApp.CreateOrder) })]

namespace ExternalApp
{
    public sealed class AppContext { }
    public sealed class CreateOrder : ICommand { }
    public sealed class OrderPolicy { }

    public sealed class PolicyMiddleware<TCommand, TContext> : ICommandMiddleware<TCommand, TContext>
        where TCommand : ICommand
    {
        public System.Threading.Tasks.ValueTask InvokeAsync(
            TCommand command,
            TContext context,
            TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
            System.Threading.CancellationToken ct)
            => runtime.NextAsync(command, context, ct);
    }
}
");

        var (_, diagnostics) = Run(CreateHostCompilation(referencedAssembly));

        Assert.Contains(diagnostics, d => d.Id == "DISP414");
    }

    private static MetadataReference CreateMetadataReference(string source, string assemblyName = "ExternalContrib")
    {
        var compilation = CSharpCompilation.Create(
            assemblyName: assemblyName,
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: CreateReferences(),
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics.Select(d => d.ToString())));
        stream.Position = 0;
        return MetadataReference.CreateFromImage(stream.ToArray());
    }

    private static CSharpCompilation CreateHostCompilation(MetadataReference referencedAssembly)
    {
        var source = @"
using System;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection { }

    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseTinyDispatcher<TContext>(
            this IServiceCollection services,
            Action<TinyDispatcher.TinyBootstrapp> tiny)
            => services;
    }
}

namespace HostApp
{
    public static class Startup
    {
        public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
            => services.UseTinyDispatcher<ExternalApp.AppContext>(_ => { });
    }
}";

        return CSharpCompilation.Create(
            "Host",
            new[] { CSharpSyntaxTree.ParseText(source) },
            CreateReferences().Concat(new[] { referencedAssembly }),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static (GeneratorDriver Driver, Diagnostic[] Diagnostics) Run(CSharpCompilation compilation)
    {
        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);
        return (driver, diagnostics.ToArray());
    }

    private static MetadataReference[] CreateReferences()
    {
        return AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
            .Select(a => MetadataReference.CreateFromFile(a.Location))
            .Concat(new[] { MetadataReference.CreateFromFile(typeof(Generator).Assembly.Location) })
            .Distinct()
            .ToArray();
    }
}
