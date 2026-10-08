using System.Reflection;
using ScriptEditor.Runtime.Services;
using Xunit;

namespace ScriptEditor.Tests.Runtime;

public sealed class AsyncCancellationEntryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AsyncEntryReturnsWhilePreviousCancellationCallbackIsPending(bool loop)
    {
        var session = new ScriptSession(() => "");
        using var previous = new CancellationTokenSource();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var release = new ManualResetEventSlim();
        using var registration = previous.Token.Register(() =>
        {
            entered.TrySetResult();
            if (!release.Wait(TimeSpan.FromSeconds(10))) throw new TimeoutException();
        });
        typeof(ScriptSession).GetField(loop ? "_loopCts" : "_executionCts", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session, previous);
        var returned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var operation = Task.Run(async () =>
        {
            var pending = loop ? session.LoopRunAsync() : session.RunAsync();
            returned.TrySetResult();
            await pending;
        });
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Same(returned.Task, await Task.WhenAny(returned.Task, Task.Delay(1000)));
            if (loop)
            {
                var gate = typeof(ScriptSession).GetField("_loopLock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
                Assert.True(Monitor.TryEnter(gate));
                Monitor.Exit(gate);
            }
        }
        finally
        {
            release.Set();
            await returned.Task.WaitAsync(TimeSpan.FromSeconds(10));
            session.StopLoop();
            await operation.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }
}
