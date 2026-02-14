using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
using TinyDispatcher.UnitTests;
using Xunit;

namespace TinyDispatcher.UnitTets
{
    public sealed class PipelineSelectionTests
    {
        [Fact]
        public async Task Pipeline_is_used_when_registered()
        {
            // Arrange
            using var sp = BuildProvider(services =>
            {
                services.AddTransient<ICommandPipeline<TestCommand, TestContext>, CommandPipeline>();
            });

            var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();
            var tracker = sp.GetRequiredService<CallTracker>();

            // Act
            await dispatcher.DispatchAsync(new TestCommand(string.Empty), CancellationToken.None);

            // Assert
            Assert.True(tracker.PipelineCalled);
            Assert.True(tracker.HandlerCalled);
        }

        [Fact]
        public async Task Handler_is_called_directly_when_no_pipeline_registered()
        {
            // Arrange
            using var sp = BuildProvider(services =>
            {
                // no pipeline
            });

            var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();
            var tracker = sp.GetRequiredService<CallTracker>();

            // Act
            await dispatcher.DispatchAsync(new TestCommand("x"), CancellationToken.None);

            // Assert
            Assert.False(tracker.PipelineCalled);
            Assert.True(tracker.HandlerCalled);
        }

        private static ServiceProvider BuildProvider(Action<IServiceCollection> configurePipelines)
        {
            var services = new ServiceCollection();

            // Shared fixtures
            services.AddSingleton<CallTracker>();
            services.AddScoped<IContextFactory<TestContext>, TestContextFactory>();
            services.AddTransient<ICommandHandler<TestCommand, TestContext>, TestHandler>();

            // Pipelines for this test
            configurePipelines(services);

            // Dispatcher
            services.AddScoped<IDispatcher<TestContext>>(sp =>
                new Dispatcher<TestContext>(sp, sp.GetRequiredService<IContextFactory<TestContext>>()));

            return services.BuildServiceProvider();
        }
    }
}
