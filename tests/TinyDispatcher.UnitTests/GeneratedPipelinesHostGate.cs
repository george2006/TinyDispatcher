using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static TinyDispatcher.UnitTests.ConcurrentDispatchSameCommandTypeTests;

namespace TinyDispatcher.UnitTests
{
    // -----------------------------------------------------------------------------
    // HOST GATE (compile-time only):
    // This exists so SourceGen sees ALL the configuration up-front and generates pipelines.
    // It is never executed.
    // -----------------------------------------------------------------------------
    internal static class GeneratedPipelinesHostGate
    {
        // Do not call. Just needs to exist in the compilation.
        public static void Configure(IServiceCollection services)
        {
            services.UseTinyDispatcher<TestContext>(tiny =>
            {
                tiny.UseGlobalMiddleware(typeof(GlobalLogMiddleware<,>));
                tiny.UseGlobalMiddleware(typeof(ProbeMiddleware0<,>));
                tiny.UseGlobalMiddleware(typeof(ProbeMiddleware1<,>));
                
                tiny.UseMiddlewareFor<TestCommand>(typeof(PerCommandLogMiddleware<,>));
                
                tiny.UsePolicy<CheckoutPolicy>();
                tiny.UsePolicy<PolicyOnlyPolicy>();
            });
        }
    }
}
