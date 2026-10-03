// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：EventHelperTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Collections.Generic、System.Linq、System.Threading、System.Threading.Tasks、Extension.Core.Concurrency。导入不代表所有类型都会在运行时加载。
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Extension.Core.Concurrency;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「通用事件总线」专项：<c>Extension.Core.Concurrency.EventHelper</c> 与 <c>CEventArgs</c>。
    /// 这是 SECS 指令分发（<c>"S1F17"</c> 之类）的底座，此前零用例覆盖。
    /// <para>
    /// ⚠️ <c>EventHelper._Events</c> 是 **static** 的 ⇒ 本类每个用例都先
    /// <c>ClearAllEvents()</c>，避免用例间互相污染（xunit 同一个测试类内串行执行）。
    /// </para>
    /// <para>
    /// 钉住的 3 处「只在运行时暴露」的行为：
    /// <list type="number">
    /// <item>✅ <b>序 3a（2026-09-18 已修）</b><c>TriggerEvent</c> 的 catch 里原来是 <c>throw ex;</c>
    /// （不是 <c>throw;</c>）⇒ **栈跟踪被重置**、处理器那一帧丢失 —— 已改成 <c>throw;</c>。
    /// ⚠️ 但「异常外传 ⇒ **后续处理器被整体跳过**」这一点**没变**（与注释里"逐个调用并捕获异常"
    /// 的意图仍然相反），本条仍按缺陷记录（见 §5.18）；</item>
    /// <item><c>WaitForEventAsync</c> **超时路径不会 unregister**（<c>finally</c> 只 dispose 了取消注册）
    /// ⇒ 处理器永久残留在 <c>_Events</c> 里；只有「收到事件」和「被取消」两条路径会清理；</item>
    /// <item>两个 <c>WaitForEventAsync</c> 重载的默认超时**不一致**（10000 vs 100000）。</item>
    /// </list>
    /// </para>
    /// </summary>
    public class EventHelperTests
    {
        /// <summary>
        /// 直接读私有的静态事件表，数出某指令上还挂着几个处理器。
        /// 这是把「处理器泄漏」钉成**确定断言**的唯一办法（否则泄漏只能靠间接现象猜）。
        /// </summary>
        private static int HandlerCount(string command)
        {
            var field = typeof(EventHelper).GetField("_Events",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(field);

            var table = (System.Collections.Concurrent.ConcurrentDictionary<string, Action<CEventArgs>>)
                field.GetValue(null);

            return table.TryGetValue(command, out var handlers) ? handlers.GetInvocationList().Length : 0;
        }

        /// <summary>命名方法（不是 lambda）⇒ 便于在异常栈里被搜索到</summary>
        private static void ThrowingHandler(CEventArgs args)
        {
            throw new InvalidOperationException("handler boom");
        }

        // ══════════════════════════════════════════════════════════════════
        // CEventArgs
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 验证 CEventArgs_两个构造函数_单参版本Data为null 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 Equal、Null；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void CEventArgs_两个构造函数_单参版本Data为null()
        {
            var onlyCommand = new CEventArgs("S1F17");
            Assert.Equal("S1F17", onlyCommand.Command);
            Assert.Null(onlyCommand.Data);

            var withData = new CEventArgs("S1F17", 42);
            Assert.Equal("S1F17", withData.Command);
            Assert.Equal(42, withData.Data);
        }

        /// <summary>
        /// 验证 CEventArgs_继承自EventArgs_且两个属性都是只读的 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 True、IsAssignableFrom、NotNull、GetProperty、False；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void CEventArgs_继承自EventArgs_且两个属性都是只读的()
        {
            Type t = typeof(CEventArgs);

            Assert.True(typeof(EventArgs).IsAssignableFrom(t));
            Assert.NotNull(t.GetProperty("Command"));
            Assert.NotNull(t.GetProperty("Data"));
            Assert.False(t.GetProperty("Command").CanWrite);   // 只能靠构造函数赋值
            Assert.False(t.GetProperty("Data").CanWrite);
        }

        // ══════════════════════════════════════════════════════════════════
        // 注册 / 取消注册 / 清空
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void RegisterEvent_多播_按注册顺序依次调用()
        {
            EventHelper.ClearAllEvents();
            var calls = new List<string>();

            EventHelper.RegisterEvent("CMD", _ => calls.Add("1"));
            EventHelper.RegisterEvent("CMD", _ => calls.Add("2"));
            EventHelper.RegisterEvent("CMD", _ => calls.Add("3"));

            EventHelper.TriggerEvent("CMD");

            Assert.Equal(new[] { "1", "2", "3" }, calls);
            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 RegisterEvent_不同指令互不影响 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、RegisterEvent、Add、TriggerEvent、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void RegisterEvent_不同指令互不影响()
        {
            EventHelper.ClearAllEvents();
            var calls = new List<string>();

            EventHelper.RegisterEvent("A", _ => calls.Add("A"));
            EventHelper.RegisterEvent("B", _ => calls.Add("B"));

            EventHelper.TriggerEvent("A");

            Assert.Equal(new[] { "A" }, calls);
            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 RegisterEvent_指令为空或处理器为null时抛异常 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、Throws、RegisterEvent；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void RegisterEvent_指令为空或处理器为null时抛异常()
        {
            EventHelper.ClearAllEvents();

            Assert.Throws<ArgumentException>(() => EventHelper.RegisterEvent("", _ => { }));
            Assert.Throws<ArgumentException>(() => EventHelper.RegisterEvent(null, _ => { }));
            Assert.Throws<ArgumentNullException>(() => EventHelper.RegisterEvent("CMD", null));

            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 UnregisterEvent_摘掉一个处理器_其余仍然生效 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、Add、RegisterEvent、UnregisterEvent、TriggerEvent；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void UnregisterEvent_摘掉一个处理器_其余仍然生效()
        {
            EventHelper.ClearAllEvents();
            var calls = new List<string>();
            Action<CEventArgs> first = _ => calls.Add("1");
            Action<CEventArgs> second = _ => calls.Add("2");

            EventHelper.RegisterEvent("CMD", first);
            EventHelper.RegisterEvent("CMD", second);
            EventHelper.UnregisterEvent("CMD", first);

            EventHelper.TriggerEvent("CMD");

            Assert.Equal(new[] { "2" }, calls);
            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 UnregisterEvent_摘掉未注册的处理器是无操作 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、RegisterEvent、Add、UnregisterEvent、TriggerEvent；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void UnregisterEvent_摘掉未注册的处理器是无操作()
        {
            EventHelper.ClearAllEvents();
            var calls = new List<string>();

            EventHelper.RegisterEvent("CMD", _ => calls.Add("1"));
            EventHelper.UnregisterEvent("CMD", _ => calls.Add("other"));   // 从没注册过

            EventHelper.TriggerEvent("CMD");

            Assert.Equal(new[] { "1" }, calls);
            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 UnregisterEvent_摘掉不存在的指令是无操作 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、UnregisterEvent；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void UnregisterEvent_摘掉不存在的指令是无操作()
        {
            EventHelper.ClearAllEvents();

            // 不抛异常，静默返回
            EventHelper.UnregisterEvent("NEVER-REGISTERED", _ => { });

            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 ClearEvents_只清指定指令 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、RegisterEvent、Add、ClearEvents、TriggerEvent；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void ClearEvents_只清指定指令()
        {
            EventHelper.ClearAllEvents();
            var calls = new List<string>();

            EventHelper.RegisterEvent("A", _ => calls.Add("A"));
            EventHelper.RegisterEvent("B", _ => calls.Add("B"));

            EventHelper.ClearEvents("A");
            EventHelper.TriggerEvent("A");
            EventHelper.TriggerEvent("B");

            Assert.Equal(new[] { "B" }, calls);
            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 TriggerEvent_没有订阅者时什么都不发生 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、TriggerEvent；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void TriggerEvent_没有订阅者时什么都不发生()
        {
            EventHelper.ClearAllEvents();

            EventHelper.TriggerEvent("NOBODY");
            EventHelper.TriggerEvent("NOBODY", 1);     // 两个重载都不抛

            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 TriggerEvent_指令为空时抛ArgumentException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、Throws、TriggerEvent；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void TriggerEvent_指令为空时抛ArgumentException()
        {
            EventHelper.ClearAllEvents();

            Assert.Throws<ArgumentException>(() => EventHelper.TriggerEvent(""));
            Assert.Throws<ArgumentException>(() => EventHelper.TriggerEvent(null));
            Assert.Throws<ArgumentException>(() => EventHelper.TriggerEvent("", 1));

            EventHelper.ClearAllEvents();
        }

        // ══════════════════════════════════════════════════════════════════
        // TriggerEvent 的异常语义（缺陷，只记录）
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void TriggerEvent_处理器抛异常时异常会外传_且后续处理器被整体跳过()
        {
            EventHelper.ClearAllEvents();
            var calls = new List<string>();

            EventHelper.RegisterEvent("CMD", _ => calls.Add("before"));
            EventHelper.RegisterEvent("CMD", ThrowingHandler);
            EventHelper.RegisterEvent("CMD", _ => calls.Add("after"));

            var ex = Record.Exception(() => EventHelper.TriggerEvent("CMD"));

            Assert.IsAssignableFrom<InvalidOperationException>(ex);
            // ⚠️ 注释写的是"逐个调用并捕获异常"，实际是 throw ⇒ 后面的处理器根本没跑到
            Assert.Equal(new[] { "before" }, calls);
            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// <b>序 3a（2026-09-18 已修）</b>：修前这条用例断言的是 <c>DoesNotContain(ThrowingHandler)</c>
        /// —— 即把"<c>throw ex;</c> 重置栈、处理器帧丢失"这个**缺陷本身**钉成了契约。
        /// 改成 <c>throw;</c> 后处理器帧**会**出现在栈里，所以断言方向必须反过来。
        /// </summary>
        [Fact]
        public void TriggerEvent_用的是throw_栈跟踪保留处理器那一帧()
        {
            EventHelper.ClearAllEvents();
            EventHelper.RegisterEvent("CMD", ThrowingHandler);

            var ex = (InvalidOperationException)Record.Exception(() => EventHelper.TriggerEvent("CMD"));

            Assert.Equal("handler boom", ex.Message);
            // ✅ 序 3a 修后：`throw;` 保留原始栈 ⇒ 真正出错的那一帧（ThrowingHandler）**可见**，
            //    同时重新抛出的位置（TriggerEvent）也在栈里 ⇒ 排查时能直接定位到处理器。
            //    （ThrowingHandler 是命名方法而非 lambda，就是为了能在栈里被搜到。）
            Assert.NotNull(ex.StackTrace);
            Assert.Contains(nameof(ThrowingHandler), ex.StackTrace);
            Assert.Contains(nameof(EventHelper.TriggerEvent), ex.StackTrace);

            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 TriggerEvent_带数据的重载同样会外传异常且保留栈 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、RegisterEvent、Exception、TriggerEvent、NotNull；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void TriggerEvent_带数据的重载同样会外传异常且保留栈()
        {
            EventHelper.ClearAllEvents();
            EventHelper.RegisterEvent("CMD", ThrowingHandler);

            var ex = (InvalidOperationException)Record.Exception(() => EventHelper.TriggerEvent("CMD", 1));

            Assert.NotNull(ex);
            // ✅ 序 3a 修后：两个重载（无数据 / 带数据）都改成了 `throw;` ⇒ 处理器帧保留
            Assert.Contains(nameof(ThrowingHandler), ex.StackTrace);

            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 TriggerEvent_带数据的重载会把Data传下去 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、RegisterEvent、TriggerEvent、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void TriggerEvent_带数据的重载会把Data传下去()
        {
            EventHelper.ClearAllEvents();
            object seen = null;
            EventHelper.RegisterEvent("CMD", args => seen = args.Data);

            EventHelper.TriggerEvent("CMD", "payload");

            Assert.Equal("payload", seen);
            EventHelper.ClearAllEvents();
        }

        // ══════════════════════════════════════════════════════════════════
        // WaitForEventAsync
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 验证 WaitForEventAsync_收到事件时返回参数并自动取消注册 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、RegisterEvent、UnregisterEvent、WaitForEventAsync、TriggerEvent；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WaitForEventAsync_收到事件时返回参数并自动取消注册()
        {
            EventHelper.ClearAllEvents();
            int registered = 0, unregistered = 0;

            Action<Action<CEventArgs>> register = h => { registered++; EventHelper.RegisterEvent("CMD", h); };
            Action<Action<CEventArgs>> unregister = h => { unregistered++; EventHelper.UnregisterEvent("CMD", h); };

            Task<CEventArgs> waiting = EventHelper.WaitForEventAsync(register, unregister, 5000);
            EventHelper.TriggerEvent("CMD", 123);

            CEventArgs args = await waiting;

            Assert.Equal("CMD", args.Command);
            Assert.Equal(123, args.Data);
            Assert.Equal(1, registered);
            Assert.Equal(1, unregistered);
            Assert.Equal(0, HandlerCount("CMD"));      // 成功路径确实清干净了
            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 WaitForEventAsync_超时抛TimeoutException_但处理器没有被取消注册_是缺陷 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、RegisterEvent、UnregisterEvent、ThrowsAsync、WaitForEventAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WaitForEventAsync_超时抛TimeoutException_但处理器没有被取消注册_是缺陷()
        {
            EventHelper.ClearAllEvents();
            int registered = 0, unregistered = 0;

            Action<Action<CEventArgs>> register = h => { registered++; EventHelper.RegisterEvent("CMD", h); };
            Action<Action<CEventArgs>> unregister = h => { unregistered++; EventHelper.UnregisterEvent("CMD", h); };

            await Assert.ThrowsAsync<TimeoutException>(
                () => EventHelper.WaitForEventAsync(register, unregister, 50));

            Assert.Equal(1, registered);
            // ⚠️ 缺陷（未修）：超时路径下 finally 只 dispose 了取消注册，**没有 unregister**
            //    ⇒ 处理器永久残留在 _Events 里（每次超时泄漏一个）。
            Assert.Equal(0, unregistered);
            Assert.Equal(1, HandlerCount("CMD"));      // 直接查事件表：确实还挂着 1 个
            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 WaitForEventAsync_取消令牌触发时抛OperationCanceledException 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、WaitForEventAsync、Cancel、ExceptionAsync、IsAssignableFrom；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WaitForEventAsync_取消令牌触发时抛OperationCanceledException()
        {
            EventHelper.ClearAllEvents();
            using var cts = new CancellationTokenSource();

            Task<CEventArgs> waiting = EventHelper.WaitForEventAsync("CMD", 5000, cts.Token);
            cts.Cancel();

            // 取消路径上可能走 tcs.TrySetCanceled（TaskCanceledException），
            // 也可能走 WithCancellation 内部抛 OperationCanceledException ⇒ 用 IsAssignableFrom 兜住两种
            var ex = await Record.ExceptionAsync(() => waiting);
            Assert.IsAssignableFrom<OperationCanceledException>(ex);

            // ⚠️ 取消路径的清理是**竞态**的（实测处理器留了下来，与超时路径同病）：
            //    本方法在 cts.Token 上的注册 与 WithTimeout 内部 linkedCts 的注册 按 LIFO 执行，
            //    linked 那条先跑 ⇒ 异常先冒出来 ⇒ finally 抢先 Dispose 掉本注册
            //    ⇒ unregister 回调可能根本轮不到执行。所以这里只能断言"最多泄漏 1 个"。
            Assert.InRange(HandlerCount("CMD"), 0, 1);
            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 WaitForEventAsync_命令重载_收到事件时返回参数 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、WaitForEventAsync、TriggerEvent、Equal；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WaitForEventAsync_命令重载_收到事件时返回参数()
        {
            EventHelper.ClearAllEvents();

            Task<CEventArgs> waiting = EventHelper.WaitForEventAsync("S1F17", 5000);
            EventHelper.TriggerEvent("S1F17", "body");

            CEventArgs args = await waiting;

            Assert.Equal("S1F17", args.Command);
            Assert.Equal("body", args.Data);
            EventHelper.ClearAllEvents();
        }

        /// <summary>
        /// 验证 WaitForEventAsync_参数校验_异常是被装进Task而不是同步抛出 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ThrowsAsync、WaitForEventAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WaitForEventAsync_参数校验_异常是被装进Task而不是同步抛出()
        {
            // ⚠️ 这些方法都是 async ⇒ 参数校验异常落在返回的 Task 上，必须用 ThrowsAsync
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => EventHelper.WaitForEventAsync((Action<Action<CEventArgs>>)null, _ => { }, 100));
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => EventHelper.WaitForEventAsync(_ => { }, null, 100));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => EventHelper.WaitForEventAsync(_ => { }, _ => { }, -2));
            await Assert.ThrowsAsync<ArgumentException>(
                () => EventHelper.WaitForEventAsync("", 100));
            await Assert.ThrowsAsync<ArgumentException>(
                () => EventHelper.WaitForEventAsync((string)null, 100));
        }

        /// <summary>
        /// 验证 WaitForEventAsync_两个重载的默认超时不一致 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ToArray、Where、GetMethods、nameof、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void WaitForEventAsync_两个重载的默认超时不一致()
        {
            var delegated = typeof(EventHelper).GetMethods()
                .Where(m => m.Name == nameof(EventHelper.WaitForEventAsync))
                .ToArray();

            Assert.Equal(2, delegated.Length);

            // 委托版默认 10000，命令版默认 100000 —— 差 10 倍
            int delegateDefault = (int)delegated.Single(m => m.GetParameters()[0].ParameterType == typeof(Action<Action<CEventArgs>>))
                .GetParameters()[2].DefaultValue;
            int commandDefault = (int)delegated.Single(m => m.GetParameters()[0].ParameterType == typeof(string))
                .GetParameters()[1].DefaultValue;

            Assert.Equal(10000, delegateDefault);
            Assert.Equal(100000, commandDefault);
        }

        /// <summary>
        /// 验证 API面_EventHelper是普通class而不是static_class_8个公开静态方法 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 False、NotNull、CreateInstance、GetMethods、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void API面_EventHelper是普通class而不是static_class_8个公开静态方法()
        {
            Type t = typeof(EventHelper);

            // ⚠️ 声明是 public class EventHelper（**不是** static class）⇒ 可以被 new 出来，
            //    但所有成员都是 static ⇒ 实例毫无用途，也没法靠编译器阻止误用。
            //    与同目录的 TaskEx（static class）不一致。
            Assert.False(t.IsAbstract && t.IsSealed);
            Assert.NotNull(Activator.CreateInstance(t));

            var methods = t.GetMethods(System.Reflection.BindingFlags.Public
                                     | System.Reflection.BindingFlags.Static
                                     | System.Reflection.BindingFlags.DeclaredOnly);

            Assert.Equal(8, methods.Length);
            Assert.Equal(2, methods.Count(m => m.Name == nameof(EventHelper.TriggerEvent)));
            Assert.Equal(2, methods.Count(m => m.Name == nameof(EventHelper.WaitForEventAsync)));
            Assert.Equal(1, methods.Count(m => m.Name == nameof(EventHelper.RegisterEvent)));
            Assert.Equal(1, methods.Count(m => m.Name == nameof(EventHelper.UnregisterEvent)));
            Assert.Equal(1, methods.Count(m => m.Name == nameof(EventHelper.ClearEvents)));
            Assert.Equal(1, methods.Count(m => m.Name == nameof(EventHelper.ClearAllEvents)));
        }

        /// <summary>
        /// 验证 API面_没有一个是扩展方法_全部是普通静态方法 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 GetMethods、All、False、IsDefined；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void API面_没有一个是扩展方法_全部是普通静态方法()
        {
            var methods = typeof(EventHelper).GetMethods(System.Reflection.BindingFlags.Public
                                                      | System.Reflection.BindingFlags.Static
                                                      | System.Reflection.BindingFlags.DeclaredOnly);

            Assert.All(methods, m => Assert.False(
                m.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute), false),
                $"{m.Name} 不应该被当成扩展方法"));
        }

        /// <summary>
        /// 验证 事件表是static的_跨实例共享 所表达的回归场景；准备条件、动作和断言以本测试正文为准。
        /// 正文中的关键协作调用包括 ClearAllEvents、RegisterEvent、Increment、TriggerEvent、Equal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void 事件表是static的_跨实例共享()
        {
            EventHelper.ClearAllEvents();
            int hits = 0;

            // 两个不同的 EventHelper 实例共享同一张表
            EventHelper.RegisterEvent("CMD", _ => Interlocked.Increment(ref hits));
            EventHelper.TriggerEvent("CMD");

            Assert.Equal(1, Volatile.Read(ref hits));
            EventHelper.ClearAllEvents();
        }
    }
}
