#nullable enable

using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Generator;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Pipelines;

public sealed class PipelineEmitter : ICodeEmitter
{
    private readonly ImmutableArray<MiddlewareRef> _globalMiddlewares;
    private readonly ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> _perCommand;
    private readonly ImmutableDictionary<string, PolicySpec> _policies;

    public PipelineEmitter(
        ImmutableArray<MiddlewareRef> globalMiddlewares,
        ImmutableDictionary<string, ImmutableArray<MiddlewareRef>> perCommand,
        ImmutableDictionary<string, PolicySpec> policies)
    {
        _globalMiddlewares = globalMiddlewares;
        _perCommand = perCommand ?? ImmutableDictionary<string, ImmutableArray<MiddlewareRef>>.Empty;
        _policies = policies ?? ImmutableDictionary<string, PolicySpec>.Empty;
    }

    public void Emit(IGeneratorContext context, DiscoveryResult result, GeneratorOptions options)
    {
        if (options is null) return;
        if (string.IsNullOrWhiteSpace(options.CommandContextType)) return;

        var hasAny =
            (!_globalMiddlewares.IsDefaultOrEmpty && _globalMiddlewares.Length > 0) ||
            (_perCommand.Count > 0) ||
            (_policies.Count > 0);

        if (!hasAny) return;

        var plan = PipelinePlanner.Build(_globalMiddlewares, _perCommand, _policies, result, options);
        if (!plan.ShouldEmit) return;

        var source = PipelineSourceWriter.Write(plan);

        context.AddSource(
            hintName: "TinyDispatcherPipeline.g.cs",
            sourceText: SourceText.From(source, Encoding.UTF8));
    }
}
