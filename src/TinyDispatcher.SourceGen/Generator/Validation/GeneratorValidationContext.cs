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
        DiscoveryResult = b.DiscoveryResult ?? throw new ArgumentNullException(nameof(b.DiscoveryResult));
        Diagnostics = b.Diagnostics ?? throw new ArgumentNullException(nameof(b.Diagnostics));

        // Optional / phase-dependent
        UseTinyDispatcherCalls = b.UseTinyDispatcherCalls;
        IsHostProject = b.IsHostProject;

        ExpectedContextFqn = b.ExpectedContextFqn ?? string.Empty;

        Pipeline = b.Pipeline ?? new PipelineConfig(
            ImmutableArray<MiddlewareRef>.Empty,
            ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
            ImmutableDictionary<string, PolicySpec>.Empty);
    }

    public DiscoveryResult DiscoveryResult { get; }
    public DiagnosticsCatalog Diagnostics { get; }

    // Host gate / discovery
    public ImmutableArray<UseTinyDispatcherCall> UseTinyDispatcherCalls { get; }
    public bool IsHostProject { get; }

    // Context
    public string ExpectedContextFqn { get; }

    // Pipeline config
    public PipelineConfig Pipeline { get; }
    public ImmutableArray<MiddlewareRef> Globals => Pipeline.Globals;
    public ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> PerCommand => Pipeline.PerCommand;
    public ImmutableDictionary<string, PolicySpec> Policies => Pipeline.Policies;

    public IEnumerable<MiddlewareRef> EnumerateAllMiddlewares()
    {
        for (int i = 0; i < Globals.Length; i++)
            yield return Globals[i];

        foreach (var kv in PerCommand)
        {
            var arr = kv.Value;
            for (int i = 0; i < arr.Length; i++)
                yield return arr[i];
        }

        foreach (var p in Policies.Values)
        {
            var arr = p.Middlewares;
            for (int i = 0; i < arr.Length; i++)
                yield return arr[i];
        }
    }

    internal sealed class Builder
    {
        public Builder(DiscoveryResult discoveryResult, DiagnosticsCatalog diagnostics)
        {
            DiscoveryResult = discoveryResult;
            Diagnostics = diagnostics;
        }

        public DiscoveryResult DiscoveryResult { get; }
        public DiagnosticsCatalog Diagnostics { get; }

        public ImmutableArray<UseTinyDispatcherCall> UseTinyDispatcherCalls { get; private set; } =
            ImmutableArray<UseTinyDispatcherCall>.Empty;

        public bool IsHostProject { get; private set; }

        public string? ExpectedContextFqn { get; private set; }

        public PipelineConfig? Pipeline { get; private set; }

        public Builder WithHostGate(bool isHost)
        {
            IsHostProject = isHost;
            return this;
        }

        public Builder WithUseTinyDispatcherCalls(ImmutableArray<UseTinyDispatcherCall> calls)
        {
            UseTinyDispatcherCalls = calls;
            return this;
        }

        public Builder WithExpectedContext(string expectedContextFqn)
        {
            ExpectedContextFqn = expectedContextFqn;
            return this;
        }

        public Builder WithPipelineConfig(PipelineConfig pipeline)
        {
            Pipeline = pipeline;
            return this;
        }

        public GeneratorValidationContext Build() => new(this);
    }
}
