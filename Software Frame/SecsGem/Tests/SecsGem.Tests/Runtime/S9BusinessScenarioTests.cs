using System.Buffers;
using System.Text;
using Secs4Net;
using SecsGem.Runtime;
using SecsGem.Runtime.Protocol;
using SecsGem.Tests.Fakes;
using SecsGem.Tests.Infrastructure;
using Xunit;

namespace SecsGem.Tests.Runtime;

public sealed class S9BusinessScenarioTests
{
    private static readonly byte[] Recipe = Encoding.UTF8.GetBytes("{\"version\":1,\"exposureUs\":5000,\"gain\":1.5,\"speedMmPerSec\":100}");
    private static async Task Receive(InMemorySecsTransport transport, byte function, Item body)
    {
        using var message = new SecsMessage(7, function, true) { SecsItem = body };
        await transport.EmitReceivedAsync(message);
    }
    private static Task Grant(InMemorySecsTransport transport, string id) => Receive(transport, 1, Item.L(Item.A(id), Item.U4((uint)Recipe.Length)));
    private static Task Download(InMemorySecsTransport transport, string id) => Receive(transport, 3, Item.L(Item.A(id), Item.B(Recipe.ToArray())));
    private static SecsGemRuntime Create(InMemorySecsTransport transport, ManualScenarioTimeProvider clock, int max = 1024)
    {
        var runtime = RuntimeTestFixture.Create(transport, clock);
        runtime.ConfigureProtocolScenarios(new SecsProtocolScenarioOptions
        { MaxMessageBodyBytes = max, ProcessProgramTransferTimeout = TimeSpan.FromSeconds(30) });
        return runtime;
    }
    private static Task Connect(InMemorySecsTransport transport) => transport.ConnectAsync(true, "127.0.0.1", 1, 0);

    [Fact]
    public async Task Granted_Recipe_Download_Completes_Conversation_Stores_Recipe_And_Remains_Readable()
    {
        var transport = new InMemorySecsTransport();
        var clock = new ManualScenarioTimeProvider();
        using var runtime = Create(transport, clock);
        await Connect(transport);
        await Grant(transport, "JOB-A");
        Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
        Assert.Null(runtime.GetUnformattedProcessProgram("JOB-A")); // 获准不创建空配方。
        clock.Advance(TimeSpan.FromSeconds(29));
        await Download(transport, "JOB-A");
        await runtime.DrainProtocolScenariosAsync().WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Empty(transport.Sent);
        Assert.Equal(Recipe, runtime.GetUnformattedProcessProgram("JOB-A"));
        await Receive(transport, 5, Item.A("JOB-A"));
        Assert.Equal(Recipe, transport.LastReplyItem!.Items[1].GetMemory<byte>().ToArray());
    }

    [Fact]
    public async Task Missing_Expected_Recipe_Reports_One_Conversation_Timeout_Late_Download_Is_Independent()
    {
        var transport = new InMemorySecsTransport();
        var clock = new ManualScenarioTimeProvider();
        using var runtime = Create(transport, clock);
        await Connect(transport);
        await Grant(transport, "JOB-A");
        await Download(transport, "JOB-B"); // 其他配方不能完成 JOB-A 的会话。
        clock.Advance(TimeSpan.FromSeconds(30));
        await runtime.DrainProtocolScenariosAsync().WaitAsync(TimeSpan.FromSeconds(5));
        var notice = Assert.Single(transport.Sent);
        Assert.Equal("S9F13", notice.Order);
        Assert.Equal("S7F3", notice.Item.Items[0].GetString());
        Assert.Equal("JOB-A", notice.Item.Items[1].GetString());
        Assert.Null(runtime.GetUnformattedProcessProgram("JOB-A"));
        Assert.Equal(Recipe, runtime.GetUnformattedProcessProgram("JOB-B"));
        await Download(transport, "JOB-A"); // 兼容直接/迟到下载；不恢复旧计时器。
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Single(transport.Sent);
        Assert.Equal(Recipe, runtime.GetUnformattedProcessProgram("JOB-A"));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Disconnect_Or_Dispose_Cancels_Old_Session_And_Reconnect_Does_Not_Replay_Timeout(bool dispose)
    {
        var transport = new InMemorySecsTransport();
        var clock = new ManualScenarioTimeProvider();
        using var runtime = Create(transport, clock);
        await Connect(transport);
        await Grant(transport, "JOB-A");
        if (dispose) runtime.Dispose(); else transport.Close();
        await runtime.DrainProtocolScenariosAsync().WaitAsync(TimeSpan.FromSeconds(5));
        if (!dispose) await Connect(transport);
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task Repeated_Grant_Replaces_Only_That_Ppid_Deadline()
    {
        var transport = new InMemorySecsTransport();
        var clock = new ManualScenarioTimeProvider();
        using var runtime = Create(transport, clock);
        await Connect(transport);
        await Grant(transport, "JOB-A");
        clock.Advance(TimeSpan.FromSeconds(15));
        await Grant(transport, "JOB-A");
        clock.Advance(TimeSpan.FromSeconds(15));
        Assert.Empty(transport.Sent);
        clock.Advance(TimeSpan.FromSeconds(15));
        await runtime.DrainProtocolScenariosAsync().WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("S9F13", Assert.Single(transport.Sent).Order);
    }

    [Fact]
    public async Task Size_Boundary_Allows_Exact_Message_Rejects_One_Byte_Over_And_Preserves_Previous_Recipe()
    {
        var transport = new InMemorySecsTransport { ID = 0x12345678 };
        var clock = new ManualScenarioTimeProvider();
        using var runtime = Create(transport, clock, 9); // L[2] A[1] B[2] 正好 9 编码字节。
        await Connect(transport);
        await Receive(transport, 3, Item.L(Item.A("X"), Item.B(3, 4)));
        Assert.Equal(new byte[] { 3, 4 }, runtime.GetUnformattedProcessProgram("X"));
        var replies = transport.ReplyAttempts;
        await Receive(transport, 3, Item.L(Item.A("X"), Item.B(3, 4, 5)));
        Assert.Equal(replies, transport.ReplyAttempts); // 不再执行处理器，不回 ACKC7=0。
        var notice = Assert.Single(transport.Sent);
        Assert.Equal("S9F11", notice.Order);
        Assert.Equal(new byte[] { 0, 0, 0x87, 3, 0, 0, 0x12, 0x34, 0x56, 0x78 }, notice.Item.GetMemory<byte>().ToArray());
        Assert.Equal(new byte[] { 3, 4 }, runtime.GetUnformattedProcessProgram("X"));
    }

    [Fact]
    public async Task Received_But_Rejected_Download_Does_Not_Also_Claim_It_Was_Missing()
    {
        var transport = new InMemorySecsTransport();
        var clock = new ManualScenarioTimeProvider();
        using var runtime = Create(transport, clock, 32);
        await Connect(transport);
        await Receive(transport, 1, Item.L(Item.A("JOB-A"), Item.U4(1))); // 声明可容纳的长度，实际下载却超限。
        Assert.Equal((byte)0, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
        await Download(transport, "JOB-A"); // 超限：S9F11，取消对应等待，不追加 S9F13。
        await runtime.DrainProtocolScenariosAsync().WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal("S9F11", Assert.Single(transport.Sent).Order);
        Assert.Null(runtime.GetUnformattedProcessProgram("JOB-A"));
    }

    [Fact]
    public async Task Declared_Oversized_Recipe_Is_Denied_At_Grant_Instead_Of_Opening_An_Impossible_Session()
    {
        var transport = new InMemorySecsTransport();
        var clock = new ManualScenarioTimeProvider();
        using var runtime = Create(transport, clock, 32);
        await Connect(transport);
        await Grant(transport, "JOB-A");
        Assert.Equal((byte)2, transport.LastReplyItem!.GetMemory<byte>().Span[0]);
        clock.Advance(TimeSpan.FromHours(1));
        await runtime.DrainProtocolScenariosAsync();
        Assert.Empty(transport.Sent);
        Assert.Null(runtime.GetUnformattedProcessProgram("JOB-A"));
    }

    [Fact]
    public async Task Invalid_Expected_Download_Ends_Arrival_Wait_And_S9_Itself_Bypasses_Size_Policy()
    {
        var transport = new InMemorySecsTransport();
        var clock = new ManualScenarioTimeProvider();
        using var runtime = Create(transport, clock);
        await Connect(transport);
        await Grant(transport, "JOB-A");
        await Receive(transport, 3, Item.L(Item.A("JOB-A"), Item.A("not binary")));
        await runtime.DrainProtocolScenariosAsync().WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromHours(1));
        Assert.Empty(transport.Sent); // Fake 不模拟底层 W=1 的自动 S9F7；此处只验证不追加 S9F13。
        Assert.Null(runtime.GetUnformattedProcessProgram("JOB-A"));
        transport.Close();
        runtime.ConfigureProtocolScenarios(new() { MaxMessageBodyBytes = 1 });
        await Connect(transport);
        using var notice = new SecsMessage(9, 7, true) { SecsItem = Item.B(new byte[10]) };
        var replies = transport.ReplyAttempts;
        await transport.EmitReceivedAsync(notice);
        Assert.Equal(replies, transport.ReplyAttempts);
        Assert.Empty(transport.Sent);
    }

    [Fact]
    public async Task Disabled_Policy_Does_Not_Create_Sessions_And_Connected_Configuration_Is_Rejected()
    {
        var transport = new InMemorySecsTransport();
        var clock = new ManualScenarioTimeProvider();
        using var runtime = RuntimeTestFixture.Create(transport, clock);
        await Connect(transport);
        Assert.Throws<InvalidOperationException>(() => runtime.ConfigureProtocolScenarios(new() { MaxMessageBodyBytes = 8 }));
        await Grant(transport, "JOB-A");
        clock.Advance(TimeSpan.FromDays(1));
        await runtime.DrainProtocolScenariosAsync();
        Assert.Empty(transport.Sent);
    }

    [Theory]
    [InlineData(0)] [InlineData(255)] [InlineData(256)] [InlineData(65535)] [InlineData(65536)]
    public void Encoded_Size_Matches_Independent_Encoding_For_Nested_And_Variable_Length_Data(int count)
    {
        using var item = Item.L(Item.A("批次"), Item.J("工艺"), Item.B(new byte[count]), Item.L(Item.F8(1.5), Item.I4(-3, 7)));
        var writer = new ArrayBufferWriter<byte>();
        item.EncodeTo(writer);
        Assert.Equal((long)writer.WrittenCount, SecsProtocolScenarios.EncodedSize(item, long.MaxValue));
    }
}
