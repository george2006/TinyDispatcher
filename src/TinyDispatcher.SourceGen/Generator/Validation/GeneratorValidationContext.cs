#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class GeneratorValidationContext
{
    internal GeneratorValidationContext(Builder b)
    {
        Lane = b.Lane ?? throw new ArgumentNullException(nameof(b.Lane));
        Diagnostics = b.Diagnostics ?? throw new ArgumentNullException(nameof(b.Diagnostics));

        IsHostProject = b.IsHostProject;
        ReferencedContributions = b.ReferencedContributions ?? ReferencedAssemblyContributions.Empty;
    }

    public HostLane Lane { get; }
    public DiscoveryResult DiscoveryResult => Lane.Discovery;
    public DiagnosticsCatalog Diagnostics { get; }

    // Host gate / discovery
    public ImmutableArray<UseTinyDispatcherCall> UseTinyDispatcherCalls => Lane.BootstrapCalls;
    public bool IsHostProject { get; }

    // Context
    public string ContextTypeFqn => Lane.ContextTypeFqn;
    public ReferencedAssemblyContributions ReferencedContributions { get; }

    // Pipeline config
    public PipelineConfig ThisAssemblyPipeline => Lane.ThisAssemblyPipeline;
    public PipelineConfig Pipeline => Lane.Pipeline;
    public ImmutableArray<MiddlewareRef> Globals => Pipeline.Globals;
    public ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> PerCommand => Pipeline.PerCommand;
    public ImmutableDictionary<string, PolicySpec> Policies => Pipeline.Policies;

    public IEnumerable<MiddlewareRef> EnumerateAllMiddlewares()
    {
        for (var i = 0; i < Globals.Length; i++)
        {
            yield return Globals[i];
        }

        foreach (var pair in PerCommand)
        {
            var middlewares = pair.Value;

            for (var i = 0; i < middlewares.Length; i++)
            {
                yield return middlewares[i];
            }
        }

        foreach (var policy in Policies.Values)
        {
            var middlewares = policy.Middlewares;

            for (var i = 0; i < middlewares.Length; i++)
            {
                yield return middlewares[i];
            }
        }
    }

    internal sealed class Builder
    {
        public Builder(HostLane lane, DiagnosticsCatalog diagnostics)
        {
            Lane = lane;
            Diagnostics = diagnostics;
        }

        public HostLane Lane { get; }
        public DiagnosticsCatalog Diagnostics { get; }

        public bool IsHostProject { get; private set; }

        public ReferencedAssemblyContributions? ReferencedContributions { get; private set; }

        public Builder WithHostGate(bool isHost)
        {
            IsHostProject = isHost;
            return this;
        }

        public Builder WithReferencedContributions(ReferencedAssemblyContributions referencedContributions)
        {
            ReferencedContributions = referencedContributions;
            return this;
        }

        public GeneratorValidationContext Build() => new(this);
    }
}
