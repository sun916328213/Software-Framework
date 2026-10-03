// 【中文阅读说明】
// Device 模块：设备契约、驱动适配、工作区及界面；真实连接和释放顺序由设备生命周期约定决定。
// 本文件定义：CkS650RfidDevice。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Device.Runtime.Rfid、System.IO.Ports、System.Text、Communication.Runtime.Protocols.Modbus、Communication.Core.Protocols.Modbus.ModbusRtu、Communication.Runtime.Protocols.Modbus.ModbusRtu。导入不代表所有类型都会在运行时加载。
using Device.Runtime.Rfid;
using System.IO.Ports;
using System.Text;
using Communication.Runtime.Protocols.Modbus;
using Communication.Core.Protocols.Modbus.ModbusRtu;
using Communication.Runtime.Protocols.Modbus.ModbusRtu;
using Device.Core.Rfid;

namespace Device.Runtime.Rfid.Ck;

/// <summary>
/// 晨控 S650 Modbus RTU RFID 适配器。
/// </summary>
internal sealed class CkS650RfidDevice : RfidDeviceBase
{
    private const ushort StartRegister = 5;
    private const int RegistersPerPage = 4;
    private readonly ModbusRtuClientDevice _client;

    /// <summary>使用 S650 配置创建适配器。</summary>
    /// <param name="options">已经验证的 RFID 配置。</param>
    public CkS650RfidDevice(RfidOptions options)
        : base(options)
    {
        _client = new ModbusRtuClientDevice(new ModbusRtuParam
        {
            DeviceName = options.Name,
            PortName = options.PortName.Trim(),
            BaudRate = options.BaudRate,
            DataBits = options.DataBits,
            StopBits = options.StopBits == 2 ? StopBits.Two : StopBits.One,
            Parity = ParseParity(options.Parity),
            SlaveId = options.SlaveId,
            TimeoutMs = options.TimeoutMilliseconds,
        });
    }

    /// <inheritdoc />
    protected override string ErrorCodePrefix => "rfid.ck.s650";

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
    /// 正文中的关键协作调用包括 CreateAddress、ConfigureAwait、ReadAsync、ToString、GetString；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    protected override async ValueTask<string> ReadPagesCoreAsync(
        int startPage,
        int pageCount,
        CancellationToken cancellationToken)
    {
        var address = CreateAddress(startPage);
        var registerCount = checked(RegistersPerPage * pageCount);
        var data = await _client.ReadAsync(address.ToString(), registerCount, cancellationToken).ConfigureAwait(false);
        return Encoding.UTF8.GetString(data);
    }

    /// <inheritdoc />
    /// <remarks>
    /// 执行写入操作；对应入口为 WritePagesCoreAsync。
    /// 正文中的关键协作调用包括 GetBytes、CopyTo、ConfigureAwait、WriteAsync、ToString；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </remarks>
    protected override async ValueTask WritePagesCoreAsync(
        int startPage,
        IReadOnlyList<string> pages,
        CancellationToken cancellationToken)
    {
        var payload = new byte[checked(pages.Count * Options.PageSizeBytes)];
        for (var index = 0; index < pages.Count; index++)
        {
            var pageBytes = Encoding.UTF8.GetBytes(pages[index]);
            pageBytes.CopyTo(payload, index * Options.PageSizeBytes);
        }

        var success = await _client
            .WriteAsync(CreateAddress(startPage).ToString(), payload, cancellationToken)
            .ConfigureAwait(false);
        if (!success)
            throw new IOException("晨控 S650 写入失败。");
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
    /// 创建当前实现提供的对象或结果；对应入口为 CreateAddress。
    /// </summary>
    private static ModbusAddressValue CreateAddress(int startPage)
    {
        var zeroBasedPage = checked(startPage - 1);
        var register = checked(StartRegister + RegistersPerPage * zeroBasedPage);
        return new ModbusAddressValue(ModbusRegion.HoldingRegister, checked((ushort)register));
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
