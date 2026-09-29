using System;
using System.Threading;
using System.Threading.Tasks;
using DataManager.Log;
using Extension.Automation.Secs;
using System.Text;
using Secs;
using Secs.Manager;
using SecsGem.Runtime.Tests.Infrastructure;
using Secs.Variable;
using Secs.Variable.Data;
using SecsGem.Core;
using SecsGem.Runtime;
using SecsGem.Runtime.Transport;
using ProtocolRegistry = SecsGem.Runtime.Protocol.Registry.SecsMessageRegistry;
using Secs4Net;
using SemiTool.Tool;
using Xunit;

namespace SecsGem.Runtime.Tests.Integration
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
        private static SecsControl CreateControl() => CreateControl(out _);

        private static SecsControl CreateControl(out TestPrismContainer container)
        {
            container = new TestPrismContainer();

            container.RegisterSingleton(typeof(LogManager), () => new LogManager());

            // Variable 数据类
            container.RegisterSingleton(typeof(ReportID), typeof(ReportID));
            container.RegisterSingleton(typeof(CEID), typeof(CEID));
            container.RegisterSingleton(typeof(DVID), typeof(DVID));
            container.RegisterSingleton(typeof(SVID), typeof(SVID));
            container.RegisterSingleton(typeof(ECID), typeof(ECID));
            container.RegisterSingleton(typeof(Alarm), typeof(Alarm));
            container.RegisterSingleton(typeof(ActiveReceive), typeof(ActiveReceive));

            // 使用正式模块注册，保证 Core 接口与旧门面指向同一个运行时。
            new SecsGemModule().RegisterTypes(container);
            var control = (SecsControl)container.Resolve(typeof(SecsControl));
            Assert.Same(control, container.Resolve(typeof(ISecsGemRuntime)));
            return control;
        }

        private sealed class TestEvents : ISecsGemEventHandler
        {
            public SecsGemRemoteCommand LastCommand;
            public int CallCount;

            public Task<byte> OnRemoteCommandAsync(SecsGemRemoteCommand command)
            {
                LastCommand = command;
                CallCount++;
                return Task.FromResult((byte)0);
            }
        }

        private sealed class TestEquipmentDataProvider : IEquipmentDataProvider
        {
            public IReadOnlyDictionary<string, object?> GetVariableValues() =>
                new Dictionary<string, object?> { ["LotId"] = "LOT-42" };
        }

        private sealed class TestTerminalDisplay : ITerminalDisplay
        {
            public string Title { get; private set; }
            public string Message { get; private set; }

            public void Show(string title, string message)
            {
                Title = title;
                Message = message;
            }
        }

        [Theory]
        [InlineData("S10F3", 3)]
        [InlineData("S10F5", 5)]
        public void Terminal_Message_Uses_Protocol_Parser_And_Display(string order, byte function)
        {
            using var control = CreateControl(out var container);
            var display = new TestTerminalDisplay();
            control.TerminalDisplay = display;
            var registry = (ProtocolRegistry)container.Resolve(typeof(ProtocolRegistry));
            var message = new SecsMessage(10, function, true)
            {
                SecsItem = Item.L(Item.B(1), Item.A("Host message"))
            };

            var reply = registry.GetHandler(order).GetReplyItem(message);

            Assert.Equal(SecsFormat.Binary, reply.Format);
            Assert.Equal(order, display.Title);
            Assert.Equal("1/r/nHost message", display.Message);
        }

        [Fact]
        public void Terminal_Message_Is_Published_Without_A_Wpf_Display()
        {
            using var control = CreateControl(out var container);
            string? title = null;
            string? text = null;
            control.TerminalMessageReceived += (receivedTitle, receivedText) =>
            {
                title = receivedTitle;
                text = receivedText;
            };
            var registry = (ProtocolRegistry)container.Resolve(typeof(ProtocolRegistry));
            var message = new SecsMessage(10, 3, true)
            {
                SecsItem = Item.L(Item.B(1), Item.A("Headless host"))
            };

            var reply = registry.GetHandler("S10F3").GetReplyItem(message);

            Assert.Equal(SecsFormat.Binary, reply.Format);
            Assert.Equal("S10F3", title);
            Assert.Equal("1/r/nHeadless host", text);
        }

        [Fact]
        public void CoreContract_Refreshes_Configured_Variable_From_Equipment()
        {
            using var control = CreateControl();
            control._VIDManager.VIDDic_Name["LotId"] = new VIDInfo
            {
                ID = "1",
                Name = "LotId",
                Value = "old",
                DataTypeName = "A"
            };

            ISecsGemRuntime runtime = control;
            runtime.SetEquipmentDataProvider(new TestEquipmentDataProvider());

            Assert.Equal(SecsGemMode.Offline, runtime.GetState().Mode);
            Assert.False(runtime.GetState().IsConnected);
            Assert.Equal("LOT-42", runtime.GetVariableValue("LotId"));
            Assert.Equal("LOT-42", control.GetVidValue("LotId"));
        }

        [Fact(Timeout = 30000)]
        public async Task Disconnect_Records_Offline_Mode_Without_A_Host()
        {
            using var host = HsmsHostSimulator.Start();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            using var control = CreateControl();

            // State changes made while disconnected are retained locally.
            control._StatusManager.UpdateSEMIMode("Mode", ESecsMode.Online_Remote);
            Assert.Equal(ESecsMode.Online_Remote, control.GetDeviceMode());

            await control.ConnectAsync(true, "127.0.0.1", host.Port, DeviceId).ConfigureAwait(false);
            await host.WaitForDeviceAsync(cts.Token).ConfigureAwait(false);
            await host.CompleteSelectAsync(cts.Token).ConfigureAwait(false);
            await WaitUntilAsync(() => control.IsConnected, TimeSpan.FromSeconds(5), "设备进入 Selected").ConfigureAwait(false);

            control.Close();
            await WaitUntilAsync(() => !control.IsConnected, TimeSpan.FromSeconds(5), "设备断开").ConfigureAwait(false);
            Assert.Equal(ESecsMode.Offline, control.GetDeviceMode());
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
            ((ISecsGemRuntime)control).SetEventHandler(events);

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
            Assert.Equal("START", events.LastCommand.Name);
            Assert.Equal("V1", events.LastCommand.Parameters["P1"]);

            var reply = await host.ReadMessageAsync(cts.Token).ConfigureAwait(false);
            Assert.Equal(HsmsHostSimulator.StypeData, reply.SType);
            Assert.Equal(2, reply.S);
            Assert.Equal(42, reply.F);              // 应答为 S2F42

            // 回包体结构: L(B(HCACK), L(...)) 且 HCACK=业务设置的 Return
            Assert.Equal(0x01, reply.Body[0]);
            Assert.Equal(0x02, reply.Body[1]);
            Assert.Equal(0x21, reply.Body[2]);
            Assert.Equal(0x01, reply.Body[3]);
            Assert.Equal(0, reply.Body[4]);
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

        [Fact(Timeout = 30000)]
        public async Task Multiple_Receive_Subscribers_Are_Awaited_Before_Reply_Context_Closes()
        {
            using var host = HsmsHostSimulator.Start();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            var container = new TestPrismContainer();
            container.RegisterSingleton(typeof(LogManager), () => new LogManager());
            using var transport = new Secs4NetTransport(container);
            var releaseHandler = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var monitorSeen = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var handlerResult = new TaskCompletionSource<Exception?>(TaskCreationOptions.RunContinuationsAsynchronously);

            transport.OnReceive += async (message, _) =>
            {
                try
                {
                    await releaseHandler.Task.ConfigureAwait(false);
                    await transport.ReplyAsync(message, Item.L()).ConfigureAwait(false);
                    handlerResult.TrySetResult(null);
                }
                catch (Exception ex)
                {
                    handlerResult.TrySetResult(ex);
                }
            };
            transport.OnReceive += (_, _) =>
            {
                monitorSeen.TrySetResult();
                return Task.CompletedTask;
            };

            await transport.ConnectAsync(true, "127.0.0.1", host.Port, DeviceId).ConfigureAwait(false);
            await host.WaitForDeviceAsync(cts.Token).ConfigureAwait(false);
            await host.CompleteSelectAsync(cts.Token).ConfigureAwait(false);
            await WaitUntilAsync(() => transport.IsConnected, TimeSpan.FromSeconds(5), "设备进入 Selected").ConfigureAwait(false);

            host.SendDataMessage(1, 13, w: true, DeviceId, 0x0000_0404,
                new byte[] { 0x01, 0x00 }, cts.Token);
            await Task.Delay(100, cts.Token).ConfigureAwait(false);
            Assert.False(monitorSeen.Task.IsCompleted);
            releaseHandler.SetResult();
            Assert.Null(await handlerResult.Task.WaitAsync(cts.Token).ConfigureAwait(false));
            await monitorSeen.Task.WaitAsync(cts.Token).ConfigureAwait(false);
            var reply = await host.ReadMessageAsync(cts.Token).ConfigureAwait(false);
            Assert.Equal((byte)1, reply.S);
            Assert.Equal((byte)14, reply.F);
        }

        [Fact(Timeout = 30000)]
        public async Task S5F1_Alarm_Is_Acknowledged_By_Host_S5F2()
        {
            using var host = HsmsHostSimulator.Start();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            using var control = CreateControl();
            await control.ConnectAsync(true, "127.0.0.1", host.Port, DeviceId).ConfigureAwait(false);
            await host.WaitForDeviceAsync(cts.Token).ConfigureAwait(false);
            await host.CompleteSelectAsync(cts.Token).ConfigureAwait(false);
            await WaitUntilAsync(() => control.IsConnected, TimeSpan.FromSeconds(5), "设备进入 Selected").ConfigureAwait(false);

            var report = control.S5F1AlarmReportSendAsync(new AlarmInfo
            {
                ID = "12", Description = "Low pressure", Code = 1, Enable = true
            });
            var primary = await host.ReadMessageAsync(cts.Token).ConfigureAwait(false);
            Assert.Equal((byte)5, primary.S);
            Assert.Equal((byte)1, primary.F);
            Assert.True(primary.WBit);
            Assert.Contains("Low pressure", Encoding.ASCII.GetString(primary.Body));

            host.SendDataMessage(5, 2, w: false, DeviceId, primary.SystemBytes,
                new byte[] { 0x21, 0x01, 0x00 }, cts.Token);
            Assert.True(await report.ConfigureAwait(false));
        }

        [Fact(Timeout = 30000)]
        public async Task S6F11_Event_Is_Acknowledged_By_Host_S6F12()
        {
            using var host = HsmsHostSimulator.Start();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            using var control = CreateControl();
            string? reportError = null;
            control.OnError += message => reportError = message;
            var catalog = control._VIDManager;
            var lot = new VIDInfo { ID = "1", Name = "LotId", Value = "LOT-42", DataType = SecsFormat.ASCII };
            var clock = new VIDInfo { ID = "2", Name = "Clock", Value = "", DataType = SecsFormat.ASCII };
            catalog.VIDList.Add(lot);
            catalog.VIDList.Add(clock);
            catalog.VIDDic_Name[lot.Name] = lot;
            catalog.VIDDic_Name[clock.Name] = clock;
            catalog.ReportID.Datas.Add(new ReportInfo { ID = "7", Name = "LotReport", VID_Collection = "1" });
            var ceid = new CEIDInfo { ID = "9", Name = "LotChanged", ReportID_Collection = "7" };
            catalog.CEID.Datas.Add(ceid);
            catalog.CEID.Update(catalog.ReportID.GetDatas());

            await control.ConnectAsync(true, "127.0.0.1", host.Port, DeviceId).ConfigureAwait(false);
            await host.WaitForDeviceAsync(cts.Token).ConfigureAwait(false);
            await host.CompleteSelectAsync(cts.Token).ConfigureAwait(false);
            await WaitUntilAsync(() => control.IsConnected, TimeSpan.FromSeconds(5), "设备进入 Selected").ConfigureAwait(false);

            var report = control.S6F11EventReportSendAsync(ceid);
            if (report.IsCompleted)
                Assert.True(await report.ConfigureAwait(false), reportError);
            var primary = await host.ReadMessageAsync(cts.Token).ConfigureAwait(false);
            Assert.Equal((byte)6, primary.S);
            Assert.Equal((byte)11, primary.F);
            Assert.True(primary.WBit);
            Assert.Contains("LOT-42", Encoding.ASCII.GetString(primary.Body));

            host.SendDataMessage(6, 12, w: false, DeviceId, primary.SystemBytes,
                new byte[] { 0x21, 0x01, 0x00 }, cts.Token);
            Assert.True(await report.ConfigureAwait(false));
        }

        [Fact(Timeout = 30000)]
        public async Task Manual_Sml_Send_Uses_The_Workbench_Message_Format()
        {
            using var host = HsmsHostSimulator.Start();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            using var control = CreateControl();
            await control.ConnectAsync(true, "127.0.0.1", host.Port, DeviceId).ConfigureAwait(false);
            await host.WaitForDeviceAsync(cts.Token).ConfigureAwait(false);
            await host.CompleteSelectAsync(cts.Token).ConfigureAwait(false);
            await WaitUntilAsync(() => control.IsConnected, TimeSpan.FromSeconds(5), "设备进入 Selected").ConfigureAwait(false);

            var send = control.Send("'S1F1' W", true);
            var primary = await host.ReadMessageAsync(cts.Token).ConfigureAwait(false);
            Assert.Equal((byte)1, primary.S);
            Assert.Equal((byte)1, primary.F);
            host.SendDataMessage(1, 2, w: false, DeviceId, primary.SystemBytes,
                new byte[] { 0x01, 0x00 }, cts.Token);
            var reply = await send.ConfigureAwait(false);
            Assert.Equal((byte)2, reply.F);
        }
    }
}
