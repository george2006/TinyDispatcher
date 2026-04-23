#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class HostGateTests
{
    [Fact]
    public void Does_not_emit_TinyDispatcherPipeline_g_when_no_UseTinyDispatcher_call_exists()
    {
        var source = @"
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TinyDispatcher
{
    public interface ICommand { }

    public interface ICommandHandler<TCommand, TContext> where TCommand : ICommand
    {
        Task HandleAsync(TCommand command, TContext context, CancellationToken ct);
    }
}
";

        var compilation = CreateCompilation(source);

        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        // No sourcegen errors expected for this input
        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var run = driver.GetRunResult();
        var generatedNames = run.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.HintName)
            .ToArray();

        // Sanity: generator produced *something* (baseline artifacts may evolve)
        Assert.NotEmpty(generatedNames);

        // Contract: host artifacts must NOT exist without UseTinyDispatcher(...)
        Assert.DoesNotContain(
            generatedNames,
            n => string.Equals(n, "TinyDispatcherPipeline.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Does_not_treat_unrelated_UseTinyDispatcher_extension_as_host_bootstrap()
    {
        var source = @"
using System;
using Microsoft.Extensions.DependencyInjection;
using MyFramework;

namespace Microsoft.Extensions.DependencyInjection
{
    public interface IServiceCollection { }
}

namespace MyFramework
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection UseTinyDispatcher<TContext>(
            this IServiceCollection services,
            Action<object> configure)
            => services;
    }
}

namespace ConsoleApp
{
    public sealed class Ctx { }

    public static class Startup
    {
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<Ctx>(_ => { });
        }
    }
}
";

        var compilation = CreateCompilation(source);

        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Id == "DISP110" || d.Id == "DISP111");

        var run = driver.GetRunResult();
        var generatedNames = run.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.HintName)
            .ToArray();

        Assert.DoesNotContain(
            generatedNames,
            n => string.Equals(n, "TinyDispatcherPipeline.g.cs", StringComparison.Ordinal));
        Assert.DoesNotContain(
            generatedNames,
            n => string.Equals(n, "TinyDispatcherPipelineMap.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Contributing_assembly_emits_structured_contribution_without_emitting_final_pipeline()
    {
        var source = @"
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;

namespace MyApp
{
    public sealed class AppContext { }
    public sealed record CreateOrder() : ICommand;

    public sealed class CreateOrderHandler : ICommandHandler<CreateOrder, AppContext>
    {
        public Task HandleAsync(CreateOrder command, AppContext context, CancellationToken ct)
            => Task.CompletedTask;
    }
}
";

        var compilation = CreateCompilation(source);

        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diagnostics);

        Assert.DoesNotContain(diagnostics, d => d.Severity == DiagnosticSeverity.Error);

        var run = driver.GetRunResult();
        var generatedNames = run.Results
            .SelectMany(r => r.GeneratedSources)
            .Select(s => s.HintName)
            .ToArray();

        Assert.Contains("ThisAssemblyContribution.g.cs", generatedNames);
        Assert.Contains("DispatcherModuleInitializer.g.cs", generatedNames);
        Assert.DoesNotContain("TinyDispatcherPipeline.g.cs", generatedNames);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var refs =
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToList();

        // Ensure generator assembly is referenced (tests usually already reference it, but keep explicit)
        refs.Add(MetadataReference.CreateFromFile(typeof(Generator).Assembly.Location));

        return CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
