#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Emitters.ModuleInitializer;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.Emitters;

public sealed class ModuleInitializerEmitterTests
{
    [Fact]
    public void Planner_does_not_emit_when_no_handlers_exist()
    {
        var discovery = new DiscoveryResult(
            Commands: ImmutableArray<HandlerContract>.Empty,
            Queries: ImmutableArray<QueryHandlerContract>.Empty);

        var options = new GeneratorOptions(
            GeneratedNamespace: "MyApp.Generated",
            EmitDiExtensions: false,
            EmitHandlerRegistrations: false,
            IncludeNamespacePrefix: null,
            CommandContextType: "global::MyApp.AppContext",
            EmitPipelineMap: false,
            PipelineMapFormat: null);

        var plan = ModuleInitializerPlanner.Build(discovery, options);

        Assert.False(plan.ShouldEmit);
    }

    [Fact]
    public void Planner_emits_when_any_command_handler_exists()
    {
        var discovery = new DiscoveryResult(
            Commands: ImmutableArray.Create(new HandlerContract(
                MessageTypeFqn: "global::MyApp.Cmd",
                HandlerTypeFqn: "global::MyApp.CmdHandler")),
            Queries: ImmutableArray<QueryHandlerContract>.Empty);

        var options = new GeneratorOptions(
            GeneratedNamespace: "MyApp.Generated",
            EmitDiExtensions: false,
            EmitHandlerRegistrations: false,
            IncludeNamespacePrefix: null,
            CommandContextType: "global::MyApp.AppContext",
            EmitPipelineMap: false,
            PipelineMapFormat: null);

        var plan = ModuleInitializerPlanner.Build(discovery, options);

        Assert.True(plan.ShouldEmit);
    }

    [Fact]
    public void Writer_emits_expected_shape_and_namespaces()
    {
        var plan = new ModuleInitializerPlan(
            GeneratedNamespace: "MyApp.Generated",
            CoreNamespace: "TinyDispatcher",
            ShouldEmit: true);

        var source = ModuleInitializerSourceWriter.Write(plan);

        Assert.Contains("namespace MyApp.Generated", source);
        Assert.Contains("internal static class DispatcherModuleInitializer", source);
        Assert.Contains("[ModuleInitializer]", source);
        Assert.Contains("global::TinyDispatcher.DispatcherPipelineBootstrap.AddContribution", source);
        Assert.Contains("global::MyApp.Generated.ThisAssemblyPipelineContribution.Add", source);
    }
}
