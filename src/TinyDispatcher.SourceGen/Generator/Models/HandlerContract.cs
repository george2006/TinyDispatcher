using System;
using System.Collections.Generic;
using System.Text;

namespace TinyDispatcher.SourceGen.Generator.Models;
public sealed record HandlerContract(
        string MessageTypeFqn,
        string HandlerTypeFqn);

