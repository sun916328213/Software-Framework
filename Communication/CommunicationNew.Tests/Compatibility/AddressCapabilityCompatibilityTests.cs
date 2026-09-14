using CommunicationNew.Core;
using CommunicationNew.Protocols.Http;
using CommunicationNew.Protocols.Serial;
using CommunicationNew.Protocols.Socket;
using CommunicationNew.Protocols.WebApi;
using Xunit;

namespace CommunicationNew.Tests.Compatibility;

/// <summary>锁定冻结设备接口在不支持地址语义时的显式失败行为。</summary>
public sealed class AddressCapabilityCompatibilityTests
{
    [Fact]
    public async Task Legacy_address_read_does_not_return_fake_empty_success()
    {
        ICommunicationDevice[] devices =
        [
            new TcpClientDevice(new SocketParam()),
            new TcpServerDevice(new SocketParam()),
            new UdpClientDevice(new SocketParam()),
            new UdpServerDevice(new SocketParam()),
            new SerialPortDevice(new SerialParam()),
            new HttpServerDevice(new HttpParam()),
            new WebApiServerDevice(new WebApiParam()),
        ];

        try
        {
            foreach (var device in devices)
            {
                await Assert.ThrowsAsync<NotSupportedException>(
                    () => device.ReadAsync("unsupported", 1));
            }
        }
        finally
        {
            foreach (var device in devices)
            {
                await device.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task Legacy_http_server_address_write_does_not_return_fake_success()
    {
        ICommunicationDevice[] devices =
        [
            new HttpServerDevice(new HttpParam()),
            new WebApiServerDevice(new WebApiParam()),
        ];

        try
        {
            foreach (var device in devices)
            {
                await Assert.ThrowsAsync<NotSupportedException>(
                    () => device.WriteAsync("unsupported", [0x01]));
            }
        }
        finally
        {
            foreach (var device in devices)
            {
                await device.DisposeAsync();
            }
        }
    }
}
