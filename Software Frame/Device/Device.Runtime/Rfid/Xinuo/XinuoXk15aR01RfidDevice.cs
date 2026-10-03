// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：XinuoXk15aR01RfidDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Runtime.Rfid、System.IO.Ports、System.Text、Communication.Core.Protocols.Serial、Communication.Runtime.Protocols.Serial、Device.Core.Rfid。导入不代表所有类型都会在运行时加载。
using Device.Runtime.Rfid;
using System.IO.Ports;
using System.Text;
using Communication.Core.Protocols.Serial;
using Communication.Runtime.Protocols.Serial;
using Device.Core.Rfid;

namespace Device.Runtime.Rfid.Xinuo;

/// <summary>
/// 椿诺 XK15A-R01 串口文本协议 RFID 适配器。
/// </summary>
internal sealed class XinuoXk15aR01RfidDevice : RfidDeviceBase
{
    private readonly SerialPortDevice _client;

    /// <summary>使用 XK15A-R01 配置创建适配器。</summary>
    /// <param name="options">已经验证的 RFID 配置。</param>
    public XinuoXk15aR01RfidDevice(RfidOptions options)
        : base(options)
    {
        _client = new SerialPortDevice(new SerialParam
        {
            DeviceName = options.Name,
            PortName = options.PortName.Trim(),
            BaudRate = options.BaudRate,
            DataBits = options.DataBits,
            StopBits = options.StopBits == 2 ? StopBits.Two : StopBits.One,
            Parity = ParseParity(options.Parity),
            TimeoutMs = options.TimeoutMilliseconds,
            Delimiter = null,
        });
    }

    /// <inheritdoc />
    protected override string ErrorCodePrefix => "rfid.xinuo.xk15a-r01";

    /// <inheritdoc />
    /// <remarks>
    /// 发起连接操作；对应入口为 ConnectCoreAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、ConnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    protected override async ValueTask<bool> ConnectCoreAsync(CancellationToken cancellationToken) =>
        await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks>
    /// 执行断开连接操作；对应入口为 DisconnectCoreAsync。
    /// 正文中的关键协作调用包括 ConfigureAwait、DisconnectAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    protected override async ValueTask DisconnectCoreAsync(CancellationToken cancellationToken) =>
        await _client.DisconnectAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    /// <remarks>
    /// 读取当前实现提供的数据；对应入口为 ReadPagesCoreAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、GetBytes、ConfigureAwait、SendAndReceiveAsync、Append；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    protected override async ValueTask<string> ReadPagesCoreAsync(
        int startPage,
        int pageCount,
        CancellationToken cancellationToken)
    {
        var result = new StringBuilder(checked(pageCount * Options.PageSizeBytes));
        for (var offset = 0; offset < pageCount; offset++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var command = Encoding.UTF8.GetBytes($"\nRPP{startPage + offset}\r");
            var response = await _client.SendAndReceiveAsync(command, cancellationToken).ConfigureAwait(false);
            result.Append(ParseResponse(response));
        }
        return result.ToString();
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行写入操作；对应入口为 WritePagesCoreAsync。
    /// 正文中的关键协作调用包括 ThrowIfCancellationRequested、GetBytes、CopyTo、ConfigureAwait、SendAndReceiveAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    protected override async ValueTask WritePagesCoreAsync(
        int startPage,
        IReadOnlyList<string> pages,
        CancellationToken cancellationToken)
    {
        for (var index = 0; index < pages.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prefix = Encoding.UTF8.GetBytes($"\nWPP{startPage + index},");
            var pageBytes = Encoding.UTF8.GetBytes(pages[index]);
            var command = new byte[prefix.Length + Options.PageSizeBytes + 1];
            prefix.CopyTo(command, 0);
            pageBytes.CopyTo(command, prefix.Length);
            command[^1] = (byte)'\r';
            var response = await _client.SendAndReceiveAsync(command, cancellationToken).ConfigureAwait(false);
            _ = ParseResponse(response);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行资源释放步骤；对应入口为 DisposeCoreAsync。
    /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
    /// </remarks>
    protected override ValueTask DisposeCoreAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ParseResponse。
    /// 正文中的关键协作调用包括 GetString、Contains、Trim、TrimEnd、ToArray；具体条件和先后次序见方法体。
    /// </summary>
    private static string ParseResponse(byte[] response)
    {
        var text = Encoding.UTF8.GetString(response);
        if (text.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
            throw new IOException($"椿诺 RFID 返回失败：{text.Trim()}。");

        return new string(text.Where(character => !char.IsControl(character)).ToArray()).TrimEnd('\0');
    }

    /// <summary>
    /// 处理当前类型提供的操作；对应入口为 ParseParity。
    /// 正文中的关键协作调用包括 ToLowerInvariant、Trim、nameof；具体条件和先后次序见方法体。
    /// </summary>
    private static Parity ParseParity(string parity) => parity.Trim().ToLowerInvariant() switch
    {
        "none" => Parity.None,
        "odd" => Parity.Odd,
        "even" => Parity.Even,
        "mark" => Parity.Mark,
        "space" => Parity.Space,
        _ => throw new ArgumentOutOfRangeException(nameof(parity), parity, "不支持的串口校验方式。"),
    };
}
