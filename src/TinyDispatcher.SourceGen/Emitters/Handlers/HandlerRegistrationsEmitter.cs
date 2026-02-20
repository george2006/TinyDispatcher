using Microsoft.CodeAnalysis.Text;
using System.Text;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.Handlers;

internal sealed class HandlerRegistrationsEmitter : ICodeEmitter
{
    public void Emit(IGeneratorContext context, DiscoveryResult result, GeneratorOptions options)
    {
        var plan = HandlerRegistrationsPlanner.Build(result, options);

        var sourceWriter = new HandlerRegistrationsSourceWriter();
        var source = sourceWriter.Write(plan);

        context.AddSource(
            "ThisAssemblyHandlerRegistrations.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }
}