#nullable enable

using System.Collections.Generic;
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
        var queryDescriptorPlanner = new QueryPipelineDescriptorPlanner(contributions, options);
        var commandDescriptors = BuildCommandDescriptors(pipelinePlan);
        var descriptors = ImmutableArray.CreateBuilder<PipelineDescriptor>(
            discovery.Commands.Length + discovery.Queries.Length);

        AddCommands(
            descriptors,
            discovery.Commands,
            commandDescriptors,
            options);
        AddQueries(descriptors, discovery.Queries, queryDescriptorPlanner);

        var attributeDescriptors = formats.EmitAttributes
            ? PipelineMapAttributesPlanner.Build(pipelinePlan)
            : ImmutableArray<PipelineMapAttributeDescriptor>.Empty;

        return new PipelineMapsPlan(
            descriptors.ToImmutable(),
            attributeDescriptors,
            formats);
    }

    private static IReadOnlyDictionary<string, PipelineDescriptor> BuildCommandDescriptors(
        PipelinePlan? pipelinePlan)
    {
        var descriptors = new Dictionary<string, PipelineDescriptor>(System.StringComparer.Ordinal);

        if (pipelinePlan is null)
        {
            return descriptors;
        }

        for (var i = 0; i < pipelinePlan.ResolvedPipelines.Length; i++)
        {
            var pipeline = pipelinePlan.ResolvedPipelines[i];
            var operation = pipeline.Operation;
            var commandType = PipelineTypeNames.NormalizeFqn(operation.MessageTypeFqn);

            descriptors[commandType] = new PipelineDescriptor(
                CommandFullName: commandType,
                ContextFullName: PipelineTypeNames.NormalizeFqn(operation.ContextTypeFqn),
                HandlerFullName: PipelineTypeNames.NormalizeFqn(operation.HandlerTypeFqn),
                Middlewares: BuildMiddlewares(pipeline.Pipeline.Steps),
                PoliciesApplied: GetPolicies(pipeline.Pipeline.Steps));
        }

        return descriptors;
    }

    private static void AddCommands(
        ImmutableArray<PipelineDescriptor>.Builder descriptors,
        ImmutableArray<HandlerContract> handlers,
        IReadOnlyDictionary<string, PipelineDescriptor> commandDescriptors,
        GeneratorOptions options)
    {
        for (var i = 0; i < handlers.Length; i++)
        {
            var handler = handlers[i];
            var commandType = PipelineTypeNames.NormalizeFqn(handler.MessageTypeFqn);

            if (commandDescriptors.TryGetValue(commandType, out var descriptor))
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

    private static IReadOnlyList<MiddlewareDescriptor> BuildMiddlewares(
        ImmutableArray<MiddlewareStep> steps)
    {
        var middlewares = new List<MiddlewareDescriptor>(steps.Length);

        for (var i = 0; i < steps.Length; i++)
        {
            middlewares.Add(new MiddlewareDescriptor(
                steps[i].Middleware.OpenTypeFqn,
                GetSource(steps[i])));
        }

        return middlewares;
    }

    private static IReadOnlyList<string> GetPolicies(ImmutableArray<MiddlewareStep> steps)
    {
        var policies = new List<string>();

        for (var i = 0; i < steps.Length; i++)
        {
            var step = steps[i];
            if (step.Source != PipelineStepSource.Policy)
            {
                continue;
            }

            var policy = GetPolicyType(step);
            if (!policies.Contains(policy))
            {
                policies.Add(policy);
            }
        }

        return policies;
    }

    private static string GetSource(MiddlewareStep step)
    {
        return step.Source switch
        {
            PipelineStepSource.Global => "global",
            PipelineStepSource.Policy => "policy:" + GetPolicyType(step),
            PipelineStepSource.Operation => "per-command",
            _ => throw new System.ArgumentOutOfRangeException(nameof(step), step.Source, "Unknown pipeline step source.")
        };
    }

    private static string GetPolicyType(MiddlewareStep step)
    {
        return step.PolicyTypeFqn
            ?? throw new System.InvalidOperationException("A policy pipeline step has no policy type.");
    }

    private static void AddQueries(
        ImmutableArray<PipelineDescriptor>.Builder descriptors,
        ImmutableArray<QueryHandlerContract> handlers,
        QueryPipelineDescriptorPlanner planner)
    {
        for (var i = 0; i < handlers.Length; i++)
        {
            descriptors.Add(planner.Build(handlers[i]));
        }
    }

}

