#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TinyDispatcher.PipelineMaps;

public static class DispatcherPipelineMaps
{
    public static IReadOnlyList<DispatcherPipelineMap> Get()
    {
        return Get(AppDomain.CurrentDomain.GetAssemblies());
    }

    public static IReadOnlyList<DispatcherPipelineMap> Get(Assembly assembly)
    {
        if (assembly is null) throw new ArgumentNullException(nameof(assembly));

        return Get(new[] { assembly });
    }

    private static IReadOnlyList<DispatcherPipelineMap> Get(IEnumerable<Assembly> assemblies)
    {
        return assemblies
            .SelectMany(assembly => assembly
                .GetCustomAttributes<TinyDispatcherPipelineMapAttribute>()
                .Select(CreateMap))
            .OrderBy(map => GetTypeIdentity(map.PipelineType), StringComparer.Ordinal)
            .ToArray();
    }

    private static DispatcherPipelineMap CreateMap(TinyDispatcherPipelineMapAttribute attribute)
    {
        var operationStart = attribute.GlobalMiddlewareCount + attribute.PolicyMiddlewareCount;

        return new DispatcherPipelineMap(
            attribute.PipelineType,
            attribute.ContextType,
            Array.AsReadOnly(attribute.CommandTypes),
            GetMiddlewares(attribute, 0, attribute.GlobalMiddlewareCount),
            attribute.PolicyType,
            GetMiddlewares(
                attribute,
                attribute.GlobalMiddlewareCount,
                attribute.PolicyMiddlewareCount),
            GetMiddlewares(
                attribute,
                operationStart,
                attribute.MiddlewareTypes.Length - operationStart));
    }

    private static IReadOnlyList<Type> GetMiddlewares(
        TinyDispatcherPipelineMapAttribute attribute,
        int start,
        int count)
    {
        var middlewares = new Type[count];
        Array.Copy(attribute.MiddlewareTypes, start, middlewares, 0, count);
        return Array.AsReadOnly(middlewares);
    }

    private static string GetTypeIdentity(Type type)
        => type.AssemblyQualifiedName ?? type.FullName ?? type.Name;
}
