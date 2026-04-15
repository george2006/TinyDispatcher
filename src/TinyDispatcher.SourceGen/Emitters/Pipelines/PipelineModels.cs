using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

internal sealed record PipelinePlan(
        string GeneratedNamespace,
        string ContextFqn,
        string CoreFqn,
        bool ShouldEmit,
        PipelineDefinition? GlobalPipeline,
        ImmutableArray<PipelineDefinition> PolicyPipelines,
        ImmutableArray<PipelineDefinition> PerCommandPipelines,
        ImmutableArray<OpenGenericRegistration> OpenGenericMiddlewareRegistrations,
        ImmutableArray<ServiceRegistration> ServiceRegistrations
    );

internal sealed record PipelineDefinition(
    string ClassName,
    bool IsOpenGeneric,
    string CommandType, // "TCommand" or FQN
    ImmutableArray<MiddlewareStep> Steps
);

internal sealed record MiddlewareStep(MiddlewareRef Middleware);

internal sealed record OpenGenericRegistration(string TypeofExpression);

internal sealed record ServiceRegistration(string ServiceTypeExpression, string ImplementationTypeExpression);
