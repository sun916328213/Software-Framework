using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Extension.Core.IntPtrEx
{
    public class DynamicDllLoaderEx : IDisposable
    {
        private IntPtr _moduleHandle;
        private readonly string _dllPath;
        private readonly object _lockObj = new object();

        public DynamicDllLoaderEx(string dllPath)
        {
            try
            {
                lock (_lockObj)
                {
                    if (string.IsNullOrEmpty(dllPath))
                        throw new ArgumentNullException(nameof(dllPath));

                    _dllPath = dllPath;
                    _moduleHandle = LoadLibrary(_dllPath);

                    if (_moduleHandle == IntPtr.Zero)
                    {
                        int errorCode = Marshal.GetLastWin32Error();
                        throw new DllNotFoundException($"Failed to load DLL '{_dllPath}'. Error code: {errorCode}");
                    }
                }
            }
            catch
            {

            }
        }

        public TDelegate GetFunction<TDelegate>(string functionName) where TDelegate : Delegate
        {
            try
            {
                IntPtr procAddress = GetProcAddress(_moduleHandle, functionName);
                if (procAddress == IntPtr.Zero)
                {
                    throw new EntryPointNotFoundException($"Function '{functionName}' not found in {_dllPath}");
                }
                return Marshal.GetDelegateForFunctionPointer<TDelegate>(procAddress);
            }
            catch
            {
            }
            return Marshal.GetDelegateForFunctionPointer<TDelegate>(IntPtr.Zero);
        }

        public void Dispose()
        {
            lock (_lockObj)
            {
                if (_moduleHandle == IntPtr.Zero) return;

                var freeThread = new Thread(() =>
                {
                    if (!FreeLibrary(_moduleHandle))
                    {

                    }
                });

                freeThread.Start();
                if (!freeThread.Join(2000))
                {
                    freeThread.Abort();
                }
                _moduleHandle = IntPtr.Zero;
                GC.SuppressFinalize(this);
            }
        }

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Ansi)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        [DllImport("kernel32", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        ~DynamicDllLoaderEx() => Dispose();
    }
}
