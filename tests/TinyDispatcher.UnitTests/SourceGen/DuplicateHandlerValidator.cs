#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTest.SourceGen;

public sealed class DuplicateHandlersDiagnosticsTests
{
    [Fact]
    public void Emits_DISP101_for_duplicate_command_handlers()
    {
        var (_, diags) = Run(@"
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
  public interface ICommand { }
  public interface ICommandHandler<TCommand, TContext> where TCommand : ICommand
  {
    Task HandleAsync(TCommand command, TContext ctx, CancellationToken ct);
  }
  public sealed class TinyBootstrapp { }
}

namespace Microsoft.Extensions.DependencyInjection
{
  public interface IServiceCollection { }
  public static class ServiceCollectionExtensions
  {
    public static IServiceCollection UseTinyDispatcher<TContext>(this IServiceCollection s, System.Action<TinyDispatcher.TinyBootstrapp> t) => s;
  }
}

namespace ConsoleApp
{
  public sealed record Ctx;
  public sealed record Ping : TinyDispatcher.ICommand;

  public sealed class H1 : TinyDispatcher.ICommandHandler<Ping, Ctx>
  { public Task HandleAsync(Ping c, Ctx x, CancellationToken ct) => Task.CompletedTask; }

  public sealed class H2 : TinyDispatcher.ICommandHandler<Ping, Ctx>
  { public Task HandleAsync(Ping c, Ctx x, CancellationToken ct) => Task.CompletedTask; }

  public static class Startup
  {
    public static void Configure(IServiceCollection services)
      => services.UseTinyDispatcher<Ctx>(_ => { });
  }
}
");
        Assert.Contains(diags, d => d.Id == "DISP101");
    }

    [Fact]
    public void Emits_DISP201_for_duplicate_query_handlers()
    {
        var (_, diags) = Run(@"
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
  public interface IQuery<TResult> { }

  // ✅ Must be `2`, not `3`
  public interface IQueryHandler<TQuery, TResult> where TQuery : IQuery<TResult>
  {
    Task<TResult> HandleAsync(TQuery query, CancellationToken ct);
  }

  public sealed class TinyBootstrapp { }
}

namespace Microsoft.Extensions.DependencyInjection
{
  public interface IServiceCollection { }
  public static class ServiceCollectionExtensions
  {
    public static IServiceCollection UseTinyDispatcher<TContext>(this IServiceCollection s, System.Action<TinyDispatcher.TinyBootstrapp> t) => s;
  }
}

namespace ConsoleApp
{
  public sealed record Ctx;

  public sealed record GetN() : TinyDispatcher.IQuery<int>;

  public sealed class H1 : TinyDispatcher.IQueryHandler<GetN, int>
  { public Task<int> HandleAsync(GetN q, CancellationToken ct) => Task.FromResult(1); }

  public sealed class H2 : TinyDispatcher.IQueryHandler<GetN, int>
  { public Task<int> HandleAsync(GetN q, CancellationToken ct) => Task.FromResult(2); }

  public static class Startup
  {
    public static void Configure(IServiceCollection services)
      => services.UseTinyDispatcher<Ctx>(_ => { });
  }
}
");
        Assert.Contains(diags, d => d.Id == "DISP201");
    }


    private static (GeneratorDriver Driver, Diagnostic[] Diagnostics) Run(string source)
    {
        var compilation = CreateCompilation(source);
        var generator = new Generator();
        GeneratorDriver driver = CSharpGeneratorDriver.Create(generator);

        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var diags);
        return (driver, diags.ToArray());
    }

    private static CSharpCompilation CreateCompilation(string source)
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
}
