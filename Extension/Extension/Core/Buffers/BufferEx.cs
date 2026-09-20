using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
 

namespace Extension.Core.Buffers
{
    public class BufferEx
    {
        /// <summary>
        /// 平移缓冲
        /// 高性能的数据存储，用于减少数据复制此时
        /// </summary>
        public sealed class SlidingBuffer
        {
            private byte[] mBuffer;
            private int mRead;   // 已消费起点
            private int mWrite;  // 已写入末尾

            public SlidingBuffer(int capacity = 2048)
            {
                mBuffer = new byte[Math.Max(16, capacity)];
            }

            public int Count => mWrite - mRead;

            /// <summary>
            /// 追加数据
            /// </summary>
            /// <param name="data"></param>
            public void Append(ReadOnlySpan<byte> data)
            {
                EnsureCapacity(mWrite + data.Length);
                data.CopyTo(mBuffer.AsSpan(mWrite));
                mWrite += data.Length;
            }

            /// <summary>
            /// 当前有效区间视图
            /// </summary>
            public ReadOnlySpan<byte> Span => mBuffer.AsSpan(mRead, mWrite - mRead);

            /// <summary>
            /// 丢弃前 N 字节
            /// </summary>
            /// <param name="count"></param>
            public void Consume(int count)
            {
                if (count <= 0) return;
                if (count > Count) count = Count;

                mRead += count;
                CompactIfNeeded();
            }

            /// <summary>
            /// 拷贝部分数据
            /// </summary>
            /// <param name="start"></param>
            /// <param name="length"></param>
            /// <returns></returns>
            /// <exception cref="ArgumentOutOfRangeException"></exception>
            public byte[] CopyFrame(int start, int length)
            {
                if (start < 0 || length < 0 || start + length > Count)
                    throw new ArgumentOutOfRangeException();

                var dst = new byte[length];
                Span.Slice(start, length).CopyTo(dst);
                return dst;
            }

            /// <summary>
            /// 将有效数据平移到数组起点
            /// </summary>
            private void CompactIfNeeded()
            {
                if (mRead == 0) return;

                // 当已消费超过一半，做一次平移
                if (mRead > mBuffer.Length / 2)
                {
                    int remain = mWrite - mRead;
                    Array.Copy(mBuffer, mRead, mBuffer, 0, remain);
                    mRead = 0;
                    mWrite = remain;
                }
            }

            /// <summary>
            /// 自动扩容机制
            /// </summary>
            /// <param name="required"></param>
            private void EnsureCapacity(int required)
            {
                if (required <= mBuffer.Length) return;

                int newSize = mBuffer.Length * 2;
                while (newSize < required) newSize *= 2;
                Array.Resize(ref mBuffer, newSize);
            }

            /// <summary>
            /// 清空内存
            /// </summary>
            public void Clear()
            {
                mRead = 0;
                mWrite = 0;
            }
        }
    }
}
