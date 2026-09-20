using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Extension.Platform.Memory
{
    public sealed class SafeMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public int Size { get; }

        private SafeMemoryHandle() : base(true)
        {
            Size = 0;
        }

        private SafeMemoryHandle(int size) : base(true)
        {
            Size = size;
            SetHandle(Marshal.AllocHGlobal(size));
        }

        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                Marshal.FreeHGlobal(handle);
                return true;
            }
            return false;
        }

        public static SafeMemoryHandle CreateFromBytes(byte[] data)
        {
            var handle = new SafeMemoryHandle(data.Length);
            Marshal.Copy(data, 0, handle.handle, data.Length);
            return handle;
        }

        /// <summary>
        /// 获取非托管内存的指针。
        /// </summary>
        /// <returns>非托管内存的起始地址。</returns>
        /// <exception cref="ObjectDisposedException">本句柄已被 <see cref="IDisposable.Dispose"/> 释放。</exception>
        /// <remarks>
        /// ⛔ 序 28（2026-09-18 修）：判据原来是**只有** <see cref="SafeHandle.IsInvalid"/>。
        /// <para><see cref="SafeHandle.IsInvalid"/> 读的是句柄**值**（`SafeHandleZeroOrMinusOneIsInvalid`
        /// 下即"等于 0 或 -1"），而 <c>Dispose()</c> **并不把内部 <c>handle</c> 字段清零** ——
        /// 实测释放后 <c>handle</c> 仍是释放前那个非零地址 ⇒ <c>IsInvalid</c> 返回 <c>false</c>，
        /// 这道检查失效，调用方拿到的是**悬垂指针**。</para>
        /// <para>后果：对它做 <c>Marshal.Read*</c>/<c>Write*</c> 就是 use-after-free ——
        /// 轻则读到垃圾数据，重则 <see cref="AccessViolationException"/> **直接崩进程**（catch 不住，
        /// 因为它是 <c>CorruptedStateException</c> 一类，.NET Core 起不再交回托管代码）。</para>
        /// <para>正确判据是 <see cref="SafeHandle.IsClosed"/>（释放后为 <c>true</c>）。
        /// 这里用 <c>||</c> 保留 <c>IsInvalid</c>：它额外覆盖"从未分配成功"（<c>AllocHGlobal</c> 返回 0）
        /// 这种既未释放、也无有效指针的情形。</para>
        /// <para>⚠️ <c>ReleaseHandle()</c> 里的 <c>IsInvalid</c> **不需要**同步改：那个方法是 CLR
        /// 在释放时**至多调用一次**的，执行到它时 <c>IsClosed</c> 必然还是 <c>false</c>；
        /// 那里用 <c>IsInvalid</c> 表达的语义是"当初就没分配成功 ⇒ 释放失败（返回 false）"，是正确的。</para>
        /// </remarks>
        public IntPtr GetPointer()
        {
            if (IsInvalid || IsClosed)
                throw new ObjectDisposedException("SafeMemoryHandle");
            return handle;
        }
    }
}
