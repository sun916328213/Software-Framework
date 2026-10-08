// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：TaskExTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Diagnostics、System.Linq、System.Reflection、System.Threading、System.Threading.Tasks。导入不代表所有类型都会在运行时加载。
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Extension.Core.Concurrency;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「任务超时/取消」专项：<c>Extension.Core.Concurrency.TaskEx</c>（7 个扩展方法）。
    /// 这是全仓异步超时的统一入口，此前零用例覆盖。
    /// <para>
    /// 钉住的 6 处「只在运行时暴露」的行为（硬规则 1：只记录不顺手改）：
    /// <list type="number">
    /// <item><c>WithTimeout</c> 的 <c>timeout = 0</c> 表示**立即超时**，而
    /// <c>ValueTaskSourceHelper.WaitSignal</c> 的 <c>timeout = 0</c> 表示**不设超时**
    /// —— 同一个语义在两个类里相反；</item>
    /// <item><c>WithTimeout</c> 超时**不会取消原始任务**，它仍在后台继续跑（只是不再等它）；</item>
    /// <item><c>WithCancellation</c> 先检查 <c>token.IsCancellationRequested</c> 再 <c>WhenAny</c>
    /// ⇒ **令牌已取消时，即使任务早已完成也会抛异常**；</item>
    /// <item><c>LongTimeRun</c> / <c>TimesRun</c> 用的是
    /// <c>await Task.Factory.StartNew(async () =&gt; {...}, LongRunning)</c>
    /// ⇒ 拿到的是 <c>Task&lt;Task&gt;</c>，<c>await</c> 只等**外层**委托返回 ⇒
    /// <c>await</c> 一返回循环其实还在后台跑，返回的 Task **不代表循环生命周期**；</item>
    /// <item><c>ThreadRun</c> **没有任何参数校验**（其余方法都有）⇒ 传 null 会在
    /// 后台线程上抛未处理异常，可能直接终结进程，故本文件**故意不测**该分支；</item>
    /// <item><c>ThreadRun</c> 的 <c>threadPriority</c> 参数**从未被使用**（死参数）。</item>
    /// </list>
    /// </para>
    /// </summary>
    public class TaskExTests
    {
        /// <summary>
        /// 测试相关入口 SpinUntil；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 StartNew、condition、Sleep；具体条件和先后次序见方法体。
        /// </summary>
        private static bool SpinUntil(Func<bool> condition, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                if (condition()) return true;
                Thread.Sleep(5);
            }
            return condition();
        }

        // ══════════════════════════════════════════════════════════════════
        // WithTimeout（有返回值）
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task WithTimeout_任务按时完成时返回原结果()
        {
            Assert.Equal(7, await Task.FromResult(7).WithTimeout(5000));
        }

        /// <summary>
        /// 测试相关入口 WithTimeout_超时抛TimeoutException_消息里带超时毫秒数；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowsAsync、WithTimeout、Contains；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithTimeout_超时抛TimeoutException_消息里带超时毫秒数()
        {
            var never = new TaskCompletionSource<int>();

            var ex = await Assert.ThrowsAsync<TimeoutException>(() => never.Task.WithTimeout(50));

            Assert.Contains("50ms", ex.Message);
        }

        /// <summary>
        /// 测试相关入口 WithTimeout_超时不会取消原始任务_它仍在后台继续跑；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowsAsync、WithTimeout、False、SetResult、Equal；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithTimeout_超时不会取消原始任务_它仍在后台继续跑()
        {
            var never = new TaskCompletionSource<int>();

            await Assert.ThrowsAsync<TimeoutException>(() => never.Task.WithTimeout(50));

            // ⚠️ WithTimeout 只是"不再等"，并没有 Cancel 掉原任务
            Assert.False(never.Task.IsCompleted);

            never.SetResult(9);
            Assert.Equal(9, await never.Task);      // 原任务仍可正常完成
        }

        /// <summary>
        /// 测试相关入口 WithTimeout_外部取消时抛OperationCanceledException而不是TimeoutException；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WithTimeout、Cancel、ExceptionAsync、IsAssignableFrom、IsNotType；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithTimeout_外部取消时抛OperationCanceledException而不是TimeoutException()
        {
            using var cts = new CancellationTokenSource();
            var never = new TaskCompletionSource<int>();

            Task<int> waiting = never.Task.WithTimeout(5000, cts.Token);
            cts.Cancel();

            var ex = await Record.ExceptionAsync(() => waiting);

            Assert.IsAssignableFrom<OperationCanceledException>(ex);
            Assert.IsNotType<TimeoutException>(ex);
        }

        /// <summary>
        /// 测试相关入口 WithTimeout_timeout为0会立即超时_与WaitSignal的0表示不超时相反；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowsAsync、WithTimeout；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithTimeout_timeout为0会立即超时_与WaitSignal的0表示不超时相反()
        {
            var never = new TaskCompletionSource<int>();

            // ⚠️ TaskEx: 0 ⇒ 立即超时；ValueTaskSourceHelper.WaitSignal: 0 ⇒ 不设超时
            await Assert.ThrowsAsync<TimeoutException>(() => never.Task.WithTimeout(0));
        }

        /// <summary>
        /// 测试相关入口 WithTimeout_timeout为负1表示无限等待_直接透传；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、WithTimeout、FromResult；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithTimeout_timeout为负1表示无限等待_直接透传()
        {
            Assert.Equal(3, await Task.FromResult(3).WithTimeout(-1));
        }

        /// <summary>
        /// 测试相关入口 WithTimeout_timeout为负1但有取消令牌时走取消路径；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WithTimeout、Cancel、ExceptionAsync、IsAssignableFrom；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithTimeout_timeout为负1但有取消令牌时走取消路径()
        {
            using var cts = new CancellationTokenSource();
            var never = new TaskCompletionSource<int>();

            Task<int> waiting = never.Task.WithTimeout(-1, cts.Token);
            cts.Cancel();

            var ex = await Record.ExceptionAsync(() => waiting);
            Assert.IsAssignableFrom<OperationCanceledException>(ex);
        }

        /// <summary>
        /// 测试相关入口 WithTimeout_超时时间小于负1抛ArgumentOutOfRangeException_且是装进Task的；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowsAsync、WithTimeout、FromResult；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithTimeout_超时时间小于负1抛ArgumentOutOfRangeException_且是装进Task的()
        {
            // async 方法 ⇒ 参数校验异常落在 Task 上
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => Task.FromResult(1).WithTimeout(-2));
        }

        /// <summary>
        /// 测试相关入口 WithTimeout_原始任务的异常会原样传播；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WithTimeout、SetException、ThrowsAsync、Equal；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithTimeout_原始任务的异常会原样传播()
        {
            var faulted = new TaskCompletionSource<int>();
            Task<int> waiting = faulted.Task.WithTimeout(5000);
            faulted.SetException(new InvalidOperationException("inner"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => waiting);
            Assert.Equal("inner", ex.Message);
        }

        // ══════════════════════════════════════════════════════════════════
        // WithTimeout（无返回值）
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task WithTimeout_无返回值版本_正常完成()
        {
            await Task.CompletedTask.WithTimeout(5000);
        }

        /// <summary>
        /// 测试相关入口 WithTimeout_无返回值版本_超时抛TimeoutException；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowsAsync、WithTimeout；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithTimeout_无返回值版本_超时抛TimeoutException()
        {
            var never = new TaskCompletionSource<bool>();
            Task asPlainTask = never.Task;      // 强制绑到非泛型重载

            await Assert.ThrowsAsync<TimeoutException>(() => asPlainTask.WithTimeout(50));
        }

        /// <summary>
        /// 测试相关入口 WithTimeout_无返回值版本_参数校验；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowsAsync、WithTimeout；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithTimeout_无返回值版本_参数校验()
        {
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => Task.CompletedTask.WithTimeout(-2));
        }

        // ══════════════════════════════════════════════════════════════════
        // WithCancellation
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 测试相关入口 WithCancellation_任务先完成时正常返回结果；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、WithCancellation、FromResult；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithCancellation_任务先完成时正常返回结果()
        {
            Assert.Equal(3, await Task.FromResult(3).WithCancellation(default));
        }

        /// <summary>
        /// 测试相关入口 WithCancellation_令牌已取消时_即使任务早已完成也会抛异常；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Cancel、ExceptionAsync、WithCancellation、FromResult、IsAssignableFrom；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithCancellation_令牌已取消时_即使任务早已完成也会抛异常()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            // ⚠️ 先查 IsCancellationRequested 再 WhenAny ⇒ 已完成的任务也救不回来
            var ex = await Record.ExceptionAsync(() => Task.FromResult(1).WithCancellation(cts.Token));

            Assert.IsAssignableFrom<OperationCanceledException>(ex);
        }

        /// <summary>
        /// 测试相关入口 WithCancellation_等待中被取消时抛OperationCanceledException；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WithCancellation、Cancel、ExceptionAsync、IsAssignableFrom、False；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithCancellation_等待中被取消时抛OperationCanceledException()
        {
            using var cts = new CancellationTokenSource();
            var never = new TaskCompletionSource<int>();

            Task<int> waiting = never.Task.WithCancellation(cts.Token);
            cts.Cancel();

            var ex = await Record.ExceptionAsync(() => waiting);
            Assert.IsAssignableFrom<OperationCanceledException>(ex);
            Assert.False(never.Task.IsCompleted);      // 同样没有取消原任务
        }

        /// <summary>
        /// 测试相关入口 WithCancellation_原始任务异常会传播；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WithCancellation、SetException、ThrowsAsync、Equal；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithCancellation_原始任务异常会传播()
        {
            var faulted = new TaskCompletionSource<int>();
            Task<int> waiting = faulted.Task.WithCancellation(default);
            faulted.SetException(new InvalidOperationException("inner"));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => waiting);
            Assert.Equal("inner", ex.Message);
        }

        /// <summary>
        /// 测试相关入口 WithCancellation_无返回值版本；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WithCancellation、Cancel、ExceptionAsync、IsAssignableFrom；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WithCancellation_无返回值版本()
        {
            await Task.CompletedTask.WithCancellation(default);

            using var cts = new CancellationTokenSource();
            var never = new TaskCompletionSource<bool>();
            Task asPlainTask = never.Task;
            Task waiting = asPlainTask.WithCancellation(cts.Token);
            cts.Cancel();

            var ex = await Record.ExceptionAsync(() => waiting);
            Assert.IsAssignableFrom<OperationCanceledException>(ex);
        }

        // ══════════════════════════════════════════════════════════════════
        // TimesRun
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 测试相关入口 TimesRun_恰好执行指定次数；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 TimesRun、Increment、True、SpinUntil、Read；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task TimesRun_恰好执行指定次数()
        {
            int count = 0;

            await ((Action)(() => Interlocked.Increment(ref count))).TimesRun(10, 3);

            Assert.True(SpinUntil(() => Volatile.Read(ref count) >= 3, 3000));
            Assert.Equal(3, Volatile.Read(ref count));
        }

        /// <summary>
        /// 测试相关入口 TimesRun_次数为0时一次都不执行；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 TimesRun、Increment、Sleep、Equal、Read；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task TimesRun_次数为0时一次都不执行()
        {
            int count = 0;

            await ((Action)(() => Interlocked.Increment(ref count))).TimesRun(10, 0);
            Thread.Sleep(150);

            Assert.Equal(0, Volatile.Read(ref count));
        }

        /// <summary>
        /// 测试相关入口 TimesRun_动作抛异常不会中断循环；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 TimesRun、Increment、True、SpinUntil、Read；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task TimesRun_动作抛异常不会中断循环()
        {
            int count = 0;

            await ((Action)(() =>
            {
                Interlocked.Increment(ref count);
                throw new InvalidOperationException("boom");
            })).TimesRun(5, 3);

            Assert.True(SpinUntil(() => Volatile.Read(ref count) >= 3, 3000));
        }

        /// <summary>
        /// 测试相关入口 TimesRun_参数校验；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowsAsync、TimesRun；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task TimesRun_参数校验()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => ((Action)null).TimesRun(10, 1));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ((Action)(() => { })).TimesRun(-1, 1));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ((Action)(() => { })).TimesRun(1, -1));
        }

        // ══════════════════════════════════════════════════════════════════
        // LongTimeRun
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 测试相关入口 LongTimeRun_await返回后循环其实还在后台跑_返回的Task不代表循环生命周期；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 LongTimeRun、Increment、True、Wait、Read；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public async Task LongTimeRun_await返回后循环其实还在后台跑_返回的Task不代表循环生命周期()
        {
            int count = 0;
            using var cts = new CancellationTokenSource();

            Task outer = ((Action)(() => Interlocked.Increment(ref count))).LongTimeRun(30, cts.Token);

            // ⚠️ StartNew(async () => ...) 得到 Task<Task>，await 只等外层委托返回
            //    ⇒ 外层任务很快就"完成"了，但循环还在跑
            Assert.True(outer.Wait(2000));

            int atReturn = Volatile.Read(ref count);
            Assert.True(SpinUntil(() => Volatile.Read(ref count) > atReturn, 2000),
                        "await 返回后循环应当仍在继续执行");

            cts.Cancel();
        }

        /// <summary>
        /// 测试相关入口 LongTimeRun_取消令牌触发后循环停止；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 LongTimeRun、Increment、True、SpinUntil、Read；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public async Task LongTimeRun_取消令牌触发后循环停止()
        {
            int count = 0;
            using var cts = new CancellationTokenSource();

            // 故意不 await：返回的 Task 只代表"启动完成"，不代表循环结束
            _ = ((Action)(() => Interlocked.Increment(ref count))).LongTimeRun(20, cts.Token);
            Assert.True(SpinUntil(() => Volatile.Read(ref count) >= 2, 3000));

            cts.Cancel();
            Thread.Sleep(200);
            int afterCancel = Volatile.Read(ref count);
            Thread.Sleep(200);

            Assert.Equal(afterCancel, Volatile.Read(ref count));
        }

        /// <summary>
        /// 测试相关入口 LongTimeRun_动作抛异常不会终止循环；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 LongTimeRun、Increment、True、SpinUntil、Read；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public async Task LongTimeRun_动作抛异常不会终止循环()
        {
            int count = 0;
            using var cts = new CancellationTokenSource();

            _ = ((Action)(() =>
            {
                Interlocked.Increment(ref count);
                throw new InvalidOperationException("boom");
            })).LongTimeRun(10, cts.Token);

            Assert.True(SpinUntil(() => Volatile.Read(ref count) >= 3, 3000));
            cts.Cancel();
        }

        /// <summary>
        /// 测试相关入口 LongTimeRun_参数校验；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThrowsAsync、LongTimeRun；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task LongTimeRun_参数校验()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => ((Action)null).LongTimeRun(10));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ((Action)(() => { })).LongTimeRun(-1));
        }

        // ══════════════════════════════════════════════════════════════════
        // ThreadRun
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 测试相关入口 ThreadRun_在后台线程上执行一次；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThreadRun、Increment、True、SpinUntil、Read；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ThreadRun_在后台线程上执行一次()
        {
            int hit = 0;

            // ⚠️ 故意不测 ThreadRun(null)：该方法没有参数校验，
            //    会在后台线程上抛未处理异常（可能直接终结进程），不能放进测试进程里跑。
            ((Action)(() => Interlocked.Increment(ref hit))).ThreadRun();

            Assert.True(SpinUntil(() => Volatile.Read(ref hit) == 1, 2000));
        }

        /// <summary>
        /// 测试相关入口 ThreadRun_是fire_and_forget_返回时动作可能还没跑完；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 ThreadRun、Sleep、Increment、True、SpinUntil；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ThreadRun_是fire_and_forget_返回时动作可能还没跑完()
        {
            int hit = 0;

            ((Action)(() => { Thread.Sleep(150); Interlocked.Increment(ref hit); })).ThreadRun();

            // 返回是即时的（void，不等待线程结束）
            Assert.True(SpinUntil(() => Volatile.Read(ref hit) == 1, 3000));
        }

        /// <summary>
        /// 测试相关入口 API面_ThreadRun的优先级参数是死参数_源码里从未使用；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetMethod、nameof、NotNull、Equal、GetParameters；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void API面_ThreadRun的优先级参数是死参数_源码里从未使用()
        {
            // 只钉住签名（无法从外部观察线程优先级，故不做行为断言）：
            // 参数存在但方法体里只有 thread.IsBackground = true; thread.Start();
            MethodInfo m = typeof(TaskEx).GetMethod(nameof(TaskEx.ThreadRun),
                                                    new[] { typeof(Action), typeof(ThreadPriority) });

            Assert.NotNull(m);
            Assert.Equal(ThreadPriority.Normal, m.GetParameters()[1].DefaultValue);
        }

        // ══════════════════════════════════════════════════════════════════
        // API 面
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void API面_TaskEx是静态类_7个公开扩展方法()
        {
            Type t = typeof(TaskEx);

            Assert.True(t.IsAbstract && t.IsSealed);

            MethodInfo[] methods = t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);

            Assert.Equal(7, methods.Length);
            Assert.Equal(2, methods.Count(m => m.Name == nameof(TaskEx.WithTimeout)));
            Assert.Equal(2, methods.Count(m => m.Name == nameof(TaskEx.WithCancellation)));
            Assert.Equal(1, methods.Count(m => m.Name == nameof(TaskEx.LongTimeRun)));
            Assert.Equal(1, methods.Count(m => m.Name == nameof(TaskEx.TimesRun)));
            Assert.Equal(1, methods.Count(m => m.Name == nameof(TaskEx.ThreadRun)));

            Assert.All(methods, m => Assert.True(
                m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false),
                $"{m.Name} 应当是扩展方法"));
        }
    }
}
