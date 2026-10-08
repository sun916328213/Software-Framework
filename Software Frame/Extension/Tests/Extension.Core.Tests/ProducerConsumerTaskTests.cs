using System.Reflection;
using Extension.Patterns.Pipelines.ProducerConsumer;
using Xunit;

namespace Extension.Core.Tests;

public sealed class ProducerConsumerTaskTests
{
    private static Task Run(Func<int, CancellationToken, Task> produce,
        Func<int, CancellationToken, Task> consume, bool rethrowErrors) =>
        (Task)typeof(ProducerConsumer<int>).Assembly
            .GetType("Extension.Patterns.Pipelines.ProducerConsumer.ProducerConsumerTasks", true)!
            .GetMethod("RunAsync", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { 1, 1, produce, consume, rethrowErrors, CancellationToken.None })!;

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task WorkerFaultsFollowSelectedPolicy(bool rethrowErrors, bool producerFails)
    {
        var error = new InvalidOperationException("worker failed");
        Func<int, CancellationToken, Task> fail = (_, _) => Task.FromException(error);
        Func<int, CancellationToken, Task> succeed = (_, _) => Task.CompletedTask;
        var run = Run(producerFails ? fail : succeed, producerFails ? succeed : fail, rethrowErrors);

        if (rethrowErrors)
            Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(() => run));
        else
            await run;
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WorkerCancellationIsAbsorbedInBothPolicies(bool rethrowErrors)
    {
        Func<int, CancellationToken, Task> cancel = (_, _) =>
            Task.FromException(new OperationCanceledException());
        await Run(cancel, cancel, rethrowErrors);
    }
}
