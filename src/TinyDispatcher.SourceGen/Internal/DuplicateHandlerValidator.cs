using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using TinyDispatcher.SourceGen.Abstractions;

namespace TinyDispatcher.SourceGen
{
    /// <summary>
    /// Validates that there are no multiple handlers registered for the same
    /// command or query type in the discovery result.
    /// </summary>
    public sealed class DuplicateHandlerValidator : IValidator
    {
        private readonly IDiagnostics _diagnostics;

        public DuplicateHandlerValidator(IDiagnostics diagnostics)
        {
            _diagnostics = diagnostics ?? throw new ArgumentNullException(nameof(diagnostics));
        }

        /// <summary>
        /// Runs duplicate-handler validations over the discovery result and returns
        /// any diagnostics that should be reported.
        /// </summary>
        public ImmutableArray<Diagnostic> Validate(DiscoveryResult result)
        {
            var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

            ValidateCommands(result, diagnostics);
            ValidateQueries(result, diagnostics);

            return diagnostics.ToImmutable();
        }

        #region Command validation

        private void ValidateCommands(
            DiscoveryResult result,
            ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            var duplicateGroups = result.Commands
                .GroupBy(c => c.MessageTypeFqn)
                .Where(g => g.Count() > 1);

            foreach (var group in duplicateGroups)
            {
                AddDuplicateCommandDiagnostic(group, diagnostics);
            }
        }

        private void AddDuplicateCommandDiagnostic(
            IGrouping<string, HandlerContract> group,
            ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            var handlers = group
                .Select(c => c.HandlerTypeFqn)
                .Distinct()
                .ToArray();

            if (handlers.Length < 2)
                return;

            diagnostics.Add(
                Diagnostic.Create(
                    _diagnostics.DuplicateCommand,
                    Location.None,
                    group.Key,
                    handlers[0],
                    handlers[1]));
        }

        #endregion

        #region Query validation

        private void ValidateQueries(
            DiscoveryResult result,
            ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            var duplicateGroups = result.Queries
                .GroupBy(q => q.QueryTypeFqn)
                .Where(g => g.Count() > 1);

            foreach (var group in duplicateGroups)
            {
                AddDuplicateQueryDiagnostic(group, diagnostics);
            }
        }

        private void AddDuplicateQueryDiagnostic(
            IGrouping<string, QueryHandlerContract> group,
            ImmutableArray<Diagnostic>.Builder diagnostics)
        {
            var handlers = group
                .Select(q => q.HandlerTypeFqn)
                .Distinct()
                .ToArray();

            var results = group
                .Select(q => q.ResultTypeFqn)
                .Distinct()
                .ToArray();

            var queryDescription = $"{group.Key} (results: {string.Join(", ", results)})";

            var firstHandler = handlers.ElementAtOrDefault(0);
            var secondHandler = handlers.ElementAtOrDefault(1);

            diagnostics.Add(
                Diagnostic.Create(
                    _diagnostics.DuplicateQuery,
                    Location.None,
                    queryDescription,
                    firstHandler,
                    secondHandler));
        }

        #endregion
    }
}
