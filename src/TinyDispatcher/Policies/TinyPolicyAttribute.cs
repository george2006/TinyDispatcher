using System;

namespace TinyDispatcher;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class TinyPolicyAttribute : Attribute
{
}
