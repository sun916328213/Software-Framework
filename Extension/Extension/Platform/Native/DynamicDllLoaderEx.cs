using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Extension.Platform.Native
{
    public class DynamicDllLoaderEx : IDisposable
    {
        private IntPtr _moduleHandle;
        private readonly string _dllPath;
        private readonly object _lockObj = new object();

        /// <summary>
        /// 加载指定的 native DLL。
        /// </summary>
        /// <exception cref="ArgumentNullException"><paramref name="dllPath"/> 为空。</exception>
        /// <exception cref="DllNotFoundException">DLL 加载失败（详见异常的 Win32 错误码）。</exception>
        /// <remarks>
        /// 这里刻意不吞异常：加载失败时对象处于不可用状态，若静默继续，
        /// 后续 <see cref="GetFunction{TDelegate}"/> 会拿到无效句柄并在调用时崩溃，无法定位根因。
        /// </remarks>
        public DynamicDllLoaderEx(string dllPath)
        {
            if (string.IsNullOrEmpty(dllPath))
                throw new ArgumentNullException(nameof(dllPath));

            lock (_lockObj)
            {
                _dllPath = dllPath;
                _moduleHandle = LoadLibrary(_dllPath);

                if (_moduleHandle == IntPtr.Zero)
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw new DllNotFoundException(
                        $"Failed to load DLL '{_dllPath}'. Win32 error code: {errorCode}");
                }
            }
        }

        /// <summary>
        /// 获取导出函数委托。
        /// </summary>
        /// <exception cref="ObjectDisposedException">DLL 已释放。</exception>
        /// <exception cref="EntryPointNotFoundException">未找到指定导出函数。</exception>
        public TDelegate GetFunction<TDelegate>(string functionName) where TDelegate : Delegate
        {
            lock (_lockObj)
            {
                if (_moduleHandle == IntPtr.Zero)
                    throw new ObjectDisposedException(nameof(DynamicDllLoaderEx),
                        $"Native DLL '{_dllPath}' 已释放或未成功加载。");

                IntPtr procAddress = GetProcAddress(_moduleHandle, functionName);
                if (procAddress == IntPtr.Zero)
                {
                    throw new EntryPointNotFoundException(
                        $"Function '{functionName}' not found in '{_dllPath}'. Win32 error code: {Marshal.GetLastWin32Error()}");
                }

                return Marshal.GetDelegateForFunctionPointer<TDelegate>(procAddress);
            }
        }

        public void Dispose()
        {
            lock (_lockObj)
            {
                if (_moduleHandle == IntPtr.Zero) return;

                var handle = _moduleHandle;
                _moduleHandle = IntPtr.Zero;

                // 在独立线程卸载，避免个别 native 库卸载阻塞调用线程。
                // 注意：.NET Core / .NET 5+ 已移除 Thread.Abort（调用会抛 PlatformNotSupportedException），
                // 因此超时后只能放弃等待，线程会自行结束；标记 IsBackground 以免阻塞进程退出。
                var freeThread = new Thread(() =>
                {
                    try
                    {
                        if (!FreeLibrary(handle))
                        {
                            Debug.WriteLine(
                                $"[DynamicDllLoaderEx] FreeLibrary 返回 false，DLL: {_dllPath}，Win32 错误码: {Marshal.GetLastWin32Error()}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[DynamicDllLoaderEx] FreeLibrary 异常，DLL: {_dllPath}，{ex.Message}");
                    }
                })
                {
                    IsBackground = true,
                    Name = "DynamicDllLoaderEx.FreeLibrary"
                };

                freeThread.Start();
                freeThread.Join(2000);

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
