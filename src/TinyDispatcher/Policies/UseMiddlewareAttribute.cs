using System;

namespace TinyDispatcher;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class UseMiddlewareAttribute : Attribute
{
    public UseMiddlewareAttribute(Type openGenericMiddlewareType)
    {
        if (openGenericMiddlewareType is null) throw new ArgumentNullException(nameof(openGenericMiddlewareType));
        if (!openGenericMiddlewareType.IsGenericTypeDefinition)
            throw new ArgumentException(
                "Middleware must be an open generic type, e.g. typeof(MyMiddleware<,>)",
                nameof(openGenericMiddlewareType));

        OpenGenericMiddlewareType = openGenericMiddlewareType;
    }

    public Type OpenGenericMiddlewareType { get; }
}
