using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Extension.Core.IntPtrEx
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

        public IntPtr GetPointer()
        {
            if (IsInvalid)
                throw new ObjectDisposedException("SafeMemoryHandle");
            return handle;
        }
    }
}
