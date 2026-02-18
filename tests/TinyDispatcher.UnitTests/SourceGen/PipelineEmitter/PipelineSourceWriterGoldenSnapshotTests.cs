#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Emitters.Pipelines;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen.PipelineEmitter;

public sealed class PipelineSourceWriterGoldenSnapshotTests
{
    private static MiddlewareRef Mw(string openTypeFqn, int arity)
        => new MiddlewareRef(OpenTypeSymbol: default!, OpenTypeFqn: openTypeFqn, Arity: arity);

    [Fact]
    public void Write_golden_snapshot_global_policy_per_command_matches_expected()
    {
        var plan = Create_plan();

        var source = PipelineSourceWriter.Write(plan);

        var snapshot = Create_snapshot(source);

        var expected = string.Join(
            "\n",
            new[]
            {
                // Global
                "internal sealed class TinyDispatcherGlobalPipeline<TCommand> : global::TinyDispatcher.ICommandPipeline<TCommand, global::MyApp.AppContext>, global::TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, global::MyApp.AppContext>",
                "where TCommand : global::TinyDispatcher.ICommand",
                "public TinyDispatcherGlobalPipeline(",
                "case 0: return _globalLog.InvokeAsync(command, ctxValue, this, ct);",
                "default: return new ValueTask(_handler!.HandleAsync(command, ctxValue, ct));",

                // Policy
                "internal sealed class TinyDispatcherPolicyPipeline_MyApp_CheckoutPolicy<TCommand> : global::TinyDispatcher.ICommandPipeline<TCommand, global::MyApp.AppContext>, global::TinyDispatcher.Pipeline.ICommandPipelineRuntime<TCommand, global::MyApp.AppContext>",
                "where TCommand : global::TinyDispatcher.ICommand",
                "public TinyDispatcherPolicyPipeline_MyApp_CheckoutPolicy(",
                "case 0: return _globalLog.InvokeAsync(command, ctxValue, this, ct);",
                "case 1: return _policyLog.InvokeAsync(command, ctxValue, this, ct);",
                "default: return new ValueTask(_handler!.HandleAsync(command, ctxValue, ct));",

                // Per-command
                "internal sealed class TinyDispatcherPipeline_CmdA : global::TinyDispatcher.ICommandPipeline<global::MyApp.CmdA, global::MyApp.AppContext>, global::TinyDispatcher.Pipeline.ICommandPipelineRuntime<global::MyApp.CmdA, global::MyApp.AppContext>",
                "public TinyDispatcherPipeline_CmdA(",
                "case 0: return _globalLog.InvokeAsync(command, ctxValue, this, ct);",
                "case 1: return _policyLog.InvokeAsync(command, ctxValue, this, ct);",
                "case 2: return _perCommandLog.InvokeAsync(command, ctxValue, this, ct);",
                "default: return new ValueTask(_handler!.HandleAsync(command, ctxValue, ct));",

                // Registrations unchanged
                "services.TryAddTransient(typeof(global::MyApp.GlobalLogMiddleware<,>));",
                "services.TryAddTransient(typeof(global::MyApp.PerCommandLogMiddleware<,>));",
                "services.TryAddTransient(typeof(global::MyApp.PolicyLogMiddleware<,>));",

                "services.AddScoped<global::TinyDispatcher.ICommandPipeline<global::MyApp.CmdA, global::MyApp.AppContext>, global::MyApp.Generated.TinyDispatcherPipeline_CmdA>();",
                "services.AddScoped<global::TinyDispatcher.ICommandPipeline<global::MyApp.CmdB, global::MyApp.AppContext>, global::MyApp.Generated.TinyDispatcherPolicyPipeline_MyApp_CheckoutPolicy<global::MyApp.CmdB>>();",
                "services.AddScoped<global::TinyDispatcher.ICommandPipeline<global::MyApp.CmdC, global::MyApp.AppContext>, global::MyApp.Generated.TinyDispatcherGlobalPipeline<global::MyApp.CmdC>>();",
            });

        Assert.Equal(expected, snapshot);
    }

    private static PipelinePlan Create_plan()
    {
        var genNs = "MyApp.Generated";
        var ctx = "global::MyApp.AppContext";
        var core = "global::TinyDispatcher";

        // Global: applies to all commands without per-command and without policy
        var globalPipeline = new PipelineDefinition(
            ClassName: "TinyDispatcherGlobalPipeline",
            IsOpenGeneric: true,
            CommandType: "TCommand",
            Steps: ImmutableArray.Create(
                new MiddlewareStep(Mw("global::MyApp.GlobalLogMiddleware", 2)))
        );

        // Policy: applies to CmdB (CmdA has per-command, so DI registration should prefer per-command)
        var policyPipeline = new PipelineDefinition(
            ClassName: "TinyDispatcherPolicyPipeline_MyApp_CheckoutPolicy",
            IsOpenGeneric: true,
            CommandType: "TCommand",
            Steps: ImmutableArray.Create(
                new MiddlewareStep(Mw("global::MyApp.GlobalLogMiddleware", 2)),
                new MiddlewareStep(Mw("global::MyApp.PolicyLogMiddleware", 2)))
        );

        // Per-command: CmdA includes global + policy + per-command
        var perCommandPipeline = new PipelineDefinition(
            ClassName: "TinyDispatcherPipeline_CmdA",
            IsOpenGeneric: false,
            CommandType: "global::MyApp.CmdA",
            Steps: ImmutableArray.Create(
                new MiddlewareStep(Mw("global::MyApp.GlobalLogMiddleware", 2)),
                new MiddlewareStep(Mw("global::MyApp.PolicyLogMiddleware", 2)),
                new MiddlewareStep(Mw("global::MyApp.PerCommandLogMiddleware", 2)))
        );

        var mwRegs = ImmutableArray.Create(
            new OpenGenericRegistration("global::MyApp.GlobalLogMiddleware<,>"),
            new OpenGenericRegistration("global::MyApp.PerCommandLogMiddleware<,>"),
            new OpenGenericRegistration("global::MyApp.PolicyLogMiddleware<,>")
        );

        var svcRegs = ImmutableArray.Create(
            new ServiceRegistration(
                ServiceTypeExpression: $"{core}.ICommandPipeline<global::MyApp.CmdA, {ctx}>",
                ImplementationTypeExpression: $"global::{genNs}.TinyDispatcherPipeline_CmdA"
            ),
            new ServiceRegistration(
                ServiceTypeExpression: $"{core}.ICommandPipeline<global::MyApp.CmdB, {ctx}>",
                ImplementationTypeExpression: $"global::{genNs}.TinyDispatcherPolicyPipeline_MyApp_CheckoutPolicy<global::MyApp.CmdB>"
            ),
            new ServiceRegistration(
                ServiceTypeExpression: $"{core}.ICommandPipeline<global::MyApp.CmdC, {ctx}>",
                ImplementationTypeExpression: $"global::{genNs}.TinyDispatcherGlobalPipeline<global::MyApp.CmdC>"
            )
        );

        return new PipelinePlan(
            GeneratedNamespace: genNs,
            ContextFqn: ctx,
            CoreFqn: core,
            ShouldEmit: true,
            GlobalPipeline: globalPipeline,
            PolicyPipelines: ImmutableArray.Create(policyPipeline),
            PerCommandPipelines: ImmutableArray.Create(perCommandPipeline),
            OpenGenericMiddlewareRegistrations: mwRegs,
            ServiceRegistrations: svcRegs
        );
    }

    private static string Create_snapshot(string source)
    {
        // Normalize CRLF and take only "structural" lines.
        // This gives us a stable "golden" without being sensitive to whitespace/indent tweaks.
        var normalized = source.Replace("\r\n", "\n", StringComparison.Ordinal);

        var lines = normalized.Split('\n');

        var keep = new List<string>(256);

        foreach (var raw in lines)
        {
            var line = raw.TrimEnd();
            var t = line.TrimStart();

            if (t.StartsWith("internal sealed class ", StringComparison.Ordinal) ||
                t.StartsWith("where TCommand : ", StringComparison.Ordinal) ||
                t.StartsWith("public TinyDispatcher", StringComparison.Ordinal) ||
                t.StartsWith("case ", StringComparison.Ordinal) ||
                t.StartsWith("default: ", StringComparison.Ordinal) ||
                t.StartsWith("services.", StringComparison.Ordinal))
            {
                keep.Add(t);
            }
        }

        return string.Join("\n", keep);
    }
}
