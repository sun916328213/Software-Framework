// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。 本文件属于测试，构造条件与断言不代表真实硬件或生产环境已验收。
// 本文件定义：ValueTaskSourceHelperTests。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Linq、System.Reflection、System.Threading、System.Threading.Tasks、System.Threading.Tasks.Sources。导入不代表所有类型都会在运行时加载。
using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Sources;
using Extension.Core.Concurrency;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「可复用 ValueTask 源」专项：<c>Extension.Core.Concurrency.ValueTaskSourceHelper&lt;TResult&gt;</c>。
    /// 手写 <see cref="IValueTaskSource{TResult}"/> 的实现，用于「等待一个外部信号 / 超时 / 取消」，
    /// 此前零用例覆盖。
    /// <para>
    /// 钉住的行为：
    /// <list type="bullet">
    /// <item>正常路径：<c>WaitSignal()</c> → <c>SetSignal(v)</c> → <c>await</c> 拿到 v；</item>
    /// <item>超时 / 外部取消都走 <c>SetException</c>，分别抛 <c>TimeoutException</c> 与 <c>OperationCanceledException</c>；</item>
    /// <item><b>调用顺序敏感</b>：先 <c>SetSignal</c> 再 <c>WaitSignal</c> 会因为 <c>Reset()</c> 把信号丢掉，
    /// 等待方**永远等不到**（未修，只记录）；</item>
    /// <item><b>Dispose 后行为不对称</b>：<c>WaitSignal</c> 抛 <c>ObjectDisposedException</c>，
    /// 而 <c>SetSignal</c> / <c>SetException</c> 只返回 <c>false</c>；</item>
    /// <item>实例可**重复使用**：<c>WaitSignal</c> 会 <c>Reset()</c> 递增版本号。</item>
    /// </list>
    /// </para>
    /// </summary>
    public class ValueTaskSourceHelperTests
    {
        // ══════════════════════════════════════════════════════════════════
        // 正常路径
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task WaitSignal_设置信号后能取到结果()
        {
            var h = new ValueTaskSourceHelper<int>();

            ValueTask<int> vt = h.WaitSignal();
            Assert.True(h.SetSignal(42));

            Assert.Equal(42, await vt);
        }

        /// <summary>
        /// 测试相关入口 WaitSignal_引用类型结果也能传递；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WaitSignal、SetSignal、Equal；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WaitSignal_引用类型结果也能传递()
        {
            var h = new ValueTaskSourceHelper<string>();

            ValueTask<string> vt = h.WaitSignal();
            h.SetSignal("done");

            Assert.Equal("done", await vt);
        }

        /// <summary>
        /// 测试相关入口 同一个实例可以重复使用_第二次WaitSignal仍然有效；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WaitSignal、SetSignal、Equal；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task 同一个实例可以重复使用_第二次WaitSignal仍然有效()
        {
            var h = new ValueTaskSourceHelper<int>();

            ValueTask<int> first = h.WaitSignal();
            h.SetSignal(1);
            Assert.Equal(1, await first);

            // WaitSignal 内部 Reset() 递增版本号，所以可以接着用第二次
            ValueTask<int> second = h.WaitSignal();
            h.SetSignal(2);
            Assert.Equal(2, await second);
        }

        /// <summary>
        /// 测试相关入口 SetSignal_已经完成之后第二次返回false_不会覆盖首个结果；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WaitSignal、True、SetSignal、False、Equal；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task SetSignal_已经完成之后第二次返回false_不会覆盖首个结果()
        {
            var h = new ValueTaskSourceHelper<int>();

            ValueTask<int> vt = h.WaitSignal();
            Assert.True(h.SetSignal(1));
            Assert.False(h.SetSignal(2));      // 不再是 Pending

            Assert.Equal(1, await vt);
        }

        /// <summary>
        /// 测试相关入口 SetException_能让等待方收到指定异常；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WaitSignal、True、SetException、ThrowsAsync、AsTask；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task SetException_能让等待方收到指定异常()
        {
            var h = new ValueTaskSourceHelper<int>();

            ValueTask<int> vt = h.WaitSignal();
            Assert.True(h.SetException(new InvalidOperationException("boom")));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => vt.AsTask());
            Assert.Equal("boom", ex.Message);
        }

        /// <summary>
        /// 测试相关入口 SetException_第二次返回false；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WaitSignal、True、SetException、False、ThrowsAsync；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task SetException_第二次返回false()
        {
            var h = new ValueTaskSourceHelper<int>();

            ValueTask<int> vt = h.WaitSignal();
            Assert.True(h.SetException(new InvalidOperationException("first")));
            Assert.False(h.SetException(new InvalidOperationException("second")));

            await Assert.ThrowsAsync<InvalidOperationException>(() => vt.AsTask());
        }

        // ══════════════════════════════════════════════════════════════════
        // 超时 / 取消
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task WaitSignal_超时会抛TimeoutException()
        {
            var h = new ValueTaskSourceHelper<int>();

            ValueTask<int> vt = h.WaitSignal(50);

            await Assert.ThrowsAsync<TimeoutException>(() => vt.AsTask());
        }

        /// <summary>
        /// 测试相关入口 WaitSignal_外部取消令牌触发时抛OperationCanceledException；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WaitSignal、Cancel、ThrowsAsync、AsTask；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WaitSignal_外部取消令牌触发时抛OperationCanceledException()
        {
            var h = new ValueTaskSourceHelper<int>();
            using var cts = new CancellationTokenSource();

            ValueTask<int> vt = h.WaitSignal(-1, cts.Token);
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => vt.AsTask());
        }

        /// <summary>
        /// 测试相关入口 WaitSignal_timeout为0或负数表示不设超时_不会自己失败；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WaitSignal、False、SetSignal、Equal；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WaitSignal_timeout为0或负数表示不设超时_不会自己失败()
        {
            var h = new ValueTaskSourceHelper<int>();

            ValueTask<int> vt = h.WaitSignal(0);
            Assert.False(vt.IsCompleted);        // 没有超时计时器

            h.SetSignal(9);
            Assert.Equal(9, await vt);
        }

        /// <summary>
        /// 测试相关入口 WaitSignal_超时前收到信号则正常返回_不会误报超时；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 WaitSignal、SetSignal、Equal；具体条件和先后次序见方法体。
        /// 此入口等待异步协作完成；await 本身不保证切换到后台线程，界面线程要求仍由调用上下文决定。
        /// </summary>
        [Fact]
        public async Task WaitSignal_超时前收到信号则正常返回_不会误报超时()
        {
            var h = new ValueTaskSourceHelper<int>();

            ValueTask<int> vt = h.WaitSignal(5000);
            h.SetSignal(7);

            Assert.Equal(7, await vt);
        }

        // ══════════════════════════════════════════════════════════════════
        // 调用顺序敏感（缺陷，只记录）
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task SetSignal在WaitSignal之前调用会被Reset丢掉_信号永久丢失()
        {
            var h = new ValueTaskSourceHelper<int>();

            // ⚠️ 缺陷（未修）：构造后 _tcs.Version == 0 且状态是 Pending，
            //    所以此时 SetSignal 会成功；但紧接着 WaitSignal 里的 Reset() 会把结果清掉，
            //    等待方从此**永远等不到**（只能靠超时/取消兜底）。
            Assert.True(h.SetSignal(5));

            ValueTask<int> vt = h.WaitSignal();
            Assert.False(vt.IsCompleted);

            // 兜底只能靠超时/取消
            h.SetException(new TimeoutException("手动兜底"));
            await Assert.ThrowsAsync<TimeoutException>(() => vt.AsTask());
        }

        /// <summary>
        /// 测试相关入口 GetStatus_在Reset之前版本号是0且状态为Pending；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Equal、GetStatus、True、SetSignal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void GetStatus_在Reset之前版本号是0且状态为Pending()
        {
            var h = new ValueTaskSourceHelper<int>();

            // 构造后未 Reset，版本号 0；这是 SetSignal 内部用的同一个判断
            Assert.Equal(ValueTaskSourceStatus.Pending, h.GetStatus(0));

            Assert.True(h.SetSignal(3));
            Assert.Equal(ValueTaskSourceStatus.Succeeded, h.GetStatus(0));
        }

        // ══════════════════════════════════════════════════════════════════
        // Dispose
        // ══════════════════════════════════════════════════════════════════

        /// <summary>
        /// 测试相关入口 WaitSignal在Dispose之后抛ObjectDisposedException；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Dispose、Throws、WaitSignal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void WaitSignal在Dispose之后抛ObjectDisposedException()
        {
            var h = new ValueTaskSourceHelper<int>();
            h.Dispose();

            Assert.Throws<ObjectDisposedException>(() => h.WaitSignal());
        }

        /// <summary>
        /// 测试相关入口 SetSignal与SetException在Dispose之后只返回false不抛异常；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Dispose、False、SetSignal、SetException；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void SetSignal与SetException在Dispose之后只返回false不抛异常()
        {
            var h = new ValueTaskSourceHelper<int>();
            h.Dispose();

            // ⚠️ 与 WaitSignal 抛 ObjectDisposedException 不对称
            Assert.False(h.SetSignal(1));
            Assert.False(h.SetException(new Exception("x")));
        }

        /// <summary>
        /// 测试相关入口 Dispose是幂等的；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Dispose、False、SetSignal；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dispose是幂等的()
        {
            var h = new ValueTaskSourceHelper<int>();

            h.Dispose();
            h.Dispose();

            Assert.False(h.SetSignal(1));
        }

        /// <summary>
        /// 测试相关入口 Dispose_bool是protected_virtual_是给子类留的钩子；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 GetMethod、NotNull、True；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void Dispose_bool是protected_virtual_是给子类留的钩子()
        {
            Type t = typeof(ValueTaskSourceHelper<int>);

            MethodInfo d = t.GetMethod("Dispose", BindingFlags.NonPublic | BindingFlags.Instance,
                                       null, new[] { typeof(bool) }, null);

            Assert.NotNull(d);
            Assert.True(d.IsFamily);
            Assert.True(d.IsVirtual);
        }

        // ══════════════════════════════════════════════════════════════════
        // API 面
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void API面_实现IValueTaskSource与IDisposable_6个可覆写成员()
        {
            Type t = typeof(ValueTaskSourceHelper<int>);

            Assert.True(t.IsClass && !t.IsAbstract);
            Assert.Contains(typeof(IDisposable), t.GetInterfaces());
            Assert.Contains(typeof(IValueTaskSource<int>), t.GetInterfaces());

            MethodInfo[] declared = t
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .ToArray();

            // 显式 virtual（可覆写）的 6 个：GetResult / GetStatus / OnCompleted / SetSignal / SetException / WaitSignal
            string[] overridable = declared
                .Where(m => m.IsVirtual && !m.IsFinal)
                .Select(m => m.Name).OrderBy(n => n).ToArray();
            Assert.Equal(new[] { "GetResult", "GetStatus", "OnCompleted", "SetException", "SetSignal", "WaitSignal" },
                         overridable);

            // ⚠️ Dispose() 源码里写的是 public void（没有 virtual），但因为实现 IDisposable，
            //    编译器会为它补一个 virtual + final 的接口槽 ⇒ IsVirtual 也是 true，而 IsFinal 为 true
            //    ⇒ 子类**无法覆写**它（想扩展只能覆写 protected virtual Dispose(bool)）。
            MethodInfo dispose = declared.Single(m => m.Name == "Dispose");
            Assert.True(dispose.IsVirtual);
            Assert.True(dispose.IsFinal);
        }

        /// <summary>
        /// 测试相关入口 API面_泛型参数是结果类型而不是元素类型；准备条件、协作调用与结果检查以方法正文为准。
        /// 正文中的关键协作调用包括 Single、GetGenericArguments；具体条件和先后次序见方法体。
        /// </summary>
        [Fact]
        public void API面_泛型参数是结果类型而不是元素类型()
        {
            // ValueTaskSourceHelper<TResult> 只有 1 个泛型参数
            Assert.Single(typeof(ValueTaskSourceHelper<>).GetGenericArguments());
        }
    }
}
