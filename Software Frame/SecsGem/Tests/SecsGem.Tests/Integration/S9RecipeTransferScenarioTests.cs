using System.Text;
using SecsGem.Runtime;
using SecsGem.Runtime.Protocol;
using SecsGem.Tests.Infrastructure;
using Xunit;

namespace SecsGem.Tests.Integration;

/// <summary>Host 经真实 Loopback 完成业务流程；只推进业务时钟，不伪造通信回包。</summary>
public sealed class S9RecipeTransferScenarioTests
{
    private static readonly byte[] Recipe = Encoding.UTF8.GetBytes("{\"version\":1,\"exposureUs\":5000,\"gain\":1.5,\"speedMmPerSec\":100}");
    private static readonly byte[] Id = { 0x41, 5, 74, 79, 66, 45, 65 }; // A JOB-A
    private static byte[] DownloadBody() => new byte[] { 1, 2 }.Concat(Id).Concat(new byte[] { 0x21, (byte)Recipe.Length }).Concat(Recipe).ToArray();

    private static async Task Connect(SecsGemRuntime runtime, HsmsHostSimulator host, CancellationToken token)
    {
        runtime.ConfigureProtocolScenarios(new() { MaxMessageBodyBytes = 128, ProcessProgramTransferTimeout = TimeSpan.FromSeconds(30) });
        await runtime.ConnectAsync(true, "127.0.0.1", host.Port, 0);
        await host.WaitForDeviceAsync(token);
        await host.CompleteSelectAsync(token);
        while (!runtime.IsConnected) await Task.Delay(10, token);
    }

    private static async Task Grant(SecsGemRuntime runtime, HsmsHostSimulator host, uint transaction, CancellationToken token)
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Info(string text) { if (text.Contains("PPID=JOB-A：S7F1 已获准")) started.TrySetResult(); }
        runtime.OnInfo += Info;
        try
        {
            var body = new byte[] { 1, 2 }.Concat(Id).Concat(new byte[] { 0xB1, 4, 0, 0, 0, (byte)Recipe.Length }).ToArray();
            host.SendDataMessage(7, 1, true, 0, transaction, body, token);
            var reply = await host.ReadMessageAsync(token);
            Assert.Equal((byte)7, reply.S);
            Assert.Equal((byte)2, reply.F);
            Assert.Equal(new byte[] { 0x21, 1, 0 }, reply.Body);
            await started.Task.WaitAsync(token); // 确保计时器已建立，而不是靠 Delay 猜测。
        }
        finally { runtime.OnInfo -= Info; }
    }

    private static async Task Download(HsmsHostSimulator host, CancellationToken token)
    {
        host.SendDataMessage(7, 3, true, 0, 0x70000002, DownloadBody(), token);
        var reply = await host.ReadMessageAsync(token);
        Assert.Equal((byte)7, reply.S);
        Assert.Equal((byte)4, reply.F);
        Assert.Equal(new byte[] { 0x21, 1, 0 }, reply.Body);
    }

    private static async Task ReadBack(HsmsHostSimulator host, CancellationToken token)
    {
        host.SendDataMessage(7, 5, true, 0, 0x70000003, Id, token);
        var reply = await host.ReadMessageAsync(token);
        Assert.Equal((byte)7, reply.S);
        Assert.Equal((byte)6, reply.F);
        Assert.Equal(DownloadBody(), reply.Body);
    }

    [Theory(Timeout = 30000)]
    [InlineData(false)] [InlineData(true)]
    public async Task Host_Grant_Download_Readback_And_Timeout_Then_Late_Download_Are_End_To_End(bool withholdDownload)
    {
        using var host = HsmsHostSimulator.Start();
        var clock = new ManualScenarioTimeProvider();
        using var runtime = RuntimeTestFixture.CreateReal(clock);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await Connect(runtime, host, timeout.Token);
        await Grant(runtime, host, 0x70000001, timeout.Token);
        Assert.Null(runtime.GetUnformattedProcessProgram("JOB-A"));
        if (withholdDownload)
        {
            clock.Advance(TimeSpan.FromSeconds(30));
            var error = await host.ReadMessageAsync(timeout.Token);
            Assert.Equal((byte)9, error.S);
            Assert.Equal((byte)13, error.F);
            Assert.False(error.WBit);
            Assert.Equal(new byte[] { 1, 2, 0x41, 4, 83, 55, 70, 51 }.Concat(Id).ToArray(), error.Body);
            Assert.Null(runtime.GetUnformattedProcessProgram("JOB-A"));
        }
        await Download(host, timeout.Token);
        await runtime.DrainProtocolScenariosAsync().WaitAsync(timeout.Token);
        clock.Advance(TimeSpan.FromMinutes(1));
        await ReadBack(host, timeout.Token); // 下一条必须是 S7F6，不能混入旧会话超时。
        Assert.Equal(Recipe, runtime.GetUnformattedProcessProgram("JOB-A"));
    }

    [Fact(Timeout = 30000)]
    public async Task Host_Oversized_Replacement_Is_Refused_And_Old_Recipe_Remains_Readable_Without_Second_Timeout()
    {
        using var host = HsmsHostSimulator.Start();
        var clock = new ManualScenarioTimeProvider();
        using var runtime = RuntimeTestFixture.CreateReal(clock);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await Connect(runtime, host, timeout.Token);
        await Grant(runtime, host, 0x70000001, timeout.Token);
        await Download(host, timeout.Token);
        await Grant(runtime, host, 0x70000004, timeout.Token);
        var oversized = new byte[] { 1, 2 }.Concat(Id).Concat(new byte[] { 0x22, 1, 0 }).Concat(new byte[256]).ToArray();
        host.SendDataMessage(7, 3, true, 0, 0x12345678, oversized, timeout.Token);
        var error = await host.ReadMessageAsync(timeout.Token);
        Assert.Equal((byte)9, error.S);
        Assert.Equal((byte)11, error.F);
        Assert.False(error.WBit);
        Assert.Equal(new byte[] { 0x21, 10, 0, 0, 0x87, 3, 0, 0, 0x12, 0x34, 0x56, 0x78 }, error.Body);
        await runtime.DrainProtocolScenariosAsync().WaitAsync(timeout.Token);
        clock.Advance(TimeSpan.FromMinutes(1));
        await ReadBack(host, timeout.Token);
        Assert.Equal(Recipe, runtime.GetUnformattedProcessProgram("JOB-A"));
    }
}
