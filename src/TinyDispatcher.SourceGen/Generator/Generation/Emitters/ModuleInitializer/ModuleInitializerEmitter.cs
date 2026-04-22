#nullable enable

using System.Text;
using Microsoft.CodeAnalysis.Text;

using TinyDispatcher.SourceGen.Generator;

namespace TinyDispatcher.SourceGen.Generator.Generation.Emitters.ModuleInitializer;

internal sealed class ModuleInitializerEmitter
{
    public void Emit(IGeneratorContext context, ModuleInitializerPlan plan)
    {
        if (!plan.ShouldEmit)
            return;

        var source = ModuleInitializerSourceWriter.Write(plan);

        context.AddSource(
            "DispatcherModuleInitializer.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }
}

