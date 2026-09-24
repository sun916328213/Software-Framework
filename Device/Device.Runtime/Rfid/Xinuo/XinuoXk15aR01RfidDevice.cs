using System.IO.Ports;
using System.Text;
using CommunicationNew.Protocols.Serial;
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
    protected override ValueTask DisposeCoreAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string ParseResponse(byte[] response)
    {
        var text = Encoding.UTF8.GetString(response);
        if (text.Contains("FAIL", StringComparison.OrdinalIgnoreCase))
            throw new IOException($"椿诺 RFID 返回失败：{text.Trim()}。");

        return new string(text.Where(character => !char.IsControl(character)).ToArray()).TrimEnd('\0');
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
