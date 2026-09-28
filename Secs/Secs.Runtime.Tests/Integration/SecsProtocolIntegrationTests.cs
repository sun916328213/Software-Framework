using System;
using System.Threading;
using System.Threading.Tasks;
using DataManager.Log;
using Secs.Manager;
using Secs.Runtime.Tests.Infrastructure;
using Secs.Variable;
using Secs.Variable.Data;
using SemiTool.Tool;
using Xunit;

namespace Secs.Runtime.Tests.Integration
{
    /// <summary>
    /// 协议级集成测试：
    /// 真实 TCP 回环 + 最小 HSMS Host 模拟器，验证 设备建链→指令应答→远程命令回调 全链路。
    /// </summary>
    public class SecsProtocolIntegrationTests
    {
        private const ushort DeviceId = 0;

        /// <summary>
        /// 组装一个完整可用的 SecsControl（迷你容器注册全部依赖）
        /// </summary>
        private static SecsControl CreateControl()
        {
            var container = new TestPrismContainer();

            container.RegisterSingleton(typeof(LogManager), typeof(LogManager));

            // Variable 数据类
            container.RegisterSingleton(typeof(ReportID), typeof(ReportID));
            container.RegisterSingleton(typeof(CEID), typeof(CEID));
            container.RegisterSingleton(typeof(DVID), typeof(DVID));
            container.RegisterSingleton(typeof(SVID), typeof(SVID));
            container.RegisterSingleton(typeof(ECID), typeof(ECID));
            container.RegisterSingleton(typeof(Alarm), typeof(Alarm));
            container.RegisterSingleton(typeof(ActiveReceive), typeof(ActiveReceive));

            // Manager 与门面
            container.RegisterSingleton(typeof(VIDManager), typeof(VIDManager));
            container.RegisterSingleton(typeof(DeviceManager), typeof(DeviceManager));
            container.RegisterSingleton(typeof(StatusManager), typeof(StatusManager));
            container.RegisterSingleton(typeof(ConnectManager), typeof(ConnectManager));
            container.RegisterSingleton(typeof(TraceManager), typeof(TraceManager));
            container.RegisterSingleton(typeof(FactoryCollection), typeof(FactoryCollection));
            container.RegisterSingleton(typeof(SecsControl), typeof(SecsControl));

            return (SecsControl)container.Resolve(typeof(SecsControl));
        }

        private sealed class TestEvents : ISecsControlEvents
        {
            public RemoteCommandArgs LastArgs;
            public int CallCount;

            public Task OnRemoteCommandAsync(RemoteCommandArgs e)
            {
                LastArgs = e;
                CallCount++;
                return Task.CompletedTask;
            }
        }

        private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout, string description)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition()) return;
                await Task.Delay(20).ConfigureAwait(false);
            }
            throw new TimeoutException($"等待超时: {description}");
        }

        [Fact(Timeout = 30000)]
        public async Task Connect_Then_S1F13_Should_Get_S1F14_Reply()
        {
            using var host = HsmsHostSimulator.Start();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            using var control = CreateControl();

            await control.ConnectAsync(true, "127.0.0.1", host.Port, DeviceId).ConfigureAwait(false);
            await host.WaitForDeviceAsync(cts.Token).ConfigureAwait(false);

            // 完成 HSMS Select 握手
            await host.CompleteSelectAsync(cts.Token).ConfigureAwait(false);

            // 设备侧应进入 Selected 状态
            await WaitUntilAsync(() => control.IsConnected, TimeSpan.FromSeconds(5), "设备进入 Selected").ConfigureAwait(false);

            // 发送 S1F13 建立通信请求，body = 空列表
            host.SendDataMessage(1, 13, w: true, DeviceId, systemBytes: 0x0000_0101,
                new byte[] { 0x01, 0x00 }, cts.Token);

            var reply = await host.ReadMessageAsync(cts.Token).ConfigureAwait(false);

            Assert.Equal(HsmsHostSimulator.StypeData, reply.SType);
            Assert.Equal(1, reply.S);
            Assert.Equal(14, reply.F);              // 应答为 S1F14
            Assert.False(reply.WBit);
            Assert.Equal(0x0000_0101u, reply.SystemBytes); // 系统字节与请求一致

            // 回包体结构: L(B(0), L(A,A)) => 01 02 | 21 01 00 ...
            Assert.Equal(0x01, reply.Body[0]);
            Assert.Equal(0x02, reply.Body[1]);
            Assert.Equal(0x21, reply.Body[2]);
            Assert.Equal(0x01, reply.Body[3]);
            Assert.Equal(0x00, reply.Body[4]);      // HCACK/B(0)=0 接受
        }

        [Fact(Timeout = 30000)]
        public async Task S2F41_RemoteCommand_Should_Invoke_Handler_And_Reply_S2F42_HCACK()
        {
            using var host = HsmsHostSimulator.Start();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            using var control = CreateControl();

            var events = new TestEvents();
            control.SetEvents(events);

            await control.ConnectAsync(true, "127.0.0.1", host.Port, DeviceId).ConfigureAwait(false);
            await host.WaitForDeviceAsync(cts.Token).ConfigureAwait(false);
            await host.CompleteSelectAsync(cts.Token).ConfigureAwait(false);
            await WaitUntilAsync(() => control.IsConnected, TimeSpan.FromSeconds(5), "设备进入 Selected").ConfigureAwait(false);

            /*
             * S2F41 body:
             * L,2
             *   A"START"
             *   L,1
             *     L,1
             *       L,2
             *         A"P1"
             *         A"V1"
             */
            byte[] body =
            {
                0x01, 0x02,
                0x41, 0x05, (byte)'S', (byte)'T', (byte)'A', (byte)'R', (byte)'T',
                0x01, 0x01,
                0x01, 0x01,
                0x01, 0x02,
                0x41, 0x02, (byte)'P', (byte)'1',
                0x41, 0x02, (byte)'V', (byte)'1',
            };

            host.SendDataMessage(2, 41, w: true, DeviceId, systemBytes: 0x0000_0202, body, cts.Token);

            // 业务回调应被触发并携带解析结果（替代旧的 ActiveReceive 轮询）
            await WaitUntilAsync(() => events.CallCount > 0, TimeSpan.FromSeconds(5), "远程命令回调被调用").ConfigureAwait(false);
            Assert.Equal("START", events.LastArgs.RCMD);
            Assert.Equal("V1", events.LastArgs.Params["P1"]);

            var reply = await host.ReadMessageAsync(cts.Token).ConfigureAwait(false);
            Assert.Equal(HsmsHostSimulator.StypeData, reply.SType);
            Assert.Equal(2, reply.S);
            Assert.Equal(42, reply.F);              // 应答为 S2F42

            // 回包体结构: L(B(HCACK), L(...)) 且 HCACK=业务设置的 Return
            Assert.Equal(0x01, reply.Body[0]);
            Assert.Equal(0x02, reply.Body[1]);
            Assert.Equal(0x21, reply.Body[2]);
            Assert.Equal(0x01, reply.Body[3]);
            Assert.Equal(events.LastArgs.Return, reply.Body[4]);
        }

        [Fact(Timeout = 30000)]
        public async Task Unknown_Order_Should_Get_Auto_S9F7_Fallback_Instead_Of_T3_Timeout()
        {
            using var host = HsmsHostSimulator.Start();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            using var control = CreateControl();

            await control.ConnectAsync(true, "127.0.0.1", host.Port, DeviceId).ConfigureAwait(false);
            await host.WaitForDeviceAsync(cts.Token).ConfigureAwait(false);
            await host.CompleteSelectAsync(cts.Token).ConfigureAwait(false);
            await WaitUntilAsync(() => control.IsConnected, TimeSpan.FromSeconds(5), "设备进入 Selected").ConfigureAwait(false);

            // 发送未实现的 S12F1（body=空列表）
            host.SendDataMessage(12, 1, w: true, DeviceId, systemBytes: 0x0000_0303,
                new byte[] { 0x01, 0x00 }, cts.Token);

            var reply = await host.ReadMessageAsync(cts.Token).ConfigureAwait(false);

            // 底层自动兜底 S9F7（Illegal Data），Host 无需等待 T3 超时
            Assert.Equal(HsmsHostSimulator.StypeData, reply.SType);
            Assert.Equal(9, reply.S);
            Assert.Equal(7, reply.F);
        }
    }
}
