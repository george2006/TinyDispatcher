#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTest.SourceGen;

public sealed class ContextConsistencyDiagnosticsTests
{
    [Fact]
    public void DISP110_when_UseTinyDispatcher_is_called_twice_with_different_contexts()
    {
        var (_, diags) = Run(CreateCompilationAllRefs(@"
using System;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
  public sealed class TinyBootstrapp
  {
    public void UsePolicy<T>() { }
  }
}

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

namespace ConsoleApp
{
  public sealed class CtxA { }
  public sealed class CtxB { }

  public static class Startup
  {
    public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
      services.UseTinyDispatcher<CtxA>(tiny => tiny.UsePolicy<int>());
      services.UseTinyDispatcher<CtxB>(tiny => tiny.UsePolicy<int>());
    }
  }
}
"));

        Assert.Contains(diags, d => d.Id == "DISP110");
    }

    [Fact]
    public void DISP110_when_UseTinyDispatcher_is_called_twice_with_same_context()
    {
        var (_, diags) = Run(CreateCompilationAllRefs(@"
using System;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
  public sealed class TinyBootstrapp
  {
    public void UsePolicy<T>() { }
  }
}

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

namespace ConsoleApp
{
  public sealed class Ctx { }

  public static class Startup
  {
    public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
    {
      services.UseTinyDispatcher<Ctx>(tiny => tiny.UsePolicy<int>());
      services.UseTinyDispatcher<Ctx>(tiny => tiny.UsePolicy<int>());
    }
  }
}
"));

        Assert.Contains(diags, d => d.Id == "DISP110");
    }

    [Fact]
    public void No_DISP110_when_UseTinyDispatcher_is_called_once()
    {
        var (_, diags) = Run(CreateCompilationAllRefs(@"
using System;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
  public sealed class TinyBootstrapp
  {
    public void UsePolicy<T>() { }
  }
}

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

namespace ConsoleApp
{
  public sealed class Ctx { }

  public static class Startup
  {
    public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
      => services.UseTinyDispatcher<Ctx>(tiny => tiny.UsePolicy<int>());
  }
}
"));

        Assert.DoesNotContain(diags, d => d.Id == "DISP110");
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

        // Ensure the source generator assembly is referenced.
        refs.Add(MetadataReference.CreateFromFile(typeof(Generator).Assembly.Location));

        return CSharpCompilation.Create(
            assemblyName: "Tests",
            syntaxTrees: new[] { CSharpSyntaxTree.ParseText(source) },
            references: refs,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
