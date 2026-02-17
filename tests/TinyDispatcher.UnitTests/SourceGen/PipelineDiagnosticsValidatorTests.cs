#nullable enable

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TinyDispatcher.SourceGen.Generator;
using Xunit;

namespace TinyDispatcher.UnitTest.SourceGen;

public sealed class PipelineDiagnosticsValidatorTests
{
    [Fact]
    public void DISP410_when_per_command_middleware_targets_unknown_command()
    {
        var (_, diags) = Run(CreateCompilationAllRefs(@"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
  public interface ICommand { }

  public interface ICommandHandler<TCommand, TContext> where TCommand : ICommand
  {
    Task HandleAsync(TCommand command, TContext ctx, CancellationToken ct = default);
  }

  namespace Pipeline
  {
    public interface ICommandPipelineRuntime<TCommand, TContext> where TCommand : ICommand
    {
      ValueTask NextAsync(TCommand command, TContext ctx, CancellationToken ct = default);
    }
  }

  public interface ICommandMiddleware<TCommand, TContext> where TCommand : ICommand
  {
    ValueTask InvokeAsync(
      TCommand command,
      TContext ctx,
      Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
      CancellationToken ct);
  }

  public sealed class TinyBootstrapp
  {
    public void UseMiddlewareFor<TCommand>(Type middlewareOpenGeneric) { }
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
  public sealed record Ctx;

  public sealed class KnownCmd : TinyDispatcher.ICommand { }
  public sealed class UnknownCmd : TinyDispatcher.ICommand { }

  // Only handler for KnownCmd (UnknownCmd has NO handler)
  public sealed class KnownHandler : TinyDispatcher.ICommandHandler<KnownCmd, Ctx>
  {
    public Task HandleAsync(KnownCmd command, Ctx ctx, CancellationToken ct = default) => Task.CompletedTask;
  }

  // Open generic middleware
  public sealed class Mw<TCommand, TContext> : TinyDispatcher.ICommandMiddleware<TCommand, TContext>
    where TCommand : TinyDispatcher.ICommand
  {
    public ValueTask InvokeAsync(
      TCommand command,
      TContext ctx,
      TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
      CancellationToken ct)
      => runtime.NextAsync(command, ctx, ct);
  }

  public static class Startup
  {
    public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
      => services.UseTinyDispatcher<Ctx>(tiny => tiny.UseMiddlewareFor<UnknownCmd>(typeof(Mw<,>)));
      // UnknownCmd has no handler => DISP410 (warning)
  }
}
"));

        Assert.Contains(diags, d => d.Id == "DISP410" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void DISP411_when_policy_targets_unknown_command()
    {
        var (_, diags) = Run(CreateCompilationAllRefs(@"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
  public interface ICommand { }

  public interface ICommandHandler<TCommand, TContext> where TCommand : ICommand
  {
    Task HandleAsync(TCommand command, TContext ctx, CancellationToken ct = default);
  }

  namespace Pipeline
  {
    public interface ICommandPipelineRuntime<TCommand, TContext> where TCommand : ICommand
    {
      ValueTask NextAsync(TCommand command, TContext ctx, CancellationToken ct = default);
    }
  }

  public interface ICommandMiddleware<TCommand, TContext> where TCommand : ICommand
  {
    ValueTask InvokeAsync(
      TCommand command,
      TContext ctx,
      Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
      CancellationToken ct);
  }

  [AttributeUsage(AttributeTargets.Class)]
  public sealed class TinyPolicyAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
  public sealed class ForCommandAttribute : Attribute
  {
    public ForCommandAttribute(Type t) { }
  }

  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
  public sealed class UseMiddlewareAttribute : Attribute
  {
    public UseMiddlewareAttribute(Type t) { }
  }

  public sealed class TinyBootstrapp
  {
    public void UseTinyPolicy<TPolicy>() { }
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
  public sealed record Ctx;

  public sealed class KnownCmd : TinyDispatcher.ICommand { }
  public sealed class UnknownCmd : TinyDispatcher.ICommand { }

  public sealed class KnownHandler : TinyDispatcher.ICommandHandler<KnownCmd, Ctx>
  {
    public Task HandleAsync(KnownCmd command, Ctx ctx, CancellationToken ct = default) => Task.CompletedTask;
  }

  public sealed class Mw<TCommand, TContext> : TinyDispatcher.ICommandMiddleware<TCommand, TContext>
    where TCommand : TinyDispatcher.ICommand
  {
    public ValueTask InvokeAsync(
      TCommand command,
      TContext ctx,
      TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
      CancellationToken ct)
      => runtime.NextAsync(command, ctx, ct);
  }

  [TinyDispatcher.TinyPolicy]
  [TinyDispatcher.UseMiddleware(typeof(Mw<,>))]
  [TinyDispatcher.ForCommand(typeof(UnknownCmd))]
  public sealed class PolicyTargetsUnknownCmd { }

  public static class Startup
  {
    public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
      => services.UseTinyDispatcher<Ctx>(tiny => tiny.UseTinyPolicy<PolicyTargetsUnknownCmd>());
      // Policy targets UnknownCmd which has no handler => DISP411 (warning)
  }
}
"));

        Assert.Contains(diags, d => d.Id == "DISP411" && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void DISP412_when_multiple_policies_target_same_command()
    {
        var (_, diags) = Run(CreateCompilationAllRefs(@"
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace TinyDispatcher
{
  public interface ICommand { }

  public interface ICommandHandler<TCommand, TContext> where TCommand : ICommand
  {
    Task HandleAsync(TCommand command, TContext ctx, CancellationToken ct = default);
  }

  namespace Pipeline
  {
    public interface ICommandPipelineRuntime<TCommand, TContext> where TCommand : ICommand
    {
      ValueTask NextAsync(TCommand command, TContext ctx, CancellationToken ct = default);
    }
  }

  public interface ICommandMiddleware<TCommand, TContext> where TCommand : ICommand
  {
    ValueTask InvokeAsync(
      TCommand command,
      TContext ctx,
      Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
      CancellationToken ct);
  }

  [AttributeUsage(AttributeTargets.Class)]
  public sealed class TinyPolicyAttribute : Attribute { }

  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
  public sealed class ForCommandAttribute : Attribute
  {
    public ForCommandAttribute(Type t) { }
  }

  [AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
  public sealed class UseMiddlewareAttribute : Attribute
  {
    public UseMiddlewareAttribute(Type t) { }
  }

  public sealed class TinyBootstrapp
  {
    public void UseTinyPolicy<TPolicy>() { }
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
  public sealed record Ctx;

  public sealed class Cmd : TinyDispatcher.ICommand { }

  public sealed class Handler : TinyDispatcher.ICommandHandler<Cmd, Ctx>
  {
    public Task HandleAsync(Cmd command, Ctx ctx, CancellationToken ct = default) => Task.CompletedTask;
  }

  public sealed class Mw<TCommand, TContext> : TinyDispatcher.ICommandMiddleware<TCommand, TContext>
    where TCommand : TinyDispatcher.ICommand
  {
    public ValueTask InvokeAsync(
      TCommand command,
      TContext ctx,
      TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, TContext> runtime,
      CancellationToken ct)
      => runtime.NextAsync(command, ctx, ct);
  }

  [TinyDispatcher.TinyPolicy]
  [TinyDispatcher.UseMiddleware(typeof(Mw<,>))]
  [TinyDispatcher.ForCommand(typeof(Cmd))]
  public sealed class PolicyA { }

  [TinyDispatcher.TinyPolicy]
  [TinyDispatcher.UseMiddleware(typeof(Mw<,>))]
  [TinyDispatcher.ForCommand(typeof(Cmd))]
  public sealed class PolicyB { }

  public static class Startup
  {
    public static void Configure(Microsoft.Extensions.DependencyInjection.IServiceCollection services)
      => services.UseTinyDispatcher<Ctx>(tiny =>
      {
        tiny.UseTinyPolicy<PolicyA>();
        tiny.UseTinyPolicy<PolicyB>();
      });
      // Two policies target Cmd => DISP412 (warning)
  }
}
"));

        Assert.Contains(diags, d => d.Id == "DISP412" && d.Severity == DiagnosticSeverity.Warning);
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
}
