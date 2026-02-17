#nullable enable

using System;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Validation;

/// <summary>
/// Shared context object for generator validators.
/// Keeps validation APIs stable as the generator grows.
/// </summary>
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
        Compilation = compilation ?? throw new ArgumentNullException(nameof(compilation));
        DiscoveryResult = discoveryResult ?? throw new ArgumentNullException(nameof(discoveryResult));
        ExpectedContextFqn = expectedContextFqn ?? throw new ArgumentNullException(nameof(expectedContextFqn));
        Globals = globals;
        PerCommand = perCommand ?? throw new ArgumentNullException(nameof(perCommand));
        Policies = policies ?? throw new ArgumentNullException(nameof(policies));
        Diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
    }

    public Compilation Compilation { get; }
    public DiscoveryResult DiscoveryResult { get; }
    public string ExpectedContextFqn { get; }

    public ImmutableArray<MiddlewareRef> Globals { get; }
    public ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> PerCommand { get; }
    public ImmutableDictionary<string, PolicySpec> Policies { get; }

    public DiagnosticsCatalog Diagnostics { get; }

    /// <summary>
    /// Creates a context for validators that only need discovery results and diagnostics.
    /// This avoids breaking the main constructor contract while enabling "early" validation.
    /// </summary>
    public static GeneratorValidationContext ForDiscoveryOnly(
        Compilation compilation,
        DiscoveryResult discoveryResult,
        DiagnosticsCatalog diagnostics)
    {
        return new GeneratorValidationContext(
            compilation: compilation,
            discoveryResult: discoveryResult,
            expectedContextFqn: string.Empty,
            globals: ImmutableArray<MiddlewareRef>.Empty,
            perCommand: ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty,
            policies: ImmutableDictionary<string, PolicySpec>.Empty,
            diagnostics: diagnostics);
    }
}
