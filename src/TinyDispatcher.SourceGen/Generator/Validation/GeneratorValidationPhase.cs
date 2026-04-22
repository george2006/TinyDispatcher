#nullable enable

using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Diagnostics;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class GeneratorValidationPhase
{
    public GeneratorValidationResult Validate(
        Compilation compilation,
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        var commandMiddlewareInterface =
            compilation.GetTypeByMetadataName("TinyDispatcher.ICommandMiddleware`2");
        var middlewareTypeResolver = new MiddlewareTypeResolver(compilation);

        var validationContext = BuildValidationContext(
            hostBootstrap,
            extraction,
            diagnosticsCatalog);
        var diagnostics = GeneratorValidator.Validate(
            validationContext,
            commandMiddlewareInterface,
            middlewareTypeResolver);

        return new GeneratorValidationResult(validationContext, diagnostics);
    }

    private static GeneratorValidationContext BuildValidationContext(
        HostBootstrapInfo hostBootstrap,
        GeneratorExtraction extraction,
        DiagnosticsCatalog diagnosticsCatalog)
    {
        return new GeneratorValidationContext.Builder(
                extraction.Discovery,
                diagnosticsCatalog)
            .WithHostGate(isHost: hostBootstrap.IsHostProject)
            .WithUseTinyDispatcherCalls(hostBootstrap.UseTinyDispatcherCalls)
            .WithExpectedContext(hostBootstrap.ExpectedContextFqn)
            .WithPipelineConfig(extraction.Pipeline)
            .Build();
    }
}
