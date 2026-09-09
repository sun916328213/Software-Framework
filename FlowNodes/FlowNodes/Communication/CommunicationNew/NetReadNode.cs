using CommunicationNew.Core;
using FlowEditor.Core;
using FlowEditor.Nodes;
using WPFCustomControls.PropertyGrid.Models;

namespace FlowNodes.Communication.CommunicationNew;

/// <summary>
/// 新通讯读取节点：按地址从设备读取数据。
/// 地址格式因协议而异：Modbus 用 "HR0"/"40001"，MC 用 "D100"/"M10"，
/// OPC UA 用 "ns=2;s=Temperature"，HTTP/WebAPI 用相对路径 "/api/data"。
/// 失败语义：读不到有效数据抛异常走容错路径（可配置 Retry 应对瞬时抖动），
/// 下游不应消费旧值。
/// </summary>
[FlowTool("新通讯地址读取", "Download", ToolGroup = "通讯", ToolSubGroup = "CommunicationNew", SortIndex = 30, Description = "按地址读取 Modbus/MC/OPC/HTTP 数据")]
internal class NetReadNode : NetDeviceNodeBase
{
    public NetReadNode(CommunicationManager manager) : base(manager) { }

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
    [NodeProperty(DisplayName = "文本格式", Group = "1.基础", Order = 3, Description = "读取文本的呈现格式")]
    public ENetDataFormat Format
    {
        get => _format;
        set => SetProperty(ref _format, value);
    }

    private string _readText = "";
    /// <summary>读取到的数据文本（按文本格式呈现）。</summary>
    [NodeProperty(DisplayName = "读取文本", Group = "2.结果", Order = 10, ReadOnly = 1)]
    public string ReadText
    {
        get => _readText;
        set => SetProperty(ref _readText, value);
    }

    private int _readCount;
    /// <summary>读取到的字节数。</summary>
    [NodeProperty(DisplayName = "字节数", Group = "2.结果", Order = 11, ReadOnly = 1)]
    public int ReadCount
    {
        get => _readCount;
        set => SetProperty(ref _readCount, value);
    }

    private byte[] _readBytes = [];
    /// <summary>读取到的原始字节（可绑定到输出端口；面板不显示）。</summary>
    [NodeProperty(DisplayName = "原始字节", Group = "2.结果", Order = 12, IsDisplay = false)]
    public byte[] ReadBytes
    {
        get => _readBytes;
        set => SetProperty(ref _readBytes, value);
    }

    public override async Task ExecuteAsync(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        ReadBytes = [];
        ReadCount = 0;
        ReadText = string.Empty;
        if (string.IsNullOrWhiteSpace(Address))
            throw await CreateFailureAsync("读取", null, detail: "地址不能为空");
        if (Length <= 0)
            throw await CreateFailureAsync("读取", Address, detail: "读取长度必须大于 0");

        var device = GetDevice();
        if (!NetDeviceHelper.SupportsAddressAccess(device))
        {
            throw await CreateFailureAsync(
                "读取",
                Address,
                detail: $"{device.ProtocolType} 不支持按地址读取，请使用「新通讯接收」节点");
        }

        var data = await RunDeviceOperationAsync(
            "读取",
            Address,
            ct,
            currentDevice => currentDevice.ReadAsync(Address.Trim(), Length, ct),
            device: device);

        if (data == null || data.Length == 0)
        {
            throw await CreateFailureAsync("读取", Address, detail: "未返回有效数据");
        }

        ReadBytes = data;
        ReadCount = data.Length;
        ReadText = NetDeviceHelper.FormatData(data, Format);
        await NodeLog.InfoAsync($"读取 [{DeviceName}] {Address}：{ReadCount} 字节，{NetDeviceHelper.Preview(ReadText)}");
    }
}
