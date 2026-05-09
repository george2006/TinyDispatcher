namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ReferencedHandlerContribution(
    string AssemblyName,
    string? ContextTypeFqn,
    HandlerContract Handler)
{
    public bool MatchesContext(string expectedContextFqn)
    {
        if (string.IsNullOrWhiteSpace(ContextTypeFqn) ||
            string.IsNullOrWhiteSpace(expectedContextFqn))
        {
            return true;
        }

        return string.Equals(
            ContextTypeFqn,
            expectedContextFqn,
            System.StringComparison.Ordinal);
    }
}
