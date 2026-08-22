#nullable enable

using System;
using System.Reflection;
using System.Reflection.Emit;
using TinyDispatcher.PipelineMaps;
using Xunit;

[assembly: TinyDispatcher.TinyDispatcherPipelineMapAttribute(
    typeof(TinyDispatcher.UnitTests.PipelineMaps.DispatcherPipelineMapsTests.TestPipeline),
    typeof(TinyDispatcher.UnitTests.PipelineMaps.DispatcherPipelineMapsTests.TestContext),
    new[] { typeof(TinyDispatcher.UnitTests.PipelineMaps.DispatcherPipelineMapsTests.TestCommand) },
    new[]
    {
        typeof(TinyDispatcher.UnitTests.PipelineMaps.DispatcherPipelineMapsTests.GlobalMiddleware),
        typeof(TinyDispatcher.UnitTests.PipelineMaps.DispatcherPipelineMapsTests.PolicyMiddleware),
        typeof(TinyDispatcher.UnitTests.PipelineMaps.DispatcherPipelineMapsTests.OperationMiddleware)
    },
    1,
    typeof(TinyDispatcher.UnitTests.PipelineMaps.DispatcherPipelineMapsTests.TestPolicy),
    1)]

namespace TinyDispatcher.UnitTests.PipelineMaps;

public sealed class DispatcherPipelineMapsTests
{
    [Fact]
    public void Get_returns_pipeline_maps_from_all_loaded_assemblies()
    {
        AddPipelineMapAssembly("FirstPipelineMapAssembly", typeof(FirstExternalPipeline));
        AddPipelineMapAssembly("SecondPipelineMapAssembly", typeof(SecondExternalPipeline));

        var maps = DispatcherPipelineMaps.Get();

        Assert.Contains(maps, map => map.PipelineType == typeof(FirstExternalPipeline));
        Assert.Contains(maps, map => map.PipelineType == typeof(SecondExternalPipeline));
    }

    [Fact]
    public void Get_returns_pipeline_maps_without_exposing_attribute_metadata()
    {
        var maps = DispatcherPipelineMaps.Get(typeof(DispatcherPipelineMapsTests).Assembly);

        var map = Assert.Single(maps);

        Assert.Equal(typeof(TestPipeline), map.PipelineType);
        Assert.Equal(typeof(TestContext), map.ContextType);
        Assert.Equal(new[] { typeof(TestCommand) }, map.CommandTypes);
        Assert.Equal(new[] { typeof(GlobalMiddleware) }, map.GlobalMiddlewares);
        Assert.Equal(typeof(TestPolicy), map.PolicyType);
        Assert.Equal(new[] { typeof(PolicyMiddleware) }, map.PolicyMiddlewares);
        Assert.Equal(new[] { typeof(OperationMiddleware) }, map.OperationMiddlewares);
    }

    [Fact]
    public void Get_rejects_a_null_assembly()
    {
        var exception = Assert.Throws<ArgumentNullException>(() =>
            DispatcherPipelineMaps.Get(null!));

        Assert.Equal("assembly", exception.ParamName);
    }

    public sealed class TestPipeline;

    public sealed class TestContext;

    public sealed class TestCommand;

    public sealed class TestPolicy;

    public sealed class GlobalMiddleware;

    public sealed class PolicyMiddleware;

    public sealed class OperationMiddleware;

    public sealed class FirstExternalPipeline;

    public sealed class SecondExternalPipeline;

    private static void AddPipelineMapAssembly(string assemblyName, Type pipelineType)
    {
        var assembly = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName(assemblyName),
            AssemblyBuilderAccess.Run);
        var constructor = typeof(TinyDispatcherPipelineMapAttribute).GetConstructor(
            new[]
            {
                typeof(Type),
                typeof(Type),
                typeof(Type[]),
                typeof(Type[]),
                typeof(int),
                typeof(Type),
                typeof(int)
            }) ?? throw new InvalidOperationException("Pipeline-map attribute constructor was not found.");

        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            constructor,
            new object[]
            {
                pipelineType,
                typeof(TestContext),
                new[] { typeof(TestCommand) },
                Array.Empty<Type>(),
                0,
                null!,
                0
            }));
    }
}
