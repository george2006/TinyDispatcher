using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.Pipelines;

internal sealed record PipelinePlan(
    string GeneratedNamespace,
    string ContextFqn,
    string CoreFqn,
    bool ShouldEmit,
    PipelineDefinition? GlobalPipeline,
    ImmutableArray<PolicyPipelineDefinition> PolicyPipelines,
    ImmutableArray<PipelineDefinition> PerCommandPipelines,
    ImmutableArray<OpenGenericRegistration> OpenGenericMiddlewareRegistrations,
    ImmutableArray<PipelineRegistration> ServiceRegistrations);

internal sealed record PipelineDefinition(
    string ClassName,
    bool IsOpenGeneric,
    string CommandType,
    ImmutableArray<MiddlewareStep> Steps);

internal sealed record PolicyPipelineDefinition(
    PipelinePolicyContribution Policy,
    PipelineDefinition Pipeline);

internal sealed record MiddlewareStep(MiddlewareRef Middleware);

internal sealed record OpenGenericRegistration(string TypeofExpression);

internal sealed record PipelineRegistration(
    string CommandType,
    PipelineDefinition Pipeline,
    string ServiceTypeExpression,
    string ImplementationTypeExpression);

