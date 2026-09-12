using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace FifthBox.ServerManager.Eventing.Tests;

[TestClass]
public class InProcessEventPublisherTests
{
    private sealed record TestEvent(string Value);

    private sealed class RecordingHandler : IEventHandler<TestEvent>
    {
        public int Calls { get; private set; }
        public TestEvent? Last { get; private set; }

        public Task HandleAsync(TestEvent @event, CancellationToken ct = default)
        {
            Calls++;
            Last = @event;
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IEventHandler<TestEvent>
    {
        public Task HandleAsync(TestEvent @event, CancellationToken ct = default) =>
            throw new InvalidOperationException("boom");
    }

    [TestMethod]
    public async Task Invokes_all_registered_handlers()
    {
        var h1 = new RecordingHandler();
        var h2 = new RecordingHandler();
        var publisher = BuildPublisher(services =>
        {
            services.AddSingleton<IEventHandler<TestEvent>>(h1);
            services.AddSingleton<IEventHandler<TestEvent>>(h2);
        });

        await publisher.PublishAsync(new TestEvent("x"));

        Assert.AreEqual(1, h1.Calls);
        Assert.AreEqual(1, h2.Calls);
        Assert.AreEqual("x", h1.Last!.Value);
    }

    [TestMethod]
    public async Task A_throwing_handler_does_not_stop_the_others()
    {
        var good = new RecordingHandler();
        var publisher = BuildPublisher(services =>
        {
            services.AddSingleton<IEventHandler<TestEvent>>(new ThrowingHandler());
            services.AddSingleton<IEventHandler<TestEvent>>(good);
        });

        await publisher.PublishAsync(new TestEvent("y"));   // must not throw

        Assert.AreEqual(1, good.Calls);
    }

    [TestMethod]
    public async Task No_handlers_is_a_no_op()
    {
        var publisher = BuildPublisher(_ => { });
        await publisher.PublishAsync(new TestEvent("z"));   // must not throw
    }

    private static IEventPublisher BuildPublisher(Action<IServiceCollection> register)
    {
        var services = new ServiceCollection();
        register(services);
        var provider = services.BuildServiceProvider();
        return new InProcessEventPublisher(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<InProcessEventPublisher>.Instance);
    }
}
