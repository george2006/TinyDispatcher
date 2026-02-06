using System;
using System.Collections.Generic;
using System.Text;

namespace TinyDispatcher.SourceGen.Generator.Models;

public readonly record struct MiddlewareRef(string OpenTypeFqn, int Arity);
