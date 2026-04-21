using Microsoft.CodeAnalysis.Text;
using System.Text;

using TinyDispatcher.SourceGen.Generator;

namespace TinyDispatcher.SourceGen.Emitters.Handlers;

internal sealed class HandlerRegistrationsEmitter
{
    public void Emit(IGeneratorContext context, HandlerRegistrationsPlan plan)
    {
        var sourceWriter = new HandlerRegistrationsSourceWriter();
        var source = sourceWriter.Write(plan);

        context.AddSource(
            "ThisAssemblyHandlerRegistrations.g.cs",
            SourceText.From(source, Encoding.UTF8));
    }
}
