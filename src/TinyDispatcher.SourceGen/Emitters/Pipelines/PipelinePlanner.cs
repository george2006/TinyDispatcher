using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Generator.Models;
using static TinyDispatcher.SourceGen.Emitters.Pipelines.PipelineEmitter;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

internal static class PipelinePlanner
{
    public static PipelinePlan Build(
        ImmutableArray<MiddlewareRef> globalMiddlewares,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand,
        ImmutableDictionary<string, PolicySpec> policies,
        DiscoveryResult discovery,
        GeneratorOptions options)
    {
        var core = "global::TinyDispatcher";
        var genNs = options.GeneratedNamespace;
        var ctx = TypeNames.NormalizeFqn(options.CommandContextType!);

        var global = MiddlewareSets.NormalizeDistinct(globalMiddlewares);
        var hasGlobal = global.Length > 0;

        var perCmd = new Dictionary<string, MiddlewareRef[]>(StringComparer.Ordinal);
        foreach (var kv in perCommand)
        {
            var cmd = TypeNames.NormalizeFqn(kv.Key);
            var mids = MiddlewareSets.NormalizeDistinct(kv.Value);
            if (string.IsNullOrWhiteSpace(cmd) || mids.Length == 0) continue;
            perCmd[cmd] = mids;
        }

        var cmdToPolicyMids = BuildCommandToPolicyMiddlewares(policies);

        PipelineDefinition? globalPipeline = null;
        if (hasGlobal)
        {
            globalPipeline = new PipelineDefinition(
                ClassName: "TinyDispatcherGlobalPipeline",
                IsOpenGeneric: true,
                CommandType: "TCommand",
                Steps: global.Select(m => new MiddlewareStep(m)).ToImmutableArray());
        }

        var policyPipelines = BuildPolicyPipelines(global, policies);
        var perCommandPipelines = BuildPerCommandPipelines(global, perCmd, cmdToPolicyMids);
        var mwRegs = BuildOpenGenericMiddlewareRegistrations(global, perCommand, policies);
        var svcRegs = BuildServiceRegistrations(genNs, core, ctx, hasGlobal, discovery, perCmd, policies);

        var shouldEmit =
            globalPipeline is not null ||
            policyPipelines.Length > 0 ||
            perCommandPipelines.Length > 0 ||
            mwRegs.Length > 0 ||
            svcRegs.Length > 0;

        return new PipelinePlan(
            GeneratedNamespace: genNs,
            ContextFqn: ctx,
            CoreFqn: core,
            ShouldEmit: shouldEmit,
            GlobalPipeline: globalPipeline,
            PolicyPipelines: policyPipelines,
            PerCommandPipelines: perCommandPipelines,
            OpenGenericMiddlewareRegistrations: mwRegs,
            ServiceRegistrations: svcRegs);
    }

    private static ImmutableArray<PipelineDefinition> BuildPolicyPipelines(
        MiddlewareRef[] global,
        ImmutableDictionary<string, PolicySpec> policies)
    {
        if (policies.Count == 0) return ImmutableArray<PipelineDefinition>.Empty;

        var list = new List<PipelineDefinition>(policies.Count);

        foreach (var p in policies.Values.OrderBy(x => TypeNames.NormalizeFqn(x.PolicyTypeFqn), StringComparer.Ordinal))
        {
            var policyMids = MiddlewareSets.NormalizeDistinct(p.Middlewares);
            if (policyMids.Length == 0) continue;

            // ORDER: Global -> Policy -> Handler
            var steps = new List<MiddlewareStep>(global.Length + policyMids.Length);
            for (int i = 0; i < global.Length; i++) steps.Add(new MiddlewareStep(global[i]));
            for (int i = 0; i < policyMids.Length; i++) steps.Add(new MiddlewareStep(policyMids[i]));

            list.Add(new PipelineDefinition(
                ClassName: "TinyDispatcherPolicyPipeline_" + NameFactory.SanitizePolicyName(p.PolicyTypeFqn),
                IsOpenGeneric: true,
                CommandType: "TCommand",
                Steps: steps.ToImmutableArray()));
        }

        return list.ToImmutableArray();
    }

    private static ImmutableArray<PipelineDefinition> BuildPerCommandPipelines(
        MiddlewareRef[] global,
        Dictionary<string, MiddlewareRef[]> perCmd,
        Dictionary<string, MiddlewareRef[]> cmdToPolicyMids)
    {
        if (perCmd.Count == 0) return ImmutableArray<PipelineDefinition>.Empty;

        var list = new List<PipelineDefinition>(perCmd.Count);

        foreach (var kv in perCmd.OrderBy(k => k.Key, StringComparer.Ordinal))
        {
            var cmdFqn = kv.Key;
            var perCmdMids = kv.Value;

            if (!cmdToPolicyMids.TryGetValue(cmdFqn, out var policyMids))
                policyMids = Array.Empty<MiddlewareRef>();

            // ORDER: Global -> Policy -> PerCommand -> Handler
            var steps = new List<MiddlewareStep>(global.Length + policyMids.Length + perCmdMids.Length);
            for (int i = 0; i < global.Length; i++) steps.Add(new MiddlewareStep(global[i]));
            for (int i = 0; i < policyMids.Length; i++) steps.Add(new MiddlewareStep(policyMids[i]));
            for (int i = 0; i < perCmdMids.Length; i++) steps.Add(new MiddlewareStep(perCmdMids[i]));

            list.Add(new PipelineDefinition(
                ClassName: "TinyDispatcherPipeline_" + NameFactory.SanitizeCommandName(cmdFqn),
                IsOpenGeneric: false,
                CommandType: cmdFqn,
                Steps: steps.ToImmutableArray()));
        }

        return list.ToImmutableArray();
    }

    private static Dictionary<string, MiddlewareRef[]> BuildCommandToPolicyMiddlewares(
        ImmutableDictionary<string, PolicySpec> policies)
    {
        var map = new Dictionary<string, MiddlewareRef[]>(StringComparer.Ordinal);

        foreach (var p in policies.Values.OrderBy(x => TypeNames.NormalizeFqn(x.PolicyTypeFqn), StringComparer.Ordinal))
        {
            var mids = MiddlewareSets.NormalizeDistinct(p.Middlewares);
            if (mids.Length == 0) continue;

            for (int i = 0; i < p.Commands.Length; i++)
            {
                var cmd = TypeNames.NormalizeFqn(p.Commands[i]);
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                if (!map.ContainsKey(cmd))
                    map[cmd] = mids; // first wins
            }
        }

        return map;
    }

    private static ImmutableArray<OpenGenericRegistration> BuildOpenGenericMiddlewareRegistrations(
        MiddlewareRef[] global,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand,
        ImmutableDictionary<string, PolicySpec> policies)
    {
        var all = new List<MiddlewareRef>(256);

        all.AddRange(global);

        foreach (var kv in perCommand)
            all.AddRange(MiddlewareSets.NormalizeDistinct(kv.Value));

        foreach (var p in policies.Values)
            all.AddRange(MiddlewareSets.NormalizeDistinct(p.Middlewares));

        var distinct = MiddlewareSets.NormalizeDistinct(all.ToImmutableArray());

        var regs = new List<OpenGenericRegistration>(distinct.Length);
        for (int i = 0; i < distinct.Length; i++)
            regs.Add(new OpenGenericRegistration(TypeNames.OpenGenericTypeof(distinct[i])));

        return regs.ToImmutableArray();
    }

    private static ImmutableArray<ServiceRegistration> BuildServiceRegistrations(
        string genNs,
        string core,
        string ctx,
        bool hasGlobal,
        DiscoveryResult discovery,
        Dictionary<string, MiddlewareRef[]> perCmd,
        ImmutableDictionary<string, PolicySpec> policies)
    {
        var perCmdSet = new HashSet<string>(perCmd.Keys, StringComparer.Ordinal);

        var cmdToPolicyPipelineOpen = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var p in policies.Values.OrderBy(x => TypeNames.NormalizeFqn(x.PolicyTypeFqn), StringComparer.Ordinal))
        {
            var open = "global::" + genNs + ".TinyDispatcherPolicyPipeline_" + NameFactory.SanitizePolicyName(p.PolicyTypeFqn);

            for (int i = 0; i < p.Commands.Length; i++)
            {
                var cmd = TypeNames.NormalizeFqn(p.Commands[i]);
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                if (!cmdToPolicyPipelineOpen.ContainsKey(cmd))
                    cmdToPolicyPipelineOpen[cmd] = open;
            }
        }

        var policyCmdSet = new HashSet<string>(cmdToPolicyPipelineOpen.Keys, StringComparer.Ordinal);

        var regs = new List<ServiceRegistration>(256);

        foreach (var cmd in perCmdSet.OrderBy(x => x, StringComparer.Ordinal))
        {
            regs.Add(new ServiceRegistration(
                ServiceTypeExpression: $"{core}.ICommandPipeline<{cmd}, {ctx}>",
                ImplementationTypeExpression: $"global::{genNs}.TinyDispatcherPipeline_{NameFactory.SanitizeCommandName(cmd)}"));
        }

        foreach (var cmd in policyCmdSet.OrderBy(x => x, StringComparer.Ordinal))
        {
            if (perCmdSet.Contains(cmd)) continue;

            regs.Add(new ServiceRegistration(
                ServiceTypeExpression: $"{core}.ICommandPipeline<{cmd}, {ctx}>",
                ImplementationTypeExpression: $"{cmdToPolicyPipelineOpen[cmd]}<{cmd}>"));
        }

        if (hasGlobal && discovery != null && discovery.Commands.Length > 0)
        {
            for (int i = 0; i < discovery.Commands.Length; i++)
            {
                var cmd = TypeNames.NormalizeFqn(discovery.Commands[i].MessageTypeFqn);
                if (string.IsNullOrWhiteSpace(cmd)) continue;

                if (perCmdSet.Contains(cmd)) continue;
                if (policyCmdSet.Contains(cmd)) continue;

                regs.Add(new ServiceRegistration(
                    ServiceTypeExpression: $"{core}.ICommandPipeline<{cmd}, {ctx}>",
                    ImplementationTypeExpression: $"global::{genNs}.TinyDispatcherGlobalPipeline<{cmd}>"));
            }
        }

        return regs.ToImmutableArray();
    }
}
