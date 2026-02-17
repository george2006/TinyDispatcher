using System;
using System.Collections.Generic;
using System.Text;

namespace TinyDispatcher.SourceGen.Generator.Models;
public sealed record QueryHandlerContract(
      string QueryTypeFqn,
      string ResultTypeFqn,
      string HandlerTypeFqn);
