// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：AddressCapabilityCompatibilityTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Runtime.Core、Communication.Core.Protocols.Http、Communication.Runtime.Protocols.Http、Communication.Core.Protocols.Serial、Communication.Runtime.Protocols.Serial。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Runtime.Core;
using Communication.Core.Protocols.Http;
using Communication.Runtime.Protocols.Http;
using Communication.Core.Protocols.Serial;
using Communication.Runtime.Protocols.Serial;
using Communication.Core.Protocols.Socket;
using Communication.Runtime.Protocols.Socket;
using Communication.Core.Protocols.WebApi;
using Communication.Runtime.Protocols.WebApi;
using Xunit;

namespace Communication.Tests.Compatibility;

/// <summary>锁定冻结设备接口在不支持地址语义时的显式失败行为。</summary>
public sealed class AddressCapabilityCompatibilityTests
{
    /// <summary>
    /// 测试相关入口 Legacy_address_read_does_not_return_fake_empty_success；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ThrowsAsync、ReadAsync、DisposeAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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

    /// <summary>
    /// 测试相关入口 Legacy_http_server_address_write_does_not_return_fake_success；准备条件、协作调用与结果检查以方法正文为准。
    /// 正文中的关键协作调用包括 ThrowsAsync、WriteAsync、DisposeAsync；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
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
