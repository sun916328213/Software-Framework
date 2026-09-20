using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Extension.Core.Concurrency;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「有界通道」专项：<c>Extension.Core.Concurrency.ChannelHelper&lt;T&gt;</c>。
    /// 这是设备回调 / 日志队列的缓冲底座，此前零用例覆盖。
    /// <para>
    /// 钉住的 5 处「只在运行时暴露」的行为（硬规则 1：只记录不顺手改）：
    /// <list type="number">
    /// <item><b>构造默认满模式是 <c>DropOldest</c>，不是 XML 注释说的「等待」</b>
    /// —— 默认配置下写满会**静默丢弃最旧**的数据；</item>
    /// <item><c>Write</c> 内部是 <c>_writer.TryWrite(item)</c> 且**丢弃返回值** ⇒
    /// 满模式为 <c>Wait</c> 时写满会**静默丢新数据**（既不阻塞也不报错）；</item>
    /// <item><c>ClearAsync</c> 的 <c>while (await WaitToReadAsync())</c> 在**通道未完成**时
    /// 空转等待 ⇒ 空通道上**永久挂起**，只有 <c>Complete()</c> 之后才会返回；</item>
    /// <item><c>TryReadAsync</c> 名不副实：内部是 <c>ReadAsync</c> ⇒ 空通道上挂起、
    /// 已完成且为空的通道上抛 <c>ChannelClosedException</c>；</item>
    /// <item><c>IsCompleted</c> 镜像的是 <c>Reader.Completion</c>，与私有的 <c>_isCompleted</c> 标志
    /// **不是同一件事**：<c>Complete()</c> 之后只要缓冲里还有未读数据，它仍是 <c>false</c>。</item>
    /// </list>
    /// </para>
    /// </summary>
    public class ChannelHelperTests
    {
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
        // 构造
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void 构造_默认满模式其实是DropOldest_与XML注释说的等待不符()
        {
            // 注释写「通道满时的行为，默认为等待」，实际签名默认值是 DropOldest
            using var h = new ChannelHelper<int>(2);

            h.Write(1);
            h.Write(2);
            h.Write(3);        // 若真是「等待」，这条会阻塞；实际是丢掉最旧的 1

            Assert.True(h.TryRead(out int first));
            Assert.Equal(2, first);          // 1 被丢掉了 ⇒ 证明是 DropOldest
            Assert.True(h.TryRead(out int second));
            Assert.Equal(3, second);
        }

        [Fact]
        public void 构造_容量为0或负数会抛异常()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChannelHelper<int>(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChannelHelper<int>(-1));
        }

        // ══════════════════════════════════════════════════════════════════
        // Write / TryRead
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void Write与TryRead_先进先出()
        {
            using var h = new ChannelHelper<string>(4);

            h.Write("a");
            h.Write("b");

            Assert.True(h.TryRead(out string first));
            Assert.Equal("a", first);
            Assert.True(h.TryRead(out string second));
            Assert.Equal("b", second);
            Assert.False(h.TryRead(out _));       // 已空
        }

        [Fact]
        public void Write_满模式为Wait时写满会静默丢新数据_是缺陷()
        {
            // _writer.TryWrite 返回 false 表示没写进去，但 Write 是 void 且忽略返回值
            using var h = new ChannelHelper<int>(1, BoundedChannelFullMode.Wait);

            h.Write(1);
            h.Write(2);        // 没空间 ⇒ 返回 false，被吞掉

            Assert.True(h.TryRead(out int first));
            Assert.Equal(1, first);
            Assert.False(h.TryRead(out _));       // 2 已经彻底丢了，调用方无从感知
        }

        [Fact]
        public void WriteAsync_满模式为Wait时会等到有空间再写()
        {
            using var h = new ChannelHelper<int>(1, BoundedChannelFullMode.Wait);
            h.Write(1);

            Task pending = h.WriteAsync(2);
            Assert.False(pending.Wait(200));      // 卡在满通道上

            Assert.True(h.TryRead(out _));        // 腾出空间
            Assert.True(pending.Wait(2000));
            Assert.True(h.TryRead(out int written));
            Assert.Equal(2, written);
        }

        [Fact]
        public void Clear_返回清除条数并清空通道()
        {
            using var h = new ChannelHelper<int>(8);
            h.Write(1);
            h.Write(2);
            h.Write(3);

            Assert.Equal(3, h.Clear());
            Assert.Equal(0, h.Clear());            // 再清一次是 0
            Assert.False(h.TryRead(out _));
        }

        [Fact]
        public void ClearAsync_空通道上会一直挂着_只有Complete之后才返回()
        {
            using var h = new ChannelHelper<int>(8);

            Task<int> clearing = h.ClearAsync();
            // ⚠️ WaitToReadAsync 在「未完成且无数据」时是**等待**而不是返回 false
            Assert.False(clearing.Wait(300));

            h.Complete();
            Assert.True(clearing.Wait(2000));
            Assert.Equal(0, clearing.Result);
        }

        [Fact]
        public void ClearAsync_即使有数据也会先清完再挂住_必须Complete才返回()
        {
            using var h = new ChannelHelper<int>(8);
            h.Write(1);
            h.Write(2);

            Task<int> clearing = h.ClearAsync();
            Assert.False(clearing.Wait(300));

            h.Complete();
            Assert.True(clearing.Wait(2000));
            Assert.Equal(2, clearing.Result);
        }

        [Fact]
        public async Task TryReadAsync_名字像Try但空通道上是挂起而不是返回默认值()
        {
            using var h = new ChannelHelper<int>(4);

            Task<int> reading = h.TryReadAsync();
            Assert.False(reading.Wait(200));

            h.Write(7);
            Assert.Equal(7, await reading);
        }

        [Fact]
        public async Task TryReadAsync_通道已完成且为空时抛ChannelClosedException()
        {
            using var h = new ChannelHelper<int>(4);
            h.Complete();

            await Assert.ThrowsAsync<ChannelClosedException>(() => h.TryReadAsync());
        }

        [Fact]
        public async Task WaitToReadAsync_有数据时返回true_完成且空时返回false()
        {
            using var h = new ChannelHelper<int>(4);

            h.Write(1);
            Assert.True(await h.WaitToReadAsync());

            h.Complete();
            Assert.True(h.TryRead(out _));
            Assert.False(await h.WaitToReadAsync());
        }

        // ══════════════════════════════════════════════════════════════════
        // Complete / IsCompleted
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void Complete_之后再写入或清空都抛InvalidOperationException()
        {
            using var h = new ChannelHelper<int>(4);
            h.Complete();

            Assert.Throws<InvalidOperationException>(() => h.Write(1));
            Assert.Throws<InvalidOperationException>(() => h.Clear());
        }

        [Fact]
        public async Task Complete_之后再WriteAsync或ClearAsync也抛InvalidOperationException()
        {
            using var h = new ChannelHelper<int>(4);
            h.Complete();

            await Assert.ThrowsAsync<InvalidOperationException>(() => h.WriteAsync(1));
            await Assert.ThrowsAsync<InvalidOperationException>(() => h.ClearAsync());
        }

        [Fact]
        public void Complete_是幂等的_且已缓冲的数据仍可读出()
        {
            using var h = new ChannelHelper<int>(4);
            h.Write(1);

            h.Complete();
            h.Complete();      // 幂等，不抛

            Assert.True(h.TryRead(out int v));
            Assert.Equal(1, v);
        }

        [Fact]
        public void IsCompleted_镜像Reader_Completion_缓冲未读尽时仍为false()
        {
            using var h = new ChannelHelper<int>(4);
            h.Write(1);
            h.Complete();

            // ⚠️ 与私有的 _isCompleted 标志不是一回事：写入端完成了，但缓冲里还有数据
            Assert.False(h.IsCompleted);

            Assert.True(h.TryRead(out _));
            Assert.True(SpinUntil(() => h.IsCompleted, 1000));
        }

        // ══════════════════════════════════════════════════════════════════
        // Dispose
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public async Task Dispose_之后所有操作抛ObjectDisposedException()
        {
            var h = new ChannelHelper<int>(4);
            h.Dispose();

            Assert.Throws<ObjectDisposedException>(() => h.Write(1));
            Assert.Throws<ObjectDisposedException>(() => h.Clear());
            Assert.Throws<ObjectDisposedException>(() => h.Complete());
            await Assert.ThrowsAsync<ObjectDisposedException>(() => h.WriteAsync(1));
            await Assert.ThrowsAsync<ObjectDisposedException>(() => h.ClearAsync());
        }

        [Fact]
        public void Dispose_只完成写入端_已缓冲数据仍可读且读取侧不受影响()
        {
            var h = new ChannelHelper<int>(4);
            h.Write(1);

            h.Dispose();

            // 读取侧（TryRead / Reader / IsCompleted）没有 disposed 检查
            Assert.True(h.TryRead(out int v));
            Assert.Equal(1, v);
            Assert.True(SpinUntil(() => h.IsCompleted, 1000));
        }

        [Fact]
        public void Dispose_是幂等的()
        {
            var h = new ChannelHelper<int>(4);

            h.Dispose();
            h.Dispose();

            Assert.Throws<ObjectDisposedException>(() => h.Write(1));
        }

        [Fact]
        public void Dispose_bool是protected_virtual_是给子类留的钩子()
        {
            Type t = typeof(ChannelHelper<int>);

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
        public void API面_是泛型实例类_实现IDisposable_并暴露Reader与IsCompleted()
        {
            Type t = typeof(ChannelHelper<int>);

            Assert.True(t.IsClass && !t.IsAbstract);
            Assert.True(t.IsGenericType);                     // ChannelHelper<int> 是封闭泛型
            Assert.True(typeof(ChannelHelper<>).IsGenericTypeDefinition);
            Assert.Contains(typeof(IDisposable), t.GetInterfaces());

            Assert.NotNull(t.GetProperty("Reader"));
            Assert.NotNull(t.GetProperty("IsCompleted"));

            // Reader 每次返回同一个底层读取器实例
            using var h = new ChannelHelper<int>(4);
            Assert.Same(h.Reader, h.Reader);
        }
    }
}
