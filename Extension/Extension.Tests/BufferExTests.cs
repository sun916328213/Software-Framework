using System;
using Extension.Core.Buffers;
using Extension.IO.Compression;
using Extension.Integration.Cloning;
using Xunit;

namespace Extension.Tests
{
    /// <summary>
    /// T0 层「平移缓冲」专项：<c>Extension.Core.Buffers.BufferEx.SlidingBuffer</c>。
    /// 通信/采集侧的高性能环形缓冲（少拷贝），此前零用例覆盖。
    /// 重点是**索引算术**：追加 / 消费 / 平移压缩 / 自动扩容四者的交互。
    /// </summary>
    public class BufferExTests
    {
        // ══════════════════════════════════════════════════════════════════
        // 基本语义
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void 新建缓冲_Count为0且Span为空()
        {
            var buf = new BufferEx.SlidingBuffer();

            Assert.Equal(0, buf.Count);
            Assert.True(buf.Span.IsEmpty);
            Assert.Empty(buf.CopyFrame(0, 0));
        }

        [Fact]
        public void Append_累加Count并保持顺序()
        {
            var buf = new BufferEx.SlidingBuffer(64);

            buf.Append(new byte[] { 1, 2, 3 });
            Assert.Equal(3, buf.Count);

            buf.Append(new byte[] { 4, 5 });
            Assert.Equal(5, buf.Count);

            Assert.Equal(new byte[] { 1, 2, 3, 4, 5 }, buf.CopyFrame(0, 5));
        }

        [Fact]
        public void Append_空数据是noop()
        {
            var buf = new BufferEx.SlidingBuffer(64);
            buf.Append(new byte[] { 1, 2 });

            buf.Append(ReadOnlySpan<byte>.Empty);

            Assert.Equal(2, buf.Count);
            Assert.Equal(new byte[] { 1, 2 }, buf.CopyFrame(0, 2));
        }

        [Fact]
        public void Span暴露当前有效区间()
        {
            var buf = new BufferEx.SlidingBuffer(64);
            buf.Append(new byte[] { 1, 2, 3 });
            buf.Consume(1);

            Assert.Equal(2, buf.Count);
            Assert.Equal(new byte[] { 2, 3 }, buf.Span.ToArray());
        }

        // ══════════════════════════════════════════════════════════════════
        // Consume
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void Consume_丢弃前N字节()
        {
            var buf = new BufferEx.SlidingBuffer(64);
            buf.Append(new byte[] { 1, 2, 3, 4, 5 });

            buf.Consume(2);

            Assert.Equal(3, buf.Count);
            Assert.Equal(new byte[] { 3, 4, 5 }, buf.CopyFrame(0, 3));
        }

        [Fact]
        public void Consume_超出Count时按Count截断_不抛()
        {
            var buf = new BufferEx.SlidingBuffer(64);
            buf.Append(new byte[] { 1, 2, 3 });

            buf.Consume(100);

            Assert.Equal(0, buf.Count);
            Assert.True(buf.Span.IsEmpty);
        }

        [Fact]
        public void Consume_零或负数直接返回()
        {
            var buf = new BufferEx.SlidingBuffer(64);
            buf.Append(new byte[] { 1, 2, 3 });

            buf.Consume(0);
            buf.Consume(-5);

            Assert.Equal(3, buf.Count);
            Assert.Equal(new byte[] { 1, 2, 3 }, buf.CopyFrame(0, 3));
        }

        [Fact]
        public void Consume_全部消费后还能继续追加()
        {
            var buf = new BufferEx.SlidingBuffer(64);
            buf.Append(new byte[] { 1, 2, 3 });
            buf.Consume(3);

            buf.Append(new byte[] { 9 });

            Assert.Equal(1, buf.Count);
            Assert.Equal(new byte[] { 9 }, buf.CopyFrame(0, 1));
        }

        // ══════════════════════════════════════════════════════════════════
        // CompactIfNeeded —— 只在"已消费超过缓冲区一半"时才平移
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void 消费超过一半时触发平移_剩余数据仍正确()
        {
            // 容量 16；写入 0..15；消费 9 ⇒ mRead(9) > 16/2 ⇒ 触发 Array.Copy 平移
            var buf = new BufferEx.SlidingBuffer(16);
            var all = new byte[16];
            for (int i = 0; i < 16; i++) all[i] = (byte)i;
            buf.Append(all);

            buf.Consume(9);

            Assert.Equal(7, buf.Count);
            Assert.Equal(new byte[] { 9, 10, 11, 12, 13, 14, 15 }, buf.CopyFrame(0, 7));
        }

        [Fact]
        public void 消费不超过一半时不平移_但数据依然正确()
        {
            // 容量 16；消费 8 ⇒ mRead(8) 不 > 8 ⇒ 不平移（只是 mRead 前移）
            var buf = new BufferEx.SlidingBuffer(16);
            var all = new byte[16];
            for (int i = 0; i < 16; i++) all[i] = (byte)i;
            buf.Append(all);

            buf.Consume(8);

            Assert.Equal(8, buf.Count);
            Assert.Equal(new byte[] { 8, 9, 10, 11, 12, 13, 14, 15 }, buf.CopyFrame(0, 8));
        }

        [Fact]
        public void 多轮追加消费_内容顺序始终不乱()
        {
            // 这个用例专门压"平移 + 扩容"交叉路径：50 轮 × (追加5 + 消费3)
            var buf = new BufferEx.SlidingBuffer(16);
            var expected = new System.Collections.Generic.Queue<byte>();

            for (int round = 0; round < 50; round++)
            {
                for (int i = 0; i < 5; i++)
                {
                    byte v = (byte)(round * 5 + i);
                    buf.Append(new byte[] { v });
                    expected.Enqueue(v);
                }

                var head = buf.CopyFrame(0, 3);
                for (int i = 0; i < 3; i++)
                    Assert.Equal(expected.Dequeue(), head[i]);

                buf.Consume(3);
            }

            Assert.Equal(expected.Count, buf.Count);
            Assert.Equal(expected.ToArray(), buf.CopyFrame(0, buf.Count));
        }

        // ══════════════════════════════════════════════════════════════════
        // EnsureCapacity —— 容量下限 16 + 翻倍扩容
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void 容量下限是16_传更小的值会被抬高()
        {
            // 构造里是 Math.Max(16, capacity)；容量 1 实际得到 16
            var buf = new BufferEx.SlidingBuffer(1);

            buf.Append(new byte[16]);           // 16 字节刚好装下

            Assert.Equal(16, buf.Count);
            Assert.Equal(new byte[16], buf.CopyFrame(0, 16));
        }

        [Fact]
        public void 超出容量时自动翻倍扩容_数据不丢()
        {
            var buf = new BufferEx.SlidingBuffer(16);
            var first = new byte[16];
            for (int i = 0; i < 16; i++) first[i] = (byte)(i + 1);
            buf.Append(first);

            buf.Append(new byte[] { 0xAB });    // 第 17 字节 ⇒ 触发扩容

            Assert.Equal(17, buf.Count);

            var frame = buf.CopyFrame(0, 17);
            Assert.Equal((byte)16, frame[15]);
            Assert.Equal((byte)0xAB, frame[16]);
        }

        [Fact]
        public void 扩容后原有数据全部保留()
        {
            var buf = new BufferEx.SlidingBuffer(16);
            var all = new byte[16];
            for (int i = 0; i < 16; i++) all[i] = (byte)(i + 1);
            buf.Append(all);

            buf.Append(new byte[64]);           // 触发扩容

            Assert.Equal(80, buf.Count);
            Assert.Equal(all, buf.CopyFrame(0, 16));
            Assert.Equal(new byte[64], buf.CopyFrame(16, 64));
        }

        [Fact]
        public void 先消费再追加_不会因为mRead残留而写错位置()
        {
            // 关键交叉：mRead > 0（未触发平移）时继续 Append
            var buf = new BufferEx.SlidingBuffer(16);
            buf.Append(new byte[] { 1, 2, 3, 4 });
            buf.Consume(2);                     // mRead=2，2 不 > 8 ⇒ 不平移

            buf.Append(new byte[] { 5, 6 });

            Assert.Equal(4, buf.Count);
            Assert.Equal(new byte[] { 3, 4, 5, 6 }, buf.CopyFrame(0, 4));
        }

        // ══════════════════════════════════════════════════════════════════
        // CopyFrame 边界
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void CopyFrame_可截取中间片段()
        {
            var buf = new BufferEx.SlidingBuffer(64);
            buf.Append(new byte[] { 1, 2, 3, 4, 5 });

            Assert.Equal(new byte[] { 2, 3 }, buf.CopyFrame(1, 2));
            Assert.Equal(new byte[] { 5 }, buf.CopyFrame(4, 1));
            Assert.Empty(buf.CopyFrame(5, 0));      // 恰好落在末尾是合法的
            Assert.Empty(buf.CopyFrame(0, 0));
        }

        [Fact]
        public void CopyFrame_越界抛ArgumentOutOfRangeException()
        {
            var buf = new BufferEx.SlidingBuffer(64);
            buf.Append(new byte[] { 1, 2, 3 });

            Assert.Throws<ArgumentOutOfRangeException>(() => buf.CopyFrame(-1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => buf.CopyFrame(0, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => buf.CopyFrame(0, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => buf.CopyFrame(3, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => buf.CopyFrame(2, 2));
        }

        [Fact]
        public void CopyFrame_返回的是新数组_修改它不影响缓冲()
        {
            var buf = new BufferEx.SlidingBuffer(64);
            buf.Append(new byte[] { 1, 2, 3 });

            var frame = buf.CopyFrame(0, 3);
            frame[0] = 0xFF;

            Assert.Equal(new byte[] { 1, 2, 3 }, buf.CopyFrame(0, 3));
        }

        // ══════════════════════════════════════════════════════════════════
        // Clear
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void Clear_重置计数但不清零底层数组()
        {
            var buf = new BufferEx.SlidingBuffer(64);
            buf.Append(new byte[] { 1, 2, 3 });

            buf.Clear();

            Assert.Equal(0, buf.Count);
            Assert.True(buf.Span.IsEmpty);

            // 只是索引归零，底层数组没被抹掉 —— 旧数据会留在后面新写入的内容里被"覆盖"
            buf.Append(new byte[] { 9 });
            Assert.Equal(new byte[] { 9 }, buf.CopyFrame(0, 1));
        }

        [Fact]
        public void Clear后可以重新增长到原容量之外()
        {
            var buf = new BufferEx.SlidingBuffer(16);
            buf.Append(new byte[16]);
            buf.Clear();

            buf.Append(new byte[32]);

            Assert.Equal(32, buf.Count);
        }

        // ══════════════════════════════════════════════════════════════════
        // API 面
        // ══════════════════════════════════════════════════════════════════

        [Fact]
        public void API面_BufferEx是非静态类且只有SlidingBuffer一个嵌套类型()
        {
            // ⚠️ 与 ReflectionEx 一样：全仓其他 *Ex 都是 static class，BufferEx 却是普通实例类
            //    （且自身零实例成员，纯做命名空间容器）。
            Assert.False(typeof(BufferEx).IsAbstract && typeof(BufferEx).IsSealed);

            var nested = typeof(BufferEx).GetNestedTypes();
            Assert.Single(nested);
            Assert.Equal("SlidingBuffer", nested[0].Name);
            Assert.True(nested[0].IsSealed);      // sealed class
        }

        [Fact]
        public void API面_SlidingBuffer的公开成员面()
        {
            var t = typeof(BufferEx.SlidingBuffer);
            const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Public
                                                       | System.Reflection.BindingFlags.Instance
                                                       | System.Reflection.BindingFlags.DeclaredOnly;

            Assert.Equal(
                new[] { "Append", "Clear", "Consume", "CopyFrame", "get_Count", "get_Span" },
                t.GetMethods(Flags).Select(m => m.Name).OrderBy(n => n).ToArray());

            Assert.Equal(
                new[] { "Count", "Span" },
                t.GetProperties(Flags).Select(p => p.Name).OrderBy(n => n).ToArray());

            // 公开字段一个都没有（没有把 mRead/mWrite 暴露出去）
            Assert.Empty(t.GetFields(Flags));
        }
    }
}
