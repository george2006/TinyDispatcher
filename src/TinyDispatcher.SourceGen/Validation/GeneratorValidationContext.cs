using Microsoft.CodeAnalysis;
using System.Collections.Immutable;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Validation;

internal sealed class GeneratorValidationContext
{
    public GeneratorValidationContext(
        Compilation compilation,
        DiscoveryResult discoveryResult,
        string expectedContextFqn,
        ImmutableArray<MiddlewareRef> globals,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand,
        ImmutableDictionary<string, PolicySpec> policies,
        DiagnosticsCatalog diagnostics)
    {
        Compilation = compilation;
        DiscoveryResult = discoveryResult;
        ExpectedContextFqn = expectedContextFqn;
        Globals = globals;
        PerCommand = perCommand;
        Policies = policies;
        Diagnostics = diagnostics;
    }

    public Compilation Compilation { get; }
    public DiscoveryResult DiscoveryResult { get; }
    public string ExpectedContextFqn { get; }

    public ImmutableArray<MiddlewareRef> Globals { get; }
    public ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> PerCommand { get; }
    public ImmutableDictionary<string, PolicySpec> Policies { get; }

    public DiagnosticsCatalog Diagnostics { get; }
}

