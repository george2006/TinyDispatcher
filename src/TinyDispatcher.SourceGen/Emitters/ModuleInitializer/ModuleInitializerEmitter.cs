#nullable enable

using System.Text;
using Microsoft.CodeAnalysis.Text;
using TinyDispatcher.SourceGen.Abstractions;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Emitters.ModuleInitializer;

internal sealed class ModuleInitializerEmitter : ICodeEmitter
{
    public void Emit(IGeneratorContext context, DiscoveryResult result, GeneratorOptions options)
    {
        var plan = ModuleInitializerPlanner.Build(result, options);
        if (!plan.ShouldEmit)
            return;

        var source = ModuleInitializerSourceWriter.Write(plan);

        context.AddSource(
            "DispatcherModuleInitializer.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }
}
