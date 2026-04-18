using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

internal static class PipelinePlanner
{
    private static readonly MiddlewareRef[] NoMiddlewares = Array.Empty<MiddlewareRef>();

    public static PipelinePlan Build(
        ImmutableArray<MiddlewareRef> globalMiddlewares,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand,
        ImmutableDictionary<string, PolicySpec> policies,
        DiscoveryResult discovery,
        GeneratorOptions options)
    {
        var coreNamespace = "global::TinyDispatcher";
        var generatedNamespace = options.GeneratedNamespace;
        var contextType = PipelineTypeNames.NormalizeFqn(options.CommandContextType!);

        var global = PipelineMiddlewareSets.NormalizeDistinct(globalMiddlewares);
        var hasGlobalMiddlewares = global.Length > 0;

        var perCommandMiddlewares = NormalizePerCommandMiddlewares(perCommand);
        var commandToPolicyMiddlewares = BuildCommandToPolicyMiddlewares(policies);
        var globalPipeline = BuildGlobalPipeline(global);

        var policyPipelines = BuildPolicyPipelines(global, policies);
        var perCommandPipelines = BuildPerCommandPipelines(
            global,
            perCommandMiddlewares,
            commandToPolicyMiddlewares);

        var middlewareRegistrations = BuildOpenGenericMiddlewareRegistrations(
            global,
            perCommandMiddlewares,
            policies);

        var serviceRegistrations = PipelineRegistrationPlanner.Build(
            generatedNamespace,
            coreNamespace,
            contextType,
            hasGlobalMiddlewares,
            discovery,
            perCommandMiddlewares,
            policies);

        var shouldEmit = ShouldEmitPlan(
            globalPipeline,
            policyPipelines,
            perCommandPipelines,
            middlewareRegistrations,
            serviceRegistrations);

        return new PipelinePlan(
            GeneratedNamespace: generatedNamespace,
            ContextFqn: contextType,
            CoreFqn: coreNamespace,
            ShouldEmit: shouldEmit,
            GlobalPipeline: globalPipeline,
            PolicyPipelines: policyPipelines,
            PerCommandPipelines: perCommandPipelines,
            OpenGenericMiddlewareRegistrations: middlewareRegistrations,
            ServiceRegistrations: serviceRegistrations);
    }

    private static Dictionary<string, MiddlewareRef[]> NormalizePerCommandMiddlewares(
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand)
    {
        var normalized = new Dictionary<string, MiddlewareRef[]>(StringComparer.Ordinal);

        foreach (var pair in perCommand)
        {
            AddNormalizedPerCommandMiddlewares(normalized, pair.Key, pair.Value);
        }

        return normalized;
    }

    private static void AddNormalizedPerCommandMiddlewares(
        Dictionary<string, MiddlewareRef[]> normalized,
        string commandType,
        ImmutableArray<MiddlewareRef> middlewares)
    {
        var command = PipelineTypeNames.NormalizeFqn(commandType);
        var commandIsMissing = string.IsNullOrWhiteSpace(command);

        if (commandIsMissing)
        {
            return;
        }

        var distinctMiddlewares = PipelineMiddlewareSets.NormalizeDistinct(middlewares);
        var hasNoMiddlewares = distinctMiddlewares.Length == 0;

        if (hasNoMiddlewares)
        {
            return;
        }

        normalized[command] = distinctMiddlewares;
    }

    private static PipelineDefinition? BuildGlobalPipeline(MiddlewareRef[] global)
    {
        var hasGlobalMiddlewares = global.Length > 0;

        if (!hasGlobalMiddlewares)
        {
            return null;
        }

        return new PipelineDefinition(
            ClassName: "TinyDispatcherGlobalPipeline",
            IsOpenGeneric: true,
            CommandType: "TCommand",
            Steps: BuildSteps(global, NoMiddlewares, NoMiddlewares));
    }

    private static bool ShouldEmitPlan(
        PipelineDefinition? globalPipeline,
        ImmutableArray<PipelineDefinition> policyPipelines,
        ImmutableArray<PipelineDefinition> perCommandPipelines,
        ImmutableArray<OpenGenericRegistration> middlewareRegistrations,
        ImmutableArray<ServiceRegistration> serviceRegistrations)
    {
        var hasGlobalPipeline = globalPipeline is not null;
        var hasPolicyPipelines = policyPipelines.Length > 0;
        var hasPerCommandPipelines = perCommandPipelines.Length > 0;
        var hasMiddlewareRegistrations = middlewareRegistrations.Length > 0;
        var hasServiceRegistrations = serviceRegistrations.Length > 0;

        return hasGlobalPipeline ||
            hasPolicyPipelines ||
            hasPerCommandPipelines ||
            hasMiddlewareRegistrations ||
            hasServiceRegistrations;
    }

    private static ImmutableArray<PipelineDefinition> BuildPolicyPipelines(
        MiddlewareRef[] global,
        ImmutableDictionary<string, PolicySpec> policies)
    {
        if (policies.Count == 0)
        {
            return ImmutableArray<PipelineDefinition>.Empty;
        }

        var list = new List<PipelineDefinition>(policies.Count);

        var orderedPolicies = PipelineOrdering.GetPoliciesInStableOrder(policies);
        for (var i = 0; i < orderedPolicies.Length; i++)
        {
            var p = orderedPolicies[i];
            var policyMids = PipelineMiddlewareSets.NormalizeDistinct(p.Middlewares);
            var hasNoPolicyMiddlewares = policyMids.Length == 0;

            if (hasNoPolicyMiddlewares)
            {
                continue;
            }

            list.Add(new PipelineDefinition(
                ClassName: "TinyDispatcherPolicyPipeline_" + PipelineNameFactory.SanitizePolicyName(p.PolicyTypeFqn),
                IsOpenGeneric: true,
                CommandType: "TCommand",
                Steps: BuildSteps(global, policyMids, NoMiddlewares)));
        }

        return list.ToImmutableArray();
    }

    private static ImmutableArray<PipelineDefinition> BuildPerCommandPipelines(
        MiddlewareRef[] global,
        Dictionary<string, MiddlewareRef[]> perCmd,
        Dictionary<string, MiddlewareRef[]> cmdToPolicyMids)
    {
        if (perCmd.Count == 0)
        {
            return ImmutableArray<PipelineDefinition>.Empty;
        }

        var list = new List<PipelineDefinition>(perCmd.Count);

        var orderedCommands = PipelineOrdering.GetStringsInStableOrder(perCmd.Keys);
        for (var i = 0; i < orderedCommands.Length; i++)
        {
            var cmdFqn = orderedCommands[i];
            var perCmdMids = perCmd[cmdFqn];

            if (!cmdToPolicyMids.TryGetValue(cmdFqn, out var policyMids))
            {
                policyMids = NoMiddlewares;
            }

            list.Add(new PipelineDefinition(
                ClassName: "TinyDispatcherPipeline_" + PipelineNameFactory.SanitizeCommandName(cmdFqn),
                IsOpenGeneric: false,
                CommandType: cmdFqn,
                Steps: BuildSteps(global, policyMids, perCmdMids)));
        }

        return list.ToImmutableArray();
    }

    private static ImmutableArray<MiddlewareStep> BuildSteps(
        MiddlewareRef[] global,
        MiddlewareRef[] policy,
        MiddlewareRef[] perCommand)
    {
        var steps = new List<MiddlewareStep>(global.Length + policy.Length + perCommand.Length);

        AddSteps(steps, global);
        AddSteps(steps, policy);
        AddSteps(steps, perCommand);

        return steps.ToImmutableArray();
    }

    private static void AddSteps(List<MiddlewareStep> steps, MiddlewareRef[] middlewares)
    {
        for (var i = 0; i < middlewares.Length; i++)
        {
            steps.Add(new MiddlewareStep(middlewares[i]));
        }
    }

    private static Dictionary<string, MiddlewareRef[]> BuildCommandToPolicyMiddlewares(
        ImmutableDictionary<string, PolicySpec> policies)
    {
        var map = new Dictionary<string, MiddlewareRef[]>(StringComparer.Ordinal);

        var orderedPolicies = PipelineOrdering.GetPoliciesInStableOrder(policies);
        for (var i = 0; i < orderedPolicies.Length; i++)
        {
            var p = orderedPolicies[i];
            var mids = PipelineMiddlewareSets.NormalizeDistinct(p.Middlewares);
            var hasNoPolicyMiddlewares = mids.Length == 0;

            if (hasNoPolicyMiddlewares)
            {
                continue;
            }

            PipelinePolicyCommandMap.AddFirstPolicyWins(map, p.Commands, mids);
        }

        return map;
    }

    private static ImmutableArray<OpenGenericRegistration> BuildOpenGenericMiddlewareRegistrations(
        MiddlewareRef[] global,
        Dictionary<string, MiddlewareRef[]> perCommand,
        ImmutableDictionary<string, PolicySpec> policies)
    {
        var all = new List<MiddlewareRef>(256);

        all.AddRange(global);

        foreach (var pair in perCommand)
        {
            all.AddRange(pair.Value);
        }

        foreach (var policy in policies.Values)
        {
            all.AddRange(PipelineMiddlewareSets.NormalizeDistinct(policy.Middlewares));
        }

        var distinct = PipelineMiddlewareSets.NormalizeDistinct(all.ToImmutableArray());

        var regs = new List<OpenGenericRegistration>(distinct.Length);
        for (var i = 0; i < distinct.Length; i++)
        {
            regs.Add(new OpenGenericRegistration(PipelineTypeNames.OpenGenericTypeof(distinct[i])));
        }

        return regs.ToImmutableArray();
    }

}
