namespace TinyDispatcher.SourceGen.Generator.Models;

internal sealed record ReferencedHandlerContribution(
    string? ContextTypeFqn,
    HandlerContract Handler)
{
    public bool MatchesContext(string contextFqn)
    {
        if (string.IsNullOrWhiteSpace(ContextTypeFqn) ||
            string.IsNullOrWhiteSpace(contextFqn))
        {
            return true;
        }

        return string.Equals(
            ContextTypeFqn,
            contextFqn,
            System.StringComparison.Ordinal);
    }
}
