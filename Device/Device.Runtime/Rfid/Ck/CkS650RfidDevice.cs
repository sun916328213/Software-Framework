using System.IO.Ports;
using System.Text;
using CommunicationNew.Protocols.Modbus;
using CommunicationNew.Protocols.Modbus.ModbusRtu;
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
    protected override async ValueTask<bool> ConnectCoreAsync(CancellationToken cancellationToken) =>
        await _client.ConnectAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
    protected override async ValueTask DisconnectCoreAsync(CancellationToken cancellationToken) =>
        await _client.DisconnectAsync(cancellationToken).ConfigureAwait(false);

    /// <inheritdoc />
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
    protected override ValueTask DisposeCoreAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    private static ModbusAddressValue CreateAddress(int startPage)
    {
        var zeroBasedPage = checked(startPage - 1);
        var register = checked(StartRegister + RegistersPerPage * zeroBasedPage);
        return new ModbusAddressValue(ModbusRegion.HoldingRegister, checked((ushort)register));
    }

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
