using CommunicationNew.Core;
using FlowEditor.Core;
using FlowEditor.Nodes;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>
/// 新通讯触发节点：轮询读取设备数据，仅当读取值与期望数据按字节相等时才放行继续执行。
/// 比对与「新通讯读取」同一份编解码：UTF8 按文本转字节，HEX 容忍空格/横杠/0x 前缀差异。
/// 等待语义（与旧 MC触发 一致）：读抖动按「读取重试次数」容忍，超过才判死；
/// 超时（等待超时 / 引擎执行超时）与外部取消（停止按钮）都会结束等待——取消直接透出，不包装。
/// </summary>
[FlowTool("新通讯地址触发", "TimerSand", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 35, Description = "地址值等于期望数据时才继续执行")]
internal class NetWaitNode : NetDeviceNodeBase
{
    public NetWaitNode(CommunicationManager manager) : base(manager) { }

    private string _address = "D100";
    [NodeProperty(DisplayName = "地址", Group = "1.基础", Order = 1, Description = "Modbus:HR0/40001；MC:D100/M10；OPC:ns=2;s=Tag；HTTP:/api/data")]
    public string Address
    {
        get => _address;
        set => SetProperty(ref _address, value);
    }

    private int _length = 1;
    [NodeProperty(DisplayName = "读取长度", Group = "1.基础", Order = 2, Min = 1, Description = "寄存器数或字节数（按协议语义）")]
    public int Length
    {
        get => _length;
        set => SetProperty(ref _length, value);
    }

    private ENetDataFormat _format = ENetDataFormat.UTF8;
    [NodeProperty(DisplayName = "内容格式", Group = "1.基础", Order = 3, Description = "期望数据与实际数据的编解码格式")]
    public ENetDataFormat Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }

    private string _expectedText = "";
    [NodeProperty(DisplayName = "期望数据", Group = "1.基础", Order = 4, Description = "可连线输入；读取值与此按字节相等即放行")]
    public string ExpectedText
    {
        get => _expectedText;
        set => SetProperty(ref _expectedText, value);
    }

    private int _pollIntervalMs = 200;
    [NodeProperty(DisplayName = "轮询间隔(ms)", Group = "1.基础", Order = 5, Min = 1)]
    public int PollIntervalMs
    {
        get => _pollIntervalMs;
        set => SetProperty(ref _pollIntervalMs, Math.Clamp(value, 1, 60_000));
    }

    private int _waitTimeoutMs;
    [NodeProperty(DisplayName = "等待超时(ms)", Group = "1.基础", Order = 6, Min = 0, Description = "0=无限等待（可用停止按钮或节点执行超时中断）；超限抛异常")]
    public int WaitTimeoutMs
    {
        get => _waitTimeoutMs;
        set => SetProperty(ref _waitTimeoutMs, Math.Clamp(value, 0, 3_600_000));
    }

    private int _readRetryCount = 3;
    [NodeProperty(DisplayName = "读取重试次数", Group = "1.基础", Order = 7, Description = "连续读取失败的容忍上限（0=首次失败即判死），超过后抛异常")]
    public int ReadRetryCount
    {
        get => _readRetryCount;
        set => SetProperty(ref _readRetryCount, Math.Clamp(value, 0, 10));
    }

    private string _actualText = "";
    /// <summary>最后一次读到的实际数据文本。</summary>
    [NodeProperty(DisplayName = "实际数据", Group = "2.结果", Order = 10, ReadOnly = 1)]
    public string ActualText
    {
        get => _actualText;
        set => SetProperty(ref _actualText, value);
    }

    private bool _triggered;
    /// <summary>是否因值相等而放行（超时/取消时为 false）。</summary>
    [NodeProperty(DisplayName = "已触发", Group = "2.结果", Order = 11, ReadOnly = 1)]
    public bool Triggered
    {
        get => _triggered;
        set => SetProperty(ref _triggered, value);
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Triggered = false;
        ActualText = string.Empty;
        if (string.IsNullOrWhiteSpace(Address))
            throw await CreateFailureAsync("触发", null, detail: "地址不能为空");
        if (Length <= 0)
            throw await CreateFailureAsync("触发", Address, detail: "读取长度必须大于 0");

        var device = GetDevice();
        if (!NetDeviceHelper.SupportsAddressAccess(device))
        {
            throw await CreateFailureAsync(
                "触发",
                Address,
                detail: $"{device.ProtocolType} 不支持按地址轮询，请使用「新通讯接收」节点");
        }

        byte[] expected;
        try
        {
            expected = NetDeviceHelper.ParseData(ExpectedText, Format);
        }
        catch (Exception ex)
        {
            throw await CreateFailureAsync("触发", Address, ex, $"期望数据解析失败：{ex.Message}");
        }

        if (expected.Length == 0)
            throw await CreateFailureAsync("触发", Address, detail: "期望数据为空，无法比对");

        var start = Environment.TickCount64;
        var readFailStreak = 0;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            byte[]? actual = null;
            Exception? readError = null;
            try
            {
                actual = await device.ReadAsync(Address, Length, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // 停止按钮 / 节点执行超时：直接透出，交由引擎按取消/超时处理，不包装。
                // 注意必须带 when 守卫：库内部读超时同样是 TaskCanceledException，
                // 此时并未取消，应落到下方的抖动容忍，而不是整个等待被判死。
                throw;
            }
            catch (Exception ex)
            {
                readError = ex;
            }

            if (readError == null && actual is { Length: > 0 })
            {
                readFailStreak = 0;
                ActualText = NetDeviceHelper.FormatData(actual, Format);
                if (actual.SequenceEqual(expected))
                {
                    Triggered = true;
                    await NodeLog.InfoAsync($"触发 [{DeviceName}] {Address}：读取值与期望一致（{NetDeviceHelper.Preview(ActualText)}），继续执行");
                    return;
                }
            }
            else
            {
                readFailStreak++;
                if (readFailStreak > Math.Max(0, ReadRetryCount))
                {
                    var detail = readError is OperationCanceledException
                        ? "读取超时"
                        : readError?.Message ?? "未返回有效数据";
                    throw await CreateFailureAsync(
                        "触发",
                        Address,
                        readError,
                        $"连续 {readFailStreak} 次读取无效：{detail}");
                }
            }

            if (WaitTimeoutMs > 0 && Environment.TickCount64 - start >= WaitTimeoutMs)
            {
                throw await CreateFailureAsync(
                    "触发",
                    Address,
                    detail: $"{WaitTimeoutMs}ms 内未读到期望值（期望：{NetDeviceHelper.Preview(ExpectedText)}，最后一次：{NetDeviceHelper.Preview(ActualText)}）");
            }

            await Task.Delay(Math.Max(1, PollIntervalMs), ct);
        }
    }
}
