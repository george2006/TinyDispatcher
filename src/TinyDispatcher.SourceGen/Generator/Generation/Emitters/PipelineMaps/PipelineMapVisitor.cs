using System.Collections.Generic;
using TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.PipelineMaps;

internal sealed class PipelineMapVisitor : IResolvedPipelineVisitor
{
    private readonly Dictionary<string, PipelineDescriptor> _descriptors =
        new(System.StringComparer.Ordinal);

    public void Visit(ResolvedPipeline pipeline)
    {
        var operation = pipeline.Operation;
        var middlewares = BuildMiddlewares(pipeline.Pipeline.Steps);
        var commandType = PipelineTypeNames.NormalizeFqn(operation.MessageTypeFqn);

        _descriptors[commandType] = new PipelineDescriptor(
            CommandFullName: commandType,
            ContextFullName: PipelineTypeNames.NormalizeFqn(operation.ContextTypeFqn),
            HandlerFullName: PipelineTypeNames.NormalizeFqn(operation.HandlerTypeFqn),
            Middlewares: middlewares,
            PoliciesApplied: GetPolicies(pipeline.Pipeline.Steps));
    }

    public bool TryGetDescriptor(string commandTypeFqn, out PipelineDescriptor descriptor)
    {
        return _descriptors.TryGetValue(commandTypeFqn, out descriptor!);
    }

    private static IReadOnlyList<MiddlewareDescriptor> BuildMiddlewares(
        System.Collections.Immutable.ImmutableArray<MiddlewareStep> steps)
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

    private static IReadOnlyList<string> GetPolicies(
        System.Collections.Immutable.ImmutableArray<MiddlewareStep> steps)
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
}
