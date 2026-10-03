// 【中文阅读说明】
// Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：CommunicationDeviceBaseTests、BlockingDisposeDevice、TestParam。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：Communication.Core、Communication.Runtime.Core、Xunit。导入不代表所有类型都会在运行时加载。
using Communication.Core;
using Communication.Runtime.Core;
using Xunit;

namespace Communication.Tests.Core;

/// <summary>
/// CommunicationDeviceBaseTests 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
/// </summary>
public sealed class CommunicationDeviceBaseTests
{
    /// <summary>
    /// 验证 Concurrent_dispose_calls_share_completion_and_cleanup_once 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
    /// 正文中的关键协作调用包括 AsTask、DisposeAsync、WaitAsync、FromSeconds、Same；具体条件和先后次序见方法体。
    /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
    /// </summary>
    [Fact]
    public async Task Concurrent_dispose_calls_share_completion_and_cleanup_once()
    {
        var device = new BlockingDisposeDevice();

        var firstDispose = device.DisposeAsync().AsTask();
        await device.DisconnectStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        var secondDispose = device.DisposeAsync().AsTask();

        try
        {
            Assert.Same(firstDispose, secondDispose);
        }
        finally
        {
            device.ResumeDisconnect.TrySetResult();
        }

        await Task.WhenAll(firstDispose, secondDispose);
        Assert.Equal(1, device.DisconnectCount);
        Assert.Equal(1, device.CleanupCount);
    }

    /// <summary>
    /// BlockingDisposeDevice 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：CommunicationDeviceBase。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class BlockingDisposeDevice : CommunicationDeviceBase
    {
        /// <summary>
        /// 初始化 BlockingDisposeDevice 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        public BlockingDisposeDevice()
            : base(new TestParam())
        {
        }

        public TaskCompletionSource DisconnectStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ResumeDisconnect { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int DisconnectCount { get; private set; }

        public int CleanupCount { get; private set; }

        public override DeviceRole Role => DeviceRole.Client;

        /// <summary>
        /// 验证 ConnectAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public override Task<bool> ConnectAsync(CancellationToken ct = default) =>
            Task.FromResult(true);

        /// <summary>
        /// 验证 DisconnectAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 TrySetResult、WaitAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        public override async Task DisconnectAsync(CancellationToken ct = default)
        {
            DisconnectCount++;
            DisconnectStarted.TrySetResult();
            await ResumeDisconnect.Task.WaitAsync(ct);
        }

        /// <summary>
        /// 验证 ReadAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult、Empty；具体条件和先后次序见方法体。
        /// </summary>
        public override Task<byte[]> ReadAsync(
            string address,
            int length,
            CancellationToken ct = default) =>
            Task.FromResult(Array.Empty<byte>());

        /// <summary>
        /// 验证 WriteAsync 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 FromResult；具体条件和先后次序见方法体。
        /// </summary>
        public override Task<bool> WriteAsync(
            string address,
            byte[] data,
            CancellationToken ct = default) =>
            Task.FromResult(true);

        /// <summary>
        /// 验证 Dispose 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Dispose；具体条件和先后次序见方法体。
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CleanupCount++;
            }

            base.Dispose(disposing);
        }
    }

    /// <summary>
    /// TestParam 的类型定义。Communication 模块：连接、协议适配及通信界面；主动请求应答与被动数据接收需遵守所用连接的实际契约。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
    /// 继承或实现的契约：CommunicationParam。具体约束按这些契约及各成员实现解释。
    /// </summary>
    private sealed class TestParam : CommunicationParam
    {
    }
}
