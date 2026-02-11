using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TinyDispatcher;
using TinyDispatcher.Context;
using TinyDispatcher.Dispatching;
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
            await dispatcher.DispatchAsync(new TestCommand(), CancellationToken.None);

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
            await dispatcher.DispatchAsync(new TestCommand(), CancellationToken.None);

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
            await dispatcher.DispatchAsync(new TestCommand(), CancellationToken.None);

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
            services.AddSingleton<IContextFactory<TestContext>, TestContextFactory>();
            services.AddTransient<TestHandler>();

            // Registry: map command -> handler
            services.AddSingleton<IDispatcherRegistry>(_ =>
                new DefaultDispatcherRegistry(
                    commandHandlers: new[]
                    {
                        new KeyValuePair<Type, Type>(typeof(TestCommand), typeof(TestHandler))
                    },
                    queryHandlers: Array.Empty<KeyValuePair<Type, Type>>()));

            // Pipelines for this test
            configurePipelines(services);

            // Dispatcher
            services.AddSingleton<IDispatcher<TestContext>>(sp =>
                new Dispatcher<TestContext>(sp, sp.GetRequiredService<IDispatcherRegistry>(), sp.GetRequiredService<IContextFactory<TestContext>>()));

            return services.BuildServiceProvider();
        }

        // -----------------------
        // Test fixtures
        // -----------------------

        public sealed class CallTracker
        {
            public bool CommandPipelineCalled { get; set; }
            public bool PolicyPipelineCalled { get; set; }
            public bool GlobalPipelineCalled { get; set; }
            public bool HandlerCalled { get; set; }
        }

        public sealed class TestCommand : ICommand { }

        public sealed class TestContext { }

        public sealed class TestContextFactory : IContextFactory<TestContext>
        {
            public ValueTask<TestContext> CreateAsync(CancellationToken ct = default)
                => new(new TestContext());
        }

        public sealed class TestHandler : ICommandHandler<TestCommand, TestContext>
        {
            private readonly CallTracker _tracker;

            public TestHandler(CallTracker tracker) => _tracker = tracker;

            public Task HandleAsync(TestCommand command, TestContext ctx, CancellationToken ct = default)
            {
                _tracker.HandlerCalled = true;
                return Task.CompletedTask;
            }
        }

        private sealed class CommandPipeline : ICommandPipeline<TestCommand, TestContext>
        {
            private readonly CallTracker _tracker;

            public CommandPipeline(CallTracker tracker) => _tracker = tracker;

            public Task ExecuteAsync(
                TestCommand command,
                TestContext ctx,
                ICommandHandler<TestCommand, TestContext> handler,
                CancellationToken ct = default)
            {
                _tracker.CommandPipelineCalled = true;
                return handler.HandleAsync(command, ctx, ct);
            }
        }

        private sealed class PolicyPipeline : IPolicyCommandPipeline<TestCommand, TestContext>
        {
            private readonly CallTracker _tracker;

            public PolicyPipeline(CallTracker tracker) => _tracker = tracker;

            public Task ExecuteAsync(
                TestCommand command,
                TestContext ctx,
                ICommandHandler<TestCommand, TestContext> handler,
                CancellationToken ct = default)
            {
                _tracker.PolicyPipelineCalled = true;
                return handler.HandleAsync(command, ctx, ct);
            }
        }

        private sealed class GlobalPipeline : IGlobalCommandPipeline<TestCommand, TestContext>
        {
            private readonly CallTracker _tracker;

            public GlobalPipeline(CallTracker tracker) => _tracker = tracker;

            public Task ExecuteAsync(
                TestCommand command,
                TestContext ctx,
                ICommandHandler<TestCommand, TestContext> handler,
                CancellationToken ct = default)
            {
                _tracker.GlobalPipelineCalled = true;
                return handler.HandleAsync(command, ctx, ct);
            }
        }
    }
}
