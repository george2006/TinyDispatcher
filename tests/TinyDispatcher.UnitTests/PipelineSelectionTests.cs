using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
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
        public async Task Command_pipeline_is_chosen_over_policy_and_global()
        {
            // Arrange
            using var sp = BuildProvider(services =>
            {
                services.AddTransient<ICommandPipeline<TestCommand, TestContext>, CommandPipeline>();
                services.AddTransient<IPolicyCommandPipeline<TestCommand, TestContext>, PolicyPipeline>();
                services.AddTransient<IGlobalCommandPipeline<TestCommand, TestContext>, GlobalPipeline>();
            });

            var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();
            var tracker = sp.GetRequiredService<CallTracker>();

            // Act
            await dispatcher.DispatchAsync(new TestCommand(string.Empty), CancellationToken.None);

            // Assert
            Assert.True(tracker.CommandPipelineCalled);
            Assert.False(tracker.PolicyPipelineCalled);
            Assert.False(tracker.GlobalPipelineCalled);
            Assert.True(tracker.HandlerCalled);
        }

        [Fact]
        public async Task Policy_pipeline_is_chosen_over_global_when_no_command_pipeline()
        {
            // Arrange
            using var sp = BuildProvider(services =>
            {
                services.AddTransient<IPolicyCommandPipeline<TestCommand, TestContext>, PolicyPipeline>();
                services.AddTransient<IGlobalCommandPipeline<TestCommand, TestContext>, GlobalPipeline>();
            });

            var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();
            var tracker = sp.GetRequiredService<CallTracker>();

            // Act
            await dispatcher.DispatchAsync(new TestCommand("x"), CancellationToken.None);

            // Assert
            Assert.False(tracker.CommandPipelineCalled);
            Assert.True(tracker.PolicyPipelineCalled);
            Assert.False(tracker.GlobalPipelineCalled);
            Assert.True(tracker.HandlerCalled);
        }

        [Fact]
        public async Task Global_pipeline_is_used_when_no_command_or_policy_pipeline()
        {
            // Arrange
            using var sp = BuildProvider(services =>
            {
                services.AddTransient<IGlobalCommandPipeline<TestCommand, TestContext>, GlobalPipeline>();
            });

            var dispatcher = sp.GetRequiredService<IDispatcher<TestContext>>();
            var tracker = sp.GetRequiredService<CallTracker>();

            // Act
            await dispatcher.DispatchAsync(new TestCommand("x"), CancellationToken.None);

            // Assert
            Assert.False(tracker.CommandPipelineCalled);
            Assert.False(tracker.PolicyPipelineCalled);
            Assert.True(tracker.GlobalPipelineCalled);
            Assert.True(tracker.HandlerCalled);
        }

        private static ServiceProvider BuildProvider(Action<IServiceCollection> configurePipelines)
        {
            var services = new ServiceCollection();

            // Shared fixtures
            services.AddSingleton<CallTracker>();
            services.AddScoped<IContextFactory<TestContext>, TestContextFactory>();
            services.AddTransient<ICommandHandler<TestCommand,TestContext>,TestHandler>();
            // Pipelines for this test
            configurePipelines(services);

            // Dispatcher
            services.AddScoped<IDispatcher<TestContext>>(sp =>
                new Dispatcher<TestContext>(sp, sp.GetRequiredService<IContextFactory<TestContext>>()));

            return services.BuildServiceProvider();
        }
    }
}
