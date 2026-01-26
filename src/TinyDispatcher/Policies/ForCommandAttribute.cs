using System;

namespace TinyDispatcher;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class ForCommandAttribute : Attribute
{
    public ForCommandAttribute(Type commandType)
    {
        if (commandType is null) throw new ArgumentNullException(nameof(commandType));
        CommandType = commandType;
    }

    public Type CommandType { get; }
}
