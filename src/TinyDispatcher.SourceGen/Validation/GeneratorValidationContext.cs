#nullable enable

using Microsoft.CodeAnalysis;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Validation;

internal sealed class GeneratorValidationContext
{
    internal GeneratorValidationContext(Builder b)
    {
        Compilation = b.Compilation ?? throw new ArgumentNullException(nameof(b.Compilation));
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

        // Resolver cache (optional)
        _middlewareSymbolCache = b.MiddlewareSymbolCache;
    }

    public Compilation Compilation { get; }
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

    private readonly ImmutableDictionary<string, INamedTypeSymbol>? _middlewareSymbolCache;

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

    public INamedTypeSymbol? TryResolveMiddlewareOpenTypeSymbol(string openTypeFqn)
    {
        if (string.IsNullOrWhiteSpace(openTypeFqn))
            return null;

        if (_middlewareSymbolCache != null && _middlewareSymbolCache.TryGetValue(openTypeFqn, out var cached))
            return cached;

        // Fallback: resolve by metadata name (strip global::)
        var md = openTypeFqn.StartsWith("global::", StringComparison.Ordinal)
            ? openTypeFqn.Substring("global::".Length)
            : openTypeFqn;

        return Compilation.GetTypeByMetadataName(md);
    }

    // =====================================================================
    // Builder
    // =====================================================================

    internal sealed class Builder
    {
        public Builder(Compilation compilation, DiscoveryResult discoveryResult, DiagnosticsCatalog diagnostics)
        {
            Compilation = compilation;
            DiscoveryResult = discoveryResult;
            Diagnostics = diagnostics;
        }

        public Compilation Compilation { get; }
        public DiscoveryResult DiscoveryResult { get; }
        public DiagnosticsCatalog Diagnostics { get; }

        public ImmutableArray<UseTinyDispatcherCall> UseTinyDispatcherCalls { get; private set; } =
            ImmutableArray<UseTinyDispatcherCall>.Empty;

        public bool IsHostProject { get; private set; }

        public string? ExpectedContextFqn { get; private set; }

        public PipelineConfig? Pipeline { get; private set; }

        public ImmutableDictionary<string, INamedTypeSymbol>? MiddlewareSymbolCache { get; private set; }

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

        public Builder WithMiddlewareSymbolCache(ImmutableDictionary<string, INamedTypeSymbol> cache)
        {
            MiddlewareSymbolCache = cache;
            return this;
        }

        public GeneratorValidationContext Build() => new(this);
    }
}
