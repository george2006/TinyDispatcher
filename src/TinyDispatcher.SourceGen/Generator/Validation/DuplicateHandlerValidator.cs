#nullable enable

using System;
using System.Collections.Generic;
using TinyDispatcher.SourceGen.Generator.Models;

namespace TinyDispatcher.SourceGen.Generator.Validation;

internal sealed class DuplicateHandlerValidator : IGeneratorValidator
{
    public void Validate(GeneratorValidationContext context, DiagnosticBag diags)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (diags is null)
        {
            throw new ArgumentNullException(nameof(diags));
        }

        ValidateDuplicateCommandHandlers(context, diags);
        ValidateDuplicateQueryHandlers(context, diags);
    }

    private static void ValidateDuplicateCommandHandlers(
        GeneratorValidationContext context,
        DiagnosticBag diags)
    {
        var commandsByMessageType = new Dictionary<string, HandlerContract>(StringComparer.Ordinal);
        var reportedMessageTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var command in context.DiscoveryResult.Commands)
        {
            var isFirstHandlerForCommand = TryRememberFirstCommandHandler(commandsByMessageType, command);
            if (isFirstHandlerForCommand)
            {
                continue;
            }

            var duplicateWasAlreadyReported = WasAlreadyReported(reportedMessageTypes, command.MessageTypeFqn);
            if (duplicateWasAlreadyReported)
            {
                continue;
            }

            ReportDuplicateCommandHandler(context, diags, commandsByMessageType, command);
        }
    }

    private static void ValidateDuplicateQueryHandlers(
        GeneratorValidationContext context,
        DiagnosticBag diags)
    {
        var queriesByMessageType = new Dictionary<string, QueryHandlerContract>(StringComparer.Ordinal);
        var reportedMessageTypes = new HashSet<string>(StringComparer.Ordinal);

        foreach (var query in context.DiscoveryResult.Queries)
        {
            var isFirstHandlerForQuery = TryRememberFirstQueryHandler(queriesByMessageType, query);
            if (isFirstHandlerForQuery)
            {
                continue;
            }

            var duplicateWasAlreadyReported = WasAlreadyReported(reportedMessageTypes, query.QueryTypeFqn);
            if (duplicateWasAlreadyReported)
            {
                continue;
            }

            ReportDuplicateQueryHandler(context, diags, queriesByMessageType, query);
        }
    }

    private static bool TryRememberFirstCommandHandler(
        Dictionary<string, HandlerContract> commandsByMessageType,
        HandlerContract command)
    {
        var handlerAlreadyExists = commandsByMessageType.ContainsKey(command.MessageTypeFqn);

        if (handlerAlreadyExists)
        {
            return false;
        }

        commandsByMessageType[command.MessageTypeFqn] = command;
        return true;
    }

    private static bool TryRememberFirstQueryHandler(
        Dictionary<string, QueryHandlerContract> queriesByMessageType,
        QueryHandlerContract query)
    {
        var handlerAlreadyExists = queriesByMessageType.ContainsKey(query.QueryTypeFqn);

        if (handlerAlreadyExists)
        {
            return false;
        }

        queriesByMessageType[query.QueryTypeFqn] = query;
        return true;
    }

    private static bool WasAlreadyReported(HashSet<string> reportedMessageTypes, string messageTypeFqn)
    {
        return !reportedMessageTypes.Add(messageTypeFqn);
    }

    private static void ReportDuplicateCommandHandler(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        Dictionary<string, HandlerContract> commandsByMessageType,
        HandlerContract duplicateCommand)
    {
        var firstCommand = commandsByMessageType[duplicateCommand.MessageTypeFqn];

        diags.Add(context.Diagnostics.Create(
            context.Diagnostics.DuplicateCommand,
            duplicateCommand.MessageTypeFqn,
            firstCommand.HandlerTypeFqn,
            duplicateCommand.HandlerTypeFqn));
    }

    private static void ReportDuplicateQueryHandler(
        GeneratorValidationContext context,
        DiagnosticBag diags,
        Dictionary<string, QueryHandlerContract> queriesByMessageType,
        QueryHandlerContract duplicateQuery)
    {
        var firstQuery = queriesByMessageType[duplicateQuery.QueryTypeFqn];

        diags.Add(context.Diagnostics.Create(
            context.Diagnostics.DuplicateQuery,
            duplicateQuery.QueryTypeFqn,
            firstQuery.HandlerTypeFqn,
            duplicateQuery.HandlerTypeFqn));
    }
}
