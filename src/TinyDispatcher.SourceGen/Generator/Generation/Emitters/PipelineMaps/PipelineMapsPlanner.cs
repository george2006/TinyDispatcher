#nullable enable

using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;

using TinyDispatcher.SourceGen.Generator.Options;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;

internal static class PipelineMapsPlanner
{
    public static PipelineMapsPlan Build(
        DiscoveryResult discovery,
        PipelineContributions contributions,
        GeneratorOptions options)
    {
        if (!options.EmitPipelineMap)
        {
            return PipelineMapsPlan.Empty;
        }

        if (string.IsNullOrWhiteSpace(options.CommandContextType))
        {
            return PipelineMapsPlan.Empty;
        }

        var formats = PipelineMapOutputFormats.ParseOrDefault(options.PipelineMapFormat);
        var inspector = new PipelineMapInspector(contributions, options);
        var descriptors = ImmutableArray.CreateBuilder<PipelineDescriptor>(
            discovery.Commands.Length + discovery.Queries.Length);

        AddCommands(descriptors, discovery.Commands, inspector);
        AddQueries(descriptors, discovery.Queries, inspector);

        return new PipelineMapsPlan(descriptors.ToImmutable(), formats);
    }

    private static void AddCommands(
        ImmutableArray<PipelineDescriptor>.Builder descriptors,
        ImmutableArray<HandlerContract> handlers,
        PipelineMapInspector inspector)
    {
        for (var i = 0; i < handlers.Length; i++)
        {
            descriptors.Add(inspector.InspectCommand(handlers[i]));
        }
    }

    private static void AddQueries(
        ImmutableArray<PipelineDescriptor>.Builder descriptors,
        ImmutableArray<QueryHandlerContract> handlers,
        PipelineMapInspector inspector)
    {
        for (var i = 0; i < handlers.Length; i++)
        {
            descriptors.Add(inspector.InspectQuery(handlers[i]));
        }
    }
}

