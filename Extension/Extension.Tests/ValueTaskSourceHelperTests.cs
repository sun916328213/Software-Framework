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

        [Fact]
        public async Task WaitSignal_引用类型结果也能传递()
        {
            var h = new ValueTaskSourceHelper<string>();

            ValueTask<string> vt = h.WaitSignal();
            h.SetSignal("done");

            Assert.Equal("done", await vt);
        }

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

        [Fact]
        public async Task SetSignal_已经完成之后第二次返回false_不会覆盖首个结果()
        {
            var h = new ValueTaskSourceHelper<int>();

            ValueTask<int> vt = h.WaitSignal();
            Assert.True(h.SetSignal(1));
            Assert.False(h.SetSignal(2));      // 不再是 Pending

            Assert.Equal(1, await vt);
        }

        [Fact]
        public async Task SetException_能让等待方收到指定异常()
        {
            var h = new ValueTaskSourceHelper<int>();

            ValueTask<int> vt = h.WaitSignal();
            Assert.True(h.SetException(new InvalidOperationException("boom")));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => vt.AsTask());
            Assert.Equal("boom", ex.Message);
        }

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

        [Fact]
        public async Task WaitSignal_外部取消令牌触发时抛OperationCanceledException()
        {
            var h = new ValueTaskSourceHelper<int>();
            using var cts = new CancellationTokenSource();

            ValueTask<int> vt = h.WaitSignal(-1, cts.Token);
            cts.Cancel();

            await Assert.ThrowsAsync<OperationCanceledException>(() => vt.AsTask());
        }

        [Fact]
        public async Task WaitSignal_timeout为0或负数表示不设超时_不会自己失败()
        {
            var h = new ValueTaskSourceHelper<int>();

            ValueTask<int> vt = h.WaitSignal(0);
            Assert.False(vt.IsCompleted);        // 没有超时计时器

            h.SetSignal(9);
            Assert.Equal(9, await vt);
        }

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

        [Fact]
        public void WaitSignal在Dispose之后抛ObjectDisposedException()
        {
            var h = new ValueTaskSourceHelper<int>();
            h.Dispose();

            Assert.Throws<ObjectDisposedException>(() => h.WaitSignal());
        }

        [Fact]
        public void SetSignal与SetException在Dispose之后只返回false不抛异常()
        {
            var h = new ValueTaskSourceHelper<int>();
            h.Dispose();

            // ⚠️ 与 WaitSignal 抛 ObjectDisposedException 不对称
            Assert.False(h.SetSignal(1));
            Assert.False(h.SetException(new Exception("x")));
        }

        [Fact]
        public void Dispose是幂等的()
        {
            var h = new ValueTaskSourceHelper<int>();

            h.Dispose();
            h.Dispose();

            Assert.False(h.SetSignal(1));
        }

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

        [Fact]
        public void API面_泛型参数是结果类型而不是元素类型()
        {
            // ValueTaskSourceHelper<TResult> 只有 1 个泛型参数
            Assert.Single(typeof(ValueTaskSourceHelper<>).GetGenericArguments());
        }
    }
}
