// 【中文阅读说明】
// Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
// 本文件定义：SafeMemoryHandle。成员说明可结合下方具体实现阅读。
// 直接导入的协作命名空间包括：System、System.Runtime.InteropServices、Microsoft.Win32.SafeHandles。导入不代表所有类型都会在运行时加载。
using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Extension.Platform.Memory
{
    /// <summary>
    /// SafeMemoryHandle 的类型定义。Extension 模块：共享基础能力与集成适配；Extension.Core 保持轻量，WPF 和厂商 SDK 集成归其他部分。
    /// 继承或实现的契约：SafeHandleZeroOrMinusOneIsInvalid。具体约束按这些契约及各成员实现解释。
    /// </summary>
    public sealed class SafeMemoryHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        public int Size { get; }

        /// <summary>
        /// 初始化 SafeMemoryHandle 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// </summary>
        private SafeMemoryHandle() : base(true)
        {
            Size = 0;
        }

        /// <summary>
        /// 初始化 SafeMemoryHandle 实例及其协作对象；保持下方赋值、订阅和初始化调用的顺序。
        /// 正文中的关键协作调用包括 SetHandle、AllocHGlobal；具体条件和先后次序见方法体。
        /// </summary>
        private SafeMemoryHandle(int size) : base(true)
        {
            Size = size;
            SetHandle(Marshal.AllocHGlobal(size));
        }

        /// <summary>
        /// 处理当前类型提供的操作；对应入口为 ReleaseHandle。
        /// 正文中的关键协作调用包括 FreeHGlobal；具体条件和先后次序见方法体。
        /// </summary>
        protected override bool ReleaseHandle()
        {
            if (!IsInvalid)
            {
                Marshal.FreeHGlobal(handle);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 创建当前实现提供的对象或结果；对应入口为 CreateFromBytes。
        /// 正文中的关键协作调用包括 Copy；具体条件和先后次序见方法体。
        /// </summary>
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
