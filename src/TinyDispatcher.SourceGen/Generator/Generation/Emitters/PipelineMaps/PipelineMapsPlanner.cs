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
        PipelinePlan? pipelinePlan,
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
        var visitor = new PipelineMapVisitor();
        pipelinePlan?.Accept(visitor);
        var descriptors = ImmutableArray.CreateBuilder<PipelineDescriptor>(
            discovery.Commands.Length + discovery.Queries.Length);

        AddCommands(
            descriptors,
            discovery.Commands,
            visitor,
            options);
        AddQueries(descriptors, discovery.Queries, inspector);

        return new PipelineMapsPlan(descriptors.ToImmutable(), formats);
    }

    private static void AddCommands(
        ImmutableArray<PipelineDescriptor>.Builder descriptors,
        ImmutableArray<HandlerContract> handlers,
        PipelineMapVisitor visitor,
        GeneratorOptions options)
    {
        for (var i = 0; i < handlers.Length; i++)
        {
            var handler = handlers[i];
            var commandType = PipelineTypeNames.NormalizeFqn(handler.MessageTypeFqn);

            if (visitor.TryGetDescriptor(commandType, out var descriptor))
            {
                descriptors.Add(descriptor);
                continue;
            }

            descriptors.Add(new PipelineDescriptor(
                CommandFullName: commandType,
                ContextFullName: PipelineTypeNames.NormalizeFqn(options.CommandContextType!),
                HandlerFullName: PipelineTypeNames.NormalizeFqn(handler.HandlerTypeFqn),
                Middlewares: System.Array.Empty<MiddlewareDescriptor>(),
                PoliciesApplied: System.Array.Empty<string>()));
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

