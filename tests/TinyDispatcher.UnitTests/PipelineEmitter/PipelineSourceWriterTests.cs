#nullable enable

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text.RegularExpressions;
using TinyDispatcher.SourceGen.Emitters;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;
using static TinyDispatcher.SourceGen.Emitters.PipelineEmitterRefactored;

namespace TinyDispatcher.UnitTests.PipelineEmitter;

public sealed class PipelineSourceWriterTests
{
    [Fact]
    public void Open_generic_pipeline_has_where_clause_before_class_body()
    {
        var plan = Create_plan(
            globalPipeline: Create_pipeline(
                className: "TinyDispatcherGlobalPipeline",
                isOpenGeneric: true,
                commandType: "TCommand",
                steps: new[] { new MiddlewareRef("global::MyApp.GlobalLogMiddleware", 2) }
            ));

        var source = PipelineEmitterRefactored.PipelineSourceWriter.Write(plan);

        Assert.Contains("internal sealed class TinyDispatcherGlobalPipeline<TCommand>", source, StringComparison.Ordinal);
        Assert.Contains("where TCommand : global::TinyDispatcher.ICommand", source, StringComparison.Ordinal);

        var whereIndex = source.IndexOf("where TCommand : global::TinyDispatcher.ICommand", StringComparison.Ordinal);
        var braceIndex = source.IndexOf("{", whereIndex, StringComparison.Ordinal);

        Assert.True(whereIndex >= 0);
        Assert.True(braceIndex > whereIndex);
    }

    [Fact]
    public void Runtime_is_not_at_namespace_scope()
    {
        var plan = Create_plan(
            globalPipeline: Create_pipeline(
                "TinyDispatcherGlobalPipeline",
                true,
                "TCommand",
                new[] { new MiddlewareRef("global::MyApp.G1", 2) }),
            policyPipelines: new[]
            {
                Create_pipeline(
                    "TinyDispatcherPolicyPipeline_X",
                    true,
                    "TCommand",
                    new[] { new MiddlewareRef("global::MyApp.P1", 2) })
            });

        var source = PipelineEmitterRefactored.PipelineSourceWriter.Write(plan);

        // It MUST exist (nested inside pipeline types)
        Assert.Contains("private sealed class Runtime", source, StringComparison.Ordinal);

        // But it MUST NOT appear as a namespace member (column 0 or "  " indentation).
        // Namespace members in our generated file are indented 2 spaces.
        // Nested members are indented 4+ spaces.
        Assert.DoesNotMatch(
            new Regex(@"^(?:private|  private)\s+sealed\s+class\s+Runtime\b", RegexOptions.Multiline),
            source);
    }

    [Fact]
    public void Constructor_is_not_generic_for_open_generic_pipeline()
    {
        var plan = Create_plan(
            globalPipeline: Create_pipeline(
                "TinyDispatcherGlobalPipeline",
                true,
                "TCommand",
                new[] { new MiddlewareRef("global::MyApp.G1", 2) }));

        var source = PipelineEmitterRefactored.PipelineSourceWriter.Write(plan);

        Assert.Contains("public TinyDispatcherGlobalPipeline(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("public TinyDispatcherGlobalPipeline<TCommand>(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Switch_cases_follow_step_order()
    {
        var pipeline = Create_pipeline(
            "TinyDispatcherPipeline_CommandA",
            false,
            "global::MyApp.CommandA",
            new[]
            {
                new MiddlewareRef("global::MyApp.G", 2),
                new MiddlewareRef("global::MyApp.P", 2),
                new MiddlewareRef("global::MyApp.C", 2)
            });

        var plan = Create_plan(perCommandPipelines: new[] { pipeline });

        var source = PipelineEmitterRefactored.PipelineSourceWriter.Write(plan);

        var case0 = source.IndexOf("case 0:", StringComparison.Ordinal);
        var case1 = source.IndexOf("case 1:", StringComparison.Ordinal);
        var case2 = source.IndexOf("case 2:", StringComparison.Ordinal);

        Assert.True(case0 >= 0);
        Assert.True(case1 > case0);
        Assert.True(case2 > case1);
    }

    [Fact]
    public void Generated_source_has_balanced_braces()
    {
        var plan = Create_plan(
            globalPipeline: Create_pipeline(
                "TinyDispatcherGlobalPipeline",
                true,
                "TCommand",
                new[] { new MiddlewareRef("global::MyApp.G1", 2) }),
            policyPipelines: new[]
            {
                Create_pipeline(
                    "TinyDispatcherPolicyPipeline_X",
                    true,
                    "TCommand",
                    new[] { new MiddlewareRef("global::MyApp.P1", 2) })
            },
            perCommandPipelines: new[]
            {
                Create_pipeline(
                    "TinyDispatcherPipeline_CommandA",
                    false,
                    "global::MyApp.CommandA",
                    new[] { new MiddlewareRef("global::MyApp.C1", 2) })
            });

        var source = PipelineEmitterRefactored.PipelineSourceWriter.Write(plan);

        Assert.Equal(Count(source, "{"), Count(source, "}"));
    }

    // ------------------------------------------------------------
    // Plan builders
    // ------------------------------------------------------------

    private static PipelineEmitterRefactored.PipelinePlan Create_plan(
        PipelineEmitterRefactored.PipelineDefinition? globalPipeline = null,
        PipelineEmitterRefactored.PipelineDefinition[]? policyPipelines = null,
        PipelineEmitterRefactored.PipelineDefinition[]? perCommandPipelines = null)
    {
        return new PipelineEmitterRefactored.PipelinePlan(
            GeneratedNamespace: "MyApp.Generated",
            ContextFqn: "global::MyApp.AppContext",
            CoreFqn: "global::TinyDispatcher",
            ShouldEmit: true,
            GlobalPipeline: globalPipeline,
            PolicyPipelines: (policyPipelines ?? Array.Empty<PipelineEmitterRefactored.PipelineDefinition>()).ToImmutableArray(),
            PerCommandPipelines: (perCommandPipelines ?? Array.Empty<PipelineEmitterRefactored.PipelineDefinition>()).ToImmutableArray(),
            OpenGenericMiddlewareRegistrations: ImmutableArray<PipelineEmitterRefactored.OpenGenericRegistration>.Empty,
            ServiceRegistrations: ImmutableArray<PipelineEmitterRefactored.ServiceRegistration>.Empty
        );
    }

    private static PipelineDefinition Create_pipeline(
        string className,
        bool isOpenGeneric,
        string commandType,
        MiddlewareRef[] steps)
    {
        return new PipelineEmitterRefactored.PipelineDefinition(
            ClassName: className,
            IsOpenGeneric: isOpenGeneric,
            CommandType: commandType,
            Steps: steps.Select(m => new PipelineEmitterRefactored.MiddlewareStep(m)).ToImmutableArray()
        );
    }

    private static int Count(string text, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }
}
