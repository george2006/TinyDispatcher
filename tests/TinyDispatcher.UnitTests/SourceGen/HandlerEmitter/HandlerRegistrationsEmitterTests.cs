#nullable enable

using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TinyDispatcher.SourceGen;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Emitters.Handlers;
using TinyDispatcher.SourceGen.Generator.Models;
using Xunit;

namespace TinyDispatcher.UnitTest.SourceGen.HandlerEmitter;

public sealed class HandlerRegistrationsEmitterTests
{
    [Fact]
    public void Emit_adds_source_with_expected_hint_name()
    {
        var ctx = new FakeGeneratorContext();

        var result = new DiscoveryResult(
            Commands: System.Collections.Immutable.ImmutableArray.Create(
                new HandlerContract("global::A.Cmd", "global::A.CmdHandler")),
            Queries: System.Collections.Immutable.ImmutableArray.Create(
                new QueryHandlerContract("global::A.Q", "global::A.R", "global::A.QHandler")));

        var options = new GeneratorOptions(
            GeneratedNamespace: "Acme.Gen",
            EmitDiExtensions: true,
            EmitHandlerRegistrations: true,
            IncludeNamespacePrefix: null,
            CommandContextType: "global::Acme.Ctx",
            EmitPipelineMap: false,
            PipelineMapFormat: null);

        var sut = new HandlerRegistrationsEmitter();

        sut.Emit(ctx, result, options);

        Assert.Single(ctx.Sources);
        Assert.True(ctx.Sources.ContainsKey("ThisAssemblyHandlerRegistrations.g.cs"));

        var src = ctx.Sources["ThisAssemblyHandlerRegistrations.g.cs"].ToString();

        Assert.Contains("namespace Acme.Gen", src);
        Assert.Contains("static partial void AddGeneratedHandlers(IServiceCollection services)", src);
        Assert.Contains("ICommandHandler<global::A.Cmd, global::Acme.Ctx>", src);
        Assert.Contains("IQueryHandler<global::A.Q, global::A.R>", src);

        Assert.Empty(ctx.Diagnostics);
    }

    private sealed class FakeGeneratorContext : IGeneratorContext
    {
        public Dictionary<string, SourceText> Sources { get; } = new();
        public List<Diagnostic> Diagnostics { get; } = new();

        public void AddSource(string hintName, SourceText sourceText)
            => Sources.Add(hintName, sourceText);

        public void ReportDiagnostic(Diagnostic diagnostic)
            => Diagnostics.Add(diagnostic);
    }
}