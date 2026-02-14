#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTest.SourceGen;

public sealed class MiddlewareDiagnosticsTests
{
    [Fact]
    public void DISP301_when_middleware_is_not_open_generic_definition()
    {
        var (_, diags) = Run(CreateCompilationAllRefs(@"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
  public interface ICommand { }

  namespace Pipeline
  {
    public interface ICommandPipelineRuntime<TCommand, TContext> where TCommand : ICommand
    {
      ValueTask NextAsync(TCommand command, TContext ctx, CancellationToken ct = default);
    }
  }

  public interface ICommandMiddleware<TCommand,TContext> where TCommand : ICommand
  {
    ValueTask InvokeAsync(
      TCommand c,
      TContext ctx,
      Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
      CancellationToken ct);
  }

  public sealed class TinyBootstrapp
  {
    public void UseGlobalMiddleware(Type t) { }
  }
}

namespace Microsoft.Extensions.DependencyInjection
{
  public static class ServiceCollectionExtensions
  {
    public static IServiceCollection UseTinyDispatcher<TContext>(
      this IServiceCollection services,
      Action<TinyDispatcher.TinyBootstrapp> tiny)
      => services;
  }
}

namespace ConsoleApp
{
  public sealed record Ctx;
  public sealed class Cmd : TinyDispatcher.ICommand { }

  // NON-GENERIC middleware => should trigger DISP301
  public sealed class NotOpenGenericMw : TinyDispatcher.ICommandMiddleware<Cmd, Ctx>
  {
    public ValueTask InvokeAsync(
      Cmd c,
      Ctx ctx,
      TinyDispatcher.Pipeline.ICommandPipelineRuntime<Cmd, Ctx> runtime,
      CancellationToken ct)
      => runtime.NextAsync(c, ctx, ct);
  }

  public static class Startup
  {
    public static void Configure(IServiceCollection services)
      => services.UseTinyDispatcher<Ctx>(tiny => tiny.UseGlobalMiddleware(typeof(NotOpenGenericMw)));
  }
}
"));

        Assert.Contains(diags, d => d.Id == "DISP301");
    }



    [Fact]
    public void DISP302_when_middleware_arity_is_not_1_or_2()
    {
        var (_, diags) = Run(CreateCompilationAllRefs(@"
using System;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
  public interface ICommand { }
  public sealed class TinyBootstrapp { public void UseGlobalMiddleware(Type t) { } }
}

namespace Microsoft.Extensions.DependencyInjection
{
  public interface IServiceCollection { }
  public static class ServiceCollectionExtensions
  {
    public static IServiceCollection UseTinyDispatcher<TContext>(this IServiceCollection s, Action<TinyDispatcher.TinyBootstrapp> t) => s;
  }
}

namespace ConsoleApp
{
  public sealed record Ctx;
  public sealed class Mw<T1,T2,T3> { }

  public static class Startup
  {
    public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
      => services.UseTinyDispatcher<Ctx>(tiny => tiny.UseGlobalMiddleware(typeof(Mw<,,>))); // arity 3 => DISP302
  }
}
"));

        Assert.Contains(diags, d => d.Id == "DISP302");
    }

    [Fact]
    public void DISP303_when_ICommandMiddleware_cannot_be_resolved_from_compilation()
    {
        // IMPORTANT: This compilation deliberately excludes TinyDispatcher runtime assembly refs,
        // so GetTypeByMetadataName("TinyDispatcher.ICommandMiddleware`2") returns null => DISP303.
        var (_, diags) = Run(CreateCompilationWithoutTinyDispatcherRuntime(@"
using System;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
  public interface ICommand { }
  public sealed class TinyBootstrapp { public void UseGlobalMiddleware(Type t) { } }
}

namespace Microsoft.Extensions.DependencyInjection
{
  public interface IServiceCollection { }
  public static class ServiceCollectionExtensions
  {
    public static IServiceCollection UseTinyDispatcher<TContext>(this IServiceCollection s, Action<TinyDispatcher.TinyBootstrapp> t) => s;
  }
}

namespace ConsoleApp
{
  public sealed record Ctx;

  // arity-1 open generic (forces generator to look up ICommandMiddleware`2 in compilation)
  public sealed class ClosedMw<TCommand> where TCommand : TinyDispatcher.ICommand { }

  public static class Startup
  {
    public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
      => services.UseTinyDispatcher<Ctx>(tiny => tiny.UseGlobalMiddleware(typeof(ClosedMw<>)));
  }
}
"));

        Assert.Contains(diags, d => d.Id == "DISP303");
    }

    private static (GeneratorDriver Driver, Diagnostic[] Diagnostics) Run(CSharpCompilation compilation)
    {
        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diags);
        return (driver, diags.ToArray());
    }

    private static CSharpCompilation CreateCompilationAllRefs(string source)
    {
        var refs =
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .ToList();

        refs.Add(MetadataReference.CreateFromFile(typeof(Generator).Assembly.Location));

        return CSharpCompilation.Create(
            "Tests",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static CSharpCompilation CreateCompilationWithoutTinyDispatcherRuntime(string source)
    {
        var refs =
            AppDomain.CurrentDomain.GetAssemblies()
                .Where(a =>
                    !a.IsDynamic &&
                    !string.IsNullOrWhiteSpace(a.Location) &&
                    // Exclude the runtime assembly that would make ICommandMiddleware`2 resolvable
                    !string.Equals(a.GetName().Name, "TinyDispatcher", StringComparison.Ordinal))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .ToList();

        // Ensure generator assembly stays referenced
        refs.Add(MetadataReference.CreateFromFile(typeof(Generator).Assembly.Location));

        return CSharpCompilation.Create(
            "Tests.NoTinyDispatcherRuntime",
            new[] { CSharpSyntaxTree.ParseText(source) },
            refs,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
