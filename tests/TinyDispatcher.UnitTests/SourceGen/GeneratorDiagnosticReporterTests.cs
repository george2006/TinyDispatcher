#nullable enable

using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Generator;
using TinyDispatcher.SourceGen.Validation;
using Xunit;

namespace TinyDispatcher.UnitTests.SourceGen;

public sealed class GeneratorDiagnosticReporterTests
{
    [Fact]
    public void ReportAndHasErrors_reports_all_diagnostics_and_returns_true_when_any_are_errors()
    {
        var context = new CapturingGeneratorContext();
        var warning = CreateDiagnostic("TEST001", DiagnosticSeverity.Warning);
        var error = CreateDiagnostic("TEST002", DiagnosticSeverity.Error);
        var bag = new DiagnosticBag();
        bag.Add(warning);
        bag.Add(error);

        var hasErrors = GeneratorDiagnosticReporter.ReportAndHasErrors(context, bag);

        Assert.True(hasErrors);
        Assert.Collection(
            context.Diagnostics,
            diagnostic => Assert.Same(warning, diagnostic),
            diagnostic => Assert.Same(error, diagnostic));
    }

    [Fact]
    public void ReportAndHasErrors_returns_false_when_bag_is_empty()
    {
        var context = new CapturingGeneratorContext();
        var bag = new DiagnosticBag();

        var hasErrors = GeneratorDiagnosticReporter.ReportAndHasErrors(context, bag);

        Assert.False(hasErrors);
        Assert.Empty(context.Diagnostics);
    }

    private static Diagnostic CreateDiagnostic(string id, DiagnosticSeverity severity)
    {
        return Diagnostic.Create(
            new DiagnosticDescriptor(
                id,
                "Test diagnostic",
                "Test diagnostic",
                "Tests",
                severity,
                isEnabledByDefault: true),
            Location.None);
    }

}
